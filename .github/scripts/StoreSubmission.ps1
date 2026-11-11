# Microsoft Store MSIX submission API. This module never commits a submission.
# https://learn.microsoft.com/windows/uwp/monetize/manage-app-submissions
function Get-StoreConfiguration {
    $config = @{}
    foreach ($name in @('APPLICATION_ID', 'IDENTITY_NAME', 'PUBLISHER', 'PUBLISHER_DISPLAY_NAME', 'TENANT_ID', 'CLIENT_ID', 'CLIENT_SECRET')) {
        $value = [Environment]::GetEnvironmentVariable("MSSTORE_$name")
        if ([string]::IsNullOrWhiteSpace($value)) { throw "Missing MSSTORE_$name. Configure Partner Center before enabling channel:msstore." }
        $config[$name] = $value
    }
    if ($config.APPLICATION_ID -notmatch '^[A-Z0-9]{12}$' -or
        $config.IDENTITY_NAME -eq 'PaperHammer.VirtualPaper.Preview' -or
        $config.IDENTITY_NAME -like 'PaperHammer.VirtualPaper.Test.*') { throw 'Use the formal Store product and package identity.' }
    foreach ($name in @('TENANT_ID', 'CLIENT_ID')) {
        $parsed = [guid]::Empty
        if (-not [guid]::TryParse($config[$name], [ref]$parsed)) { throw "Invalid MSSTORE_$name." }
    }
    return $config
}

function Get-StorePackageVersion([string] $BuildNumber) {
    if ($BuildNumber -notmatch '^(\d{2})(0[1-9]|1[0-2])R(0[1-9]|[1-9][0-9])$') { throw 'Invalid Store build number.' }
    # A separate monotonically increasing Store version also advances for product
    # revision and plugin-only releases. Product AssemblyVersion remains unchanged.
    return "1.$([int]($Matches[1] + $Matches[2])).$([int]$Matches[3]).0"
}

function Get-NormalizedStoreNotes($Value) { return ([string]$Value).Replace("`r`n", "`n") }

function Get-StoreAccessToken($Configuration) {
    try {
        $result = Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$($Configuration.TENANT_ID)/oauth2/token" `
            -ContentType 'application/x-www-form-urlencoded' -Body @{
                grant_type = 'client_credentials'; client_id = $Configuration.CLIENT_ID
                client_secret = $Configuration.CLIENT_SECRET; resource = 'https://manage.devcenter.microsoft.com'
            }
        if (-not $result.access_token) { throw 'No token.' }
        return $result.access_token
    } catch { throw 'Partner Center authentication failed. Check the tenant, application permissions and client secret.' }
}

function Invoke-StoreRequest([string] $Method, [string] $Path, [string] $Token, $Body = $null) {
    if ($Path -notmatch '^applications/[A-Z0-9]{12}(/submissions(/[0-9]+)?)?$' -or $Method -notin @('Get', 'Post', 'Put')) {
        throw 'Only app lookup and uncommitted submission operations are supported.'
    }
    $arguments = @{ Method = $Method; Uri = "https://manage.devcenter.microsoft.com/v1.0/my/$Path"
        Headers = @{ Authorization = "Bearer $Token" }; ContentType = 'application/json; charset=utf-8' }
    if ($null -ne $Body) { $arguments.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 100)) }
    try { return Invoke-RestMethod @arguments }
    catch { throw "Partner Center $Method $Path failed. Check initial submission/age ratings, permissions and pending submission state." }
}

function Send-StoreUpload([string] $Uri, [string] $Path) {
    $address = [uri]$Uri
    if ($address.Scheme -cne 'https' -or $address.Host -notmatch '\.blob\.core\.windows\.net$') { throw 'Unexpected Partner Center upload destination.' }
    try {
        Invoke-WebRequest -Method Put -Uri $Uri -InFile $Path -ContentType 'application/zip' `
            -Headers @{ 'x-ms-blob-type' = 'BlockBlob'; 'x-ms-version' = '2021-12-02' } | Out-Null
    } catch { throw 'Store package ZIP upload failed. Retry the same workflow run; upload credentials are omitted from logs.' }
}

function Get-StoreReleaseState($Configuration, [string] $Token, [string] $Marker, [string] $PackageVersion) {
    $path = "applications/$($Configuration.APPLICATION_ID)"
    $app = Invoke-StoreRequest Get $path $Token
    if ($app.packageIdentityName -cne $Configuration.IDENTITY_NAME -or $app.publisherName -cne $Configuration.PUBLISHER) {
        throw 'Partner Center identity differs from the configured package identity.'
    }
    if (-not $app.lastPublishedApplicationSubmission.id -and -not $app.pendingApplicationSubmission.id) {
        throw 'Prepare the initial Partner Center submission, including age ratings, before using the submission API.'
    }
    if ($app.lastPublishedApplicationSubmission.id) {
        $previous = Invoke-StoreRequest Get "$path/submissions/$($app.lastPublishedApplicationSubmission.id)" $Token
        foreach ($package in $previous.applicationPackages) {
            if ($package.version -and [version]$PackageVersion -le [version]$package.version) {
                throw "Store package version $PackageVersion must exceed published version $($package.version)."
            }
        }
    }
    $pending = $null
    if ($app.pendingApplicationSubmission.id) {
        $pending = Invoke-StoreRequest Get "$path/submissions/$($app.pendingApplicationSubmission.id)" $Token
        if ($pending.status -cne 'PendingCommit' -or -not (Get-NormalizedStoreNotes $pending.notesForCertification).StartsWith("$Marker`n", [StringComparison]::Ordinal)) {
            throw 'An unrelated or already committed Store submission exists. It will not be changed or deleted.'
        }
    }
    return $pending
}

function New-StoreDraftBody($Submission, [string] $FileName, [string] $Notes) {
    # Preserve listing, pricing, ratings and declarations from Partner Center.
    $body = @{}
    foreach ($property in @('applicationCategory', 'pricing', 'visibility', 'listings', 'hardwarePreferences',
        'automaticBackupEnabled', 'canInstallOnRemovableMedia', 'isGameDvrEnabled', 'gamingOptions',
        'hasExternalInAppProducts', 'meetAccessibilityGuidelines', 'packageDeliveryOptions', 'enterpriseLicensing',
        'allowMicrosoftDecideAppAvailabilityToFutureDeviceFamilies', 'allowTargetFutureDeviceFamilies', 'trailers')) {
        # Pricing v2 may be represented as UnknownTier by this API. Leave that
        # inherited module untouched rather than sending an unsupported price.
        if ($property -eq 'pricing' -and $Submission.pricing.priceId -match '^(UnknownTier|Unknown)$') { continue }
        if ($null -ne $Submission.$property) { $body[$property] = $Submission.$property }
    }
    $body.targetPublishMode = 'Manual'
    $body.notesForCertification = $Notes
    $body.applicationPackages = @($Submission.applicationPackages | Where-Object fileName -CNE $FileName | ForEach-Object {
        $copy = $_ | ConvertTo-Json -Depth 30 | ConvertFrom-Json -AsHashtable
        if ($copy.architecture -ieq 'x64') { $copy.fileStatus = 'PendingDelete' }
        $copy
    }) + @(@{ fileName = $FileName; fileStatus = 'PendingUpload'; minimumDirectXVersion = 'None'; minimumSystemRam = 'None' })
    return $body
}
