#ifndef MyAppVersion
#define MyAppVersion "0.1.0-alpha.1"
#endif
#ifndef PublishDir
#define PublishDir "publish"
#endif

#define MyAppName "hygeia"
#define MyAppPublisher "Cong Dev"
#define MyAppExeName "hygeia.exe"

[Setup]
AppId={{8F4A1C2E-6B57-4D1A-9E33-7C0A5B2D91F4}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\hygeia
DisableDirPage=no
DefaultGroupName=hygeia
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=hygeia-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.22000
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName=hygeia
VersionInfoVersion=0.1.0.0
ShowLanguageDialog=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
Name: "{app}\data"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\hygeia"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\hygeia"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,hygeia}"; Flags: nowait postinstall skipifsilent

[Code]
procedure GrantDataFolder;
var
  DataDir: string;
  ResultCode: Integer;
begin
  DataDir := ExpandConstant('{app}\data');
  ForceDirectories(DataDir);
  Exec('icacls.exe', '"' + DataDir + '" /grant *S-1-5-32-545:(OI)(CI)M /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DataDir, Lang, Json: string;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    GrantDataFolder;
    DataDir := ExpandConstant('{app}\data');
    if not FileExists(DataDir + '\settings.json') then
    begin
      if ActiveLanguage = 'english' then
        Lang := 'en'
      else
        Lang := 'zh-CN';
      Json := '{' + #13#10 + '  "Language": "' + Lang + '"' + #13#10 + '}' + #13#10;
      SaveStringToFile(DataDir + '\settings.json', Json, False);
      Exec('icacls.exe', '"' + DataDir + '\settings.json" /grant *S-1-5-32-545:M', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'hygeia');
end;
