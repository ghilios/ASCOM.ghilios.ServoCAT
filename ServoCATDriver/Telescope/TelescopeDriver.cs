#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Astrometry.NOVAS;
using ASCOM.DeviceInterface;
using ASCOM.Joko.ServoCAT.Interfaces;
using ASCOM.Joko.ServoCAT.Service;
using ASCOM.Joko.ServoCAT.Service.Utility;
using ASCOM.Joko.ServoCAT.Threading;
using ASCOM.Joko.ServoCAT.Utility;
using ASCOM.Joko.ServoCAT.ViewModel;
using ASCOM.Utilities;
using Ninject;
using System;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ASCOM.Joko.ServoCAT.Telescope {

    //
    // Your driver's DeviceID is ASCOM.Joko.ServoCAT.Telescope
    //
    // The Guid attribute sets the CLSID for ASCOM.Joko.ServoCAT.Telescope
    // The ClassInterface/None attribute prevents an empty interface called
    // _Joko.ServoCAT from being created and used as the [default] interface
    //
    [ComVisible(true)]
    [Guid("02891d62-2316-476e-93ad-bb4bea5ac154")]
    [ProgId("ASCOM.Joko.ServoCAT.Telescope")]
    [ServedClassName("ServoCAT Driver, by George Hilios")]
    [ClassInterface(ClassInterfaceType.None)]
    public class Telescope : ReferenceCountedObjectBase, ITelescopeV3 {
        private readonly IServoCatOptions servoCatOptions;
        private readonly ISharedState sharedState;
        private readonly Util ascomUtilities;
        private readonly IAstroUtils astroUtilities;
        private readonly TraceLogger Logger;
        private readonly IDriverConnectionManager driverConnectionManager;
        private readonly IServoCatDeviceFactory servoCatDeviceFactory;
        private readonly Guid driverClientId;
        private readonly ISerialUtilities serialUtilities;

        private IServoCatDevice servoCatDevice;
        private bool connectedState = false;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Joko.ServoCAT"/> class. Must be public to successfully register for COM.
        /// </summary>
        public Telescope() : this(
            sharedState: CompositionRoot.Kernel.Get<ISharedState>(),
            options: CompositionRoot.Kernel.Get<IServoCatOptions>(),
            driverConnectionManager: CompositionRoot.Kernel.Get<IDriverConnectionManager>(),
            servoCatDeviceFactory: CompositionRoot.Kernel.Get<IServoCatDeviceFactory>(),
            logger: CompositionRoot.Kernel.Get<TraceLogger>("Telescope"),
            astroUtilities: CompositionRoot.Kernel.Get<IAstroUtils>(),
            ascomUtilities: CompositionRoot.Kernel.Get<Util>(),
            serialUtilities: CompositionRoot.Kernel.Get<ISerialUtilities>()) {
        }

        public Telescope(
            ISharedState sharedState,
            IServoCatOptions options,
            IDriverConnectionManager driverConnectionManager,
            IServoCatDeviceFactory servoCatDeviceFactory,
            TraceLogger logger,
            IAstroUtils astroUtilities,
            Util ascomUtilities,
            ISerialUtilities serialUtilities) {
            try {
                if (string.IsNullOrEmpty(sharedState.TelescopeDriverId)) {
                    throw new ASCOM.DriverException("ProgID is not set");
                }

                if (string.IsNullOrEmpty(sharedState.TelescopeDriverDescription)) {
                    throw new ASCOM.DriverException("DriverDescription is not set");
                }

                this.sharedState = sharedState;
                this.servoCatOptions = options;
                this.driverConnectionManager = driverConnectionManager;
                this.servoCatDeviceFactory = servoCatDeviceFactory;
                this.serialUtilities = serialUtilities;
                Logger = logger;
                Logger.LogMessage("Telescope", "Starting initialization");
                Logger.LogMessage("Telescope", $"ProgID: {sharedState.TelescopeDriverId}, Description: {sharedState.TelescopeDriverDescription}");

                this.driverClientId = driverConnectionManager.RegisterClient();
                Logger.LogMessage("Telescope", $"Registed with driver client id {driverClientId}");
                this.ascomUtilities = ascomUtilities;
                this.astroUtilities = astroUtilities;

                Logger.LogMessage("Telescope", "Completed initialization");
            } catch (Exception ex) {
                Logger.LogMessageCrLf("Telescope", $"Initialization exception: {ex}");
                throw;
            }
        }

        ~Telescope() {
            ReleaseManagedResources();
        }

        // PUBLIC COM INTERFACE ITelescopeV3 IMPLEMENTATION

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialogue form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog() {
            // consider only showing the setup dialogue if not connected
            // or call a different dialogue if connected
            if (IsConnected) {
                MessageBox.Show("Already connected, just press OK");
                return;
            }

            try {
                SetupVM.Show(this.servoCatOptions, serialUtilities);
            } catch (Exception ex) {
                Logger.LogMessageCrLf("OpenSetupDialog", $"Exception: {ex}");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ArrayList SupportedActions {
            get {
                Logger.LogMessage("SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters) {
            LogMessage("", "Action {0}, parameters {1} not implemented", actionName, actionParameters);
            throw new ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw) {
            CheckConnected("CommandBlind");
            // TODO The optional CommandBlind method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBlind must send the supplied command to the mount and return immediately without waiting for a response

            throw new MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw) {
            CheckConnected("CommandBool");
            // TODO The optional CommandBool method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBool must send the supplied command to the mount, wait for a response and parse this to return a True or False value

            // string retString = CommandString(command, raw); // Send the command and wait for the response
            // bool retBool = XXXXXXXXXXXXX; // Parse the returned string and create a boolean True / False value
            // return retBool; // Return the boolean value to the client

            throw new MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw) {
            CheckConnected("CommandString");
            // TODO The optional CommandString method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandString must send the supplied command to the mount and wait for a response before returning this to the client

            throw new MethodNotImplementedException("CommandString");
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            try {
                ReleaseManagedResources();
                GC.SuppressFinalize(this);
            } finally {
                disposed = true;
            }
        }

        public bool Connected {
            get {
                LogMessage("Connected", "Get {0}", IsConnected);
                return IsConnected;
            }
            set {
                Logger.LogMessage("Connected", "Set {0}", value);
                if (value == IsConnected) {
                    return;
                }

                if (value) {
                    try {
                        var channel = driverConnectionManager.Connect(driverClientId, TaskExtensions.TimeoutCancellationToken(sharedState.DeviceConnectionTimeout)).Result;
                        servoCatDevice = servoCatDeviceFactory.Create(channel);
                    } catch (Exception e) {
                        LogException("Connected Set", "Failed to connect", e);
                        try {
                            driverConnectionManager.Disconnect(driverClientId, TaskExtensions.TimeoutCancellationToken(sharedState.DeviceConnectionTimeout)).RunSynchronously();
                        } catch (Exception e2) {
                            LogException("Connected Set", "Failed to disconnect after failed connection", e2);
                        }
                        throw;
                    }
                    connectedState = true;
                } else {
                    try {
                        driverConnectionManager.Disconnect(driverClientId, TaskExtensions.TimeoutCancellationToken(sharedState.DeviceConnectionTimeout)).RunSynchronously();
                    } catch (Exception e) {
                        LogException("Connected Set", "Failed to disconnect", e);
                    }
                    connectedState = false;
                }
            }
        }

        public string Description {
            get {
                var driverDescription = sharedState.TelescopeDriverDescription;
                Logger.LogMessage("Description Get", driverDescription);
                return driverDescription;
            }
        }

        public string DriverInfo {
            get {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                // TODO customise this driver description
                string driverInfo = "Information about the driver itself. Version: " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                Logger.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion {
            get {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                Logger.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        public short InterfaceVersion {
            get {
                LogMessage("InterfaceVersion Get", "3");
                return Convert.ToInt16("3");
            }
        }

        public string Name {
            get {
                string name = "ServoCAT ASCOM Driver, by ghilios";
                Logger.LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region ITelescope Implementation

        public void AbortSlew() {
            Logger.LogMessage("AbortSlew", "Not implemented");
            throw new MethodNotImplementedException("AbortSlew");
        }

        public AlignmentModes AlignmentMode {
            get {
                Logger.LogMessage("AlignmentMode Get", "Not implemented");
                throw new PropertyNotImplementedException("AlignmentMode", false);
            }
        }

        public double Altitude {
            get {
                Logger.LogMessage("Altitude", "Not implemented");
                throw new PropertyNotImplementedException("Altitude", false);
            }
        }

        public double ApertureArea {
            get {
                Logger.LogMessage("ApertureArea Get", "Not implemented");
                throw new PropertyNotImplementedException("ApertureArea", false);
            }
        }

        public double ApertureDiameter {
            get {
                Logger.LogMessage("ApertureDiameter Get", "Not implemented");
                throw new PropertyNotImplementedException("ApertureDiameter", false);
            }
        }

        public bool AtHome {
            get {
                Logger.LogMessage("AtHome", "Get - " + false.ToString());
                return false;
            }
        }

        public bool AtPark {
            get {
                Logger.LogMessage("AtPark", "Get - " + false.ToString());
                return false;
            }
        }

        public IAxisRates AxisRates(TelescopeAxes Axis) {
            Logger.LogMessage("AxisRates", "Get - " + Axis.ToString());
            return new AxisRates(Axis);
        }

        public double Azimuth {
            get {
                Logger.LogMessage("Azimuth Get", "Not implemented");
                throw new PropertyNotImplementedException("Azimuth", false);
            }
        }

        public bool CanFindHome {
            get {
                Logger.LogMessage("CanFindHome", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanMoveAxis(TelescopeAxes Axis) {
            Logger.LogMessage("CanMoveAxis", "Get - " + Axis.ToString());
            switch (Axis) {
                case TelescopeAxes.axisPrimary: return false;
                case TelescopeAxes.axisSecondary: return false;
                case TelescopeAxes.axisTertiary: return false;
                default: throw new InvalidValueException("CanMoveAxis", Axis.ToString(), "0 to 2");
            }
        }

        public bool CanPark {
            get {
                Logger.LogMessage("CanPark", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanPulseGuide {
            get {
                Logger.LogMessage("CanPulseGuide", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetDeclinationRate {
            get {
                Logger.LogMessage("CanSetDeclinationRate", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetGuideRates {
            get {
                Logger.LogMessage("CanSetGuideRates", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetPark {
            get {
                Logger.LogMessage("CanSetPark", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetPierSide {
            get {
                Logger.LogMessage("CanSetPierSide", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetRightAscensionRate {
            get {
                Logger.LogMessage("CanSetRightAscensionRate", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetTracking {
            get {
                Logger.LogMessage("CanSetTracking", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSlew {
            get {
                Logger.LogMessage("CanSlew", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSlewAltAz {
            get {
                Logger.LogMessage("CanSlewAltAz", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSlewAltAzAsync {
            get {
                Logger.LogMessage("CanSlewAltAzAsync", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSlewAsync {
            get {
                Logger.LogMessage("CanSlewAsync", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSync {
            get {
                Logger.LogMessage("CanSync", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSyncAltAz {
            get {
                Logger.LogMessage("CanSyncAltAz", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanUnpark {
            get {
                Logger.LogMessage("CanUnpark", "Get - " + false.ToString());
                return false;
            }
        }

        public double Declination {
            get {
                double declination = 0.0;
                Logger.LogMessage("Declination", "Get - " + ascomUtilities.DegreesToDMS(declination, ":", ":"));
                return declination;
            }
        }

        public double DeclinationRate {
            get {
                double declination = 0.0;
                Logger.LogMessage("DeclinationRate", "Get - " + declination.ToString());
                return declination;
            }
            set {
                Logger.LogMessage("DeclinationRate Set", "Not implemented");
                throw new PropertyNotImplementedException("DeclinationRate", true);
            }
        }

        public PierSide DestinationSideOfPier(double RightAscension, double Declination) {
            Logger.LogMessage("DestinationSideOfPier Get", "Not implemented");
            throw new PropertyNotImplementedException("DestinationSideOfPier", false);
        }

        public bool DoesRefraction {
            get {
                Logger.LogMessage("DoesRefraction Get", "Not implemented");
                throw new PropertyNotImplementedException("DoesRefraction", false);
            }
            set {
                Logger.LogMessage("DoesRefraction Set", "Not implemented");
                throw new PropertyNotImplementedException("DoesRefraction", true);
            }
        }

        public EquatorialCoordinateType EquatorialSystem {
            get {
                EquatorialCoordinateType equatorialSystem = EquatorialCoordinateType.equTopocentric;
                Logger.LogMessage("DeclinationRate", "Get - " + equatorialSystem.ToString());
                return equatorialSystem;
            }
        }

        public void FindHome() {
            Logger.LogMessage("FindHome", "Not implemented");
            throw new MethodNotImplementedException("FindHome");
        }

        public double FocalLength {
            get {
                Logger.LogMessage("FocalLength Get", "Not implemented");
                throw new PropertyNotImplementedException("FocalLength", false);
            }
        }

        public double GuideRateDeclination {
            get {
                Logger.LogMessage("GuideRateDeclination Get", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateDeclination", false);
            }
            set {
                Logger.LogMessage("GuideRateDeclination Set", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateDeclination", true);
            }
        }

        public double GuideRateRightAscension {
            get {
                Logger.LogMessage("GuideRateRightAscension Get", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateRightAscension", false);
            }
            set {
                Logger.LogMessage("GuideRateRightAscension Set", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateRightAscension", true);
            }
        }

        public bool IsPulseGuiding {
            get {
                Logger.LogMessage("IsPulseGuiding Get", "Not implemented");
                throw new PropertyNotImplementedException("IsPulseGuiding", false);
            }
        }

        public void MoveAxis(TelescopeAxes Axis, double Rate) {
            Logger.LogMessage("MoveAxis", "Not implemented");
            throw new MethodNotImplementedException("MoveAxis");
        }

        public void Park() {
            Logger.LogMessage("Park", "Not implemented");
            throw new MethodNotImplementedException("Park");
        }

        public void PulseGuide(GuideDirections Direction, int Duration) {
            Logger.LogMessage("PulseGuide", "Not implemented");
            throw new MethodNotImplementedException("PulseGuide");
        }

        public double RightAscension {
            get {
                double rightAscension = 0.0;
                Logger.LogMessage("RightAscension", "Get - " + ascomUtilities.HoursToHMS(rightAscension));
                return rightAscension;
            }
        }

        public double RightAscensionRate {
            get {
                double rightAscensionRate = 0.0;
                Logger.LogMessage("RightAscensionRate", "Get - " + rightAscensionRate.ToString());
                return rightAscensionRate;
            }
            set {
                Logger.LogMessage("RightAscensionRate Set", "Not implemented");
                throw new PropertyNotImplementedException("RightAscensionRate", true);
            }
        }

        public void SetPark() {
            Logger.LogMessage("SetPark", "Not implemented");
            throw new MethodNotImplementedException("SetPark");
        }

        public PierSide SideOfPier {
            get {
                Logger.LogMessage("SideOfPier Get", "Not implemented");
                throw new PropertyNotImplementedException("SideOfPier", false);
            }
            set {
                Logger.LogMessage("SideOfPier Set", "Not implemented");
                throw new PropertyNotImplementedException("SideOfPier", true);
            }
        }

        public double SiderealTime {
            get {
                double siderealTime = 0.0; // Sidereal time return value

                // Use NOVAS 3.1 to calculate the sidereal time
                using (var novas = new NOVAS31()) {
                    double julianDate = ascomUtilities.DateUTCToJulian(DateTime.UtcNow);
                    novas.SiderealTime(julianDate, 0, novas.DeltaT(julianDate), GstType.GreenwichApparentSiderealTime, Method.EquinoxBased, Accuracy.Full, ref siderealTime);
                }

                // Adjust the calculated sidereal time for longitude using the value returned by the SiteLongitude property, allowing for the possibility that this property has not yet been implemented
                try {
                    siderealTime += SiteLongitude / 360.0 * 24.0;
                } catch (PropertyNotImplementedException) // SiteLongitude hasn't been implemented
                  {
                    // No action, just return the calculated sidereal time unadjusted for longitude
                } catch (Exception) // Some other exception occurred so return it to the client
                  {
                    throw;
                }

                // Reduce sidereal time to the range 0 to 24 hours
                siderealTime = astroUtilities.ConditionRA(siderealTime);

                Logger.LogMessage("SiderealTime", "Get - " + siderealTime.ToString());
                return siderealTime;
            }
        }

        public double SiteElevation {
            get {
                Logger.LogMessage("SiteElevation Get", "Not implemented");
                throw new PropertyNotImplementedException("SiteElevation", false);
            }
            set {
                Logger.LogMessage("SiteElevation Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteElevation", true);
            }
        }

        public double SiteLatitude {
            get {
                Logger.LogMessage("SiteLatitude Get", "Not implemented");
                throw new PropertyNotImplementedException("SiteLatitude", false);
            }
            set {
                Logger.LogMessage("SiteLatitude Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteLatitude", true);
            }
        }

        public double SiteLongitude {
            get {
                Logger.LogMessage("SiteLongitude Get", "Returning 0.0 to ensure that SiderealTime method is functional out of the box.");
                return 0.0;
            }
            set {
                Logger.LogMessage("SiteLongitude Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteLongitude", true);
            }
        }

        public short SlewSetLoggereTime {
            get {
                Logger.LogMessage("SlewSetLoggereTime Get", "Not implemented");
                throw new PropertyNotImplementedException("SlewSetLoggereTime", false);
            }
            set {
                Logger.LogMessage("SlewSetLoggereTime Set", "Not implemented");
                throw new PropertyNotImplementedException("SlewSetLoggereTime", true);
            }
        }

        public void SlewToAltAz(double Azimuth, double Altitude) {
            Logger.LogMessage("SlewToAltAz", "Not implemented");
            throw new MethodNotImplementedException("SlewToAltAz");
        }

        public void SlewToAltAzAsync(double Azimuth, double Altitude) {
            Logger.LogMessage("SlewToAltAzAsync", "Not implemented");
            throw new MethodNotImplementedException("SlewToAltAzAsync");
        }

        public void SlewToCoordinates(double RightAscension, double Declination) {
            Logger.LogMessage("SlewToCoordinates", "Not implemented");
            throw new MethodNotImplementedException("SlewToCoordinates");
        }

        public void SlewToCoordinatesAsync(double RightAscension, double Declination) {
            Logger.LogMessage("SlewToCoordinatesAsync", "Not implemented");
            throw new MethodNotImplementedException("SlewToCoordinatesAsync");
        }

        public void SlewToTarget() {
            Logger.LogMessage("SlewToTarget", "Not implemented");
            throw new MethodNotImplementedException("SlewToTarget");
        }

        public void SlewToTargetAsync() {
            Logger.LogMessage("SlewToTargetAsync", "Not implemented");
            throw new MethodNotImplementedException("SlewToTargetAsync");
        }

        public bool Slewing {
            get {
                Logger.LogMessage("Slewing Get", "Not implemented");
                throw new PropertyNotImplementedException("Slewing", false);
            }
        }

        public void SyncToAltAz(double Azimuth, double Altitude) {
            Logger.LogMessage("SyncToAltAz", "Not implemented");
            throw new MethodNotImplementedException("SyncToAltAz");
        }

        public void SyncToCoordinates(double RightAscension, double Declination) {
            Logger.LogMessage("SyncToCoordinates", "Not implemented");
            throw new MethodNotImplementedException("SyncToCoordinates");
        }

        public void SyncToTarget() {
            Logger.LogMessage("SyncToTarget", "Not implemented");
            throw new MethodNotImplementedException("SyncToTarget");
        }

        public double TargetDeclination {
            get {
                Logger.LogMessage("TargetDeclination Get", "Not implemented");
                throw new PropertyNotImplementedException("TargetDeclination", false);
            }
            set {
                Logger.LogMessage("TargetDeclination Set", "Not implemented");
                throw new PropertyNotImplementedException("TargetDeclination", true);
            }
        }

        public double TargetRightAscension {
            get {
                Logger.LogMessage("TargetRightAscension Get", "Not implemented");
                throw new PropertyNotImplementedException("TargetRightAscension", false);
            }
            set {
                Logger.LogMessage("TargetRightAscension Set", "Not implemented");
                throw new PropertyNotImplementedException("TargetRightAscension", true);
            }
        }

        public bool Tracking {
            get {
                bool tracking = true;
                Logger.LogMessage("Tracking", "Get - " + tracking.ToString());
                return tracking;
            }
            set {
                Logger.LogMessage("Tracking Set", "Not implemented");
                throw new PropertyNotImplementedException("Tracking", true);
            }
        }

        public DriveRates TrackingRate {
            get {
                const DriveRates DEFAULT_DRIVERATE = DriveRates.driveSidereal;
                Logger.LogMessage("TrackingRate Get", $"{DEFAULT_DRIVERATE}");
                return DEFAULT_DRIVERATE;
            }
            set {
                Logger.LogMessage("TrackingRate Set", "Not implemented");
                throw new PropertyNotImplementedException("TrackingRate", true);
            }
        }

        public ITrackingRates TrackingRates {
            get {
                ITrackingRates trackingRates = new TrackingRates();
                Logger.LogMessage("TrackingRates", "Get - ");
                foreach (DriveRates driveRate in trackingRates) {
                    Logger.LogMessage("TrackingRates", "Get - " + driveRate.ToString());
                }
                return trackingRates;
            }
        }

        public DateTime UTCDate {
            get {
                DateTime utcDate = DateTime.UtcNow;
                Logger.LogMessage("TrackingRates", "Get - " + String.Format("MM/dd/yy HH:mm:ss", utcDate));
                return utcDate;
            }
            set {
                Logger.LogMessage("UTCDate Set", "Not implemented");
                throw new PropertyNotImplementedException("UTCDate", true);
            }
        }

        public void Unpark() {
            Logger.LogMessage("Unpark", "Not implemented");
            throw new MethodNotImplementedException("Unpark");
        }

        #endregion

        #region Private properties and methods

        // here are some useful properties and methods that can be used as required
        // to help with driver development

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered.
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister) {
            using (var P = new Profile()) {
                P.DeviceType = "Telescope";
                var sharedState = CompositionRoot.Kernel.Get<ISharedState>();
                if (bRegister) {
                    P.Register(sharedState.TelescopeDriverId, sharedState.TelescopeDriverDescription);
                } else {
                    P.Unregister(sharedState.TelescopeDriverId);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correcLoggery, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t) {
            _ = t.Name; // Just included to remove a compiler informational message that the mandatory type parameter "t" is not used within the member
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correcLoggery, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t) {
            _ = t.Name; // Just included to remove a compiler informational message that the mandatory type parameter "t" is not used within the member
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected {
            get {
                // TODO check that the driver hardware connection exists and is connected to the hardware
                return connectedState;
            }
        }

        public short SlewSettleTime {
            get => throw new ASCOM.PropertyNotImplementedException();
            set => throw new ASCOM.PropertyNotImplementedException();
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message) {
            if (!IsConnected) {
                throw new NotConnectedException(message);
            }
        }

        internal void LogMessage(string identifier, string message, params object[] args) {
            var msg = string.Format(message, args);
            Logger.LogMessage(identifier, msg);
        }

        internal void LogException(string identifier, string message, Exception e) {
            var msg = $"Exception={e.Message}, Message={message}, Details={e}";
            Logger.LogMessage(identifier, msg);
        }

        #endregion

        private void ReleaseManagedResources() {
            if (disposed) {
                return;
            }

            driverConnectionManager.Disconnect(driverClientId, CancellationToken.None).RunSynchronously();
            driverConnectionManager.UnregisterClient(driverClientId).RunSynchronously();
        }
    }
}