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
using System;
using System.IO.Ports;
using System.Text;
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
                ReadTimeout = TimeSpan.FromSeconds(10),
                WriteTimeout = TimeSpan.FromSeconds(10),
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
                WriteTimeout = (int)WriteTimeout.TotalMilliseconds
            };
        }
    }

    public class SerialChannel : IChannel {
        private readonly SerialChannelConfig config;
        private readonly IServoCatOptions options;
        private readonly SerialPort serialPort;
        private readonly TraceLogger serialLogger;

        public SerialChannel(
            SerialChannelConfig config,
            IServoCatOptions options,
            [Named("Serial")] TraceLogger serialLogger) {
            this.config = config;
            this.options = options;
            this.serialPort = config.CreateSerialPort();
            this.serialLogger = serialLogger;
        }

        public bool IsOpen => serialPort.IsOpen;

        public async Task Open(CancellationToken ct) {
            if (serialPort.IsOpen) {
                if (options.EnableSerialLogging) {
                    serialLogger.LogMessage("Open", $"Channel already open");
                }
                return;
            }

            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("Open", $"Opening channel");
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => tcs.SetCanceled())) {
                var connectTask = Task.Run(() => serialPort.Open(), ct);
                await Task.WhenAny(tcs.Task, connectTask);
                ct.ThrowIfCancellationRequested();

                await connectTask;
            }
        }

        public async Task Close(CancellationToken ct) {
            if (!serialPort.IsOpen) {
                if (options.EnableSerialLogging) {
                    serialLogger.LogMessage("Close", $"Channel already closed");
                }
                return;
            }

            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("Close", $"Closing channel");
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => tcs.SetCanceled())) {
                var closeTask = Task.Run(() => serialPort.Close(), ct);
                await Task.WhenAny(tcs.Task, closeTask);
                ct.ThrowIfCancellationRequested();
            }
        }

        public async Task<byte[]> ReadBytes(int byteCount, CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ReadBytes", $"Begin reading {byteCount} bytes");
            }

            var data = await serialPort.ReadSynchronous(byteCount, ct);
            if (options.EnableSerialLogging) {
                var dataString = BitConverter.ToString(data);
                var dataASCIIString = Encoding.ASCII.GetString(data);
                serialLogger.LogMessage("ReadBytes", $"Read {data.Length} bytes. {dataString} = {dataASCIIString}");
            }
            return data;
        }

        public async Task Write(byte[] data, CancellationToken ct) {
            if (options.EnableSerialLogging) {
                var dataString = BitConverter.ToString(data);
                var dataASCIIString = Encoding.ASCII.GetString(data);
                serialLogger.LogMessage("Write", $"Writing {data.Length} bytes. {dataString} = {dataASCIIString}");
            }

            await serialPort.WriteSynchronous(data, 0, data.Length, ct);
        }

        public async Task<byte[]> FlushReadExisting(CancellationToken ct) {
            var bytesInBuffer = serialPort.BytesToRead;
            byte[] discardBuffer;
            if (bytesInBuffer > 0) {
                discardBuffer = await serialPort.ReadSynchronous(bytesInBuffer, ct);
                if (options.EnableSerialLogging) {
                    var discardBufferString = BitConverter.ToString(discardBuffer);
                    var discardBufferASCIIString = Encoding.ASCII.GetString(discardBuffer);
                    serialLogger.LogMessage("FlushReadExisting", $"Discarded {bytesInBuffer} bytes. {discardBufferString} = {discardBufferASCIIString}");
                }
            } else {
                discardBuffer = new byte[0];
            }
            serialPort.DiscardInBuffer();
            return discardBuffer;
        }
    }
}