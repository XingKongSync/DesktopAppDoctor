using Common;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DesktopAppDoctor.Config;
using Core;

namespace DesktopAppDoctor.ViewModels
{
    public class SettingsVM : BindableBase, IConfig
    {
        public ConfigManager ConfigManager { get => ConfigManager.Instance.Value; }
        public DelegateCommand SaveConfigCommand { get; private set; }
        public DelegateCommand RestoreConfigCommand { get; private set; } 
        public DelegateCommand BrowseDumpFolderCommand { get; private set; }

        private static readonly Dictionary<string, DumpLevel> _dumpLevels = new Dictionary<string, DumpLevel>() { { "完整 Dump", DumpLevel.Full }, { "最小 Dump", DumpLevel.Mini } };

        private bool _hasModified = false;

        public bool HasModified { get => _hasModified; set => SetProperty(ref _hasModified, value); }

        public Dictionary<string, DumpLevel> DumpLevels => _dumpLevels;

        #region Config Properties

        public DumpLevel DumpLevel { get => GetCacheProperty<DumpLevel>(); set => SetCacheProperty(value); }
        public string ProcessName { get => GetCacheProperty<string>(); set => SetCacheProperty(value); }
        public string DumpPath { get => GetCacheProperty<string>(); set => SetCacheProperty(value); }
        public bool HangDump { get => GetCacheProperty<bool>(); set => SetCacheProperty(value); }
        public bool AutoRun { get => GetCacheProperty<bool>(); set => SetCacheProperty(value); }
        public bool CrashDump { get => GetCacheProperty<bool>(); set => SetCacheProperty(value); }
        
        #endregion

        public SettingsVM()
        {
            SaveConfigCommand = new DelegateCommand(SaveConfigCommandHandler);
            RestoreConfigCommand = new DelegateCommand(RestoreConfigCommandHandler);
            BrowseDumpFolderCommand = new DelegateCommand(BrowseDumpFolderCommandHandler);
        }

        /// <summary>
        /// 放弃修改
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        private void RestoreConfigCommandHandler()
        {
            Discard();
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        private void SaveConfigCommandHandler()
        {
            Submit();
        }

        /// <summary>
        /// 设置导出文件夹
        /// </summary>
        private void BrowseDumpFolderCommandHandler()
        {
            var dialog = new CommonOpenFileDialog();
            dialog.Title = "请选择导出诊断信息所在文件夹";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                ConfigManager.Config.DumpPath = dialog.FileName;
            }
        }

        #region Cache Property Utils

        private Dictionary<string, object> _propertyCache = new Dictionary<string, object>();

        private void SetCacheProperty<T>(T value, [CallerMemberName] string propertyName = null)
        {
            T old = GetCacheProperty<T>(propertyName);

            if (old.Equals(value))
                return;

            _propertyCache[propertyName] = value;
            RaisePropertyChanged(propertyName);

            HasModified = true;
        }

        private T GetCacheProperty<T>([CallerMemberName] string propertyName = null)
        {
            if (_propertyCache.TryGetValue(propertyName, out object v))
            {
                return (T)v;
            }

            var property = typeof(ConfigEntity).GetProperty(propertyName);
            return (T)property.GetValue(ConfigManager.Config);
        }

        private void Discard()
        {
            var temp = _propertyCache.Keys.ToList();
            _propertyCache.Clear();

            HasModified = false;
            temp.ForEach(property => RaisePropertyChanged(property));
        }

        private void Submit()
        {
            foreach (var pair in _propertyCache)
            {
                string propertyName = pair.Key;
                object value = pair.Value;

                var propertyInfo = typeof(ConfigEntity).GetProperty(propertyName);
                propertyInfo.SetValue(ConfigManager.Config, value);
            }
            Discard();

            ConfigManager.Save();
        }

        #endregion
    }
}
