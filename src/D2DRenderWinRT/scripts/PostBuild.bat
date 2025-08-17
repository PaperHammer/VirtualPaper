@echo off
setlocal enabledelayedexpansion

:: 获取参数
set ProjectDir=%~1
set Platform=%~2
set Configuration=%~3
set TargetProjectName=%~4
set DotnetPlatform=%~5

set ProjectDir=%ProjectDir:~0,-1%

:: 调用 PowerShell 脚本
powershell -ExecutionPolicy Bypass -NoProfile -File "%ProjectDir%\PostBuild.ps1" ^
    -ProjectDir "%ProjectDir%" ^
    -Platform "%Platform%" ^
    -Configuration "%Configuration%" ^
    -TargetProjectName "%TargetProjectName%" ^
    -DotnetPlatform "%DotnetPlatform%"

endlocal