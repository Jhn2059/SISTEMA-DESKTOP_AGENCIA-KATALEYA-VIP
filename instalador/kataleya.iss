; Instalador del Sistema Desktop de la Agencia Kataleya VIP (Inno Setup 6).
; Se instala para el usuario de Windows, sin permisos de administrador.
; El AppId no cambia entre versiones: así una versión nueva reemplaza a la anterior.

#define Nombre "Sistema Desktop de la Agencia Kataleya VIP"
#define Version "1.0.0"
#define Ejecutable "Kataleya.Escritorio.exe"

[Setup]
AppId={{3C0A6173-4C69-476F-B884-622A5495F8DF}
AppName={#Nombre}
AppVersion={#Version}
AppPublisher=Empresa Turismo VIP Kataleya SV E.I.R.L.
DefaultDirName={autopf}\Kataleya
DefaultGroupName=Kataleya
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\publicacion
OutputBaseFilename=Instalar-Kataleya-{#Version}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#Ejecutable}
UninstallDisplayName={#Nombre}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "..\publicacion\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Kataleya"; Filename: "{app}\{#Ejecutable}"
Name: "{autodesktop}\Kataleya"; Filename: "{app}\{#Ejecutable}"

[Run]
Filename: "{app}\{#Ejecutable}"; Description: "Abrir Kataleya"; Flags: nowait postinstall skipifsilent
