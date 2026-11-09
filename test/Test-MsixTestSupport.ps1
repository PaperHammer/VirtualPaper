$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/UIOperationTestSupport.ps1"
. "$PSScriptRoot/MsixTestSupport.ps1"

function Assert-Rejected([scriptblock] $Action) {
    try { & $Action } catch { return }
    throw 'Invalid MSIX acceptance evidence was accepted.'
}
function New-PassReport {
    [pscustomobject]@{ Passed = $true; PackageFullName = 'test_1.0.0.0_x64__publisher'; PackageSha256 = ('a' * 64)
        Stages = @(Get-MsixAcceptanceStages | ForEach-Object { [pscustomobject]@{ Name = $_; Status = 'Passed' } }) }
}
Assert-MsixAcceptanceReport (New-PassReport)
$report = New-PassReport
$report.Stages += [pscustomobject]@{ Name = 'Unknown'; Status = 'Failed' }
Assert-Rejected { Assert-MsixAcceptanceReport $report }
foreach ($name in Get-MsixAcceptanceStages) {
    foreach ($status in @('Failed', 'Skipped')) {
        $report = New-PassReport
        ($report.Stages | Where-Object Name -EQ $name).Status = $status
        Assert-Rejected { Assert-MsixAcceptanceReport $report }
    }
    $report = New-PassReport
    $report.Stages = @($report.Stages | Where-Object Name -NE $name)
    Assert-Rejected { Assert-MsixAcceptanceReport $report }
}
$directory = Join-Path ([IO.Path]::GetTempPath()) "VirtualPaper.MsixReportContract.$([guid]::NewGuid().ToString('N'))"
[void](New-Item -ItemType Directory -Path $directory)
$path = Join-Path $directory 'ui.json'
try {
    $result = [pscustomobject]@{ Name = 'Application.Example.Run'; Passed = $true; Evidence = [pscustomobject]@{
        CoreProcessId = 1; UiProcessIds = @(2); ProcessesExitedNormally = $true
        PackageFullName = 'test'; PackageActivation = 'AUMID'; UnitTestBehaviorOverridesEnabled = $false } }
    $ui = [pscustomobject]@{ Passed = $true; ExpectedCount = 1; Results = @($result) }
    $ui | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path
    $null = Assert-MsixUIReport $path 'test'
    Assert-Rejected { $null = Assert-MsixUIReport $path 'wrong-package' }
    Assert-Rejected { $null = Assert-MsixUIReport $path 'test' -Extended }
    $result.Evidence.PackageActivation = 'Executable'
    $ui | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path
    Assert-Rejected { $null = Assert-MsixUIReport $path 'test' }
    $result.Evidence.PackageActivation = 'AUMID'
    $result.Evidence.UnitTestBehaviorOverridesEnabled = $true
    $ui | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path
    Assert-Rejected { $null = Assert-MsixUIReport $path 'test' }
} finally {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    Remove-Item -LiteralPath $directory
}
Write-Host '[OK] MSIX acceptance rejects skipped/missing stages, unpackaged processes and incomplete UI suites.'
