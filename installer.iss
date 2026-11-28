#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef AppExe
  #error AppExe is required
#endif

[Setup]
AppId={{CAAE25EB-0F69-4292-BDFB-F8B4862B538D}
AppName=lk_Screenshot
AppVersion={#AppVersion}
AppPublisher=lee2key
DefaultDirName={localappdata}\Programs\lk_Screenshot
DefaultGroupName=lk_Screenshot
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.18362
OutputBaseFilename=lk_Screenshot-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
AppMutex=Local\lkScreenshot.Prototype
SetupIconFile=assets\app.ico
UninstallDisplayIcon={app}\lk_Screenshot.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\lk_Screenshot"; Filename: "{app}\lk_Screenshot.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\lk_Screenshot"; Filename: "{app}\lk_Screenshot.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\lk_Screenshot.exe"; Description: "Launch lk_Screenshot"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
procedure RemoveInstalledAutostart(const ValueName: String);
var
  Command: String;
  ExePath: String;
begin
  ExePath := ExpandConstant('{app}\lk_Screenshot.exe');
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', ValueName, Command) then
    if (CompareText(Command, '"' + ExePath + '"') = 0) or (CompareText(Command, ExePath) = 0) then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', ValueName);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveInstalledAutostart('lk_Screenshot');
    RemoveInstalledAutostart('lkScreenshot');
  end;
end;
