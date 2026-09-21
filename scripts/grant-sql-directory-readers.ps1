[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F-]{36}$')]
    [string]$SqlServerPrincipalId
)

$ErrorActionPreference = 'Stop'

function Invoke-MicrosoftGraph {
    param(
        [Parameter(Mandatory)][ValidateSet('GET', 'POST')][string]$Method,
        [Parameter(Mandatory)][string]$Url,
        [string]$Body
    )

    $arguments = @('rest', '--method', $Method, '--url', $Url, '--resource-type', 'ms-graph')
    if ($Body) {
        $arguments += @('--headers', 'Content-Type=application/json', '--body', $Body)
    }

    $response = & az @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Microsoft Graph request failed: $Method $Url"
    }

    return $response | ConvertFrom-Json
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI is required.'
}

$directoryReaders = Invoke-MicrosoftGraph -Method GET -Url (
    "https://graph.microsoft.com/v1.0/directoryRoles?`$filter=displayName%20eq%20'Directory%20Readers'"
)
$directoryRole = $directoryReaders.value | Select-Object -First 1

if (-not $directoryRole) {
    $templates = Invoke-MicrosoftGraph -Method GET -Url (
        "https://graph.microsoft.com/v1.0/directoryRoleTemplates?`$filter=displayName%20eq%20'Directory%20Readers'"
    )
    $template = $templates.value | Select-Object -First 1
    if (-not $template) {
        throw 'The Directory Readers role template was not found in this tenant.'
    }

    Invoke-MicrosoftGraph -Method POST -Url 'https://graph.microsoft.com/v1.0/directoryRoles' -Body (
        @{ roleTemplateId = $template.id } | ConvertTo-Json -Compress
    ) | Out-Null
    $directoryReaders = Invoke-MicrosoftGraph -Method GET -Url (
        "https://graph.microsoft.com/v1.0/directoryRoles?`$filter=displayName%20eq%20'Directory%20Readers'"
    )
    $directoryRole = $directoryReaders.value | Select-Object -First 1
}

$members = Invoke-MicrosoftGraph -Method GET -Url (
    "https://graph.microsoft.com/v1.0/directoryRoles/$($directoryRole.id)/members"
)
$existingMember = $members.value | Where-Object id -eq $SqlServerPrincipalId
if ($existingMember) {
    Write-Host 'The Azure SQL server identity already has Directory Readers.'
    exit 0
}

$reference = @{ '@odata.id' = "https://graph.microsoft.com/v1.0/directoryObjects/$SqlServerPrincipalId" } |
    ConvertTo-Json -Compress
Invoke-MicrosoftGraph -Method POST -Url (
    "https://graph.microsoft.com/v1.0/directoryRoles/$($directoryRole.id)/members/`$ref"
) -Body $reference | Out-Null

Write-Host 'Granted Directory Readers to the Azure SQL server identity.'