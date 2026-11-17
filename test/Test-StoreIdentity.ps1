#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../.github/scripts/StoreSubmission.ps1"
function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock] $Action) { try { & $Action | Out-Null } catch { return }; throw 'Invalid Store identity was accepted.' }
$names = @('APPLICATION_ID', 'IDENTITY_NAME', 'PUBLISHER', 'PUBLISHER_DISPLAY_NAME', 'TENANT_ID', 'CLIENT_ID', 'CLIENT_SECRET') |
    ForEach-Object { "MSSTORE_$_" }
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
$root = Join-Path ([IO.Path]::GetTempPath()) "VPStoreIdentity-$([guid]::NewGuid().ToString('N'))"
$path = Join-Path $root 'identity.json'
$valid = @{ StoreId = '9NBLGGH4R315'; IdentityName = 'Contoso.Wallpaper'
    Publisher = 'CN=Contoso'; PublisherDisplayName = 'Contoso' }
function Save-Identity($Value) { $Value | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding utf8NoBOM }
try {
    [void](New-Item -ItemType Directory -Path $root)
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, '') }
    Save-Identity $valid
    $identity = Get-StoreIdentity -Path $path -RequireStoreId
    foreach ($name in $valid.Keys) { Require ($identity[$name] -ceq $valid[$name]) "JSON field was not used: $name" }
    $env:MSSTORE_TENANT_ID = '00000000-0000-0000-0000-000000000001'
    $env:MSSTORE_CLIENT_ID = '00000000-0000-0000-0000-000000000002'
    $env:MSSTORE_CLIENT_SECRET = 'fixture-secret'
    $config = Get-StoreConfiguration -IdentityPath $path
    Require ($config.APPLICATION_ID -ceq $valid.StoreId -and $config.IDENTITY_NAME -ceq $valid.IdentityName -and
        $config.PUBLISHER -ceq $valid.Publisher -and $config.PUBLISHER_DISPLAY_NAME -ceq $valid.PublisherDisplayName -and
        $config.CLIENT_SECRET -ceq 'fixture-secret') 'Submission did not combine JSON identity with separate credentials.'
    $env:MSSTORE_IDENTITY_NAME = 'Environment.Wallpaper'
    Require ((Get-StoreIdentity -Path $path).IdentityName -ceq 'Environment.Wallpaper') 'Existing environment overrides must keep working.'
    Require ((Get-StoreIdentity -Path $path -IdentityName 'Argument.Wallpaper').IdentityName -ceq 'Argument.Wallpaper') 'Explicit parameters must take precedence.'
    $env:MSSTORE_IDENTITY_NAME = ''
    foreach ($name in $valid.Keys) {
        $invalid = $valid.Clone(); $invalid[$name] = ''
        Save-Identity $invalid
        Reject { Get-StoreIdentity -Path $path -RequireStoreId }
        if ($name -eq 'StoreId') {
            Require ((Get-StoreIdentity -Path $path).IdentityName -ceq $valid.IdentityName) 'Local Store packaging does not require an API Store ID.'
            Reject { Get-StoreConfiguration -IdentityPath $path }
        }
    }
    foreach ($name in @('PaperHammer.VirtualPaper.Preview', 'PaperHammer.VirtualPaper.Test.fixture')) {
        $invalid = $valid.Clone(); $invalid.IdentityName = $name; Save-Identity $invalid
        Reject { Get-StoreIdentity -Path $path }
    }
    $invalid = $valid.Clone(); $invalid.StoreId = 'not-a-store-id'; Save-Identity $invalid
    Reject { Get-StoreIdentity -Path $path -RequireStoreId }
    $invalid = $valid.Clone(); $invalid.PublisherDisplayName = 123; Save-Identity $invalid
    Reject { Get-StoreIdentity -Path $path }
    $invalid = $valid.Clone(); $invalid.ClientSecret = 'must-stay-outside-json'; Save-Identity $invalid
    Reject { Get-StoreIdentity -Path $path }
    foreach ($text in @('{invalid', '[]', 'null')) {
        Set-Content -LiteralPath $path $text
        Reject { Get-StoreIdentity -Path $path }
    }
    Set-Content -LiteralPath $path ('[' + ($valid | ConvertTo-Json -Compress) + ']')
    Reject { Get-StoreIdentity -Path $path }
    Reject { Get-StoreIdentity -Path (Join-Path $root 'missing.json') }
    Save-Identity $valid
    $env:MSSTORE_CLIENT_SECRET = ''
    Reject { Get-StoreConfiguration -IdentityPath $path }
    Write-Host '[OK] Store JSON identity, local/API requirements, parameter/environment precedence, separate credentials and invalid-input guards passed.'
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    $expected = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetFileName($root))
    if ([IO.Path]::GetFullPath($root) -cne [IO.Path]::GetFullPath($expected) -or [IO.Path]::GetFileName($root) -notmatch '^VPStoreIdentity-[0-9a-f]{32}$') {
        throw 'Unexpected Store identity fixture cleanup path.'
    }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
