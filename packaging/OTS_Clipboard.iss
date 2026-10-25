#define MyAppName "OTS Clipboard"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Ohana Tech Solutions"
#define MyAppExeName "OTS.Clipboard.exe"

[Setup]
AppId={{6F6A1C42-281E-4286-A98F-6E8A276C9112}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\OTS Clipboard
DefaultGroupName=OTS Clipboard
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist\setup
OutputBaseFilename=OTS_Clipboard_Setup
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
Name: "{autoprograms}\OTS Clipboard"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\OTS Clipboard"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch OTS Clipboard"; Flags: nowait postinstall skipifsilent
