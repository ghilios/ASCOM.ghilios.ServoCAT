#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Joko.ServoCAT.Interfaces;
using ASCOM.Utilities.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.Joko.ServoCAT.Astrometry {

    public class AstrometryOptions : IAstrometryOptions {
        private readonly IProfile ascomProfile;

        public AstrometryOptions(IProfile ascomProfile) {
            this.ascomProfile = ascomProfile;
        }
    }
}