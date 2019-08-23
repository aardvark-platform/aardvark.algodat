@echo off
SETLOCAL
PUSHD %~dp0

if NOT exist bin\points\win-x64\points.exe (
	.paket\paket.bootstrapper.exe
	.paket\paket.exe restore
	dotnet publish src\Apps\points\points.fsproj -c Release -o "%~dp0\bin\points\win-x64" -r win10-x64 --self-contained
)

bin\points\win-x64\points.exe %*

