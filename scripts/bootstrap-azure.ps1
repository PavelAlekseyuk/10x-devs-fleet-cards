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

    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string]$GitHubEnvironment = 'demo',

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9]{3,24}$')]
    [string]$StateStorageAccountName,

    [ValidatePattern('^[a-z0-9-]{3,63}$')]
    [string]$StateContainerName = 'tfstate'
)

$ErrorActionPreference = 'Stop'
$azureCliCommand = (Get-Command az -ErrorAction SilentlyContinue).Path
$githubCliCommand = (Get-Command gh -ErrorAction SilentlyContinue).Path
if (-not $githubCliCommand) {
    $githubCliFallbackPath = Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe'
    if (Test-Path -LiteralPath $githubCliFallbackPath) {
        $githubCliCommand = $githubCliFallbackPath
    }
}

function Invoke-AzCli {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $result = & $azureCliCommand @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Arguments -join ' ')"
    }

    return $result
}

function Invoke-GitHubCli {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $result = & $githubCliCommand @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI command failed: gh $($Arguments -join ' ')"
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
        'role', 'assignment', 'list', '--assignee', $PrincipalObjectId, '--scope', $Scope,
        '--query', "[?roleDefinitionName=='$RoleName'].id | [0]", '--output', 'tsv'
    )

    if (-not $existingAssignment) {
        Invoke-AzCli @(
            'role', 'assignment', 'create', '--assignee-object-id', $PrincipalObjectId,
            '--assignee-principal-type', 'ServicePrincipal', '--role', $RoleName, '--scope', $Scope
        ) | Out-Null
    }
}

if (-not $azureCliCommand) {
    throw 'Azure CLI is required. Install it from https://learn.microsoft.com/cli/azure/install-azure-cli-windows.'
}

if (-not $githubCliCommand) {
    throw 'GitHub CLI is required to read the repository OIDC subject customization. Install it from https://cli.github.com/ and authenticate with gh auth login.'
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

$servicePrincipalObjectId = & $azureCliCommand ad sp show --id $appId --query 'id' --output 'tsv' 2>$null
if ($LASTEXITCODE -ne 0) {
    $servicePrincipalObjectId = Invoke-AzCli @(
        'ad', 'sp', 'create', '--id', $appId, '--query', 'id', '--output', 'tsv'
    )
}
$servicePrincipalObjectId = $servicePrincipalObjectId.Trim()

$githubOidcSubjectPrefix = (Invoke-GitHubCli @(
    'api', "repos/$GitHubRepository/actions/oidc/customization/sub", '--jq', '.sub_claim_prefix'
)).Trim()
if (-not $githubOidcSubjectPrefix) {
    throw "GitHub did not return an OIDC subject prefix for $GitHubRepository."
}

$federatedCredentialName = "github-$GitHubEnvironment"
$federatedCredentialSubject = '{0}:environment:{1}' -f $githubOidcSubjectPrefix, $GitHubEnvironment
$federatedCredential = @{
    name      = $federatedCredentialName
    issuer    = 'https://token.actions.githubusercontent.com'
    subject   = $federatedCredentialSubject
    audiences = @('api://AzureADTokenExchange')
} | ConvertTo-Json -Compress

$existingFederatedCredentialSubject = Invoke-AzCli @(
    'ad', 'app', 'federated-credential', 'list', '--id', $appId, '--query', "[?name=='$federatedCredentialName'].subject | [0]",
    '--output', 'tsv'
)
if ($existingFederatedCredentialSubject -and $existingFederatedCredentialSubject.Trim() -ne $federatedCredentialSubject) {
    Invoke-AzCli @(
        'ad', 'app', 'federated-credential', 'delete', '--id', $appId, '--federated-credential-id', $federatedCredentialName
    ) | Out-Null
    $existingFederatedCredentialSubject = $null
}

if (-not $existingFederatedCredentialSubject) {
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

$legacyFederatedCredential = Invoke-AzCli @(
    'ad', 'app', 'federated-credential', 'list', '--id', $appId, '--query', "[?name=='github-main'].id | [0]",
    '--output', 'tsv'
)
if ($legacyFederatedCredential) {
    Invoke-AzCli @(
        'ad', 'app', 'federated-credential', 'delete', '--id', $appId, '--federated-credential-id', 'github-main'
    ) | Out-Null
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
Ensure-RoleAssignment -PrincipalObjectId $servicePrincipalObjectId -RoleName 'Contributor' -Scope "/subscriptions/$SubscriptionId"
Ensure-RoleAssignment -PrincipalObjectId $servicePrincipalObjectId -RoleName 'Contributor' -Scope $demoResourceGroupScope

$dotnet10Runtime = Invoke-AzCli @(
    'webapp', 'list-runtimes', '--os', 'windows', '--query', "[?contains(@, 'dotnet:10')]",
    '--output', 'tsv'
)
if (-not $dotnet10Runtime) {
    throw 'The selected subscription does not currently report a Windows .NET 10 App Service runtime. Do not provision the demo tier.'
}

Write-Host 'Bootstrap completed. Configure these GitHub repository variables:'
Write-Host "AZURE_CLIENT_ID=$appId"
Write-Host "AZURE_PRINCIPAL_OBJECT_ID=$servicePrincipalObjectId"
Write-Host "AZURE_PRINCIPAL_DISPLAY_NAME=$appDisplayName"
Write-Host "AZURE_TENANT_ID=$((Invoke-AzCli @('account', 'show', '--query', 'tenantId', '--output', 'tsv')).Trim())"
Write-Host "AZURE_SUBSCRIPTION_ID=$SubscriptionId"
Write-Host "TF_STATE_RESOURCE_GROUP=$stateResourceGroup"
Write-Host "TF_STATE_STORAGE_ACCOUNT=$StateStorageAccountName"
Write-Host "TF_STATE_CONTAINER=$StateContainerName"
Write-Host "DEMO_RESOURCE_GROUP=$demoResourceGroup"
Write-Host "AZURE_LOCATION=$Location"