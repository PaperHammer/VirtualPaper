# Public Partner Center identifiers only. Authentication stays in environment variables/secrets.
function Get-StoreIdentity {
    param(
        [string] $Path = (Join-Path $PSScriptRoot '../../packaging/msix/StoreIdentity.json'),
        [string] $StoreId,
        [string] $IdentityName,
        [string] $Publisher,
        [string] $PublisherDisplayName,
        [switch] $RequireStoreId
    )
    $fields = [ordered]@{ StoreId = 'APPLICATION_ID'; IdentityName = 'IDENTITY_NAME'
        Publisher = 'PUBLISHER'; PublisherDisplayName = 'PUBLISHER_DISPLAY_NAME' }
    try { $document = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop | ConvertFrom-Json -AsHashtable -NoEnumerate -ErrorAction Stop }
    catch { throw "Cannot read Store identity JSON: $Path" }
    if ($document -isnot [System.Collections.IDictionary] -or
        @($document.Keys | Where-Object { $_ -cnotin $fields.Keys }).Count) {
        throw 'StoreIdentity.json must contain only StoreId, IdentityName, Publisher and PublisherDisplayName.'
    }
    $identity = @{}
    foreach ($name in $fields.Keys) {
        if ($document.Contains($name) -and $null -ne $document[$name] -and $document[$name] -isnot [string]) {
            throw "Store identity field $name must be text."
        }
        $value = [string]$PSBoundParameters[$name]
        if ([string]::IsNullOrWhiteSpace($value)) { $value = [Environment]::GetEnvironmentVariable("MSSTORE_$($fields[$name])") }
        if ([string]::IsNullOrWhiteSpace($value)) { $value = [string]$document[$name] }
        $identity[$name] = ([string]$value).Trim()
        if (-not $identity[$name] -and ($name -ne 'StoreId' -or $RequireStoreId)) {
            throw "Fill $name in packaging/msix/StoreIdentity.json or set MSSTORE_$($fields[$name])."
        }
    }
    if (($identity.StoreId -and $identity.StoreId -notmatch '^[A-Z0-9]{12}$') -or
        $identity.IdentityName -eq 'PaperHammer.VirtualPaper.Preview' -or
        $identity.IdentityName -like 'PaperHammer.VirtualPaper.Test.*') {
        throw 'Use the formal Partner Center Store ID and package identity.'
    }
    return $identity
}
