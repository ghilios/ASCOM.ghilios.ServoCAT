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

    public class ServoCatDevice : IServoCatDevice {
        private readonly IChannel channel;
        private readonly IServoCatOptions options;
        private readonly AstrometryConverter astrometryConverter;

        public ServoCatDevice(IChannel channel, IServoCatOptions options, AstrometryConverter astrometryConverter) {
            this.channel = channel;
            this.options = options;
            this.astrometryConverter = astrometryConverter;
        }
    }
}