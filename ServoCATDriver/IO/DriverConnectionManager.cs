﻿#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Joko.ServoCAT.Interfaces;
using ASCOM.Utilities;
using Ninject;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.Joko.ServoCAT.IO {

    public partial class DriverConnectionManager : IDriverConnectionManager {
        private readonly AsyncReaderWriterLock readerWriterLock;
        private readonly TraceLogger logger;
        private IChannel activeConnection;
        private readonly IChannelFactory connectionFactory;
        private List<ClientInfo> registeredClients = new List<ClientInfo>();

        public DriverConnectionManager(IChannelFactory connectionFactory, [Named("Telescope")] TraceLogger logger) {
            readerWriterLock = new AsyncReaderWriterLock();
            this.logger = logger;
            this.connectionFactory = connectionFactory;
        }

        public int RegisteredClientCount {
            get {
                using (readerWriterLock.ReaderLock()) {
                    return registeredClients.Count;
                }
            }
        }

        public int ConnectedClientCount {
            get {
                using (readerWriterLock.ReaderLock()) {
                    return registeredClients.Count(c => c.Connected);
                }
            }
        }

        public async Task<IChannel> Connect(Guid guid, CancellationToken ct) {
            using (await readerWriterLock.WriterLockAsync(ct)) {
                var client = registeredClients.SingleOrDefault(c => c.Guid == guid);
                if (client == null) {
                    throw new Exception($"Client {guid} is not registered");
                }

                if (client.Connected) {
                    logger.LogMessage("DriverConnectionManager.Connect", $"Client {guid} is already connected");
                    return GetActiveConnection();
                }

                activeConnection = this.connectionFactory.Create();
                await activeConnection.Open(ct);
                return activeConnection;
            }
        }

        public async Task Disconnect(Guid guid, CancellationToken ct) {
            using (await readerWriterLock.WriterLockAsync(ct)) {
                var client = registeredClients.SingleOrDefault(c => c.Guid == guid);
                if (client == null) {
                    logger.LogMessage("DriverConnectionManager.DisconnectClient", $"Client {guid} isn't registered");
                    return;
                }

                await DisconnectClient(client, ct);
            }
        }

        public Guid RegisterClient() {
            using (readerWriterLock.WriterLock()) {
                var guid = Guid.NewGuid();
                var clientInfo = new ClientInfo(guid);
                registeredClients.Add(clientInfo);
                return guid;
            }
        }

        public async Task UnregisterClient(Guid guid) {
            using (readerWriterLock.WriterLock()) {
                var client = registeredClients.SingleOrDefault(c => c.Guid == guid);
                if (client == null) {
                    logger.LogMessage("DriverConnectionManager.UnregisterClient", $"Client {guid} isn't registered");
                    return;
                }

                await DisconnectClient(client, CancellationToken.None);
                registeredClients.Remove(client);
            }
        }

        private IChannel GetActiveConnection() {
            if (activeConnection == null) {
                throw new Exception("No active connection found, when one was expected");
            }
            return activeConnection;
        }

        private async Task DisconnectClient(ClientInfo client, CancellationToken ct) {
            if (!client.Connected) {
                logger.LogMessage("DriverConnectionManager.DisconnectClient", $"Client {client.Guid} isn't connected. No-op");
                return;
            }

            var numConnections = ConnectedClientCount;
            logger.LogMessage("DriverConnectionManager.DisconnectClient", $"Disconnecting client {client.Guid}. {numConnections} remaining beforehand");
            if (numConnections <= 1) {
                logger.LogMessage("DriverConnectionManager.DisconnectClient", $"No more connections after disconnecting client {client.Guid}. Disconnecting driver");
                await CloseActiveConnection(ct);
            }
            client.Connected = false;
        }

        private async Task CloseActiveConnection(CancellationToken ct) {
            if (activeConnection == null) {
                logger.LogMessage("DriverConnectionManager.CloseActiveConnection", "No active connection to close. Moving on");
                return;
            }

            await activeConnection.Close(ct);
            activeConnection = null;
        }
    }
}