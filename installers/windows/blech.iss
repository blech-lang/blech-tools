; Install the Blech Compiler
#ifndef BLECH_SOURCE
  #define BLECH_SOURCE "..\..\blech\src\blechc"
#endif

#ifndef VERSION
  #define VERSION  "0.5.4+0"
#endif


[Setup]
AppName=Blech Compiler
AppVersion={#VERSION}
AppVerName=Blech {#VERSION}
AppPublisher=Robert Bosch GmbH
AppPublisherURL=http://blech-lang.org
AppSupportURL=https://github.com/boschresearch/blech
AppCopyright=Copyright (C) 2019-2020 see blech-lang.org
DisableWelcomePage = no
DefaultDirName={userpf}\Blech
DefaultGroupName=Blech
Compression=lzma
SolidCompression=yes
OutputDir="."
OutputBaseFilename=Blech-{#VERSION}-setup
PrivilegesRequired=lowest
ChangesEnvironment=true
#ifdef LOGO
WizardImageFile=bosch.bmp
#endif
WizardImageStretch=no
;WizardImageBackColor=clWhite

[Dirs]
Name: "{app}\bin"
Name: "{app}\doc"
Name: "{app}\include"

[Files]
Source: "{#BLECH_SOURCE}\bin\Release\netcoreapp3.1\win-x64\publish\*"; DestDir: "{app}\bin"; Flags: ignoreversion
;Source: "reference.html"; DestDir: "{app}\doc"
Source: "{#BLECH_SOURCE}\include\*"; DestDir: "{app}\include"

[InstallDelete]


[Icons]

;Name: "{group}\Reference Manual"; Filename: "{app}\doc\reference.html"
Name: "{group}\Source Code"; Filename: "https://github.com/boschresearch/blech"
Name: "{group}\Uninstall Blech"; Filename: "{uninstallexe}"

[Tasks]
Name: addBlechToPath; Description: Add installation directory to the user path;

[Registry]
;Root: HKCU; Subkey: "Environment"; ValueType: string; ValueName: "BLECH"; ValueData: "{app}\bin"; Flags: deletevalue uninsdeletevalue;

[Run]

[UninstallRun]

[Code]

function BlechDir(): String;
begin
    Result := ExpandConstant('{app}\bin');
end;

procedure AddBlechToPath();
var 
    path: String;
    dir: String;
begin
    RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', path);
    dir := BlechDir();
    if Pos(dir, path) = 0 then begin  // Blech not already in the path
        path := path + ';' + dir;
        RegWriteStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', path);
    end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
	if CurStep = ssPostInstall then
		if WizardIsTaskSelected('addBlechToPath') then
			AddBlechToPath();
end;

procedure TryRemoveBlechFromPath();
var 
    path: String;
    dir: String;
    posDir: Integer;
begin
    RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', path);
    dir := BlechDir();
    posDir := Pos(dir, path);
    if posDir > 0 then begin    // Blech is in the path
        Delete(path, posDir, Length(dir)); 
        if Length(path) >= posDir then   // Remove ';' if another dir follows Blech
            if path[posDir] = ';' then
                Delete(path, posDir, 1);
        RegWriteStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', path);
    end;        
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
    if CurUninstallStep = usPostUninstall then
        TryRemoveBlechFromPath();
end;
