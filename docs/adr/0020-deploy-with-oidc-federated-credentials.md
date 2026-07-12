# ADR-0020: Authenticate GitHub Actions to Azure with OIDC federated credentials

- **Status:** Accepted
- **Date:** 2026-07-12
- **Deciders:** Project owner
- **Committed in advance by:** [ADR-0014](0014-ci-with-github-actions.md) (D11 of the CI/CD design)

## Context

CI must push images to ACR, apply migrations to Azure SQL, and deploy to Container Apps.
All three require permission to act on the Azure subscription.

The conventional approach is to create a service principal with a client **secret**, and
store that secret in GitHub. That secret is long-lived, must be rotated by hand (nobody
does), can be leaked into a build log, and — if stolen — works for anyone, from anywhere,
until someone notices.

This repository is **public**, which sharpens the question considerably.

## Decision

**OpenID Connect federated credentials. No secret exists.**

1. An Entra **app registration** (`factorypulse-github-actions`) with a service
   principal — and, notably, `passwordCredentials: []` and `keyCredentials: []`. It has
   **no credentials at all** and never will.
2. A **federated credential** on it, trusting GitHub's OIDC issuer for exactly one
   subject:
   ```
   repo:wihanduplessis/FactoryPulse:ref:refs/heads/main
   ```
3. Role assignments **scoped to `rg-factorypulse`**, never the subscription:
   - `Contributor` — create and update resources
   - `User Access Administrator` — because the Bicep templates contain role assignments,
     and a Contributor cannot grant permissions (see ADR-0016)
   - `AcrPush` on the registry
   - `db_owner` inside the database (for migrations, ADR-0019)
4. The workflow requests a token with `permissions: id-token: write` and exchanges it
   via `azure/login@v2`.

### Why this is not just "a nicer secret"

Each workflow run receives a **short-lived, cryptographically signed token from GitHub**
asserting *"this run belongs to `wihanduplessis/FactoryPulse`, on branch `main`."* Azure
was configured, in advance, to trust exactly that assertion and nothing else.

- **There is no password to leak**, because none was ever created.
- **Forks get nothing.** Anyone can fork a public repository and run the workflow —
  GitHub will issue their run a token too, but its subject reads
  `repo:theirname/FactoryPulse`. It does not match, and Azure refuses.
- **Pull requests get nothing.** A PR's token subject is `…:pull_request`, which also
  does not match — so the migrate and deploy jobs are gated to `push` on `main` and
  *skip* on every PR. That is the security model working, not a limitation.
- Even a leaked token is expired by the time anyone reads the log.
- A compromise is bounded by one resource group that can be deleted and rebuilt from
  Bicep in minutes.

### Client ID, tenant ID and subscription ID are `vars`, not `secrets`

They are identifiers, not credentials — useless without a GitHub-signed token. Storing
them as secrets would be theatre, and would obscure the actual achievement: **the list
of secrets required to deploy this system is empty.**

(Two values *are* stored as GitHub Secrets — the operator's Entra object ID and UPN — but
only because a public repository has **public workflow logs**, and `vars` are echoed into
them while secrets are masked. That is a privacy decision about an email address, not a
security one.)

## Alternatives considered

- **Service principal with a client secret.** The common approach. Rejected: a long-lived
  credential in a public repository's settings, with no rotation story.
- **A stored ACR username/password.** Same objection, plus it would require enabling the
  registry admin user, undoing ADR-0018.
- **Subscription-scoped roles.** One word shorter, dramatically worse blast radius.
- **A second federated credential for pull requests.** Rejected — a pull request,
  potentially from a fork, has no business authenticating to a cloud account.

## Consequences

### Positive

- **No secret is stored anywhere** for Azure access. Nothing to rotate, nothing to leak.
- Trust is bound to one repository, one branch, one resource group.
- Safe to operate in the open: the repository is public and this remains true.
- The three values a reader can see (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
  `AZURE_SUBSCRIPTION_ID`) grant nothing.

### Negative / trade-offs

- **Cannot be verified on a pull request.** The `azure-*` jobs skip on PRs by design, so
  a misconfiguration is only discovered after merge, on `main`. Accepted — the
  alternative is trusting fork PRs with cloud credentials.
- The federated **subject string must match byte for byte**, including repository case.
  When it does not, the error (`AADSTS70021: No matching federated identity record
  found`) is unhelpful.
- CI holds `User Access Administrator` on the resource group, so it can grant roles
  there. Necessary for IaC-managed RBAC; scoped to a disposable resource group; named
  here rather than buried.
