using Core;
using LiveCharts.Wpf;
using LogManager;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
using DesktopAppDoctor.Views;
using DesktopAppDoctor.Config;

namespace DesktopAppDoctor
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static MainWindow _instance;

        public static MainWindow Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MainWindow();
                }
                return _instance;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = MainWindowVM.Instance;
        }

        private void self_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }


        /// <summary>
        /// 用户点击了生成 Dump 按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DumpButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is AppHandler app)
            {
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.FileName = $"{MainWindowVM.Instance.AppDiagnosis.ProcessName}_{DateTime.Now:yyyyMMdd_HHmmss}.DMP";
                dlg.DefaultExt = ".DMP";
                dlg.Filter = "Dump files (.DMP)|*.DMP";

                if (dlg.ShowDialog() == true)
                {
                    string fileName = dlg.FileName;
                    var config = ConfigManager.Instance.Value.Config;

                    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                    Task t = new Task(() =>
                    {
                        try
                        {
                            app.Dump(fileName, config.DumpLevel, cancellationTokenSource);
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Value.LogError($"导出 Dump 失败，原因：{ex.Message}");
                        }
                    });
                    Action<ProgressDialog> start = d => t.Start();
                    Action<ProgressDialog> cancel = d => cancellationTokenSource.Cancel();

                    ProgressDialog progressDialog = new ProgressDialog();
                    progressDialog.Owner = this;
                    progressDialog.ViewModel.Task = t;
                    progressDialog.ViewModel.StartHandler = start;
                    progressDialog.ViewModel.CancelHandler = cancel;
                    progressDialog.ViewModel.Content = "正在导出 Dump";
                    progressDialog.ShowDialog();
                }
            }
        }

        private void FullDumpButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is AppHandler app)
            {
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.FileName = $"{MainWindowVM.Instance.AppDiagnosis.ProcessName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                dlg.DefaultExt = ".zip";
                dlg.Filter = "Zip files (.Zip)|*.zip";

                if (dlg.ShowDialog() == true)
                {
                    string fileName = dlg.FileName;
                    MainWindowVM.Instance.Export(app, fileName);
                }
            }
        }
    }
}
