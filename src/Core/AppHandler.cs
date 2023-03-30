using Common;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using LogManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

namespace Core
{
    public enum DumpLevel
    {
        Full,
        Mini
    }

    public class AppHandler : BindableBase
    {
        private Process _proc;
        private Dispatcher _mainDispatcher;
        private bool _hasDumped = false;

        private int _threadCount;
        private int _handleCount;
        private long _memorySize;
        private string _response;
        private string _workDir;
        private ChartValues<ChartDataModel> _memoryUsage;
        private ChartValues<ChartDataModel> _cpuUsage;
        private DateTime _lastUpdateTime;
        private TimeSpan _lastTotalCpuTime;
        private double _cpuCost;
        private SeriesCollection _cpuUsageCollection;
        private SeriesCollection _memoryUsageCollection;

        private static readonly CartesianMapper<ChartDataModel> _mapperConfig = Mappers.Xy<ChartDataModel>()
                                                                                    .X(model => model.DateTime.Ticks)
                                                                                    .Y(model => model.Value);
        public Func<double, string> Formatter { get; } = value => new DateTime((long)value).ToString("HH:mm:ss");

        /// <summary>
        /// 标记是否已经 Dump 过
        /// </summary>
        public bool HasDumped { get => _hasDumped; set => SetProperty(ref _hasDumped, value); }

        public int Pid { get; private set; }
        public DateTime StartTime { get; set; }
        public string CommandLine { get; set; }
        public int ThreadCount { get => _threadCount; set => SetProperty(ref _threadCount, value); }
        public int HandleCount { get => _handleCount; set => SetProperty(ref _handleCount, value); }
        public long MemorySize { get => _memorySize; set => SetProperty(ref _memorySize, value); }
        public double CpuCost { get => _cpuCost; set => SetProperty(ref _cpuCost, value); }
        public string Response { get => _response; set => SetProperty(ref _response, value); }
        public string WorkDir { get => _workDir; set => SetProperty(ref _workDir, value); }
        public ChartValues<ChartDataModel> MemoryUsage { get => _memoryUsage; set => SetProperty(ref _memoryUsage, value); }
        public ChartValues<ChartDataModel> CpuUsage { get => _cpuUsage; set => SetProperty(ref _cpuUsage, value); }
        public SeriesCollection CpuUsageCollection { get => _cpuUsageCollection; set => SetProperty(ref _cpuUsageCollection, value); }
        public SeriesCollection MemoryUsageCollection { get => _memoryUsageCollection; set => SetProperty(ref _memoryUsageCollection, value); }

        public AppHandler(Process proc, Dispatcher mainDispatcher)
        {
            _proc = proc;
            _mainDispatcher = mainDispatcher;

            _mainDispatcher.Invoke(() =>
            {
                MemoryUsage = new ChartValues<ChartDataModel>();
                CpuUsage = new ChartValues<ChartDataModel>();
                MemoryUsage.Add(new ChartDataModel() { DateTime = DateTime.Now, Value = double.NaN });
                CpuUsage.Add(new ChartDataModel() { DateTime = DateTime.Now, Value = double.NaN });

                CpuUsageCollection = new SeriesCollection(_mapperConfig) { new LineSeries { Values = CpuUsage } };
                MemoryUsageCollection = new SeriesCollection(_mapperConfig) { new LineSeries { Values = MemoryUsage } };
            });

            Pid = _proc.Id;
            StartTime = _proc.StartTime;
            //CommandLine = _proc.StartInfo.Arguments;
            //WorkDir = _proc.StartInfo.WorkingDirectory;
            CommandLine = _proc.GetCommandLine();
            WorkDir = _proc.GetCurrentDirectory();
            if (string.IsNullOrEmpty(CommandLine))
            {
                CommandLine = "（空）";
            }
        }

