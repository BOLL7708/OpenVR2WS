using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpenVR2WS
{
    public partial class MainWindow : Window
    {
        private MainController _controller;
        private Properties.Settings _settings = Properties.Settings.Default;
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private static Mutex _mutex = null;
        public MainWindow()
        {
            InitializeComponent();

            // Prevent multiple instances
            _mutex = new Mutex(true, Properties.Resources.AppName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                Application.Current.MainWindow,
                "This application is already running!",
                Properties.Resources.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information
                );
                Application.Current.Shutdown();
            }

            // Tray icon
            var icon = Properties.Resources.Logo.Clone() as System.Drawing.Icon;
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Click += NotifyIcon_Click;
            _notifyIcon.Text = $"Click to show the {Properties.Resources.AppName} window";
            _notifyIcon.Icon = icon;
            _notifyIcon.Visible = true;


            // Window setup
            Title = Properties.Resources.AppName;
            Label_Version.Content = Properties.Resources.Version;
            TextBox_ServerPort.Text = _settings.Port.ToString();
            CheckBox_LaunchMinimized.IsChecked = _settings.LaunchMinimized;
            CheckBox_Tray.IsChecked = _settings.Tray;

            // Controller
            _controller = new MainController((status, value) => {
                Dispatcher.Invoke(() =>
                {
                    switch (status) {
                        case SuperServer.ServerStatus.Connected:
                            Label_ServerStatus.Background = Brushes.OliveDrab;
                            Label_ServerStatus.Content = "Connected";
                            break;
                        case SuperServer.ServerStatus.Disconnected:
                            Label_ServerStatus.Background = Brushes.Tomato;
                            Label_ServerStatus.Content = "Disconnected";
                            break;
                        case SuperServer.ServerStatus.Error:
                            Label_ServerStatus.Background = Brushes.Gray;
                            Label_ServerStatus.Content = "Error";
                            break;
                        case SuperServer.ServerStatus.DeliveredCount:
                            Label_MessagesDelivered.Content = value.ToString();
                            break;
                        case SuperServer.ServerStatus.ReceivedCount:
                            Label_MessagesReceived.Content = value.ToString();
                            break;
                        case SuperServer.ServerStatus.SessionCount:
                            Label_ConnectedClients.Content = value.ToString();
                            break;
                    } 
                });
                }, 
                (status) => {
                    Dispatcher.Invoke(() => {
                        if (status)
                        {
                            Label_OpenVRStatus.Background = Brushes.OliveDrab;
                            Label_OpenVRStatus.Content = "Connected";
                        }
                        else 
                        {
                            Label_OpenVRStatus.Background = Brushes.Tomato;
                            Label_OpenVRStatus.Content = "Disconnected";
                        }
                    });
                });
        }

        private void ClickedURL(object sender, RoutedEventArgs e)
        {
            var link = (Hyperlink)sender;
            Process.Start(link.NavigateUri.ToString());
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _controller.Shutdown();
        }

        private void Button_ServerPort_Click(object sender, RoutedEventArgs e)
        {
            InputDialog dlg = new InputDialog(_settings.Port, "Port");
            dlg.Owner = this;
            dlg.ShowDialog();
            var result = dlg.DialogResult == true ? dlg.value : 0;
            if (result != 0)
            {
                _controller.RestartServer(result);
                _settings.Port = result;
                TextBox_ServerPort.Text = result.ToString();
            }
        }

        private void CheckBox_LaunchMinimized_Checked(object sender, RoutedEventArgs e)
        {
            _settings.LaunchMinimized = e.RoutedEvent.Name == "Checked";
            _settings.Save();
        }

        private void CheckBox_Tray_Checked(object sender, RoutedEventArgs e)
        {
            _settings.Tray = e.RoutedEvent.Name == "Checked";
            _settings.Save();
            ShowInTaskbar = !_settings.Tray;
        }

        // Restore window
        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            WindowState = WindowState.Normal;
            ShowInTaskbar = !_settings.Tray;
            Show();
            Activate();
        }

        // Not doing this will leave the icon after app closure
        protected override void OnClosing(CancelEventArgs e)
        {
            _notifyIcon.Dispose();
            base.OnClosing(e);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized: ShowInTaskbar = !_settings.Tray; break; // Setting here for tray icon only
                default: ShowInTaskbar = true; Show(); break;
            }
        }
    }
}
