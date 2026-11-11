#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Validate', 'Draft')][string] $Action,
    [Parameter(Mandatory)][string] $BuildNumber,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $SourceSha,
    [string] $PackageArtifact,
    [string] $ReportPath = 'store-draft.json'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/StoreSubmission.ps1"
. "$PSScriptRoot/../../test/PackageArtifactSupport.ps1"
$config = Get-StoreConfiguration
$version = Get-StorePackageVersion $BuildNumber
if ($env:GITHUB_REPOSITORY -notmatch '^[\w.-]+/[\w.-]+$' -or $env:GITHUB_RUN_ID -notmatch '^\d+$') { throw 'Store drafts require a GitHub workflow run identity.' }
$marker = "VirtualPaper CI:$env:GITHUB_REPOSITORY`:$env:GITHUB_RUN_ID`:$SourceSha"
$token = Get-StoreAccessToken $config
$pending = Get-StoreReleaseState $config $token $marker $version
if ($Action -eq 'Validate') {
    if ($env:GITHUB_OUTPUT) { "msix_version=$version" >> $env:GITHUB_OUTPUT }
    Write-Host "Store configuration and published version baseline verified: $version"
    return
}
$artifact = Get-PackageArtifact -ManifestPath $PackageArtifact -Channel MSIX -SourceSha $SourceSha
if ($artifact.Store -ne $true -or $artifact.IdentityName -cne $config.IDENTITY_NAME -or
    $artifact.Publisher -cne $config.PUBLISHER -or $artifact.PackageVersion -cne $version) { throw 'Store draft requires the validated formal release artifact.' }
$path = "applications/$($config.APPLICATION_ID)/submissions"
$notes = "$marker`nPackage SHA256:$($artifact.PackageSha256)`nSource:$SourceSha`nAutomated package upload only; certification and publication remain manual."
if ($pending -and (Get-NormalizedStoreNotes $pending.notesForCertification) -cne $notes) { throw 'Retry artifact differs from the existing Store draft.' }
$submission = if ($pending) { $pending } else { Invoke-StoreRequest Post $path $token }
$id = [string]$submission.id
if ($id -notmatch '^\d+$' -or $submission.status -cne 'PendingCommit') { throw 'Partner Center did not create an uncommitted draft.' }
@{ ApplicationId = $config.APPLICATION_ID; SubmissionId = $id; Status = 'CreatedUncommitted'
    SourceSha = $SourceSha; PackageVersion = $version; PackageSha256 = $artifact.PackageSha256
    UploadSha256 = $artifact.UploadSha256; Uploaded = $false; Committed = $false } |
    ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding utf8NoBOM
$body = New-StoreDraftBody $submission $artifact.UploadFile $notes
# Write ownership before uploading, so a failed upload can safely reuse this draft.
$updated = Invoke-StoreRequest Put "$path/$id" $token $body
$uploadUrl = if ($updated.fileUploadUrl) { $updated.fileUploadUrl } else { $submission.fileUploadUrl }
$temporary = Join-Path ([IO.Path]::GetTempPath()) "VirtualPaper.StoreUpload-$([guid]::NewGuid().ToString('N')).zip"
try {
    $root = Split-Path -Parent ([IO.Path]::GetFullPath($PackageArtifact))
    Compress-Archive -LiteralPath (Join-Path $root $artifact.UploadFile) -DestinationPath $temporary
    Send-StoreUpload $uploadUrl $temporary
    $draft = Invoke-StoreRequest Get "$path/$id" $token
    if ($draft.status -cne 'PendingCommit' -or $draft.targetPublishMode -cne 'Manual' -or (Get-NormalizedStoreNotes $draft.notesForCertification) -cne $notes) {
        throw 'Store draft verification failed; no commit was requested.'
    }
    @{ ApplicationId = $config.APPLICATION_ID; SubmissionId = $id; Status = $draft.status
        SourceSha = $SourceSha; PackageVersion = $version; PackageSha256 = $artifact.PackageSha256
        UploadSha256 = $artifact.UploadSha256; Uploaded = $true; Committed = $false } |
        ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding utf8NoBOM
    if ($env:GITHUB_STEP_SUMMARY) {
        "### Microsoft Store draft`n`n- Submission: $id`n- Version: $version`n- Package uploaded; PendingCommit. Certification/publication was not requested." >> $env:GITHUB_STEP_SUMMARY
    }
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
