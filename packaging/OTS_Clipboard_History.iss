#define MyAppName "OTS Clipboard History"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "Ohana Tech Solutions"
#define MyAppExeName "OTS.Clipboard.History.exe"

[Setup]
AppId={{6F6A1C42-281E-4286-A98F-6E8A276C9112}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\OTS Clipboard History
DefaultGroupName=OTS Clipboard History
PrivilegesRequired=lowest
UsePreviousAppDir=no
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\OTS.Clipboard.History\Resources\App.ico
OutputDir=..\dist\setup
OutputBaseFilename=OTS_Clipboard_History_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\OTS Clipboard History"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\OTS Clipboard History"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[InstallDelete]
; Remove stale shortcuts/executable from version 1.0 without touching saved clipboard history.
Type: files; Name: "{autoprograms}\OTS Clipboard.lnk"
Type: files; Name: "{autodesktop}\OTS Clipboard.lnk"
Type: files; Name: "{localappdata}\Programs\OTS Clipboard\OTS.Clipboard.exe"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch OTS Clipboard History"; Flags: nowait postinstall skipifsilent
