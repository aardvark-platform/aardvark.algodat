#!/bin/bash

dotnet tool restore
dotnet paket restore

dotnet build -c Release src/Aardvark.Algodat.sln
