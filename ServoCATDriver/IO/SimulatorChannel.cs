#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.DeviceInterface;
using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.ghilios.ServoCAT.Telescope;
using ASCOM.Utilities;
using Ninject;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.IO {

    public class SimulatorChannel : IChannel {
        private readonly ServoCatFirmwareConfig firmwareConfig;

        private readonly IServoCatOptions options;
        private readonly DriverAccess.Telescope simulatorTelescope;
        private readonly MemoryQueueBufferStream memoryStream;
        private readonly AstrometryConverter astrometryConverter;
        private readonly TraceLogger logger;
        private bool lastMoveIsSlew;

        private Axis trackingState;

        public SimulatorChannel(IServoCatOptions options, AstrometryConverter astrometryConverter, [Named("Telescope")] TraceLogger logger) {
            this.options = options;
            this.astrometryConverter = astrometryConverter;
            this.logger = logger;
            simulatorTelescope = new DriverAccess.Telescope("ASCOM.Simulator.Telescope");
            simulatorTelescope.SiteLatitude = options.Latitude;
            simulatorTelescope.SiteLongitude = options.Longitude;
            simulatorTelescope.SiteElevation = options.Elevation;

            memoryStream = new MemoryQueueBufferStream();
            trackingState = Axis.NONE;
            lastMoveIsSlew = true;
            firmwareConfig = new ServoCatFirmwareConfig(
                azimuthConfig: new ServoCatFirmwareAxisConfig() {
                    EncoderResolution = 2000,
                    GearRatioValue1 = 7542,
                    SlewRateValue1_TDPS = 55,
                    JogRateValue1_AMPS = 140,
                    GuideRateValue1_ASPS = 290,
                    SlewRateValue2_TDPS = 45,
                    JogRateValue2_AMPS = 120,
                    GuideRateValue2_ASPS = 200,
                    AccelDecelRateSecs = 4,
                    BacklashValue = 2,
                    AxisLimit = 0,
                    TrackDirectionPositive = true
                },
                altitudeConfig: new ServoCatFirmwareAxisConfig() {
                    EncoderResolution = 2000,
                    GearRatioValue1 = 9873,
                    SlewRateValue1_TDPS = 50,
                    JogRateValue1_AMPS = 140,
                    GuideRateValue1_ASPS = 290,
                    SlewRateValue2_TDPS = 40,
                    JogRateValue2_AMPS = 120,
                    GuideRateValue2_ASPS = 200,
                    AccelDecelRateSecs = 4,
                    BacklashValue = 1120,
                    AxisLimit = 70,
                    TrackDirectionPositive = false
                }) {
                EasyTrackLatitudeValue = 17500,
                EasyTrackSignValue = 3
            };
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
            trackingState = Axis.NONE;
            lastMoveIsSlew = true;
            return Task.CompletedTask;
        }

        public Task<byte[]> ReadBytes(int byteCount, CancellationToken ct) {
            return memoryStream.ReadAsync(byteCount, ct);
        }

        public Task<byte[]> ReadUntil(string terminator, CancellationToken ct) {
            throw new System.NotImplementedException();
        }

        public async Task Write(byte[] data, CancellationToken ct) {
            if (CommandMatch(data, new byte[] { 0x0D })) {
                await GetCoordinatesRequest();
            } else if (CommandMatch(data, new byte[] { 0x0E })) {
                await GetExtendedStatusRequest();
            } else if (CommandMatch(data, "v")) {
                await VersionRequest();
            } else if (CommandStartsWith(data, "M")) {
                await MoveRequest(data);
            } else if (CommandStartsWith(data, "g")) {
                await GotoLegacyRequest(data);
            } else if (CommandStartsWith(data, "G")) {
                await GotoExtendedPrecisionRequest(data);
            } else if (CommandMatch(data, "RI")) {
                await EnableTracking();
            } else if (CommandMatch(data, "RF")) {
                await DisableTracking(Axis.BOTH, 'F');
            } else if (CommandMatch(data, "RZ")) {
                await DisableTracking(Axis.AZ, 'Z');
            } else if (CommandMatch(data, "RE")) {
                await DisableTracking(Axis.ALT, 'E');
            } else if (CommandMatch(data, "P")) {
                await Park();
            } else if (CommandMatch(data, "p")) {
                await Unpark();
            } else if (CommandMatch(data, "D")) {
                await GetFirmwareConfig();
            }
        }

        private ICRSCoordinates GetCoordinates() {
            var ra = Angle.ByHours(simulatorTelescope.RightAscension);
            var dec = Angle.ByDegree(simulatorTelescope.Declination);
            var epoch = simulatorTelescope.EquatorialSystem == DeviceInterface.EquatorialCoordinateType.equTopocentric ? Epoch.JNOW : Epoch.J2000;
            var icrsCoordinates = new ICRSCoordinates(ra: ra, dec: dec, epoch: epoch);
            return astrometryConverter.TransformEpoch(icrsCoordinates, Epoch.J2000);
        }

        private async Task GetCoordinatesRequest() {
            var tranformedIcrsCoordinates = GetCoordinates();
            var sign = tranformedIcrsCoordinates.Dec.NonNegative ? '+' : '-';
            var response = FormattableString.Invariant($" {tranformedIcrsCoordinates.RA.Hours:00.000} {sign}{tranformedIcrsCoordinates.Dec.ToAbsolute().Degrees:00.000}\0");
            await WriteResponse(response);
        }

        private async Task GetExtendedStatusRequest() {
            var tranformedIcrsCoordinates = GetCoordinates();
            var sign = tranformedIcrsCoordinates.Dec.NonNegative ? '+' : '-';
            var motionState = MotionStatusEnum.NONE;
            if (simulatorTelescope.Tracking) {
                motionState |= MotionStatusEnum.TRACK;
            }
            if (simulatorTelescope.Slewing) {
                if (lastMoveIsSlew) {
                    motionState |= MotionStatusEnum.GOTO;
                } else {
                    motionState |= MotionStatusEnum.USER_MOTION;
                }
            }
            if (simulatorTelescope.AtPark) {
                motionState |= MotionStatusEnum.PARK;
            }
            if (options.SimulatorAligned) {
                motionState |= MotionStatusEnum.ALIGN;
            }

            var response = FormattableString.Invariant($" {tranformedIcrsCoordinates.RA.Hours:00.00000} {sign}{tranformedIcrsCoordinates.Dec.ToAbsolute().Degrees:00.0000}");
            var responseBytes = new byte[20];
            Encoding.ASCII.GetBytes(response, 0, response.Length, responseBytes, 0);
            responseBytes[18] = (byte)motionState;
            responseBytes[19] = XORResponse(responseBytes, 1, 18);

            if (!options.SimulatorAligned) {
                // If not aligned, it can take up to 0.5 seconds to respond
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            await WriteResponse(responseBytes);
        }

        private async Task MoveRequest(byte[] request) {
            if (request.Length != 4) {
                logger.LogMessage("MoveRequest", $"Failed - Invalid length. Expected {4} got {request.Length}");
            }
            var expectedXOR = request[1] ^ request[2];
            var actualXOR = request[3];
            if (expectedXOR != actualXOR) {
                logger.LogMessage("MoveRequest", "Failed - XOR validation");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                await WriteResponse(new byte[] { (byte)'X' });
                return;
            }

            var directionValue = request[1];
            TelescopeAxes axis;
            var positive = true;
            ServoCatFirmwareAxisConfig axisConfig;
            if (directionValue == 'N') {
                axis = TelescopeAxes.axisSecondary;
                axisConfig = options.FirmwareConfig.AltitudeConfig;
            } else if (directionValue == 'S') {
                axis = TelescopeAxes.axisSecondary;
                positive = false;
                axisConfig = options.FirmwareConfig.AltitudeConfig;
            } else if (directionValue == 'E') {
                axis = TelescopeAxes.axisPrimary;
                axisConfig = options.FirmwareConfig.AzimuthConfig;
            } else if (directionValue == 'W') {
                axis = TelescopeAxes.axisPrimary;
                positive = false;
                axisConfig = options.FirmwareConfig.AzimuthConfig;
            } else {
                logger.LogMessage("MoveRequest", $"Failed - Unexpected direction {directionValue}");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                await WriteResponse(new byte[] { (byte)'X' });
                return;
            }

            var rateValue = request[2];
            if (rateValue < 0 || rateValue > 4) {
                logger.LogMessage("MoveRequest", $"Failed - Unexpected rate {rateValue}");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                await WriteResponse(new byte[] { (byte)'X' });
                return;
            }

            double[] rates;
            if (options.UseSpeed1) {
                rates = new double[] { 0.0d, axisConfig.GuideRatePerSecond1.Degrees / 2.0d, axisConfig.GuideRatePerSecond1.Degrees, axisConfig.JogRatePerSecond1.Degrees, axisConfig.SlewRatePerSecond1.Degrees };
            } else {
                rates = new double[] { 0.0d, axisConfig.GuideRatePerSecond2.Degrees / 2.0d, axisConfig.GuideRatePerSecond2.Degrees, axisConfig.JogRatePerSecond2.Degrees, axisConfig.SlewRatePerSecond2.Degrees };
            }

            var rate = positive ? rates[rateValue] : -rates[rateValue];
            simulatorTelescope.MoveAxis(axis, rate);

            await Task.Delay(TimeSpan.FromMilliseconds(500));
            await WriteResponse(new byte[] { (byte)'M' });
        }

        private async Task GotoExtendedPrecisionRequest(byte[] request) {
            var expectedXOR = XORResponse(request, 1, request.Length - 2);
            var actualXOR = request[18];
            if (expectedXOR != actualXOR) {
                logger.LogMessage("GotoExtendedPrecisionRequest", "Failed - XOR validation");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                await WriteResponse(new byte[] { (byte)'X' });
                return;
            }

            const string abortMoveCommand = "G99.99999 +99.9999";
            try {
                var requestString = Encoding.ASCII.GetString(request);
                if (requestString.Substring(0, abortMoveCommand.Length) == abortMoveCommand) {
                    simulatorTelescope.AbortSlew();
                    return;
                }

                var raHours = double.Parse(requestString.Substring(1, 8));
                var sign = requestString[10] == '+' ? 1 : -1;
                var decDegrees = double.Parse(requestString.Substring(11, 7)) * sign;
                simulatorTelescope.SlewToCoordinatesAsync(RightAscension: raHours, Declination: decDegrees);
            } catch (Exception e) {
                logger.LogMessage("GotoExtendedPrecisionRequest", $"Failed - {e.Message}");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                await WriteResponse(new byte[] { (byte)'X' });
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
            await WriteResponse(new byte[] { (byte)'G' });
        }

        private async Task GotoLegacyRequest(byte[] request) {
            var expectedXOR = XORResponse(request, 1, request.Length - 2);
            var actualXOR = request[15];
            if (expectedXOR != actualXOR) {
                logger.LogMessage("GotoLegacyRequest", "Failed - XOR validation");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                await WriteResponse(new byte[] { (byte)'X' });
                return;
            }

            try {
                var requestString = Encoding.ASCII.GetString(request);
                var raHours = double.Parse(requestString.Substring(1, 6));
                var sign = requestString[8] == '+' ? 1 : -1;
                var decDegrees = double.Parse(requestString.Substring(9, 6)) * sign;
                simulatorTelescope.SlewToCoordinatesAsync(RightAscension: raHours, Declination: decDegrees);
            } catch (Exception e) {
                logger.LogMessage("GotoExtendedPrecisionRequest", $"Failed - {e.Message}");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                await WriteResponse(new byte[] { (byte)'X' });
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
            await WriteResponse(new byte[] { (byte)'g' });
        }

        private async Task Unpark() {
            var success = false;
            try {
                simulatorTelescope.Unpark();
                success = true;
            } catch (Exception) {
            }

            if (options.SimulatorVersion.SubVersion > 60 && success) {
                await WriteResponse("p");
            }
        }

        private async Task Park() {
            var success = false;
            try {
                // SC doesn't wait for park to complete before returning
                _ = Task.Run(() => simulatorTelescope.Park());
                success = true;
            } catch (Exception) {
            }

            if (options.SimulatorVersion.SubVersion > 60 && success) {
                await WriteResponse("P");
            }
        }

        private async Task DisableTracking(Axis disableAxes, char response) {
            var success = false;
            try {
                trackingState = trackingState & ~disableAxes;
                if (trackingState == Axis.NONE) {
                    simulatorTelescope.Tracking = false;
                }
                success = true;
            } catch (Exception) {
            }

            if (options.SimulatorVersion.SubVersion > 60 && success) {
                await WriteResponse($"{response}");
            }
        }

        private async Task EnableTracking() {
            var success = false;
            try {
                simulatorTelescope.Tracking = true;
                success = true;
            } catch (Exception) {
            }

            if (options.SimulatorVersion.SubVersion > 60 && success) {
                await WriteResponse("I");
            }
        }

        private async Task VersionRequest() {
            var version = options.SimulatorVersion;
            var response = $"{version.Version:00}.{version.SubVersion}\0";
            if (options.SimulatorVersion.SubVersion > 60) {
                await WriteResponse(response);
            }
        }

        private static void WriteFirmwareSetting(BinaryWriter bw, byte dataId, short value) {
            bw.Write((byte)(dataId + 'A'));
            bw.Write(value);
            bw.Write('\r');
        }

        private async Task GetFirmwareConfig() {
            using (var ms = new MemoryStream()) {
                using (var bw = new BinaryWriter(ms)) {
                    bw.Write("AZ\r".ToCharArray());
                    WriteFirmwareSetting(bw, 0, firmwareConfig.AzimuthConfig.EncoderResolution);
                    WriteFirmwareSetting(bw, 1, firmwareConfig.AzimuthConfig.GearRatioValue1);
                    WriteFirmwareSetting(bw, 2, firmwareConfig.AzimuthConfig.SlewRateValue1_TDPS);
                    WriteFirmwareSetting(bw, 3, firmwareConfig.AzimuthConfig.JogRateValue1_AMPS);
                    WriteFirmwareSetting(bw, 4, firmwareConfig.AzimuthConfig.GuideRateValue1_ASPS);
                    WriteFirmwareSetting(bw, 5, firmwareConfig.AzimuthConfig.SlewRateValue2_TDPS);
                    WriteFirmwareSetting(bw, 6, firmwareConfig.AzimuthConfig.JogRateValue2_AMPS);
                    WriteFirmwareSetting(bw, 7, firmwareConfig.AzimuthConfig.GuideRateValue2_ASPS);
                    WriteFirmwareSetting(bw, 8, firmwareConfig.AzimuthConfig.AccelDecelRateSecs);
                    WriteFirmwareSetting(bw, 9, firmwareConfig.AzimuthConfig.BacklashValue);
                    WriteFirmwareSetting(bw, 10, firmwareConfig.AzimuthConfig.AxisLimit);
                    WriteFirmwareSetting(bw, 11, firmwareConfig.AzimuthConfig.TrackDirectionPositive ? (short)1 : (short)0);
                    WriteFirmwareSetting(bw, 12, firmwareConfig.AzimuthConfig.GoToDirectionPositive ? (short)1 : (short)0);
                    WriteFirmwareSetting(bw, 13, firmwareConfig.EasyTrackLatitudeValue);

                    bw.Write("AL\r".ToCharArray());
                    WriteFirmwareSetting(bw, 0, firmwareConfig.AltitudeConfig.EncoderResolution);
                    WriteFirmwareSetting(bw, 1, firmwareConfig.AltitudeConfig.GearRatioValue1);
                    WriteFirmwareSetting(bw, 2, firmwareConfig.AltitudeConfig.SlewRateValue1_TDPS);
                    WriteFirmwareSetting(bw, 3, firmwareConfig.AltitudeConfig.JogRateValue1_AMPS);
                    WriteFirmwareSetting(bw, 4, firmwareConfig.AltitudeConfig.GuideRateValue1_ASPS);
                    WriteFirmwareSetting(bw, 5, firmwareConfig.AltitudeConfig.SlewRateValue2_TDPS);
                    WriteFirmwareSetting(bw, 6, firmwareConfig.AltitudeConfig.JogRateValue2_AMPS);
                    WriteFirmwareSetting(bw, 7, firmwareConfig.AltitudeConfig.GuideRateValue2_ASPS);
                    WriteFirmwareSetting(bw, 8, firmwareConfig.AltitudeConfig.AccelDecelRateSecs);
                    WriteFirmwareSetting(bw, 9, firmwareConfig.AltitudeConfig.BacklashValue);
                    WriteFirmwareSetting(bw, 10, firmwareConfig.AltitudeConfig.AxisLimit);
                    WriteFirmwareSetting(bw, 11, firmwareConfig.AltitudeConfig.TrackDirectionPositive ? (short)1 : (short)0);
                    WriteFirmwareSetting(bw, 12, firmwareConfig.AltitudeConfig.GoToDirectionPositive ? (short)1 : (short)0);
                    WriteFirmwareSetting(bw, 13, firmwareConfig.EasyTrackSignValue);
                }

                var responseBytes = ms.ToArray();
                await WriteResponse(responseBytes);
            }
        }

        private async Task WriteResponse(string response) {
            var responseBytes = Encoding.ASCII.GetBytes(response);
            await WriteResponse(responseBytes);
        }

        private async Task WriteResponse(byte[] responseBytes) {
            await memoryStream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static bool CommandMatch(byte[] data, string command) {
            var commandBytes = Encoding.ASCII.GetBytes(command);
            return CommandMatch(data, commandBytes);
        }

        private static bool CommandMatch(byte[] data, byte[] commandBytes) {
            return ByteArrayEqual(data, commandBytes);
        }

        private static bool CommandStartsWith(byte[] data, string command) {
            var commandBytes = Encoding.ASCII.GetBytes(command);
            if (data.Length < command.Length) {
                return false;
            }
            return ByteArrayEqual(data.AsSpan(0, commandBytes.Length), commandBytes);
        }

        private static bool ByteArrayEqual(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2) {
            return a1.SequenceEqual(a2);
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