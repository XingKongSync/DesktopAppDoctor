using Common;
using Core;
using LogManager;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopAppDoctor.Config
{
    public interface IConfig
    {
        DumpLevel DumpLevel { get; set; }

        string ProcessName { get; set; }

        string DumpPath { get; set; }

        bool HangDump { get; set; }

        [JsonIgnore]
        bool AutoRun { get; set; }

        [JsonIgnore]
        bool CrashDump { get; set; }
    }

    public class ConfigEntity : BindableBase, IConfig
    {
        private DumpLevel _dumpLevel = DumpLevel.Full;
        private string _processName = "TestApp";
        private string _dumpPath = @"D:\DesktopAppDoctor";
        private bool _autoDump = true;
        private bool _autoRun = false;

        private const string CONST_TASK_NAME = nameof(DesktopAppDoctor);

        private const string CONST_REG_LOCAL_DUMPS_PATH = "SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\\LocalDumps";

        /// <summary>
        /// Dump 日志的等级
        /// </summary>
        public DumpLevel DumpLevel { get => _dumpLevel; set => SetProperty(ref _dumpLevel, value); }

        /// <summary>
        /// 进程名称（不含.exe）
        /// </summary>
        public string ProcessName { get => _processName; set => SetProperty(ref _processName, value); }


        /// <summary>
        /// 导出路径
        /// </summary>
        public string DumpPath { get => _dumpPath; set => SetProperty(ref _dumpPath, value); }

        /// <summary>
        /// 自动导出
        /// </summary>
        public bool HangDump { get => _autoDump; set => SetProperty(ref _autoDump, value); }

        [JsonIgnore]
        public bool AutoRun
        {
            get => _autoRun;
            set
            {
                if (SetProperty(ref _autoRun, value))
                {

                    try
                    {
                        if (value)
                        {
                            Scheduler.InstallAutoRunClient(CONST_TASK_NAME, Process.GetCurrentProcess().MainModule.FileName, CONST_TASK_NAME, "--hide");
                        }
                        else
                        {
                            Scheduler.UninstallAutoRunClient(CONST_TASK_NAME);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (value)
                        {
                            LogService.Instance.Value.LogError($"配置开机自启时发生错误，原因：{ex.Message}");
                        }
                        else
                        {
                            LogService.Instance.Value.LogError($"取消开机自启时发生错误，原因：{ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 设置或者取消自动 Dump
        /// </summary>
        [JsonIgnore]
        public bool CrashDump
        {
            get
            {
                RegistryKey reg = null;
                try
                {
                    reg = Registry.LocalMachine.OpenSubKey(CONST_REG_LOCAL_DUMPS_PATH, false);
                    return reg != null;
                }
                catch (Exception ex)
                {
                    LogService.Instance.Value.LogError($"读取注册表失败，原因：{ex.Message}");
                }
                finally
                {
                    reg?.Close();
                }
                return false;
            }

            set
            {
                RegistryKey reg = null;
                try
                {
                    if (value)
                    {
                        reg = Registry.LocalMachine.CreateSubKey(CONST_REG_LOCAL_DUMPS_PATH, true);
                        reg.SetValue("DumpCount", 10, RegistryValueKind.DWord);
                        reg.SetValue("DumpFolder", "%LOCALAPPDATA%\\CrashDumps", RegistryValueKind.ExpandString);
                    }
                    else
                    {
                        Registry.LocalMachine.DeleteSubKeyTree(CONST_REG_LOCAL_DUMPS_PATH);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Value.LogError($"写入注册表失败，原因：{ex.Message}");
                }
                finally
                {
                    reg?.Close();
                }
            }
        }


        public ConfigEntity()
        {
            _autoRun = Scheduler.TryGetTask(CONST_TASK_NAME, out _);
        }
    }
}
