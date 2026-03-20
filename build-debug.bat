@echo off
setlocal

dotnet restore
if errorlevel 1 exit /b %errorlevel%

dotnet build -c Debug
if errorlevel 1 exit /b %errorlevel%

echo.
echo Debug build complete.
