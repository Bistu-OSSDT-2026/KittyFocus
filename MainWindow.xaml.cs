using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KittyFocus.Models;
using KittyFocus.Services;
using KittyFocus.Tray;
using KittyFocus.Windows;

namespace KittyFocus
{
    /// <summary>
    /// MainWindow 的交互逻辑：专注控制台界面。
    /// 持有 FocusEngine / ConfigService / TrayIcon / ProcessWatcher / BlacklistService
    /// 五个协作对象，通过事件驱动刷新 UI 与检测流程。
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly FocusEngine _engine;
        private readonly ConfigService _configService;
        private readonly TrayIcon _trayIcon;
        private readonly ProcessWatcher _watcher;
        private readonly BlacklistService _blacklistService;

        private AppConfig _config;

        // 里程碑气泡去重标记（避免重复弹）
        private bool _halfNotified;
        private bool _fiveMinNotified;
        private bool _oneMinNotified;
        private bool _firstHideNotified;

        // ---- 模块二状态 ----
        /// <summary>专注开始时刻（用于 FR-08 缓冲期判断）。</summary>
        private DateTime _focusStartedAt;

        /// <summary>是否正在显示惩罚/警告对话框（排重）。</summary>
        private bool _isPenaltyInProgress;

        /// <summary>被「放弃运行」的进程冷却字典（进程名 → 冷却到期时间）。</summary>
        private readonly Dictionary<string, DateTime> _warnCooldowns = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private const int WarnCooldownSeconds = 15;
        private const int BufferPeriodSeconds = 180;   // FR-08

        // ---- SettingsWindow 单例 ----
        private SettingsWindow _settingsWindow;

        public MainWindow()
        {
            InitializeComponent();

            // 允许无边框窗口拖动
            this.MouseLeftButtonDown += (s, e) => DragMove();

            _configService = new ConfigService();
            _engine = new FocusEngine();
            _trayIcon = new TrayIcon();
            _watcher = new ProcessWatcher();

            LoadConfig();

            _blacklistService = new BlacklistService(_config);

            _engine.StateChanged += OnEngineStateChanged;
            _engine.Tick += OnEngineTick;
            _engine.Finished += OnEngineFinished;

            _watcher.Polled += OnProcessPolled;

            _trayIcon.ShowMainWindowRequested += (s, e) => RestoreWindow();
            _trayIcon.ExitRequested += (s, e) => ExitApp();

            if (_config.TrayEnabled)
                _trayIcon.Show();

            UpdateView();
        }

        private void LoadConfig()
        {
            _config = _configService.Load();
            _engine.SetDuration(_config.FocusDurationMinutes);
            DurationTextBox.Text = _config.FocusDurationMinutes.ToString();
        }

