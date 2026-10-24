; Script tao bo cai dat Installer cho LMScreenShot bang Inno Setup
#define MyAppName "LMScreenShot"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "tuanhaii26"
#define MyAppURL "https://github.com/tuanhaii26/LMScreenShot"
#define MyAppExeName "LMScreenShot.exe"

[Setup]
AppId={{9F57D12A-7253-4CF5-934E-C639D275A4E1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=publish\installer
OutputBaseFilename=LMScreenShot_Setup_v1.0.0
SetupIconFile=assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "Tu dong chay cung Windows khi khoi dong (Start with Windows)"; GroupDescription: "Tuy chon them (Additional options):"; Flags: unchecked

[Files]
Source: "publish\portable\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "assets\app.ico"; DestDir: "{app}\assets"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
