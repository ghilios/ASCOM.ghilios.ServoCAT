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
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.IO {

    public static class StreamExtensions {

        public static async Task ReadAsyncEx(this Stream stream, byte[] buffer, int offset, int count, CancellationToken ct = default) {
            var bytesToRead = count;
            var temp = new byte[count];
            var totalBytesRead = 0;

            while (bytesToRead > 0) {
                ct.ThrowIfCancellationRequested();
                var readBytes = await stream.ReadAsync(temp, 0, bytesToRead, ct);
                if (readBytes == 0) {
                    throw new EndOfStreamException();
                }
                Buffer.BlockCopy(temp, 0, buffer, offset + totalBytesRead, readBytes);
                bytesToRead -= readBytes;
                totalBytesRead += readBytes;
            }
        }

        public static async Task<byte[]> ReadAsync(this Stream stream, int count, CancellationToken ct = default) {
            var buffer = new byte[count];
            await stream.ReadAsyncEx(buffer, 0, count, ct);
            return buffer;
        }
    }
}