        // ---- FR-01 / FR-02：开始专注 ----
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(DurationTextBox.Text, out int minutes) ||
                !_engine.SetDuration(minutes))
            {
                _trayIcon.ShowBubble("时长无效", "请输入 1 ~ 720 分钟之间的数字。");
                DurationTextBox.Text = _engine.FocusDurationMinutes.ToString();
                return;
            }

            _config.FocusDurationMinutes = minutes;
            _configService.Save(_config);

            ResetMilestones();
            _engine.Start();

            // ---- 模块二：启动进程轮询 ----
            _focusStartedAt = DateTime.UtcNow;
            _warnCooldowns.Clear();
            _watcher.Start();
        }

        // ---- FR-01：强制结束 ----
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _watcher.Stop();
            _engine.ForceStop();
            _trayIcon.ShowBubble("已中止", "🐱 猫咪松了口气，专注已中止。");
        }

        // ---- ⚙ 设置按钮（FR-04） ----
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Focus();
                return;
            }

            _settingsWindow = new SettingsWindow(_config, () =>
            {
                _configService.Save(_config);
                _blacklistService.UpdateConfig(_config);
            });

            _settingsWindow.Closed += (s, args) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        // ---- 引擎事件 ----
        private void OnEngineStateChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // 专注结束（强制结束或已完成）→ 停止轮询
                if (_engine.State != FocusState.Running)
                    _watcher.Stop();

                UpdateView();
            });
        }

        private void OnEngineTick(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                CountdownText.Text = FormatTime(_engine.RemainingSeconds);
                UpdateProgressRing();
                _trayIcon.UpdateTooltip(BuildTooltip());

                if (!IsVisible)
                    CheckMilestones();
            });
        }

        private void OnEngineFinished(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _watcher.Stop();
                UpdateView();
                _trayIcon.ShowBubble("专注完成", "🎉 专注完成！猫咪很满意~");
                _trayIcon.UpdateTooltip("🐱 专注完成，猫咪很满意");
            });
        }

        // ================================================================
        // 模块二核心：进程轮询检测 → 黑名单匹配 → 缓冲/警告/惩罚
        // ================================================================

        /// <summary>
        /// ProcessWatcher 轮询回调（线程池线程）。
        /// 将匹配与 UI 流程跳转到 UI 线程处理。
        /// </summary>
        private void OnProcessPolled(object sender, ProcessInfo info)
        {
            if (_isPenaltyInProgress) return;

            var matches = _blacklistService.Check(info);
            if (matches.Count == 0) return;

            // 有命中 → 跳转到 UI 线程统一处理
            Dispatcher.Invoke(() => HandleBlacklistMatch(info, matches));
        }

        /// <summary>
        /// 处理黑名单命中（UI 线程）。
        /// FR-08: 缓冲期内仅 Toast
        /// FR-07: 非缓冲期 → 警告对话框
        /// FR-06: 牺牲 → 全屏惩罚
        /// </summary>
        private void HandleBlacklistMatch(ProcessInfo info, List<BlacklistMatch> matches)
        {
            if (_isPenaltyInProgress) return;
            if (_engine.State != FocusState.Running) return;

            // ---- FR-08：缓冲期检查 ----
            double elapsedSeconds = (DateTime.UtcNow - _focusStartedAt).TotalSeconds;
            if (elapsedSeconds < BufferPeriodSeconds)
            {
                string matchDesc = string.Join("、", matches.Select(m => $"[{m.MatchType}:{m.RuleText}]"));
                _trayIcon.ShowBubble("注意（缓冲期）",
                    $"检测到黑名单：{matchDesc}。专注前 3 分钟仅提醒，不触发惩罚。");
                return;
            }

            // ---- 冷却检查：用户刚点过「放弃运行」的同名进程 ----
            if (_warnCooldowns.TryGetValue(info.ProcessName, out DateTime cooldownUntil))
            {
                if (DateTime.UtcNow < cooldownUntil)
                    return; // 还在冷却中，静默跳过
                else
                    _warnCooldowns.Remove(info.ProcessName);
            }

            // ---- FR-07：警告对话框 ----
            _isPenaltyInProgress = true;

            try
            {
                // 恢复主窗口（让用户看到情况）
                if (!IsVisible)
                    RestoreWindow();

                var warnDialog = new WarnDialog($"{info.ProcessName}.exe");
                warnDialog.Owner = this;
                bool? warnResult = warnDialog.ShowDialog();

                if (warnResult == true && warnDialog.Sacrificed)
                {
                    // ---- FR-06：牺牲 → 全屏惩罚遮罩 ----
                    var deathOverlay = new DeathOverlay($"{info.ProcessName}.exe", info.ProcessId);
                    deathOverlay.ShowDialog();
                    // 用户已操作（已知晓/关闭程序），继续
                }
                else
                {
                    // 用户选择「放弃运行」→ 冷却期内不再警告同进程
                    _warnCooldowns[info.ProcessName] = DateTime.UtcNow.AddSeconds(WarnCooldownSeconds);
                }
            }
            finally
            {
                _isPenaltyInProgress = false;
            }
        }

        // ---- UI 状态联动 ----
        private void UpdateView()
        {
            bool running = _engine.State == FocusState.Running;

            StartButton.IsEnabled = !running;
            StopButton.IsEnabled = running;
            DurationTextBox.IsEnabled = !running;
            BlacklistButton.IsEnabled = !running; // 专注中禁设

            switch (_engine.State)
            {
                case FocusState.Idle:
                    HintText.Text = "设置时长后点击开始";
                    CountdownText.Text = FormatTime(_engine.TotalSeconds);
                    StartButton.Content = "开始专注";
                    UpdateProgressRing();
                    _trayIcon.UpdateTooltip("🐱 空闲中 — 专注猫灵");
                    break;
                case FocusState.Running:
                    HintText.Text = "坚持住，猫咪在守护你";
                    CountdownText.Text = FormatTime(_engine.RemainingSeconds);
                    StartButton.Content = "专注中";
                    UpdateProgressRing();
                    _trayIcon.UpdateTooltip(BuildTooltip());
                    break;
                case FocusState.Finished:
                    HintText.Text = "太棒了！可点击「再来一轮」";
                    CountdownText.Text = "00:00";
                    StartButton.Content = "再来一轮";
                    UpdateProgressRing();
                    break;
            }
        }

        // ---- 里程碑气泡 ----
        private void CheckMilestones()
        {
            int remain = _engine.RemainingSeconds;
            int half = _engine.TotalSeconds / 2;

            if (!_halfNotified && remain <= half && remain > 300)
            {
                _halfNotified = true;
                _trayIcon.ShowBubble("进度过半", $"🐱 已过半，剩余 {FormatTime(remain)}。");
            }
            else if (!_fiveMinNotified && remain <= 300 && remain > 60)
            {
                _fiveMinNotified = true;
                _trayIcon.ShowBubble("剩余 5 分钟", "🐱 加油，快完成了！");
            }
            else if (!_oneMinNotified && remain <= 60 && remain > 0)
            {
                _oneMinNotified = true;
                _trayIcon.ShowBubble("最后 1 分钟", "🐱 冲刺！");
            }
        }

        private void ResetMilestones()
        {
            _halfNotified = false;
            _fiveMinNotified = false;
            _oneMinNotified = false;
        }

        // ---- FR-03：关闭按钮拦截 → 驻留托盘 ----
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_trayIcon != null)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;

                if (!_firstHideNotified)
                {
                    _firstHideNotified = true;
                    _trayIcon.ShowBubble("我还在这里",
                        "双击图标恢复窗口，右键可选择退出。");
                }
            }
            base.OnClosing(e);
        }

        private void RestoreWindow()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApp()
        {
            _watcher.Stop();
            _engine.ForceStop();
            _trayIcon.Dispose();
            Application.Current.Shutdown();
        }

        // ---- FR-02：时长输入只允许数字 ----
        private void DurationTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void DurationTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = (string)e.DataObject.GetData(DataFormats.Text);
                if (!Regex.IsMatch(text, "^[0-9]+$"))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        // ---- 工具方法 ----
        private static string FormatTime(int totalSeconds)
        {
            int m = totalSeconds / 60;
            int s = totalSeconds % 60;
            return $"{m:D2}:{s:D2}";
        }

        private string BuildTooltip()
        {
            if (_engine.State == FocusState.Running)
                return $"🐱 专注中 {FormatTime(_engine.RemainingSeconds)}";
            return "🐱 空闲中 — 专注猫灵";
        }

        /// <summary>
        /// 更新环状进度条：根据剩余时间比例计算圆弧终点，
        /// 实现圆环随计时减少而逆时针消失的效果。
        /// 坐标系与 XAML 中 Canvas 一致：圆心(50,50)，半径44，顶部起点(50,6)。
        /// 前景弧与背景 Ellipse（Left=6, Top=6, W=88, H=88）完全同心同径。
        /// </summary>
        private void UpdateProgressRing()
        {
            if (_engine.TotalSeconds <= 0)
            {
                ProgressArc.Data = Geometry.Parse("M 50,6 A 44,44 0 0 1 50,6");
                return;
            }

            double progress = (double)_engine.RemainingSeconds / _engine.TotalSeconds;
            // 从顶部(50,6)顺时针绘制弧线
            double angle = progress * 360.0;

            if (progress >= 0.999)
            {
                // 满圆：用 359.9° 代替（ArcSegment 不支持 360°）
                double nearEndAngle = 359.9 * Math.PI / 180.0;
                double ex = 50 + 44 * Math.Sin(nearEndAngle);
                double ey = 50 - 44 * Math.Cos(nearEndAngle);
                ProgressArc.Data = Geometry.Parse($"M 50,6 A 44,44 0 1 1 {ex:0.###},{ey:0.###}");
            }
            else if (progress <= 0.001)
            {
                ProgressArc.Data = Geometry.Parse("M 50,6 A 44,44 0 0 1 50,6");
            }
            else
            {
                double rad = angle * Math.PI / 180.0;
                double ex = 50 + 44 * Math.Sin(rad);
                double ey = 50 - 44 * Math.Cos(rad);
                string largeArc = angle > 180.0 ? "1" : "0";
                ProgressArc.Data = Geometry.Parse($"M 50,6 A 44,44 0 {largeArc} 1 {ex:0.###},{ey:0.###}");
            }
        }
    }
}
