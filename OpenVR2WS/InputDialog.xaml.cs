using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace OpenVR2WS
{
    public partial class InputDialog : Window
    {
        public string labelText;
        public int value;
        public InputDialog(int value, string labelText)
        {
            this.value = value;
            this.labelText = $"{labelText}:";
            InitializeComponent();
            Title = $"Set {labelText}";
            labelValue.Content = this.labelText;
            textBoxValue.Text = value.ToString();
            textBoxValue.Focus();
            textBoxValue.SelectAll();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = Int32.TryParse(textBoxValue.Text, out value);
        }
    }
}
