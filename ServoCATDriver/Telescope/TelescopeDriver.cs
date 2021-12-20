//tabs=4
// TODO fill in this information for your driver, then remove this line!
//
// ASCOM Telescope driver for Joko.ServoCAT
//
// Description:	 <To be completed by driver developer>
//
// Implements:	ASCOM Telescope interface version: <To be completed by driver developer>
// Author:		(XXX) Your N. Here <your@email.here>
//

using ASCOM;
using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Astrometry.NOVAS;
using ASCOM.DeviceInterface;
using ASCOM.LocalServer;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ASCOM.Joko.ServoCAT.Telescope {
    //
    // Your driver's DeviceID is ASCOM.Joko.ServoCAT.Telescope
    //
    // The Guid attribute sets the CLSID for ASCOM.Joko.ServoCAT.Telescope
    // The ClassInterface/None attribute prevents an empty interface called
    // _Joko.ServoCAT from being created and used as the [default] interface
    //
    // TODO Replace the not implemented exceptions with code to implement the function or
    // throw the appropriate ASCOM exception.
    //

    /// <summary>
    /// ASCOM Telescope Driver for Joko.ServoCAT.
    /// </summary>
    [ComVisible(true)]
    [Guid("02891d62-2316-476e-93ad-bb4bea5ac154")]
    [ProgId("ASCOM.Joko.ServoCAT.Telescope")]
    [ServedClassName("ASCOM Telescope Driver for Joko.ServoCAT")] // Driver description that appears in the Chooser, customise as required
    [ClassInterface(ClassInterfaceType.None)]
    public class Telescope : ReferenceCountedObjectBase, ITelescopeV3 {
        // Constants used for Profile persistence
        internal const string comPortProfileName = "COM Port";
        internal const string comPortDefault = "COM1";
        internal const string traceStateProfileName = "Trace Level";
        internal const string traceStateDefault = "true";

        internal static string driverID; // ASCOM DeviceID (COM ProgID) for this driver, the value is retrieved from the ServedClassName attribute in the class initialiser.
        internal static string driverDescription; // The value is retrieved from the ServedClassName attribute in the class initialiser.
        internal static string comPort; // Variable to hold the COM port if required
        internal static bool connectedState; // variable to hold the connected state
        internal static Util utilities; // Private variable to hold an ASCOM Utilities object
        internal static AstroUtils astroUtilities; // Variable to hold an ASCOM AstroUtilities object to provide the Range method
        internal static TraceLogger tl; // Variable to hold the trace logger object (creates a diagnostic log file with information that you specify)

        /// <summary>
        /// Initializes a new instance of the <see cref="Joko.ServoCAT"/> class. Must be public to successfully register for COM.
        /// </summary>
        public Telescope() {
            try {
                // Pull the ProgID from the ProgID class attribute.
                Attribute attr = Attribute.GetCustomAttribute(this.GetType(), typeof(ProgIdAttribute));
                driverID = ((ProgIdAttribute)attr).Value ?? "PROGID NOT SET!";  // Get the driver ProgIDfrom the ProgID attribute.

                // Pull the display name from the ServedClassName class attribute.
                attr = Attribute.GetCustomAttribute(this.GetType(), typeof(ServedClassNameAttribute));
                driverDescription = ((ServedClassNameAttribute)attr).DisplayName ?? "DISPLAY NAME NOT SET!";  // Get the driver description that displays in the ASCOM Chooser from the ServedClassName attribute.

                tl = new TraceLogger("", "ASCOM.Joko.ServoCAT.Telescope");
                ReadProfile(); // Read device configuration from the ASCOM Profile store, including the trace state

                tl.LogMessage("Telescope", "Starting initialisation");
                tl.LogMessage("Telescope", $"ProgID: {driverID}, Description: {driverDescription}");

                connectedState = false; // Initialise connected to false
                utilities = new Util(); //Initialise util object
                astroUtilities = new AstroUtils(); // Initialise astro-utilities object

                //TODO: Implement your additional construction here

                tl.LogMessage("Telescope", "Completed initialisation");
            } catch (Exception ex) {
                tl.LogMessageCrLf("Telescope", $"Initialisation exception: {ex}");
                MessageBox.Show($"{ex.Message}", "Exception creating ASCOM.Joko.ServoCAT.Telescope", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

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
            if (IsConnected)
                MessageBox.Show("Already connected, just press OK");

            using (SetupDialogForm F = new SetupDialogForm(tl)) {
                var result = F.ShowDialog();
                if (result == DialogResult.OK) {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        public ArrayList SupportedActions {
            get {
                tl.LogMessage("SupportedActions Get", "Returning empty arraylist");
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
            // Clean up the trace logger and util objects
            tl.Enabled = false;
            tl.Dispose();
            tl = null;
            utilities.Dispose();
            utilities = null;
            astroUtilities.Dispose();
            astroUtilities = null;
        }

        public bool Connected {
            get {
                LogMessage("Connected", "Get {0}", IsConnected);
                return IsConnected;
            }
            set {
                tl.LogMessage("Connected", "Set {0}", value);
                if (value == IsConnected)
                    return;

                if (value) {
                    connectedState = true;
                    LogMessage("Connected Set", "Connecting to port {0}", comPort);
                    // TODO connect to the device
                } else {
                    connectedState = false;
                    LogMessage("Connected Set", "Disconnecting from port {0}", comPort);
                    // TODO disconnect from the device
                }
            }
        }

        public string Description {
            // TODO customise this device description
            get {
                tl.LogMessage("Description Get", driverDescription);
                return driverDescription;
            }
        }

        public string DriverInfo {
            get {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                // TODO customise this driver description
                string driverInfo = "Information about the driver itself. Version: " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion {
            get {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        public short InterfaceVersion {
            // set by the driver wizard
            get {
                LogMessage("InterfaceVersion Get", "3");
                return Convert.ToInt16("3");
            }
        }

        public string Name {
            get {
                string name = "Short driver name - please customise";
                tl.LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region ITelescope Implementation
        public void AbortSlew() {
            tl.LogMessage("AbortSlew", "Not implemented");
            throw new MethodNotImplementedException("AbortSlew");
        }

        public AlignmentModes AlignmentMode {
            get {
                tl.LogMessage("AlignmentMode Get", "Not implemented");
                throw new PropertyNotImplementedException("AlignmentMode", false);
            }
        }

        public double Altitude {
            get {
                tl.LogMessage("Altitude", "Not implemented");
                throw new PropertyNotImplementedException("Altitude", false);
            }
        }

        public double ApertureArea {
            get {
                tl.LogMessage("ApertureArea Get", "Not implemented");
                throw new PropertyNotImplementedException("ApertureArea", false);
            }
        }

        public double ApertureDiameter {
            get {
                tl.LogMessage("ApertureDiameter Get", "Not implemented");
                throw new PropertyNotImplementedException("ApertureDiameter", false);
            }
        }

        public bool AtHome {
            get {
                tl.LogMessage("AtHome", "Get - " + false.ToString());
                return false;
            }
        }

        public bool AtPark {
            get {
                tl.LogMessage("AtPark", "Get - " + false.ToString());
                return false;
            }
        }

        public IAxisRates AxisRates(TelescopeAxes Axis) {
            tl.LogMessage("AxisRates", "Get - " + Axis.ToString());
            return new AxisRates(Axis);
        }

        public double Azimuth {
            get {
                tl.LogMessage("Azimuth Get", "Not implemented");
                throw new PropertyNotImplementedException("Azimuth", false);
            }
        }

        public bool CanFindHome {
            get {
                tl.LogMessage("CanFindHome", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanMoveAxis(TelescopeAxes Axis) {
            tl.LogMessage("CanMoveAxis", "Get - " + Axis.ToString());
            switch (Axis) {
                case TelescopeAxes.axisPrimary: return false;
                case TelescopeAxes.axisSecondary: return false;
                case TelescopeAxes.axisTertiary: return false;
                default: throw new InvalidValueException("CanMoveAxis", Axis.ToString(), "0 to 2");
            }
        }

        public bool CanPark {
            get {
                tl.LogMessage("CanPark", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanPulseGuide {
            get {
                tl.LogMessage("CanPulseGuide", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetDeclinationRate {
            get {
                tl.LogMessage("CanSetDeclinationRate", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetGuideRates {
            get {
                tl.LogMessage("CanSetGuideRates", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetPark {
            get {
                tl.LogMessage("CanSetPark", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetPierSide {
            get {
                tl.LogMessage("CanSetPierSide", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetRightAscensionRate {
            get {
                tl.LogMessage("CanSetRightAscensionRate", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSetTracking {
            get {
                tl.LogMessage("CanSetTracking", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSlew {
            get {
                tl.LogMessage("CanSlew", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSlewAltAz {
            get {
                tl.LogMessage("CanSlewAltAz", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSlewAltAzAsync {
            get {
                tl.LogMessage("CanSlewAltAzAsync", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSlewAsync {
            get {
                tl.LogMessage("CanSlewAsync", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSync {
            get {
                tl.LogMessage("CanSync", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSyncAltAz {
            get {
                tl.LogMessage("CanSyncAltAz", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanUnpark {
            get {
                tl.LogMessage("CanUnpark", "Get - " + false.ToString());
                return false;
            }
        }

        public double Declination {
            get {
                double declination = 0.0;
                tl.LogMessage("Declination", "Get - " + utilities.DegreesToDMS(declination, ":", ":"));
                return declination;
            }
        }

        public double DeclinationRate {
            get {
                double declination = 0.0;
                tl.LogMessage("DeclinationRate", "Get - " + declination.ToString());
                return declination;
            }
            set {
                tl.LogMessage("DeclinationRate Set", "Not implemented");
                throw new PropertyNotImplementedException("DeclinationRate", true);
            }
        }

        public PierSide DestinationSideOfPier(double RightAscension, double Declination) {
            tl.LogMessage("DestinationSideOfPier Get", "Not implemented");
            throw new PropertyNotImplementedException("DestinationSideOfPier", false);
        }

        public bool DoesRefraction {
            get {
                tl.LogMessage("DoesRefraction Get", "Not implemented");
                throw new PropertyNotImplementedException("DoesRefraction", false);
            }
            set {
                tl.LogMessage("DoesRefraction Set", "Not implemented");
                throw new PropertyNotImplementedException("DoesRefraction", true);
            }
        }

        public EquatorialCoordinateType EquatorialSystem {
            get {
                EquatorialCoordinateType equatorialSystem = EquatorialCoordinateType.equTopocentric;
                tl.LogMessage("DeclinationRate", "Get - " + equatorialSystem.ToString());
                return equatorialSystem;
            }
        }

        public void FindHome() {
            tl.LogMessage("FindHome", "Not implemented");
            throw new MethodNotImplementedException("FindHome");
        }

        public double FocalLength {
            get {
                tl.LogMessage("FocalLength Get", "Not implemented");
                throw new PropertyNotImplementedException("FocalLength", false);
            }
        }

        public double GuideRateDeclination {
            get {
                tl.LogMessage("GuideRateDeclination Get", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateDeclination", false);
            }
            set {
                tl.LogMessage("GuideRateDeclination Set", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateDeclination", true);
            }
        }

        public double GuideRateRightAscension {
            get {
                tl.LogMessage("GuideRateRightAscension Get", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateRightAscension", false);
            }
            set {
                tl.LogMessage("GuideRateRightAscension Set", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateRightAscension", true);
            }
        }

        public bool IsPulseGuiding {
            get {
                tl.LogMessage("IsPulseGuiding Get", "Not implemented");
                throw new PropertyNotImplementedException("IsPulseGuiding", false);
            }
        }

        public void MoveAxis(TelescopeAxes Axis, double Rate) {
            tl.LogMessage("MoveAxis", "Not implemented");
            throw new MethodNotImplementedException("MoveAxis");
        }

        public void Park() {
            tl.LogMessage("Park", "Not implemented");
            throw new MethodNotImplementedException("Park");
        }

        public void PulseGuide(GuideDirections Direction, int Duration) {
            tl.LogMessage("PulseGuide", "Not implemented");
            throw new MethodNotImplementedException("PulseGuide");
        }

        public double RightAscension {
            get {
                double rightAscension = 0.0;
                tl.LogMessage("RightAscension", "Get - " + utilities.HoursToHMS(rightAscension));
                return rightAscension;
            }
        }

        public double RightAscensionRate {
            get {
                double rightAscensionRate = 0.0;
                tl.LogMessage("RightAscensionRate", "Get - " + rightAscensionRate.ToString());
                return rightAscensionRate;
            }
            set {
                tl.LogMessage("RightAscensionRate Set", "Not implemented");
                throw new PropertyNotImplementedException("RightAscensionRate", true);
            }
        }

        public void SetPark() {
            tl.LogMessage("SetPark", "Not implemented");
            throw new MethodNotImplementedException("SetPark");
        }

        public PierSide SideOfPier {
            get {
                tl.LogMessage("SideOfPier Get", "Not implemented");
                throw new PropertyNotImplementedException("SideOfPier", false);
            }
            set {
                tl.LogMessage("SideOfPier Set", "Not implemented");
                throw new PropertyNotImplementedException("SideOfPier", true);
            }
        }

        public double SiderealTime {
            get {
                double siderealTime = 0.0; // Sidereal time return value

                // Use NOVAS 3.1 to calculate the sidereal time
                using (var novas = new NOVAS31()) {
                    double julianDate = utilities.DateUTCToJulian(DateTime.UtcNow);
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

                tl.LogMessage("SiderealTime", "Get - " + siderealTime.ToString());
                return siderealTime;
            }
        }

        public double SiteElevation {
            get {
                tl.LogMessage("SiteElevation Get", "Not implemented");
                throw new PropertyNotImplementedException("SiteElevation", false);
            }
            set {
                tl.LogMessage("SiteElevation Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteElevation", true);
            }
        }

        public double SiteLatitude {
            get {
                tl.LogMessage("SiteLatitude Get", "Not implemented");
                throw new PropertyNotImplementedException("SiteLatitude", false);
            }
            set {
                tl.LogMessage("SiteLatitude Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteLatitude", true);
            }
        }

        public double SiteLongitude {
            get {
                tl.LogMessage("SiteLongitude Get", "Returning 0.0 to ensure that SiderealTime method is functional out of the box.");
                return 0.0;
            }
            set {
                tl.LogMessage("SiteLongitude Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteLongitude", true);
            }
        }

        public short SlewSettleTime {
            get {
                tl.LogMessage("SlewSettleTime Get", "Not implemented");
                throw new PropertyNotImplementedException("SlewSettleTime", false);
            }
            set {
                tl.LogMessage("SlewSettleTime Set", "Not implemented");
                throw new PropertyNotImplementedException("SlewSettleTime", true);
            }
        }

        public void SlewToAltAz(double Azimuth, double Altitude) {
            tl.LogMessage("SlewToAltAz", "Not implemented");
            throw new MethodNotImplementedException("SlewToAltAz");
        }

        public void SlewToAltAzAsync(double Azimuth, double Altitude) {
            tl.LogMessage("SlewToAltAzAsync", "Not implemented");
            throw new MethodNotImplementedException("SlewToAltAzAsync");
        }

        public void SlewToCoordinates(double RightAscension, double Declination) {
            tl.LogMessage("SlewToCoordinates", "Not implemented");
            throw new MethodNotImplementedException("SlewToCoordinates");
        }

        public void SlewToCoordinatesAsync(double RightAscension, double Declination) {
            tl.LogMessage("SlewToCoordinatesAsync", "Not implemented");
            throw new MethodNotImplementedException("SlewToCoordinatesAsync");
        }

        public void SlewToTarget() {
            tl.LogMessage("SlewToTarget", "Not implemented");
            throw new MethodNotImplementedException("SlewToTarget");
        }

        public void SlewToTargetAsync() {
            tl.LogMessage("SlewToTargetAsync", "Not implemented");
            throw new MethodNotImplementedException("SlewToTargetAsync");
        }

        public bool Slewing {
            get {
                tl.LogMessage("Slewing Get", "Not implemented");
                throw new PropertyNotImplementedException("Slewing", false);
            }
        }

        public void SyncToAltAz(double Azimuth, double Altitude) {
            tl.LogMessage("SyncToAltAz", "Not implemented");
            throw new MethodNotImplementedException("SyncToAltAz");
        }

        public void SyncToCoordinates(double RightAscension, double Declination) {
            tl.LogMessage("SyncToCoordinates", "Not implemented");
            throw new MethodNotImplementedException("SyncToCoordinates");
        }

        public void SyncToTarget() {
            tl.LogMessage("SyncToTarget", "Not implemented");
            throw new MethodNotImplementedException("SyncToTarget");
        }

        public double TargetDeclination {
            get {
                tl.LogMessage("TargetDeclination Get", "Not implemented");
                throw new PropertyNotImplementedException("TargetDeclination", false);
            }
            set {
                tl.LogMessage("TargetDeclination Set", "Not implemented");
                throw new PropertyNotImplementedException("TargetDeclination", true);
            }
        }

        public double TargetRightAscension {
            get {
                tl.LogMessage("TargetRightAscension Get", "Not implemented");
                throw new PropertyNotImplementedException("TargetRightAscension", false);
            }
            set {
                tl.LogMessage("TargetRightAscension Set", "Not implemented");
                throw new PropertyNotImplementedException("TargetRightAscension", true);
            }
        }

        public bool Tracking {
            get {
                bool tracking = true;
                tl.LogMessage("Tracking", "Get - " + tracking.ToString());
                return tracking;
            }
            set {
                tl.LogMessage("Tracking Set", "Not implemented");
                throw new PropertyNotImplementedException("Tracking", true);
            }
        }

        public DriveRates TrackingRate {
            get {
                const DriveRates DEFAULT_DRIVERATE = DriveRates.driveSidereal;
                tl.LogMessage("TrackingRate Get", $"{DEFAULT_DRIVERATE}");
                return DEFAULT_DRIVERATE;
            }
            set {
                tl.LogMessage("TrackingRate Set", "Not implemented");
                throw new PropertyNotImplementedException("TrackingRate", true);
            }
        }

        public ITrackingRates TrackingRates {
            get {
                ITrackingRates trackingRates = new TrackingRates();
                tl.LogMessage("TrackingRates", "Get - ");
                foreach (DriveRates driveRate in trackingRates) {
                    tl.LogMessage("TrackingRates", "Get - " + driveRate.ToString());
                }
                return trackingRates;
            }
        }

        public DateTime UTCDate {
            get {
                DateTime utcDate = DateTime.UtcNow;
                tl.LogMessage("TrackingRates", "Get - " + String.Format("MM/dd/yy HH:mm:ss", utcDate));
                return utcDate;
            }
            set {
                tl.LogMessage("UTCDate Set", "Not implemented");
                throw new PropertyNotImplementedException("UTCDate", true);
            }
        }

        public void Unpark() {
            tl.LogMessage("Unpark", "Not implemented");
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
                if (bRegister) {
                    P.Register(driverID, driverDescription);
                } else {
                    P.Unregister(driverID);
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
        /// For this to work correctly, the option <c>Register for COM Interop</c>
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
        /// For this to work correctly, the option <c>Register for COM Interop</c>
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

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message) {
            if (!IsConnected) {
                throw new NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile() {
            using (Profile driverProfile = new Profile()) {
                driverProfile.DeviceType = "Telescope";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, traceStateDefault));
                comPort = driverProfile.GetValue(driverID, comPortProfileName, string.Empty, comPortDefault);
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile() {
            using (Profile driverProfile = new Profile()) {
                driverProfile.DeviceType = "Telescope";
                driverProfile.WriteValue(driverID, traceStateProfileName, tl.Enabled.ToString());
                driverProfile.WriteValue(driverID, comPortProfileName, comPort.ToString());
            }
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal void LogMessage(string identifier, string message, params object[] args) {
            var msg = string.Format(message, args);
            tl.LogMessage(identifier, msg);
        }
        #endregion
    }
}
