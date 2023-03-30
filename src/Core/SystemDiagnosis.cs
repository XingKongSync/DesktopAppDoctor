using Common;
using Core.Infos;
using LiveCharts;
using LogManager;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Core
{
    public partial class SystemDiagnosis : BindableBase
    {
        private Dispatcher _mainDispatcher;
        private LogService LogService = LogService.Instance.Value;

        private object _locker = new object();//导出数据时的互斥锁

        private string _osVersion;
        private ulong _physicalMemory;
        private ChartValues<ChartDataModel> _memoryUsage;
        private CPUInfo _cpuInfo;
        private ChartValues<ChartDataModel> _cpuUsage;
        private ObservableCollection<DiskInfo> _disks;
        //private ObservableCollection<ChartValues<ChartDataModel>> _allDiskFreeSpace;
        //private List<Bitmap> _screenShots;
        //private List<EventLogInfo> _eventLogs;
        private ObservableCollection<NetworkInfo> _allNetworkInfo;

        public string OsVersion { get => _osVersion; set => SetProperty(ref _osVersion, value); }
        public ulong PhysicalMemory { get => _physicalMemory; set => SetProperty(ref _physicalMemory, value); }
        public ChartValues<ChartDataModel> MemoryUsage { get => _memoryUsage; set => SetProperty(ref _memoryUsage, value); }
        public CPUInfo CpuInfo { get => _cpuInfo; set => SetProperty(ref _cpuInfo, value); }
        public ChartValues<ChartDataModel> CpuUsage { get => _cpuUsage; set => SetProperty(ref _cpuUsage, value); }
        public ObservableCollection<DiskInfo> Disks { get => _disks; set => SetProperty(ref _disks, value); }
        //public ObservableCollection<ChartValues<ChartDataModel>> AllDiskFreeSpace { get => _allDiskFreeSpace; set => SetProperty(ref _allDiskFreeSpace, value); }
        //public List<Bitmap> ScreenShots { get => _screenShots; set => SetProperty(ref _screenShots, value); }
        //public List<EventLogInfo> EventLogs { get => _eventLogs; set => SetProperty(ref _eventLogs, value); }
        public ObservableCollection<NetworkInfo> AllNetworkInfo { get => _allNetworkInfo; set => SetProperty(ref _allNetworkInfo, value); }

        public SystemDiagnosis()
        {
            _mainDispatcher = Dispatcher.CurrentDispatcher;

            _disks = new ObservableCollection<DiskInfo>();
            BindingOperations.EnableCollectionSynchronization(_disks, _disks);

            _allNetworkInfo = new ObservableCollection<NetworkInfo>();
            BindingOperations.EnableCollectionSynchronization(_allNetworkInfo, _allNetworkInfo);

            MemoryUsage = new ChartValues<ChartDataModel>();
            CpuUsage = new ChartValues<ChartDataModel>();

            MemoryUsage.Add(new ChartDataModel() { DateTime = DateTime.Now, Value = double.NaN });
            CpuUsage.Add(new ChartDataModel() { DateTime = DateTime.Now, Value = double.NaN });

            UpdateAll();
        }

        private void UpdateAll()
        {
            UpdateOnce();
            InnerUpdateAlmostStatic();
            InnerUpdateDynamic();
        }

        private void UpdateOnce()
        {
            UpdateOS();
            UpdateMemory();
            UpdateCPU();
            UpdateNetworkInteface();
        }

        private void InnerUpdateAlmostStatic()
        {
            UpdateDisk();
            //UpdateEventLog();
        }

        private void InnerUpdateDynamic()
        {
            UpdateMemoryUsage();
            UpdateCPUUSage();
            UpdateNetworkUsage();
        }

        public void UpdateAlmostStatic()
        {
            bool locked = false;
            try
            {
                locked = Monitor.TryEnter(_locker);
                if (!locked)
                    return;

                InnerUpdateAlmostStatic();
            }
            finally
            {
                if (locked)
                    Monitor.Exit(_locker);
            }
        }

        public void UpdateDynamic()
        {
            bool locked = false;
            try
            {
                locked = Monitor.TryEnter(_locker);
                if (!locked)
                    return;

                InnerUpdateDynamic();
            }
            finally
            {
                if (locked)
                    Monitor.Exit(_locker);
            }
        }

        public void SaveScreenShots(string dir)
        {
            try
            {
                int i = 0;
                foreach (var scr in Screen.AllScreens)
                {
                    string fileName = Path.Combine(dir, $"screenshot_{i}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                    Bitmap bmp = new Bitmap(scr.Bounds.Width, scr.Bounds.Height);
                    Graphics g = Graphics.FromImage(bmp);
                    g.CopyFromScreen(scr.Bounds.Left, scr.Bounds.Top, 0, 0, scr.Bounds.Size);
                    bmp.Save(fileName, ImageFormat.Png);

                    i++;
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Value.LogError("保存截图时出错，原因：" + ex.Message);
            }
        }

        public void Export(string dir)
        {
            try
            {
                List<DiskInfo> disks = null;
                lock (_disks)
                {
                    disks = _disks?.ToList();
                }
                List<NetworkInfo> adapters = null;
                lock (_allNetworkInfo)
                {
                    adapters = _allNetworkInfo?.ToList();
                }
                lock (_locker)
                {
                    string fileName = $"SystemInfo_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    fileName = Path.Combine(dir, fileName);
                    StreamWriter sw = new StreamWriter(fileName);
                    using (sw)
                    {
                        sw.WriteLine("操作系统："+OsVersion);
                        sw.WriteLine("CPU：" + CpuInfo.CPUName);
                        sw.WriteLine("CPU厂家：" + CpuInfo.Manufacture);
                        sw.WriteLine("CPU频率：" + CpuInfo.MaxClockSpeed);
                        sw.WriteLine($"内存：{UnitHelper.GetStorageUnitString(PhysicalMemory)} MB");
                        sw.WriteLine();

                        List<ChartDataModel> usage = null;
                        _mainDispatcher.Invoke(() => usage = MemoryUsage?.ToList());
                        if (null != usage)
                        {
                            sw.WriteLine("最近一段时间的内存剩余量变化：");
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

                        if (null != disks)
                        {
                            sw.WriteLine("当前硬盘使用情况：");
                            foreach (var d in disks)
                            {
                                sw.Write('\t');
                                sw.WriteLine($"卷标：{d.Name}, 总空间：{UnitHelper.GetStorageUnitString(d.TotalSize)}, 剩余空间：{UnitHelper.GetStorageUnitString(d.FreeSize)}");
                            }
                        }
                        sw.WriteLine();

                        if (null != adapters)
                        {
                            sw.WriteLine("网络适配器：");
                            foreach (var a in adapters)
                            {
                                if (!a.IsEnabled)
                                    continue;
                                sw.Write('\t');
                                sw.WriteLine(a.Name);

                                sw.Write("\t\t");
                                sw.WriteLine($"链路速度：{UnitHelper.GetSpeedUnit(a.MaxSpeed)}");

                                sw.Write("\t\t");
                                sw.WriteLine($"MAC：{a.Mac}");

                                sw.Write("\t\t");
                                sw.WriteLine($"IP：{a.IPAddress}");

                                sw.Write("\t\t");
                                sw.WriteLine($"上传速度：{UnitHelper.GetSpeedUnit2(a.UploadSpeed)}");

                                sw.Write("\t\t");
                                sw.WriteLine($"下载速度：{UnitHelper.GetSpeedUnit2(a.DownloadSpeed)}");

                            }
                        }
                        sw.WriteLine();

                        var events = UpdateEventLog();
                        if (null != events)
                        {
                            sw.WriteLine($"最近 {events.Count} 条系统日志（应用程序）：");
                            foreach (var e in events)
                            {
                                sw.WriteLine($"{e.TimeWritten:yyyy-MM-dd HH:mm:ss} 来源：{e.Source}");
                                sw.WriteLine(e.Message);
                                sw.WriteLine();
                            }
                        }
                     
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Value.LogError("导出系统信息时发生错误，原因：" + ex.Message);
            }
        }
    }
}
