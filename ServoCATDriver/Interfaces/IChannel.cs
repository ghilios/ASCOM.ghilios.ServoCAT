#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.Interfaces {

    public interface IChannel {
        bool IsOpen { get; }

        Task Open(CancellationToken ct);

        Task Close(CancellationToken ct);

        void FlushReadExisting();

        Task Write(byte[] data, CancellationToken ct);

        Task<byte[]> ReadBytes(int byteCount, CancellationToken ct);
    }
}