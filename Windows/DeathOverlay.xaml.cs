using System;
using System.Diagnostics;
using System.Media;
using System.Windows;
using System.Windows.Interop;

namespace KittyFocus.Windows
{
    /// <summary>
    /// FR-06 全屏惩罚遮罩。专注期间命中黑名单时弹出，
    /// 全屏暗色遮罩 + 猫咪阵亡提示 + 音效。
    /// 用户需点击「已知晓」或「关闭违规程序」才可关闭。
    /// </summary>
    public partial class DeathOverlay : Window
    {
        private readonly int _processId;

        /// <summary>true = 用户点击了「关闭违规程序」。</summary>
        public bool KillRequested { get; private set; }

        public DeathOverlay(string processName, int processId)
        {
            InitializeComponent();

            ProcessNameText.Text = processName;
            _processId = processId;

            // 播放系统警告音
            SystemSounds.Hand.Play();
        }

        private void AcknowledgeButton_Click(object sender, RoutedEventArgs e)
        {
            KillRequested = false;
            DialogResult = true;
            Close();
        }

        private void KillButton_Click(object sender, RoutedEventArgs e)
        {
            KillRequested = true;
            TryKillProcess();
            DialogResult = true;
            Close();
        }

        private void TryKillProcess()
        {
            if (_processId <= 0) return;

            try
            {
                using (var proc = Process.GetProcessById(_processId))
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                    }
                }
            }
            catch
            {
                // 进程可能已退出或无权限
            }
        }
    }
}
