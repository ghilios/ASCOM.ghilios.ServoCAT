#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Joko.ServoCAT.Interfaces;
using ASCOM.Joko.ServoCAT.View;
using System;
using System.Windows;
using System.Windows.Input;

namespace ASCOM.Joko.ServoCAT.ViewModel {

    public class SetupVM : BaseVM {

        public SetupVM(IServoCatOptions servoCatOptions) {
            this.ServoCatOptions = servoCatOptions;
        }

        public IServoCatOptions ServoCatOptions { get; private set; }

        public static bool Show(IServoCatOptions servoCatOptions) {
            return Application.Current.Dispatcher.Invoke(() => {
                var optionsClone = servoCatOptions.Clone();
                var setupVM = new SetupVM(optionsClone);
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