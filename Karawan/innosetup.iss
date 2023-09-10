; -- innosetup.iss --
; Creates setup file for silicon desert.

[Setup]
AppName=Silicon Desert 2
AppVersion=0.0390
WizardStyle=modern
DefaultDirName={autopf}\SiliconDesert2
DefaultGroupName=SiliconDesert2
UninstallDisplayIcon={app}\Karawan.exe
Compression=lzma2
SolidCompression=yes
SourceDir=.\bin\Release\net7.0-windows10.0.17763.0\
OutputDir=..\..\..\..\Setup\
OutputBaseFilename=InstallSiliconDesert2
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "*.dll"; DestDir: "{app}\"; Flags: ignoreversion
Source: "Karawan.runtimeconfig.json"; DestDir: "{app}\"; Flags: ignoreversion
Source: "Karawan.xml"; DestDir: "{app}\"; Flags: ignoreversion
Source: "Karawan.exe"; DestDir: "{app}\"; Flags: ignoreversion
Source: "runtimes\win-x64\native\*"; DestDir: "{app}\"; Flags: ignoreversion 
Source: "..\..\..\..\Wuka\Platforms\Android\*.ogg"; DestDir: "{app}\assets\"; Flags: ignoreversion
Source: "..\..\..\..\Wuka\Platforms\Android\*.mtl"; DestDir: "{app}\assets\"; Flags: ignoreversion
Source: "..\..\..\..\Wuka\Platforms\Android\*.obj"; DestDir: "{app}\assets\"; Flags: ignoreversion
Source: "..\..\..\..\Wuka\Platforms\Android\*.png"; DestDir: "{app}\assets\"; Flags: ignoreversion
Source: "..\..\..\..\Wuka\Platforms\Android\*.ttf"; DestDir: "{app}\assets\"; Flags: ignoreversion
Source: "..\..\..\appicon.ico"; DestDir: "{app}\"; Flags: ignoreversion

[Icons]
Name: "{group}\SiliconDesert2"; Filename: "{app}\Karawan.exe"; IconFilename: "{app}\appicon.ico"
