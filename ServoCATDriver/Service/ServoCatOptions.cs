#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.ghilios.ServoCAT.Telescope;
using ASCOM.ghilios.ServoCAT.Utility;
using ASCOM.Utilities.Interfaces;
using Ninject;
using PostSharp.Patterns.Model;
using System;

namespace ASCOM.ghilios.ServoCAT.Service {

    [NotifyPropertyChanged]
    public class ServoCatOptions : BaseINPC, IServoCatOptions {
        private readonly IProfile ascomProfile;
        private readonly ISharedState sharedState;
        private readonly string driverId;
        private const string astrometrySettingKey = "astrometry_settings";
        private const string connectionSettingsKey = "connection_settings";
        private const string firmwareSettingsKey = "firmware_settings";

        public ServoCatOptions([Named("Telescope")] IProfile ascomProfile, ISharedState sharedState) {
            this.ascomProfile = ascomProfile;
            this.sharedState = sharedState;
            this.driverId = sharedState.TelescopeDriverId;
        }

        public void Load() {
            Longitude = ascomProfile.GetDouble(driverId, "longitude", astrometrySettingKey, 0.0d);
            Latitude = ascomProfile.GetDouble(driverId, "latitude", astrometrySettingKey, 0.0d);
            Elevation = ascomProfile.GetDouble(driverId, "elevation", astrometrySettingKey, 0.0d);
            SerialPort = ascomProfile.GetString(driverId, "serialPort", connectionSettingsKey, null);
            UseJ2000 = ascomProfile.GetBool(driverId, "useJ2000", astrometrySettingKey, false);
            ConnectionType = ascomProfile.GetEnum(driverId, "connectionType", connectionSettingsKey, ConnectionType.Serial);
            var simulatorFirmwareVersion = ascomProfile.GetInt32(driverId, "simulatorFirmwareVersion", connectionSettingsKey, 61);
            var simulatorFirmwareSubVersion = ascomProfile.GetString(driverId, "simulatorFirmwareSubVersion", connectionSettingsKey, "C");
            if (simulatorFirmwareVersion < 61 || simulatorFirmwareVersion >= 100 || simulatorFirmwareSubVersion.Length != 1) {
                SimulatorVersion = FirmwareVersion.GetDefault();
            } else {
                SimulatorVersion = new FirmwareVersion() { Version = (ushort)simulatorFirmwareVersion, SubVersion = simulatorFirmwareSubVersion[0] };
            }
            SimulatorAligned = ascomProfile.GetBool(driverId, "simulatorAligned", connectionSettingsKey, true);
            FirmwareConfigLoaded = ascomProfile.GetBool(driverId, "firmwareConfigLoaded", connectionSettingsKey, false);
            UseSpeed1 = ascomProfile.GetBool(driverId, "useSpeed1", connectionSettingsKey, true);
            EnableServerLogging = ascomProfile.GetBool(driverId, "enableServerLogging", connectionSettingsKey, true);
            EnableTelescopeLogging = ascomProfile.GetBool(driverId, "enableTelescopeLogging", connectionSettingsKey, true);
            EnableSerialLogging = ascomProfile.GetBool(driverId, "enableSerialLogging", connectionSettingsKey, true);
            AlignmentMode = ascomProfile.GetEnum(driverId, "alignmentMode", astrometrySettingKey, AlignmentMode.AltAz);
            LoadFirmwareConfig();
        }

        public void Save() {
            ascomProfile.WriteDouble(driverId, "longitude", astrometrySettingKey, Longitude);
            ascomProfile.WriteDouble(driverId, "latitude", astrometrySettingKey, Latitude);
            ascomProfile.WriteDouble(driverId, "elevation", astrometrySettingKey, Elevation);
            ascomProfile.WriteString(driverId, "serialPort", connectionSettingsKey, SerialPort);
            ascomProfile.WriteBool(driverId, "useJ2000", astrometrySettingKey, UseJ2000);
            ascomProfile.WriteEnum(driverId, "connectionType", connectionSettingsKey, ConnectionType);
            ascomProfile.WriteInt32(driverId, "simulatorFirmwareVersion", connectionSettingsKey, SimulatorVersion.Version);
            ascomProfile.WriteString(driverId, "simulatorFirmwareSubVersion", connectionSettingsKey, $"{SimulatorVersion.SubVersion}");
            ascomProfile.WriteBool(driverId, "simulatorAligned", connectionSettingsKey, SimulatorAligned);
            ascomProfile.WriteBool(driverId, "firmwareConfigLoaded", connectionSettingsKey, FirmwareConfigLoaded);
            ascomProfile.WriteBool(driverId, "useSpeed1", connectionSettingsKey, UseSpeed1);
            ascomProfile.WriteBool(driverId, "enableServerLogging", connectionSettingsKey, EnableServerLogging);
            ascomProfile.WriteBool(driverId, "enableTelescopeLogging", connectionSettingsKey, EnableTelescopeLogging);
            ascomProfile.WriteBool(driverId, "enableSerialLogging", connectionSettingsKey, EnableSerialLogging);
            ascomProfile.WriteEnum(driverId, "alignmentMode", astrometrySettingKey, AlignmentMode);
            SaveFirmwareConfig();
        }

