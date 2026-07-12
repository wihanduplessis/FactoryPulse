# ADR-0015: Harden the authentication endpoints before public exposure

- **Status:** Accepted
- **Date:** 2026-07-12
- **Deciders:** Project owner

## Context

Until now FactoryPulse has only ever run on `localhost` and inside a private
Compose network. v0.7 puts it on a public HTTPS URL, on a CV, with a **seeded admin
account whose email is predictable**.

In that setting the current `POST /api/auth/login` is a liability:

- `AuthService.LoginAsync` called `UserManager.CheckPasswordAsync`, which verifies a
  password and **nothing else** — it does not count failures and never locks anything.
  ASP.NET Core Identity ships a complete lockout mechanism; we simply had not switched
  it on.
- There was no request throttling of any kind. A single client could attempt passwords
  as fast as the server could hash them.

This is not a theoretical exposure. It is the first thing an attacker — or an
interviewer — would poke at.

## Decision

Two **independent** layers, added before the app is publicly reachable.

### 1. Account lockout (per account)

Identity's lockout, enabled in `AddIdentityCore`:

- `MaxFailedAccessAttempts = 5`
- `DefaultLockoutTimeSpan = 5 minutes`
- `AllowedForNewUsers = true`

`LoginAsync` now checks `IsLockedOutAsync` **before** verifying the password, calls
`AccessFailedAsync` on a bad password, **re-checks the lockout immediately after** (so
the attempt that *trips* the lock reports the lock, rather than reporting "invalid
credentials" and confusing the caller), and calls `ResetAccessFailedCountAsync` on
success.

A new `Errors.Auth.AccountLocked` (an `Error.Unauthorized`) flows through the existing
`Result` → `ApiController.HandleFailure` path and surfaces as **401** with no
controller change. No migration was needed: `AccessFailedCount`, `LockoutEnd` and
`LockoutEnabled` already exist on `AspNetUsers`.

**A time-limited lockout, not a permanent one.** Permanent lockout would convert this
defence into a denial-of-service *against our own users*: anyone could disable any
account by typing rubbish at it. Five minutes reduces an attacker to five guesses per
five minutes — hopeless — while a legitimate user who fumbled their password waits
minutes, not days.

### 2. Rate limiting (per caller)

ASP.NET Core's built-in rate limiter (no new package), as a **named policy** applied
**only** to `POST /api/auth/login`:

- Fixed window, **5 requests per 60 seconds**, `QueueLimit = 0`.
- **Partitioned by client IP** — one caller's flood must not lock out everybody else.
- `OnRejected` writes a `ProblemDetails` body, so a 429 looks like every other error
  the API returns.
- Limits come from configuration (`RateLimiting:Login:*`), matching the existing
  `UseHttpsRedirection` / `ApplyMigrationsOnStartup` seams.
- `app.UseRateLimiter()` sits **before** `UseAuthentication()`, so a flood is rejected
  before anything expensive — notably password hashing — is paid for.

**Both layers, not one.** Lockout alone permits credential *spraying* (one guess each
against a thousand accounts, tripping no single account's counter). Rate limiting
alone permits a slow, patient grind against one account. They close different doors.

### 3. Forwarded headers (a consequence, not an extra)

Behind a reverse proxy — Container Apps ingress in v0.7 — `RemoteIpAddress` is the
*proxy's* address, which would collapse every user on earth into a single rate-limit
partition and make the limiter worse than useless. `ForwardedHeaders` middleware
(config-gated by `UseForwardedHeaders`, off by default) restores the true client IP
from `X-Forwarded-For`, and runs **first** in the pipeline.

`ForwardLimit = 1` is a security control, not a default we inherited: the ingress
*appends* the address it actually observed, so the genuine client IP is the **last**
entry. Reading only that entry means a client that spoofs its own `X-Forwarded-For`
merely prepends a value we ignore. Raising the limit would hand attackers a
rate-limit bypass.

## Alternatives considered

- **`SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)`** — would
  handle lockout in one call, but pulls `SignInManager` (and its `IHttpContextAccessor`
  / scheme-provider dependencies) into a project that deliberately uses
  `AddIdentityCore`. The explicit `UserManager` sequence is a few more lines and makes
  the policy visible in our own code rather than implied by a flag.
- **Global rate limiting on every endpoint** — rejected. The threat is on the endpoint
  that hands out credentials. Throttling reads would degrade the API for no security
  gain.
- **Permanent lockout / CAPTCHA / IP banning** — disproportionate for a portfolio API,
  and permanent lockout is actively harmful (see above).
- **Deferring this to after deployment** — rejected. The whole point is that it must be
  true *before* the URL is public, not after.

## Consequences

### Positive

- Brute-forcing the seeded admin is no longer viable: five guesses per five minutes
  per account, and 5 requests per minute per IP.
- Hostile traffic is rejected before it costs a password hash.
- 429 responses are `ProblemDetails`, consistent with the rest of the API.
- The `X-Forwarded-For` handling that v0.7 needed anyway landed here, correctly, with
  its security caveat documented.
- Covered by tests: 4 unit tests on the lockout sequence (including the exact moment
  the lock trips, via a consecutive-return stub) and 2 integration tests — one proving
  a locked account is refused **even with the correct password**, one proving a flood
  returns 429.

### Negative / trade-offs

- A user who forgets their password waits 5 minutes. Acceptable; there is no password
  reset flow in this project.
- The in-memory rate limiter is **per instance**. If the app ever scales beyond one
  replica, each replica enforces its own budget. Fine at single-replica (v0.7 runs
  single-revision, scale-to-zero); a distributed limiter would be needed at scale.
- Trusting `X-Forwarded-For` is only safe *because* the app sits behind an ingress that
  appends it. If FactoryPulse were ever exposed directly, `UseForwardedHeaders` must
  stay **off** — hence the config gate and the default of `false`.

### Testing note

The first attempt at the integration tests failed in an instructive way: all tests
shared one rate-limit partition (under `WebApplicationFactory` there is no remote IP,
so everything keyed to `"unknown"`), and the flood test starved three unrelated tests
of their budget. Shrinking the window only made the collision *less likely* — a flaky
CI test in waiting. Partitioning properly, via the same forwarded-headers mechanism
production needs, fixed it structurally: each stress test presents its own
`X-Forwarded-For` and gets its own bucket.
