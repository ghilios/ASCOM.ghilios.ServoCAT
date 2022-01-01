#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.Interfaces {

    public interface IDriverConnectionManager {
        int RegisteredClientCount { get; }
        int ConnectedClientCount { get; }

        Task<IChannel> Connect(Guid guid, CancellationToken ct);

        Task Disconnect(Guid guid, CancellationToken ct);

        Guid RegisterClient();

        Task UnregisterClient(Guid guid);

        event EventHandler<ConnectionEventArgs> OnConnected;

        event EventHandler<ConnectionEventArgs> OnDisconnected;
    }

    public class ConnectionEventArgs : EventArgs {
        public Guid ClientGuid { get; set; } = Guid.Empty;
    }
}