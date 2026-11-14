# Cross-version acceptance stays separate from same-package smoke.
. "$PSScriptRoot/PackageArtifactSupport.ps1"

function Get-UpgradePackagePair([string] $PreviousArtifact, [string] $PackageArtifact, [string] $Channel) {
    $previous = Get-PackageArtifact $PreviousArtifact $Channel
    $next = Get-PackageArtifact $PackageArtifact $Channel
    Assert-UpgradePackageMetadata $previous $next $Channel
    return @{ Previous = $previous; Next = $next }
}

function Assert-UpgradePackageMetadata($Previous, $Next, [string] $Channel) {
    if ($Previous.Channel -cne $Channel -or $Next.Channel -cne $Channel) {
        throw 'Both package artifacts must match the upgrade report channel.'
    }
    if ($previous.SourceSha -ceq $next.SourceSha -or $previous.PackageSha256 -ieq $next.PackageSha256 -or
        [version]$next.PackageVersion -le [version]$previous.PackageVersion) {
        throw 'Upgrade requires different source snapshots and package bytes, and a strictly increasing version.'
    }
    if ($Channel -eq 'MSIX' -and ($previous.IdentityName -cne $next.IdentityName -or
        $previous.Publisher -cne $next.Publisher -or $previous.CertificateSha256 -ine $next.CertificateSha256 -or
        $previous.IdentityName -cnotmatch '^PaperHammer\.VirtualPaper\.Test\.[0-9a-f]{16}$')) {
        throw 'MSIX upgrade requires one owned test family and the same acceptance signing certificate.'
    }
}

function Get-UpgradeStages([string] $Channel) {
    'InstallPrevious', 'PreparePrevious', 'Upgrade', 'VerifyRetained', 'Restart', 'Uninstall'
    if ($Channel -eq 'MSIX') { 'CertificateCleanup' }
}

function Assert-UpgradeReport($Report) {
    if ($Report.Suite -cne 'CrossVersionUpgrade' -or $Report.Channel -cnotin @('EXE', 'MSIX') -or
        $Report.Passed -cne $true -or $Report.Error -or
        $Report.Previous.SourceSha -notmatch '^[0-9a-f]{40}$' -or $Report.Next.SourceSha -notmatch '^[0-9a-f]{40}$' -or
        $Report.Previous.SourceSha -ceq $Report.Next.SourceSha -or
        $Report.Previous.PackageSha256 -notmatch '^[0-9a-fA-F]{64}$' -or $Report.Next.PackageSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        $Report.Previous.PackageSha256 -ieq $Report.Next.PackageSha256 -or
        [version]$Report.Next.PackageVersion -le [version]$Report.Previous.PackageVersion -or
        $Report.NewSettingsInitialized -cne $true -or
        $Report.BuiltInWallpapers.ExistingDataPreserved -cne $true -or
        $Report.BuiltInWallpapers.MissingFolderRestored -cne $true -or
        $Report.BuiltInWallpapers.SavedDirectoryHonored -cne $true -or
        $Report.BuiltInWallpapers.UninstallDataPreserved -cne $true -or
        (@($Report.Stages.Name) -join ',') -cne (@(Get-UpgradeStages $Report.Channel) -join ',') -or
        @($Report.Stages | Where-Object Status -CNE 'Passed').Count) {
        throw 'Cross-version upgrade must complete every stage without skips using different source versions.'
    }
    Assert-UpgradePackageMetadata $Report.Previous $Report.Next $Report.Channel
}

function Update-BuiltInWallpaperUpgradeExpectation($State, [string] $NewPayload) {
    # Existing folders are user-owned. Only whole missing/new folders may be added.
    $existing = @(Get-ChildItem -LiteralPath $State.Destination -Directory | Select-Object -ExpandProperty Name)
    $State.Expected = Get-BuiltInWallpaperHashes $State.Destination
    foreach ($folder in Get-ChildItem -LiteralPath $NewPayload -Directory) {
        if ($folder.Name -in $existing) { continue }
        foreach ($entry in (Get-BuiltInWallpaperHashes $folder.FullName).GetEnumerator()) {
            $State.Expected[(Join-Path $folder.Name $entry.Key)] = $entry.Value
        }
    }
}
