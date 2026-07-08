# Design: Authentication & Authorization (v0.4)

Design-first specification for auth. Locks decisions, contracts, the authorization
matrix, and per-issue acceptance criteria before any code is written.

> Status: Proposed &bull; Target: v0.4 &bull; Depends on: v0.3 (Production Orders)

---

## 1. Goals & what's new

Secure the API with real, recognizable auth:

- **ASP.NET Core Identity** — user store + password hashing (never roll your own).
- **JWT bearer tokens** — stateless authentication.
- **Role-based authorization** + **policies** — protect endpoints by role.
- **Refresh tokens** — optional, sequenced last.

New concepts vs. everything so far: Identity integration, token issuance/validation,
the `[Authorize]` pipeline, claims, and keeping all of this out of the pure Domain.

---

## 2. Locked decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | **ASP.NET Core Identity** over EF, storing users in `FactoryPulseDb` | Standard, secure, recognized; no custom password handling. |
| D2 | **JWT bearer** with a **symmetric (HMAC-SHA256)** signing key | Simplest correct choice for a single monolith; key in user-secrets. |
| D3 | Identity lives in **Infrastructure**; the Domain stays Identity-free | `ApplicationUser` depends on `Microsoft.AspNetCore.Identity`, which must not leak into the pure Domain. |
| D4 | `FactoryPulseDbContext` **inherits `IdentityDbContext<ApplicationUser>`** | One database, one migration path. (Alternative — a separate identity context — considered and rejected for simplicity.) |
| D5 | Auth logic behind an **`IAuthService`** (Application) implemented in Infrastructure | Keeps Application/controllers free of `UserManager`/`SignInManager`; auth returns `Result<T>` like everything else. |
| D6 | **Three roles**: `Admin`, `Manager`, `Viewer` | Clear, minimal authorization tiers (see §4). |
| D7 | Roles + a seeded **admin user** are seeded on startup | So the API is usable immediately; avoids a chicken-and-egg "who creates the first admin". |
| D8 | **Admin-only registration** — only an `Admin` can create users/assign roles | Realistic for an internal factory tool; the seeded admin bootstraps everyone else. |
| D9 | **Core auth first, refresh tokens last** (Issue 7, optional) | Ship a working secured API sooner; add refresh-token rotation afterward. |

---

## 3. Identity storage & the user

- **`ApplicationUser : IdentityUser`** (Infrastructure) — GUID string key by default; add `FullName` (and later profile fields). Not a Domain entity.
- **`FactoryPulseDbContext : IdentityDbContext<ApplicationUser>`** — adds the ASP.NET Identity tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, …) alongside `Machine`/`ProductionOrder`. `OnModelCreating` must call `base.OnModelCreating(...)` first (Identity configures its own model).
- One EF migration adds all Identity tables.

## 4. Roles & authorization matrix

| Role | Intent |
|------|--------|
| **Admin** | Everything, incl. user management. |
| **Manager** | Manage machines & orders (create/update/delete, drive order lifecycle). |
| **Viewer** | Read-only. |

| Endpoint group | Anonymous | Viewer | Manager | Admin |
|---|:---:|:---:|:---:|:---:|
| `POST /api/auth/login`, `/refresh` | ✅ | ✅ | ✅ | ✅ |
| `POST /api/auth/register`, role assignment | | | | ✅ |
| `GET` machines / orders | | ✅ | ✅ | ✅ |
| `POST/PUT/DELETE` machines | | | ✅ | ✅ |
| `POST/PUT/DELETE` orders + `start/complete/cancel` | | | ✅ | ✅ |

Implemented with `[Authorize(Roles = "...")]` (or named policies). A default
`[Authorize]` on `ApiController` makes endpoints **secure by default**; read
endpoints allow any authenticated role, writes require `Manager`/`Admin`.

## 5. JWT design

- **Claims:** `sub` (user id), `email`, `name`, `role` (one per role), `jti`, `iat`, `exp`, `iss`, `aud`.
- **Lifetime:** short access token (e.g. **15–60 min**).
- **Signing:** HMAC-SHA256 with a secret key from configuration/user-secrets.
- **Validation:** `AddAuthentication().AddJwtBearer(...)` validates issuer, audience,
  lifetime, and signing key.
- **Settings** bound from config into a `JwtSettings` options class:
  `Issuer`, `Audience`, `Key`, `AccessTokenMinutes`.

## 6. Refresh tokens (optional, last issue)

If included:
- A `RefreshToken` entity (hashed token, user id, expiry, revoked flag) stored in the DB.
- `POST /api/auth/refresh` — validates the refresh token, issues a new access token,
  **rotates** the refresh token (old one revoked).
- Longer lifetime (e.g. 7 days) than the access token.

## 7. Contracts

