#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Telescope;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.Interfaces {

    [Flags]
    public enum MotionStatusEnum : byte {
        NONE = 0,
        TRACK = 1,
        USER_MOTION = 2,
        GOTO = 4,
        PARK = 8,
        ALIGN = 16
    }

    public struct ExtendedStatusResult {
        public ICRSCoordinates Coordinates { get; set; }
        public MotionStatusEnum MotionStatus { get; set; }
    }

    public struct FirmwareVersion {

        public static FirmwareVersion GetDefault() {
            return new FirmwareVersion() {
                Version = 60,
                SubVersion = '_'
            };
        }

        public ushort Version { get; set; }
        public char SubVersion { get; set; }

        public override string ToString() {
            return $"{Version:00}.{SubVersion}";
        }
    }

    [Flags]
    public enum Axis : byte {
        NONE = 0,
        AZ = 1,
        ALT = 2,
        BOTH = AZ | ALT
    }

    public enum Direction : byte {
        North = (byte)'N',
        South = (byte)'S',
        East = (byte)'E',
        West = (byte)'W'
    }

    public enum SlewRate : byte {
        STOP = 0,
        GUIDE_SLOW = 1,
        GUIDE_FAST = 2,
        JOG = 3,
        SLEW = 4
    }

    public interface IServoCatDevice {
        bool IsConnected { get; }

        Task Close(CancellationToken ct);

        Task Open(IChannel channel, CancellationToken ct);

        Task<ICRSCoordinates> GetCoordinates(CancellationToken ct);

        Task<ExtendedStatusResult> GetExtendedStatus(CancellationToken ct);

        Task<FirmwareVersion> GetVersion(CancellationToken ct);

        Task<bool> GotoLegacy(ICRSCoordinates coordinates, CancellationToken ct);

        Task<bool> GotoExtendedPrecision(ICRSCoordinates coordinates, CancellationToken ct);

        Task<bool> EnableTracking(CancellationToken ct);

        Task<bool> DisableTracking(Axis axis, CancellationToken ct);

        Task<bool> Park(CancellationToken ct);

        Task<bool> Unpark(CancellationToken ct);

        Task<bool> Move(Direction direction, SlewRate rate, CancellationToken ct);

        Task<ServoCatFirmwareConfig> GetConfig(CancellationToken ct);
    }
}