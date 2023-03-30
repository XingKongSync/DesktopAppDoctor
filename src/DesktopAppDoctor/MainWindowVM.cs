using Common;
using Core;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using LogManager;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DesktopAppDoctor.Config;
using DesktopAppDoctor.Views;
using System.ComponentModel;

namespace DesktopAppDoctor
{
    public class MainWindowVM : BindableBase
    {
        private static readonly Lazy<MainWindowVM> _instance = new Lazy<MainWindowVM>(() => new MainWindowVM());

        public static MainWindowVM Instance => _instance.Value;

        
        private SystemDiagnosis _systemDiagnosis;
        private AppDiagnosis _appDiagnosis;
        private DispatcherTimer _timer;
        private Thread _timerThread;
        private int _intervalSeconds = 2;

        private int _updateCount = 0;

        private CartesianMapper<ChartDataModel> _mapperConfig = Mappers.Xy<ChartDataModel>()
                                                                 .X(model => model.DateTime.Ticks)
                                                                 .Y(model => model.Value);

        private SeriesCollection _cpuUsage;
        private SeriesCollection _memoryUsage;
        //private SolidColorBrush _color1 = new SolidColorBrush(Colors.Orange);
        //private SolidColorBrush _color2 = new SolidColorBrush(Colors.DarkCyan);

        public SystemDiagnosis SystemDiagnosis { get => _systemDiagnosis; set => SetProperty(ref _systemDiagnosis, value); }
        public SeriesCollection CpuUsage { get => _cpuUsage; set => SetProperty(ref _cpuUsage, value); }
        public SeriesCollection MemoryUsage { get => _memoryUsage; set => SetProperty(ref _memoryUsage, value); }
        public AppDiagnosis AppDiagnosis { get => _appDiagnosis; set => SetProperty(ref _appDiagnosis, value); }
        public MemoryLeakDetect MemoryLeakDetect { get; private set; }

        public int IntervalSeconds
        {
            get => _intervalSeconds;
            set
            {
                if (value <= 0)
                    return;

                DispatcherTimer timer = _timer;
                if (SetProperty(ref _intervalSeconds, value) && timer != null)
                {
                    timer.Interval = TimeSpan.FromSeconds(value);
                }
            }
        }

        public ObservableCollection<string> CurrentLogs { get; private set; } = LogManagerForUI.Instance.CoreLogs;

        public Func<double, string> Formatter { get; } = value => new DateTime((long)value).ToString("HH:mm:ss");

        private MainWindowVM()
        {
            //_color1.Freeze();
            //_color2.Freeze();

            var config = ConfigManager.Instance.Value.Config;
         
            SystemDiagnosis = new SystemDiagnosis();
            AppDiagnosis = new AppDiagnosis(config.ProcessName);

            PropertyChangedEventManager.AddHandler(config, ConfigProcessNameChangedHandler, nameof(config.ProcessName));

            MemoryLeakDetect = new MemoryLeakDetect(512);
            InitTimer();

            CpuUsage = new SeriesCollection(_mapperConfig) { new LineSeries { Values = SystemDiagnosis.CpuUsage } };
            MemoryUsage = new SeriesCollection(_mapperConfig) { new LineSeries { Values = SystemDiagnosis.MemoryUsage } };
        }

        /// <summary>
        /// 当用户更改了进程名称时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConfigProcessNameChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            AppDiagnosis.ProcessName = ConfigManager.Instance.Value.Config.ProcessName;
        }

