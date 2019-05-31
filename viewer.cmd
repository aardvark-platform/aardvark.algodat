@echo off
SETLOCAL
PUSHD %~dp0

if NOT exist bin\Viewer\win10-x64\Viewer.exe (
	.paket\paket.bootstrapper.exe
	.paket\paket.exe restore
	dotnet publish src\Apps\Viewer\Viewer.fsproj -c Release -o "%~dp0\bin\Viewer\win10-x64" -r win10-x64 --self-contained
)

bin\Viewer\win10-x64\Viewer.exe %*

