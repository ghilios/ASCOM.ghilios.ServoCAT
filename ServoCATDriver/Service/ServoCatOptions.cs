#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Joko.ServoCAT.Interfaces;
using ASCOM.Joko.ServoCAT.Utility;
using ASCOM.Utilities.Interfaces;
using Ninject;
using PostSharp.Patterns.Model;

namespace ASCOM.Joko.ServoCAT.Service {

    [NotifyPropertyChanged]
    public class ServoCatOptions : BaseINPC, IServoCatOptions {
        private readonly IProfile ascomProfile;
        private readonly ISharedState sharedState;
        private readonly string driverId;
        private const string profileSubKey = "astrometry_settings";

        public ServoCatOptions([Named("Telescope")] IProfile ascomProfile, ISharedState sharedState) {
            this.ascomProfile = ascomProfile;
            this.sharedState = sharedState;
            this.driverId = sharedState.TelescopeDriverId;
        }

        public void Load() {
            Longitude = ascomProfile.GetDouble(driverId, "longitude", profileSubKey, 0.0d);
            Latitude = ascomProfile.GetDouble(driverId, "latitude", profileSubKey, 0.0d);
            Elevation = ascomProfile.GetDouble(driverId, "elevation", profileSubKey, 0.0d);
            SerialPort = ascomProfile.GetString(driverId, "serialPort", profileSubKey, null);
            UseJ2000 = ascomProfile.GetBool(driverId, "useJ2000", profileSubKey, false);
            ConnectionType = ascomProfile.GetEnum(driverId, "connectionType", profileSubKey, ConnectionType.Simulator);
        }

        public void Save() {
            ascomProfile.WriteDouble(driverId, "longitude", profileSubKey, Longitude);
            ascomProfile.WriteDouble(driverId, "latitude", profileSubKey, Latitude);
            ascomProfile.WriteDouble(driverId, "elevation", profileSubKey, Elevation);
            ascomProfile.WriteString(driverId, "serialPort", profileSubKey, SerialPort);
            ascomProfile.WriteBool(driverId, "useJ2000", profileSubKey, UseJ2000);
            ascomProfile.WriteEnum(driverId, "connectionType", profileSubKey, ConnectionType);
        }

        public void CopyFrom(IServoCatOptions servoCatOptions) {
            this.Longitude = servoCatOptions.Longitude;
            this.Latitude = servoCatOptions.Latitude;
            this.Elevation = servoCatOptions.Elevation;
            this.SerialPort = servoCatOptions.SerialPort;
            this.UseJ2000 = servoCatOptions.UseJ2000;
            this.ConnectionType = servoCatOptions.ConnectionType;
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
    }
}