        private void InitTimer()
        {
            if (null != _timer)
                return;

            using (var mre = new ManualResetEvent(false))
            {
                _timerThread = new Thread(() =>
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    mre.Set();
                    Dispatcher.Run();
                });
                _timerThread.SetApartmentState(ApartmentState.STA);
                _timerThread.Name = "TimerThread";
                _timerThread.IsBackground = true;
                _timerThread.Start();
                mre.WaitOne();

                _timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.FromThread(_timerThread));
                _timer.Interval = TimeSpan.FromSeconds(IntervalSeconds);
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
        }

        private void ShutdownTimer()
        {
            _timer?.Stop();
            Dispatcher.FromThread(_timerThread)?.InvokeShutdown();
            _timer = null;
            _timerThread = null;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                MemoryLeakDetect.CheckMemory();
                SystemDiagnosis.UpdateDynamic();
                AppDiagnosis.UpdateDynamic();
                _updateCount = (_updateCount + 1) % 5;
                if (_updateCount == 0)
                {
                    //寻找应崩溃产生的 Dump
                    if (ConfigManager.Instance.Value.Config.CrashDump)
                    {
                        var crashedApps = AppDiagnosis.CheckCrash();
                        foreach (var item in crashedApps)
                        {
                            (FileInfo dumpfile, AppHandler app) = item;
                            ExportCrash(app, dumpfile);
                        }
                        if (crashedApps.Count > 0)
                        {
                            //因为系统在 Dump 进程的时候会 Clone 一个一模一样的进程出来
                            //此时如果继续执行下面的代码，会将系统 Dump 进程误判为未响应进程
                            return;
                        }
                    }

                    SystemDiagnosis.UpdateAlmostStatic();
                    AppDiagnosis.UpdateAlmostStatic();

                    //检测未响应的程序并导出相关信息
                    if (ConfigManager.Instance.Value.Config.HangDump)
                    {
                        var noRespondingApps = AppDiagnosis.TestResponding();
                        foreach (AppHandler app in noRespondingApps)
                        {
                            //已经 Dump 过的应用程序将不再被二次 Dump
                            if (app.HasDumped)
                                continue;
                            LogService.Instance.Value.LogDebug($"进程（Pid：{app.Pid}）失去响应，即将导出相关信息...");
                            Export(app);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Value.LogError($"刷新数据时遇到错误，原因：{ex.Message}");
            }

        }

        /// <summary>
        /// 生成默认导出路径
        /// </summary>
        /// <returns></returns>
        private string GetDefaultExportFileName()
        {
            //使用默认路径导出
            string outputDir = ConfigManager.Instance.Value.Config.DumpPath;
            string fileName = $"{AppDiagnosis.ProcessName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            fileName = Path.Combine(outputDir, fileName);//打包文件路径
            return fileName;
        }

        public void Export(AppHandler app, string fileName = null)
        {
            if (Thread.CurrentThread.ManagedThreadId != App.Current.Dispatcher.Thread.ManagedThreadId)
            {
                App.Current.Dispatcher.Invoke(() => Export(app, fileName));
                return;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                //使用默认路径导出
                fileName = GetDefaultExportFileName();
            }

            InnerExport(app, fileName);
        }

        /// <summary>
        /// 导出 APP 相关数据
        /// </summary>
        /// <param name="app"></param>
        /// <param name="fileName"></param>
        private void InnerExport(AppHandler app, string fileName)
        {
            var config = ConfigManager.Instance.Value.Config;
            string tempDir = Path.GetDirectoryName(fileName);
            tempDir = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(fileName) + "_tmp");

            //创建导出目录
            try
            {
                Directory.CreateDirectory(tempDir);
            }
            catch (Exception ex)
            {
                LogService.Instance.Value.LogError("创建临时导出目录失败，原因：" + ex.Message);
            }

            //在弹出窗口之前先截屏
            SystemDiagnosis.SaveScreenShots(tempDir);
            MainWindow.Instance.Show();

            ProgressDialog progressDialog = new ProgressDialog();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Task t = new Task(() =>
            {
                var vm = progressDialog.ViewModel;
                try
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    vm.Content2 = "正在导出系统信息";
                    SystemDiagnosis.Export(tempDir);
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    vm.Content2 = "正在导出应用程序信息";
                    AppDiagnosis.Export(app, tempDir);
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    vm.Content2 = "正在导出应用程序 Dump";
                    string dumpTempFile = Path.Combine(tempDir, $"application_{app.Pid}.DMP");
                    app.Dump(dumpTempFile, config.DumpLevel, cancellationTokenSource);

                    //zip
                    vm.Content2 = "正在打包诊断文件";
                    if (!ZipHelper.ZipDirectoryWithExtraFile(tempDir, fileName, cancellationTokenSource))
                    {
                        throw new Exception("打包文件失败");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Value.LogError($"导出 Dump 失败，原因：{ex.Message}");
                }
                finally
                {
                    //删除临时文件夹
                    vm.Content2 = "正在清理临时文件";
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Value.LogError($"正在清理临时文件失败，原因：{ex.Message}");
                    }
                    //如果用户点击了取消
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        //删除 zip 文件
                        try { File.Delete(fileName); } catch { }
                    }
                }
            });
            Action<ProgressDialog> start = d => t.Start();
            Action<ProgressDialog> cancel = d => { d.ViewModel.Content2 = "正在取消"; cancellationTokenSource.Cancel(); };

            try
            {
                progressDialog.Owner = MainWindow.Instance;
                progressDialog.ViewModel.Task = t;
                progressDialog.ViewModel.StartHandler = start;
                progressDialog.ViewModel.CancelHandler = cancel;
                progressDialog.ViewModel.Content = "正在导出诊断信息，请稍后...";
                progressDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                LogService.Instance.Value.LogError($"打开导出窗口时出错，原因：{ex.Message}");
            }
        }

        /// <summary>
        /// 导出 APP 相关数据
        /// </summary>
        /// <param name="app"></param>
        /// <param name="crashDump"></param>
        private void ExportCrash(AppHandler app, FileInfo crashDump)
        {
            if (Thread.CurrentThread.ManagedThreadId != App.Current.Dispatcher.Thread.ManagedThreadId)
            {
                App.Current.Dispatcher.Invoke(() => ExportCrash(app, crashDump));
                return;
            }

            //使用默认路径导出
            string fileName = GetDefaultExportFileName();
            //临时打包路径
            string tempDir = Path.GetDirectoryName(fileName);
            tempDir = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(fileName) + "_tmp");
            //创建导出目录
            try
            {
                Directory.CreateDirectory(tempDir);
            }
            catch (Exception ex)
            {
                LogService.Instance.Value.LogError("创建临时导出目录失败，原因：" + ex.Message);
            }

            //在弹出窗口之前先截屏
            SystemDiagnosis.SaveScreenShots(tempDir);
            MainWindow.Instance.Show();

            ProgressDialog progressDialog = new ProgressDialog();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Task t = new Task(() =>
            {
                var vm = progressDialog.ViewModel;
                try
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    vm.Content2 = "正在导出系统信息";
                    SystemDiagnosis.Export(tempDir);
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    if (app != null)
                    {
                        vm.Content2 = "正在导出应用程序信息";
                        AppDiagnosis.Export(app, tempDir);
                        app.HasDumped = true;
                        cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    }

                    //zip
                    vm.Content2 = "正在打包诊断文件";
                    if (!ZipHelper.ZipDirectoryWithExtraFile(tempDir, fileName, cancellationTokenSource, crashDump.FullName))
                    {
                        throw new Exception("打包文件失败");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Value.LogError($"导出 Dump 失败，原因：{ex.Message}");
                }
                finally
                {
                    //删除临时文件夹
                    vm.Content2 = "正在清理临时文件";
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Value.LogError($"清理临时文件失败，原因：{ex.Message}");
                    }

                    //删除 Dump
                    Exception lastException = null;
                    int retryCount = 0;
                    while (retryCount < 10)
                    {
                        ++retryCount;
                        try
                        {
                            crashDump.Delete();
                        }
                        catch (Exception e)
                        {
                            lastException = e;
                            Thread.Sleep(1000);
                        }
                        break;
                    }
                    if (null != lastException)
                        LogService.Instance.Value.LogError($"删除 Dump 文件失败，原因：{lastException.Message}");

                    //如果用户点击了取消
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        //删除 zip 文件
                        try { File.Delete(fileName); } catch { }
                    }
                }
            });
            Action<ProgressDialog> start = d => t.Start();
            Action<ProgressDialog> cancel = d => { d.ViewModel.Content2 = "正在取消"; cancellationTokenSource.Cancel(); };

            try
            {
                progressDialog.Owner = MainWindow.Instance;
                progressDialog.ViewModel.Task = t;
                progressDialog.ViewModel.StartHandler = start;
                progressDialog.ViewModel.CancelHandler = cancel;
                progressDialog.ViewModel.Content = "正在导出诊断信息，请稍后...";
                progressDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                LogService.Instance.Value.LogError($"打开导出窗口时出错，原因：{ex.Message}");
            }
        }
    }
}
