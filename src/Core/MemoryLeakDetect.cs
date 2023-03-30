using Common;
using LogManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Core
{
    public class MemoryLeakDetect
    {
        private long _memoryLimit;
        private Process _proc;

        public MemoryLeakDetect(int megabytes)
        {
            _proc = Process.GetCurrentProcess();
            _memoryLimit = megabytes * 1024 * 1024;
        }

        public bool CheckMemory()
        {
            long memorySize = _proc.WorkingSet64;

            if (memorySize > _memoryLimit)
            {
                string memorySizeStr = UnitHelper.GetStorageUnitString(memorySize);
                LogService.Instance.Value.LogDebug($"当前进程内存：{memorySizeStr}，已经超出了上限，即将重启...");

                string args = string.Empty;
                string[] tempArgs = Environment.GetCommandLineArgs();
                if (tempArgs != null)
                {
                    for (int i = 1; i < tempArgs.Length; i++)
                    {
                        string a = tempArgs[i];
                        args += $" {a}";
                    }
                }

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = _proc.MainModule.FileName;
                startInfo.Arguments = args;
                Process.Start(startInfo);

                Task.Delay(10 * 1000).ContinueWith(a => Environment.Exit(0));
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                Thread.Sleep(Timeout.Infinite);
            }

            return true;
        }
    }
}
