using Newtonsoft.Json;
using System;
using System.IO;

namespace DesktopAppDoctor.Config
{
    public class ConfigManager
    {
        public static readonly Lazy<ConfigManager> Instance = new Lazy<ConfigManager>(() => new ConfigManager());

        private readonly string CONST_CONFIG_FILE_PATH;
        private ConfigEntity _config;

        public ConfigEntity Config
        {
            get => _config;
            private set { _config = value; ConfigReferenceChanged?.Invoke(); }
        }
        public event Action ConfigReferenceChanged;

        private ConfigManager()
        {
            CONST_CONFIG_FILE_PATH = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Config.json");

            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(CONST_CONFIG_FILE_PATH))
                {
                    string configStr = File.ReadAllText(CONST_CONFIG_FILE_PATH);
                    Config = JsonConvert.DeserializeObject<ConfigEntity>(configStr);
                }
            }
            catch (Exception)
            {
            }
            if (Config == null)
            {
                Config = GenerateDefaultConfig();
                Save();
            }
        }

        private ConfigEntity GenerateDefaultConfig()
        {
            ConfigEntity config = new ConfigEntity();
            return config;
        }

        public void Save()
        {
            if (Config == null)
            {
                return;
            }
            try
            {
                string configStr = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(CONST_CONFIG_FILE_PATH, configStr);
            }
            catch (Exception ex)
            {
                LogManager.LogService.Instance.Value.LogError("保存配置文件出错，原因：" + ex.Message);
            }
        }
    }
}
