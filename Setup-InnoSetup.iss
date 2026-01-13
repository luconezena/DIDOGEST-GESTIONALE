; Script Inno Setup per DIDO-GEST
; Per creare l'installer: 
; 1. Installa Inno Setup da https://jrsoftware.org/isinfo.php
; 2. Apri questo file con Inno Setup Compiler
; 3. Clicca su Build -> Compile

#define MyAppName "DIDO-GEST"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DIDO Software"
#define MyAppURL "https://www.didogest.com"
#define MyAppExeName "DidoGest.exe"

[Setup]
; Informazioni base
AppId={{8F5A9B2C-3D4E-5F6A-7B8C-9D0E1F2A3B4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE.txt
InfoBeforeFile=README.md
OutputDir=Installer
OutputBaseFilename=DidoGest-Setup-v{#MyAppVersion}
SetupIconFile=DidoGest.UI\Resources\logo.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
Source: "Publish\DidoGest\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "Publish\DidoGest\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Non usare "Flags: ignoreversion" su file condivisi di sistema

[Dirs]
Name: "{app}\FattureElettroniche"; Permissions: users-full
Name: "{app}\Certificati"; Permissions: users-full
Name: "{app}\Archivio"; Permissions: users-full
Name: "{app}\Modelli"; Permissions: users-full
Name: "{app}\Stampe"; Permissions: users-full
Name: "{app}\Logs"; Permissions: users-full
Name: "{app}\Backup"; Permissions: users-full

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Manuale Utente"; Filename: "{app}\README.md"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\DidoGest.db"
Type: filesandordirs; Name: "{app}\Logs"
Type: files; Name: "{app}\*.log"

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  
  // Verifica se .NET 8.0 è installato
  if not RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost\8.0') then
  begin
    if MsgBox('Questo software richiede .NET 8.0 Runtime.' + #13#10 + #13#10 +
              'Vuoi scaricare e installare .NET 8.0 Runtime ora?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 
        'https://dotnet.microsoft.com/download/dotnet/8.0/runtime',
        '', '', SW_SHOW, ewNoWait, ResultCode);
      Result := False;
    end
    else
    begin
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Crea un file di configurazione di default se non esiste
    if not FileExists(ExpandConstant('{app}\App.config')) then
    begin
      FileCopy(ExpandConstant('{app}\App.config.example'), 
               ExpandConstant('{app}\App.config'), False);
    end;
  end;
end;
