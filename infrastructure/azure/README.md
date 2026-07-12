# Azure infrastructure

Everything FactoryPulse runs on in Azure, described as code. Provisioning is one
command; deleting it all is one command.

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- `az bicep install`
- `az login`

## A budget alert comes first

Before provisioning anything, a **monthly budget with alerts** must exist on the
subscription. It is the one piece of this setup that is deliberately *not* code:
it has to exist before any infrastructure does, and making the safety net depend
on the same toolchain it is meant to protect you from is a bad trade.

Cost Management → Budgets (scoped to the **subscription**, not the billing
account) → monthly, **$5**, alerting on **actual at 80%** and **forecasted at
100%**.

Both alerts, not just one: *actual* tells you money is already gone; *forecasted*
tells you Azure projects — from the current burn rate — that you are about to
overshoot, which is the one that leaves you time to react.

## Provision

```powershell
az group create --name rg-factorypulse --location swedencentral `
  --tags application=factorypulse managedBy=bicep

./infrastructure/azure/deploy.ps1
```

Deployments are **idempotent**: ARM compares the template to what exists and
reconciles the difference, so re-running is safe and does not create duplicates.

`deploy.ps1` resolves the operator-specific parameters (your Entra identity, the CI
service principal) from Azure itself. They are **not** in `main.parameters.json` —
that file describes the *system*, and anything true only of this subscription or this
person is an input, kept out of a public repository. Passing them by hand works too,
but a fresh shell then silently supplies empty strings and the deployment fails deep
inside ARM; the script exists so that cannot happen.

CI deploys the same template with `imageTag` set to the commit SHA, authenticating
with [OIDC federated credentials](../../docs/adr/0020-deploy-with-oidc-federated-credentials.md)
— no stored secret.

## Layout

| File | Purpose |
|------|---------|
| `main.bicep` | Entry point. Parameters, naming, tags; composes the modules. |
| `main.parameters.json` | System-level values only. No identities. |
| `deploy.ps1` | Resolves identities and deploys. |
| `modules/identity.bicep` | User-assigned managed identity the app runs as. |
| `modules/registry.bicep` | Container registry (+ `AcrPush` for CI, `AcrPull` for the app). |
| `modules/keyvault.bicep` | Key vault (+ read for the app, write for the operator). |
| `modules/database.bicep` | Azure SQL — Entra-only auth, serverless, free tier. |
| `modules/monitoring.bicep` | Log Analytics + Application Insights. |
| `modules/containerapp.bicep` | Container Apps environment and the app itself. |

## Regions run out

West Europe refused to create a container registry and North Europe refused to create a
SQL server — both are old, oversubscribed regions that stop accepting new customers for
specific services. The environment lives in **Sweden Central**.

**Probe before committing an environment to a region.** A SQL logical server costs
nothing to create and delete:

```powershell
az group create --name rg-probe --location <candidate>
az sql server create --name sql-probe-<something> --resource-group rg-probe --location <candidate> `
  --enable-ad-only-auth --external-admin-principal-type User `
  --external-admin-name <your-upn> --external-admin-sid <your-object-id>
az group delete --name rg-probe --yes --no-wait
```

## Naming

Several Azure resources (container registries, key vaults) need names that are
unique across **all of Azure**, not just this subscription. `main.bicep` derives a
short `resourceToken` with `uniqueString(subscription().subscriptionId,
resourceGroup().id)` and appends it to those names.

It is deterministic on purpose: the same subscription and resource group always
produce the same token, so redeploying reuses the existing resources instead of
creating a second set alongside them — which you would then be paying for twice.

## Tear down

```bash
az group delete --name rg-factorypulse --yes --no-wait
```

Every billable resource lives inside that single resource group, so this returns
the bill to **$0** with nothing orphaned. The templates stay in the repository, so
the entire environment can be rebuilt in minutes — which is why tearing it down is
a safe thing to do rather than a decision to agonise over.
