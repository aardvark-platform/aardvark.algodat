@echo off

dotnet tool restore
dotnet paket restore

IF "%1"=="restore" exit /B

dotnet build -c Release src/Aardvark.Algodat.sln