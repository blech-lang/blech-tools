@echo off

rem Relative Path to submodule blech
set BLECH_SOURCE=..\..\blech\src\blechc

if not exist %BLECH_SOURCE% (
    echo "Please configure the relative path to the Blech compiler sources"
    goto :end
)

rem Add path to Innosetup
set INNOSETUP="%HOME%\AppData\Local\Programs\Inno Setup 6"
if not exist %INNOSETUP% (
    echo "Please install Inno Setup 6 from http://www.jrsoftware.org/isdl.php use option -> Install for me only"
    goto :end
)

set PATH=%INNOSETUP%;%PATH% 

where dotnet
if not %ERRORLEVEL% == 0 (
    echo "Please install .NET Core from https://www.microsoft.com/net/download"
    goto :end
)

rem Blech semantic versioning
rem major.minor.patch.build

rem Build = Commits since tagging 
pushd %BLECH_SOURCE%
git rev-list --count HEAD > build.txt
set /p BUILD=<build.txt
del build.txt
popd

set PATCH=2

set DOTNET_VERSION=0.7.%PATCH%.%BUILD%
set SEMANTIC_VERSION=0.7.%PATCH%+%BUILD%


rem build the blech compiler
dotnet publish /p:VERSION=%DOTNET_VERSION% ^
    --runtime win-x64 ^
    --configuration Release ^
    --verbosity normal ^
    --self-contained ^
        %BLECH_SOURCE%\blechc.fsproj


rem Generate the installer
if exist bosch.bmp (
    iscc.exe /DBLECH_SOURCE=%BLECH_SOURCE% /DVERSION=%SEMANTIC_VERSION% /DLOGO blech.iss
) else (
    iscc.exe /DBLECH_SOURCE=%BLECH_SOURCE% /DVERSION=%SEMANTIC_VERSION% blech.iss
)

:end
pause