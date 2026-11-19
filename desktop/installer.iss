[Setup]
AppId={{75D5CD52-8734-4C3E-89ED-3D1CF1966514}
AppName=CoinWars
AppVersion=1.1.0
SetupIconFile=CoinWars\Assets\CoinWars.ico
DefaultDirName={localappdata}\Programs\CoinWars
DefaultGroupName=CoinWars
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=artifacts
OutputBaseFilename=CoinWars-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\CoinWars.exe
CloseApplications=yes
[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成"; Flags: unchecked
[Files]
Source: "artifacts\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
[Icons]
Name: "{group}\CoinWars"; Filename: "{app}\CoinWars.exe"
Name: "{autodesktop}\CoinWars"; Filename: "{app}\CoinWars.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\CoinWars.exe"; Description: "CoinWarsを起動"; Flags: nowait postinstall skipifsilent
