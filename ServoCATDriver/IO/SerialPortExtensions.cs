#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.IO {

    public static class SerialPortExtensions {

        public static async Task<byte[]> ReadAsync(this SerialPort serialPort, int count, CancellationToken ct = default) {
            var bytesToRead = count;
            int totalBytesRead = 0;
            var buffer = new byte[count];

            // BaseStream can't be used for async IO because it can hang, and doesn't honor cancellation tokens or timeouts. As a workaround,
            // we check for bytes to read before issuing a ReadAsync
            while (bytesToRead > 0) {
                ct.ThrowIfCancellationRequested();
                if (!serialPort.IsOpen) {
                    throw new EndOfStreamException();
                }

                if (serialPort.BytesToRead > 0) {
                    // SerialPort's BaseStream ignores the cancellation token. Passing None to avoid confusion
                    var readBytes = await serialPort.BaseStream.ReadAsync(buffer, totalBytesRead, bytesToRead, CancellationToken.None);
                    if (readBytes == 0) {
                        throw new EndOfStreamException();
                    }
                    bytesToRead -= readBytes;
                    totalBytesRead += readBytes;
                }
                await Task.Delay(50, ct);
            }
            return buffer;
        }

        public static Task WriteAsync(this SerialPort serialPort, byte[] buffer, int offset, int count, CancellationToken ct = default) {
            return serialPort.BaseStream.WriteAsync(buffer, offset, count, ct);
        }
    }
}