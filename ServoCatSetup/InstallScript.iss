[Setup]
AppID={{4353892F-6F45-4802-A767-AB5A7ADAF783}
AppName=ASCOM ServoCAT, by ghilios
AppVerName=ASCOM ServoCAT, by ghilios 0.4
AppVersion=0.4
AppPublisher=George Hilios <ghilios@gmail.com>
AppPublisherURL=mailto:ghilios@gmail.com
AppSupportURL=https://github.com/ghilios/ASCOM.ghilios.ServoCAT/issues
AppUpdatesURL=https://github.com/ghilios/ASCOM.ghilios.ServoCAT/releases
VersionInfoVersion=1.0.0
MinVersion=6.2.9200
DefaultDirName="{commoncf}\ASCOM\Telescope\ServoCAT"
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir="Output"
OutputBaseFilename="ASCOM ServoCAT Setup"
Compression=lzma
SolidCompression=yes
WizardImageFile="..\ServoCatLogo.bmp"
LicenseFile="..\LICENSE"
UninstallFilesDir="{commoncf}\ASCOM\Uninstall\Telescope\ASCOM.ghilios.ServoCAT"

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{commoncf}\ASCOM\Uninstall\Telescope\ASCOM.ghilios.ServoCAT"

[Icons]
Name: "{group}\ServoCAT ASCOM"; Filename: "{app}\ASCOM.ghilios.ServoCAT.exe"; IconFilename: "{app}\ServoCatLogo.ico"

[Files]
Source: "..\ServoCATDriver\bin\Release\ASCOM.ghilios.ServoCAT.exe"; DestDir: "{app}"
Source: "..\ServoCatLogo.ico"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\MathNet.Numerics.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\MathNet.Spatial.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\Ninject.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\Nito.AsyncEx.Context.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\Nito.AsyncEx.Coordination.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\Nito.AsyncEx.Interop.WaitHandles.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\Nito.AsyncEx.Oop.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\Nito.AsyncEx.Tasks.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\Nito.Cancellation.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\Nito.Collections.Deque.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\Nito.Disposables.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\PostSharp.Patterns.Aggregation.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\PostSharp.Patterns.Caching.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\PostSharp.Patterns.Common.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\PostSharp.Patterns.Model.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\PostSharp.Patterns.Xaml.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\PostSharp.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\System.Buffers.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\System.Collections.Immutable.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\System.Memory.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\System.Numerics.Vectors.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\System.Runtime.CompilerServices.Unsafe.dll"; DestDir: "{app}"
Source: "..\ServoCATDriver\bin\Release\ToggleSwitch.dll"; DestDir: "{app}"

[Run]
Filename: "{app}\ASCOM.ghilios.ServoCAT.exe"; Parameters: "/register"

[Code]
const
   REQUIRED_PLATFORM_VERSION = 6.2;    // Set this to the minimum required ASCOM Platform version for this application

//
// Function to return the ASCOM Platform's version number as a double.
//
function PlatformVersion(): Double;
var
   PlatVerString : String;
begin
   Result := 0.0;  // Initialise the return value in case we can't read the registry
   try
      if RegQueryStringValue(HKEY_LOCAL_MACHINE_32, 'Software\ASCOM','PlatformVersion', PlatVerString) then 
      begin // Successfully read the value from the registry
         Result := StrToFloat(PlatVerString); // Create a double from the X.Y Platform version string
      end;
   except                                                                   
      ShowExceptionMessage;
      Result:= -1.0; // Indicate in the return value that an exception was generated
   end;
end;

//
// Before the installer UI appears, verify that the required ASCOM Platform version is installed.
//
function InitializeSetup(): Boolean;
var
   PlatformVersionNumber : double;
 begin
   Result := FALSE;  // Assume failure
   PlatformVersionNumber := PlatformVersion(); // Get the installed Platform version as a double
   If PlatformVersionNumber >= REQUIRED_PLATFORM_VERSION then	// Check whether we have the minimum required Platform or newer
      Result := TRUE
   else
      if PlatformVersionNumber = 0.0 then
         MsgBox('No ASCOM Platform is installed. Please install Platform ' + Format('%3.1f', [REQUIRED_PLATFORM_VERSION]) + ' or later from https://www.ascom-standards.org', mbCriticalError, MB_OK)
      else 
         MsgBox('ASCOM Platform ' + Format('%3.1f', [REQUIRED_PLATFORM_VERSION]) + ' or later is required, but Platform '+ Format('%3.1f', [PlatformVersionNumber]) + ' is installed. Please install the latest Platform before continuing; you will find it at https://www.ascom-standards.org', mbCriticalError, MB_OK);
end;

// Code to enable the installer to uninstall previous versions of itself when a new version is installed
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  UninstallExe: String;
  UninstallRegistry: String;
begin
  if (CurStep = ssInstall) then // Install step has started
	begin
      // Create the correct registry location name, which is based on the AppId
      UninstallRegistry := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}' + '_is1');
      // Check whether an extry exists
      if RegQueryStringValue(HKLM, UninstallRegistry, 'UninstallString', UninstallExe) then
        begin // Entry exists and previous version is installed so run its uninstaller quietly after informing the user
          MsgBox('Setup will now remove the previous version.', mbInformation, MB_OK);
          Exec(RemoveQuotes(UninstallExe), ' /SILENT', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode);
          sleep(1000);    //Give enough time for the install screen to be repainted before continuing
        end
  end;
end;

//
// Register and unregister the driver with the Chooser
// We already know that the Helper is available
//
procedure RegASCOM();
var
   P: Variant;
begin
   P := CreateOleObject('ASCOM.Utilities.Profile');
   P.DeviceType := 'Telescope';
   P.Register('ASCOM.ghilios.ServoCAT.Telescope', 'ServoCAT, by ghilios');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
   P: Variant;
begin
   if CurUninstallStep = usUninstall then
   begin
     P := CreateOleObject('ASCOM.Utilities.Profile');
     P.DeviceType := 'Telescope';
     P.Unregister('ASCOM.ghilios.ServoCAT.Telescope');
  end;
end;
