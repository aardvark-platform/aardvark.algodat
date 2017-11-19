@echo off
SETLOCAL
REM PUSHD %~dp0\bin\Release

%~dp0\bin\Release\pointcloud.exe %*
if errorlevel 1 (
  exit /b %errorlevel%
)
