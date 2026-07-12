# Design: Azure deployment (v0.7)

Design-first note for the final milestone: take the container image CI already
builds, and run it in Azure — publicly reachable, backed by a managed database,
provisioned by code, deployable from a pull request, and **cheap enough to leave
running while job-hunting**.

> Status: Proposed &bull; Target: v0.7 &bull; Depends on: v0.5 (Containers), v0.6 (CI)

---

## 1. Goals

- The API is **live on the public internet** over HTTPS, with a URL that can go on
  a CV and be clicked by a recruiter.
- **Everything is provisioned by code** (Bicep) — reproducible, reviewable, and
  destroyable in one command.
- **CI/CD end to end**: merge to `main` → image pushed to ACR → deployed to Azure.
- **No secrets stored in GitHub** — OIDC federated credentials, not a
  service-principal password.
- **Known, bounded cost**, with a budget alert in place *before* the first resource
  exists, and a documented teardown.

---

## 2. The cost conversation (re-opened, as promised)

This is the first milestone that costs real money, so it goes first rather than
buried at the bottom.

### What each piece would cost

Prices are **approximate**, in USD, for **West Europe**, and they change — treat
this as the shape of the bill, not a quote.

| Resource | Option | Cost if left running | Notes |
|---|---|---|---|
| **Container registry** | ACR **Basic** | **~$5/mo** | Flat fee. The one unavoidable cost. Already decided (ADR-0014). |
| | ACR Standard | ~$20/mo | No reason to. Basic's 10 GB is plenty. |
| **Compute** | **Container Apps** (Consumption) | **~$0** | Monthly free grant (180k vCPU-s / 360k GiB-s / 2M requests). With **scale-to-zero**, an idle portfolio app consumes almost nothing. |
| | App Service **B1** | ~$13/mo | Always on, no free tier for Linux containers. |
| | App Service **F1** (free) | $0 | Does **not** support custom containers. Rules itself out. |
| **Database** | **Azure SQL free offer** (serverless GP) | **$0** | 100k vCore-seconds + 32 GB/month, free indefinitely. **One per subscription.** Auto-pauses when idle. |
| | Azure SQL **Basic** (DTU) | ~$5/mo | The fallback if the free offer is unavailable. |
| **Secrets** | **Key Vault** (Standard) | **~$0.03/mo** | Billed per 10k operations. An app that reads secrets on startup costs cents. |
| **Monitoring** | Application Insights | **$0** | First 5 GB/month ingestion is free. This project will not come close. |
| **Networking** | Container Apps ingress + FQDN + TLS | $0 | Free managed certificate on the `*.azurecontainerapps.io` hostname. |

### The realistic bill

| Scenario | Monthly |
|---|---|
| **Recommended shape** (ACR Basic + Container Apps + SQL free offer + Key Vault) | **≈ $5** |
| Same, but App Service B1 instead of Container Apps | ≈ $18 |
| Same, but Azure SQL Basic instead of the free offer | ≈ $10 |
| **After teardown** (delete the resource group) | **$0** |

So: **about $5/month, entirely from ACR**, for as long as the project is live.
That matches what was agreed — "stick with ACR, cancel right after the project is
done."

### Guardrails (non-negotiable, and they come first)

1. **A budget alert at $5, created before any other resource.** This is Issue #1,
   not an afterthought. Email alert at 80% and 100% of forecast.
2. **Everything in one resource group** (`rg-factorypulse`). Teardown is then
   `az group delete --name rg-factorypulse --yes` — one command, nothing orphaned.
   This is the single most important cost control: a resource you forgot about is
   the only way this bill surprises you.
3. **A dated teardown reminder.** When the project has served its purpose, delete
   the group. The Bicep templates stay in the repo, so it can all be recreated in
   minutes — which is itself a better portfolio story than a live URL.

> **If a new Azure account:** the $200 / 30-day free credit covers all of this
> comfortably. Don't let it lull you — the credit expires and billing continues.
> The budget alert still goes in first.

