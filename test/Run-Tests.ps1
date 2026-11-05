#Requires -Version 7.2
[CmdletBinding()]
param(
    [string]$ResultsRoot = "TestResults/local-acceptance-$(Get-Date -Format 'yyyyMMdd-HHmmss')-$([guid]::NewGuid().ToString('N').Substring(0, 8))",
    [string]$InnoSetupCompiler,
    [switch]$IncludeMLIntegration
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Installed application acceptance requires Windows.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$ResultsRoot = [IO.Path]::GetFullPath($ResultsRoot, $repositoryRoot)
if (Test-Path -LiteralPath $ResultsRoot) { throw "Results must use a new directory: $ResultsRoot" }
. "$PSScriptRoot/InstallerTestSupport.ps1"
. "$PSScriptRoot/TestReportSupport.ps1"
# Refuse a daily installation before building or changing any installed files.
Assert-CleanInstallerEnvironment (Join-Path $repositoryRoot 'InnoSetup/setup.iss')
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run PowerShell as administrator for the production installer and uninstaller.'
}
$msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
$msbuildPath = if ($msbuild) { $msbuild.Source } else {
    & "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
}
if (-not $msbuildPath) { throw 'Visual Studio MSBuild is required.' }
if (-not $InnoSetupCompiler) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $InnoSetupCompiler = if ($compiler) { $compiler.Source } else { "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe" }
}
if (-not (Test-Path -LiteralPath $InnoSetupCompiler -PathType Leaf)) { throw 'Install Inno Setup 6.7.1 or supply -InnoSetupCompiler.' }
Get-Command dotnet.exe,node.exe -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path $ResultsRoot | Out-Null
Write-Host "Acceptance reports: $ResultsRoot"
$jobs = @()
Push-Location -LiteralPath $repositoryRoot
try {
    & ./.github/scripts/validate-test-workflow.ps1 *> "$ResultsRoot/workflow-contracts.log"
    Write-Host 'Building the complete Release solution...'
    & $msbuildPath src/VirtualPaper.sln /t:Rebuild /p:Configuration=Release '/p:Platform=Any CPU' /restore /m /nologo /v:minimal *> "$ResultsRoot/build.log"
    if ($LASTEXITCODE -ne 0) { throw "Release build failed. See $ResultsRoot/build.log" }
    # Separate PowerShell processes preserve the same isolation as the parallel CI jobs.
    foreach ($spec in Get-ProjectTestSpecs) {
        $jobs += Start-Job -Name $spec.Id -ArgumentList $repositoryRoot,$ResultsRoot,$spec.Id,$spec.Artifact,$IncludeMLIntegration.IsPresent -ScriptBlock {
            param($repositoryRoot, $resultsRoot, $jobId, $artifact, $includeML)
            $ErrorActionPreference = 'Stop'
            Set-Location -LiteralPath $repositoryRoot
            $directory = Join-Path $resultsRoot $artifact
            New-Item -ItemType Directory -Path $directory | Out-Null
            if ($jobId -eq 'test-ui-logic') {
                & node .github/scripts/test-atmosphere.cjs
                if ($LASTEXITCODE -ne 0) { throw 'Atmosphere script tests failed.' }
                & node .github/scripts/test-depth-player.cjs
                if ($LASTEXITCODE -ne 0) { throw 'Depth player script tests failed.' }
            }
            & ./test/Run-ProjectTests.ps1 -JobId $jobId -ResultsDirectory $directory -IncludeMLIntegration:$includeML
        }
    }
    Write-Host 'Running eight project test jobs in parallel...'
    $jobs | Wait-Job | Out-Null
    foreach ($job in $jobs) {
        Receive-Job $job -ErrorAction Continue *> (Join-Path $ResultsRoot "$($job.Name).log")
    }
    $failed = @($jobs | Where-Object State -NE 'Completed')
    if ($failed.Count) { throw "Project tests failed: $($failed.Name -join ', '). Installation was not attempted." }
    Get-ProjectTestReports -Root $ResultsRoot | Format-Table Total, Executed, Passed
    Write-Host 'Compiling the production ISS installer...'
    $packageDir = Join-Path $ResultsRoot 'installer-package'
    & $InnoSetupCompiler "InnoSetup/setup.iss" "/O$packageDir" *> "$ResultsRoot/installer-build.log"
    if ($LASTEXITCODE -ne 0) { throw "ISS packaging failed. See $ResultsRoot/installer-build.log" }
    $installers = @(Get-ChildItem -LiteralPath $packageDir -Filter '*.exe' -File)
    if ($installers.Count -ne 1) { throw 'Expected exactly one freshly built installer.' }
    Write-Host 'Installing, opening, running extended UI acceptance and uninstalling...'
    & ./test/Run-InstallerTests.ps1 -InstallerPath $installers[0].FullName -NoBuild -RunUIOperationTests `
        -UIHostExe test/VirtualPaper.UIOperationTest/bin/x64/Release/net8.0-windows10.0.19041.0/VirtualPaper.UIOperationTest.exe `
        -ReportPath "$ResultsRoot/test-results-installer/installer-results.json" `
        -UIReportPath "$ResultsRoot/test-results-ui-operation/ui-operation-results.json" *> "$ResultsRoot/installed-acceptance.log"
    & ./test/Summarize-TestResults.ps1 -Root $ResultsRoot
} finally {
    $jobs | Where-Object State -EQ 'Running' | Stop-Job
    $jobs | Remove-Job -Force
    Pop-Location
    Write-Host "Acceptance reports: $ResultsRoot"
}
