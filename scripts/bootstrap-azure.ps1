[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F-]{36}$')]
    [string]$SubscriptionId,

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9-]{3,20}$')]
    [string]$ProjectPrefix,

    [Parameter(Mandatory)]
    [ValidateSet('westeurope', 'polandcentral')]
    [string]$Location,

    [Parameter(Mandatory)]
    [ValidatePattern('^[^/\s]+/[^/\s]+$')]
    [string]$GitHubRepository,

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9]{3,24}$')]
    [string]$StateStorageAccountName,

    [ValidatePattern('^[a-z0-9-]{3,63}$')]
    [string]$StateContainerName = 'tfstate'
)

$ErrorActionPreference = 'Stop'

function Invoke-AzCli {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $result = & az @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Arguments -join ' ')"
    }

    return $result
}

function Ensure-RoleAssignment {
    param(
        [Parameter(Mandatory)][string]$PrincipalObjectId,
        [Parameter(Mandatory)][string]$RoleName,
        [Parameter(Mandatory)][string]$Scope
    )

    $existingAssignment = Invoke-AzCli @(
        'role', 'assignment', 'list', '--assignee-object-id', $PrincipalObjectId, '--scope', $Scope,
        '--query', "[?roleDefinitionName=='$RoleName'].id | [0]", '--output', 'tsv'
    )

    if (-not $existingAssignment) {
        Invoke-AzCli @(
            'role', 'assignment', 'create', '--assignee-object-id', $PrincipalObjectId,
            '--assignee-principal-type', 'ServicePrincipal', '--role', $RoleName, '--scope', $Scope
        ) | Out-Null
    }
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI is required. Install it from https://learn.microsoft.com/cli/azure/install-azure-cli-windows.'
}

Invoke-AzCli @('account', 'set', '--subscription', $SubscriptionId) | Out-Null

$stateResourceGroup = "$ProjectPrefix-tfstate-rg"
$demoResourceGroup = "$ProjectPrefix-demo-rg"
$appDisplayName = "$ProjectPrefix-github-oidc"

Invoke-AzCli @('group', 'create', '--name', $stateResourceGroup, '--location', $Location) | Out-Null
Invoke-AzCli @('group', 'create', '--name', $demoResourceGroup, '--location', $Location) | Out-Null

$appId = Invoke-AzCli @(
    'ad', 'app', 'list', '--display-name', $appDisplayName, '--query', '[0].appId', '--output', 'tsv'
)
if (-not $appId) {
    $appId = Invoke-AzCli @(
        'ad', 'app', 'create', '--display-name', $appDisplayName, '--query', 'appId', '--output', 'tsv'
    )
}
$appId = $appId.Trim()

$servicePrincipalObjectId = & az ad sp show --id $appId --query 'id' --output 'tsv' 2>$null
if ($LASTEXITCODE -ne 0) {
    $servicePrincipalObjectId = Invoke-AzCli @(
        'ad', 'sp', 'create', '--id', $appId, '--query', 'id', '--output', 'tsv'
    )
}
$servicePrincipalObjectId = $servicePrincipalObjectId.Trim()

$federatedCredential = @{
    name      = 'github-main'
    issuer    = 'https://token.actions.githubusercontent.com'
    subject   = "repo:$GitHubRepository:ref:refs/heads/main"
    audiences = @('api://AzureADTokenExchange')
} | ConvertTo-Json -Compress

$existingFederatedCredential = Invoke-AzCli @(
    'ad', 'app', 'federated-credential', 'list', '--id', $appId, '--query', "[?name=='github-main'].id | [0]",
    '--output', 'tsv'
)
if (-not $existingFederatedCredential) {
    $federatedCredentialPath = New-TemporaryFile
    try {
        Set-Content -LiteralPath $federatedCredentialPath -Value $federatedCredential -NoNewline
        Invoke-AzCli @(
            'ad', 'app', 'federated-credential', 'create', '--id', $appId, '--parameters', "@$federatedCredentialPath"
        ) | Out-Null
    }
    finally {
        Remove-Item -LiteralPath $federatedCredentialPath -Force -ErrorAction SilentlyContinue
    }
}

Invoke-AzCli @(
    'storage', 'account', 'create', '--name', $StateStorageAccountName, '--resource-group', $stateResourceGroup,
    '--location', $Location, '--sku', 'Standard_LRS', '--kind', 'StorageV2', '--allow-blob-public-access', 'false',
    '--https-only', 'true', '--min-tls-version', 'TLS1_2'
) | Out-Null

Invoke-AzCli @(
    'storage', 'container', 'create', '--name', $StateContainerName, '--account-name', $StateStorageAccountName,
    '--auth-mode', 'login', '--public-access', 'off'
) | Out-Null

$stateStorageScope = Invoke-AzCli @(
    'storage', 'account', 'show', '--name', $StateStorageAccountName, '--resource-group', $stateResourceGroup,
    '--query', 'id', '--output', 'tsv'
)
$stateStorageScope = $stateStorageScope.Trim()
$demoResourceGroupScope = Invoke-AzCli @(
    'group', 'show', '--name', $demoResourceGroup, '--query', 'id', '--output', 'tsv'
)
$demoResourceGroupScope = $demoResourceGroupScope.Trim()

Ensure-RoleAssignment -PrincipalObjectId $servicePrincipalObjectId -RoleName 'Storage Blob Data Contributor' `
    -Scope "$stateStorageScope/blobServices/default/containers/$StateContainerName"
Ensure-RoleAssignment -PrincipalObjectId $servicePrincipalObjectId -RoleName 'Contributor' -Scope $demoResourceGroupScope

$dotnet10Runtime = Invoke-AzCli @(
    'webapp', 'list-runtimes', '--os', 'windows', '--query', "[?contains(value, 'DOTNET') && contains(value, '10')].value",
    '--output', 'tsv'
)
if (-not $dotnet10Runtime) {
    throw 'The selected subscription does not currently report a Windows .NET 10 App Service runtime. Do not provision the demo tier.'
}

Write-Host 'Bootstrap completed. Configure these GitHub repository variables:'
Write-Host "AZURE_CLIENT_ID=$appId"
Write-Host "AZURE_TENANT_ID=$((Invoke-AzCli @('account', 'show', '--query', 'tenantId', '--output', 'tsv')).Trim())"
Write-Host "AZURE_SUBSCRIPTION_ID=$SubscriptionId"
Write-Host "TF_STATE_RESOURCE_GROUP=$stateResourceGroup"
Write-Host "TF_STATE_STORAGE_ACCOUNT=$StateStorageAccountName"
Write-Host "TF_STATE_CONTAINER=$StateContainerName"
Write-Host "DEMO_RESOURCE_GROUP=$demoResourceGroup"
Write-Host "AZURE_LOCATION=$Location"