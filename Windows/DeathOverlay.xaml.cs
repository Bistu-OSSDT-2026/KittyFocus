using System.Drawing;
using System.Media;
using System.Windows;

namespace KittyFocus.Windows
{
    /// <summary>
    /// FR-06 专注失败界面。WarnDialog 倒计时超时、用户未关闭违规程序时弹出。
    /// 全屏暗色遮罩 + 失败提示 + 音效。
    /// 用户确认后本次专注将被强制结束。
    /// </summary>
    public partial class DeathOverlay : Window
    {

        public DeathOverlay(string processName) : this(processName, isForcedTermination: false) { 
        }

        public DeathOverlay(string reason, bool isForcedTermination)
        {
            InitializeComponent();

            if (isForcedTermination)
            {
                TitleText.Text = "专注已终止";
                SubtitleText.Text = "猫咪离开了…";
                DescriptionText.Text = "本次专注已被强制终止";
                ProcessNameText.Text = reason; // "强制终止"
            }
            else
            {
                ProcessNameText.Text = reason;
            }

            // 播放警告音
            SystemSounds.Hand.Play();
        }

        private void AcknowledgeButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
