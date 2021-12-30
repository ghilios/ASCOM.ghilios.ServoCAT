#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Joko.ServoCAT.Interfaces;
using ASCOM.Joko.ServoCAT.Utility;
using ASCOM.Joko.ServoCAT.View;
using ASCOM.Utilities;
using Ninject;
using System;
using System.Windows;
using System.Windows.Input;

namespace ASCOM.Joko.ServoCAT.ViewModel {

    public class MainVM : BaseVM, IMainVM {
        private readonly TraceLogger Logger;

        public MainVM(
            IServoCatOptions servoCatOptions,
            [Named("Server")] TraceLogger logger) {
            this.ServoCatOptions = servoCatOptions;
            this.Logger = logger;
            this.SetupCommand = new RelayCommand(OpenSetupDialog);
        }

        private void OpenSetupDialog(object o) {
            try {
                SetupVM.Show(ServoCatOptions);
            } catch (Exception ex) {
                Logger.LogMessageCrLf("MainVM.OpenSetupDialog", $"Exception: {ex}");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public IServoCatOptions ServoCatOptions { get; private set; }

        public ICommand SetupCommand { get; private set; }
    }
}