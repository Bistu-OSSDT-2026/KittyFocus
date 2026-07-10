using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace KittyFocus.Windows
{
    /// <summary>
    /// FR-07 警告对话框 + 进程自动检测。
    /// 检测到违规进程运行时弹出倒计时警告。
    /// 用户关闭违规进程 → 弹窗自动关闭（成功）。
    /// 倒计时归零时进程仍在运行 → 通知调用方触发惩罚（FR-06）。
    /// 无任何手动按钮，全靠用户实际行动响应。
    /// </summary>
    public partial class WarnDialog : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly string _processKey; // 小写无后缀，供 Process.GetProcessesByName 使用
        private readonly int _livesRemaining; // 剩余命数（-1 表示未知/不显示）
        private int _secondsRemaining = 30;

        /// <summary>true = 用户主动关闭了违规进程。false = 超时未关。</summary>
        public bool ProcessClosedByUser { get; private set; }

        /// <param name="processDisplayName">界面显示名，如 "notepad.exe"。</param>
        /// <param name="livesRemaining">当前剩余命数（用于提示用户超时代价）。-1 表示不显示。</param>
        public WarnDialog(string processDisplayName, int livesRemaining = -1)
        {
            InitializeComponent();

            ProcessNameText.Text = processDisplayName;
            _livesRemaining = livesRemaining;

            // 提取无后缀进程名用于监测
            _processKey = processDisplayName?.Trim().ToLowerInvariant() ?? "";
            if (_processKey.EndsWith(".exe"))
                _processKey = _processKey.Substring(0, _processKey.Length - 4);

            _timer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();

            UpdateCountdownDisplay();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _secondsRemaining--;
            UpdateCountdownDisplay();

            // 每秒同步检测违规进程是否已退出
            if (!IsProcessAlive())
            {
                // 用户关闭了违规程序 → 自动关闭弹窗
                _timer.Stop();
                ProcessClosedByUser = true;
                DialogResult = true;
                Close();
                return;
            }

            // 倒计时归零且进程仍在运行 → 通知调用方触发惩罚
            if (_secondsRemaining <= 0)
            {
                _timer.Stop();
                ProcessClosedByUser = false;
                DialogResult = false;
                Close();
            }
        }

        /// <summary>检测同名进程是否仍在运行。</summary>
        private bool IsProcessAlive()
        {
            if (string.IsNullOrEmpty(_processKey)) return false;
            try
            {
                return Process.GetProcessesByName(_processKey).Length > 0;
            }
            catch
            {
                // 无权限等情况保守返回 true（视为还活着）
                return true;
            }
        }

        private void UpdateCountdownDisplay()
        {
            CountdownText.Text = _secondsRemaining > 0
                ? _secondsRemaining.ToString()
                : "⚠";

            string baseHint = _secondsRemaining > 0
                ? "请关闭违规程序以保护猫咪 ✨"
                : "时间到…";

            // 追加命数提示，让用户意识到超时代价
            if (_livesRemaining >= 0)
            {
                string lifeHint = _livesRemaining == 0
                    ? "⚠ 超时将直接导致猫咪死亡！"
                    : $"超时将失去 1 条命（当前剩余 {_livesRemaining} 条）";
                StatusHint.Text = $"{baseHint}\n{lifeHint}";
            }
            else
            {
                StatusHint.Text = baseHint;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}
