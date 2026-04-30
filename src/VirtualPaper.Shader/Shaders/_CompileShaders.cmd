@echo off
setlocal
pushd "%~dp0"

:: -------------------------------------------------------
:: 允许外部通过参数传入，优先使用参数
:: 用法: CompileShaders.bat [FXC_PATH] [INCLUDE_PATH]
:: -------------------------------------------------------
if not "%~1"=="" (
    set "FXC=%~1"
) else (
    set "FXC=fxc"
    where /q fxc >nul
    if errorlevel 1 (
        echo fxc not found.
        goto WRONG_COMMAND_PROMPT
    )
)

if not "%~2"=="" (
    set "INCLUDEPATH=%~2"
) else (
    if "%WindowsSdkDir%"=="" goto WRONG_COMMAND_PROMPT
    set "INCLUDEPATH=%WindowsSdkDir%Include\%WindowsSDKVersion%um"
)

if not exist "%INCLUDEPATH%\d2d1effecthelpers.hlsli" (
    echo d2d1effecthelpers.hlsli not found in %INCLUDEPATH%
    goto WRONG_COMMAND_PROMPT
)

call :COMPILE GeometryAlphaEraseEffect.hlsl || goto END

goto END

:COMPILE
    echo.
    echo Compiling %1

    "%FXC%" %1 /nologo /T lib_4_0_level_9_3_ps_only /D D2D_FUNCTION /D D2D_ENTRY=main /Fl %~n1.fxlib /I "%INCLUDEPATH%"                        || exit /b
    "%FXC%" %1 /nologo /T ps_4_0_level_9_3 /D D2D_FULL_SHADER /D D2D_ENTRY=main /E main /setprivate %~n1.fxlib /Fo:%~n1.bin /I "%INCLUDEPATH%" || exit /b

    del %~n1.fxlib
    exit /b

:WRONG_COMMAND_PROMPT
echo Please run from a Developer Command Prompt for VS2017, or pass [FXC_PATH] [INCLUDE_PATH] as arguments.

:END
popd
exit /b %errorlevel%