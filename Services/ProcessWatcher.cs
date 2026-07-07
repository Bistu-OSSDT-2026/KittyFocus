using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using KittyFocus.Models;

namespace KittyFocus.Services
{
    /// <summary>
    /// 进程轮询引擎。专注进行时后台每隔 2 秒轮询顶层窗口，
    /// 获取进程名与窗口标题，通过 Detected 事件向外通知。
    /// 不持有黑名单逻辑，专注单一职责。
    /// FR-05 核心实现。
    /// </summary>
    public class ProcessWatcher : IDisposable
    {
        private const int PollIntervalMs = 2000;
        private const int MaxWindowTextLength = 512;

        private Timer _timer;
        private bool _disposed;

        /// <summary>轮询获取到当前活动窗口信息时触发（线程池线程回调）。</summary>
        public event EventHandler<ProcessInfo> Polled;

        /// <summary>启动轮询。</summary>
        public void Start()
        {
            if (_timer == null)
                _timer = new Timer(OnTimerTick, null, 0, PollIntervalMs);
        }

        /// <summary>停止轮询。</summary>
        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnTimerTick(object state)
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return;

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0) return;

                string processName = "";
                try
                {
                    using (var proc = Process.GetProcessById((int)pid))
                    {
                        processName = proc.ProcessName ?? "";
                    }
                }
                catch
                {
                    // 进程可能已退出
                }

                string windowTitle = GetWindowText(hWnd);

                if (string.IsNullOrEmpty(processName)) return;

                var info = new ProcessInfo
                {
                    ProcessName = processName.ToLowerInvariant(),
                    WindowTitle = windowTitle ?? "",
                    ProcessId = (int)pid
                };

                Polled?.Invoke(this, info);
            }
            catch
            {
                // 单次轮询异常不影响后续
            }
        }

        private static string GetWindowText(IntPtr hWnd)
        {
            var sb = new StringBuilder(MaxWindowTextLength);
            int len = GetWindowText(hWnd, sb, sb.Capacity);
            return len > 0 ? sb.ToString(0, len) : "";
        }

        // ---- Win32 P/Invoke ----
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _timer?.Dispose();
        }
    }
}
