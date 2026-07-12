# ADR-0019: Migrations applied by the pipeline, not on application startup

- **Status:** Accepted
- **Date:** 2026-07-12
- **Deciders:** Project owner
- **Delivers the follow-up recorded in:** [ADR-0013](0013-containerize-the-api.md)

## Context

Since v0.5 the application has called `Database.MigrateAsync()` on startup, behind an
`ApplyMigrationsOnStartup` flag. ADR-0013 already flagged this as unsuitable for
production. In Azure it stops being a matter of taste:

- **Two replicas would race** to migrate the same database.
- The application would need **schema-altering rights permanently**, merely to serve
  HTTP requests.
- A failed migration takes down the **application** — as a crash loop — rather than
  failing the **deployment**, which is where the failure belongs and where someone is
  watching.

And under ADR-0018 the application identity holds `db_datareader` + `db_datawriter`
and nothing more. **It cannot create a table.** Migrations had to move.

## Decision

**The build job generates an idempotent SQL script; the deploy job applies it before the
new image is rolled out.**

```
build ──► dotnet ef migrations script --idempotent ──► artifact
                                                          │
                                                          ▼
                                    migrate ──► apply to Azure SQL ──┐
                                                                     ├──► deploy image
                                            docker ──► push image ───┘
```

- `deploy` has `needs: [ docker, migrate ]`, so **new code never meets an old schema.**
- The script is applied by **`factorypulse-github-actions`**, the CI identity, which
  holds `db_owner` — and which exists only for the duration of a workflow run, not for
  the lifetime of a serving process.
- **`ApplyMigrationsOnStartup` is `false` in Azure.** The flag added in v0.5 turned out
  to be exactly the seam required, with no code change.

### Seeding is split from migrating

The two used to share one flag. They are not the same thing:

- **Migrating is DDL.** The app has no right to it. → pipeline.
- **Seeding is DML** (`INSERT` into `AspNetRoles` / `AspNetUsers`). `db_datawriter`
  covers it. → may still run on startup.

Hence a second flag, `SeedIdentityOnStartup`. Both default to **`false`**, so the safe
behaviour is what you get by doing nothing — you must opt *in* to schema changes, not
remember to opt out.

### Why a script rather than `dotnet ef database update`

This was challenged in review on the grounds that `database update` is simpler. The
reasoning does not hold:

- **Both are the same size.** Both need `dotnet-ef` on the runner; both need to reach
  Azure SQL through the firewall — and *that* is where the actual complexity lives. The
  difference is one CLI flag.
- The script is generated in the **build** job (which already has the SDK and the
  source) and published as an **artifact**. The **deploy** job then needs only `sqlcmd`
  — no SDK, no checkout, no `DbContext`.
- **The SQL can be read before it runs against the database.** Every team that has been
  burned by a migration ends up wanting exactly this.
- `--idempotent` guards each migration with `IF NOT EXISTS`, so applying it on every
  merge is a no-op when there is nothing new.

Generating the script is also an **offline** operation — it touches no database — so no
credentials are anywhere near the build job. (It does boot the API host to locate the
`DbContext`, so placeholder values are supplied for the JWT key and connection string;
neither is used to connect to anything.)

## The serverless wrinkle

Azure SQL on the free serverless tier **auto-pauses when idle**, and a paused database
answers the first connection with *"database is not currently available"* rather than
waiting. A serverless database can only be resumed **by an attempted login** — there is
no CLI command for it.

So the migrate job **knocks, waits, then migrates**: a deliberately-failing
`continue-on-error` connection triggers the resume, then it polls
`az sql db show --query status` until `Online`, and only then applies the script.

The application faces the same problem, and the answer there is EF Core's
`EnableRetryOnFailure()` — which is not a workaround for the free tier but the correct
configuration for **any** application talking to Azure SQL. A cloud database throttles,
fails over, and pauses; transient failure is normal operation, not an exception.

## Alternatives considered

- **Keep migrating on startup.** Impossible under ADR-0018 — the app has no DDL rights.
- **`dotnet ef database update` in the pipeline.** Defensible; see above. Rejected on
  separation of concerns, not on complexity.
- **A separate migration container/job.** More moving parts than a `.sql` file and
  `sqlcmd`.

## Consequences

### Positive

- The application runs with the least privilege it can, permanently.
- A failed migration fails the **deployment** — visible, in the pipeline, before the new
  image is rolled out — rather than crash-looping a container at 2am.
- The migration SQL is a reviewable artifact of every build.
- Local Docker and the integration tests are unchanged: they still migrate on startup,
  where it is convenient and safe.

### Negative / trade-offs

- **The GitHub runner must reach Azure SQL.** GitHub-hosted runners are Azure VMs, so
  the `AllowAllWindowsAzureIps` firewall rule already admits them. That rule is broader
  than ideal and is accepted knowingly: the server has **no password to guess**, so
  reaching it buys an attacker nothing without an Entra identity that already holds a
  role.
- The pipeline has one more job, and one more failure mode.
- CI holds `db_owner` on the production database. It is scoped to a workflow run
  authenticated by OIDC, with no standing credential — but it is real, and worth naming.
