@echo off
SETLOCAL
PUSHD %~dp0

ECHO bootstrapping package manager ...
.paket\paket.bootstrapper.exe
if errorlevel 1 (
  exit /b %errorlevel%
)

SET FSI_PATH=packages\build\FAKE\tools\Fake.exe

IF exist boot.fsx ( 
    "%FSHARPINSTALLDIR%fsi.exe" "boot.fsx" 
    del "boot.fsx"
	.paket\paket.exe install
)

if NOT exist paket.lock (
	echo No paket.lock found, running paket install.
	.paket\paket.exe install
)

"%FSI_PATH%" "build.fsx" Dummy --fsiargs build.fsx %* 
