@echo off
SETLOCAL
PUSHD %~dp0

dotnet publish src\Apps\Viewer\Viewer.fsproj -c Release -o "%~dp0\bin\Viewer\win10-x64" -r win10-x64 --self-contained
dotnet publish src\Apps\points\points.fsproj -c Release -o "%~dp0\bin\points\win-x64" -r win10-x64 --self-contained

