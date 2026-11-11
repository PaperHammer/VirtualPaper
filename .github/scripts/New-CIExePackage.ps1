#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $SourceSha,
    [string] $Version,
    [string] $OutputDirectory = 'dist/exe',
    [string] $InnoSetupCompiler,
    [string] $BuildNumber,
    [string] $UpdatePlugins
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$payload = Join-Path $repositoryRoot 'src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0'
$actualVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payload 'VirtualPaper.exe')).FileVersion
if ($Version -and [version]$actualVersion -ne [version]$Version) { throw 'Build and requested installer version differ.' }
$Version = $actualVersion
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh EXE artifact directory.' }
$setup = Join-Path $repositoryRoot 'InnoSetup/setup.iss'
$originalSetup = [IO.File]::ReadAllBytes($setup)
$content = Get-Content -LiteralPath $setup -Raw
$content = $content -replace '#define MyAppVersion "[\d.]+"', "#define MyAppVersion `"$Version`""
$content = $content -replace '#define MyAppVersionText "[\d_]+"', "#define MyAppVersionText `"$($Version.Replace('.', '_'))`""
if (-not $InnoSetupCompiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $InnoSetupCompiler = if ($command) { $command.Source } else { "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe" }
}
if (-not (Test-Path -LiteralPath $InnoSetupCompiler)) { throw 'Install Inno Setup 6.7.1 or supply -InnoSetupCompiler.' }
try {
    Set-Content -LiteralPath $setup -Value $content -NoNewline
    & $InnoSetupCompiler $setup /O+
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
} finally { [IO.File]::WriteAllBytes($setup, $originalSetup) }
$installers = @(Get-ChildItem "$repositoryRoot/InnoSetup/Output" -Filter '*.exe' -File)
if ($installers.Count -ne 1) { throw 'Expected one freshly generated EXE installer.' }
[void](New-Item -ItemType Directory -Path $OutputDirectory)
$installer = Copy-Item -LiteralPath $installers[0].FullName -Destination $OutputDirectory -PassThru
$hash = (Get-FileHash $installer.FullName).Hash.ToLowerInvariant()
$hash | Set-Content "$OutputDirectory/SHA256.txt" -Encoding utf8NoBOM
@{ FormatVersion = 1; Channel = 'EXE'; SourceSha = $SourceSha; PackageVersion = $Version
    PackageFile = $installer.Name; PackageSha256 = $hash } |
    ConvertTo-Json | Set-Content "$OutputDirectory/package.json" -Encoding utf8NoBOM
if ($UpdatePlugins) {
    & "$PSScriptRoot/New-PluginPackage.ps1" -PayloadRoot $payload -BuildNumber $BuildNumber `
        -UpdatePlugins $UpdatePlugins -OutputDirectory $OutputDirectory
}
