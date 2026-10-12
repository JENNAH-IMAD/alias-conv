#ifndef StageDir
  #define StageDir "..\output\server-package"
#endif
[Setup]
AppId={{A523F198-7FAD-40AE-B063-1BE5AE347E39}
AppName=Alias Serveur
AppVersion=1.1.6
DefaultDirName={autopf}\Alias Serveur
DefaultGroupName=Alias Serveur
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\output\installers
OutputBaseFilename=Alias-Serveur-Setup-1.1.6
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
Name: "{commondesktop}\Alias"; Filename: "{app}\Acces-Alias.url"; IconFilename: "{app}\Alias.ico"
Name: "{group}\Ouvrir Alias"; Filename: "{app}\Acces-Alias.url"; IconFilename: "{app}\Alias.ico"

[Code]
var
  PortsPage, DatabasePage, AccessPage: TInputQueryWizardPage;
  DataPage: TInputDirWizardPage;
  DatabaseModePage: TInputOptionWizardPage;
  RuntimeRestartRequired: Boolean;

function JsonString(Value: String): String;
begin
  StringChangeEx(Value, '\', '\\', True);
  StringChangeEx(Value, '"', '\"', True);
  StringChangeEx(Value, #13, '\r', True);
  StringChangeEx(Value, #10, '\n', True);
  StringChangeEx(Value, #9, '\t', True);
  Result := '"' + Value + '"';
end;

procedure InitializeWizard;
begin
  PortsPage := CreateInputQueryPage(wpSelectDir, 'Ports du serveur',
    'Choisissez deux ports libres.', 'Seul le frontend est accessible aux postes. Le backend reste local au serveur.');
  PortsPage.Add('Port frontend (acces des postes) :', False);
  PortsPage.Add('Port backend (local) :', False);
  PortsPage.Values[0] := '3002'; PortsPage.Values[1] := '5081';
  DatabaseModePage := CreateInputOptionPage(PortsPage.ID, 'Base de donnees',
    'Comment souhaitez-vous preparer la base ?',
    'Le mode automatique installe une base PostgreSQL reservee a Alias sur ce serveur. Les identifiants et un port libre sont generes automatiquement.', True, False);
  DatabaseModePage.Add('Creer et configurer automatiquement la base locale (recommande)');
  DatabaseModePage.Add('Utiliser une base PostgreSQL existante (configuration avancee)');
  DatabaseModePage.SelectedValueIndex := 0;
  DatabasePage := CreateInputQueryPage(DatabaseModePage.ID, 'Base PostgreSQL existante',
    'Renseignez uniquement une base existante.',
    'Exemple : Host=127.0.0.1;Port=5432;Database=alias;Username=alias;Password=... . Cette connexion reste uniquement sur le serveur.');
  DatabasePage.Add('Connexion PostgreSQL (masquee) :', True);
  DataPage := CreateInputDirPage(DatabasePage.ID, 'Donnees du serveur',
    'Dossier conserve lors de la desinstallation.',
    'PDF, exports, cles de chiffrement, configuration OCR et journaux seront conserves ici.', False, '');
  DataPage.Add('Dossier des donnees :');
  DataPage.Values[0] := ExpandConstant('{commonappdata}\Alias Serveur');
  AccessPage := CreateInputQueryPage(DataPage.ID, 'Adresse pour les postes',
    'Utilisez le nom ou une adresse IP fixe du serveur.',
    'Les postes ouvrent cette adresse dans leur navigateur. Le raccourci Acces-Alias.url peut etre distribue par votre administrateur.');
  AccessPage.Add('Adresse publique HTTP :', False);
  AccessPage.Values[0] := 'http://'+GetComputerNameString+':3002';
end;

function IsPortAvailable(Port: Integer): Boolean;
var Code: Integer; Args: String;
begin
  Args := '-NoProfile -NonInteractive -Command "if (Get-NetTCPConnection -State Listen -LocalPort '+IntToStr(Port)+' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"';
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Args, '', SW_HIDE, ewWaitUntilTerminated, Code);
  if Result then Result := Code = 0;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Front, Back: Integer;
begin
  Result := True;
  if CurPageID = PortsPage.ID then begin
    Front := StrToIntDef(PortsPage.Values[0], 0); Back := StrToIntDef(PortsPage.Values[1], 0);
    if (Front < 1024) or (Front > 65535) or (Back < 1024) or (Back > 65535) or (Front = Back) then begin
      MsgBox('Choisissez deux ports distincts entre 1024 et 65535.', mbError, MB_OK); Result := False;
    end else if not IsPortAvailable(Front) then begin
      MsgBox('Le port frontend '+IntToStr(Front)+' est deja utilise. Choisissez un port libre, par exemple 3005.', mbError, MB_OK); Result := False;
    end else if not IsPortAvailable(Back) then begin
      MsgBox('Le port backend '+IntToStr(Back)+' est deja utilise. Choisissez un port libre, par exemple 5085.', mbError, MB_OK); Result := False;
    end else AccessPage.Values[0] := 'http://'+GetComputerNameString+':'+IntToStr(Front);
  end;
  if (CurPageID = DatabasePage.ID) and (DatabaseModePage.SelectedValueIndex = 1) and (Trim(DatabasePage.Values[0]) = '') then begin
    MsgBox('Renseignez la connexion PostgreSQL.', mbError, MB_OK); Result := False;
  end;
  if (CurPageID = DataPage.ID) and
    (Pos(Lowercase(AddBackslash(ExpandConstant('{app}'))), Lowercase(AddBackslash(DataPage.Values[0]))) = 1) then begin
    MsgBox('Choisissez un dossier de donnees en dehors du dossier du programme.', mbError, MB_OK); Result := False;
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = DatabasePage.ID) and (DatabaseModePage.SelectedValueIndex = 0);
end;

function NeedRestart: Boolean;
begin
  Result := RuntimeRestartRequired;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer; Args: String;
begin
  Result := '';
  Args := '-NoProfile -NonInteractive -Command "if (Get-Service -Name AliasBackend,AliasFrontend,AliasPostgreSQL -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Args, '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := 'Impossible de verifier les services Windows. Verifiez les droits administrateur.'
  else if Code <> 0 then
    Result := 'Des services Alias existent deja. Sauvegardez la base et les donnees, puis desinstallez Alias avant de reinstaller. Les donnees sont conservees.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Settings, Payload, Args: String; Code: Integer;
begin
  if CurStep = ssPostInstall then begin
    if DatabaseModePage.SelectedValueIndex = 0 then begin
      if not Exec(ExpandConstant('{app}\installer\vc_redist.x64.exe'), '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, Code) then Code := -1;
      if (Code <> 0) and (Code <> 1638) and (Code <> 3010) then
        RaiseException('Installation du runtime PostgreSQL impossible. Verifiez les droits administrateur.');
      if Code = 3010 then RuntimeRestartRequired := True;
    end;
    Settings := ExpandConstant('{tmp}\Alias-install.json');
    Payload := '{"FrontendPort":'+IntToStr(StrToInt(PortsPage.Values[0]))+',"BackendPort":'+IntToStr(StrToInt(PortsPage.Values[1]))+
      ',"DatabaseMode":'+JsonString(IntToStr(DatabaseModePage.SelectedValueIndex))+',"ConnectionString":'+JsonString(DatabasePage.Values[0])+',"DataDirectory":'+JsonString(DataPage.Values[0])+
      ',"PublicUrl":'+JsonString(AccessPage.Values[0])+'}';
    if not SaveStringToFile(Settings, UTF8Encode(Payload), False) then
      RaiseException('Impossible de preparer la configuration temporaire.');
    Args := '-NoProfile -ExecutionPolicy Bypass -File "'+ExpandConstant('{app}\installer\Run-Installation.ps1')+
      '" -AppRoot "'+ExpandConstant('{app}')+'" -SettingsFile "'+Settings+'"';
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Args, '', SW_HIDE, ewWaitUntilTerminated, Code) then Code := -1;
    DeleteFile(Settings); DatabasePage.Values[0] := ''; Payload := '';
    if Code <> 0 then RaiseException('Configuration serveur echouee. Consultez '+ExpandConstant('{app}\installer\installation.log')+' et les journaux du dossier de donnees.');
  end;
end;

function InitializeUninstall: Boolean;
var Code: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -File "'+ExpandConstant('{app}\installer\Remove-Services.ps1')+
    '" -AppRoot "'+ExpandConstant('{app}')+'"', '', SW_HIDE, ewWaitUntilTerminated, Code);
  if Result then Result := Code = 0;
  if not Result then MsgBox('Impossible de retirer les services. Verifiez les droits administrateur.', mbError, MB_OK);
end;
