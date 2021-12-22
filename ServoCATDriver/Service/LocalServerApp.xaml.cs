#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Joko.ServoCAT.Service.Utility;
using ASCOM.Joko.ServoCAT.View;
using ASCOM.Joko.ServoCAT.ViewModel;
using ASCOM.Utilities;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ASCOM.Joko.ServoCAT.Service {

    /// <summary>
    /// Interaction logic for LocalServerApp.xaml
    /// </summary>
    public partial class LocalServerApp : Application {
        private uint mainThreadId;
        private bool startedByCOM; // True if server started by COM (-embedding)
        private int driversInUseCount;
        private volatile int serverLockCount;
        private ArrayList driverTypes;
        private ArrayList classFactories;
        private string localServerAppId = "{289163c8-6579-4b32-90d2-68fb447a01df}";

        private readonly Object serverLock = new object();
        private Task GCTask;
        private CancellationTokenSource GCTokenSource;

        public LocalServerApp() {
            InitializeComponent();
        }

        protected override void OnStartup(StartupEventArgs e) {
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            // Create a trace logger for the local server.
            ServerLogger = new TraceLogger("", "ASCOM.LocalServer") {
                Enabled = true
            };
            ServerLogger.LogMessage("Main", $"Joko ServoCAT Server started");

            // Load driver COM assemblies and get types, ending the program if something goes wrong.
            ServerLogger.LogMessage("Main", $"Loading drivers");
            if (!PopulateListOfAscomDrivers()) {
                Current.Shutdown();
                return;
            }

            // Process command line arguments e.g. to Register/Unregister drivers, ending the program if required.
            ServerLogger.LogMessage("Main", $"Processing command-line arguments");
            if (!ProcessArguments(e.Args)) {
                Current.Shutdown();
                return;
            }

            ServerLogger.LogMessage("Main", $"Initializing variables");
            driversInUseCount = 0;
            serverLockCount = 0;
            mainThreadId = GetCurrentThreadId();
            Thread.CurrentThread.Name = "Joko.ServoCAT Local Server Thread";

            ServerLogger.LogMessage("Main", $"Creating host window");
            var mainVM = new MainVM();
            Main mainWindow = new Main();
            mainWindow.DataContext = mainVM;
            mainWindow.Show();

            // Register the class factories of the served objects
            ServerLogger.LogMessage("Main", $"Registering class factories");
            RegisterClassFactories();

            // Start the garbage collection thread.
            ServerLogger.LogMessage("Main", $"Starting garbage collection");
            StartGarbageCollection(10000);
            ServerLogger.LogMessage("Main", $"Garbage collector thread started");
        }

        protected override void OnExit(ExitEventArgs e) {
            try {
                ServerLogger.LogMessage("Main", "Revoking class factories");
                RevokeClassFactories();
                ServerLogger.LogMessage("Main", "Class factories revoked");
            } catch (Exception ex) {
                ServerLogger.LogMessage("Main", $"Failed to revoke class factories: {ex}");
            }

            try {
                ServerLogger.LogMessage("Main", $"Stopping garbage collector");
                StopGarbageCollection();
            } catch (Exception ex) {
                ServerLogger.LogMessage("Main", $"Failed to stop garbage collector: {ex}");
            }

            ServerLogger.LogMessage("Main", $"Local server closing");
            ServerLogger.Dispose();
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
            ServerLogger.LogMessage("Main", $"Joko ServoCAT Server exited with an unhandled exception: {e.Exception.GetType()}, {e.Exception.Message}");

            e.Handled = true;
            Current.Shutdown();
        }

        public static LocalServerApp App => (LocalServerApp)Current;

        public TraceLogger ServerLogger { get; private set; }

        #region Command line argument processing

        /// <summary>
        /// Process the command-line arguments returning true to continue execution or false to terminate the application immediately.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool ProcessArguments(string[] args) {
            bool returnStatus = true;

            if (args.Length > 0) {
                switch (args[0].ToLower()) {
                    case "-embedding":
                        ServerLogger.LogMessage("ProcessArguments", $"Started by COM: {args[0]}");
                        startedByCOM = true; // Indicate COM started us and continue
                        returnStatus = true; // Continue on return
                        break;

                    case "-register":
                    case @"/register":
                    case "-regserver": // Emulate VB6
                    case @"/regserver":
                        ServerLogger.LogMessage("ProcessArguments", $"Registering drivers: {args[0]}");
                        RegisterObjects(); // Register each served object
                        returnStatus = false; // Terminate on return
                        break;

                    case "-unregister":
                    case @"/unregister":
                    case "-unregserver": // Emulate VB6
                    case @"/unregserver":
                        ServerLogger.LogMessage("ProcessArguments", $"Unregistering drivers: {args[0]}");
                        UnregisterObjects(); //Unregister each served object
                        returnStatus = false; // Terminate on return
                        break;

                    default:
                        ServerLogger.LogMessage("ProcessArguments", $"Unknown argument: {args[0]}");
                        MessageBox.Show("Unknown argument: " + args[0] + "\nValid are : -register, -unregister and -embedding", "ASCOM.Joko.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        break;
                }
            } else {
                startedByCOM = false;
                ServerLogger.LogMessage("ProcessArguments", $"No arguments supplied");
            }

            return returnStatus;
        }

        #endregion

        #region COM Registration and Unregistration

        /// <summary>
        /// Register drivers contained in this local server. (Must run as Administrator.)
        /// </summary>
        /// <remarks>
        /// Do everything to register this for COM. Never use REGASM on this exe assembly! It would create InProcServer32 entries which would prevent proper activation!
        /// Using the list of COM object types generated during dynamic assembly loading, this method registers each driver for COM and registers it for ASCOM.
        /// It also adds DCOM info for the local server itself, so it can be activated via an outbound connection from TheSky.
        /// </remarks>
        private void RegisterObjects() {
            // Request administrator privilege if we don't already have it
            if (!IsAdministrator) {
                ElevateSelf("/register");
                return;
            }

            // If we reach here, we're running elevated

            // Initialise variables
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            Attribute assemblyTitleAttribute = Attribute.GetCustomAttribute(executingAssembly, typeof(AssemblyTitleAttribute));
            string assemblyTitle = ((AssemblyTitleAttribute)assemblyTitleAttribute).Title;
            assemblyTitleAttribute = Attribute.GetCustomAttribute(executingAssembly, typeof(AssemblyDescriptionAttribute));
            string assemblyDescription = ((AssemblyDescriptionAttribute)assemblyTitleAttribute).Description;
            var executablePath = Process.GetCurrentProcess().MainModule.FileName;

            // Set the local server's DCOM/AppID information
            try {
                ServerLogger.LogMessage("RegisterObjects", $"Setting local server's APPID");

                // Set HKCR\APPID\appid
                using (RegistryKey appIdKey = Registry.ClassesRoot.CreateSubKey($"APPID\\{localServerAppId}")) {
                    appIdKey.SetValue(null, assemblyDescription);
                    appIdKey.SetValue("AppID", localServerAppId);
                    appIdKey.SetValue("AuthenticationLevel", 1, RegistryValueKind.DWord);
                    appIdKey.SetValue("RunAs", "Interactive User", RegistryValueKind.String); // Added to ensure that only one copy of the local server runs if the user uses both elevated and non-elevated clients concurrently
                }

                // Set HKCR\APPID\exename.ext
                using (RegistryKey exeNameKey = Registry.ClassesRoot.CreateSubKey($"APPID\\{executablePath.Substring(executablePath.LastIndexOf('\\') + 1)}")) {
                    exeNameKey.SetValue("AppID", localServerAppId);
                }
                ServerLogger.LogMessage("RegisterObjects", $"APPID set successfully");
            } catch (Exception ex) {
                ServerLogger.LogMessageCrLf("RegisterObjects", $"Setting AppID exception: {ex}");
                MessageBox.Show("Error while registering the server:\n" + ex.ToString(), "ASCOM.Joko.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            // Register each discovered driver
            foreach (Type driverType in driverTypes) {
                ServerLogger.LogMessage("RegisterObjects", $"Creating COM registration for {driverType.Name}");
                bool bFail = false;
                try {
                    // HKCR\CLSID\clsid
                    string clsId = Marshal.GenerateGuidForType(driverType).ToString("B");
                    string progId = Marshal.GenerateProgIdForType(driverType);
                    string deviceType = driverType.Name; // Generate device type from the Class name
                    ServerLogger.LogMessage("RegisterObjects", $"Assembly title: {assemblyTitle}, ASsembly description: {assemblyDescription}, CLSID: {clsId}, ProgID: {progId}, Device type: {deviceType}");

                    using (RegistryKey clsIdKey = Registry.ClassesRoot.CreateSubKey($"CLSID\\{clsId}")) {
                        clsIdKey.SetValue(null, progId);
                        clsIdKey.SetValue("AppId", localServerAppId);
                        using (RegistryKey implementedCategoriesKey = clsIdKey.CreateSubKey("Implemented Categories")) {
                            implementedCategoriesKey.CreateSubKey("{62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}");
                        }

                        using (RegistryKey progIdKey = clsIdKey.CreateSubKey("ProgId")) {
                            progIdKey.SetValue(null, progId);
                        }
                        clsIdKey.CreateSubKey("Programmable");

                        using (RegistryKey localServer32Key = clsIdKey.CreateSubKey("LocalServer32")) {
                            localServer32Key.SetValue(null, executablePath);
                        }
                    }

                    // HKCR\CLSID\progid
                    using (RegistryKey progIdKey = Registry.ClassesRoot.CreateSubKey(progId)) {
                        progIdKey.SetValue(null, assemblyTitle);
                        using (RegistryKey clsIdKey = progIdKey.CreateSubKey("CLSID")) {
                            clsIdKey.SetValue(null, clsId);
                        }
                    }

                    // Pull the display name from the ServedClassName attribute.
                    assemblyTitleAttribute = Attribute.GetCustomAttribute(driverType, typeof(ServedClassNameAttribute));
                    string chooserName = ((ServedClassNameAttribute)assemblyTitleAttribute).DisplayName ?? "MultiServer";
                    ServerLogger.LogMessage("RegisterObjects", $"Registering {chooserName} ({driverType.Name}) in Profile");

                    using (var profile = new Profile()) {
                        profile.DeviceType = deviceType;
                        profile.Register(progId, chooserName);
                    }
                } catch (Exception ex) {
                    ServerLogger.LogMessageCrLf("RegisterObjects", $"Driver registration exception: {ex}");
                    MessageBox.Show("Error while registering the server:\n" + ex.ToString(), "ASCOM.Joko.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Stop);
                    bFail = true;
                }

                // Stop processing drivers if something has gone wrong
                if (bFail) break;
            }
        }

        /// <summary>
        /// Unregister drivers contained in this local server. (Must run as administrator.)
        /// </summary>
        private void UnregisterObjects() {
            // Request administrator privilege if we don't already have it
            if (!IsAdministrator) {
                ElevateSelf("/unregister");
                return;
            }

            // If we reach here, we're running elevated
            var executablePath = Process.GetCurrentProcess().MainModule.FileName;

            // Delete the Local Server's DCOM/AppID information
            Registry.ClassesRoot.DeleteSubKey($"APPID\\{localServerAppId}", false);
            Registry.ClassesRoot.DeleteSubKey($"APPID\\{executablePath.Substring(executablePath.LastIndexOf('\\') + 1)}", false);

            // Delete each driver's COM registration
            foreach (Type driverType in driverTypes) {
                ServerLogger.LogMessage("UnregisterObjects", $"Removing COM registration for {driverType.Name}");

                string clsId = Marshal.GenerateGuidForType(driverType).ToString("B");
                string progId = Marshal.GenerateProgIdForType(driverType);

                // Remove ProgID entries
                Registry.ClassesRoot.DeleteSubKey($"{progId}\\CLSID", false);
                Registry.ClassesRoot.DeleteSubKey(progId, false);

                // Remove CLSID entries
                Registry.ClassesRoot.DeleteSubKey($"CLSID\\{clsId}\\Implemented Categories\\{{62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}}", false);
                Registry.ClassesRoot.DeleteSubKey($"CLSID\\{clsId}\\Implemented Categories", false);
                Registry.ClassesRoot.DeleteSubKey($"CLSID\\{clsId}\\ProgId", false);
                Registry.ClassesRoot.DeleteSubKey($"CLSID\\{clsId}\\LocalServer32", false);
                Registry.ClassesRoot.DeleteSubKey($"CLSID\\{clsId}\\Programmable", false);
                Registry.ClassesRoot.DeleteSubKey($"CLSID\\{clsId}", false);

                // Uncomment the following lines to remove ASCOM Profile information when unregistering.
                // Unregistering often occurs during version upgrades and, if the code below is enabled, will result in loss of all device configuration during the upgrade.
                // For this reason, enabling this capability is not recommended.

                //try
                //{
                //    TL.LogMessage("UnregisterObjects", $"Deleting ASCOM Profile registration for {driverType.Name} ({progId})");
                //    using (var profile = new Profile())
                //    {
                //        profile.DeviceType = driverType.Name;
                //        profile.Unregister(progId);
                //    }
                //}
                //catch (Exception) { }
            }
        }

        private bool IsAdministrator {
            get {
                WindowsIdentity userIdentity = WindowsIdentity.GetCurrent();
                WindowsPrincipal userPrincipal = new WindowsPrincipal(userIdentity);
                bool isAdministrator = userPrincipal.IsInRole(WindowsBuiltInRole.Administrator);

                ServerLogger.LogMessage("IsAdministrator", isAdministrator.ToString());
                return isAdministrator;
            }
        }

        /// <summary>
        /// Elevate privileges by re-running ourselves with elevation dialogue
        /// </summary>
        /// <param name="argument">Argument to pass to ourselves</param>
        private void ElevateSelf(string argument) {
            var executablePath = Process.GetCurrentProcess().MainModule.FileName;
            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.Arguments = argument;
            processStartInfo.WorkingDirectory = Environment.CurrentDirectory;
            processStartInfo.FileName = executablePath;
            processStartInfo.Verb = "runas";
            try {
                ServerLogger.LogMessage("IsAdministrator", $"Starting elevated process");
                Process.Start(processStartInfo);
            } catch (System.ComponentModel.Win32Exception) {
                ServerLogger.LogMessage("IsAdministrator", $"The ASCOM.Joko.ServoCAT.Telescope was not " + (argument == "/register" ? "registered" : "unregistered because you did not allow it."));
                MessageBox.Show("The ASCOM.Joko.ServoCAT.Telescope was not " + (argument == "/register" ? "registered" : "unregistered because you did not allow it.", "ASCOM.Joko.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Warning));
            } catch (Exception ex) {
                ServerLogger.LogMessageCrLf("IsAdministrator", $"Exception: {ex}");
                MessageBox.Show(ex.ToString(), "ASCOM.Joko.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
            return;
        }

        #endregion

        #region Class Factory Support

        /// <summary>
        /// Register the class factories of drivers that this local server serves.
        /// </summary>
        /// <remarks>This requires the class factory name to be equal to the served class name + "ClassFactory".</remarks>
        /// <returns>True if there are no errors, otherwise false.</returns>
        private bool RegisterClassFactories() {
            ServerLogger.LogMessage("RegisterClassFactories", $"Registering class factories");
            classFactories = new ArrayList();
            foreach (Type driverType in driverTypes) {
                ServerLogger.LogMessage("RegisterClassFactories", $"  Creating class factory for: {driverType.Name}");
                ClassFactory factory = new ClassFactory(driverType); // Use default context & flags
                classFactories.Add(factory);

                ServerLogger.LogMessage("RegisterClassFactories", $"  Registering class factory for: {driverType.Name}");
                if (!factory.RegisterClassObject()) {
                    ServerLogger.LogMessage("RegisterClassFactories", $"  Failed to register class factory for " + driverType.Name);
                    MessageBox.Show($"Failed to register class factory for {driverType.Name}", "ASCOM.Joko.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return false;
                }
                ServerLogger.LogMessage("RegisterClassFactories", $"  Registered class factory OK for: {driverType.Name}");
            }

            ServerLogger.LogMessage("RegisterClassFactories", $"Making class factories live");
            ClassFactory.ResumeClassObjects(); // Served objects now go live
            ServerLogger.LogMessage("RegisterClassFactories", $"Class factories live OK");
            return true;
        }

        /// <summary>
        /// Revoke the class factories
        /// </summary>
        private void RevokeClassFactories() {
            ServerLogger.LogMessage("RevokeClassFactories", $"Suspending class factories");
            ClassFactory.SuspendClassObjects(); // Prevent race conditions
            ServerLogger.LogMessage("RevokeClassFactories", $"Class factories suspended OK");

            foreach (ClassFactory factory in classFactories) {
                factory.RevokeClassObject();
            }
        }

        #endregion

        #region Dynamic Driver Assembly Loader

        /// <summary>
        /// Populates the list of ASCOM drivers by searching for driver classes within the local server executable.
        /// </summary>
        /// <returns>True if successful, otherwise False</returns>
        private bool PopulateListOfAscomDrivers() {
            // Initialise the driver types list
            driverTypes = new ArrayList();

            try {
                // Get the types contained within the local server assembly
                Assembly so = Assembly.GetExecutingAssembly(); // Get the local server assembly
                Type[] types = so.GetTypes(); // Get the types in the assembly

                // Iterate over the types identifying those which are drivers
                foreach (Type type in types) {
                    ServerLogger.LogMessage("PopulateListOfAscomDrivers", $"Found type: {type.Name}");

                    // Check to see if this type has the ServedClassName attribute, which indicates that this is a driver class.
                    object[] attrbutes = type.GetCustomAttributes(typeof(ServedClassNameAttribute), false);
                    if (attrbutes.Length > 0) // There is a ServedClassName attribute on this class so it is a driver
                    {
                        ServerLogger.LogMessage("PopulateListOfAscomDrivers", $"  {type.Name} is a driver assembly");
                        driverTypes.Add(type); // Add the driver type to the list
                    }
                }
                ServerLogger.BlankLine();

                // Log discovered drivers
                ServerLogger.LogMessage("PopulateListOfAscomDrivers", $"Found {driverTypes.Count} drivers");
                foreach (Type type in driverTypes) {
                    ServerLogger.LogMessage("PopulateListOfAscomDrivers", $"Found Driver : {type.Name}");
                }
                ServerLogger.BlankLine();
            } catch (Exception e) {
                ServerLogger.LogMessageCrLf("PopulateListOfAscomDrivers", $"Exception: {e}");
                MessageBox.Show($"Failed to load served COM class assembly from within this local server - {e.Message}", "Rotator Simulator", MessageBoxButton.OK, MessageBoxImage.Stop);
                return false;
            }

            return true;
        }

        #endregion

        #region Lifecycle Management

        public int IncrementObjectCount() {
            int newCount = Interlocked.Increment(ref driversInUseCount);
            ServerLogger.LogMessage("IncrementObjectCount", $"New object count: {newCount}");
            return newCount;
        }

        public int DecrementObjectCount() {
            int newCount = Interlocked.Decrement(ref driversInUseCount);
            ServerLogger.LogMessage("DecrementObjectCount", $"New object count: {newCount}");
            return newCount;
        }

        public int ServerLockCount => serverLockCount;
        public int ObjectCount => driversInUseCount;

        public int IncrementServerLockCount() {
            int newCount = Interlocked.Increment(ref serverLockCount);
            ServerLogger.LogMessage("IncrementServerLockCount", $"New server lock count: {newCount}");

            return newCount;
        }

        public int DecrementServerLockLock() {
            int newCount = Interlocked.Decrement(ref serverLockCount);
            ServerLogger.LogMessage("DecrementServerLockLock", $"New server lock count: {newCount}");
            return newCount;
        }

        public void ExitIf() {
            lock (serverLock) {
                ServerLogger.LogMessage("ExitIf", $"Object count: {ObjectCount}, Server lock count: {serverLockCount}");
                if ((ObjectCount <= 0) && (ServerLockCount <= 0)) {
                    if (startedByCOM) {
                        ServerLogger.LogMessage("ExitIf", $"Server started by COM so shutting down the Windows message loop on the main process to end the local server.");
                        var wParam = UIntPtr.Zero;
                        var lParam = IntPtr.Zero;
                        PostThreadMessage(mainThreadId, 0x0012, wParam, lParam);
                    }
                }
            }
        }

        #endregion

        #region Garbage collection support

        /// <summary>
        /// Start a garbage collection thread that can be cancelled
        /// </summary>
        /// <param name="interval">Frequency of garbage collections</param>
        private void StartGarbageCollection(int interval) {
            ServerLogger.LogMessage("StartGarbageCollection", $"Creating garbage collector with interval: {interval} seconds");
            var garbageCollector = new GarbageCollection(interval);

            ServerLogger.LogMessage("StartGarbageCollection", $"Starting garbage collector thread");
            GCTokenSource = new CancellationTokenSource();
            GCTask = Task.Factory.StartNew(() => garbageCollector.GCWatch(GCTokenSource.Token), GCTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            ServerLogger.LogMessage("StartGarbageCollection", $"Garbage collector thread started OK");
        }

        /// <summary>
        /// Stop the garbage collection task by sending it the cancellation token and wait for the task to complete
        /// </summary>
        private void StopGarbageCollection() {
            // Signal the garbage collector thread to stop
            ServerLogger.LogMessage("StopGarbageCollection", $"Stopping garbage collector thread");
            GCTokenSource.Cancel();
            GCTask.Wait();
            ServerLogger.LogMessage("StopGarbageCollection", $"Garbage collector thread stopped OK");

            // Clean up
            GCTask = null;
            GCTokenSource.Dispose();
            GCTokenSource = null;
        }

        #endregion

        #region kernel32.dll and user32.dll functions

        // Post a Windows Message to a specific thread (identified by its thread id). Used to post a WM_QUIT message to the main thread in order to terminate this application.)
        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint idThread, uint Msg, UIntPtr wParam, IntPtr lParam);

        // Obtain the thread id of the calling thread allowing us to post the WM_QUIT message to the main thread.
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        #endregion
    }
}