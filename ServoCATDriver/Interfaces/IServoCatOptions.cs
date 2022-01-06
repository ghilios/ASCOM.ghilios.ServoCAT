#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.ComponentModel;
using ASCOM.ghilios.ServoCAT.Converters;
using ASCOM.ghilios.ServoCAT.Telescope;

namespace ASCOM.ghilios.ServoCAT.Interfaces {

    [TypeConverter(typeof(EnumStaticDescriptionTypeConverter))]
    public enum ConnectionType {
        Simulator,
        Serial
    }

    public interface IServoCatOptions {

        void Save();

        void Load();

        void CopyFrom(IServoCatOptions servoCatOptions);

        IServoCatOptions Clone();

        double Latitude { get; set; }
        double Longitude { get; set; }
        double Elevation { get; set; }
        ConnectionType ConnectionType { get; set; }
        string SerialPort { get; set; }
        FirmwareVersion SimulatorVersion { get; set; }
        bool SimulatorAligned { get; set; }
        bool UseJ2000 { get; set; }
        TimeSpan MainWindowPollInterval { get; }
        TimeSpan TelescopeStatusCacheTTL { get; }
        TimeSpan DeviceRequestTimeout { get; }
        TimeSpan SlewTimeout { get; }
        bool FirmwareConfigLoaded { get; set; }
        ServoCatFirmwareConfig FirmwareConfig { get; }
        bool UseSpeed1 { get; set; }
        bool EnableServerLogging { get; set; }
        bool EnableTelescopeLogging { get; set; }
        bool EnableSerialLogging { get; set; }
        int DeviceUnexpectedResponseRetries { get; }
    }
}