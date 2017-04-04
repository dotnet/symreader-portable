@echo off
powershell -ExecutionPolicy ByPass .\build\Build.ps1 -test %*
exit /b %ErrorLevel%