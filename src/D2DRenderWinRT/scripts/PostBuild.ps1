<#
.SYNOPSIS
    C++项目生成后文件复制工具（修正文件匹配问题）
#>
param(
    [string]$ProjectDir,
    [string]$Platform,
    [string]$Configuration,
    [string]$TargetProjectName,
    [string]$DotnetPlatform = "net8.0-windows10.0.19041.0"
)

# 确保路径格式正确
$ProjectDir = $ProjectDir.TrimEnd('\')
$SourceDir = "$ProjectDir\bin\$Platform\$Configuration"
$SolutionDir = Split-Path $ProjectDir -Parent
$TargetDir = "$SolutionDir\$TargetProjectName\bin\$Configuration\$DotnetPlatform"

Write-Host "`n========== 路径信息 =========="
Write-Host "源目录: $SourceDir"
Write-Host "目标目录: $TargetDir`n"

# 创建目标目录（如果不存在）
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Write-Host "已创建目标目录: $TargetDir"
}

# 根据配置决定要复制的文件
if ($Configuration -eq "Debug") {
    $fileTypes = @("dll", "exe", "pdb", "lib", "exp")
    Write-Host "Debug模式 - 复制所有输出文件"
} else {
    $fileTypes = @("dll", "exe")
    Write-Host "Release模式 - 仅复制DLL和EXE文件"
}

# 修正后的文件复制逻辑
$copiedFiles = 0
if (Test-Path $SourceDir) {
    # 获取源目录所有文件
    $allFiles = Get-ChildItem -Path $SourceDir -File
    
    foreach ($type in $fileTypes) {
        $matchedFiles = $allFiles | Where-Object { $_.Extension -eq ".$type" }
        foreach ($file in $matchedFiles) {
            try {
                $destFile = Join-Path $TargetDir $file.Name
                Copy-Item -Path $file.FullName -Destination $destFile -Force -ErrorAction Stop
                Write-Host "已复制: $($file.Name)"
                $copiedFiles++
            }
            catch {
                Write-Host "错误: 无法复制 $($file.Name) - $_" -ForegroundColor Red
            }
        }
    }
} else {
    Write-Host "错误: 源目录不存在 - $SourceDir" -ForegroundColor Red
    exit 1
}

# 输出结果
if ($copiedFiles -gt 0) {
    Write-Host "`n成功复制 $copiedFiles 个文件到: $TargetDir" -ForegroundColor Green
} else {
    Write-Host "`n警告: 在 $SourceDir 中未找到匹配的文件" -ForegroundColor Yellow
    Write-Host "目录内容:"
    Get-ChildItem $SourceDir | ForEach-Object { Write-Host " - $($_.Name)" }
}

exit 0