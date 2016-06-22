@echo off
@setlocal

set NuGetExe="%~dp0NuGet.exe"
set NuGetAdditionalCommandLineArgs=-verbosity quiet -configfile "%~dp0nuget.config" -Project2ProjectTimeOut 1200
set Solution=%~dp0SymReaderPortable.sln

echo Restoring packages: Toolsets
call %NugetExe% restore "%~dp0build\ToolsetPackages\project.json" %NuGetAdditionalCommandLineArgs% || goto :RestoreFailed

echo Restoring packages: %Solution%
call %NugetExe% restore "%Solution%" %NuGetAdditionalCommandLineArgs% || goto :RestoreFailed

exit /b 0

:RestoreFailed
echo Restore failed with ERRORLEVEL %ERRORLEVEL%
exit /b 1