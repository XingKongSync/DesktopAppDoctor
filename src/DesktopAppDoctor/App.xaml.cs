using LogManager;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DesktopAppDoctor
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon = null;

        public App() : base()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Exit += App_Exit;
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            LogService.Instance.Value.Dispose();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            MountExceptionHandler();
            InitLogHelper();

            _ = MainWindowVM.Instance;
            CreateTrayIcon();

            if (!e.Args.Any(arg=> arg?.ToLower()?.Equals("--hide") == true))
                ShowMainWindow();

            base.OnStartup(e);
        }


        private void ShowMainWindow()
        {
            var mainWnd = DesktopAppDoctor.MainWindow.Instance;
            mainWnd.Show();
            mainWnd.Activate();
        }

        #region HanlderException

        private void MountExceptionHandler()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            LogManager.LogService.Instance.Value.LogError($"发生未处理的异常，异常类型：{e.ExceptionObject.GetType().Name}，原因：{ex?.Message}，堆栈：\r\n{ex?.StackTrace}");
        }

        #endregion

        #region LogHelper

        private void InitLogHelper()
        {
            _ = LogManagerForUI.Instance;//在主线程上初始化 LogManagerForUI
            LogManager.LogService.Instance.Value.LogPrinted += LogManager_LogPrinted;
        }

        private void LogManager_LogPrinted(string obj)
        {
            LogManagerForUI.Instance.AddCoreLog(obj);
        }

        #endregion

        #region TrayHelper

        private void CreateTrayIcon()
        {
            if (_notifyIcon == null)
            {
                //Icon
                System.Drawing.Icon icon = DesktopAppDoctor.Properties.Resources.Icon;

                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
                _notifyIcon.Icon = icon;
                _notifyIcon.Visible = true;

                System.Windows.Forms.MenuItem open = new System.Windows.Forms.MenuItem("显示");
                System.Windows.Forms.MenuItem close = new System.Windows.Forms.MenuItem("退出");
                open.Click += Open_Click;
                close.Click += Close_Click;
                open.DefaultItem = true;

                _notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new System.Windows.Forms.MenuItem[] { open, close });
                _notifyIcon.DoubleClick += Open_Click;
            }
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void Close_Click(object sender, EventArgs e)
        {
            LogManager.LogService.Instance.Value.Dispose();
            Current.Shutdown();
        }

        private void Open_Click(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }
        #endregion
    }

}
