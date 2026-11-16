#ifndef PayloadRoot
  #error PayloadRoot must point to the verified release directory.
#endif
#ifndef InstallerOutput
  #error InstallerOutput must point to the installer output directory.
#endif

[Setup]
AppId={{A3FAF55E-6D04-4F7D-AE6F-B48276E48388}
AppName=GiftCompanion
AppVersion=2.0.2
AppVerName=GiftCompanion 2.0.2
VersionInfoVersion=2.0.2.0
DefaultDirName={localappdata}\Programs\GiftCompanion
DefaultGroupName=GiftCompanion
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousAppDir=yes
WizardStyle=modern
UninstallDisplayIcon={app}\GiftCompanion.exe
OutputDir={#InstallerOutput}
OutputBaseFilename=GiftCompanion-2.0.2-Setup
Compression=zip/5
SolidCompression=no
DiskSpanning=yes
DiskSliceSize=2000000000
SlicesPerDisk=1
SetupLogging=yes
CloseApplications=no
RestartApplications=no
AppMutex=Local\GiftCompanion.V1
Uninstallable=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked

[Files]
Source: "{#PayloadRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\GiftCompanion"; Filename: "{app}\GiftCompanion.exe"; WorkingDir: "{app}"
Name: "{group}\卸载 GiftCompanion"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GiftCompanion"; Filename: "{app}\GiftCompanion.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\GiftCompanion.exe"; Description: "启动 GiftCompanion"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent unchecked

