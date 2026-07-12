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

From the repository root:

```bash
az group create --name rg-factorypulse --location westeurope \
  --tags application=factorypulse managedBy=bicep

az deployment group create \
  --resource-group rg-factorypulse \
  --template-file infrastructure/azure/main.bicep \
  --parameters infrastructure/azure/main.parameters.json
```

Deployments are **idempotent**: ARM compares the template to what exists and
reconciles the difference, so re-running is safe and does not create duplicates.

## Layout

| File | Purpose |
|------|---------|
| `main.bicep` | Entry point. Parameters, naming, tags; composes the modules. |
| `main.parameters.json` | Values for a deployment. |

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
