#define sysfolder "system"
#define version GetEnv("NADEKOBOT_INSTALL_VERSION")
#define target "win7-x64"
#define platform "netcoreapp2.1"

[Setup]
AppName=NadekoBot
AppVersion={#version}
AppPublisher=Kwoth
DefaultDirName={commonpf}\NadekoBot
DefaultGroupName=NadekoBot
UninstallDisplayIcon={app}\{#sysfolder}\nadeko_icon.ico
Compression=lzma2
SolidCompression=yes
OutputDir=userdocs:_projekti/NadekoInstallerOutput/{#version}/
OutputBaseFilename=nadeko-setup-{#version}
AppReadmeFile=https://nadeko.bot/commands
ArchitecturesInstallIn64BitMode=x64
UsePreviousSetupType=no
DisableWelcomePage=no
WizardStyle=modern

[Files]
;install 
Source: "src\NadekoBot\bin\Release\{#platform}\{#target}\publish\*"; DestDir: "{app}\{#sysfolder}"; Permissions: users-full; Flags: recursesubdirs onlyifdoesntexist ignoreversion createallsubdirs; Excludes: "*.pdb, *.db"
Source: "src\NadekoBot\bin\Release\{#platform}\{#target}\publish\data\hangman.json"; DestDir: "{app}\{#sysfolder}\data"; DestName: "hangman.json"; Permissions: users-full; Flags: skipifsourcedoesntexist ignoreversion createallsubdirs recursesubdirs;
;rename credentials example to credentials, but don't overwrite if it exists
;Source: "src\NadekoBot\bin\Release\{#platform}\{#target}\publish\credentials_example.json"; DestName: "credentials.json"; DestDir: "{app}\{#sysfolder}"; Permissions: users-full; Flags: skipifsourcedoesntexist onlyifdoesntexist;

;reinstall - i want to copy all files, but i don't want to overwrite any data files because users will lose their customization if they don't have a backup, 
;            and i don't want them to have to backup and then copy-merge into data folder themselves, or lose their currency images due to overwrite.
Source: "src\NadekoBot\bin\Release\{#platform}\{#target}\publish\*"; DestDir: "{app}\{#sysfolder}"; Permissions: users-full; Flags: recursesubdirs ignoreversion onlyifdestfileexists createallsubdirs; Excludes: "*.pdb, *.db, data\*, credentials.json";
Source: "src\NadekoBot\bin\Release\{#platform}\{#target}\publish\data\*"; DestDir: "{app}\{#sysfolder}\data"; Permissions: users-full; Flags: recursesubdirs onlyifdoesntexist createallsubdirs;
;overwrite pokemon folder always
Source: "src\NadekoBot\bin\Release\{#platform}\{#target}\publish\data\pokemon"; DestDir: "{app}\{#sysfolder}\data\pokemon"; Permissions: users-full; Flags: skipifsourcedoesntexist ignoreversion createallsubdirs recursesubdirs;
;readme   
;Source: "readme"; DestDir: "{app}"; Flags: isreadme

[Dirs]
Name:"{app}\{#sysfolder}\data"; Permissions: everyone-modify
Name:"{app}\{#sysfolder}"; Permissions: everyone-modify

[Run]
Filename: "http://nadekobot.readthedocs.io/en/latest/jsons-explained/"; Flags: postinstall shellexec runasoriginaluser; Description: "Open setup guide"
Filename: "{app}\{#sysfolder}\credentials.json"; Flags: postinstall shellexec runasoriginaluser; Description: "Open credentials file"

[Icons]
; for pretty install directory
Name: "{app}\NadekoBot"; Filename: "{app}\{#sysfolder}\NadekoBot.exe"; IconFilename: "{app}\{#sysfolder}\nadeko_icon.ico"
Name: "{app}\credentials"; Filename: "{app}\{#sysfolder}\credentials.json" 
Name: "{app}\data"; Filename: "{app}\{#sysfolder}\data" 

; desktop shortcut 
Name: "{commondesktop}\NadekoBot"; Filename: "{app}\NadekoBot"; Tasks: desktopicon
; desktop icon checkbox
[Tasks]
Name: desktopicon; Description: "Create a &desktop shortcut";

[Registry]
;make the app run as administrator
Root: "HKLM"; Subkey: "SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; \
    ValueType: String; ValueName: "{app}\{#sysfolder}\NadekoBot.exe"; ValueData: "RUNASADMIN"; \
    Flags: uninsdeletekeyifempty uninsdeletevalue;
Root: "HKCU"; Subkey: "SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; \
    ValueType: String; ValueName: "{app}\{#sysfolder}\NadekoBot.exe"; ValueData: "RUNASADMIN"; \
    Flags: uninsdeletekeyifempty uninsdeletevalue;
Root: "HKLM"; Subkey: "SOFTWARE\NadekoBot"; \
    ValueType: String; ValueName: "InstallPath"; ValueData: "{app}\{#sysfolder}"; \
    Flags: deletevalue uninsdeletekeyifempty uninsdeletevalue;
Root: "HKLM"; Subkey: "SOFTWARE\NadekoBot"; \
    ValueType: String; ValueName: "Version"; ValueData: "{#version}"; \
    Flags: deletevalue uninsdeletekeyifempty uninsdeletevalue;

[Messages]
WelcomeLabel2=Hello, if you have any issues, join https://discord.nadeko.bot and ask for help in #help channel.%n%nIt is recommended that you CLOSE any ANTI VIRUS before continuing.

;ask the user if they want to delete all settings
[Code]
var
X: string;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    X := ExpandConstant('{app}');
    if FileExists(X + '\{#sysfolder}\data\NadekoBot.db') then begin
      if MsgBox('Do you want to delete all settings associated with this bot?', mbConfirmation, MB_YESNO) = IDYES then begin
        DelTree(X + '\{#sysfolder}', True, True, True);
      end
    end else begin
      MsgBox(X + '\{#sysfolder}\data\NadekoBot.db doesn''t exist', mbConfirmation, MB_YESNO)
    end
  end;
end;

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