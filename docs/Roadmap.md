# Roadmap

| Version | Scope | Status |
|---------|-------|--------|
| 0.1 | Solution, API, Database, Logging, Swagger, Architecture | ✅ |
| 0.2 | Machine Management | ✅ |
| 0.3 | Production Orders | ✅ |
| 0.3.1 | Quality — unit tests, integration tests, docs | ✅ |
| 0.4 | Authentication (Identity + JWT + role policies) | ✅ |
| 0.5 | Docker (multi-stage build, Compose) | ✅ |
| 0.6 | CI/CD (GitHub Actions) | ✅ |
| 0.7 | Azure deployment | ✅ |
| **1.0** | **A fully working cloud-hosted manufacturing management API** | ✅ |

## What v1.0 means

The API is live on a public HTTPS URL, deployed from `main` by a pipeline that stores no
secrets, backed by a managed database it does not have permission to alter, with its
whole environment described by Bicep and destroyable in one command.

Not "feature complete" — it never set out to model every aspect of manufacturing. It set
out to demonstrate that a backend can be designed, built, documented, tested,
containerized, automated and deployed properly, and it does.

## Deliberately not built

Recording what was *left out*, and why, is part of the point:

- **Dashboard / KPI aggregation.** Originally planned for v0.4. Dropped in favour of
  authentication and deployment — a dashboard is another set of read endpoints, and would
  have demonstrated nothing the existing ones do not.
- **Refresh tokens.** Access tokens expire in 30 minutes and that is the end of it.
  Deferred deliberately at the start of v0.4; still deferred.
- **An Angular frontend.** This is a backend portfolio piece. A thin UI would have added
  weeks and demonstrated less than the ADRs do.
- **High availability.** One replica, one region, no traffic splitting. See
  [ADR-0017](adr/0017-container-apps-over-app-service.md) — the alternative costs money
  the project does not need to spend.

## After v1.0

Not more features here. A **second, contrasting project** — event-driven messaging,
SignalR, a different architectural style — says more than one ever-expanding application.
