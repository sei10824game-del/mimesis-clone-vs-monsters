@echo off
REM CloneVsMonsters ビルドスクリプト (Windows)

REM 使用方法:
REM   build.bat                                    REM 環境変数 MIMESIS_PATH または自動検出
REM   build.bat C:\Path\To\MIMESIS                REM GamePath を直接指定
REM   build.bat --stub                             REM ゲームなしでスタブビルド（CI用）

set PROJECT=%~dp0CloneVsMonsters\CloneVsMonsters.csproj

if "%1"=="--stub" (
    echo === Stub build (no game required) ===
    dotnet build "%PROJECT%" -c Release -p:SkipGameRefs=true
    echo === Done: bin/Release/netstandard2.1/CloneVsMonsters.dll ===
    exit /b 0
)

if not "%1"=="" (
    echo === Building with GamePath: %1 ===
    dotnet build "%PROJECT%" -c Release -p:GamePath="%1"
) else if not "%MIMESIS_PATH%"=="" (
    echo === Building with MIMESIS_PATH: %MIMESIS_PATH% ===
    dotnet build "%PROJECT%" -c Release -p:GamePath="%MIMESIS_PATH%"
) else (
    echo === Building with auto-detected GamePath ===
    dotnet build "%PROJECT%" -c Release
)

echo === Done ===
echo Output: CloneVsMonsters/bin/Release/netstandard2.1/CloneVsMonsters.dll
echo Copy to: %%MIMESIS%%/Mods/CloneVsMonsters.dll
