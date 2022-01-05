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

namespace ASCOM.ghilios.ServoCAT.IO {

    public class ChannelFactory : IChannelFactory {
        private readonly IServoCatOptions options;
        private readonly AstrometryConverter astrometryConverter;
        private readonly TraceLogger telescopeLogger;
        private readonly TraceLogger serialLogger;

        public ChannelFactory(
            IServoCatOptions options,
            AstrometryConverter astrometryConverter,
            [Named("Telescope")] TraceLogger telescopeLogger,
            [Named("Serial")] TraceLogger serialLogger) {
            this.options = options;
            this.astrometryConverter = astrometryConverter;
            this.telescopeLogger = telescopeLogger;
            this.serialLogger = serialLogger;
        }

        public IChannel Create() {
            if (options.ConnectionType == ConnectionType.Simulator) {
                return new SimulatorChannel(options, astrometryConverter, telescopeLogger);
            } else if (options.ConnectionType == ConnectionType.Serial) {
                var serialConfig = SerialChannelConfig.CreateDefaultConfig(options.SerialPort);
                return new SerialChannel(serialConfig, serialLogger);
            } else {
                throw new NotImplementedException();
            }
        }
    }
}