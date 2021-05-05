#define sysfolder "system"
#define version GetEnv("Mewdeko_INSTALL_VERSION")
#define target "win7-x64"
#define platform "netcoreapp2.1"

[Setup]
AppName = {param:botname|Mewdeko}
AppVersion={#version}
AppPublisher=Kwoth
DefaultDirName={param:installpath|{commonpf}\Mewdeko}
DefaultGroupName=Mewdeko
UninstallDisplayIcon={app}\{#sysfolder}\Mewdeko_icon.ico
Compression=lzma2
SolidCompression=yes
UsePreviousLanguage=no
UsePreviousSetupType=no
UsePreviousAppDir=no
OutputDir=userdocs:_projekti/Mewdeko-installers/{#version}/
OutputBaseFilename=Mewdeko-setup-{#version}
AppReadmeFile=https://Mewdeko.bot/commands
ArchitecturesInstallIn64BitMode=x64
DisableWelcomePage=yes
DisableDirPage=yes
DisableFinishedPage=yes
DisableReadyMemo=yes
DisableProgramGroupPage=yes
WizardStyle=modern
UpdateUninstallLogAppName=no
CreateUninstallRegKey=no
Uninstallable=no

[Files]
;install 
Source: "src\Mewdeko\bin\Release\{#platform}\{#target}\publish\*"; DestDir: "{app}\{#sysfolder}"; Permissions: users-full; Flags: recursesubdirs onlyifdoesntexist ignoreversion createallsubdirs; Excludes: "*.pdb, *.db"

;reinstall - i want to copy all files, but i don't want to overwrite any data files because users will lose their customization if they don't have a backup, 
;            and i don't want them to have to backup and then copy-merge into data folder themselves, or lose their currency images due to overwrite.
Source: "src\Mewdeko\bin\Release\{#platform}\{#target}\publish\*"; DestDir: "{app}\{#sysfolder}"; Permissions: users-full; Flags: recursesubdirs ignoreversion onlyifdestfileexists createallsubdirs; Excludes: "*.pdb, *.db, data\*, credentials.json";
Source: "src\Mewdeko\bin\Release\{#platform}\{#target}\publish\data\*"; DestDir: "{app}\{#sysfolder}\data"; Permissions: users-full; Flags: recursesubdirs onlyifdoesntexist createallsubdirs;
Source: "src\Mewdeko\bin\Release\{#platform}\{#target}\publish\config\*"; DestDir: "{app}\{#sysfolder}\config"; Permissions: users-full; Flags: recursesubdirs onlyifdoesntexist createallsubdirs;

[Dirs]
Name:"{app}\{#sysfolder}\data"; Permissions: everyone-modify
Name:"{app}\{#sysfolder}\config"; Permissions: everyone-modify
Name:"{app}\{#sysfolder}"; Permissions: everyone-modify

; [Run]
; Filename: "http://Mewdeko.readthedocs.io/en/latest/jsons-explained/"; Flags: postinstall shellexec runasoriginaluser; Description: "Open setup guide"
; Filename: "{app}\{#sysfolder}\credentials.json"; Flags: postinstall shellexec runasoriginaluser; Description: "Open credentials file"

[Icons]
; for pretty install directory
Name: "{app}\Mewdeko"; Filename: "{app}\{#sysfolder}\Mewdeko.exe"; IconFilename: "{app}\{#sysfolder}\Mewdeko_icon.ico"
Name: "{app}\credentials"; Filename: "{app}\{#sysfolder}\credentials.json" 
Name: "{app}\data"; Filename: "{app}\{#sysfolder}\data" 

; desktop shortcut 
Name: "{commondesktop}\{#SetupSetting("AppName")}"; Filename: "{app}\Mewdeko";

[Code]
function GetFileName(const AFileName: string): string;
begin
  Result := ExpandConstant('{app}\{#sysfolder}\' + AFileName);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) then
  begin
    if FileExists(GetFileName('credentials_example.json')) and not FileExists(GetFileName('credentials.json')) then
      RenameFile(GetFileName('credentials_example.json'), GetFileName('credentials.json'));
  end;
end;