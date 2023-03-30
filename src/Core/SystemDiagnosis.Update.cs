using Common;
using Core.Infos;
using LiveCharts;
using LiveCharts.Configurations;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public partial class SystemDiagnosis
    {
        private void UpdateOS()
        {
            //OS
            LogService.LogDebug("正在获取操作系统信息...", false);
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
                foreach (ManagementObject os in searcher.Get())
                {
                    OsVersion = os["Caption"].ToString() + " " + os["Version"].ToString();
                    break;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("获取操作系统信息失败，原因：" + ex.Message);
            }
        }

        private void UpdateMemory()
        {
            //Memory
            LogService.LogDebug("正在获取内存信息...", false);
            try
            {
                ComputerInfo info = new ComputerInfo();
                PhysicalMemory = info.TotalPhysicalMemory;
            }
            catch (Exception ex)
            {
                LogService.LogError("获取内存信息失败，原因：" + ex.Message);
            }
        }

        private PerformanceCounter _availableMemory;

        private void UpdateMemoryUsage()
        {
            LogService.LogDebug("正在获取可用内存信息...", false);
            try
            {
                if (_availableMemory == null)
                {
                    _availableMemory = new PerformanceCounter("Memory", "Available MBytes");
                }
                _mainDispatcher?.Invoke(() =>
                {
                    MemoryUsage.Add(new ChartDataModel() { DateTime = DateTime.Now, Value = _availableMemory.NextValue() });
                    while (MemoryUsage.Count > 60)
                    {
                        MemoryUsage.RemoveAt(0);
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.LogError("获取可用内存信息失败，原因：" + ex.Message);
            }
        }

        private void UpdateCPU()
        {
            LogService.LogDebug("正在获取CPU信息...", false);
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, MaxClockSpeed FROM Win32_Processor");
                foreach (ManagementObject cpu in searcher.Get())
                {
                    CpuInfo = new Infos.CPUInfo()
                    {
                        CPUName = cpu["Name"].ToString(),
                        Manufacture = cpu["Manufacturer"].ToString(),
                        MaxClockSpeed = cpu["MaxClockSpeed"].ToString() + " MHz"
                    };
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("获取CPU信息失败，原因：" + ex.Message);
            }
        }

        private PerformanceCounter _cpuUsagePC;
        
        private void UpdateCPUUSage()
        {
            LogService.LogDebug("正在获取CPU使用率...", false);
            try
            {
                if (_cpuUsagePC == null)
                {
                    _cpuUsagePC = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                }
                _mainDispatcher?.Invoke(() =>
                {
                    CpuUsage.Add(new ChartDataModel() { DateTime = DateTime.Now, Value = _cpuUsagePC.NextValue() });
                    while (CpuUsage.Count > 60)
                    {
                        CpuUsage.RemoveAt(0);
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.LogError("获取CPU使用率失败，原因：" + ex.Message);
            }
        }

        private void UpdateDisk()
        {
            LogService.LogDebug("正在获取磁盘信息...", false);
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in drives)
                {
                    lock (_disks)
                    {
                        var ui = _disks.FirstOrDefault(d => d.Name?.Equals(drive.Name) == true);
                        if (null == ui)
                        {
                            _disks.Add(new DiskInfo()
                            {
                                Name = drive.Name,
                                FreeSize = drive.AvailableFreeSpace,
                                TotalSize = drive.TotalSize
                            });
                        }
                        else
                        {
                            ui.Name = drive.Name;
                            ui.TotalSize = drive.TotalSize;
                            ui.FreeSize = drive.AvailableFreeSpace;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("获取磁盘信息失败，原因：" + ex.Message);
            }
        }

        private List<EventLogInfo> UpdateEventLog()
        {
            LogService.LogDebug("正在获取系统事件...", false);
            try
            {
                List<EventLogInfo> infos = new List<EventLogInfo>();
                const string logName = "Application";
                EventLog eventLog = new EventLog(logName);
                // 最近的10条
                EventLogEntryCollection entries = eventLog.Entries;
                int count = entries.Count;
                int addCount = 0;

                for (int i = count - 1; i >= 0 && addCount < 10; i--)
                {
                    EventLogEntry entry = entries[i];
                    if (entry.EntryType != EventLogEntryType.Error && entry.EntryType != EventLogEntryType.Warning)
                    {
                        continue;
                    }
                    ++addCount;
                    infos.Add(new EventLogInfo()
                    {
                        Message = entry.Message,
                        Source = entry.Source,
                        TimeWritten = entry.TimeWritten
                    });
                }
                return infos;
            }
            catch (Exception ex)
            {
                LogService.LogError("获取系统事件失败，原因：" + ex.Message);
            }
            return null;
        }

        private void UpdateNetworkInteface()
        {
            LogService.LogDebug("正在获取网卡信息...", false);
            try
            {
                NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface networkInterface in networkInterfaces)
                {
                    if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    NetworkInfo info = new NetworkInfo(networkInterface);
                    info.Update();
                    lock (_allNetworkInfo)
                    {
                        AllNetworkInfo.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("获取网卡信息失败，原因：" + ex.Message);
            }
        }

        private void UpdateNetworkUsage()
        {
            LogService.LogDebug("正在获取网卡使用率...", false);
            try
            {
                lock (_allNetworkInfo)
                {
                    var infos = AllNetworkInfo;
                    if (infos != null)
                    {
                        foreach (var info in infos)
                        {
                            try
                            {
                                info.Update();
                            }
                            catch (Exception ex)
                            {
                                LogService.LogError("获取网卡使用率失败，原因：" + ex.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("获取网卡使用率失败，原因：" + ex.Message);
            }
        }
    }
}
