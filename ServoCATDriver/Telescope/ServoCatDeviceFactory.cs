#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.Utilities;
using Ninject;

namespace ASCOM.ghilios.ServoCAT.Telescope {

    public class ServoCatDeviceFactory : IServoCatDeviceFactory {
        private readonly IServoCatOptions options;
        private readonly AstrometryConverter astrometryConverter;
        private readonly TraceLogger logger;

        public ServoCatDeviceFactory(
            IServoCatOptions options,
            AstrometryConverter astrometryConverter,
            [Named("Telescope")] TraceLogger logger) {
            this.options = options;
            this.astrometryConverter = astrometryConverter;
            this.logger = logger;
        }

        public IServoCatDevice Create(IChannel channel) {
            return new ServoCatDevice(channel, options, astrometryConverter, logger);
        }
    }
}