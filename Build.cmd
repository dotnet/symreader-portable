@echo off
powershell -ExecutionPolicy ByPass .\build\Build.ps1 -restore -build %*
exit /b %ErrorLevel%