        private void SaveFirmwareAxisConfig(string axisName, ServoCatFirmwareAxisConfig axisConfig) {
            var profileSubKey = $"{firmwareSettingsKey}_{axisName}";
            ascomProfile.WriteInt16(driverId, "encoderResolution", profileSubKey, axisConfig.EncoderResolution);
            ascomProfile.WriteInt16(driverId, "gearRatioValue1", profileSubKey, axisConfig.GearRatioValue1);
            ascomProfile.WriteInt16(driverId, "slewRateValue1_TDPS", profileSubKey, axisConfig.SlewRateValue1_TDPS);
            ascomProfile.WriteInt16(driverId, "jogRateValue1_AMPS", profileSubKey, axisConfig.JogRateValue1_AMPS);
            ascomProfile.WriteInt16(driverId, "guideRateValue1_ASPS", profileSubKey, axisConfig.GuideRateValue1_ASPS);
            ascomProfile.WriteInt16(driverId, "slewRateValue2_TDPS", profileSubKey, axisConfig.SlewRateValue2_TDPS);
            ascomProfile.WriteInt16(driverId, "jogRateValue2_AMPS", profileSubKey, axisConfig.JogRateValue2_AMPS);
            ascomProfile.WriteInt16(driverId, "guideRateValue2_ASPS", profileSubKey, axisConfig.GuideRateValue2_ASPS);
            ascomProfile.WriteInt16(driverId, "accelDecelRateSecs", profileSubKey, axisConfig.AccelDecelRateSecs);
            ascomProfile.WriteInt16(driverId, "backlashValue", profileSubKey, axisConfig.BacklashValue);
            ascomProfile.WriteInt16(driverId, "axisLimit", profileSubKey, axisConfig.AxisLimit);
            ascomProfile.WriteBool(driverId, "trackDirectionPositive", profileSubKey, axisConfig.TrackDirectionPositive);
            ascomProfile.WriteBool(driverId, "goToDirectionPositive", profileSubKey, axisConfig.GoToDirectionPositive);
        }

        private void LoadFirmwareAxisConfig(string axisName, ServoCatFirmwareAxisConfig axisConfig) {
            var profileSubKey = $"{firmwareSettingsKey}_{axisName}";
            axisConfig.EncoderResolution = ascomProfile.GetInt16(driverId, "encoderResolution", profileSubKey, short.MaxValue);
            axisConfig.GearRatioValue1 = ascomProfile.GetInt16(driverId, "gearRatioValue1", profileSubKey, short.MaxValue);
            axisConfig.SlewRateValue1_TDPS = ascomProfile.GetInt16(driverId, "slewRateValue1_TDPS", profileSubKey, short.MaxValue);
            axisConfig.JogRateValue1_AMPS = ascomProfile.GetInt16(driverId, "jogRateValue1_AMPS", profileSubKey, short.MaxValue);
            axisConfig.SlewRateValue2_TDPS = ascomProfile.GetInt16(driverId, "slewRateValue2_TDPS", profileSubKey, short.MaxValue);
            axisConfig.JogRateValue2_AMPS = ascomProfile.GetInt16(driverId, "jogRateValue2_AMPS", profileSubKey, short.MaxValue);
            axisConfig.GuideRateValue2_ASPS = ascomProfile.GetInt16(driverId, "guideRateValue2_ASPS", profileSubKey, short.MaxValue);
            axisConfig.AccelDecelRateSecs = ascomProfile.GetInt16(driverId, "accelDecelRateSecs", profileSubKey, short.MaxValue);
            axisConfig.BacklashValue = ascomProfile.GetInt16(driverId, "backlashValue", profileSubKey, short.MaxValue);
            axisConfig.AxisLimit = ascomProfile.GetInt16(driverId, "axisLimit", profileSubKey, short.MaxValue);
            axisConfig.TrackDirectionPositive = ascomProfile.GetBool(driverId, "trackDirectionPositive", profileSubKey, true);
            axisConfig.GoToDirectionPositive = ascomProfile.GetBool(driverId, "goToDirectionPositive", profileSubKey, true);
        }

