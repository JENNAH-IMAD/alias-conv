#ifndef StageDir
  #define StageDir "..\output\desktop-package"
#endif
[Setup]
AppId={{C29A5A28-512A-496C-B8A5-39D82AA5AE10}
AppName=Alias Desktop
AppVersion=2.0.0
DefaultDirName={autopf}\Alias Desktop
DefaultGroupName=Alias Desktop
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\Desktop-Install
OutputBaseFilename=Alias-Desktop-Setup-2.0.0
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
SetupIconFile=assets\Alias.ico
UninstallDisplayIcon={app}\Alias.ico
[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{commondesktop}\Alias Desktop"; Filename: "{app}\desktop\Alias.Desktop.exe"; IconFilename: "{app}\Alias.ico"
Name: "{group}\Alias Desktop"; Filename: "{app}\desktop\Alias.Desktop.exe"; IconFilename: "{app}\Alias.ico"
Name: "{group}\Guide d'installation"; Filename: "{app}\GUIDE-INSTALLATION.md"
[Code]
var Ports: TInputQueryWizardPage; Data: TInputDirWizardPage; Snapshot: TInputOptionWizardPage;
function Json(Value: String): String;
begin
  StringChangeEx(Value, '\', '\\', True); StringChangeEx(Value, '"', '\"', True);
  Result := '"'+Value+'"';
end;
procedure InitializeWizard;
begin
  Ports := CreateInputQueryPage(wpSelectDir,'Application desktop','Ports des services locaux','Ces services restent accessibles uniquement sur ce PC. PostgreSQL est inclus et configure automatiquement.');
  Ports.Add('Port de l interface :',False); Ports.Values[0] := '3010';
  Ports.Add('Port du backend :',False); Ports.Values[1] := '5090';
  Data := CreateInputDirPage(Ports.ID,'Stockage local','Un dossier protege et conserve a la desinstallation','Les PDF, la base, les exports et les reglages restent sur ce PC.',False,'');
  Data.Add('Dossier des donnees :'); Data.Values[0] := ExpandConstant('{commonappdata}\Alias Desktop');
  Snapshot := CreateInputOptionPage(Data.ID,'Donnees de depart','Copie privee du projet ou nouvelle base','La copie contient les utilisateurs, les clients, les modeles, les releves et les documents presents lors de la fabrication. Les sessions et cles API ne sont pas transferees. Ne distribuez pas cet installateur publiquement.',True,False);
  Snapshot.Add('Commencer avec une nouvelle base'); Snapshot.Add('Importer la copie des donnees du projet'); Snapshot.SelectedValueIndex := 1;
end;
function NextButtonClick(CurPageID: Integer): Boolean;
var Front,Back,Code: Integer;
begin
  Result := True;
  if CurPageID = Ports.ID then begin
    Front := StrToIntDef(Ports.Values[0],0); Back := StrToIntDef(Ports.Values[1],0);
    if (Front < 1024) or (Front > 65535) or (Back < 1024) or (Back > 65535) or (Front = Back) then begin
      MsgBox('Choisissez deux ports distincts entre 1024 et 65535.',mbError,MB_OK); Result := False;
    end else begin
      Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),'-NoProfile -NonInteractive -Command "if(Get-NetTCPConnection -State Listen -LocalPort '+IntToStr(Front)+','+IntToStr(Back)+' -ErrorAction SilentlyContinue){exit 1}else{exit 0}"','',SW_HIDE,ewWaitUntilTerminated,Code);
      if Code <> 0 then begin MsgBox('Un port est deja utilise. Choisissez un autre numero.',mbError,MB_OK); Result := False; end;
    end;
  end;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := '';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),'-NoProfile -NonInteractive -Command "if(Get-Service AliasDesktopBackend,AliasDesktopFrontend,AliasDesktopPostgreSQL -ErrorAction SilentlyContinue){exit 1}else{exit 0}"','',SW_HIDE,ewWaitUntilTerminated,Code) then Result := 'Verification des services impossible.'
  else if Code <> 0 then Result := 'Alias Desktop existe deja. Sauvegardez les donnees et desinstallez la version precedente avant de reinstaller. Les donnees sont conservees.';
end;
procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer; Payload,Settings,Args: String;
begin
  if CurStep = ssPostInstall then begin
    if not Exec(ExpandConstant('{app}\installer\vc_redist.x64.exe'),'/install /quiet /norestart','',SW_HIDE,ewWaitUntilTerminated,Code) then Code := -1;
    if (Code <> 0) and (Code <> 1638) and (Code <> 3010) then RaiseException('Installation Visual C++ impossible.');
    if not DirExists(ExpandConstant('{commonpf32}\Microsoft\EdgeWebView\Application')) then begin
      if not Exec(ExpandConstant('{app}\installer\MicrosoftEdgeWebview2Setup.exe'),'/silent /install','',SW_HIDE,ewWaitUntilTerminated,Code) then Code := -1;
      if Code <> 0 then RaiseException('Installation WebView2 impossible. Verifiez Internet, puis relancez son installateur.');
    end;
    Settings := ExpandConstant('{tmp}\AliasDesktop-install.json');
    Payload := '{"FrontendPort":'+Ports.Values[0]+',"BackendPort":'+Ports.Values[1]+',"DatabaseMode":"0","DataDirectory":'+Json(Data.Values[0])+',"PublicUrl":'+Json('http://127.0.0.1:'+Ports.Values[0])+',"ImportProjectData":'+Json(IntToStr(Snapshot.SelectedValueIndex))+'}';
    SaveStringToFile(Settings,UTF8Encode(Payload),False);
    Args := '-NoProfile -ExecutionPolicy Bypass -File "'+ExpandConstant('{app}\installer\Run-DesktopInstallation.ps1')+'" -AppRoot "'+ExpandConstant('{app}')+'" -SettingsFile "'+Settings+'"';
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),Args,'',SW_HIDE,ewWaitUntilTerminated,Code) then Code := -1;
    DeleteFile(Settings);
    if Code <> 0 then RaiseException('Configuration desktop echouee. Consulter installer\desktop-installation.log et les journaux du dossier de donnees.');
  end;
end;
function InitializeUninstall: Boolean;
var Code: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),'-NoProfile -ExecutionPolicy Bypass -File "'+ExpandConstant('{app}\installer\Remove-Desktop.ps1')+'" -AppRoot "'+ExpandConstant('{app}')+'"','',SW_HIDE,ewWaitUntilTerminated,Code);
  if Result then Result := Code = 0;
end;
