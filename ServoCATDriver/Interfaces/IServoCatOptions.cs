#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System.ComponentModel;
using ASCOM.Joko.ServoCAT.Converters;

namespace ASCOM.Joko.ServoCAT.Interfaces {

    [TypeConverter(typeof(EnumStaticDescriptionTypeConverter))]
    public enum ConnectionType {
        Simulator,
        Serial
    }

    public interface IServoCatOptions {

        void Save();

        void Load();

        double Latitude { get; set; }
        double Longitude { get; set; }
        double Elevation { get; set; }
        ConnectionType ConnectionType { get; set; }
        bool CoordinatesSet { get; }
        string SerialPort { get; set; }
        bool UseJ2000 { get; set; }
    }
}