---

## 3. Proposed architecture

```
GitHub (main)
   │  merge
   ▼
GitHub Actions ──── OIDC federated credential ────► Azure (no stored secrets)
   │
   ├─ build + test          (existing)
   ├─ build image           (existing)
   ├─ push  image  ────────────────────────────────► Azure Container Registry
   └─ deploy ──────────────────────────────────────► Azure Container Apps
                                                          │
                                    ┌─────────────────────┼─────────────────────┐
                                    ▼                     ▼                     ▼
                            Azure SQL Database     Azure Key Vault      Application Insights
                            (serverless, free)     (JWT key, admin pw)   (Serilog sink)
                                    ▲                     │
                                    └─── managed identity ┘
```

**Everything lives in one resource group, in one region (West Europe).**

---

## 4. Locked / proposed decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | **Budget alert before any resource** | The cheapest mistake to prevent is the one you never make. Acceptance criterion for Issue #1. |
| D2 | **One resource group**, `rg-factorypulse`, **West Europe** | Teardown becomes atomic. Region matches the target job market. |
| D3 | **Bicep** for all infrastructure (not the portal, not Terraform) | Azure-native, no state file to manage, and it's what Azure shops actually use. The portal is fine for *looking*, terrible for *reproducing*. |
| D4 | **Azure Container Registry (Basic)** | Already decided in ADR-0014. Costs ~$5/mo; deleted at teardown. |
| D5 | **Azure Container Apps** for compute (not App Service) | **Recommended.** Scale-to-zero means an idle app is ~free, it consumes the container image we already build, and it's the more current Azure story. See the trade-off below. |
| D6 | **Azure SQL Database — free serverless offer**, with auto-pause | $0, genuinely managed SQL, and serverless auto-pause is a real cloud concept worth being able to talk about. Fall back to Basic (~$5/mo) if unavailable. |
| D7 | **OIDC federated credentials** for GitHub → Azure | No long-lived secret in GitHub. Already locked as D11 in the CI/CD design. |
| D8 | **Managed identity everywhere it can replace a password**; Key Vault only for the secrets that genuinely *are* secrets | The database uses **Entra authentication** — the connection string has no password in it, because there is no password. Key Vault then holds only the JWT signing key and the seed admin password. "There is no database password" beats "the database password is in Key Vault." See §6. |
| D9 | **Fresh secrets for Azure.** New JWT signing key, new admin password | Never the dev values, and never anything that has appeared in a doc or an example. Generated once, written straight to Key Vault, never into a file. |
| D10 | **Migrations move out of app startup**, applied as an **idempotent SQL script** by the deploy job | Delivers the ADR-0013 follow-up. See §8 for why the script beats `dotnet ef database update` here. |
| D11 | **Application Insights** via a Serilog sink, enriched with environment, machine name and **Git SHA** | Free tier. Every log line is traceable to the commit that produced it. Correlation comes free from the W3C `traceparent` ASP.NET Core already emits — no hand-rolled correlation ID. |
| D12 | **Rate limiting on the auth endpoints + Identity lockout** | The API is about to be on the public internet with a seeded admin account. See §7 — this is a *prerequisite*, not a polish item. |
| D13 | **Single-revision mode.** No traffic splitting, no blue/green | Container Apps supports weighted traffic across revisions (its answer to App Service's slots). We deliberately don't use it: one environment, one user, and a second live revision would double the compute. Documented as an ADR precisely *because* we're not using it. |
| D14 | **Deploy on merge to `main` only** | PRs build and test but do not deploy. One environment. Appropriate for a portfolio, and honest about it. |
| D15 | **Container Apps pulls from ACR with a managed identity** holding `AcrPull` | No registry username/password anywhere. Also the most common first-deploy failure — call it out now so we recognise the symptom (a generic "image pull failed"). |

### D5 trade-off: Container Apps vs App Service

This is the one place where cost and CV-keyword-matching pull in opposite directions,
so it's worth being explicit.

- **App Service** appears in more job ads. It is the "classic" .NET hosting answer,
  it costs ~$13/mo for B1, and it is always warm.
- **Container Apps** is cheaper (effectively free at this scale), scales to zero,
  and is where Azure is actually heading. Its cost is a **cold start** — the first
  request after an idle period takes a few seconds. For a demo link a recruiter
  clicks once, that is a non-issue.

**Recommendation: Container Apps.** It's ~$13/mo cheaper, it's the more modern
answer, and "I chose Container Apps over App Service because scale-to-zero suits an
intermittently-used API, and here's the ADR" is a *better* interview answer than
having used the more common service without thinking about it. The knowledge
transfers; you can discuss App Service fluently either way.

---

## 5. Authentication: OIDC federated credentials (D7)

The old way: create a service principal, put its password in a GitHub secret, hope
it never leaks and remember to rotate it.

**The current way**, and what we'll do:

1. Register an **Entra ID app** (or a user-assigned managed identity).
2. Add a **federated credential** that trusts GitHub's OIDC issuer for
   *this repo, on this branch* — e.g. subject
   `repo:wihanduplessis/FactoryPulse:ref:refs/heads/main`.
3. Give it the **AcrPush** and **Contributor** roles, scoped to the resource group only.
4. In the workflow, request an OIDC token and exchange it:

```yaml
permissions:
  id-token: write        # allows the runner to request an OIDC token
  contents: read

steps:
  - uses: azure/login@v2
    with:
      client-id:       ${{ vars.AZURE_CLIENT_ID }}
      tenant-id:       ${{ vars.AZURE_TENANT_ID }}
      subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
```

Note those are `vars`, not `secrets` — **none of the three is sensitive**. There is
no password anywhere. GitHub proves its identity to Azure cryptographically, and
Azure hands back a short-lived token scoped to one repo and one branch. If the repo
is public and someone forks it, their fork's OIDC subject doesn't match, so it gets
nothing.

---

## 6. Secrets and identity: aim for *no password to store* (D8)

The obvious design is "put every secret in Key Vault." That's fine, but it stops one
step short. The stronger position is to **eliminate the secrets that don't need to
exist**, and only vault what genuinely must remain secret.

| Value | Old way | What we'll do |
|---|---|---|
| SQL connection string | `User Id=sa;Password=...` in config | **Entra authentication + managed identity.** The connection string is `Server=...;Database=...;Authentication=Active Directory Default;` — **no password, because there is no password.** The container app authenticates to SQL *as itself*. |
| App Insights | Instrumentation key | **Connection string** (keys are legacy), injected as config — not sensitive. |
| ACR pull | Registry username + password | **Managed identity with `AcrPull`** (D15). |
| **JWT signing key** | — | **Key Vault.** This one is irreducibly a secret. |
| **Seed admin password** | — | **Key Vault.** Likewise. |

So the app ends up with **exactly two things in Key Vault**, read at startup via
managed identity, and *nothing* sensitive in Bicep, in GitHub, or in a config file.

That's the interview answer. Not "I used Key Vault" — everybody says that — but
"I removed two of the four secrets entirely, and vaulted the two that were real."

**Note on the SQL side:** the container app's managed identity must be created as a
*database user* (`CREATE USER [...] FROM EXTERNAL PROVIDER`) and granted
`db_datareader` / `db_datawriter`. Notably **not** `db_owner` — which is what makes
D10 (migrations out of startup) not just tidier but *necessary*: the app cannot alter
its own schema, by design.

---

## 7. Hardening the public API (D12)

Right now, `POST /api/auth/login` will accept unlimited password attempts from
anyone. On `localhost` that is irrelevant. On a public Azure URL, attached to a CV,
with a seeded admin account, it is a genuine weakness — and exactly the thing an
interviewer would prod at.

Two small additions, both first-party, no new packages:

1. **Rate limiting** — ASP.NET Core's built-in `AddRateLimiter`, a fixed or sliding
   window on the auth endpoints (e.g. 5 attempts per minute per IP), returning `429`.
2. **Identity lockout** — `SignInManager.PasswordSignInAttempt` with
   `lockoutOnFailure: true`, plus `MaxFailedAccessAttempts` and
   `DefaultLockoutTimeSpan` configured. Identity supports this already; we simply
   aren't switching it on.

Roughly an hour of work, and it converts "publicly exposed login endpoint" into
"defence in depth, with an ADR." **This lands before the app is reachable, not
after** — hence Issue #0.

---

## 8. Migrations: the interesting problem (D10)

Today, the container runs `Database.MigrateAsync()` on startup, gated behind
`ApplyMigrationsOnStartup`. ADR-0013 already flagged this as **unsuitable for
production**, for good reasons:

- If Container Apps runs **two replicas**, both race to migrate the same database.
- The app needs **schema-altering rights** on the database at all times — far more
  privilege than it needs to serve traffic.
- A failed migration takes the **app** down, and the failure surfaces as a crash
  loop rather than a failed deployment.

**Proposed:** CI generates an **idempotent SQL script** at build time —

```bash
dotnet ef migrations script --idempotent --output migrations.sql
```

— and the deploy job applies it to Azure SQL **before** rolling out the new image.
The script has `IF NOT EXISTS` guards around every migration, so running it twice is
harmless.

`ApplyMigrationsOnStartup` stays `true` for local Compose and integration tests
(where it's convenient and safe) and is set **`false` in Azure**. The flag we added
in v0.5 turns out to be exactly the seam we need — which is a nice demonstration of
why it was worth adding.

### Why a script, and not `dotnet ef database update`?

This was challenged in review, on the grounds that `database update` is simpler and
has fewer moving parts. It's worth answering properly, because the reasoning is not
what it first appears.

**The two options are the same size.** Both need the `dotnet-ef` tool on the runner.
Both need the runner to punch through the Azure SQL firewall — and *that* is where
all the actual complexity in this milestone lives. The script adds nothing there.
The literal difference is one CLI flag.

What the script buys, for that zero extra cost:

- It is generated in the **build** job, which already has the SDK and the source, and
  published as an **artifact**. The **deploy** job then needs only `sqlcmd` — no .NET
  SDK, no source checkout, no `DbContext`.
- **You can read the SQL before it runs against your database.** Every team that has
  ever been burned by a migration ends up wanting this.
- `--idempotent` guards every migration with `IF NOT EXISTS`, so a re-run is a no-op.
  Re-running `database update` is *also* safe, but the script makes the safety
  visible rather than trusting the tool.

Either choice is defensible and the difference is small — but it should be made for
the right reason. "The script is more complex" isn't true; it's one flag and a better
separation of concerns.

### The wrinkle

The GitHub runner has to reach Azure SQL, which means either a **temporary firewall
rule for the runner's IP** (added and removed by the deploy job) or **"Allow Azure
services"**. Neither is beautiful. We'll take the temporary-rule approach — narrower,
and self-cleaning — and **document the trade-off honestly** rather than pretend it's
clean.

---

## 9. What lands in the repo

```
infrastructure/
├── docker/                  (existing)
└── azure/
    ├── main.bicep           # resource group scope: everything below
    ├── modules/
    │   ├── registry.bicep
    │   ├── database.bicep
    │   ├── keyvault.bicep
    │   ├── monitoring.bicep
    │   └── containerapp.bicep
    ├── main.parameters.json
    └── README.md            # provision + teardown, in two commands

.github/workflows/
├── ci.yml                   (existing — gains push-to-ACR)
└── cd.yml                   # or a deploy job appended to ci.yml
```

---

## 10. Issue breakdown (milestone "v0.7 — Azure deployment")

| # | Branch | Acceptance |
|---|--------|------------|
| **0** | `feature/api-hardening` | **Rate limiting on the auth endpoints (429 after N attempts) + Identity lockout enabled.** Covered by tests. Ships *before* anything is publicly reachable. No Azure dependency — can be done while the account is being created. |
| 1 | `feature/azure-foundation` | Subscription set up; **budget alert at $5 exists**; resource group created; `main.bicep` skeleton deploys cleanly. **Nothing else is provisioned until this is green.** |
| 2 | `feature/azure-identity` | Entra app + federated credential; `azure/login@v2` succeeds from a workflow; roles scoped to the resource group. No secrets in GitHub. |
| 3 | `feature/azure-registry` | ACR in Bicep; CI pushes `sha-<short-sha>` and `latest` on merge to `main`. |
| 4 | `feature/azure-database` | Azure SQL (free serverless) + Key Vault in Bicep; **Entra auth, no SQL password**; fresh JWT key and admin password generated straight into Key Vault; app reads them via managed identity. |
| 5 | `feature/azure-migrations` | Idempotent migration script generated in the build job, applied by the deploy job before rollout; `ApplyMigrationsOnStartup=false` in Azure. |
| 6 | `feature/azure-deploy` | Container App (single-revision, scale-to-zero, `AcrPull` via managed identity) running the image; **public HTTPS URL serves Swagger and a successful login**; App Insights receiving enriched Serilog traces. |
| 7 | `feature/azure-docs` | ADRs (Container Apps over App Service; single-revision/no traffic splitting; OIDC; managed identity over vaulted passwords; migrations-out-of-startup; API hardening); README with the live URL, architecture diagram, screenshots, and the teardown command. Roadmap marked **v1.0**. |

---

## 11. Acceptance walkthrough

1. A budget alert exists and would email you at $5 — **before** anything else was created.
2. `az deployment group create` builds the entire stack from an empty resource group.
3. Merging a PR to `main` pushes an image and deploys it, with **no secret in GitHub**.
4. A public HTTPS URL serves Swagger; `POST /api/auth/login` returns a JWT; an
   authenticated `POST /api/machines` persists to Azure SQL.
5. Hammering `/api/auth/login` with bad passwords returns **429**, and the account
   locks out.
6. Application Insights shows the request trace, tagged with the **Git SHA** that
   produced it.
7. `az group delete --name rg-factorypulse --yes` returns the bill to **$0**, and
   the Bicep in the repo can rebuild it all.

---

## 12. Timeline

The Azure **free account gives $200 of credit for 30 days**, which comfortably covers
this entire milestone — the realistic out-of-pocket cost is **$0** if the project is
torn down (or the trial simply lapses) within the month. The budget alert still goes
in first: the habit is the point, and the credit expires.

Target: **live within a week.** A live URL on day one is possible but optimistic — the
time sinks are not the code, they are account/card verification, getting the OIDC
subject string exactly right, and the `AcrPull` permission on first deploy (D15).
Issues #0–#3 are a comfortable day; the live URL is day one *if things go smoothly*,
day two if they go normally; hardening, ADRs, README polish and the v1.0 tag fill the
rest of the week.

---

## 13. Open questions for the build

- Is the **Azure SQL free offer** actually available on this subscription? Trial
  subscriptions sometimes have quota restrictions. Check at provisioning time; fall
  back to Basic (~$5/mo) and update the cost table honestly if not.
- **One workflow or two?** Appending a `deploy` job to `ci.yml` is simplest; a
  separate `cd.yml` is tidier. Lean simple.
- **Does `Authentication=Active Directory Default` work cleanly from the container**
  (§6)? If managed-identity SQL auth proves painful, the documented fallback is a
  SQL password in Key Vault — a step down, but not a failure. Decide with eyes open.

---

## 14. And then stop

When this milestone is done, FactoryPulse is **v1.0** — not because it models every
aspect of manufacturing, but because it satisfies the goal it was built for. The
remaining work is polish, not features: README, screenshots, release notes, the tag,
and then the CV and LinkedIn.

The temptation after v1.0 will be to keep adding to it. Don't. A **second, contrasting
project** — event-driven messaging, SignalR, a different architectural style — tells a
far stronger story than one ever-expanding application.
