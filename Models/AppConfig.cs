using System;

namespace KittyFocus.Models
{
    /// <summary>
    /// 应用配置数据模型。模块一仅包含专注时长与托盘开关；
    /// 模块二将扩展黑名单等字段，复用同一个 JSON 文件。
    /// </summary>
    [Serializable]
    public class AppConfig
    {
        /// <summary>单次专注时长（分钟），范围 1~720，默认 25。</summary>
        public int FocusDurationMinutes { get; set; } = 25;

        /// <summary>是否启用系统托盘常驻。默认启用。</summary>
        public bool TrayEnabled { get; set; } = true;

        /// <summary>返回带默认值的全新配置实例。</summary>
        public static AppConfig CreateDefault() => new AppConfig();
    }
}