        public void Update()
        {
            _proc.Refresh();

            ThreadCount = _proc.Threads.Count;
            HandleCount = _proc.HandleCount;
            MemorySize = _proc.WorkingSet64;
            Response = _proc.Responding ? "正常响应" : "未响应";
            WorkDir = _proc.GetCurrentDirectory();

            _mainDispatcher.Invoke(() =>
            {
                MemoryUsage.Add(new ChartDataModel() { DateTime = DateTime.Now, Value = MemorySize / 1024d / 1024 });
                while (MemoryUsage.Count > 60)
                {
                    MemoryUsage.RemoveAt(0);
                }
            });

            var currentCpuTotal = _proc.TotalProcessorTime;
            if (_lastUpdateTime != default)
            {
                double interval = DateTime.Now.Subtract(_lastUpdateTime).TotalMilliseconds;
                if (interval > 0)
                {
                    double usage = currentCpuTotal.Subtract(_lastTotalCpuTime).TotalMilliseconds / interval / Environment.ProcessorCount * 100;
                    CpuCost = usage;
                    _mainDispatcher.Invoke(() =>
                    {
                        CpuUsage.Add(new ChartDataModel() { DateTime = DateTime.Now, Value = usage });
                        while (CpuUsage.Count > 60)
                        {
                            CpuUsage.RemoveAt(0);
                        }
                    });
                }
            }
            _lastUpdateTime = DateTime.Now;
            _lastTotalCpuTime = currentCpuTotal;
        }

        public bool TestResponding(int seconds = 10)
        {
            if (_proc == null)
                return true;

            _proc.Refresh();

            if (_proc.HasExited)
                return true;

            if (!_proc.Responding)
            {
                return _proc.WaitForInputIdle(1000 * seconds);
            }
            return true;
        }

        private string GetDumpParam(DumpLevel dumpLevel)
        {
            if (dumpLevel == DumpLevel.Full)
            {
                return "-ma";
            }
            return "-mm";
        }

        public void Dump(string dumpFileName, DumpLevel dumpLevel, CancellationTokenSource cancellation = null)
        {
            HasDumped = true;

            string libDir = Path.GetDirectoryName(GetType().Assembly.Location);
            libDir = Path.Combine(libDir, "Libraries", "procdump64.exe");

            using (Process dumpProcess = new Process())
            {
                dumpProcess.StartInfo.FileName = libDir;
                dumpProcess.StartInfo.Arguments = $"-accepteula {GetDumpParam(dumpLevel)} {_proc.Id} {dumpFileName}";
                //dumpProcess.StartInfo.Arguments = string.Format("-accepteula -ma {0} {1}", _proc.Id, dumpFileName);
                dumpProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                dumpProcess.Start();
                while (!dumpProcess.WaitForExit(1000))
                {
                    cancellation?.Token.ThrowIfCancellationRequested();
                }
            }
        }

        public void Export(string dir)
        {
            try
            {
                ExportAppInfo(dir);
            }
            catch (Exception ex)
            {
                LogService.Instance.Value.LogError("导出应用信息时发生错误，原因：" + ex.Message);
            }
        }

        private void ExportAppInfo(string dir)
        {
            string fileName = $"AppInfo_{Pid}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            fileName = Path.Combine(dir, fileName);
            StreamWriter sw = new StreamWriter(fileName);
            using (sw)
            {
                sw.WriteLine("Pid：" + Pid);
                sw.WriteLine($"启动时间：{StartTime:yyyy-MM-dd HH:mm:ss}");
                sw.WriteLine($"线程数量：{ThreadCount}");
                sw.WriteLine($"句柄数：{HandleCount}");
                sw.WriteLine($"分配内存：{UnitHelper.GetStorageUnitString(MemorySize)}");
                sw.WriteLine($"CPU使用率：{CpuCost:0.##}%");
                sw.WriteLine($"状态：{Response}");

                List<ChartDataModel> usage = null;
                _mainDispatcher.Invoke(() => usage = MemoryUsage?.ToList());
                if (null != usage)
                {
                    sw.WriteLine("最近一段时间的内存使用率变化：");
                    foreach (var m in usage)
                    {
                        sw.Write('\t');
                        sw.WriteLine($"{m.DateTime:yyyy-MM-dd HH:mm:ss}, {m.Value:0.##}MB");
                    }
                }
                sw.WriteLine();

                usage = null;
                _mainDispatcher.Invoke(() => usage = CpuUsage.ToList());
                if (null != usage)
                {
                    sw.WriteLine("最近一段时间的CPU使用率变化：");
                    foreach (var c in usage)
                    {
                        sw.Write('\t');
                        sw.WriteLine($"{c.DateTime:yyyy-MM-dd HH:mm:ss}, {c.Value:0.##}%");
                    }
                }
                sw.WriteLine();
            }
        }
    }
}
