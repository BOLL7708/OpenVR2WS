using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using EasyFramework;
using OpenVR2WS.Properties;
using Brushes = System.Windows.Media.Brushes;

namespace OpenVR2WS;

[SupportedOSPlatform("windows7.0")]
public partial class MainWindow
{
    private readonly MainController? _controller;
    private readonly Settings _settings = Settings.Default;
    private int _currentDeliveredSecond;
    private int _currentReceivedSecond;

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
        LabelVersion.Content = $"{Properties.Resources.Version}d";
#else
        LabelVersion.Content = Properties.Resources.Version;
#endif
        TextBoxServerPort.Text = _settings.Port.ToString();
        CheckBoxLaunchMinimized.IsChecked = _settings.LaunchMinimized;
        CheckBoxTray.IsChecked = _settings.Tray;
        CheckBoxExitWithSteamVr.IsChecked = _settings.ExitWithSteam;
        CheckBoxUseDevicePoses.IsChecked = _settings.UseDevicePoses;
        CheckBoxRemoteSettings.IsChecked = _settings.RemoteSettings;

        // Controller
        _controller = new MainController(
            (status, value) =>
            {
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        var now = DateTime.Now;
                        switch (status)
                        {
                            case SuperServer.ServerStatus.Connected:
                                LabelServerStatus.Background = Brushes.OliveDrab;
                                LabelServerStatus.Content = "Connected";
                                break;
                            case SuperServer.ServerStatus.Disconnected:
                                LabelServerStatus.Background = Brushes.Tomato;
                                LabelServerStatus.Content = "Disconnected";
                                break;
                            case SuperServer.ServerStatus.Error:
                                LabelServerStatus.Background = Brushes.Gray;
                                LabelServerStatus.Content = "Error";
                                break;
                            case SuperServer.ServerStatus.DeliveredCount:
                                if (now.Second != _currentDeliveredSecond)
                                {
                                    _currentDeliveredSecond = now.Second;
                                    LabelMessagesDelivered.Content = value.ToString();
                                }

                                break;
                            case SuperServer.ServerStatus.ReceivedCount:
                                if (now.Second != _currentReceivedSecond)
                                {
                                    _currentReceivedSecond = now.Second;
                                    LabelMessagesReceived.Content = value.ToString();
                                }

                                break;
                            case SuperServer.ServerStatus.SessionCount:
                                LabelConnectedClients.Content = value.ToString();
                                break;
                        }
                    });
                }
                catch (TaskCanceledException e)
                {
                    Debug.WriteLine($"Caught exception: {e.Message}");
                }
            },
            (status) =>
            {
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (status)
                        {
                            LabelOpenVrStatus.Background = Brushes.OliveDrab;
                            LabelOpenVrStatus.Content = "Connected";
                        }
                        else
                        {
                            LabelOpenVrStatus.Background = Brushes.Tomato;
                            LabelOpenVrStatus.Content = "Disconnected";
                            if (!_settings.ExitWithSteam) return;
                            _controller?.Shutdown();
                            WindowUtils.DestroyTrayIcon();
                            Application.Current.Shutdown();
                        }
                    });
                }
                catch (TaskCanceledException e)
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

    private void ClickedUrl(object sender, RoutedEventArgs e)
    {
        var link = (Hyperlink)sender;
        MiscUtils.OpenUrl(link.NavigateUri.ToString());
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _controller?.Shutdown();
    }

    private void Button_ServerPort_Click(object sender, RoutedEventArgs e)
    {
        SingleInputDialog dlg = new(this, _settings.Port.ToString(), "Port");
        dlg.ShowDialog();
        var result = dlg.DialogResult == true ? dlg.Value : "";
        var parsedResult = int.TryParse(result, out var value);
        if (!parsedResult || value == 0) return;
        _controller?.RestartServer(value);
        _settings.Port = value;
        TextBoxServerPort.Text = result;
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
        _controller?.ReregisterActions();
    }

    private void CheckBox_RemoteSettings_Checked(object sender, RoutedEventArgs e)
    {
        _settings.RemoteSettings = e.RoutedEvent.Name == "Checked";
        _settings.Save();
    }

    private void Button_RemoteSettingsPassword_Click(object sender, RoutedEventArgs e)
    {
        SingleInputDialog dlg = new(this, "", "Password");
        dlg.ShowDialog();
        var value = dlg.DialogResult == true ? dlg.Value : "";
        var enc = Encoding.UTF8;
        var hash = SHA256.HashData(enc.GetBytes(value));
        var hashBase64String = Convert.ToBase64String(hash);
        _settings.RemoteSettingsPasswordHash = hashBase64String;
        _settings.Save();
    }
}