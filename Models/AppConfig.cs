using System;
using System.Collections.Generic;

namespace KittyFocus.Models
{
    /// <summary>
    /// 应用配置数据模型。模块一：专注时长 + 托盘开关；
    /// 模块二扩展：黑名单进程名与窗口标题关键词列表。
    /// </summary>
    [Serializable]
    public class AppConfig
    {
        /// <summary>单次专注时长（分钟），范围 1~720，默认 25。</summary>
        public int FocusDurationMinutes { get; set; } = 25;

        /// <summary>是否启用系统托盘常驻。默认启用。</summary>
        public bool TrayEnabled { get; set; } = true;

        // ---- 模块二：黑名单 ----

        /// <summary>黑名单进程名列表（含扩展名如 "League of Legends.exe"）。</summary>
        public List<string> BlacklistedProcessNames { get; set; } = new List<string>();

        /// <summary>黑名单窗口标题关键词列表。</summary>
        public List<string> BlacklistedWindowKeywords { get; set; } = new List<string>();

        /// <summary>返回带默认值的全新配置实例。</summary>
        public static AppConfig CreateDefault() => new AppConfig();
    }
}
