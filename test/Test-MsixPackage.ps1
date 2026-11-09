#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string] $PackagePath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$package = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
try {
    $files = @{}
    foreach ($entry in $package.Entries) { $files[$entry.FullName.Replace('\', '/')] = $entry }
    function Assert-PackageFile([string] $Path) {
        if (-not $files.ContainsKey($Path) -or $files[$Path].Length -eq 0) { throw "Missing/empty MSIX payload file: $Path" }
    }
    function Read-PackageText([string] $Path) {
        Assert-PackageFile $Path
        $reader = [IO.StreamReader]::new($files[$Path].Open())
        try { $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    [xml]$manifest = Read-PackageText 'AppxManifest.xml'
    $namespaces = [Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $namespaces.AddNamespace('m', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $namespaces.AddNamespace('desktop', 'http://schemas.microsoft.com/appx/manifest/desktop/windows10')
    $app = $manifest.SelectSingleNode('/m:Package/m:Applications/m:Application', $namespaces)
    if ($app.Executable -cne 'VirtualPaper.exe' -or $app.EntryPoint -cne 'Windows.FullTrustApplication') {
        throw 'The MSIX entry point must be the full-trust Core executable.'
    }
    $startup = $app.SelectSingleNode('m:Extensions/desktop:Extension', $namespaces)
    if ($startup.Executable -cne 'VirtualPaper.exe' -or $startup.'StartupTask'.TaskId -cne 'AppStartup') {
        throw 'The MSIX startup task must match WindowsAutoStart and launch Core.'
    }
    Assert-PackageFile 'resources.pri'
    Assert-PackageFile 'MsixAssets/StoreLogo.scale-200.png'
    Assert-PackageFile 'MsixAssets/Square150x150Logo.scale-200.png'
    Assert-PackageFile 'MsixAssets/Square44x44Logo.scale-200.png'
    foreach ($process in @(
        @('', 'VirtualPaper'), @('Plugins/UI/', 'VirtualPaper.UI'),
        @('Plugins/PlayerWeb/', 'VirtualPaper.PlayerWeb'), @('Plugins/ScrSaver/', 'VirtualPaper.ScreenSaver')
    )) {
        $directory, $name = $process
        foreach ($file in @("$name.exe", "$name.dll", 'coreclr.dll', 'hostpolicy.dll')) { Assert-PackageFile "$directory$file" }
        if ($files.ContainsKey("${directory}Microsoft.WindowsAppRuntime.dll")) {
            Assert-PackageFile "${directory}Microsoft.WindowsAppRuntime.pri"
        }
        $runtime = Read-PackageText "$directory$name.runtimeconfig.json" | ConvertFrom-Json
        if (-not $runtime.runtimeOptions.PSObject.Properties['includedFrameworks']) { throw "$name is not self-contained." }
    }
    foreach ($locale in @('zh-CN', 'en-US')) {
        $translations = Read-PackageText "Plugins/UI/VirtualPaper.UIComponent/Strings/$locale/Resources.resw"
        if ($translations -notmatch 'name="Ui_') { throw "Missing generated XAML translations for $locale." }
    }
    Assert-PackageFile 'Plugins/UI/PLAYER_Web/default.html'
    Assert-PackageFile 'Plugins/ScrSaver/Players/Web/default.html'
    foreach ($model in @('DepthEstimate/ai_models/depth_anything_v2_vits_dynamic.onnx',
        'StyleTransfer/ai_models/adain_style_transfer.onnx', 'SuperResolution/ai_models/realesrgan_x4plus_dynamic.onnx')) {
        Assert-PackageFile "Plugins/ML/$model"
    }
    if (@($files.Keys | Where-Object { $_ -like 'Plugins/Shaders/*.bin' }).Count -eq 0) { throw 'No compiled shaders in MSIX.' }
    if (@($files.Keys | Where-Object { $_ -like '*.pdb' -or $_ -like '*installer-language.txt' -or $_ -like '*.WebView2/*' }).Count) {
        throw 'MSIX includes debug/installer files or a WebView2 user profile.'
    }
    # Inspect the actual merged index, not just the presence of a .pri file.
    $sdkRoot = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Microsoft SDKs\Windows\v10.0').InstallationFolder
    $makePri = Join-Path $sdkRoot 'bin/10.0.26100.0/x64/makepri.exe'
    $temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) "VirtualPaper.MsixCheck.$([guid]::NewGuid().ToString('N'))"
    [void](New-Item -ItemType Directory -Path $temporaryDirectory)
    $priPath = Join-Path $temporaryDirectory 'resources.pri'
    $dumpPath = Join-Path $temporaryDirectory 'resources.xml'
    try {
        [IO.Compression.ZipFileExtensions]::ExtractToFile($files['resources.pri'], $priPath)
        & $makePri dump /if $priPath /of $dumpPath /dt detailed | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Cannot read the packaged WinUI resource index.' }
        [xml]$index = Get-Content -LiteralPath $dumpPath -Raw
        foreach ($name in @('App.xbf', 'MainWindow.xbf', 'LibraryContents.xbf')) {
            if (-not $index.SelectSingleNode("//NamedResource[@name='$name']/Candidate[@type='EmbeddedData']/Base64Value")) {
                throw "The package lost compiled WinUI XAML: $name"
            }
        }
        foreach ($value in $index.SelectNodes('//Candidate[@type="Path"]/Value')) {
            Assert-PackageFile $value.InnerText.Replace('\', '/')
        }
    }
    finally {
        foreach ($temporaryFile in @($priPath, $dumpPath)) {
            if (Test-Path -LiteralPath $temporaryFile) { Remove-Item -LiteralPath $temporaryFile }
        }
        Remove-Item -LiteralPath $temporaryDirectory
    }
    Write-Output "MSIX payload checks passed: $PackagePath"
} finally { $package.Dispose() }
