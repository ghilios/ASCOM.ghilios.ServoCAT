#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Joko.ServoCAT.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.Joko.ServoCAT.IO {

    public class SimulatorChannel : IChannel {
        private readonly IServoCatOptions options;
        private readonly DriverAccess.Telescope simulatorTelescope;

        private readonly MemoryQueueBufferStream memoryStream;

        public SimulatorChannel(IServoCatOptions options) {
            this.options = options;
            simulatorTelescope = new DriverAccess.Telescope("ASCOM.Simulator.Telescope");
            memoryStream = new MemoryQueueBufferStream();
        }

        public bool IsOpen => simulatorTelescope.Connected;

        public Task Close(CancellationToken ct) {
            simulatorTelescope.Connected = false;
            return Task.CompletedTask;
        }

        public void FlushReadExisting() {
            var length = (int)memoryStream.Length;
            if (length > 0) {
                var buffer = new byte[length];
                memoryStream.Read(buffer, 0, length);
            }
        }

        public Task Open(CancellationToken ct) {
            simulatorTelescope.Connected = true;
            return Task.CompletedTask;
        }

        public Task<byte[]> ReadBytes(int byteCount, CancellationToken ct) {
            throw new System.NotImplementedException();
        }

        public Task<byte[]> ReadUntil(string terminator, CancellationToken ct) {
            throw new System.NotImplementedException();
        }

        public Task Write(byte[] data, CancellationToken ct) {
            throw new System.NotImplementedException();
        }
    }
}