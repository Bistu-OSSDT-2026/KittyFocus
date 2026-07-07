using System;
using System.Collections.Generic;
using System.Linq;
using KittyFocus.Models;

namespace KittyFocus.Services
{
    /// <summary>匹配结果。</summary>
    public class BlacklistMatch
    {
        /// <summary>命中的规则类型：进程名 / 窗口标题。</summary>
        public string MatchType { get; set; }
        /// <summary>命中的具体规则文本。</summary>
        public string RuleText { get; set; }
    }

    /// <summary>
    /// 黑名单匹配逻辑。将 ProcessInfo 与配置中的黑名单比对，
    /// 返回所有命中项（可能同时命中进程名与窗口标题）。
    /// </summary>
    public class BlacklistService
    {
        private AppConfig _config;

        public BlacklistService(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>刷新配置引用（当外界修改配置后调用）。</summary>
        public void UpdateConfig(AppConfig config)
        {
            if (config != null) _config = config; // 故意引用更新
        }

        /// <summary>
        /// 检测给定 ProcessInfo 是否命中黑名单。
        /// 返回匹配结果列表；空列表 = 未命中。
        /// </summary>
        public List<BlacklistMatch> Check(ProcessInfo info)
        {
            var results = new List<BlacklistMatch>();

            string processKey = info?.ProcessName?.Trim().ToLowerInvariant() ?? "";

            // 1. 进程名校验（列表中可能带 .exe，统一去扩展名比较）
            foreach (var rule in _config.BlacklistedProcessNames)
            {
                string ruleKey = rule?.Trim().ToLowerInvariant() ?? "";
                if (string.IsNullOrEmpty(ruleKey)) continue;

                // 去 .exe 后缀
                if (ruleKey.EndsWith(".exe"))
                    ruleKey = ruleKey.Substring(0, ruleKey.Length - 4);

                if (processKey == ruleKey)
                {
                    results.Add(new BlacklistMatch { MatchType = "进程名", RuleText = rule });
                    break; // 进程名校验只取第一个匹配
                }
            }

            // 2. 窗口标题关键词校验
            string title = info?.WindowTitle ?? "";
            if (!string.IsNullOrEmpty(title))
            {
                foreach (var keyword in _config.BlacklistedWindowKeywords)
                {
                    if (string.IsNullOrEmpty(keyword)) continue;

                    if (title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(new BlacklistMatch { MatchType = "窗口标题", RuleText = keyword });
                        // 允许命中多个关键词
                    }
                }
            }

            return results;
        }
    }
}
