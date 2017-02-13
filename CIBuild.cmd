@echo off
powershell -ExecutionPolicy ByPass .\build\Build.ps1 -restore -build -sign -pack %*
exit /b %ErrorLevel%
