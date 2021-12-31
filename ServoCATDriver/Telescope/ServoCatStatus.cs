#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Joko.ServoCAT.Astrometry;
using ASCOM.Joko.ServoCAT.Interfaces;

namespace ASCOM.Joko.ServoCAT.Telescope {

    public class ServoCatStatus {

        public ServoCatStatus(ICRSCoordinates celestialCoordinates, TopocentricCoordinates topocentricCoordinates, MotionStatusEnum motionStatus) {
            this.CelestialCoordinates = celestialCoordinates;
            this.TopocentricCoordinates = topocentricCoordinates;
            this.MotionStatus = motionStatus;
        }

        public ICRSCoordinates CelestialCoordinates { get; private set; }
        public TopocentricCoordinates TopocentricCoordinates { get; private set; }
        public MotionStatusEnum MotionStatus { get; set; }
    }
}