# ADR-0016: Infrastructure as code with Bicep, and a budget before any of it

- **Status:** Accepted
- **Date:** 2026-07-12
- **Deciders:** Project owner

## Context

v0.7 puts FactoryPulse in Azure. Azure infrastructure can be created by clicking in
the portal, and for a one-person project that is the fastest path to a running system.
It is also the path that produces something nobody can review, nobody can reproduce,
and nobody — including future-you — can explain six months later.

There is also a money problem that does not exist on a laptop: cloud resources bill
by the hour whether or not anyone is using them, and the failure mode is silent.

## Decision

### Bicep for everything, in one resource group

All Azure resources are declared in `infrastructure/azure/*.bicep` and deployed with
`az deployment group create`. Nothing is created by clicking, with two deliberate
exceptions (below).

- **One resource group**, `rg-factorypulse`. Teardown is therefore atomic:
  `az group delete` removes every billable resource with nothing orphaned. A resource
  you forgot about is the only realistic way this bill surprises you.
- **Modules** (`identity`, `registry`, `keyvault`, `database`, `monitoring`,
  `containerapp`) composed by `main.bicep`, so no single file becomes unreadable.
- **Deterministic naming.** `uniqueString(subscription().subscriptionId,
  resourceGroup().id)` produces a stable suffix for the resources whose names must be
  globally unique (registry, key vault, SQL server). *Deterministic* is the point: a
  random suffix would create a second registry on every deploy, and you would pay for
  both.
- **Idempotent.** ARM reconciles the template against reality, so re-running a deploy
  is a no-op rather than a duplicate. This is what makes deploying on every merge safe.

### A budget alert exists before any infrastructure does

**Issue #1 of the milestone was a $5 monthly budget with alerts at 80% (actual) and
100% (forecasted). No other resource was created until it existed.**

Both alerts, not one: *actual* tells you the money is already gone; *forecasted* tells
you Azure projects, from the current burn rate, that you are about to overshoot — which
is the one that leaves time to react.

### Two things that are deliberately not code

1. **The budget itself** is created in the portal. It must exist *before* any
   infrastructure does, and making the safety net depend on the very toolchain it
   exists to protect you from is a bad trade.
2. **Operator identity** — the deployer's Entra object ID, UPN, and the CI service
   principal's object ID — are passed as deploy-time parameters, never committed.
   `main.parameters.json` describes **the system**; anything true only of *this*
   subscription or *this* person is an input. This also keeps a personal email address
   out of a public repository.

## Alternatives considered

- **The portal.** Fast, and produces nothing reviewable, reproducible, or diffable.
  Rejected — reproducibility is what makes teardown a safe decision rather than a
  frightening one.
- **Terraform.** Excellent, and the more common choice across clouds. Bicep chosen
  because this is explicitly an *Azure* portfolio project, Bicep is Azure-native, and
  it has no state file to store and protect.
- **A budget in Bicep** (subscription-scope deployment). Purer, but it would have made
  the guardrail depend on a Bicep toolchain that did not yet exist. Deliberately
  rejected in favour of having the alarm on before anything could bill.

## Consequences

### Positive

- The entire production environment is described by the repository and can be rebuilt
  from scratch in minutes. **This is what makes tearing it down cheap** — which is the
  actual cost control, more than any alert.
- Every infrastructure change is reviewable in a pull request, like code.
- Deploy-time parameter injection means the templates are genuinely reusable by anyone,
  not hardcoded to one account.
- Cost is bounded and observed: ~$5/month, all of it ACR, with an alert watching.

### Negative / trade-offs

- **Bicep is a second language to learn**, with its own sharp edges — every Azure
  resource type has different name constraints (Key Vault: 24 characters; ACR: 50, no
  hyphens), and they are discovered by failing.
- **Parameters passed at deploy time can be silently empty.** A deploy from a fresh
  shell where `$objId` was never set fails deep inside ARM with `InvalidPrincipalId`
  rather than "you forgot a parameter." Mitigated by a `deploy.ps1` wrapper that
  fetches them itself.
- Role assignments in the template mean the CI identity needs **User Access
  Administrator** on the resource group, not just Contributor — it can grant roles.
  Scoped to one disposable resource group, and the alternative (managing RBAC by hand,
  outside the templates) means the repo no longer describes the system.

### Note: Azure regions run out

West Europe refused to create a container registry ("not accepting new customers") and
North Europe refused to create a SQL server. Both are old, oversubscribed regions.
The environment was moved to **Sweden Central**.

The lesson worth recording: **probe the scarcest resource in a candidate region before
committing an environment to it.** A SQL logical server is free to create and delete —
two minutes of probing would have saved two rebuilds. That the rebuild took three
commands (`delete`, `create`, `deploy`) rather than an afternoon of re-clicking is, by
itself, the argument for this ADR.
