#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.ghilios.ServoCAT.Utility;
using ASCOM.ghilios.ServoCAT.View;
using ASCOM.Utilities;
using Ninject;
using Nito.AsyncEx;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ASCOM.ghilios.ServoCAT.ViewModel {

    public class SetupVM : BaseVM {
        private readonly TraceLogger logger;

        public SetupVM(
            ISharedState sharedState,
            IServoCatDevice servoCatDevice,
            IServoCatOptions servoCatOptions,
            ISerialUtilities serialUtilities,
            [Named("Server")] TraceLogger logger) {
            this.SharedState = sharedState;
            this.ServoCatDevice = servoCatDevice;
            this.ServoCatOptions = servoCatOptions;
            this.AvailableCOMPorts = serialUtilities.GetAvailableCOMPorts();
            if (String.IsNullOrEmpty(ServoCatOptions.SerialPort)) {
                ServoCatOptions.SerialPort = AvailableCOMPorts.FirstOrDefault();
            }

            this.logger = logger;
            this.ReloadFirmwareConfigCommand = new RelayCommand(ReloadFirmwareConfig);
        }

        public IServoCatDevice ServoCatDevice { get; private set; }

        public ICommand ReloadFirmwareConfigCommand { get; private set; }

        public IServoCatOptions ServoCatOptions { get; private set; }

        public ISharedState SharedState { get; private set; }

        public string[] AvailableCOMPorts { get; private set; }

        private void ReloadFirmwareConfig(object o) {
            try {
                if (!ServoCatDevice.IsConnected) {
                    logger.LogMessage("SetupVM.ReloadFirmwareConfig", "Device is not connected");
                    return;
                }

                var cts = new CancellationTokenSource(ServoCatOptions.DeviceRequestTimeout);
                ServoCatOptions.FirmwareConfig.CopyFrom(AsyncContext.Run(() => ServoCatDevice.GetConfig(cts.Token)));
                ServoCatOptions.FirmwareConfigLoaded = true;
                ServoCatOptions.Save();
            } catch (Exception e) {
                logger.LogMessageCrLf("SetupVM.ReloadFirmwareConfig", $"Reloading the device firmware failed. {e}");
                MessageBox.Show("Reload Failed", $"Reloading the device firmware failed. {e.Message}", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool Show(
            ISharedState sharedState,
            IServoCatDevice servoCatDevice,
            IServoCatOptions servoCatOptions,
            ISerialUtilities serialUtilities,
            [Named("Server")] TraceLogger logger) {
            return Application.Current.Dispatcher.Invoke(() => {
                var optionsClone = servoCatOptions.Clone();
                var setupVM = new SetupVM(sharedState, servoCatDevice, optionsClone, serialUtilities, logger);
                var mainwindow = Application.Current.MainWindow;
                Window win = new Setup {
                    DataContext = setupVM,
                    Title = "ServoCAT Options",
                    WindowStyle = WindowStyle.ToolWindow
                };

                win.Owner = mainwindow;
                win.Closed += (object sender, EventArgs e) => {
                    Application.Current.MainWindow.Focus();
                };

                if (win.ShowDialog() == true) {
                    optionsClone.Save();
                    servoCatOptions.CopyFrom(optionsClone);
                    return true;
                }
                return false;
            });
        }
    }
}