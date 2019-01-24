@echo off
SETLOCAL
PUSHD %~dp0

if NOT exist bin\Hum\win10-x64\Hum.exe (
	dotnet publish src\Apps\Hum\Hum.fsproj -c Release -o "%~dp0\bin\hum\win10-x64" -r win10-x64 --self-contained
)

bin\Hum\win10-x64\Hum.exe %*

