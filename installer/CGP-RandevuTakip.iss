; CGP - Randevu Takip · per-user installer
; Build: dotnet publish + ISCC (see Kurulum-Olustur.ps1)

#define MyAppName "CGP - Randevu Takip"
#define MyAppVersion "1.5.0"
#define MyAppPublisher "CGP"
#define MyAppExeName "CgpRandevuTakip.Desktop.exe"

[Setup]
AppId={{8E6B2E1A-7C3F-4F4A-9D1E-2A5B7C9D3F10}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\CGP Randevu Takip
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=CGP-RandevuTakip-{#MyAppVersion}-Setup
SetupIconFile=..\src\CgpRandevuTakip.Desktop\Assets\CGPRandevuTakip.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Kısayollar:"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CgpRandevuTakip"; Flags: uninsdeletevalue

[Files]
Source: "..\dist\CGP-RandevuTakip-{#MyAppVersion}-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: files; Name: "{userappdata}\Microsoft\Windows\Start Menu\Programs\Startup\CGP - Randevu Takip.vbs"

; Kullanıcı verisi (%LOCALAPPDATA%\CgpRandevuTakip) ve yedekler kaldırılmaz.
