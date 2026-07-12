# Architecture Decision Records (ADRs)

This folder records the **significant technical decisions** made on FactoryPulse —
not just *what* was decided, but *why*, and what trade-offs were accepted.

An ADR captures a decision at the point in time it was made. Records are
**immutable**: once accepted, an ADR is not edited to reflect a later change of
mind. Instead, a new ADR is added that supersedes the old one, and the old one's
status is updated to `Superseded by ADR-XXXX`.

## Why keep these?

- They answer "why is it done this way?" for future-you and any collaborator.
- They make the reasoning reviewable, the same way code is.
- In an interview, "here's the ADR explaining that trade-off" beats "it seemed
  like a good idea."

## Format

We use a lightweight [MADR](https://adr.github.io/madr/)-style template — see
[`template.md`](template.md). Each record has a status, the context that forced a
decision, the decision itself, the alternatives weighed, and the consequences
(good and bad).

## Index

| ADR | Title | Status |
|-----|-------|--------|
| [0001](0001-use-docker-for-local-sql-server.md) | Use Docker for the local SQL Server environment | Accepted |
| [0002](0002-use-guid-primary-keys.md) | Use GUID primary keys for entities | Accepted |
| [0003](0003-store-enums-as-strings.md) | Store enum values as strings in the database | Accepted |
| [0004](0004-use-repository-pattern.md) | Use the repository pattern for data access | Accepted (amended) |
| [0005](0005-services-return-dtos.md) | Services return DTOs, not domain entities | Accepted |
| [0006](0006-result-for-expected-outcomes.md) | `Result<T>` for expected outcomes, exceptions for unexpected | Accepted |
| [0007](0007-manual-mapping.md) | Use manual mapping in the Application layer | Accepted |
| [0008](0008-logging-strategy.md) | Structured logging (Serilog) with a layer-based strategy | Accepted |
| [0009](0009-rich-domain-model-for-aggregates.md) | Rich domain model for aggregates with a lifecycle | Accepted |
| [0010](0010-testing-strategy.md) | Testing strategy and library choices | Accepted |
| [0011](0011-authentication-with-identity-and-jwt.md) | Authentication via ASP.NET Core Identity and JWT | Accepted |
| [0012](0012-auth-in-infrastructure-with-result.md) | Keep auth in Infrastructure, surface through `Result` | Accepted |
| [0013](0013-containerize-the-api.md) | Containerize the API (multi-stage build + Compose) | Accepted |
| [0014](0014-ci-with-github-actions.md) | Continuous integration with GitHub Actions | Accepted |
| [0015](0015-api-hardening-before-public-exposure.md) | Harden the authentication endpoints before public exposure | Accepted |

## Candidate future ADRs

Decisions already made in the codebase that are worth recording as the project
grows: Clean/layered architecture split, Central Package Management, choice of
API documentation UI (Scalar/Swagger), and `.slnx` solution format.
