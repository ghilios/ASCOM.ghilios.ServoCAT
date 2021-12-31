#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Exceptions;
using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.Utilities;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.Telescope {

    public class ServoCatDevice : IServoCatDevice {
        private readonly IChannel channel;
        private readonly IServoCatOptions options;
        private readonly AstrometryConverter astrometryConverter;
        private bool initialized = false;
        private FirmwareVersion firmwareVersion;

        public ServoCatDevice(
            IChannel channel,
            IServoCatOptions options,
            AstrometryConverter astrometryConverter,
            TraceLogger logger) {
            this.channel = channel;
            this.options = options;
            this.astrometryConverter = astrometryConverter;
        }

        public async Task Initialize(CancellationToken ct) {
            if (!channel.IsOpen) {
                await channel.Open(ct);
            }

            initialized = true;
            firmwareVersion = await GetVersion(ct);
        }

        private void EnsureChannelOpen() {
            if (!initialized) {
                throw new Exception("Device not initialized. This is a coding error that should be reported to the developer");
            }

            if (!channel.IsOpen) {
                throw new Exception("Device channel is closed unexpectedly");
            }
        }

        public async Task<ICRSCoordinates> GetCoordinates(CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            var response = await SendCommandFixedResponse(new byte[] { 0x0D }, 16, ct);
            EnsureCharacter(response, ' ', 0);
            var raHoursWhole = GetUIntFromRange(response, 1, 2);
            EnsureCharacter(response, '.', 3);
            var raHoursFractional = GetUIntFromRange(response, 4, 3);
            EnsureCharacter(response, ' ', 7);
            var sign = GetSignPositive(response, 8);
            var decDegreesWhole = GetUIntFromRange(response, 9, 2);
            EnsureCharacter(response, '.', 11);
            var decDegreesFractional = GetUIntFromRange(response, 12, 3);
            EnsureCharacter(response, '\0', 15);

            var ra = Angle.ByHours((double)raHoursWhole + (double)raHoursFractional / 1000.0d);
            var dec = Angle.ByDegree(((double)decDegreesWhole + (double)decDegreesFractional / 1000.0d) * (sign ? 1 : -1));
            return new ICRSCoordinates(ra, dec, Epoch.J2000);
        }

        public async Task<ExtendedStatusResult> GetExtendedStatus(CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            var response = await SendCommandFixedResponse(new byte[] { 0x0E }, 20, ct);
            var expectedXor = response[19];
            var actualXor = XORResponse(response, 1, 18);
            if (expectedXor != actualXor) {
                throw UnexpectedResponseException.XORValidationFailed(response, 1, 18, actualXor, expectedXor);
            }

            EnsureCharacter(response, ' ', 0);
            var raHoursWhole = GetUIntFromRange(response, 1, 2);
            EnsureCharacter(response, '.', 3);
            var raHoursFractional = GetUIntFromRange(response, 4, 5);
            EnsureCharacter(response, ' ', 9);
            var sign = GetSignPositive(response, 10);
            var decDegreesWhole = GetUIntFromRange(response, 11, 2);
            EnsureCharacter(response, '.', 13);
            var decDegreesFractional = GetUIntFromRange(response, 14, 4);

            var motionStatus = (MotionStatusEnum)response[18];
            var ra = Angle.ByHours((double)raHoursWhole + (double)raHoursFractional / 100000.0d);
            var dec = Angle.ByDegree(((double)decDegreesWhole + (double)decDegreesFractional / 10000.0d) * (sign ? 1 : -1));
            var coordinates = new ICRSCoordinates(ra, dec, Epoch.J2000);
            return new ExtendedStatusResult() {
                Coordinates = coordinates,
                MotionStatus = motionStatus
            };
        }

        public async Task<FirmwareVersion> GetVersion(CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            // Pre-6.1 the v command won't return anything, so if we don't get a response within 1 second treat it as version 60._
            var response = await SendCommandMaybeFixedResponse("v", 5, ct, TimeSpan.FromSeconds(1));
            if (response.Length == 0) {
                return FirmwareVersion.GetDefault();
            }

            var version = (ushort)GetUIntFromRange(response, 0, 2);
            EnsureCharacter(response, '.', 2);
            var subVersion = (char)response[3];
            EnsureCharacter(response, '\0', 4);
            return new FirmwareVersion() {
                Version = version,
                SubVersion = subVersion
            };
        }

        public async Task<bool> GotoLegacy(ICRSCoordinates coordinates, CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            var sign = coordinates.Dec.NonNegative ? "+" : "-";
            var absDec = coordinates.Dec.ToAbsolute();
            var command = $"g{coordinates.RA.Hours:00.000} {sign}{absDec.Degrees:00.000}";
            var commandBytes = new byte[command.Length + 1];
            Encoding.ASCII.GetBytes(command, 0, command.Length - 1, commandBytes, 0);
            commandBytes[commandBytes.Length - 1] = XORResponse(commandBytes, 1, command.Length - 1);
            if (firmwareVersion.Version > 60) {
                var response = await SendCommandFixedResponse(commandBytes, 1, ct);
                // The spec calls for a 'G' response for success, and 'X' for failure
                return response[0] == 'G';
            } else {
                await SendCommandNoResponse(commandBytes, ct);
                return true;
            }
        }

        public async Task<bool> GotoExtendedPrecision(ICRSCoordinates coordinates, CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            var sign = coordinates.Dec.NonNegative ? "+" : "-";
            var absDec = coordinates.Dec.ToAbsolute();
            var command = $"G{coordinates.RA.Hours:00.00000} {sign}{absDec.Degrees:00.0000}";
            var commandBytes = new byte[command.Length + 1];
            Encoding.ASCII.GetBytes(command, 0, command.Length - 1, commandBytes, 0);
            commandBytes[commandBytes.Length - 1] = XORResponse(commandBytes, 1, command.Length - 1);
            if (firmwareVersion.Version > 60) {
                var response = await SendCommandFixedResponse(commandBytes, 1, ct);
                // The spec calls for a 'G' response for success, and 'X' for failure
                return response[0] == 'G';
            } else {
                await SendCommandNoResponse(commandBytes, ct);
                return true;
            }
        }

        public async Task<bool> EnableTracking(CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            if (firmwareVersion.Version > 60) {
                var response = await SendCommandFixedResponse("RI", 1, ct);
                if (response[0] != 'I') {
                    return false;
                }
            } else {
                await SendCommandNoResponse("RI", ct);
            }
            return true;
        }

        public async Task<bool> DisableTracking(Axis axis, CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            string command;
            if (axis == Axis.ALT) {
                command = "RE";
            } else if (axis == Axis.AZ) {
                command = "RZ";
            } else if (axis == Axis.BOTH) {
                command = "RF";
            } else {
                throw new ArgumentException($"Unexpected Axis {axis}");
            }

            if (firmwareVersion.Version > 60) {
                var response = await SendCommandFixedResponse(command, 1, ct);
                var expectedResponse = command[1];
                if (response[0] != expectedResponse) {
                    return false;
                }
            } else {
                await SendCommandNoResponse("RI", ct);
            }
            return true;
        }

        public async Task<bool> Park(CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            if (firmwareVersion.Version > 60) {
                var response = await SendCommandFixedResponse("P", 1, ct);
                EnsureCharacter(response, 'P', 0);
            } else {
                await SendCommandNoResponse("P", ct);
            }
            return true;
        }

        public async Task<bool> Unpark(CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            if (firmwareVersion.Version > 60) {
                var response = await SendCommandFixedResponse("p", 1, ct);
                EnsureCharacter(response, 'p', 0);
            } else {
                await SendCommandNoResponse("p", ct);
            }
            return true;
        }

        public async Task<bool> Move(Direction direction, SlewRate rate, CancellationToken ct) {
            EnsureChannelOpen();
            channel.FlushReadExisting();

            var commandBytes = new byte[] { (byte)'M', (byte)direction, (byte)rate, 0 };
            commandBytes[3] = (byte)(commandBytes[1] ^ commandBytes[2]);
            if (firmwareVersion.Version > 60) {
                var response = await SendCommandFixedResponse(commandBytes, 1, ct);
                // The spec calls for a 'M' response for success, and 'X' for failure
                return response[0] == 'M';
            } else {
                await SendCommandNoResponse(commandBytes, ct);
                return true;
            }
        }

        private Task SendCommandNoResponse(string command, CancellationToken ct) {
            var commandBytes = Encoding.ASCII.GetBytes(command);
            return SendCommandNoResponse(commandBytes, ct);
        }

        private async Task SendCommandNoResponse(byte[] commandBytes, CancellationToken ct) {
            await this.channel.Write(commandBytes, ct);
        }

        private Task<byte[]> SendCommandFixedResponse(string command, int responseBytes, CancellationToken ct) {
            var commandBytes = Encoding.ASCII.GetBytes(command);
            return SendCommandFixedResponse(commandBytes, responseBytes, ct);
        }

        private async Task<byte[]> SendCommandFixedResponse(byte[] commandBytes, int responseBytes, CancellationToken ct) {
            await this.channel.Write(commandBytes, ct);
            ct.ThrowIfCancellationRequested();
            return await this.channel.ReadBytes(responseBytes, ct);
        }

        private async Task<byte[]> SendCommandMaybeFixedResponse(string command, int responseBytes, CancellationToken ct, TimeSpan timetoWait) {
            var commandBytes = Encoding.ASCII.GetBytes(command);
            await this.channel.Write(commandBytes, ct);
            ct.ThrowIfCancellationRequested();

            var waitTimeCts = new CancellationTokenSource(timetoWait);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(waitTimeCts.Token, ct);
            try {
                return await this.channel.ReadBytes(responseBytes, linkedCts.Token);
            } catch (OperationCanceledException) {
                if (waitTimeCts.IsCancellationRequested) {
                    return new byte[0];
                }
                throw;
            }
        }

        private static void EnsureCharacter(byte[] response, char expectedCharacter, int offset) {
            var actualByte = response[offset];
            if (actualByte != expectedCharacter) {
                throw UnexpectedResponseException.ExpectedByteInResponse(response, $"{expectedCharacter}", offset);
            }
        }

        private static bool GetSignPositive(byte[] response, int offset) {
            var responseByte = response[offset];
            if (responseByte == '+') {
                return true;
            } else if (responseByte == '-') {
                return false;
            }
            throw UnexpectedResponseException.ExpectedByteInResponse(response, "+ or -", offset);
        }

        private static uint GetUIntFromRange(byte[] response, int startOffset, int length) {
            var responseSubString = Encoding.ASCII.GetString(response, startOffset, length);
            if (uint.TryParse(responseSubString, out var result)) {
                return result;
            }
            throw UnexpectedResponseException.ExpectedIntInResponse(response, startOffset, length);
        }

        private static byte XORResponse(byte[] response, int startOffset, int length) {
            byte result = 0;
            for (int i = startOffset; i < startOffset + length; ++i) {
                result = (byte)(result ^ response[i]);
            }
            return result;
        }
    }
}