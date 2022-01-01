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
using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.ghilios.ServoCAT.Service;
using ASCOM.ghilios.ServoCAT.Service.Utility;
using ASCOM.ghilios.ServoCAT.Utility;
using ASCOM.ghilios.ServoCAT.ViewModel;
using ASCOM.Utilities;
using Ninject;
using Nito.AsyncEx;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ASCOM.ghilios.ServoCAT.Telescope {

    [ComVisible(true)]
    [Guid("02891d62-2316-476e-93ad-bb4bea5ac154")]
    [ProgId("ASCOM.ghilios.ServoCAT.Telescope")]
    [ServedClassName("ServoCAT Driver")]
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
        private readonly IMicroCacheFactory microCacheFactory;
        private readonly AstrometryConverter astrometryConverter;
        private readonly IMicroCache<ServoCatStatus> positionCache;
        private readonly INOVAS31 novas;

        private IServoCatDevice servoCatDevice;
        private bool connectedState = false;
        private bool disposed = false;
        private Angle targetRightAscension = Angle.ByDegree(double.NaN);
        private Angle targetDeclination = Angle.ByDegree(double.NaN);
        private CancellationTokenSource disconnectTokenSource;
        private FirmwareVersion servoCatFirmwareVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="ghilios.ServoCAT"/> class. Must be public to successfully register for COM.
        /// </summary>
        public Telescope() : this(
            sharedState: CompositionRoot.Kernel.Get<ISharedState>(),
            options: CompositionRoot.Kernel.Get<IServoCatOptions>(),
            driverConnectionManager: CompositionRoot.Kernel.Get<IDriverConnectionManager>(),
            servoCatDeviceFactory: CompositionRoot.Kernel.Get<IServoCatDeviceFactory>(),
            logger: CompositionRoot.Kernel.Get<TraceLogger>("Telescope"),
            astroUtilities: CompositionRoot.Kernel.Get<IAstroUtils>(),
            ascomUtilities: CompositionRoot.Kernel.Get<Util>(),
            serialUtilities: CompositionRoot.Kernel.Get<ISerialUtilities>(),
            microCacheFactory: CompositionRoot.Kernel.Get<IMicroCacheFactory>(),
            astrometryConverter: CompositionRoot.Kernel.Get<AstrometryConverter>(),
            novas: CompositionRoot.Kernel.Get<INOVAS31>()) {
        }

        public Telescope(
            ISharedState sharedState,
            IServoCatOptions options,
            IDriverConnectionManager driverConnectionManager,
            IServoCatDeviceFactory servoCatDeviceFactory,
            TraceLogger logger,
            IAstroUtils astroUtilities,
            Util ascomUtilities,
            ISerialUtilities serialUtilities,
            IMicroCacheFactory microCacheFactory,
            AstrometryConverter astrometryConverter,
            INOVAS31 novas) {
            try {
                if (string.IsNullOrEmpty(sharedState.TelescopeDriverId)) {
                    throw new ASCOM.DriverException("ProgID is not set");
                }

                if (string.IsNullOrEmpty(sharedState.TelescopeDriverDescription)) {
                    throw new ASCOM.DriverException("DriverDescription is not set");
                }

                // TODO: Add switch for trace logging
                logger.Enabled = true;

                this.sharedState = sharedState;
                this.servoCatOptions = options;
                this.driverConnectionManager = driverConnectionManager;
                this.servoCatDeviceFactory = servoCatDeviceFactory;
                this.serialUtilities = serialUtilities;
                this.microCacheFactory = microCacheFactory;
                this.astrometryConverter = astrometryConverter;
                this.novas = novas;
                this.positionCache = this.microCacheFactory.Create<ServoCatStatus>();

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

        private T DeviceActionWithTimeout<T>(Func<CancellationToken, Task<T>> op) {
            var timeoutCts = new CancellationTokenSource(servoCatOptions.DeviceRequestTimeout);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, disconnectTokenSource.Token);
            var resultTask = op(linkedCts.Token);
            try {
                resultTask.Wait();
            } catch (AggregateException ex) when (ex.InnerException is EndOfStreamException eex) {
                LogMessage("DeviceActionWithTimeout", "Reached end of stream while reading from device. Disconnecting");
                _ = Task.Run(() => Connected = false);
                throw eex;
            } catch (AggregateException ex) {
                throw ex.InnerException;
            }
            if (timeoutCts.IsCancellationRequested) {
                throw new TimeoutException($"Operation timed out after {servoCatOptions.DeviceRequestTimeout}");
            } else if (disconnectTokenSource.IsCancellationRequested) {
                throw new DriverException("Driver disconnected");
            }

            return resultTask.Result;
        }

        // PUBLIC COM INTERFACE ITelescopeV3 IMPLEMENTATION

        #region Common properties and methods.

        public void SetupDialog() {
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
            throw new MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw) {
            CheckConnected("CommandBool");
            throw new MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw) {
            CheckConnected("CommandString");
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
                Logger.LogMessage("Connected", $"Set {value}");
                if (value == IsConnected) {
                    return;
                }

                if (value) {
                    try {
                        var channel = driverConnectionManager.Connect(driverClientId, SCTaskExtensions.TimeoutCancellationToken(sharedState.DeviceConnectionTimeout)).Result;
                        servoCatDevice = servoCatDeviceFactory.Create(channel);
                        servoCatDevice.Initialize(SCTaskExtensions.TimeoutCancellationToken(sharedState.DeviceConnectionTimeout)).Wait();
                    } catch (Exception e) {
                        LogException("Connected Set", "Failed to connect", e);
                        try {
                            driverConnectionManager.Disconnect(driverClientId, SCTaskExtensions.TimeoutCancellationToken(sharedState.DeviceConnectionTimeout)).Wait();
                        } catch (Exception e2) {
                            LogException("Connected Set", "Failed to disconnect after failed connection", e2);
                        }
                        throw;
                    }
                    disconnectTokenSource = new CancellationTokenSource();
                    servoCatFirmwareVersion = AsyncContext.Run(() => servoCatDevice.GetVersion(disconnectTokenSource.Token));
                    connectedState = true;
                } else {
                    try {
                        driverConnectionManager.Disconnect(driverClientId, SCTaskExtensions.TimeoutCancellationToken(sharedState.DeviceConnectionTimeout)).Wait();
                    } catch (Exception e) {
                        LogException("Connected Set", "Failed to disconnect", e);
                    }
                    disconnectTokenSource?.Cancel();
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
                string driverInfo;
                if (Connected) {
                    driverInfo = $"Firmware version {servoCatFirmwareVersion}";
                } else {
                    driverInfo = "ServoCAT not connected";
                }

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
                string name = "ServoCAT";
                Logger.LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region ITelescope Implementation

        private Epoch GetEpoch() {
            return servoCatOptions.UseJ2000 ? Epoch.J2000 : Epoch.JNOW;
        }

        private ServoCatStatus GetTelescopeStatus() {
            var epoch = GetEpoch();
            return this.positionCache.GetOrAdd(
                epoch.ToString(),
                () => {
                    var status = DeviceActionWithTimeout(servoCatDevice.GetExtendedStatus);
                    var celestialCoordinates = astrometryConverter.TransformEpoch(status.Coordinates, epoch);
                    var topocentricCoordinates = astrometryConverter.ToTopocentric(status.Coordinates);
                    return new ServoCatStatus(celestialCoordinates, topocentricCoordinates, status.MotionStatus);
                },
                servoCatOptions.TelescopeStatusCacheTTL);
        }

        public void AbortSlew() {
            Logger.LogMessage("AbortSlew", "Not implemented");
            throw new MethodNotImplementedException("AbortSlew");
        }

        public AlignmentModes AlignmentMode {
            get {
                var alignmentMode = AlignmentModes.algAltAz;
                Logger.LogMessage("AlignmentMode Get", $"Get - {alignmentMode}");
                return alignmentMode;
            }
        }

        public double Altitude {
            get {
                var status = GetTelescopeStatus();
                var altitude = status.TopocentricCoordinates.Altitude.Degrees;
                Logger.LogMessage("Altitude", $"Get - {altitude}");
                return altitude;
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
                var status = GetTelescopeStatus();
                var atPark = status.MotionStatus.HasFlag(MotionStatusEnum.PARK);
                Logger.LogMessage("AtPark", $"Get - {atPark}");
                return atPark;
            }
        }

        public IAxisRates AxisRates(TelescopeAxes Axis) {
            // TODO: Implement after adding support for MoveAxis
            Logger.LogMessage("AxisRates Get", "Not implemented");
            throw new PropertyNotImplementedException("AxisRates", false);

            /*
            Logger.LogMessage("AxisRates", "Get - " + Axis.ToString());
            return new AxisRates(Axis);
            */
        }

        public double Azimuth {
            get {
                var status = GetTelescopeStatus();
                var azimuth = status.TopocentricCoordinates.Azimuth.Degrees;
                Logger.LogMessage("Azimuth", $"Get - {azimuth}");
                return azimuth;
            }
        }

        public bool CanFindHome {
            get {
                Logger.LogMessage("CanFindHome", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanMoveAxis(TelescopeAxes Axis) {
            // TODO: Change to true after MoveAxis support is added
            Logger.LogMessage("CanMoveAxis", "Get - " + false.ToString());
            return false;

            /*
            Logger.LogMessage("CanMoveAxis", "Get - " + Axis.ToString());
            switch (Axis) {
                case TelescopeAxes.axisPrimary: return false;
                case TelescopeAxes.axisSecondary: return false;
                case TelescopeAxes.axisTertiary: return false;
                default: throw new InvalidValueException("CanMoveAxis", Axis.ToString(), "0 to 2");
            }
            */
        }

        public bool CanPark {
            get {
                Logger.LogMessage("CanPark", "Get - " + true.ToString());
                return true;
            }
        }

        public bool CanPulseGuide {
            get {
                // TODO: Change to true after MoveAxis + PulseGuide support is added
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
                Logger.LogMessage("CanSetTracking", "Get - " + true.ToString());
                return true;
            }
        }

        public bool CanSlew {
            get {
                Logger.LogMessage("CanSlew", "Get - " + true.ToString());
                return true;
            }
        }

        public bool CanSlewAltAz {
            get {
                Logger.LogMessage("CanSlewAltAz", "Get - " + true.ToString());
                return true;
            }
        }

        public bool CanSlewAltAzAsync {
            get {
                Logger.LogMessage("CanSlewAltAzAsync", "Get - " + true.ToString());
                return true;
            }
        }

        public bool CanSlewAsync {
            get {
                Logger.LogMessage("CanSlewAsync", "Get - " + true.ToString());
                return true;
            }
        }

        public bool CanSync {
            get {
                // TODO: Add sync support, where deltas are in Alt/Az
                Logger.LogMessage("CanSync", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanSyncAltAz {
            get {
                // TODO: Add sync support, where deltas are in Alt/Az
                Logger.LogMessage("CanSyncAltAz", "Get - " + false.ToString());
                return false;
            }
        }

        public bool CanUnpark {
            get {
                Logger.LogMessage("CanUnpark", "Get - " + true.ToString());
                return true;
            }
        }

        public double Declination {
            get {
                var status = GetTelescopeStatus();
                var dec = status.CelestialCoordinates.Dec;
                Logger.LogMessage("Declination", $"Get - {dec.DMS}");
                return dec.Degrees;
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
                var equatorialSystem = servoCatOptions.UseJ2000 ? EquatorialCoordinateType.equJ2000 : EquatorialCoordinateType.equTopocentric;
                Logger.LogMessage("EquatorialSystem", "Get - " + equatorialSystem.ToString());
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
            // TODO: Add MoveAxis support
            Logger.LogMessage("MoveAxis", "Not implemented");
            throw new MethodNotImplementedException("MoveAxis");
        }

        public void Park() {
            Logger.LogMessage("Park", "Started");
            var result = DeviceActionWithTimeout(servoCatDevice.Park);
            WaitForStatusPredicate(ms => ms.HasFlag(MotionStatusEnum.PARK), servoCatOptions.SlewTimeout).Wait();
            Logger.LogMessage("Park", $"Completed with {result}");
        }

        public void PulseGuide(GuideDirections Direction, int Duration) {
            // TODO: Add PulseGuide support
            Logger.LogMessage("PulseGuide", "Not implemented");
            throw new MethodNotImplementedException("PulseGuide");
        }

        public double RightAscension {
            get {
                var status = GetTelescopeStatus();
                var ra = status.CelestialCoordinates.RA;
                Logger.LogMessage("RightAscension", $"Get - {ra.HMS}");
                return ra.Hours;
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
                double siderealTime = 0.0;
                double julianDate = ascomUtilities.DateUTCToJulian(DateTime.UtcNow);
                novas.SiderealTime(julianDate, 0, novas.DeltaT(julianDate), GstType.GreenwichApparentSiderealTime, Method.EquinoxBased, Accuracy.Full, ref siderealTime);
                siderealTime += SiteLongitude / 360.0 * 24.0;
                siderealTime = astroUtilities.ConditionRA(siderealTime);

                Logger.LogMessage("SiderealTime", "Get - " + siderealTime.ToString());
                return siderealTime;
            }
        }

        public double SiteElevation {
            get {
                var elevation = this.servoCatOptions.Elevation;
                Logger.LogMessage("SiteElevation Get", $"Get - {elevation}");
                return elevation;
            }
            set {
                Logger.LogMessage("SiteElevation Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteElevation", true);
            }
        }

        public double SiteLatitude {
            get {
                var latitude = this.servoCatOptions.Latitude;
                Logger.LogMessage("SiteLatitude Get", $"Get - {latitude}");
                return latitude;
            }
            set {
                Logger.LogMessage("SiteLatitude Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteLatitude", true);
            }
        }

        public double SiteLongitude {
            get {
                var longitude = this.servoCatOptions.Longitude;
                Logger.LogMessage("SiteLongitude Get", $"Get - {longitude}");
                return longitude;
            }
            set {
                Logger.LogMessage("SiteLongitude Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteLongitude", true);
            }
        }

        private void StartSlewToTarget(Angle rightAscension, Angle declination) {
            var result = DeviceActionWithTimeout((ct) => {
                var epoch = GetEpoch();
                var coordinates = new ICRSCoordinates(ra: rightAscension, dec: declination, epoch: epoch);
                return servoCatDevice.GotoExtendedPrecision(coordinates, ct);
            });
            if (!result) {
                throw new DriverException($"GotoExtendedPrecision to RA={rightAscension.HMS}, Dec={declination.DMS} failed");
            }

            // Update the cached status to Slewing immediately returns true
            var status = GetTelescopeStatus();
            status.MotionStatus |= MotionStatusEnum.GOTO;
        }

        private async Task WaitForStatusPredicate(Predicate<MotionStatusEnum> condition, TimeSpan timeout) {
            var startedTime = DateTime.Now;
            var latestMotionStatus = MotionStatusEnum.NONE;
            while (true) {
                var elapsedTime = DateTime.Now - startedTime;
                if (elapsedTime > timeout) {
                    Logger.LogMessage("WaitUntilStatusFlagSet", $"Failed - Timed out waiting for motion status predicate. Latest status was {latestMotionStatus}");
                    throw new TimeoutException($"Timed out waiting for motion status change");
                }

                if (disconnectTokenSource.IsCancellationRequested) {
                    throw new DriverException("Device disconnected");
                }

                var status = GetTelescopeStatus();
                latestMotionStatus = status.MotionStatus;
                if (condition(latestMotionStatus)) {
                    return;
                }
                await Task.Delay(servoCatOptions.TelescopeStatusCacheTTL);
            }
        }

        public void SlewToAltAz(double Azimuth, double Altitude) {
            var azimuth = Angle.ByDegree(Azimuth);
            var altitude = Angle.ByDegree(Altitude);
            Logger.LogMessage("SlewToAltAz", $"Started - Alt: {altitude.DMS}, Az: {azimuth.DMS}");

            if (Azimuth < 0.0 || Azimuth > 360.0d) {
                throw new InvalidValueException($"Invalid Azimuth {Azimuth}");
            }
            if (Altitude < -90.0 || Altitude > 90.0d) {
                throw new InvalidValueException($"Invalid Altitude {Altitude}");
            }
            if (AtPark) {
                Logger.LogMessage("SlewToAltAz", "Failed - Can't slew to AltAz when AtPark = True");
                throw new ParkedException("Can't slew to target when AtPark = True");
            }
            if (Tracking) {
                Logger.LogMessage("SlewToAltAz", "Failed - Can't slew to AltAz when Tracking = True");
                throw new InvalidOperationException($"Can't slew to AltAz when Tracking = True");
            }

            var topocentricCoordinates = new TopocentricCoordinates(
                altitude: altitude,
                azimuth: azimuth,
                longitude: Angle.ByDegree(servoCatOptions.Longitude),
                latitude: Angle.ByDegree(servoCatOptions.Latitude),
                elevation: servoCatOptions.Elevation,
                referenceDateTime: DateTime.Now);
            var icrsCoordinates = astrometryConverter.ToCelestial(topocentricCoordinates, GetEpoch());
            var rightAscension = icrsCoordinates.RA;
            var declination = icrsCoordinates.Dec;
            try {
                StartSlewToTarget(rightAscension, declination);
                Logger.LogMessage("SlewToAltAz", $"request completed successfully. Telescope is slewing now. Waiting for completion since this method is not Async");
                WaitForStatusPredicate(ms => (ms & ~MotionStatusEnum.GOTO) != 0, servoCatOptions.SlewTimeout).Wait();
                Logger.LogMessage("SlewToAltAz", $"request completed successfully.");
            } catch (Exception ex) {
                Logger.LogMessage("SlewToAltAz", $"Failed - {ex.Message}");
                throw;
            }
        }

        public void SlewToAltAzAsync(double Azimuth, double Altitude) {
            var azimuth = Angle.ByDegree(Azimuth);
            var altitude = Angle.ByDegree(Altitude);
            Logger.LogMessage("SlewToAltAzAsync", $"Started - Alt: {altitude.DMS}, Az: {azimuth.DMS}");

            if (Azimuth < 0.0 || Azimuth > 360.0d) {
                throw new InvalidValueException($"Invalid Azimuth {Azimuth}");
            }
            if (Altitude < -90.0 || Altitude > 90.0d) {
                throw new InvalidValueException($"Invalid Altitude {Altitude}");
            }

            if (AtPark) {
                Logger.LogMessage("SlewToAltAzAsync", "Failed - Can't slew to AltAz when AtPark = True");
                throw new ParkedException("Can't slew to target when AtPark = True");
            }
            if (Tracking) {
                Logger.LogMessage("SlewToAltAzAsync", "Failed - Can't slew to AltAz when Tracking = True");
                throw new InvalidOperationException($"Can't slew to AltAz when Tracking = True");
            }

            var topocentricCoordinates = new TopocentricCoordinates(
                altitude: altitude,
                azimuth: azimuth,
                longitude: Angle.ByDegree(servoCatOptions.Longitude),
                latitude: Angle.ByDegree(servoCatOptions.Latitude),
                elevation: servoCatOptions.Elevation,
                referenceDateTime: DateTime.Now);
            var icrsCoordinates = astrometryConverter.ToCelestial(topocentricCoordinates, GetEpoch());
            var rightAscension = icrsCoordinates.RA;
            var declination = icrsCoordinates.Dec;
            try {
                StartSlewToTarget(rightAscension, declination);
                Logger.LogMessage("SlewToAltAzAsync", $"request completed successfully. Telescope is slewing now. Not waiting for completion since this method is Async");
            } catch (Exception ex) {
                Logger.LogMessage("SlewToAltAzAsync", $"Failed - {ex.Message}");
                throw;
            }
        }

        public void SlewToCoordinates(double RightAscension, double Declination) {
            Logger.LogMessage("SlewToCoordinates", $"Started - RA: {RightAscension}, Dec: {Declination}. Using SlewToTarget");
            TargetRightAscension = RightAscension;
            TargetDeclination = Declination;
            SlewToTarget();
        }

        public void SlewToCoordinatesAsync(double RightAscension, double Declination) {
            Logger.LogMessage("SlewToCoordinatesAsync", $"Started - RA: {RightAscension}, Dec: {Declination}. Using SlewToTargetAsync");
            TargetRightAscension = RightAscension;
            TargetDeclination = Declination;
            SlewToTargetAsync();
        }

        public void SlewToTarget() {
            Logger.LogMessage("SlewToTarget", $"Started - RA: {targetRightAscension.HMS}, Dec: {targetDeclination.DMS}");
            if (AtPark) {
                Logger.LogMessage("SlewToTarget", "Failed - Can't slew to target when AtPark = True");
                throw new ParkedException("Can't slew to target when AtPark = True");
            }
            if (!Tracking) {
                Logger.LogMessage("SlewToTarget", "Failed - Can't slew to target when Tracking = False");
                throw new InvalidOperationException($"Can't slew to target when Tracking = False");
            }
            var rightAscension = Angle.ByHours(TargetRightAscension);
            var declination = Angle.ByDegree(TargetDeclination);
            try {
                StartSlewToTarget(rightAscension, declination);
                WaitForStatusPredicate(ms => (ms & ~MotionStatusEnum.GOTO) != 0, servoCatOptions.SlewTimeout).Wait();
                Logger.LogMessage("SlewToTarget", $"Initial request completed successfully. Telescope is slewing now");
            } catch (Exception ex) {
                Logger.LogMessage("SlewToTarget", $"Failed - {ex.Message}");
                throw;
            }
        }

        public void SlewToTargetAsync() {
            Logger.LogMessage("SlewToTargetAsync", $"Started - RA: {targetRightAscension.HMS}, Dec: {targetDeclination.DMS}");
            if (AtPark) {
                Logger.LogMessage("SlewToTargetAsync", "Failed - Can't slew to target when AtPark = True");
                throw new ParkedException("Can't slew to target when AtPark = True");
            }
            if (!Tracking) {
                // TODO: Verify what happens to tracking status after each of the slew methods, in case tracking needs to explicitly be turned on/off
                Logger.LogMessage("SlewToTargetAsync", "Failed - Can't slew to target when Tracking = False");
                throw new InvalidOperationException($"Can't slew to target when Tracking = False");
            }
            var rightAscension = Angle.ByHours(TargetRightAscension);
            var declination = Angle.ByDegree(TargetDeclination);
            try {
                StartSlewToTarget(rightAscension, declination);
                Logger.LogMessage("SlewToTargetAsync", $"request completed successfully. Telescope is slewing now. Not waiting for completion since this method is Async");
            } catch (Exception ex) {
                Logger.LogMessage("SlewToTargetAsync", $"Failed - {ex.Message}");
                throw;
            }
        }

        public bool Slewing {
            get {
                var status = GetTelescopeStatus();
                var slewing = status.MotionStatus.HasFlag(MotionStatusEnum.GOTO) || status.MotionStatus.HasFlag(MotionStatusEnum.USER_MOTION);
                Logger.LogMessage("Slewing", $"Get - {slewing}");
                return slewing;
            }
        }

        public void SyncToAltAz(double Azimuth, double Altitude) {
            // TODO: Implement sync methods
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
                var targetDeclination = this.targetDeclination;
                var targetDeclinationDegrees = targetDeclination.Degrees;
                if (double.IsNaN(targetDeclinationDegrees)) {
                    Logger.LogMessage("TargetDeclination Get", "Failed - TargetDeclination hasn't been set yet");
                    throw new InvalidOperationException($"TargetDeclination hasn't been set yet");
                }
                Logger.LogMessage("TargetDeclination Get", $"Get - {targetDeclination.DMS}");
                return targetDeclinationDegrees;
            }
            set {
                if (double.IsNaN(value) || value < -90.0 || value > 90.0) {
                    Logger.LogMessage("TargetDeclination Set", $"Failed - TargetDeclination {value} is invalid");
                    throw new InvalidValueException($"TargetDeclination {value} is invalid");
                }

                Logger.LogMessage("TargetDeclination Set", $"Set - {value}");
                targetDeclination = Angle.ByDegree(value);
            }
        }

        public double TargetRightAscension {
            get {
                var targetRightAscension = this.targetRightAscension;
                var targetRightAscensionHours = targetRightAscension.Hours;
                if (double.IsNaN(targetRightAscensionHours)) {
                    Logger.LogMessage("TargetRightAscension Get", "Failed - TargetRightAscension hasn't been set yet");
                    throw new InvalidOperationException($"TargetRightAscension hasn't been set yet");
                }
                Logger.LogMessage("TargetRightAscension Get", $"Get - {targetRightAscension.HMS}");
                return targetRightAscensionHours;
            }
            set {
                if (double.IsNaN(value) || value < 0.0 || value > 24.0) {
                    Logger.LogMessage("TargetRightAscension Set", $"Failed - TargetRightAscension {value} is invalid");
                    throw new InvalidValueException($"TargetRightAscension {value} is invalid");
                }

                Logger.LogMessage("TargetRightAscension Set", $"Set - {value}");
                targetRightAscension = Angle.ByHours(value);
            }
        }

        public bool Tracking {
            get {
                var status = GetTelescopeStatus();
                var tracking = status.MotionStatus.HasFlag(MotionStatusEnum.TRACK);
                Logger.LogMessage("Tracking", $"Get - {tracking}");
                return tracking;
            }
            set {
                Logger.LogMessage("Tracking", $"Set {value} - Started");
                var result = DeviceActionWithTimeout((ct) => {
                    if (value) {
                        return servoCatDevice.EnableTracking(ct);
                    } else {
                        return servoCatDevice.DisableTracking(Axis.BOTH, ct);
                    }
                });
                Logger.LogMessage("Tracking Set", $"Completed with {result}");
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
            Logger.LogMessage("Unpark", "Started");
            var result = DeviceActionWithTimeout(servoCatDevice.Unpark);
            Logger.LogMessage("Unpark", $"Completed with {result}");
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

            driverConnectionManager.Disconnect(driverClientId, CancellationToken.None).Wait();
            driverConnectionManager.UnregisterClient(driverClientId).Wait();
        }
    }
}