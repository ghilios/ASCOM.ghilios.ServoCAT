#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.ghilios.ServoCAT.Utility;
using ASCOM.Utilities;
using Ninject;
using Nito.AsyncEx;
using PostSharp.Patterns.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ASCOM.ghilios.ServoCAT.ViewModel {

    [NotifyPropertyChanged]
    public class MainVM : BaseVM, IMainVM {
        private readonly TraceLogger Logger;
        private readonly ISerialUtilities serialUtilities;
        private readonly IDriverConnectionManager driverConnectionManager;
        private readonly AstrometryConverter astrometryConverter;
        private readonly Guid deviceClientId;
        private IServoCatDevice device;
        private CancellationTokenSource deviceCts;
        private Task pollTask;
        private object connectedClientsLock = new object();
        private HashSet<Guid> connectedClients = new HashSet<Guid>();

        public MainVM(
            IServoCatOptions servoCatOptions,
            IServoCatDevice device,
            IDriverConnectionManager driverConnectionManager,
            ISharedState sharedState,
            AstrometryConverter astrometryConverter,
            [Named("Server")] TraceLogger logger,
            ISerialUtilities serialUtilities) {
            this.ServoCatOptions = servoCatOptions;
            this.Logger = logger;
            this.SetupCommand = new RelayCommand(OpenSetupDialog);
            this.ToggleConnectCommand = new RelayCommand(ToggleConnect);
            this.ToggleParkCommand = new RelayCommand(TogglePark);
            this.serialUtilities = serialUtilities;
            this.device = device;
            this.driverConnectionManager = driverConnectionManager;
            this.driverConnectionManager.OnConnected += DriverConnectionManager_OnConnected;
            this.driverConnectionManager.OnDisconnected += DriverConnectionManager_OnDisconnected;

            this.SharedState = sharedState;
            this.astrometryConverter = astrometryConverter;
            deviceClientId = driverConnectionManager.RegisterClient();
            Logger.LogMessage("MainVM", $"Device Client Id - {deviceClientId}");
            ResetProperties();
        }

        public void Stop() {
            StopPolling();
            ConnectedDirectly = false;
        }

        private void DriverConnectionManager_OnDisconnected(object sender, ConnectionEventArgs e) {
            var clientGuid = e.ClientGuid;
            if (clientGuid == deviceClientId) {
                return;
            }

            int connectionCount;
            lock (connectedClientsLock) {
                connectedClients.Remove(clientGuid);
                connectionCount = connectedClients.Count;
                ConnectionCount = connectionCount;
            }

            Logger.LogMessage("MainVM", $"OnDisconnected - {clientGuid}, {connectionCount} connections remaining");
            if (connectionCount == 1 && !ConnectedDirectly) {
                // Last disconnection, so stop polling
                try {
                    StopPolling();
                    AsyncContext.Run(() => device.Close(CancellationToken.None));
                } catch (Exception ex) {
                    Logger.LogMessageCrLf("MainVM.DriverConnectionManager_OnDisconnected", $"Failure during device close. {ex}");
                    // Suppress and move on
                }
            }
        }

        private void DriverConnectionManager_OnConnected(object sender, ConnectionEventArgs e) {
            var clientGuid = e.ClientGuid;
            if (clientGuid == deviceClientId) {
                return;
            }

            int connectionCount;
            lock (connectedClientsLock) {
                connectedClients.Add(clientGuid);
                connectionCount = connectedClients.Count;
                ConnectionCount = connectionCount;
            }

            Logger.LogMessage("MainVM", $"OnConnected - {clientGuid}, {connectionCount} connections");
            if (connectionCount == 1) {
                // First connection, so start polling
                ConnectAndStartPolling();
            }
        }

        private void OpenSetupDialog(object o) {
            try {
                SetupVM.Show(SharedState, device, ServoCatOptions, serialUtilities, Logger);
            } catch (Exception ex) {
                Logger.LogMessageCrLf("MainVM.OpenSetupDialog", $"Exception: {ex}");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TogglePark(object o) {
            if (!Connected) {
                return;
            }

            if (AtPark) {
                device?.Unpark(deviceCts.Token);
            } else {
                device?.Park(deviceCts.Token);
            }
        }

        private void ToggleConnect(object o) {
            if (ConnectedDirectly) {
                Stop();
                return;
            } else {
                ConnectAndStartPolling();
                ConnectedDirectly = true;
            }
        }

        private void ConnectAndStartPolling() {
            this.deviceCts?.Cancel();
            this.deviceCts = new CancellationTokenSource();
            this.pollTask = Task.Run(() => ConnectAndPoll(this.deviceCts.Token));
        }

        private void StopPolling() {
            this.deviceCts?.Cancel();
        }

        private void ResetProperties() {
            var angle = Angle.ByRadians(double.NaN);
            RA = angle;
            Dec = angle;
            Altitude = angle;
            Azimuth = angle;
        }

        private async Task ConnectAndPoll(CancellationToken ct) {
            try {
                var channel = await driverConnectionManager.Connect(deviceClientId, ct);
                await device.Open(channel, ct);
            } catch (Exception e) {
                Logger.LogMessageCrLf("MainVM.ConnectAndPoll", $"Failed to connected - {e}");
                if (!SharedState.StartedByCOM) {
                    MessageBox.Show($"Failed to connect - {e.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                throw;
            }

            try {
                Connected = true;
                while (!ct.IsCancellationRequested) {
                    var status = await device.GetExtendedStatus(ct);
                    Tracking = status.MotionStatus.HasFlag(MotionStatusEnum.TRACK);
                    AtPark = status.MotionStatus.HasFlag(MotionStatusEnum.PARK);
                    IsSlewing = status.MotionStatus.HasFlag(MotionStatusEnum.USER_MOTION) || status.MotionStatus.HasFlag(MotionStatusEnum.GOTO);
                    IsAligned = status.MotionStatus.HasFlag(MotionStatusEnum.ALIGN);
                    var deviceCelestialCoordinates = astrometryConverter.TransformEpoch(status.Coordinates, GetEpoch());
                    var deviceTopocentricCoordinates = astrometryConverter.ToTopocentric(status.Coordinates);
                    var syncedTopocentricCoordinates = SharedState.SyncOffset.Rotate(deviceTopocentricCoordinates, false);
                    var syncedCelestialCoordinates = astrometryConverter.ToCelestial(syncedTopocentricCoordinates, status.Coordinates.Epoch);

                    RA = syncedCelestialCoordinates.RA;
                    Dec = syncedCelestialCoordinates.Dec;
                    Altitude = syncedTopocentricCoordinates.Altitude;
                    Azimuth = syncedTopocentricCoordinates.Azimuth;
                    await Task.Delay(ServoCatOptions.TelescopeStatusCacheTTL, ct);
                }
            } catch (TaskCanceledException) {
                Logger.LogMessage("MainVM.ConnectAndPoll", $"Terminated");
            } catch (Exception e) {
                Logger.LogMessageCrLf("MainVM.ConnectAndPoll", $"Failed - {e}");
                if (!SharedState.StartedByCOM) {
                    MessageBox.Show($"Communication failed - {e.Message}. Disconnecting", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                throw;
            } finally {
                ResetProperties();
                Connected = false;
                ConnectedDirectly = false;
                await driverConnectionManager.Disconnect(deviceClientId, ct);
            }
        }

        private Epoch GetEpoch() {
            return ServoCatOptions.UseJ2000 ? Epoch.J2000 : Epoch.JNOW;
        }

        public IServoCatOptions ServoCatOptions { get; private set; }

        public ISharedState SharedState { get; private set; }

        public ICommand ToggleConnectCommand { get; private set; }

        public ICommand ToggleParkCommand { get; private set; }

        public ICommand SetupCommand { get; private set; }

        public int ConnectionCount { get; set; }

        public bool ConnectedDirectly { get; set; }

        public bool Connected { get; set; }

        public bool Tracking { get; set; }

        public bool AtPark { get; set; }

        public bool IsSlewing { get; set; }

        public bool IsAligned { get; set; }

        public Angle Altitude { get; set; }

        public Angle Azimuth { get; set; }

        public Angle RA { get; set; }

        public Angle Dec { get; set; }
    }
}