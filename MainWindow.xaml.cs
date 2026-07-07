using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KittyFocus.Models;
using KittyFocus.Services;
using KittyFocus.Tray;

namespace KittyFocus
{
    /// <summary>
    /// MainWindow 的交互逻辑：专注控制台界面。
    /// 持有 FocusEngine / ConfigService / TrayIcon 三个协作对象，
    /// 通过事件驱动刷新 UI 与托盘状态。
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly FocusEngine _engine;
        private readonly ConfigService _configService;
        private readonly TrayIcon _trayIcon;
        private AppConfig _config;

        // 里程碑气泡去重标记（避免重复弹）
        private bool _halfNotified;
        private bool _fiveMinNotified;
        private bool _oneMinNotified;
        private bool _firstHideNotified;

        public MainWindow()
        {
            InitializeComponent();

            _configService = new ConfigService();
            _engine = new FocusEngine();
            _trayIcon = new TrayIcon();

            LoadConfig();

            _engine.StateChanged += OnEngineStateChanged;
            _engine.Tick += OnEngineTick;
            _engine.Finished += OnEngineFinished;

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
            // 先把输入框值同步到引擎并校验。
            if (!int.TryParse(DurationTextBox.Text, out int minutes) ||
                !_engine.SetDuration(minutes))
            {
                _trayIcon.ShowBubble("时长无效", "请输入 1 ~ 720 分钟之间的数字。");
                DurationTextBox.Text = _engine.FocusDurationMinutes.ToString();
                return;
            }

            // 合法则持久化新时长。
            _config.FocusDurationMinutes = minutes;
            _configService.Save(_config);

            ResetMilestones();
            _engine.Start();
        }

        // ---- FR-01：强制结束 ----
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _engine.ForceStop();
            if (_trayIcon != null)
                _trayIcon.ShowBubble("已中止", "🐱 猫咪松了口气，专注已中止。");
        }

        // ---- 引擎事件 ----
        private void OnEngineStateChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(UpdateView);
        }

        private void OnEngineTick(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                CountdownText.Text = FormatTime(_engine.RemainingSeconds);
                _trayIcon.UpdateTooltip(BuildTooltip());

                // 仅当窗口隐藏时弹里程碑气泡，避免打扰。
                if (!IsVisible)
                    CheckMilestones();
            });
        }

        private void OnEngineFinished(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateView();
                _trayIcon.ShowBubble("专注完成", "🎉 专注完成！猫咪很满意~");
                _trayIcon.UpdateTooltip("🐱 专注完成，猫咪很满意");
            });
        }

        // ---- UI 状态联动 ----
        private void UpdateView()
        {
            bool running = _engine.State == FocusState.Running;

            StartButton.IsEnabled = !running;
            StopButton.IsEnabled = running;
            DurationTextBox.IsEnabled = !running;

            switch (_engine.State)
            {
                case FocusState.Idle:
                    StatusText.Text = "空闲中";
                    HintText.Text = "设置时长后点击开始";
                    CountdownText.Text = FormatTime(_engine.TotalSeconds);
                    StartButton.Content = "开始专注";
                    _trayIcon.UpdateTooltip("🐱 空闲中 — 专注猫灵");
                    break;
                case FocusState.Running:
                    StatusText.Text = "专注中…";
                    HintText.Text = "坚持住，猫咪在守护你";
                    CountdownText.Text = FormatTime(_engine.RemainingSeconds);
                    StartButton.Content = "专注中";
                    _trayIcon.UpdateTooltip(BuildTooltip());
                    break;
                case FocusState.Finished:
                    StatusText.Text = "已完成";
                    HintText.Text = "太棒了！可点击「再来一轮」";
                    CountdownText.Text = "00:00";
                    StartButton.Content = "再来一轮";
                    break;
            }
        }

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
            // 真正退出时取消关闭拦截。
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
    }
}
