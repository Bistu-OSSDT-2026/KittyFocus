using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using KittyFocus.Models;

namespace KittyFocus.Windows
{
    /// <summary>
    /// FR-04 黑名单管理窗口。支持增删进程名与窗口标题关键词。
    /// 所有修改即时同步到 AppConfig（通过回调），由调用方持久化。
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly AppConfig _config;
        private readonly Action _saveCallback;

        public SettingsWindow(AppConfig config, Action saveCallback)
        {
            InitializeComponent();

            _config = config ?? throw new ArgumentNullException(nameof(config));
            _saveCallback = saveCallback ?? throw new ArgumentNullException(nameof(saveCallback));

            Owner = Application.Current.MainWindow;

            LoadLists();
        }

        private void LoadLists()
        {
            // 进程名列表
            ProcessListBox.ItemsSource = null;
            ProcessListBox.ItemsSource = _config.BlacklistedProcessNames.ToList();

            // 关键词列表
            KeywordListBox.ItemsSource = null;
            KeywordListBox.ItemsSource = _config.BlacklistedWindowKeywords.ToList();
        }

        private void RefreshProcessList()
        {
            ProcessListBox.ItemsSource = null;
            ProcessListBox.ItemsSource = _config.BlacklistedProcessNames.ToList();
        }

        private void RefreshKeywordList()
        {
            KeywordListBox.ItemsSource = null;
            KeywordListBox.ItemsSource = _config.BlacklistedWindowKeywords.ToList();
        }

        // ---- 进程名操作 ----
        private void AddProcess_Click(object sender, RoutedEventArgs e)
        {
            string input = ProcessInput.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                ProcessInput.Focus();
                return;
            }

            // 统一补 .exe 后缀（若用户未输入）
            string normalized = input.ToLowerInvariant();
            if (!normalized.EndsWith(".exe"))
                input += ".exe";

            // 去重
            if (_config.BlacklistedProcessNames.Any(
                    p => p.Equals(input, StringComparison.OrdinalIgnoreCase)))
            {
                ProcessInput.Clear();
                ProcessInput.Focus();
                return;
            }

            _config.BlacklistedProcessNames.Add(input);
            _saveCallback();
            RefreshProcessList();
            ProcessInput.Clear();
            ProcessInput.Focus();
        }

        private void DeleteProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string processName)
            {
                _config.BlacklistedProcessNames.Remove(processName);
                _saveCallback();
                RefreshProcessList();
            }
        }

        // ---- 关键词操作 ----
        private void AddKeyword_Click(object sender, RoutedEventArgs e)
        {
            string input = KeywordInput.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                KeywordInput.Focus();
                return;
            }

            // 去重
            if (_config.BlacklistedWindowKeywords.Any(
                    k => k.Equals(input, StringComparison.OrdinalIgnoreCase)))
            {
                KeywordInput.Clear();
                KeywordInput.Focus();
                return;
            }

            _config.BlacklistedWindowKeywords.Add(input);
            _saveCallback();
            RefreshKeywordList();
            KeywordInput.Clear();
            KeywordInput.Focus();
        }

        private void DeleteKeyword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string keyword)
            {
                _config.BlacklistedWindowKeywords.Remove(keyword);
                _saveCallback();
                RefreshKeywordList();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
