using System;
using System.IO;
using System.Web.Script.Serialization;
using KittyFocus.Models;

namespace KittyFocus.Services
{
    /// <summary>
    /// 基于 JSON 的配置读写服务。配置文件 kittyfocus.config.json
    /// 与可执行文件同目录，模块二将复用同一文件存储黑名单。
    /// </summary>
    public class ConfigService
    {
        private const string ConfigFileName = "kittyfocus.config.json";

        private readonly string _configPath;
        private readonly JavaScriptSerializer _serializer;

        public ConfigService()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            _serializer = new JavaScriptSerializer();
        }

        /// <summary>加载配置。文件缺失或损坏时返回默认配置，绝不抛异常。</summary>
        public AppConfig Load()
        {
            try
            {
                if (!File.Exists(_configPath))
                    return AppConfig.CreateDefault();

                string json = File.ReadAllText(_configPath);
                AppConfig config = _serializer.Deserialize<AppConfig>(json);
                if (config == null)
                    return AppConfig.CreateDefault();

                // 防御：超出范围时回退到默认值。
                if (config.FocusDurationMinutes < 1 || config.FocusDurationMinutes > 720)
                    config.FocusDurationMinutes = 25;

                return config;
            }
            catch
            {
                // 任何 IO/反序列化异常都不影响启动。
                return AppConfig.CreateDefault();
            }
        }

        /// <summary>保存配置（原子写：先写临时文件再替换，避免半写损坏）。</summary>
        public void Save(AppConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            try
            {
                string json = _serializer.Serialize(config);
                string tempPath = _configPath + ".tmp";

                File.WriteAllText(tempPath, json);
                if (File.Exists(_configPath))
                    File.Replace(tempPath, _configPath, null);
                else
                    File.Move(tempPath, _configPath);
            }
            catch
            {
                // 保存失败不应中断用户操作（如开始专注），静默忽略。
            }
        }
    }
}
