using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainController _controller;

        public MainWindow()
        {
            InitializeComponent();
            Title = Properties.Resources.AppName;
            TextBox_ServerPort.Text = Properties.Settings.Default.Port.ToString();
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

        

        private void Button_ServerPort_Click(object sender, RoutedEventArgs e)
        {
            InputDialog dlg = new InputDialog(Properties.Settings.Default.Port, "Port");
            dlg.Owner = this;
            dlg.ShowDialog();
            var result = dlg.DialogResult == true ? dlg.value : 0;
            if (result != 0)
            {
                _controller.RestartServer(result);
                Properties.Settings.Default.Port = result;
                TextBox_ServerPort.Text = result.ToString();
            }
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
    }
}