**Application (interfaces + DTOs + errors):**
```
IAuthService
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct)   // if enabled

IJwtTokenGenerator
    string GenerateAccessToken(<user id>, <email>, <roles>)

RegisterRequest { Email, Password, FullName, Role }
LoginRequest    { Email, Password }
AuthResponse    { AccessToken, ExpiresAtUtc, Email, Roles }

Errors.Auth.InvalidCredentials  (Validation/Unauthorized) → 401
Errors.Auth.EmailAlreadyExists  (Conflict)                → 409
Errors.Auth.RegistrationFailed  (Validation)              → 400
```

**Infrastructure (implementations):**
```
ApplicationUser : IdentityUser
AuthService : IAuthService            // uses UserManager / SignInManager + IJwtTokenGenerator
JwtTokenGenerator : IJwtTokenGenerator
```

**Note:** `Unauthorized` (401) is a new `ErrorType` — add it and map it in
`ApiController.HandleFailure`.

## 8. Architecture placement

```
Domain          — unchanged (stays Identity-free)
Application      — IAuthService, IJwtTokenGenerator, auth DTOs, Errors.Auth
Infrastructure   — ApplicationUser, AuthService, JwtTokenGenerator, Identity config, seeding
API              — AuthController, JWT middleware wiring, [Authorize] on controllers
```
Auth returns `Result<T>` and controllers use the same `Match` → HTTP pattern —
consistent with the rest of the app.

## 9. API endpoints

| Method | Route | Auth | Body | Success |
|---|---|---|---|---|
| POST | `/api/auth/login` | anonymous | `LoginRequest` | 200 `AuthResponse` |
| POST | `/api/auth/register` | Admin | `RegisterRequest` | 201 `AuthResponse` |
| POST | `/api/auth/refresh` | anonymous | `RefreshRequest` | 200 `AuthResponse` |
| — | existing machine/order endpoints | per §4 matrix | — | — |

Swagger must be configured to send the **`Authorization: Bearer <token>`** header
(add the security definition so the "Authorize" button appears).

## 10. Configuration & secrets

- `JwtSettings` in `appsettings.json` (Issuer, Audience, AccessTokenMinutes) —
  **the signing `Key` goes in user-secrets**, never committed.
- Seeded admin credentials (Development) via config/user-secrets.

## 11. Seeding

On startup (Development): ensure the three roles exist, and create a default
`Admin` user if none exists. Keeps the API immediately usable.

## 12. Persistence

- One EF migration adds the Identity tables (and `RefreshToken` if included).
- `dotnet ef migrations add AddIdentity` → `database update`.

## 13. Issue breakdown (milestone "v0.4 — Authentication")

| # | Issue | Acceptance |
|---|---|---|
| 1 | `feature/identity-setup` | packages added; `ApplicationUser`; `FactoryPulseDbContext : IdentityDbContext`; migration applied; Identity tables in DB |
| 2 | `feature/jwt-infrastructure` | `JwtSettings`, `IJwtTokenGenerator` + impl; unit-testable token generation |
| 3 | `feature/auth-service` | `IAuthService` + impl (register/login) returning `Result<AuthResponse>`; `Errors.Auth`; `Unauthorized` ErrorType + 401 mapping |
| 4 | `feature/auth-endpoints` | `AuthController` (login/register); JWT middleware wired; Swagger "Authorize" button |
| 5 | `feature/authorization` | roles seeded + admin user; `[Authorize]` per the §4 matrix; secure-by-default |
| 6 | `feature/auth-tests` | unit tests: token generation, auth service (invalid credentials, duplicate email, happy path) |
| 7 | `feature/refresh-tokens` *(optional)* | `RefreshToken` entity + rotation; `/refresh` endpoint |
| 8 | `feature/adr-authentication` | ADR-0011 (Identity + JWT), ADR-0012 (Unauthorized error type / auth in Infrastructure) |

Suggested order: 1 → 2 → 3 → 4 → 5 → 6 → (7) → 8.

## 14. Acceptance walkthrough (end of sprint)

1. `POST /api/auth/login` with the seeded admin → **200** + a JWT.
2. `GET /api/machines` without a token → **401**.
3. Same `GET` with the token → **200**.
4. `POST /api/machines` as `Viewer` → **403**; as `Manager`/`Admin` → **201**.
5. `POST /api/auth/register` as non-admin → **403**; as admin → **201**.
6. Decode the JWT (jwt.io) → correct `sub`, `email`, `role`, `exp` claims.

## 15. New ADRs

- **ADR-0011** — Authentication via ASP.NET Core Identity + JWT bearer with role-based authorization.
- **ADR-0012** — Identity kept in Infrastructure (Domain stays pure); auth surfaced through `IAuthService` returning `Result`; new `Unauthorized` error type.
