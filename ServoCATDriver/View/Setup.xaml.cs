#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using PostSharp.Patterns.Xaml;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ASCOM.ghilios.ServoCAT.View {

    /// <summary>
    /// Interaction logic for Setup.xaml
    /// </summary>
    public partial class Setup : Window {

        public Setup() {
            InitializeComponent();
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
        }

        private void InputValidation_Error(object sender, ValidationErrorEventArgs e) {
            if (e.Action == ValidationErrorEventAction.Added) {
                ++ValidationErrors;
            } else {
                --ValidationErrors;
            }
        }

        [DependencyProperty]
        public int ValidationErrors { get; set; }

        public static DependencyProperty ValidationErrorsProperty { get; private set; }
    }
}