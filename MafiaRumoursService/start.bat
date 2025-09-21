@echo off
setlocal

:: ==============================================================================
:: Launch Script for Mafia Rumours Service (Windows)
:: ------------------------------------------------------------------------------
:: This script builds and runs the .NET application. The application itself
:: will load configuration from the .env file in the root directory.
:: ==============================================================================

:: Define the relative path to your main project directory
set "PROJECT_DIR=MafiaRumoursService"
set "DLL_NAME=MafiaRumoursService.dll"
set "DLL_PATH=bin\Debug\net9.0\%DLL_NAME%"

:: Check if the project directory exists
if not exist "%PROJECT_DIR%" (
    echo Error: Project directory '%PROJECT_DIR%' not found.
    echo Please run this script from the root of your solution.
    pause
    exit /b 1
)

echo Building Mafia Rumours Service...
pushd "%PROJECT_DIR%"
dotnet build
popd

echo Build complete.
echo Running Mafia Rumours Service...

pushd "%PROJECT_DIR%"
dotnet "%DLL_PATH%"
popd

echo Application has been shut down.
pause