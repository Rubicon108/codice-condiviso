[Setup]
AppId={{A0E47494-C514-4F34-9CA9-8541C60B3AE1}
AppName=Codice Condiviso
AppVersion=0.2.0
AppPublisher=Rubicon108
DefaultDirName={autopf}\Codice Condiviso
DefaultGroupName=Codice Condiviso
OutputDir=output
OutputBaseFilename=CodiceCondiviso-Setup
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "desktop\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Codice Condiviso"; Filename: "{app}\CodiceCondiviso.exe"
Name: "{autodesktop}\Codice Condiviso"; Filename: "{app}\CodiceCondiviso.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crea un collegamento sul desktop"; GroupDescription: "Collegamenti:"
