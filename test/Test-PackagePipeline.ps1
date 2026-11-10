$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/PackageArtifactSupport.ps1"
. "$PSScriptRoot/MsixTestSupport.ps1"
. "$PSScriptRoot/InstallerTestSupport.ps1"
. "$PSScriptRoot/../.github/scripts/StoreSubmission.ps1"
. "$PSScriptRoot/TestReportSupport.ps1"
function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock] $Action) { try { & $Action | Out-Null } catch { return }; throw 'Invalid pipeline input was accepted.' }
function Save-Json($Value, [string] $Path) {
    [void](New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force)
    $Value | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}
function Write-Zip([string] $Path, $Files) {
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path }
    $zip = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $Files.Keys) {
            $stream = $zip.CreateEntry($name).Open()
            try { $bytes = $Files[$name]; if ($bytes -is [string]) { $bytes = [Text.Encoding]::UTF8.GetBytes($bytes) }; $stream.Write($bytes, 0, $bytes.Length) }
            finally { $stream.Dispose() }
        }
    } finally { $zip.Dispose() }
}
$results = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../TestResults'))
$fixture = Join-Path $results "package-pipeline-$([guid]::NewGuid().ToString('N'))"
[void](New-Item -ItemType Directory -Path "$fixture/artifacts/msix" -Force)
[void](New-Item -ItemType Directory -Path "$fixture/artifacts/exe" -Force)
$names = @('GITHUB_OUTPUT', 'GITHUB_STEP_SUMMARY', 'GITHUB_REPOSITORY', 'GITHUB_RUN_ID', 'GITHUB_ACTIONS', 'RUNNER_ENVIRONMENT') +
    @('APPLICATION_ID', 'IDENTITY_NAME', 'PUBLISHER', 'PUBLISHER_DISPLAY_NAME', 'TENANT_ID', 'CLIENT_ID', 'CLIENT_SECRET' | ForEach-Object { "MSSTORE_$_" })
