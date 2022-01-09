#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.ghilios.ServoCAT.Service.Utility;
using ASCOM.ghilios.ServoCAT.View;
using ASCOM.ghilios.ServoCAT.ViewModel;
using ASCOM.Utilities;
using Microsoft.Win32;
using Ninject;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ASCOM.ghilios.ServoCAT.Service {

    /// <summary>
    /// Main entry point for the local driver server
    /// </summary>
    public partial class LocalServerApp : Application {
        private uint mainThreadId;
        private int driversInUseCount;
        private volatile int serverLockCount;
        private ArrayList driverTypes;
        private ArrayList classFactories = new ArrayList();
        private const string LOCAL_SERVER_APPID = "{289163c8-6579-4b32-90d2-68fb447a01df}";
        private Main mainWindow;
        private IMainVM mainVM;
        private IDriverConnectionManager driverConnectionManager;
        private IServoCatOptions servoCatOptions;
        private TraceLogger ServerLogger;
        private TraceLogger TelescopeLogger;
        private TraceLogger SerialLogger;

        private readonly object serverLock = new object();

        public LocalServerApp() {
            InitializeComponent();
        }

        protected override void OnStartup(StartupEventArgs e) {
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            ServerLogger = CompositionRoot.Kernel.Get<TraceLogger>("Server");
            TelescopeLogger = CompositionRoot.Kernel.Get<TraceLogger>("Telescope");
            SerialLogger = CompositionRoot.Kernel.Get<TraceLogger>("Serial");
            servoCatOptions = CompositionRoot.Kernel.Get<IServoCatOptions>();

            ServerLogger.Enabled = servoCatOptions.EnableServerLogging;
            TelescopeLogger.Enabled = servoCatOptions.EnableTelescopeLogging;
            SerialLogger.Enabled = servoCatOptions.EnableSerialLogging;
            ((INotifyPropertyChanged)servoCatOptions).PropertyChanged += ServoCatOptions_PropertyChanged;
            ServerLogger.LogMessage("Main", $"ghilios ServoCAT Server started");

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
            Thread.CurrentThread.Name = "ghilios ServoCAT Local Server Thread";

            ServerLogger.LogMessage("Main", $"Creating host window");

            driverConnectionManager = CompositionRoot.Kernel.Get<IDriverConnectionManager>();
            driverConnectionManager.OnConnected += DriverConnectionManager_OnConnected;
            driverConnectionManager.OnDisconnected += DriverConnectionManager_OnDisconnected;

            mainVM = CompositionRoot.Kernel.Get<MainVM>();

            mainWindow = new Main();
            App.MainWindow = mainWindow;
            mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            mainWindow.Title = "ServoCAT";
            mainWindow.DataContext = mainVM;
            if (!StartedByCOM) {
                mainWindow.Show();
            }

            // Register the class factories of the served objects
            ServerLogger.LogMessage("Main", $"Registering class factories");
            RegisterClassFactories();
        }

        private void ShowWindow() {
            mainWindow.Show();
        }

        private void HideWindow() {
            mainWindow.Hide();
        }

        private void ServoCatOptions_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(servoCatOptions.EnableServerLogging)) {
                ServerLogger.Enabled = servoCatOptions.EnableServerLogging;
            } else if (e.PropertyName == nameof(servoCatOptions.EnableTelescopeLogging)) {
                TelescopeLogger.Enabled = servoCatOptions.EnableTelescopeLogging;
            } else if (e.PropertyName == nameof(servoCatOptions.EnableSerialLogging)) {
                SerialLogger.Enabled = servoCatOptions.EnableSerialLogging;
            }
        }

        public bool StartedByCOM { get; private set; }

        private object connectedClientsLock = new object();
        private HashSet<Guid> connectedClients = new HashSet<Guid>();

        private void DriverConnectionManager_OnDisconnected(object sender, ConnectionEventArgs e) {
            int clientCount;
            lock (connectedClientsLock) {
                connectedClients.Remove(e.ClientGuid);
                clientCount = connectedClients.Count;
            }

            ServerLogger?.LogMessage("Main", $"{e.ClientGuid} disconnected. {clientCount} connected clients remaining");
            if (clientCount == 0 && StartedByCOM) {
                ServerLogger.LogMessage("Main", $"Making main window hidden");
                Dispatcher.Invoke(HideWindow);
            }
        }

        private void DriverConnectionManager_OnConnected(object sender, ConnectionEventArgs e) {
            int clientCount;
            lock (connectedClientsLock) {
                connectedClients.Add(e.ClientGuid);
                clientCount = connectedClients.Count;
            }

            ServerLogger.LogMessage("Main", $"{e.ClientGuid} connected. {clientCount} connected clients");
            if (clientCount == 1 && StartedByCOM) {
                ServerLogger.LogMessage("Main", $"Making main window visible");
                Dispatcher.Invoke(ShowWindow);
            }
        }

        protected override void OnExit(ExitEventArgs e) {
            mainVM?.Stop();

            try {
                ServerLogger.LogMessage("Main", "Revoking class factories");
                RevokeClassFactories();
                ServerLogger.LogMessage("Main", "Class factories revoked");
            } catch (Exception ex) {
                ServerLogger.LogMessage("Main", $"Failed to revoke class factories: {ex}");
            }

            try {
                CompositionRoot.Kernel?.Dispose();
            } catch (Exception ex) {
                ServerLogger.LogMessage("Main", $"Failed to dispose composition root kernel: {ex}");
            }

            ServerLogger.LogMessage("Main", $"Local server closing");
            ServerLogger.Dispose();
            ServerLogger = null;
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
            ServerLogger.LogMessage("Main", $"ghilios ServoCAT Server exited with an unhandled exception: {e.Exception.GetType()}, {e.Exception.Message}");
            MessageBox.Show($"Unhandled exception {e.Exception.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);

            e.Handled = true;
            Current?.Shutdown();
        }

        public static LocalServerApp App => Current as LocalServerApp;

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
                        StartedByCOM = true;
                        returnStatus = true;
                        break;

                    case "-register":
                    case @"/register":
                    case "-regserver": // Emulate VB6
                    case @"/regserver":
                        ServerLogger.LogMessage("ProcessArguments", $"Registering drivers: {args[0]}");
                        RegisterObjects();
                        returnStatus = false;
                        break;

                    case "-unregister":
                    case @"/unregister":
                    case "-unregserver": // Emulate VB6
                    case @"/unregserver":
                        ServerLogger.LogMessage("ProcessArguments", $"Unregistering drivers: {args[0]}");
                        UnregisterObjects(false);
                        returnStatus = false;
                        break;

                    case "-fullremove":
                        ServerLogger.LogMessage("ProcessArguments", $"Fully removing drivers: {args[0]}");
                        UnregisterObjects(true);
                        returnStatus = false;
                        break;

                    default:
                        ServerLogger.LogMessage("ProcessArguments", $"Unknown argument: {args[0]}");
                        MessageBox.Show("Unknown argument: " + args[0] + "\nValid are : -register, -unregister and -embedding", "ASCOM.ghilios.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        break;
                }
            } else {
                StartedByCOM = false;
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
            if (!WindowsUtility.IsAdministrator()) {
                ElevateSelf("/register");
                return;
            }

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
                using (RegistryKey appIdKey = Registry.ClassesRoot.CreateSubKey($"APPID\\{LOCAL_SERVER_APPID}")) {
                    appIdKey.SetValue(null, assemblyDescription);
                    appIdKey.SetValue("AppID", LOCAL_SERVER_APPID);
                    appIdKey.SetValue("AuthenticationLevel", 1, RegistryValueKind.DWord);
                    appIdKey.SetValue("RunAs", "Interactive User", RegistryValueKind.String); // Added to ensure that only one copy of the local server runs if the user uses both elevated and non-elevated clients concurrently
                }

                // Set HKCR\APPID\exename.ext
                using (RegistryKey exeNameKey = Registry.ClassesRoot.CreateSubKey($"APPID\\{executablePath.Substring(executablePath.LastIndexOf('\\') + 1)}")) {
                    exeNameKey.SetValue("AppID", LOCAL_SERVER_APPID);
                }
                ServerLogger.LogMessage("RegisterObjects", $"APPID set successfully");
            } catch (Exception ex) {
                ServerLogger.LogMessageCrLf("RegisterObjects", $"Setting AppID exception: {ex}");
                MessageBox.Show("Error while registering the server:\n" + ex.ToString(), "ASCOM.ghilios.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Stop);
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
                        clsIdKey.SetValue("AppId", LOCAL_SERVER_APPID);
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
                    MessageBox.Show("Error while registering the server:\n" + ex.ToString(), "ASCOM.ghilios.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Stop);
                    bFail = true;
                }

                // Stop processing drivers if something has gone wrong
                if (bFail) break;
            }
        }

        /// <summary>
        /// Unregister drivers contained in this local server. (Must run as administrator.)
        /// </summary>
        private void UnregisterObjects(bool removeProfile) {
            if (!WindowsUtility.IsAdministrator()) {
                ElevateSelf("/unregister");
                return;
            }

            var executablePath = Process.GetCurrentProcess().MainModule.FileName;

            // Delete the Local Server's DCOM/AppID information
            Registry.ClassesRoot.DeleteSubKey($"APPID\\{LOCAL_SERVER_APPID}", false);
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

                // -fullremove removes the driver from the chooser, but also the saved configuration. This should be done only during a full uninstall
                if (removeProfile) {
                    try {
                        ServerLogger.LogMessage("UnregisterObjects", $"Deleting ASCOM Profile registration for {driverType.Name} ({progId})");
                        using (var profile = new Profile()) {
                            profile.DeviceType = driverType.Name;
                            profile.Unregister(progId);
                        }
                    } catch (Exception) { }
                }
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
                ServerLogger.LogMessage("IsAdministrator", $"The ASCOM.ghilios.ServoCAT.Telescope was not " + (argument == "/register" ? "registered" : "unregistered because you did not allow it."));
                MessageBox.Show("The ASCOM.ghilios.ServoCAT.Telescope was not " + (argument == "/register" ? "registered" : "unregistered because you did not allow it.", "ASCOM.ghilios.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Warning));
            } catch (Exception ex) {
                ServerLogger.LogMessageCrLf("IsAdministrator", $"Exception: {ex}");
                MessageBox.Show(ex.ToString(), "ASCOM.ghilios.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Stop);
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
                    MessageBox.Show($"Failed to register class factory for {driverType.Name}", "ASCOM.ghilios.ServoCAT.Telescope", MessageBoxButton.OK, MessageBoxImage.Stop);
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
                if (StartedByCOM) {
                    if ((ObjectCount <= 0) && (ServerLockCount <= 0)) {
                        Task.Run(async () => {
                            await Task.Delay(TimeSpan.FromSeconds(5));
                            if ((ObjectCount <= 0) && (ServerLockCount <= 0)) {
                                ServerLogger.LogMessage("ExitIf", $"Server started by COM so shutting down the Windows message loop on the main process to end the local server.");
                                var wParam = UIntPtr.Zero;
                                var lParam = IntPtr.Zero;
                                PostThreadMessage(mainThreadId, 0x0012, wParam, lParam);
                            } else {
                                ServerLogger.LogMessage("ExitIf", $"Exit aborted due to new connection");
                            }
                        });
                    }
                }
            }
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