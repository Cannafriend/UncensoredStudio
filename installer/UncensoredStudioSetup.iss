; Script generated for Uncensored Studio Production Release
#define MyAppName "Uncensored Studio"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Uncensored AI Studio"
#define MyAppURL "https://github.com/UncensoredAI"
#define MyAppExeName "UncensoredStudio.exe"
#define AppSourceDir "I:\UncensoredAI"

[Setup]
; Unique AppId so updates replace old installations cleanly
AppId={{9C5E4781-B674-4C2E-8931-E745DF28F120}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=I:\UncensoredAI\installer\License.txt
OutputDir=I:\UncensoredAI\installer\dist
OutputBaseFilename=UncensoredStudio-v1.0.0-Setup
SetupIconFile=I:\UncensoredAI\installer\assets\setup_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardImageFile=I:\UncensoredAI\installer\assets\wizard_banner.bmp
WizardSmallImageFile=I:\UncensoredAI\installer\assets\wizard_small.bmp
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main Executable
Source: "{#AppSourceDir}\UncensoredStudio.exe"; DestDir: "{app}"; Flags: ignoreversion

; Assets (Icons, Logo, Fonts)
Source: "{#AppSourceDir}\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppSourceDir}\Logo.png"; DestDir: "{app}"; Flags: ignoreversion

; Backend Local Engine (KoboldCPP Vulkan/CPU)
Source: "{#AppSourceDir}\backend\koboldcpp.exe"; DestDir: "{app}\backend"; Flags: ignoreversion

; Quick Launcher & Docs
Source: "{#AppSourceDir}\LaunchStudio.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppSourceDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{app}\models"; Permissions: users-modify
Name: "{app}\data"; Permissions: users-modify
Name: "{app}\exports"; Permissions: users-modify
Name: "{app}\cache"; Permissions: users-modify
Name: "{app}\temp"; Permissions: users-modify

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: startmenuicon
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{app}\cache"
