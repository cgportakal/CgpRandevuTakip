; CGP - Randevu Takip · per-user installer
; Build: dotnet publish + ISCC (see Kurulum-Olustur.ps1)

#define MyAppName "CGP - Randevu Takip"
#define MyAppVersion "1.8.3"
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
Type: files; Name: "{userappdata}\Microsoft\Windows\Start Menu\Programs\Startup\CGP - Randevu Takip.lnk"

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValueName = 'CgpRandevuTakip';
  AumidRoot = 'Software\Classes\AppUserModelId';
  ClsidRoot = 'Software\Classes\CLSID';
  AppExeName = 'CgpRandevuTakip.Desktop.exe';
  ToastIconRoot = '{localappdata}\ToastNotificationManagerCompat\Apps\';

var
  DeleteUserData: Boolean;

function StripBraces(const Value: String): String;
begin
  Result := Value;
  if (Length(Result) > 1) and (Result[1] = '{') and (Result[Length(Result)] = '}') then
    Result := Copy(Result, 2, Length(Result) - 2);
end;

function NormalizePath(const Value: String): String;
var
  Text: String;
begin
  Text := Value;
  StringChangeEx(Text, '/', '\', True);
  Result := Text;
end;

{ Bir komut satırından EXE yolunu çıkarır ("C:\...\app.exe" --arg veya C:\...\app.exe) }
function ExtractExecutablePath(const Command: String): String;
var
  Text: String;
  Index: Integer;
begin
  Result := '';
  Text := Trim(Command);
  if Text = '' then
    Exit;

  if Text[1] = '"' then
  begin
    Index := Pos('"', Copy(Text, 2, Length(Text) - 1));
    if Index > 0 then
      Result := Copy(Text, 2, Index - 1);
  end
  else
  begin
    Index := Pos(' ', Text);
    if Index > 0 then
      Result := Copy(Text, 1, Index - 1)
    else
      Result := Text;
  end;
end;

procedure RemoveToastIcon(const Guid: String);
begin
  if Guid = '' then
    Exit;

  DelTree(ExpandConstant(ToastIconRoot) + Uppercase(StripBraces(Guid)), True, True, True);
end;

procedure RemoveClsid(const Activator: String);
var
  Guid: String;
begin
  Guid := StripBraces(Activator);
  if Guid = '' then
    Exit;

  RegDeleteKeyIncludingSubkeys(HKCU, ClsidRoot + '\{' + Guid + '}');
  RemoveToastIcon(Guid);
end;

{ Kurulu uygulamanın ve silinmiş kopyaların bildirim kayıtlarını temizler.
  Başka bir yerde duran geçerli portable kopyanın kaydı korunur. }
procedure RemoveToastRegistrations();
var
  Names: TArrayOfString;
  I: Integer;
  FullKey, Activator, Command, Target, AppDir: String;
begin
  AppDir := ExpandConstant('{app}');
  Log('CGP: bildirim kayitlari temizleniyor. AppDir=' + AppDir);

  if RegGetSubkeyNames(HKCU, AumidRoot, Names) then
  begin
    Log('CGP: AUMID anahtar sayisi=' + IntToStr(GetArrayLength(Names)));
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Pos(AppExeName, Names[I]) = 0 then
        Continue;

      FullKey := AumidRoot + '\' + Names[I];
      Target := NormalizePath(Names[I]);
      Log('CGP: AUMID aday=' + Target);

      if FileExists(Target) and (Pos(Uppercase(AppDir), Uppercase(Target)) <> 1) then
      begin
        Log('CGP: AUMID korundu (baska konumdaki gecerli kopya).');
        Continue;
      end;

      Activator := '';
      RegQueryStringValue(HKCU, FullKey, 'CustomActivator', Activator);
      if Activator <> '' then
        RemoveClsid(Activator);

      RegDeleteKeyIncludingSubkeys(HKCU, FullKey);
      Log('CGP: AUMID silindi.');
    end;
  end;

  { AppUserModelId kaydı kalmamış yetim COM sunucuları }
  if RegGetSubkeyNames(HKCU, ClsidRoot, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      FullKey := ClsidRoot + '\' + Names[I] + '\LocalServer32';
      Command := '';
      if not RegQueryStringValue(HKCU, FullKey, '', Command) then
        Continue;

      if Pos(AppExeName, Command) = 0 then
        Continue;

      Target := ExtractExecutablePath(Command);
      if (Target <> '') and FileExists(Target) and
         (Pos(Uppercase(AppDir), Uppercase(Target)) <> 1) then
        Continue;

      RegDeleteKeyIncludingSubkeys(HKCU, ClsidRoot + '\' + Names[I]);
      RemoveToastIcon(Names[I]);
    end;
  end;
end;

procedure RemoveAutoStartEntries();
begin
  RegDeleteValue(HKCU, RunKey, RunValueName);
  DeleteFile(ExpandConstant('{userappdata}\Microsoft\Windows\Start Menu\Programs\Startup\CGP - Randevu Takip.vbs'));
  DeleteFile(ExpandConstant('{userappdata}\Microsoft\Windows\Start Menu\Programs\Startup\CGP - Randevu Takip.lnk'));
end;

procedure AskUserDataDeletion();
var
  Answer: Integer;
begin
  Answer := MsgBox(
    'Uygulama verileri de silinsin mi?' + #13#10 + #13#10 +
    'Evet: veritabanı, ayarlar ve günlükler silinir.' + #13#10 +
    'Hayır: veriler korunur (önerilen).' + #13#10 + #13#10 +
    'Yedekleriniz her durumda korunur.',
    mbConfirmation, MB_YESNO or MB_DEFBUTTON2);

  DeleteUserData := Answer = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    DeleteUserData := False;
    if not UninstallSilent then
      AskUserDataDeletion();
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    Log('CGP: usPostUninstall temizligi basladi.');
    RemoveAutoStartEntries();
    Log('CGP: otomatik baslatma girdileri silindi.');
    RemoveToastRegistrations();
    Log('CGP: bildirim kayitlari temizlendi.');

    { Güncelleme anlık görüntüsü program dosyasıdır; kullanıcı verisi sayılmaz. }
    DelTree(ExpandConstant('{localappdata}\CgpRandevuTakip\updates'), True, True, True);

    if DeleteUserData then
      DelTree(ExpandConstant('{localappdata}\CgpRandevuTakip'), True, True, True);

    { Yedekler (%USERPROFILE%\Documents\CgpRandevuTakip) hiçbir durumda silinmez. }
  end;
end;
