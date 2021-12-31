#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Interfaces;
using PostSharp.Patterns.Model;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.IO {

    [NotifyPropertyChanged]
    public class SerialChannelConfig {
        public string PortName { get; set; }
        public int BaudRate { get; set; }
        public Parity Parity { get; set; }
        public int DataBits { get; set; }
        public StopBits StopBits { get; set; }
        public Handshake Handshake { get; set; }
        public TimeSpan ReadTimeout { get; set; }
        public TimeSpan WriteTimeout { get; set; }

        public static SerialChannelConfig CreateDefaultConfig(string portName) {
            return new SerialChannelConfig() {
                PortName = portName,
                BaudRate = 9600,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = TimeSpan.FromSeconds(2),
                WriteTimeout = TimeSpan.FromSeconds(2)
            };
        }

        public SerialPort CreateSerialPort() {
            return new SerialPort() {
                PortName = PortName,
                BaudRate = BaudRate,
                Parity = Parity,
                DataBits = DataBits,
                StopBits = StopBits,
                Handshake = Handshake,
                ReadTimeout = (int)ReadTimeout.TotalMilliseconds,
                WriteTimeout = (int)WriteTimeout.TotalMilliseconds,
            };
        }
    }

    public class SerialChannel : IChannel {
        private readonly SerialChannelConfig config;
        private readonly SerialPort serialPort;

        public SerialChannel(SerialChannelConfig config) {
            this.config = config;
            this.serialPort = config.CreateSerialPort();
        }

        public bool IsOpen => serialPort.IsOpen;

        public async Task Open(CancellationToken ct) {
            if (serialPort.IsOpen) {
                return;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => tcs.SetCanceled())) {
                var connectTask = Task.Run(() => serialPort.Open(), ct);
                await Task.WhenAny(tcs.Task, connectTask);
                ct.ThrowIfCancellationRequested();
            }
        }

        public async Task Close(CancellationToken ct) {
            if (!serialPort.IsOpen) {
                return;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => tcs.SetCanceled())) {
                var closeTask = Task.Run(() => serialPort.Close(), ct);
                await Task.WhenAny(tcs.Task, closeTask);
                ct.ThrowIfCancellationRequested();
            }
        }

        public Task<byte[]> ReadBytes(int byteCount, CancellationToken ct) {
            return serialPort.ReadAsync(byteCount, ct);
        }

        public Task<byte[]> ReadUntil(string terminator, CancellationToken ct) {
            return serialPort.ReadToAsync(terminator, ct);
        }

        public Task Write(byte[] data, CancellationToken ct) {
            return serialPort.WriteAsync(data, 0, data.Length, ct);
        }

        public void FlushReadExisting() {
            serialPort.ReadExisting();
        }
    }
}