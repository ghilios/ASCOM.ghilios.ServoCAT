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

namespace ASCOM.Joko.ServoCAT.Service {

    public class ServoCatOptions : BaseINPC, IServoCatOptions {
        private readonly IProfile ascomProfile;
        private readonly string driverId;
        private const string profileSubKey = "astrometry_settings";

        public ServoCatOptions(IProfile ascomProfile, string driverId) {
            this.ascomProfile = ascomProfile;
            this.driverId = driverId;
            Load();
        }

        public void Load() {
            longitude = ascomProfile.GetDouble(driverId, "longitude", profileSubKey, double.NaN);
            latitude = ascomProfile.GetDouble(driverId, "latitude", profileSubKey, double.NaN);
            elevation = ascomProfile.GetDouble(driverId, "elevation", profileSubKey, 0.0d);
        }

        public void Save() {
            ascomProfile.WriteDouble(driverId, "longitude", profileSubKey, longitude);
            ascomProfile.WriteDouble(driverId, "latitude", profileSubKey, latitude);
            ascomProfile.WriteDouble(driverId, "elevation", profileSubKey, elevation);
        }

        public bool CoordinatesSet {
            get => !double.IsNaN(Longitude) && !double.IsNaN(Latitude) && !double.IsNaN(Elevation);
        }

        private double latitude;

        public double Latitude {
            get => latitude;
            set {
                if (latitude != value) {
                    latitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double longitude;

        public double Longitude {
            get => longitude;
            set {
                if (longitude != value) {
                    longitude = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double elevation;

        public double Elevation {
            get => elevation;
            set {
                if (elevation != value) {
                    elevation = value;
                    RaisePropertyChanged();
                }
            }
        }
    }
}