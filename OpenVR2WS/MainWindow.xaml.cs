using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using EasyFramework;
using OpenVR2WS.Properties;
using Brushes = System.Windows.Media.Brushes;

namespace OpenVR2WS
{
    public partial class MainWindow : Window
    {
        private MainController _controller;
        private Settings _settings = Settings.Default;
        private int _currentDeliveredSecond = 0;
        private int _currentReceivedSecond = 0;

        public MainWindow()
        {
            InitializeComponent();

            // Prevent multiple instances
            WindowUtils.CheckIfAlreadyRunning(Properties.Resources.AppName);

            // Tray icon
            var icon = Properties.Resources.Logo.Clone() as Icon;
            WindowUtils.CreateTrayIcon(this, icon, Properties.Resources.AppName);

            // Window setup
            Title = Properties.Resources.AppName;
#if DEBUG
            Label_Version.Content = $"{Properties.Resources.Version}d";
#else
            Label_Version.Content = Properties.Resources.Version;
#endif
            TextBox_ServerPort.Text = _settings.Port.ToString();
            CheckBox_LaunchMinimized.IsChecked = _settings.LaunchMinimized;
            CheckBox_Tray.IsChecked = _settings.Tray;
            CheckBox_ExitWithSteamVR.IsChecked = _settings.ExitWithSteam;
            CheckBox_UseDevicePoses.IsChecked = _settings.UseDevicePoses;
            CheckBox_RemoteSettings.IsChecked = _settings.RemoteSettings;

            // Controller
            _controller = new MainController(
                (status, value) => {
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var now = DateTime.Now;
                            switch (status)
                            {
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
                                    if (now.Second != _currentDeliveredSecond)
                                    {
                                        _currentDeliveredSecond = now.Second;
                                        Label_MessagesDelivered.Content = value.ToString();
                                    }
                                    break;
                                case SuperServer.ServerStatus.ReceivedCount:
                                    if (now.Second != _currentReceivedSecond)
                                    {
                                        _currentReceivedSecond = now.Second;
                                        Label_MessagesReceived.Content = value.ToString();
                                    }
                                    break;
                                case SuperServer.ServerStatus.SessionCount:
                                    Label_ConnectedClients.Content = value.ToString();
                                    break;
                            }
                        });
                    } catch (TaskCanceledException e) {
                        Debug.WriteLine($"Caught exception: {e.Message}");
                    }
                }, 
                (status) => {
                    try
                    {
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
                                if (_settings.ExitWithSteam)
                                {
                                    _controller.Shutdown();
                                    WindowUtils.DestroyTrayIcon();
                                    Application.Current.Shutdown();
                                }
                            }
                        });
                    } catch (TaskCanceledException e)
                    {
                        Debug.WriteLine($"Caught exception: {e.Message}");
                    }
                }
            );
            if (_settings.LaunchMinimized)
            {
                WindowUtils.Minimize(this, !_settings.Tray);
            }
        }

        private void ClickedURL(object sender, RoutedEventArgs e)
        {
            var link = (Hyperlink)sender;
            Process.Start(link.NavigateUri.ToString());
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _controller.Shutdown();
        }

        private void Button_ServerPort_Click(object sender, RoutedEventArgs e)
        {
            SingleInputDialog dlg = new(this, _settings.Port.ToString(), "Port");
            dlg.ShowDialog();
            var result = dlg.DialogResult == true ? dlg.Value : "";
            var parsedResult = int.TryParse(result, out int value);
            if (parsedResult && value != 0)
            {
                _controller.RestartServer(value);
                _settings.Port = value;
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

        // Not doing this will leave the icon after app closure
        protected override void OnClosing(CancelEventArgs e)
        {
            WindowUtils.DestroyTrayIcon();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            WindowUtils.OnStateChange(this, !_settings.Tray);
        }

        private void CheckBox_ExitWithSteamVR_Checked(object sender, RoutedEventArgs e)
        {
            _settings.ExitWithSteam = e.RoutedEvent.Name == "Checked";
            _settings.Save();
        }

        private void CheckBox_UseDevicePoses_Checked(object sender, RoutedEventArgs e)
        {
            _settings.UseDevicePoses = e.RoutedEvent.Name == "Checked";
            _settings.Save();
            if(_controller != null) _controller.ReregisterActions();
        }
        private void CheckBox_RemoteSettings_Checked(object sender, RoutedEventArgs e)
        {
            _settings.RemoteSettings = e.RoutedEvent.Name == "Checked";
            _settings.Save();
        }

        private void Button_RemoteSettingsPassword_Click(object sender, RoutedEventArgs e) {
            SingleInputDialog dlg = new(this, "", "Password");
            dlg.ShowDialog();
            var value = dlg.DialogResult == true ? dlg.Value : "";
                        
            using (SHA256 sha = SHA256.Create())
            {
                Encoding enc = Encoding.UTF8;
                byte[] hash = sha.ComputeHash(enc.GetBytes(value));
                var hashb64 = Convert.ToBase64String(hash);
                _settings.RemoteSettingsPasswordHash = hashb64;
                _settings.Save();
            }
        }
    }
}
