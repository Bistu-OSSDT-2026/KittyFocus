using System;
using System.Drawing;
using System.Windows.Forms;
using KittyFocus.Services;

namespace KittyFocus.Tray
{
    /// <summary>
    /// 系统托盘图标封装（基于 WinForms NotifyIcon）。
    /// 图标用代码绘制简笔猫脸，零资源依赖。
    /// 负责：托盘图标/tooltip、右键菜单（显示/退出）、双击恢复、气泡通知。
    /// </summary>
    public class TrayIcon : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private bool _disposed;

        /// <summary>「显示主窗口」菜单项被点击。</summary>
        public event EventHandler ShowMainWindowRequested;
        /// <summary>「退出」菜单项被点击。</summary>
        public event EventHandler ExitRequested;

        public TrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateCatIcon(),
                Visible = false,
                Text = "专注猫灵" // NotifyIcon.Text 上限 63 字符
            };

            var menu = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem("显示主窗口");
            showItem.Click += (s, e) => ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

            menu.Items.Add(showItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>显示托盘图标。</summary>
        public void Show() => _notifyIcon.Visible = true;

        /// <summary>更新 tooltip 文本（用于实时显示专注进度）。</summary>
        public void UpdateTooltip(string text)
        {
            // NotifyIcon.Text 上限 63 字符，截断保护。
            _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        /// <summary>弹出气泡通知。</summary>
        public void ShowBubble(string title, string message, int timeoutMs = 3000)
        {
            _notifyIcon.ShowBalloonTip(timeoutMs, title, message, ToolTipIcon.Info);
        }

        /// <summary>用 GDI+ 绘制简笔猫脸图标，避免引入 .ico 资源。</summary>
        private static Icon CreateCatIcon()
        {
            const int size = 32;
            using (var bmp = new Bitmap(size, size))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // 猫脸底色：暖橘
                var faceColor = Color.FromArgb(255, 167, 95);
                var darkColor = Color.FromArgb(92, 58, 30);

                // 两只耳朵（三角形）
                using (var brush = new SolidBrush(faceColor))
                {
                    g.FillPolygon(brush, new[] {
                        new PointF(5, 12), new PointF(9, 2), new PointF(13, 11)
                    });
                    g.FillPolygon(brush, new[] {
                        new PointF(19, 11), new PointF(23, 2), new PointF(27, 12)
                    });
                }

                // 圆脸
                g.FillEllipse(new SolidBrush(faceColor), 5, 9, 22, 19);

                // 两只眼睛
                using (var brush = new SolidBrush(darkColor))
                {
                    g.FillEllipse(brush, 9, 15, 4, 5);
                    g.FillEllipse(brush, 19, 15, 4, 5);
                }

                // 鼻子（小三角）
                g.FillPolygon(new SolidBrush(Color.FromArgb(220, 90, 90)), new[] {
                    new PointF(15, 20), new PointF(17, 20), new PointF(16, 22)
                });

                // 嘴（两道弧）
                using (var pen = new Pen(darkColor, 1.2f))
                {
                    g.DrawArc(pen, 12, 21, 4, 4, 0, 180);
                    g.DrawArc(pen, 16, 21, 4, 4, 0, 180);
                }

                // 胡须点
                using (var brush = new SolidBrush(darkColor))
                {
                    g.FillEllipse(brush, 8, 20, 1.5f, 1.5f);
                    g.FillEllipse(brush, 22.5f, 20, 1.5f, 1.5f);
                    g.FillEllipse(brush, 8, 23, 1.5f, 1.5f);
                    g.FillEllipse(brush, 22.5f, 23, 1.5f, 1.5f);
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
