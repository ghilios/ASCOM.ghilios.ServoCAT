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

        public static async Task<byte[]> ReadSynchronous(this SerialPort serialPort, int count, CancellationToken ct = default) {
            var bytesToRead = count;
            int totalBytesRead = 0;
            var buffer = new byte[count];

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => tcs.SetCanceled())) {
                while (bytesToRead > 0) {
                    ct.ThrowIfCancellationRequested();
                    if (!serialPort.IsOpen) {
                        throw new EndOfStreamException();
                    }

                    var readTask = Task.Run(() => serialPort.Read(buffer, totalBytesRead, bytesToRead), ct);
                    await Task.WhenAny(tcs.Task, readTask);
                    ct.ThrowIfCancellationRequested();

                    var readBytes = await readTask;
                    if (readBytes == 0) {
                        throw new EndOfStreamException();
                    }
                    bytesToRead -= readBytes;
                    totalBytesRead += readBytes;
                }
            }
            return buffer;
        }

        public static Task WriteAsync(this SerialPort serialPort, byte[] buffer, int offset, int count, CancellationToken ct = default) {
            return serialPort.BaseStream.WriteAsync(buffer, offset, count, ct);
        }

        public static async Task WriteSynchronous(this SerialPort serialPort, byte[] buffer, int offset, int count, CancellationToken ct = default) {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => tcs.SetCanceled())) {
                var writeTask = Task.Run(() => serialPort.Write(buffer, offset, count), ct);
                await Task.WhenAny(tcs.Task, writeTask);
                ct.ThrowIfCancellationRequested();
            }
        }
    }
}