$oldEnvironment = @{}
foreach ($name in $names) { $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    $env:GITHUB_OUTPUT = ''; $env:GITHUB_STEP_SUMMARY = ''
    $sha = 'a' * 40
    $msix = "$fixture/artifacts/msix"
    $files = @{ 'AppxManifest.xml' = '<Package><Identity Name="Contoso.Wallpaper" Publisher="CN=Contoso" Version="1.2611.1.0" ProcessorArchitecture="x64"/></Package>'
        'AppxSignature.p7x' = 'signature-one'; 'AppxMetadata/CodeIntegrity.cat' = 'catalog-one'; 'VirtualPaper.dll' = 'application' }
    Write-Zip "$msix/app.msix" $files
    $files['AppxSignature.p7x'] = 'signature-two'; $files['AppxMetadata/CodeIntegrity.cat'] = 'catalog-two'
    Write-Zip "$msix/upload-inner.msix" $files
    Write-Zip "$msix/app.msixupload" @{ 'app.msix' = [IO.File]::ReadAllBytes("$msix/upload-inner.msix") }
    Set-Content "$msix/acceptance.cer" 'public certificate fixture'
    $artifact = @{ FormatVersion = 1; Channel = 'MSIX'; SourceSha = $sha; Store = $true
        IdentityName = 'Contoso.Wallpaper'; Publisher = 'CN=Contoso'; PackageVersion = '1.2611.1.0'; ProductVersion = '0.6.0.0'
        PackageFile = 'app.msix'; PackageSha256 = (Get-FileHash "$msix/app.msix").Hash
        CertificateFile = 'acceptance.cer'; CertificateSha256 = (Get-FileHash "$msix/acceptance.cer").Hash
        UploadFile = 'app.msixupload'; UploadSha256 = (Get-FileHash "$msix/app.msixupload").Hash }
    Save-Json $artifact "$msix/package.json"
    $null = Get-PackageArtifact "$msix/package.json" MSIX $sha
    Reject { Get-PackageArtifact "$msix/package.json" MSIX ('b' * 40) }
    Reject { Resolve-PackageArtifactFile $msix '../app.msix' $artifact.PackageSha256 }
    $files['VirtualPaper.dll'] = 'different application'
    Write-Zip "$msix/upload-inner.msix" $files
    Write-Zip "$msix/changed.msixupload" @{ 'app.msix' = [IO.File]::ReadAllBytes("$msix/upload-inner.msix") }
    $changed = $artifact.Clone(); $changed.UploadFile = 'changed.msixupload'; $changed.UploadSha256 = (Get-FileHash "$msix/changed.msixupload").Hash
    Save-Json $changed "$msix/changed.json"
    Reject { Get-PackageArtifact "$msix/changed.json" MSIX $sha }
    foreach ($mode in @('Smoke', 'UI')) {
        $report = @{ Mode = $mode; Passed = $true; PackageFullName = 'owned_1.2611.1.0_x64__publisher'; PackageSha256 = $artifact.PackageSha256
            Stages = @(Get-MsixAcceptanceStages $mode | ForEach-Object { @{ Name = $_; Status = 'Passed' } }) }
        Assert-MsixAcceptanceReport ([pscustomobject]$report) $mode
        Reject { Assert-MsixAcceptanceReport ([pscustomobject]$report) }
        $report.Stages[0].Status = 'Skipped'
        Reject { Assert-MsixAcceptanceReport ([pscustomobject]$report) $mode }
    }
    Require ((Get-StorePackageVersion '2611R01') -ceq '1.2611.1.0') 'Store version must be independent of product revisions.'
    Require ([version](Get-StorePackageVersion '2612R01') -gt [version](Get-StorePackageVersion '2611R99')) 'Store versions must increase across months.'
    foreach ($build in @('2611R100', '2611R00', '2613R01', 'invalid')) { Reject { Get-StorePackageVersion $build } }
    $env:MSSTORE_CLIENT_SECRET = ''
    Reject { Get-StoreConfiguration }
    $env:MSSTORE_APPLICATION_ID = '9NBLGGH4R315'; $env:MSSTORE_IDENTITY_NAME = 'Contoso.Wallpaper'
    $env:MSSTORE_PUBLISHER = 'CN=Contoso'; $env:MSSTORE_PUBLISHER_DISPLAY_NAME = 'Contoso'
    $env:MSSTORE_TENANT_ID = '00000000-0000-0000-0000-000000000001'
    $env:MSSTORE_CLIENT_ID = '00000000-0000-0000-0000-000000000002'; $env:MSSTORE_CLIENT_SECRET = 'fixture-secret'
    $env:GITHUB_REPOSITORY = 'fixture/repository'; $env:GITHUB_RUN_ID = '12345'
    $api = @{ Pending = $null; Creates = 0; Updates = 0; Uploads = 0; FailUpload = $false; NoBaseline = $false }
    # Mock only the HTTP transport. Execute the production draft orchestration/body/guards.
    function Invoke-RestMethod($Method, $Uri, $Body, $Headers, $ContentType) {
        if ($Uri -like 'https://login.microsoftonline.com/*/oauth2/token') { return @{ access_token = 'fixture-token' } }
        if ($Uri -match '/applications/9NBLGGH4R315$' -and $Method -eq 'Get') {
            return @{ packageIdentityName = 'Contoso.Wallpaper'; publisherName = 'CN=Contoso'
                lastPublishedApplicationSubmission = if ($api.NoBaseline) { $null } else { @{ id = '1' } }
                pendingApplicationSubmission = if ($api.Pending) { @{ id = '2' } } else { $null } }
        }
        if ($Uri -match '/submissions/1$' -and $Method -eq 'Get') { return @{ applicationPackages = @(@{ version = '1.2610.5.0' }) } }
        if ($Uri -match '/submissions$' -and $Method -eq 'Post') {
            $api.Creates++
            return @{ id = '2'; status = 'PendingCommit'; fileUploadUrl = 'https://fixture.blob.core.windows.net/package?sas=fixture'
                listings = @{ 'en-us' = @{ baseListing = @{ title = 'Preserved listing' } } }; pricing = @{ priceId = 'Free' }
                applicationPackages = @(@{ fileName = 'old.msix'; architecture = 'x64'; fileStatus = 'Uploaded' },
                    @{ fileName = 'arm.msix'; architecture = 'ARM64'; fileStatus = 'Uploaded' }) }
        }
        if ($Uri -match '/submissions/2$' -and $Method -eq 'Put') {
            $api.Updates++
            $api.Pending = [Text.Encoding]::UTF8.GetString($Body) | ConvertFrom-Json -AsHashtable
            $api.Pending.id = '2'; $api.Pending.status = 'PendingCommit'
            $api.Pending.fileUploadUrl = 'https://fixture.blob.core.windows.net/package?sas=fixture'
            return $api.Pending
        }
        if ($Uri -match '/submissions/2$' -and $Method -eq 'Get') { return $api.Pending }
        throw "Unexpected Store request (commit/publication is forbidden): $Method $Uri"
    }
    function Invoke-WebRequest($Method, $Uri, $InFile, $ContentType, $Headers) {
        Require ($Method -eq 'Put' -and (Test-Path -LiteralPath $InFile)) 'Store upload must PUT an existing ZIP.'
        $api.Uploads++
        if ($api.FailUpload) { throw 'Fixture upload failure' }
    }
    $invoke = @{ BuildNumber = '2611R01'; SourceSha = $sha; PackageArtifact = "$msix/package.json"; ReportPath = "$fixture/store-draft.json" }
    $api.NoBaseline = $true
    Reject { & "$PSScriptRoot/../.github/scripts/Invoke-StoreDraft.ps1" -Action Validate @invoke }
    Require ($api.Creates -eq 0 -and $api.Updates -eq 0 -and $api.Uploads -eq 0) 'An unprepared initial Store submission must fail before any writes.'
    $api.NoBaseline = $false
    & "$PSScriptRoot/../.github/scripts/Invoke-StoreDraft.ps1" -Action Validate @invoke
    Require ($api.Creates -eq 0 -and $api.Updates -eq 0) 'Preparation must be read-only.'
    $api.FailUpload = $true
    Reject { & "$PSScriptRoot/../.github/scripts/Invoke-StoreDraft.ps1" -Action Draft @invoke }
    $api.FailUpload = $false
    & "$PSScriptRoot/../.github/scripts/Invoke-StoreDraft.ps1" -Action Draft @invoke
    $api.Pending.notesForCertification = $api.Pending.notesForCertification.Replace("`n", "`r`n")
    & "$PSScriptRoot/../.github/scripts/Invoke-StoreDraft.ps1" -Action Draft @invoke
    Require ($api.Creates -eq 1 -and $api.Uploads -eq 3) 'Retry must reuse the same uncommitted draft.'
    Require ($api.Pending.targetPublishMode -ceq 'Manual') 'Draft must keep manual publication.'
    Require ($api.Pending.listings.'en-us'.baseListing.title -ceq 'Preserved listing') 'Preserve Store listing metadata.'
    $pricingV2 = $api.Pending.Clone(); $pricingV2.pricing = @{ priceId = 'UnknownTier' }
    Require (-not (New-StoreDraftBody $pricingV2 'app.msixupload' 'notes').ContainsKey('pricing')) 'Do not write an unsupported Pricing v2 tier.'
    Require (($api.Pending.applicationPackages | Where-Object fileName -EQ 'old.msix').fileStatus -ceq 'PendingDelete') 'Replace the previous x64 package.'
    Require (($api.Pending.applicationPackages | Where-Object fileName -EQ 'arm.msix').fileStatus -ceq 'Uploaded') 'Preserve other architectures.'
    $draft = Get-Content "$fixture/store-draft.json" -Raw | ConvertFrom-Json
    Require ($draft.Uploaded -eq $true -and $draft.Committed -eq $false) 'Report must distinguish upload from certification.'
    $env:GITHUB_RUN_ID = '99999'
    Reject { & "$PSScriptRoot/../.github/scripts/Invoke-StoreDraft.ps1" -Action Draft @invoke }
    Require ($api.Creates -eq 1) 'Never replace another run or a manual Store submission.'

    $frameworkPresent = $true
    function Get-AppxPackage($Name) {
        if ($frameworkPresent) { return @{ Version = '14.0.33728.0'; Publisher = 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US'; Architecture = 'X64' } }
    }
    $framework = Initialize-MsixTestFramework $fixture
    Require ($framework.Origin -ceq 'Preinstalled') 'Reuse the available Microsoft AppX framework.'
    $frameworkPresent = $false; $env:GITHUB_ACTIONS = 'false'; $env:RUNNER_ENVIRONMENT = 'self-hosted'
    Reject { Initialize-MsixTestFramework $fixture }

    # Build raw report fixtures to exercise the actual strict combined summarizer.
    $raw = "$fixture/reports"
    foreach ($spec in Get-ProjectTestSpecs) {
        $dir = "$raw/$($spec.Artifact)"; [void](New-Item -ItemType Directory -Path $dir -Force)
        '<TestRun><ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1" failed="0" aborted="0"/></ResultSummary></TestRun>' |
            Set-Content "$dir/$($spec.Report)"
        if ($spec.Coverage) { '<coverage lines-covered="1" lines-valid="2"/>' | Set-Content "$dir/coverage.cobertura.xml" }
    }
    Set-Content "$fixture/artifacts/exe/setup.exe" 'installer fixture'
    $exeHash = (Get-FileHash "$fixture/artifacts/exe/setup.exe").Hash
    Save-Json @{ FormatVersion = 1; Channel = 'EXE'; SourceSha = $sha; PackageFile = 'setup.exe'; PackageSha256 = $exeHash; PackageVersion = '0.6.0.0' } "$fixture/artifacts/exe/package.json"
    $installer = Invoke-InstallerLifecycle -Install {} -Open {} -Reinstall {} -VerifyPreserved {} -OpenFresh {} -Uninstall {}
    $installer | Add-Member NoteProperty InstallerSha256 $exeHash
    $installer | Add-Member NoteProperty PackageVersion '0.6.0.0'
    Save-Json $installer "$raw/test-results-installer/installer-results.json"
    Save-Json $installer "$raw/test-results-ui-operation/installed-acceptance.json"
    $ui = @{ Passed = $true; ExpectedCount = 7; Results = @(@('GeneralSettings','WallpaperLibrary','StaticEditor','WebEditor','ModelInference','ScreenSaverLifecycle','Rendering') | ForEach-Object {
        @{ Name = "Application.$_.Fixture"; Passed = $true; Evidence = @{ CoreProcessId = 1; UiProcessIds = @(2); ProcessesExitedNormally = $true
            PackageFullName = 'owned_1.2611.1.0_x64__publisher'; PackageActivation = 'AUMID'; UnitTestBehaviorOverridesEnabled = $false } }
    }) }
    Save-Json $ui "$raw/test-results-ui-operation/ui-operation-results.json"
    foreach ($mode in @('Smoke', 'UI')) {
        $dir = "$raw/test-results-msix-$($mode.ToLowerInvariant())"
        $report = @{ Mode = $mode; Passed = $true; SourceSha = $sha; PackageFullName = 'owned_1.2611.1.0_x64__publisher'; PackageSha256 = $artifact.PackageSha256
            Stages = @(Get-MsixAcceptanceStages $mode | ForEach-Object { @{ Name = $_; Status = 'Passed' } })
            BuiltInWallpapers = @{ InitialDeployment = $true; MissingFolderRestored = $true; ExistingDataPreserved = $true; SavedDirectoryHonored = $true; UninstallDataPreserved = $true } }
        Save-Json $report "$dir/msix-acceptance.json"
        foreach ($stage in $(if ($mode -eq 'UI') { @('extended-ui') } else { @('first-run','retained-profile','fresh-profile') })) {
            Save-Json $ui "$dir/$stage/ui-operation-results.json"
        }
    }
    $jobs = @{}
    foreach ($id in @('packages-ready','package-exe','package-msix','test-exe-acceptance','test-msix-acceptance') + @(Get-ProjectTestSpecs | ForEach-Object Id)) { $jobs[$id] = @{ result = 'success' } }
    $arguments = @{ Root = $raw; ArtifactsRoot = "$fixture/artifacts"; SourceSha = $sha }
    & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-pass.log"
    foreach ($id in @('test-exe-acceptance','test-msix-acceptance','package-msix','test-core')) {
        foreach ($result in @('failure', 'cancelled', 'skipped')) {
            $jobs[$id].result = $result
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
        }
        $jobs.Remove($id)
        Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
        $jobs[$id] = @{ result = 'success' }
    }
    # A successful reusable-workflow result alone cannot replace any of the four raw reports.
    foreach ($path in @('test-results-installer/installer-results.json', 'test-results-ui-operation/ui-operation-results.json',
                         'test-results-msix-smoke/msix-acceptance.json', 'test-results-msix-ui/msix-acceptance.json')) {
        $reportPath = Join-Path $raw $path
        [IO.File]::Move($reportPath, "$reportPath.held")
        try {
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
        } finally { [IO.File]::Move("$reportPath.held", $reportPath) }
    }
    foreach ($path in @('test-results-ui-operation/ui-operation-results.json', 'test-results-msix-ui/extended-ui/ui-operation-results.json')) {
        Save-Json @{ Passed = $false; Status = 'Skipped'; ExpectedCount = 0; Results = @() } "$raw/$path"
        try {
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
        } finally { Save-Json $ui "$raw/$path" }
    }
    $smokePath = "$raw/test-results-msix-smoke/msix-acceptance.json"
    $smoke = Get-Content $smokePath -Raw | ConvertFrom-Json; $smoke.PackageSha256 = '0' * 64; Save-Json $smoke $smokePath
    Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
    foreach ($id in @('package-msix','test-msix-acceptance')) { $jobs[$id].result = 'skipped' }
    & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -MsixEnabled:$false -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-exe.log"
    $summary = Get-Content "$raw/pipeline-summary.json" -Raw | ConvertFrom-Json
    Require ($summary.Passed -eq $true -and ($summary.Checks | Where-Object Name -EQ 'MSIX').Status -ceq 'NotSelected') 'Disabled Store channel must not be reported as passed.'
    $jobs['test-msix-acceptance'].result = 'success'
    Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -MsixEnabled:$false -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name]) }
    # Only this newly allocated fixture directory may be deleted.
    if (-not ([IO.Path]::GetFullPath($fixture)).StartsWith($results + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($fixture) -notmatch '^package-pipeline-[0-9a-f]{32}$') { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
Write-Host '[OK] Package payloads, strict channel summaries and uncommitted Store draft/retry contracts passed.'
