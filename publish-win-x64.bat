@echo off
setlocal

echo Restoring packages...
dotnet restore
if errorlevel 1 exit /b %errorlevel%

echo Publishing single-file EXE...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:DebugType=None /p:DebugSymbols=false
if errorlevel 1 exit /b %errorlevel%

echo.
echo Done.
echo Output folder:
echo %~dp0bin\Release\net10.0-windows\win-x64\publish
