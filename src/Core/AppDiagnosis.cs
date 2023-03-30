using Common;
using LogManager;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;

namespace Core
{
    public class AppDiagnosis : BindableBase
    {
        private const string CONST_REG_LOCAL_DUMPS_PATH = "SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\\LocalDumps";
        private Dispatcher _mainDispatcher;
        private LogService LogService = LogService.Instance.Value;

        private string _processName;//DCCK.VisionPlus.GStudio
        private Dictionary<int, AppHandler> _appHandlers = new Dictionary<int, AppHandler>();

        private ObservableCollection<AppHandler> _applications;
        public ObservableCollection<AppHandler> Applications { get => _applications; set => SetProperty(ref _applications, value); }
        public string ProcessName { get => _processName; set => SetProperty(ref _processName, value); }

        public AppDiagnosis(string processName)
        {
            _mainDispatcher = Dispatcher.CurrentDispatcher;
            ProcessName = processName;

            Applications = new ObservableCollection<AppHandler>();
            BindingOperations.EnableCollectionSynchronization(_applications, _applications);

            UpdateAlmostStatic();
        }


        public void UpdateAlmostStatic()
        {
            bool locked = false;
            try
            {
                locked = Monitor.TryEnter(_appHandlers);
                if (!locked)
                    return;

                LogService.LogDebug("正在刷新进程列表...");
                Process[] process = Process.GetProcessesByName(ProcessName);
                if (process != null)
                {
                    List<AppHandler> tobeAdded = new List<AppHandler>();
                    List<AppHandler> tobeRemoved = new List<AppHandler>();
                    foreach (var proc in process)
                    {
                        if (!_appHandlers.TryGetValue(proc.Id, out AppHandler handler))
                        {
                            handler = new AppHandler(proc, _mainDispatcher);
                            _appHandlers.Add(proc.Id, handler);
                            tobeAdded.Add(handler);
                        }
                    }

                    foreach (var pair in _appHandlers)
                    {
                        var pid = pair.Key;
                        var handler = pair.Value;
                        if (process?.Any(p => p.Id == pid) != true)
                        {
                            tobeRemoved.Add(handler);
                        }
                    }
                    foreach (var h in tobeRemoved)
                    {
                        _appHandlers.Remove(h.Pid);
                    }

                    lock (_applications)
                    {
                        foreach (var item in tobeAdded)
                        {
                            _applications.Add(item);
                        }
                        foreach (var item in tobeRemoved)
                        {
                            _applications.Remove(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("刷新进程列表出错，原因：" + ex.Message);
            }
            finally
            {
                if (locked)
                    Monitor.Exit(_appHandlers);
            }
        }

        public void UpdateDynamic()
        {
            bool locked = false;
            try
            {
                locked = Monitor.TryEnter(_appHandlers);
                if (!locked)
                    return;

                foreach (var handler in _appHandlers.Values)
                {
                    try
                    {
                        handler.Update();
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError($"刷新进程信息出错，Pid: {handler?.Pid}，原因：{ex.Message}");
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (locked)
                    Monitor.Exit(_appHandlers);
            }
        }

        public List<AppHandler> TestResponding(int seconds = 10)
        {
            bool locked = false;
            List<AppHandler> result = new List<AppHandler>();
            try
            {
                locked = Monitor.TryEnter(_appHandlers);
                if (!locked)
                    return result;

                foreach (var handler in _appHandlers.Values)
                {
                    if (!handler.TestResponding(seconds))
                    {
                        result.Add(handler);
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (locked)
                    Monitor.Exit(_appHandlers);
            }
            return result;
        }

        private int GetPidFromDumpFileName(string dumpFileName)
        {
            if (string.IsNullOrEmpty(dumpFileName))
                return -1;

            var parts = dumpFileName.Split('.');
            if (parts == null || parts.Length < 2)
                return -1;

            if (int.TryParse(parts[parts.Length - 2], out int pid))
            {
                return pid;
            }
            return -1;
        }

        public List<(FileInfo, AppHandler)> CheckCrash()
        {
            RegistryKey reg = null;
            List<(FileInfo, AppHandler)> result = new List<(FileInfo, AppHandler)>();
            try
            {
                reg = Registry.LocalMachine.OpenSubKey(CONST_REG_LOCAL_DUMPS_PATH, false);
                if (null == reg)
                    return result;

                //找到系统 Dump 目录
                if (reg.GetValue("DumpFolder") is string dumpFolder)
                {
                    if (dumpFolder.Contains("%"))
                    {
                        dumpFolder = Environment.ExpandEnvironmentVariables(dumpFolder);
                    }

                    //查找有没有相关的 Dump 文件
                    DirectoryInfo dir = new DirectoryInfo(dumpFolder);
                    if (dir.Exists)
                    {
                        var files = dir.GetFiles("*.dmp", SearchOption.TopDirectoryOnly);
                        if (files != null)
                        {
                            //遍历所有 dump 文件，并匹配文件名
                            var dumpInfos = from f in files
                                            where f.Name.StartsWith(ProcessName)
                                            select (f, GetPidFromDumpFileName(f.Name));
                            lock (_appHandlers)
                            {
                                foreach (var info in dumpInfos)
                                {
                                    (FileInfo file, int pid) = info;
                                    if (_appHandlers.TryGetValue(pid, out var handle))
                                    {
                                        result.Add((file, handle));
                                    }
                                    else
                                    {
                                        result.Add((file, null));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Value.LogError($"检查 Crash Dump 失败，原因：{ex.Message}");
            }
            finally
            {
                reg?.Close();
            }
            return result;
        }

        public void Export(AppHandler handler, string dir)
        {
            lock (_appHandlers)
            {
                //foreach (var handler in _appHandlers.Values)
                //{
                    try
                    {
                        handler.Export(dir);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError($"导出进程信息，Pid: {handler?.Pid}，原因：{ex.Message}");
                    }
                //}
            }
        }
    }
}
