@echo off
powershell -ExecutionPolicy ByPass .\build\Build.ps1 -restore %*
exit /b %ErrorLevel%
