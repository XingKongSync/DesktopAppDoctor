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

namespace DesktopAppDoctor.Views
{
    /// <summary>
    /// ProgressDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ProgressDialog : Window
    {
        public ProgressDialog()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!ViewModel.CanClose)
            {
                e.Cancel = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CancelHandler?.Invoke(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel?.StartHandler?.Invoke(this);
            Dispatcher.BeginInvoke((Action)(async ()=>await WaitTask()), null);
        }

        private async Task WaitTask()
        {
            Task t = ViewModel?.Task;
            if (t != null)
            {
                await t;
                ViewModel.CanClose = true;
                Close();
            }
        }
    }
}
