#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.Utilities;
using Ninject;
using PostSharp.Patterns.Model;
using RJCP.IO.Ports;
using System;
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

        public SerialPortStream CreateSerialPort() {
            return new SerialPortStream(
                PortName,
                BaudRate,
                DataBits,
                Parity,
                StopBits);
        }
    }

    public class SerialChannel : IChannel {
        private readonly SerialChannelConfig config;
        private readonly SerialPortStream serialPort;
        private readonly TraceLogger serialLogger;

        public SerialChannel(
            SerialChannelConfig config,
            [Named("Serial")] TraceLogger serialLogger) {
            this.config = config;
            this.serialPort = config.CreateSerialPort();
            this.serialLogger = serialLogger;
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

        public async Task Write(byte[] data, CancellationToken ct) {
            await serialPort.WriteAsync(data, 0, data.Length, ct);
            await serialPort.FlushAsync(ct);
        }

        public void FlushReadExisting() {
            serialPort.DiscardInBuffer();
        }
    }
}