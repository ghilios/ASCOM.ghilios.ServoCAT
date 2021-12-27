#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.Joko.ServoCAT.IO {

    public static class SerialPortExtensions {

        public static async Task ReadAsync(this SerialPort serialPort, byte[] buffer, int offset, int count, CancellationToken ct = default) {
            var bytesToRead = count;
            var temp = new byte[count];

            while (bytesToRead > 0) {
                ct.ThrowIfCancellationRequested();
                var readBytes = await serialPort.BaseStream.ReadAsync(temp, 0, bytesToRead, ct);
                if (readBytes == 0) {
                    throw new EndOfStreamException();
                }
                Buffer.BlockCopy(temp, 0, buffer, offset + count, readBytes);
                bytesToRead -= readBytes;
            }
        }

        public static async Task<byte[]> ReadAsync(this SerialPort serialPort, int count, CancellationToken ct = default) {
            var buffer = new byte[count];
            await serialPort.ReadAsync(buffer, 0, count, ct);
            return buffer;
        }

        public static Task WriteAsync(this SerialPort serialPort, byte[] buffer, int offset, int count, CancellationToken ct = default) {
            return serialPort.BaseStream.WriteAsync(buffer, offset, count, ct);
        }

        public static async Task<byte[]> ReadToAsync(this SerialPort serialPort, string terminator, CancellationToken ct = default) {
            var terminatorBytes = Encoding.ASCII.GetBytes(terminator);
            var terminatorLength = terminatorBytes.Length;
            int terminatorMatchLength = 0;
            var buffer = new byte[1];
            using (var ms = new MemoryStream()) {
                while (true) {
                    ct.ThrowIfCancellationRequested();
                    await serialPort.ReadAsync(buffer, 0, 1, ct);
                    ms.Write(buffer, 0, 1);
                    if (buffer[0] == terminatorBytes[terminatorMatchLength]) {
                        if (++terminatorMatchLength == terminatorLength) {
                            return ms.ToArray();
                        }
                    } else {
                        terminatorMatchLength = 0;
                    }
                }
            }
        }
    }
}