#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Windows.Input;

namespace ASCOM.ghilios.ServoCAT.Utility {

    public class RelayCommand : ICommand {
        private Action<object> execute;
        private Predicate<object> canExecute;

        public RelayCommand(Action<object> execute)
            : this(execute, DefaultCanExecute) {
        }

        public RelayCommand(Action<object> execute, Predicate<object> canExecute) {
            if (execute == null) {
                throw new ArgumentNullException("execute");
            }

            if (canExecute == null) {
                throw new ArgumentNullException("canExecute");
            }

            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged {
            add {
                CommandManager.RequerySuggested += value;
                this.CanExecuteChangedInternal += value;
            }

            remove {
                CommandManager.RequerySuggested -= value;
                this.CanExecuteChangedInternal -= value;
            }
        }

        private event EventHandler CanExecuteChangedInternal;

        public bool CanExecute(object parameter) {
            return this.canExecute != null && this.canExecute(parameter);
        }

        public void Execute(object parameter) {
            this.execute(parameter);
        }

        public void OnCanExecuteChanged() {
            EventHandler handler = this.CanExecuteChangedInternal;
            if (handler != null) {
                handler.Invoke(this, EventArgs.Empty);
            }
        }

        public void Destroy() {
            this.canExecute = _ => false;
            this.execute = _ => { return; };
        }

        private static bool DefaultCanExecute(object parameter) {
            return true;
        }
    }
}