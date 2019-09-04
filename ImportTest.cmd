@echo off
SETLOCAL
PUSHD %~dp0

if NOT exist bin\ImportTest\win-x64\ImportTest.exe (
	.paket\paket.bootstrapper.exe
	.paket\paket.exe restore
	dotnet publish src\Apps\ImportTest\ImportTest.csproj -c Release -o "%~dp0\bin\ImportTest\win-x64" -r win10-x64 --self-contained
)

bin\ImportTest\win-x64\ImportTest.exe %*

