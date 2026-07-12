# ADR-0014: Continuous integration with GitHub Actions

- **Status:** Accepted
- **Date:** 2026-07-12
- **Deciders:** Project owner

## Context

The project has a 46-test suite and a container image, but nothing verified them
automatically. Without CI, tests are only as good as the discipline to run them,
and a broken Dockerfile would only be discovered by hand. The pipeline also needs
to produce the artifact that the Azure milestone will deploy.

## Decision

**GitHub Actions**, in one workflow (`.github/workflows/ci.yml`) triggered on
pushes to `main` and pull requests targeting `main`.

### Two jobs

1. **`build-and-test`** — restore, build (Release), and run the **whole suite**:
   40 unit tests plus 6 **integration tests**.
2. **`docker`** — `needs: build-and-test`, so it is skipped when tests fail.
   Builds the API image using the **same Dockerfile and context as local
   development** (no CI-specific build path that can drift).

### Supporting decisions

- **NuGet caching** keyed on a hash of `Directory.Packages.props` and the
  `.csproj` files. Central Package Management means one file determines every
  version — an unusually clean cache key.
- **Docker layer caching via `type=gha`** — a runner is a fresh machine with no
  layer cache, so without this every run would re-pull base images and re-run
  `dotnet restore` inside the image. Same idea as the NuGet cache, one level down.
- **Integration tests run in CI** (Testcontainers + `WebApplicationFactory`),
  delivering the follow-up from [ADR-0010](0010-testing-strategy.md). GitHub's
  hosted Linux runners provide Docker, so a real SQL Server container starts
  inside the pipeline.
- **Branch protection on `main`**: pull request required; both checks must pass.
  This is what makes the pipeline meaningful rather than decorative — a red build
  physically blocks the merge, and direct pushes to `main` are rejected.
- **Actions are pinned to floating major tags** (`@v7`, `@v6`, `@v5`) so they pick
  up patches automatically; they were bumped off Node-20-based versions to remove
  a deprecation warning before it became a breakage.

### Registry: ACR, created later

The image is **built but not pushed** in v0.6. The registry will be **Azure
Container Registry**, created in v0.7.

- **Why ACR over GitHub Container Registry:** ghcr.io is free and needs no
  credentials, but ACR is an **Azure** service, and demonstrating Azure is the
  point of the project. The ~$5/month (Basic) is accepted, and the registry is
  deleted when the project wraps.
- **Why not create it now:** pushing requires an ACR *and* Azure credentials,
  neither of which exist yet. Deferring keeps v0.6 free and Azure-independent, and
  starts the cost clock only when the registry is actually needed. Building the
  image on every run still validates the Dockerfile — the valuable half.
- v0.7 appends an Azure login (**OIDC federated credentials**, not a long-lived
  service-principal secret) and a push step to the existing `docker` job.

## Alternatives considered

- **No CI** — rejected. A test suite nobody runs is decoration.
- **ghcr.io as the registry** — free and simpler, but a weaker Azure signal. See
  above; documented as a deliberate, revisitable cost decision.
- **Unit tests only in CI** (integration tests local-only) — faster pipeline, but
  loses the strongest signal: that the app works against a real database.
- **Deploying from CI now** — rejected. All Azure coupling lands together in v0.7
  once the Azure design exists.

## Consequences

### Positive

- Every push and PR is built, fully tested (including against a real SQL Server),
  and has its Dockerfile validated.
- `main` cannot receive code that does not build or pass 46 tests.
- A green CI badge on the README makes the test suite visible.
- The `docker` job is shaped so v0.7 adds a login and a push, not a rewrite.

### Negative / trade-offs

- Integration tests add ~30s to the pipeline (container pull and boot), and are
  inherently more fragile than unit tests — a cold-start flake was observed
  locally, though not on CI. Worth watching.
- Branch protection removes the ability to push directly to `main` — deliberate,
  but a change of habit.
- Docker layer caching consumes GitHub Actions cache storage (subject to eviction).

### Follow-ups

- v0.7: ACR, OIDC federated credentials, image push, deployment.
- Consider an explicit migration step in the deployment pipeline rather than
  migrate-on-startup (see [ADR-0013](0013-containerize-the-api.md)).
