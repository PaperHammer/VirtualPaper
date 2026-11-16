$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/PackageArtifactSupport.ps1"
. "$PSScriptRoot/UIOperationTestSupport.ps1"
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
    foreach ($channel in @('msix', 'MsIx')) {
        $null = Get-PackageArtifact "$msix/package.json" $channel $sha
        Reject { Get-PackageArtifact "$msix/package.json" $channel ('b' * 40) }
    }
    Reject { Get-PackageArtifact "$msix/package.json" exe $sha }
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
                listings = @{ 'en-us' = @{ baseListing = @{ title = 'Preserved listing'; licenseTerms = 'Keep license'
                    images = @(@{ id = 'old-image'; fileName = 'old.png'; imageType = 'Screenshot'; fileStatus = 'Uploaded' },
                        @{ id = 'logo'; fileName = 'logo.png'; imageType = 'Icon'; fileStatus = 'Uploaded' }) }
                    platformOverrides = @{ Windows81 = @{ description = 'Keep override' } } }
                    'it-it' = @{ baseListing = @{ title = 'Keep other locale' } } }; pricing = @{ priceId = 'Free' }
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
        $archive = [IO.Compression.ZipFile]::OpenRead($InFile)
        try {
            Require ($null -ne $archive.GetEntry('app.msixupload')) 'Upload the accepted Store package.'
            foreach ($listing in $api.Pending.listings.Values) {
                foreach ($image in $listing.baseListing.images | Where-Object fileStatus -CEQ 'PendingUpload') {
                    Require ($null -ne $archive.GetEntry($image.fileName)) 'Upload every new image at its declared ZIP path.'
                }
            }
        } finally { $archive.Dispose() }
        $api.Uploads++
        if ($api.FailUpload) { throw 'Fixture upload failure' }
    }
    $storeMaterials = Join-Path $fixture 'store-materials'
    Copy-Item -LiteralPath "$PSScriptRoot/../resources/store" -Destination $storeMaterials -Recurse
    $notesSource = Get-ChildItem "$storeMaterials/releases" -Filter '*.json' | Sort-Object { [version]$_.BaseName } -Descending | Select-Object -First 1
    Copy-Item -LiteralPath $notesSource.FullName -Destination "$storeMaterials/releases/0.6.0.0.json" -Force
    $invoke = @{ BuildNumber = '2611R01'; ProductVersion = '0.6.0.0'; SourceSha = $sha; PackageArtifact = "$msix/package.json"
        MaterialsRoot = $storeMaterials; MaterialsOutputRoot = "$fixture/material-exports"; ReportPath = "$fixture/store-draft.json" }
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
    Require ($api.Pending.listings.'en-us'.baseListing.licenseTerms -ceq 'Keep license') 'Preserve legal declarations.'
    Require ($api.Pending.listings.'en-us'.platformOverrides.Windows81.description -ceq 'Keep override') 'Preserve platform overrides.'
    Require ($api.Pending.listings.'it-it'.baseListing.title -ceq 'Keep other locale') 'Preserve unmanaged locales.'
    Require ($api.Pending.listings.'zh-cn'.baseListing.title -ceq 'Preserved listing') 'Reuse the reserved brand for a new locale.'
    Require (($api.Pending.listings.'en-us'.baseListing.images | Where-Object id -CEQ 'logo').fileStatus -ceq 'Uploaded') 'Preserve Store logos.'
    Require (($api.Pending.listings.'en-us'.baseListing.images | Where-Object id -CEQ 'old-image').fileStatus -ceq 'PendingDelete') 'Replace the managed desktop screenshot set.'
    $screenshotCount = (Get-Content "$storeMaterials/listings/en-US.json" -Raw | ConvertFrom-Json).screenshots.Count
    Require (@($api.Pending.listings.'en-us'.baseListing.images | Where-Object fileStatus -CEQ 'PendingUpload').Count -eq $screenshotCount) 'Retries must not duplicate screenshots.'
    $pricingV2 = $api.Pending.Clone(); $pricingV2.pricing = @{ priceId = 'UnknownTier' }
    Require (-not (New-StoreDraftBody $pricingV2 'app.msixupload' 'notes').ContainsKey('pricing')) 'Do not write an unsupported Pricing v2 tier.'
    Require (($api.Pending.applicationPackages | Where-Object fileName -EQ 'old.msix').fileStatus -ceq 'PendingDelete') 'Replace the previous x64 package.'
    Require (($api.Pending.applicationPackages | Where-Object fileName -EQ 'arm.msix').fileStatus -ceq 'Uploaded') 'Preserve other architectures.'
    $draft = Get-Content "$fixture/store-draft.json" -Raw | ConvertFrom-Json
    Require ($draft.Uploaded -eq $true -and $draft.Committed -eq $false) 'Report must distinguish upload from certification.'
    $localeCount = (Get-Content "$PSScriptRoot/../localization/LanguageCatalog.json" -Raw | ConvertFrom-Json).Languages.Count
    Require ($draft.MaterialsSha256 -match '^[0-9a-f]{64}$' -and $draft.ListingLocales.Count -eq $localeCount) 'Record the materials used by the draft.'
    $exportPath = Get-ChildItem "$fixture/material-exports" -Directory | Sort-Object LastWriteTime | Select-Object -First 1
    $patch = (Get-Content (Join-Path $exportPath.FullName 'listings.json') -Raw | ConvertFrom-Json -AsHashtable).listings
    $published = $api.Pending | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
    $unchangedImage = $published.listings.'en-us'.baseListing.images | Where-Object fileStatus -CEQ 'PendingUpload' | Select-Object -First 1
    $unchangedImage.fileStatus = 'Uploaded'; $unchangedImage.id = 'reused-image'
    $nextBody = New-StoreDraftBody $published 'next.msixupload' 'next notes' $patch
    Require (@($nextBody.listings.'en-us'.baseListing.images | Where-Object { $_.id -ceq 'reused-image' -and $_.fileStatus -ceq 'Uploaded' }).Count -eq 1) 'Reuse unchanged uploaded images.'
    $badRemote = $nextBody | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
    $badRemote.listings.'en-us'.baseListing.description = 'Server did not apply the listing'
    Reject { Assert-StoreDraftListings $badRemote $nextBody @('en-us') }
    $badRemote = $nextBody | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
    $badRemote.listings.'en-us'.baseListing.images = @()
    Reject { Assert-StoreDraftListings $badRemote $nextBody @('en-us') }
    Reject { New-StoreDraftBody @{ listings = @{}; applicationPackages = @() } 'new.msixupload' 'notes' $patch }
    $copyPath = "$storeMaterials/listings/en-US.json"
    $oldCopy = [IO.File]::ReadAllText($copyPath)
    $changedCopy = $oldCopy | ConvertFrom-Json -AsHashtable
    $changedCopy.shortDescription += ' Changed.'
    Save-Json $changedCopy $copyPath
    $oldUpdates = $api.Updates
    Reject { & "$PSScriptRoot/../.github/scripts/Invoke-StoreDraft.ps1" -Action Draft @invoke }
    Require ($api.Updates -eq $oldUpdates) 'Reject changed material content before writing a retry draft.'
    [IO.File]::WriteAllText($copyPath, $oldCopy)
    $badProduct = $invoke.Clone(); $badProduct.ProductVersion = '0.6.1.0'
    Reject { & "$PSScriptRoot/../.github/scripts/Invoke-StoreDraft.ps1" -Action Draft @badProduct }
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
        if ($spec.Coverage) {
            '<coverage lines-covered="1" lines-valid="2"><packages><package name="VirtualPaper.Fixture"><classes><class filename="src/VirtualPaper.Fixture/Logic.cs"><lines><line number="1" hits="1"/><line number="2" hits="0"/></lines></class></classes></package></packages></coverage>' |
                Set-Content "$dir/coverage.cobertura.xml"
        }
    }
    Set-Content "$fixture/artifacts/exe/setup.exe" 'installer fixture'
    $exeHash = (Get-FileHash "$fixture/artifacts/exe/setup.exe").Hash
    Save-Json @{ FormatVersion = 1; Channel = 'EXE'; SourceSha = $sha; PackageFile = 'setup.exe'; PackageSha256 = $exeHash; PackageVersion = '0.6.0.0' } "$fixture/artifacts/exe/package.json"
    foreach ($channel in @('EXE', 'exe', 'ExE')) {
        $null = Get-PackageArtifact "$fixture/artifacts/exe/package.json" $channel $sha
        Reject { Get-PackageArtifact "$fixture/artifacts/exe/package.json" $channel ('b' * 40) }
    }
    Reject { Get-PackageArtifact "$fixture/artifacts/exe/package.json" msix $sha }
    $installer = Invoke-InstallerLifecycle -Install {} -Open {} -Reinstall {} -VerifyPreserved {} -OpenFresh {} -Uninstall {}
    $installer | Add-Member NoteProperty InstallerSha256 $exeHash
    $installer | Add-Member NoteProperty PackageVersion '0.6.0.0'
    Save-Json $installer "$raw/test-results-installer/installer-results.json"
    Save-Json $installer "$raw/test-results-ui-operation/installed-acceptance.json"
    $uiCases = @(Get-RequiredExtendedUICases)
    $ui = @{ Passed = $true; ExpectedCount = $uiCases.Count; Results = @($uiCases | ForEach-Object {
        @{ Name = $_; Passed = $true; Evidence = @{ CoreProcessId = 1; UiProcessIds = @(2); ProcessesExitedNormally = $true
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
    foreach ($id in @('packages-ready','tests-passed','package-exe','package-msix','test-exe-acceptance','test-msix-acceptance') + @(Get-ProjectTestSpecs | ForEach-Object Id)) { $jobs[$id] = @{ result = 'success' } }
    foreach ($id in @('test-exe-acceptance', 'test-msix-acceptance')) { $jobs[$id].outputs = @{ ui_run_result = 'success'; ui_upload_result = 'success' } }
    $arguments = @{ Root = $raw; ArtifactsRoot = "$fixture/artifacts"; SourceSha = $sha }
    $ciArguments = $arguments.Clone(); $ciArguments.AllowUIFailure = $true
    function Reject-ChangedReport([string] $Path, [string] $CheckName, [scriptblock] $Change) {
        $original = [IO.File]::ReadAllText($Path)
        try {
            $report = $original | ConvertFrom-Json
            & $Change $report
            Save-Json $report $Path
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-fail.log" }
            $summary = Get-Content "$raw/pipeline-summary.json" -Raw | ConvertFrom-Json
            Require (@($summary.Checks | Where-Object { $_.Name -ceq $CheckName -and $_.Status -ceq 'Failed' }).Count -eq 1) "Expected report guard did not reject $Path : $CheckName"
        } catch { throw "Report mutation check failed for $Path ($CheckName): $_" }
        finally { [IO.File]::WriteAllText($Path, $original) }
    }
    & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-pass.log"
    $summary = Get-Content "$raw/pipeline-summary.json" -Raw | ConvertFrom-Json
    Require (@($summary.Details.Coverage).Count -eq 1 -and $summary.Details.Coverage[0].Module -ceq 'VirtualPaper.Fixture' -and
        $summary.Details.Coverage[0].Covered -eq 1 -and $summary.Details.Coverage[0].Valid -eq 2) 'Pipeline coverage must union shared product lines across all project reports.'
    foreach ($id in @('tests-passed','test-exe-acceptance','test-msix-acceptance','package-msix','test-core')) {
        foreach ($result in @('failure', 'cancelled', 'skipped')) {
            $jobs[$id].result = $result
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-fail.log" }
        }
        $jobs.Remove($id)
        Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
        Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-fail.log" }
        $jobs[$id] = @{ result = 'success' }
        if ($id -like 'test-*-acceptance') { $jobs[$id].outputs = @{ ui_run_result = 'success'; ui_upload_result = 'success' } }
    }
    # A successful reusable-workflow result alone cannot replace raw lifecycle/UI reports.
    foreach ($path in @('test-results-installer/installer-results.json', 'test-results-ui-operation/installed-acceptance.json',
                         'test-results-ui-operation/ui-operation-results.json', 'test-results-msix-smoke/msix-acceptance.json',
                         'test-results-msix-ui/msix-acceptance.json', 'test-results-msix-ui/extended-ui/ui-operation-results.json',
                         'test-results-msix-smoke/first-run/ui-operation-results.json', 'test-results-msix-smoke/retained-profile/ui-operation-results.json',
                         'test-results-msix-smoke/fresh-profile/ui-operation-results.json')) {
        $reportPath = Join-Path $raw $path
        [IO.File]::Move($reportPath, "$reportPath.held")
        try {
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-fail.log" }
            $summary = Get-Content "$raw/pipeline-summary.json" -Raw | ConvertFrom-Json
            Require (($summary.Checks.Error -join ' ') -notmatch 'EXE and MSIX product versions differ') 'Missing EXE evidence must not be reported as a package version mismatch.'
        } finally { [IO.File]::Move("$reportPath.held", $reportPath) }
    }
    foreach ($path in @('test-results-ui-operation/ui-operation-results.json', 'test-results-msix-ui/extended-ui/ui-operation-results.json')) {
        Save-Json @{ Passed = $false; Status = 'Skipped'; ExpectedCount = 0; Results = @() } "$raw/$path"
        try {
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-fail.log" }
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-fail.log" }
        } finally { Save-Json $ui "$raw/$path" }
    }
    Reject-ChangedReport "$raw/test-results-installer/installer-results.json" 'Shared project tests / EXE smoke / installed UI' {
        param($report) $report.Checks[1].Passed = $false; $report.Checks[1].Status = 'Failed'
    }
    Reject-ChangedReport "$raw/test-results-msix-smoke/msix-acceptance.json" 'MSIX smoke / installed UI / artifact identity' {
        param($report) $report.Passed = $false; $report.Stages[1].Status = 'Failed'
    }
    foreach ($path in @('test-results-installer/installer-results.json', 'test-results-ui-operation/installed-acceptance.json')) {
        Reject-ChangedReport "$raw/$path" 'EXE artifact identity' { param($report) $report.InstallerSha256 = '0' * 64 }
        Reject-ChangedReport "$raw/$path" 'EXE artifact identity' { param($report) $report.PackageVersion = '0.6.0.1' }
    }
    $msixCheck = 'MSIX smoke / installed UI / artifact identity'
    foreach ($mode in @('smoke', 'ui')) {
        $path = "$raw/test-results-msix-$mode/msix-acceptance.json"
        Reject-ChangedReport $path $msixCheck { param($report) $report.PackageSha256 = '0' * 64 }
        Reject-ChangedReport $path $msixCheck { param($report) $report.SourceSha = 'b' * 40 }
    }
    foreach ($property in @('InitialDeployment', 'MissingFolderRestored', 'ExistingDataPreserved', 'SavedDirectoryHonored', 'UninstallDataPreserved')) {
        Reject-ChangedReport "$raw/test-results-msix-smoke/msix-acceptance.json" $msixCheck { param($report) $report.BuiltInWallpapers.$property = $false }
    }
    Reject-ChangedReport "$raw/test-results-msix-ui/msix-acceptance.json" $msixCheck { param($report) $report.PackageFullName = 'different_1.2611.1.0_x64__publisher' }
    Reject-ChangedReport "$msix/package.json" $msixCheck { param($report) $report.ProductVersion = '0.6.0.1' }
    # Counts alone cannot distinguish a reduced UI suite from full installed acceptance.
    foreach ($path in @('test-results-ui-operation/ui-operation-results.json', 'test-results-msix-ui/extended-ui/ui-operation-results.json')) {
        $check = if ($path.StartsWith('test-results-msix-')) { $msixCheck } else { 'EXE artifact identity' }
        foreach ($case in $uiCases) {
            Reject-ChangedReport "$raw/$path" $check {
                param($report)
                $report.Results = @($report.Results | Where-Object Name -CNE $case)
                $report.ExpectedCount = $report.Results.Count
            }
            Reject-ChangedReport "$raw/$path" $check {
                param($report)
                $report.Results += @($report.Results | Where-Object Name -CEQ $case)
                $report.ExpectedCount = $report.Results.Count
            }
        }
    }
    foreach ($channel in @('exe', 'msix')) {
        $job = $jobs["test-$channel-acceptance"]
        $uiPath = if ($channel -eq 'exe') { "$raw/test-results-ui-operation/ui-operation-results.json" }
            else { "$raw/test-results-msix-ui/extended-ui/ui-operation-results.json" }
        $lifecyclePath = if ($channel -eq 'exe') { "$raw/test-results-ui-operation/installed-acceptance.json" }
            else { "$raw/test-results-msix-ui/msix-acceptance.json" }
        $originalUI = [IO.File]::ReadAllText($uiPath); $originalLifecycle = [IO.File]::ReadAllText($lifecyclePath)
        try {
            $failedUI = $originalUI | ConvertFrom-Json
            $failedUI.Passed = $false; $failedUI.Results[0].Passed = $false
            Save-Json $failedUI $uiPath
            $lifecycle = $originalLifecycle | ConvertFrom-Json
            if ($channel -eq 'exe') { $lifecycle.Checks[1].Passed = $false; $lifecycle.Checks[1].Status = 'Failed' }
            else { $lifecycle.Passed = $false; ($lifecycle.Stages | Where-Object Name -CEQ 'ExtendedUI').Status = 'Failed' }
            Save-Json $lifecycle $lifecyclePath
            $job.outputs.ui_run_result = 'failure'
            $job.result = 'failure'
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-strict-ui-fail.log" }
            & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-ui-fail.log"
            $summary = Get-Content "$raw/pipeline-summary.json" -Raw | ConvertFrom-Json
            Require ($summary.Passed -eq $true -and $summary.UIOutcome -ceq 'Failed' -and $summary.Details.$channel.UIRunResult -ceq 'failure') 'Uploaded UI failure must remain visible without blocking CI.'
            # A tolerated UI failure must never conceal a smoke failure in the same channel.
            if ($channel -eq 'exe') {
                Reject-ChangedReport "$raw/test-results-installer/installer-results.json" 'Shared project tests / EXE smoke / installed UI' {
                    param($report) $report.Checks[1].Passed = $false; $report.Checks[1].Status = 'Failed'
                }
            } else {
                Reject-ChangedReport "$raw/test-results-msix-smoke/msix-acceptance.json" $msixCheck {
                    param($report) $report.Passed = $false; $report.Stages[1].Status = 'Failed'
                }
            }
            Save-Json (New-UIOperationFailureReport 'Host exited without a report (exit -1073741189)' ([pscustomobject]@{ Available = $true }) -1073741189) $uiPath
            & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-host-crash.log"
            $summary = Get-Content "$raw/pipeline-summary.json" -Raw | ConvertFrom-Json
            Require ($summary.Passed -eq $true -and $summary.UIOutcome -ceq 'Failed') 'An uploaded host crash must remain Failed, with no fabricated passing cases.'
            Require ($summary.Details.$channel.UIError -like '*-1073741189*') 'Retain the host crash reason in the pipeline summary.'
            $job.result = 'success'
            & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-tolerated-host-crash.log"
            $summary = Get-Content "$raw/pipeline-summary.json" -Raw | ConvertFrom-Json
            Require ($summary.Passed -eq $true -and $summary.UIOutcome -ceq 'Failed') 'A successful outer job must not hide the failed UI run.'
            foreach ($upload in @('failure', 'cancelled', 'skipped', $null)) {
                $job.outputs.ui_upload_result = $upload
                Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-ui-upload-fail.log" }
            }
            $job.outputs.ui_upload_result = 'success'
            $job.result = 'failure'
            # A failure after a passing UI report (such as cleanup) must also remain visible.
            [IO.File]::WriteAllText($uiPath, $originalUI); [IO.File]::WriteAllText($lifecyclePath, $originalLifecycle)
            & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-ui-step-fail.log"
            $summary = Get-Content "$raw/pipeline-summary.json" -Raw | ConvertFrom-Json
            Require ($summary.Passed -eq $true -and $summary.UIOutcome -ceq 'Failed') 'Do not hide a tolerated step failure behind a passing report.'
            $job.outputs.ui_run_result = 'success'
            $job.result = 'success'
            Save-Json (New-UIOperationSkipReport ([pscustomobject]@{ Available = $false; ReasonCode = 'NoInteractiveDesktop'; Reason = 'Fixture desktop unavailable.' })) $uiPath
            & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-ui-skip.log"
            $summary = Get-Content "$raw/pipeline-summary.json" -Raw | ConvertFrom-Json
            Require ($summary.Passed -eq $true -and $summary.UIOutcome -ceq 'Skipped') 'An uploaded environment skip must remain a skip, never a pass.'
            Set-Content -LiteralPath $uiPath -Value '{}'
            Reject { & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-malformed-ui.log" }
        } finally {
            [IO.File]::WriteAllText($uiPath, $originalUI); [IO.File]::WriteAllText($lifecyclePath, $originalLifecycle)
            $job.outputs.ui_run_result = 'success'
            $job.outputs.ui_upload_result = 'success'
            $job.result = 'success'
        }
    }
    & "$PSScriptRoot/Summarize-PipelineTests.ps1" @arguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-restored.log"
    & "$PSScriptRoot/Summarize-PipelineTests.ps1" @ciArguments -JobResultsJson ($jobs | ConvertTo-Json -Depth 4) *> "$fixture/summary-ci-restored.log"
    foreach ($id in @('package-msix','test-msix-acceptance')) { $jobs[$id].result = 'skipped' }
    # An unselected channel must not consume leftover evidence from a different run.
    Save-Json @{ Passed = $false; SourceSha = 'b' * 40 } "$raw/test-results-msix-smoke/msix-acceptance.json"
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
Write-Host '[OK] Package channels, required smoke/uploads, non-blocking CI UI, strict local summaries and Store draft/retry contracts passed.'