        private void SaveFirmwareConfig() {
            SaveFirmwareAxisConfig("altitude", FirmwareConfig.AltitudeConfig);
            SaveFirmwareAxisConfig("azimuth", FirmwareConfig.AzimuthConfig);
            ascomProfile.WriteInt16(driverId, "easyTrackLatitudeValue", firmwareSettingsKey, FirmwareConfig.EasyTrackLatitudeValue);
            ascomProfile.WriteInt16(driverId, "easyTrackSignValue", firmwareSettingsKey, FirmwareConfig.EasyTrackSignValue);
        }

        private void LoadFirmwareConfig() {
            LoadFirmwareAxisConfig("altitude", FirmwareConfig.AltitudeConfig);
            LoadFirmwareAxisConfig("azimuth", FirmwareConfig.AzimuthConfig);
            FirmwareConfig.EasyTrackLatitudeValue = ascomProfile.GetInt16(driverId, "easyTrackLatitudeValue", firmwareSettingsKey, 0);
            FirmwareConfig.EasyTrackSignValue = ascomProfile.GetInt16(driverId, "easyTrackSignValue", firmwareSettingsKey, 0);
        }

        public void CopyFrom(IServoCatOptions servoCatOptions) {
            this.Longitude = servoCatOptions.Longitude;
            this.Latitude = servoCatOptions.Latitude;
            this.Elevation = servoCatOptions.Elevation;
            this.SerialPort = servoCatOptions.SerialPort;
            this.UseJ2000 = servoCatOptions.UseJ2000;
            this.ConnectionType = servoCatOptions.ConnectionType;
            this.SimulatorVersion = servoCatOptions.SimulatorVersion;
            this.SimulatorAligned = servoCatOptions.SimulatorAligned;
            this.FirmwareConfigLoaded = servoCatOptions.FirmwareConfigLoaded;
            this.FirmwareConfig.CopyFrom(servoCatOptions.FirmwareConfig);
            this.UseSpeed1 = servoCatOptions.UseSpeed1;
            this.EnableServerLogging = servoCatOptions.EnableServerLogging;
            this.EnableTelescopeLogging = servoCatOptions.EnableTelescopeLogging;
            this.EnableSerialLogging = servoCatOptions.EnableSerialLogging;
            this.AlignmentMode = servoCatOptions.AlignmentMode;
        }

        public IServoCatOptions Clone() {
            var clone = new ServoCatOptions(ascomProfile, sharedState);
            clone.CopyFrom(this);
            return clone;
        }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double Elevation { get; set; }

        public string SerialPort { get; set; }

        public bool UseJ2000 { get; set; }
        public ConnectionType ConnectionType { get; set; }
        public FirmwareVersion SimulatorVersion { get; set; }
        public bool SimulatorAligned { get; set; }
        public TimeSpan MainWindowPollInterval => TimeSpan.FromSeconds(2);
        public TimeSpan TelescopeStatusCacheTTL => TimeSpan.FromMilliseconds(500);
        public TimeSpan DeviceRequestTimeout => TimeSpan.FromSeconds(5);
        public int DeviceUnexpectedResponseRetries => 3;
        public TimeSpan SlewTimeout => TimeSpan.FromMinutes(1);
        public bool FirmwareConfigLoaded { get; set; }
        public ServoCatFirmwareConfig FirmwareConfig { get; } = new ServoCatFirmwareConfig();

        public bool UseSpeed1 { get; set; }
        public bool EnableServerLogging { get; set; }
        public bool EnableTelescopeLogging { get; set; }
        public bool EnableSerialLogging { get; set; }

        public AlignmentMode AlignmentMode { get; set; }
    }
}