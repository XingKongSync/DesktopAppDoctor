using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

namespace TestApp
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Thread.Sleep(30 * 1000);
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            while (true)
            {
                Marshal.PtrToStructure<int>(new IntPtr(new Random().Next()));
            }
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            throw new Exception("123");
        }
    }
}
