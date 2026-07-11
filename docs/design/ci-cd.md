# Design: CI/CD with GitHub Actions (v0.6)

Design-first note for continuous integration: every push and pull request builds
the solution, runs the tests, and builds the container image — with the image
published so v0.7 (Azure) can deploy it.

> Status: Proposed &bull; Target: v0.6 &bull; Depends on: v0.5 (Containerization)

---

## 1. Goals

- Every push/PR: **restore → build → test → build image**. A red pipeline blocks a merge.
- **Publish the container image** to a registry, so the Azure milestone has
  something to deploy (each milestone reuses the last).
- A **green CI badge** on the README — the visible payoff for the 40-test suite.
- **Zero cost.**

---

## 2. Locked decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | **GitHub Actions** | Native to the repo, free minutes on public repos, no extra accounts. |
| D2 | **Azure Container Registry (ACR)** will be the registry — but it is **created in v0.7**, not now | ACR demonstrates *Azure* experience (a stronger signal than GitHub's registry for the roles being targeted). It costs ~$5/mo (Basic), so the registry is created only when it is actually needed — at deployment — and deleted when the project wraps up. |
| D3 | Triggers: **push to `main`** and **pull requests targeting `main`** | Catches problems before merge, and validates `main` after. |
| D4 | **Two jobs**: `build-and-test`, then `docker` (needs the first to pass) | Fail fast on cheap checks; don't waste time building an image for broken code. |
| D5 | v0.6 **builds the image but does not push it** | Pushing requires an ACR *and* Azure credentials, neither of which exist yet. Building still validates the Dockerfile on every commit — the valuable half. The push (and Azure auth) lands in v0.7 as one coherent piece. |
| D6 | Image tags (from v0.7): **`sha-<short-sha>`** and **`latest`** | The SHA tag is immutable and traceable to a commit (what Azure deploys); `latest` is a convenience. |
| D7 | **NuGet package caching** keyed on `Directory.Packages.props` | Central Package Management means one file determines all versions — a perfect cache key. |
| D8 | **Integration tests** (Testcontainers) run in CI alongside unit tests | Delivers the ADR-0010 follow-up. GitHub runners have Docker, so Testcontainers works out of the box. |
| D9 | **No deployment and no registry push in v0.6.** CI only | Keeps v0.6 free, self-contained, and independent of Azure. All Azure coupling (ACR, federated auth, deploy) lands together in v0.7. |
| D10 | **Branch protection on `main`**: require the workflow to pass before merge | Makes the pipeline meaningful rather than decorative. Also formalises the PR workflow. |
| D11 | v0.7 will authenticate to Azure with **OIDC federated credentials**, not a long-lived service-principal secret | No secrets stored in GitHub; the modern, recommended approach. |

---

## 3. Workflow shape

`.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - checkout
      - setup-dotnet (10.0.x)
      - cache NuGet  (key: Directory.Packages.props hash)
      - dotnet restore  backend/FactoryPulse.slnx
      - dotnet build    --no-restore -c Release
      - dotnet test     --no-build   -c Release  (unit + integration)
      - upload test results

  docker:
    needs: build-and-test
    runs-on: ubuntu-latest
    steps:
      - checkout
      - build image from backend/docker/Dockerfile.api (context: backend/)
      # v0.7 adds: azure/login (OIDC) → az acr login → docker push
```

Notes:
- The Docker build in CI uses the **same Dockerfile and context** as local
  development. No CI-specific build path to drift.
- The `docker` job is deliberately push-free in v0.6. v0.7 appends the Azure login
  and push steps to this same job.

## 4. Integration tests (D8)

A small set (3–5), separate from the unit tests:

- **Testcontainers** spins up a real SQL Server container per test run.
- **`WebApplicationFactory`** hosts the API in-process against that database.
- Coverage: an authenticated end-to-end slice — log in, create a machine, create an
  order, start it — proving migrations, EF mapping, auth, and the HTTP pipeline all
  work together.
- They must be **skippable/fast enough** not to slow the pipeline unreasonably;
  they are the minority of the suite by design.

The value is as much *signalling* as coverage: the pipeline reading
`unit → integration → image` is a stronger story than `build → publish`.

## 5. README badge

```markdown
![CI](https://github.com/<owner>/FactoryPulse/actions/workflows/ci.yml/badge.svg)
```
A green badge showing 40+ tests passing on every push is the visible dividend of
the testing work.

## 6. Trade-offs

- **ACR vs ghcr.io** — ghcr.io would be free and needs no secrets. ACR is chosen
  because it demonstrates *Azure* experience, which is the point of the project;
  the ~$5/mo (Basic) is accepted and the registry is deleted when the project
  wraps. **Cost is deferred by creating the ACR in v0.7**, not v0.6 — v0.6 stays
  free.
- **v0.6 builds but does not push.** Slightly less "complete" than a full
  build-and-publish pipeline, but it keeps the milestone free and Azure-free, and
  the push step is a small addition once ACR and federated auth exist.
- **Migrations still run on app startup** in the container. CI does not run
  migrations. v0.7 will decide whether to add an explicit migration step.
- **Testcontainers needs Docker on the runner** — fine on GitHub's hosted Linux
  runners; would need care on self-hosted ones.

## 7. Issue breakdown (milestone "v0.6 — CI/CD")

| # | Issue | Acceptance |
|---|-------|------------|
| 1 | `feature/ci-workflow` | `.github/workflows/ci.yml` with `build-and-test`; runs on push + PR; NuGet cached; all 40 tests green in Actions |
| 2 | `feature/integration-tests` | 3–5 Testcontainers + `WebApplicationFactory` tests; pass locally and in CI |
| 3 | `feature/ci-docker` | `docker` job builds the image on every run (no push); Dockerfile validated by CI |
| 4 | `feature/ci-docs` | README badge + CI section; **ADR-0014** (GitHub Actions, and the ACR-over-ghcr.io cost/signal decision); branch protection enabled on `main` |

## 8. Acceptance walkthrough

1. Open a PR → the workflow runs → **build + unit + integration tests green**, and
   the image builds.
2. Merge to `main` → the workflow runs green on `main`.
3. Break a test deliberately, open a PR → the check **fails and blocks the merge**.
4. README shows a **green CI badge**.
5. (v0.7 will extend the `docker` job to log in to Azure via OIDC and push the
   image to ACR.)
