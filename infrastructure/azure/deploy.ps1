<#
.SYNOPSIS
    Deploys the FactoryPulse Azure environment.

.DESCRIPTION
    Fetches the operator-specific parameters (your Entra identity, and the CI service
    principal's object ID) from Azure rather than reading them from a committed file,
    then runs the Bicep deployment.

    Those values are deliberately not in main.parameters.json: that file describes the
    *system*, and anything true only of this subscription or this person is an input.
    Passing them by hand, however, means a fresh shell silently supplies empty strings
    and the deployment fails deep inside ARM with "InvalidPrincipalId". This script
    exists so that cannot happen.

.PARAMETER ResourceGroup
    Target resource group. Defaults to rg-factorypulse.

.PARAMETER ImageTag
    Container image tag to deploy. Defaults to 'latest'; CI passes the commit SHA.

.EXAMPLE
    ./deploy.ps1
    ./deploy.ps1 -ImageTag 26a9903f1e...
#>
[CmdletBinding()]
param(
    [string] $ResourceGroup = 'rg-factorypulse',
    [string] $ImageTag = 'latest',
    [string] $GitHubAppId = 'b2cab9b8-87fe-4676-a1f4-b92c4779678d'
)

$ErrorActionPreference = 'Stop'
$templateDirectory = $PSScriptRoot

Write-Host 'Resolving deployment identities...' -ForegroundColor Cyan

$githubPrincipalId = az ad sp show --id $GitHubAppId --query id -o tsv
$administratorObjectId = az ad signed-in-user show --query id -o tsv
$administratorLogin = az ad signed-in-user show --query userPrincipalName -o tsv

foreach ($required in @(
    @{ Name = 'githubPrincipalId'; Value = $githubPrincipalId },
    @{ Name = 'administratorObjectId'; Value = $administratorObjectId },
    @{ Name = 'administratorLogin'; Value = $administratorLogin })) {

    if ([string]::IsNullOrWhiteSpace($required.Value)) {
        throw "Could not resolve '$($required.Name)'. Are you signed in? Run 'az login'."
    }
}

Write-Host "Deploying image tag '$ImageTag' to '$ResourceGroup'..." -ForegroundColor Cyan

az deployment group create `
    --resource-group $ResourceGroup `
    --template-file (Join-Path $templateDirectory 'main.bicep') `
    --parameters (Join-Path $templateDirectory 'main.parameters.json') `
    --parameters githubPrincipalId=$githubPrincipalId `
                 administratorObjectId=$administratorObjectId `
                 administratorLogin=$administratorLogin `
                 imageTag=$ImageTag `
    --query 'properties.outputs.apiUrl.value' -o tsv
