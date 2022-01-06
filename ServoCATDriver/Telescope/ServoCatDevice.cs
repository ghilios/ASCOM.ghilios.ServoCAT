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
using ASCOM.ghilios.ServoCAT.Utility;
using ASCOM.Utilities;
using Ninject;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.Telescope {

    public class ServoCatDevice : BaseINPC, IServoCatDevice, IDisposable {
        private IChannel channel;
        private readonly TraceLogger logger;
        private readonly TraceLogger serialLogger;
        private readonly IServoCatOptions options;
        private readonly AstrometryConverter astrometryConverter;
        private readonly ISharedState sharedState;
        private bool initialized = false;
        private readonly AsyncLock singleRequestMutex = new AsyncLock();
        private FirmwareVersion firmwareVersion;
        private readonly IMicroCache<ExtendedStatusResult> extendedStatusCache;

        public ServoCatDevice(
            IServoCatOptions options,
            IMicroCacheFactory microCacheFactory,
            AstrometryConverter astrometryConverter,
            ISharedState sharedState,
            [Named("Telescope")] TraceLogger logger,
            [Named("Serial")] TraceLogger serialLogger) {
            this.options = options;
            this.astrometryConverter = astrometryConverter;
            this.sharedState = sharedState;
            this.logger = logger;
            this.serialLogger = serialLogger;
            this.extendedStatusCache = microCacheFactory.Create<ExtendedStatusResult>();
        }

        public bool IsConnected {
            get {
                return initialized && channel?.IsOpen == true;
            }
        }

        public async Task Open(IChannel channel, CancellationToken ct) {
            if (initialized && !ReferenceEquals(channel, this.channel)) {
                logger.LogMessage("ServoCatDevice.Open", "Device is already open when Open is called with a different channel. Closing existing connection and opening new one");
                await Close(ct);
            }

            this.channel = channel;
            if (!channel.IsOpen) {
                await channel.Open(ct);
            }

            if (!initialized) {
                initialized = true;
                firmwareVersion = await GetVersion(ct);
                if (!options.FirmwareConfigLoaded) {
                    logger.LogMessage("ServoCatDevice", "Firmware config not previously loaded, so querying the device for it");
                    options.FirmwareConfig.CopyFrom(await GetConfig(ct));
                    options.FirmwareConfigLoaded = true;
                    options.Save();
                }
            }

            RaisePropertyChanged(nameof(IsConnected));
        }

        public async Task Close(CancellationToken ct) {
            initialized = false;
            if (channel.IsOpen) {
                await channel.Close(ct);
            }
            RaisePropertyChanged(nameof(IsConnected));
        }

        private void EnsureChannelOpen() {
            if (!initialized) {
                throw new NotConnectedException("Device not initialized. This is a coding error that should be reported to the developer");
            }

            if (!channel.IsOpen) {
                RaisePropertyChanged(nameof(IsConnected));
                throw new NotConnectedException("Device channel is closed unexpectedly");
            }
        }

        private async Task<T> DoWithRetries<T>(Func<Task<T>> op, CancellationToken ct) {
            int attemptsRemaining = options.DeviceUnexpectedResponseRetries;

            while (true) {
                ct.ThrowIfCancellationRequested();
                try {
                    return await op();
                } catch (UnexpectedResponseException e) {
                    --attemptsRemaining;
                    serialLogger.LogMessage("DoWithRetries", $"Operation failed with {attemptsRemaining} attempts remaining. Unexpected response: {e}.");
                    if (attemptsRemaining == 0) {
                        throw;
                    } else {
                        await Task.Delay(100, ct);
                    }
                }
            }
        }

        public Task<ICRSCoordinates> GetCoordinates(CancellationToken ct) {
            return DoWithRetries(() => GetCoordinatesImpl(ct), ct);
        }

        private async Task<ICRSCoordinates> GetCoordinatesImpl(CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", "GetCoordinates - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

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
        }

        public Task<ExtendedStatusResult> GetExtendedStatus(CancellationToken ct) {
            return this.extendedStatusCache.GetOrAddAsync(
                "",
                () => DoWithRetries(() => GetExtendedStatusImpl(ct), ct),
                options.TelescopeStatusCacheTTL,
                ct);
        }

        private async Task<ExtendedStatusResult> GetExtendedStatusImpl(CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", "GetExtendedStatus - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

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
        }

        public Task<FirmwareVersion> GetVersion(CancellationToken ct) {
            return DoWithRetries(() => GetVersionImpl(ct), ct);
        }

        private async Task<FirmwareVersion> GetVersionImpl(CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", "GetVersion - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

                // Pre-6.1 the v command won't return anything. We will not support such early firmware versions, as we can't reliably run many of the commands
                var response = await SendCommandFixedResponse("v", 5, ct);
                var version = (ushort)GetUIntFromRange(response, 0, 2);
                EnsureCharacter(response, '.', 2);
                var subVersion = (char)response[3];
                EnsureCharacter(response, '\0', 4);
                return new FirmwareVersion() {
                    Version = version,
                    SubVersion = subVersion
                };
            }
        }

        public Task<bool> GotoLegacy(ICRSCoordinates coordinates, CancellationToken ct) {
            return DoWithRetries(() => GotoLegacyImpl(coordinates, ct), ct);
        }

        private async Task<bool> GotoLegacyImpl(ICRSCoordinates coordinates, CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", $"GotoLegacy({coordinates}) - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

                var sign = coordinates.Dec.NonNegative ? "+" : "-";
                var absDec = coordinates.Dec.ToAbsolute();
                var command = FormattableString.Invariant($"g{coordinates.RA.Hours:##.###} {sign}{absDec.Degrees:##.###}");
                var commandBytes = new byte[command.Length + 1];
                Encoding.ASCII.GetBytes(command, 0, command.Length - 1, commandBytes, 0);
                commandBytes[commandBytes.Length - 1] = XORResponse(commandBytes, 1, command.Length - 1);
                if (firmwareVersion.Version > 60) {
                    var response = await SendCommandFixedResponse(commandBytes, 1, ct);
                    // The spec calls for a 'G' response for success, and 'X' for failure
                    if (response[0] == 'G') {
                        return true;
                    } else if (response[0] == 'X') {
                        return false;
                    } else {
                        throw UnexpectedResponseException.ExpectedByteInResponse(response, "G or X", 0);
                    }
                } else {
                    await SendCommandNoResponse(commandBytes, ct);
                    return true;
                }
            }
        }

        public Task<bool> GotoExtendedPrecision(ICRSCoordinates coordinates, CancellationToken ct) {
            return DoWithRetries(() => GotoExtendedPrecisionImpl(coordinates, ct), ct);
        }

        private async Task<bool> GotoExtendedPrecisionImpl(ICRSCoordinates coordinates, CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", $"GotoExtendedPrecision({coordinates}) - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

                var sign = coordinates.Dec.NonNegative ? "+" : "-";
                var absDec = coordinates.Dec.ToAbsolute();
                var command = FormattableString.Invariant($"G{coordinates.RA.Hours:00.00000} {sign}{absDec.Degrees:00.0000}");
                var commandBytes = new byte[command.Length + 1];
                Encoding.ASCII.GetBytes(command, 0, command.Length, commandBytes, 0);
                commandBytes[commandBytes.Length - 1] = XORResponse(commandBytes, 1, commandBytes.Length - 2);
                if (firmwareVersion.Version > 60) {
                    var response = await SendCommandFixedResponse(commandBytes, 1, ct);
                    // The spec calls for a 'G' response for success, and 'X' for failure
                    if (response[0] == 'G') {
                        return true;
                    } else if (response[0] == 'X') {
                        return false;
                    } else {
                        throw UnexpectedResponseException.ExpectedByteInResponse(response, "G or X", 0);
                    }
                } else {
                    await SendCommandNoResponse(commandBytes, ct);
                    return true;
                }
            }
        }

        public Task<bool> AbortMove(CancellationToken ct) {
            return DoWithRetries(() => AbortMoveImpl(ct), ct);
        }

        private async Task<bool> AbortMoveImpl(CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", "AbortMove - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

                var command = "G99.99999 +99.9999";
                var commandBytes = new byte[command.Length + 1];
                Encoding.ASCII.GetBytes(command, 0, command.Length, commandBytes, 0);
                commandBytes[commandBytes.Length - 1] = XORResponse(commandBytes, 1, commandBytes.Length - 2);
                if (firmwareVersion.Version > 60) {
                    var response = await SendCommandFixedResponse(commandBytes, 1, ct);
                    // The spec calls for a 'X' response
                    if (response[0] != 'X') {
                        throw UnexpectedResponseException.ExpectedByteInResponse(response, "X", 0);
                    }
                } else {
                    await SendCommandNoResponse(commandBytes, ct);
                }
                return true;
            }
        }

        public Task<bool> EnableTracking(CancellationToken ct) {
            return DoWithRetries(() => EnableTrackingImpl(ct), ct);
        }

        private async Task<bool> EnableTrackingImpl(CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", "EnableTracking - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

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
        }

        public Task<bool> DisableTracking(Axis axis, CancellationToken ct) {
            return DoWithRetries(() => DisableTrackingImpl(axis, ct), ct);
        }

        private async Task<bool> DisableTrackingImpl(Axis axis, CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", $"DisableTracking({axis}) - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

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
        }

        public Task<bool> Park(CancellationToken ct) {
            return DoWithRetries(() => ParkImpl(ct), ct);
        }

        private async Task<bool> ParkImpl(CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", "Park - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

                if (firmwareVersion.Version > 60) {
                    var response = await SendCommandFixedResponse("P", 1, ct);
                    EnsureCharacter(response, 'P', 0);
                } else {
                    await SendCommandNoResponse("P", ct);
                }
                return true;
            }
        }

        public Task<bool> Unpark(CancellationToken ct) {
            return DoWithRetries(() => UnparkImpl(ct), ct);
        }

        private async Task<bool> UnparkImpl(CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", "Unpark - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

                if (firmwareVersion.Version > 60) {
                    var response = await SendCommandFixedResponse("p", 1, ct);
                    EnsureCharacter(response, 'p', 0);
                } else {
                    await SendCommandNoResponse("p", ct);
                }
                return true;
            }
        }

        public Task<bool> Move(Direction direction, SlewRate rate, CancellationToken ct) {
            return DoWithRetries(() => MoveImpl(direction, rate, ct), ct);
        }

        private async Task<bool> MoveImpl(Direction direction, SlewRate rate, CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", $"Move({direction}, {rate}) - Begin");
            }

            using (await singleRequestMutex.LockAsync(ct)) {
                EnsureChannelOpen();
                await channel.FlushReadExisting(ct);

                var commandBytes = new byte[] { (byte)'M', (byte)direction, (byte)((byte)rate + '0'), 0 };
                commandBytes[3] = (byte)(commandBytes[1] ^ commandBytes[2]);
                if (firmwareVersion.Version > 60) {
                    var response = await SendCommandFixedResponse(commandBytes, 1, ct);
                    // The spec calls for a 'M' response for success, and 'X' for failure
                    if (response[0] == 'M') {
                        return true;
                    } else if (response[0] == 'X') {
                        return false;
                    } else {
                        throw UnexpectedResponseException.ExpectedByteInResponse(response, "M or X", 0);
                    }
                } else {
                    await SendCommandNoResponse(commandBytes, ct);
                    return true;
                }
            }
        }

        private static void GetFirmwareConfigSetting(BinaryReader br, byte expectedDataIndex, Action<short> setter) {
            var dataId = br.ReadByte();
            var expectedDataId = (byte)(expectedDataIndex + 'A');
            if (dataId != expectedDataId) {
                throw new InvalidDataException($"Received unexpected data id {dataId}");
            }

            var value = br.ReadInt16();
            var cr = br.ReadByte();
            if (cr != '\r') {
                throw new InvalidDataException($"Expected carriage return. Got {cr} instead");
            }

            setter(value);
        }

        public async Task<ServoCatFirmwareConfig> GetConfig(CancellationToken ct) {
            if (options.EnableSerialLogging) {
                serialLogger.LogMessage("ServoCatDevice", "GetConfig - Begin");
            }

            EnsureChannelOpen();
            await channel.FlushReadExisting(ct);

            var response = await SendCommandFixedResponse("D", 118, ct);
            using (var ms = new MemoryStream(response, false)) {
                using (var br = new BinaryReader(ms)) {
                    var config = new ServoCatFirmwareConfig();
                    var altitudeSectionReceived = false;
                    var azimuthSectionReceived = false;
                    for (int i = 0; i < 2; ++i) {
                        var configSection = new string(br.ReadChars(2));
                        var configSectionCr = br.ReadChar();
                        if (configSectionCr != '\r') {
                            throw new InvalidDataException($"Expected carriage return after axis name. Got {configSectionCr} instead");
                        }

                        ServoCatFirmwareAxisConfig axisConfig;
                        if (configSection == "AL") {
                            axisConfig = config.AltitudeConfig;
                            altitudeSectionReceived = true;
                        } else if (configSection == "AZ") {
                            axisConfig = config.AzimuthConfig;
                            azimuthSectionReceived = true;
                        } else {
                            throw new InvalidDataException($"Received unexpected data section {configSection}");
                        }

                        GetFirmwareConfigSetting(br, 0, (v) => axisConfig.EncoderResolution = v);
                        GetFirmwareConfigSetting(br, 1, (v) => axisConfig.GearRatioValue1 = v);
                        GetFirmwareConfigSetting(br, 2, (v) => axisConfig.SlewRateValue1_TDPS = v);
                        GetFirmwareConfigSetting(br, 3, (v) => axisConfig.JogRateValue1_AMPS = v);
                        GetFirmwareConfigSetting(br, 4, (v) => axisConfig.GuideRateValue1_ASPS = v);
                        GetFirmwareConfigSetting(br, 5, (v) => axisConfig.SlewRateValue2_TDPS = v);
                        GetFirmwareConfigSetting(br, 6, (v) => axisConfig.JogRateValue2_AMPS = v);
                        GetFirmwareConfigSetting(br, 7, (v) => axisConfig.GuideRateValue2_ASPS = v);
                        GetFirmwareConfigSetting(br, 8, (v) => axisConfig.AccelDecelRateSecs = v);
                        GetFirmwareConfigSetting(br, 9, (v) => axisConfig.BacklashValue = v);
                        GetFirmwareConfigSetting(br, 10, (v) => axisConfig.AxisLimit = v);
                        GetFirmwareConfigSetting(br, 11, (v) => axisConfig.TrackDirectionPositive = v > 0);
                        GetFirmwareConfigSetting(br, 12, (v) => axisConfig.GoToDirectionPositive = v > 0);
                        if (configSection == "AL") {
                            GetFirmwareConfigSetting(br, 13, (v) => config.EasyTrackSignValue = v);
                        } else {
                            GetFirmwareConfigSetting(br, 13, (v) => config.EasyTrackLatitudeValue = v);
                        }
                    }
                    if (!altitudeSectionReceived) {
                        throw new InvalidDataException($"Did not receive AL section");
                    }
                    if (!azimuthSectionReceived) {
                        throw new InvalidDataException($"Did not receive AZ section");
                    }

                    return config;
                }
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

        public void Dispose() {
            if (initialized) {
                AsyncContext.Run(() => Close(CancellationToken.None));
            }
        }
    }
}