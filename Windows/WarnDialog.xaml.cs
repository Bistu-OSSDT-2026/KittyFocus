using System;
using System.Windows;
using System.Windows.Threading;

namespace KittyFocus.Windows
{
    /// <summary>
    /// FR-07 警告对话框：检测到黑名单程序运行时弹出，
    /// 倒计时 3 秒，用户可选择「放弃运行」或「牺牲猫咪」。
    /// 超时自动视为「牺牲猫咪」。
    /// </summary>
    public partial class WarnDialog : Window
    {
        private readonly DispatcherTimer _countdownTimer;
        private int _secondsRemaining = 3;

        /// <summary>true = 用户选择牺牲猫咪（含超时）。</summary>
        public bool Sacrificed { get; private set; }

        public WarnDialog(string processName)
        {
            InitializeComponent();

            ProcessNameText.Text = processName;

            _countdownTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += OnCountdownTick;
            _countdownTimer.Start();

            UpdateCountdownDisplay();
        }

        private void OnCountdownTick(object sender, EventArgs e)
        {
            _secondsRemaining--;
            UpdateCountdownDisplay();

            if (_secondsRemaining <= 0)
            {
                _countdownTimer.Stop();
                Sacrificed = true;
                DialogResult = true;
                Close();
            }
        }

        private void UpdateCountdownDisplay()
        {
            CountdownText.Text = _secondsRemaining.ToString();
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            _countdownTimer.Stop();
            Sacrificed = false;
            DialogResult = false;
            Close();
        }

        private void SacrificeButton_Click(object sender, RoutedEventArgs e)
        {
            _countdownTimer.Stop();
            Sacrificed = true;
            DialogResult = true;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _countdownTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
