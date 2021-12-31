#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ASCOM.ghilios.ServoCAT.Utility {

    public class WindowService : IWindowService {
        protected Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        protected CustomWindow window;

        public void Show(object content, string title = "", ResizeMode resizeMode = ResizeMode.NoResize, WindowStyle windowStyle = WindowStyle.None) {
            dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                window = GenerateWindow(title, resizeMode, windowStyle, null);

                window.Content = content;
                window.Owner = Application.Current.MainWindow;
                window.Show();
            }));
        }

        public void DelayedClose(TimeSpan t) {
            Task.Run(async () => {
                await Task.Delay(t);
                await this.Close();
            });
        }

        public async Task Close() {
            await dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => {
                window?.Close();
            }));
        }

        private CustomWindow GenerateWindow(string title, ResizeMode resizeMode, WindowStyle windowStyle, ICommand closeCommand) {
            var window = new CustomWindow() {
                SizeToContent = SizeToContent.WidthAndHeight,
                Title = title,
                Background = Application.Current.TryFindResource("BackgroundBrush") as Brush,
                ResizeMode = resizeMode,
                WindowStyle = windowStyle,
                MinHeight = 300,
                MinWidth = 350,
                Style = Application.Current.TryFindResource("NoResizeWindow") as Style,
            };

            if (closeCommand == null) {
                window.CloseCommand = new RelayCommand((object o) => window.Close());
            } else {
                window.CloseCommand = closeCommand;
            }

            var mainwindow = Application.Current.MainWindow;

            window.Closing += (object sender, CancelEventArgs e) => {
                if ((sender is Window w) && w.IsFocused) {
                    mainwindow.Focus();
                }
            };
            window.Closed += (object sender, EventArgs e) => {
                this.OnClosed?.Invoke(this, null);
                mainwindow.Focus();
            };
            window.ContentRendered += (object sender, EventArgs e) => {
                var win = (System.Windows.Window)sender;
                win.InvalidateVisual();

                var rect = mainwindow.GetAbsolutePosition();
                win.Left = rect.Left + (rect.Width - win.ActualWidth) / 2;
                win.Top = rect.Top + (rect.Height - win.ActualHeight) / 2;
            };
            window.Owner = mainwindow;

            return window;
        }

        public IDispatcherOperationWrapper ShowDialog(object content, string title = "", ResizeMode resizeMode = ResizeMode.NoResize, WindowStyle windowStyle = WindowStyle.None, ICommand closeCommand = null) {
            return new DispatcherOperationWrapper(dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => {
                window = GenerateWindow(title, resizeMode, windowStyle, closeCommand);

                window.Content = content;
                Application.Current.MainWindow.Opacity = 0.8;
                var result = window.ShowDialog();
                this.OnDialogResultChanged?.Invoke(this, new DialogResultEventArgs(result));
                Application.Current.MainWindow.Opacity = 1;
            })));
        }

        public event EventHandler OnDialogResultChanged;

        public event EventHandler OnClosed;
    }

    public interface IWindowService {

        void Show(object content, string title = "", ResizeMode resizeMode = ResizeMode.NoResize, WindowStyle windowStyle = WindowStyle.None);

        IDispatcherOperationWrapper ShowDialog(object content, string title = "", ResizeMode resizeMode = ResizeMode.NoResize, WindowStyle windowStyle = WindowStyle.None, ICommand closeCommand = null);

        event EventHandler OnDialogResultChanged;

        event EventHandler OnClosed;

        void DelayedClose(TimeSpan t);

        Task Close();
    }

    public interface IDispatcherOperationWrapper {
        Dispatcher Dispatcher { get; }
        DispatcherPriority Priority { get; set; }
        DispatcherOperationStatus Status { get; }
        Task Task { get; }
        object Result { get; }

        TaskAwaiter GetAwaiter();

        DispatcherOperationStatus Wait();

        DispatcherOperationStatus Wait(TimeSpan timeout);

        bool Abort();

        event EventHandler Aborted;

        event EventHandler Completed;
    }

    public class DispatcherOperationWrapper : IDispatcherOperationWrapper {
        private readonly DispatcherOperation op;

        public DispatcherOperationWrapper(DispatcherOperation operation) {
            op = operation;
        }

        public Dispatcher Dispatcher => op.Dispatcher;

        public DispatcherPriority Priority {
            get => op.Priority;
            set => op.Priority = value;
        }

        public DispatcherOperationStatus Status => op.Status;
        public Task Task => op.Task;

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public TaskAwaiter GetAwaiter() {
            return op.GetAwaiter();
        }

        public DispatcherOperationStatus Wait() {
            return op.Wait();
        }

        [SecurityCritical]
        public DispatcherOperationStatus Wait(TimeSpan timeout) {
            return op.Wait(timeout);
        }

        public bool Abort() {
            return op.Abort();
        }

        public object Result => op.Result;

        public event EventHandler Aborted {
            add => op.Aborted += value;
            remove => op.Aborted -= value;
        }

        public event EventHandler Completed {
            add => op.Completed += value;
            remove => op.Completed -= value;
        }
    }

    public class DialogResultEventArgs : EventArgs {

        public DialogResultEventArgs(bool? dialogResult) {
            DialogResult = dialogResult;
        }

        public bool? DialogResult { get; set; }
    }
}