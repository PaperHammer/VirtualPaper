#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
$sdk = Get-Content -LiteralPath (Join-Path $root 'global.json') -Raw | ConvertFrom-Json
Require ($sdk.sdk.version -match '^10\.0\.' -and $sdk.sdk.rollForward -eq 'latestFeature' -and
    $sdk.sdk.allowPrerelease -ceq $false) 'Builds must stay on a stable .NET 10 SDK.'
$windowsVersion = '10.0.19041.0'
foreach ($project in Get-ChildItem "$root/src/*/*.csproj", "$root/test/*/*.csproj", "$root/resources/tools/*/*.csproj") {
    [xml]$definition = Get-Content -LiteralPath $project.FullName -Raw
    $frameworks = @($definition.SelectNodes('/Project/PropertyGroup/TargetFramework'))
    Require ($frameworks.Count -eq 1 -and $frameworks[0].InnerText -match '^net10\.0(?:-windows[\d.]+)?$') "Wrong target framework: $($project.FullName)"
    if ($frameworks[0].InnerText.Contains('-windows')) {
        Require ($frameworks[0].InnerText -ceq "net10.0-windows$windowsVersion") "Wrong Windows target: $($project.FullName)"
        # When no minimum is explicit, the .NET SDK defaults it to the TFM version.
        foreach ($minimum in $definition.SelectNodes('/Project/PropertyGroup/SupportedOSPlatformVersion | /Project/PropertyGroup/TargetPlatformMinVersion')) {
            Require ($minimum.InnerText -ceq $windowsVersion) "Wrong Windows minimum: $($project.FullName)"
        }
    }
}
foreach ($file in @('src/VirtualPaper.UI/Package.appxmanifest', 'packaging/msix/Package.appxmanifest')) {
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $root $file) -Raw
    $families = @($manifest.SelectNodes('//*[local-name()="TargetDeviceFamily"]'))
    Require ($families.Count -gt 0) "Missing Windows device family: $file"
    foreach ($family in $families) {
        Require ($family.MinVersion -ceq $windowsVersion) "Wrong packaged Windows minimum: $file"
    }
}
[xml]$packageProject = Get-Content -LiteralPath (Join-Path $root 'packaging/msix/VirtualPaper.Package.wapproj') -Raw
$packageMinimum = $packageProject.SelectSingleNode('//*[local-name()="TargetPlatformMinVersion"]')
Require ($null -ne $packageMinimum -and $packageMinimum.InnerText -ceq $windowsVersion) 'MSIX packaging must preserve the application Windows minimum.'
foreach ($file in @('.github/workflows/pre-merge-check.yml', '.github/workflows/pre-release.yml',
    '.github/workflows/package-acceptance.yml', 'test/actions/run-project/action.yml')) {
    $workflow = Get-Content -LiteralPath (Join-Path $root $file) -Raw
    $versions = [regex]::Matches($workflow, '(?m)^\s+dotnet-version:\s*([^\r\n]+)')
    Require ($versions.Count -gt 0) "Missing SDK setup: $file"
    foreach ($version in $versions) {
        Require ($version.Groups[1].Value.Trim(" `"'") -ceq '10.0.x') "Wrong workflow SDK: $file"
    }
}
$setup = Get-Content -LiteralPath (Join-Path $root 'packaging/inno/setup.iss') -Raw
Require ($setup -match '(?m)^MinVersion=10\.0\.19041\s*$') 'EXE installation must enforce Windows 10 build 19041 or newer.'
Require ($setup.Contains('ArchitecturesAllowed=x64compatible')) 'An x64 payload must reject unsupported 32-bit Windows.'
$dependencies = Get-Content -LiteralPath (Join-Path $root 'packaging/inno/CodeDependencies.iss') -Raw
$wizard = [regex]::Match($setup, '(?s)procedure InitializeWizard;.*?\nend;').Value
Require ($wizard -match 'Dependency_ForceX64 := True;\s+Dependency_AddDotNet100Desktop;' -and
    $wizard -match 'Dependency_AddDotNet100Asp;' -and
    $wizard -notmatch 'WizardSilent|IsUpgrade|Dependency_InitializeWizard;') 'All installs/upgrades must register the x64 Desktop Runtime dependency once.'
Require ($setup -notmatch 'Dependency_AddDotNet80|Dependency_PrepareToInstall\(|Dependency_UpdateReadyMemo\(|Dependency_NeedRestart;') 'Do not invoke annotated dependency events twice or use the old runtime.'
$prepare = [regex]::Match($setup, '(?s)function PrepareToInstall\(.*?\nend;').Value
Require ($prepare -match "Dependency_IsNetCoreInstalled\('Microsoft.WindowsDesktop.App', 10, 0," -and
    $prepare -match "Dependency_IsNetCoreInstalled\('Microsoft.AspNetCore.App', 10, 0," -and
    $prepare -match "Result := '\.NET Desktop Runtime") 'A missing prerequisite must block completion after a failed or ignored dependency install.'
Require ($dependencies -notmatch 'MB_ABORTRETRYIGNORE, IDIGNORE') 'Unattended dependency failures must abort.'
$version = [regex]::Match($dependencies, '#define Dependency_DotNet100Version "(10\.0\.(\d+))"')
Require ($version.Success -and $dependencies.Contains("#define Dependency_DotNet100Revision $($version.Groups[2].Value)")) 'Runtime download and detection versions differ.'
$publish = Get-Content -LiteralPath (Join-Path $root '.github/scripts/Build-Msix.ps1') -Raw
Require ($publish.Contains('/p:SelfContained=true') -and
    $publish.Contains("/p:MsixRuntimeVersion=$($version.Groups[1].Value)") -and
    -not $publish.Contains('/p:RuntimeFrameworkVersion=')) 'EXE and MSIX must provision the same serviced .NET 10 release without overriding Windows SDK references.'
Write-Host '[OK] Stable .NET 10 SDK, Windows 19041 targets/minimums, CI setup, mandatory x64 installer provisioning and self-contained MSIX runtime contracts passed.'
