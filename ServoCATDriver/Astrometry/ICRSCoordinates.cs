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

namespace ASCOM.Joko.ServoCAT.Astrometry {

    public class ICRSCoordinates {

        public ICRSCoordinates(Angle ra, Angle dec, Epoch epoch) : this(ra, dec, epoch, DateTime.Now) {
        }

        public ICRSCoordinates(Angle ra, Angle dec, Epoch epoch, DateTime referenceDateTime) {
            this.RA = ra.ToNormal();
            if (dec.Radians > Angle.HALF_PI || dec.Radians < -Angle.HALF_PI) {
                throw new ArgumentException($"{dec.Degrees} must be within [-90, 90] to be a valid Dec coordinate");
            }
            this.Dec = dec;
            this.Epoch = epoch;
            this.ReferenceDateTime = referenceDateTime;
        }

        public DateTime ReferenceDateTime { get; private set; }

        public Angle RA { get; private set; }

        public Angle Dec { get; private set; }

        public Epoch Epoch { get; private set; }

        public override string ToString() {
            return $"RA={RA.HMS}, Dec={Dec.DMS}, Epoch={Epoch}, Reference={ReferenceDateTime}";
        }
    }
}