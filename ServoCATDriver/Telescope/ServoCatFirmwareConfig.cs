#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.Telescope {

    public class ServoCatFirmwareAxisConfig {
        public int EncoderResolution { get; set; }
        public int GearRatio1TDPS { get; set; } // Stored as Tenth Degrees Per Second
        public int JogRate1AMPS { get; set; } // Arcmin Per Second
        public int SlewRate1ASPS { get; set; } // Arcsec Per Second
        public int GearRatio2TDPS { get; set; } // Tenth Degrees Per Second
        public int JogRate2AMPS { get; set; } // Arcmin Per Second
        public int SlewRate2ASPS { get; set; } // Arcsec Per Second
        public int AccelDecelRateSecs { get; set; } // Seconds
        public int BacklashArcSeconds { get; set; }  // >= 1000 represents arcsecs, otherwise arcmins
        public int AxisLimit { get; set; }
        public bool TrackDirectionPositive { get; set; }
        public bool GoToDirectionPositive { get; set; }
    }

    public class ServoCatFirmwareConfig {
        public ServoCatFirmwareAxisConfig AzimuthConfig { get; } = new ServoCatFirmwareAxisConfig();
        public ServoCatFirmwareAxisConfig AltitudeConfig { get; } = new ServoCatFirmwareAxisConfig();
        public int EasyTrackLatitude { get; set; } // 2000 * latitude
        public int EasyTrackSign { get; set; } // Valid values are 0-3. The config stores this as * 500 (0, 500, 1000, 1500)
    }
}