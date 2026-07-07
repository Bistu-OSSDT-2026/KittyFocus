using System;
using System.Windows.Threading;

namespace KittyFocus.Services
{
    /// <summary>专注会话状态。</summary>
    public enum FocusState
    {
        /// <summary>空闲，可开始专注。</summary>
        Idle,
        /// <summary>专注进行中。</summary>
        Running,
        /// <summary>专注已完成（倒计时归零）。</summary>
        Finished
    }

    /// <summary>
    /// 专注计时核心引擎。与 UI 解耦的纯逻辑类，
    /// 通过事件向 MainWindow 推送状态变化与每秒进度。
    /// 使用 DispatcherTimer 保证回调在 UI 线程执行。
    /// </summary>
    public class FocusEngine
    {
        public const int MinMinutes = 1;
        public const int MaxMinutes = 720;

        private readonly DispatcherTimer _timer;
        private int _remainingSeconds;

        public FocusEngine()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;

            FocusDurationMinutes = 25;
            State = FocusState.Idle;
            _remainingSeconds = FocusDurationMinutes * 60;
        }

        /// <summary>当前专注时长（分钟）。</summary>
        public int FocusDurationMinutes { get; private set; }

        /// <summary>当前状态。</summary>
        public FocusState State { get; private set; }

        /// <summary>本次专注总秒数。</summary>
        public int TotalSeconds => FocusDurationMinutes * 60;

        /// <summary>剩余秒数。</summary>
        public int RemainingSeconds => _remainingSeconds;

        /// <summary>设置专注时长（分钟），校验 1~720。返回是否成功。</summary>
        public bool SetDuration(int minutes)
        {
            if (minutes < MinMinutes || minutes > MaxMinutes)
                return false;

            FocusDurationMinutes = minutes;
            if (State == FocusState.Idle)
                _remainingSeconds = TotalSeconds;
            return true;
        }

        /// <summary>开始专注。仅 Idle / Finished 状态可开始。</summary>
        public bool Start()
        {
            if (State == FocusState.Running)
                return false;

            // 开始前重置剩余时间，保证 Finished 后重新开始也正确。
            _remainingSeconds = TotalSeconds;
            State = FocusState.Running;
            OnStateChanged();
            _timer.Start();
            return true;
        }

        /// <summary>强制结束专注，回到 Idle。已完成的专注也调此方法重置。</summary>
        public void ForceStop()
        {
            _timer.Stop();
            State = FocusState.Idle;
            _remainingSeconds = TotalSeconds;
            OnStateChanged();
        }

        /// <summary>仅重置状态为 Idle（如用户在 Finished 后点「再来一轮」复用）。</summary>
        public void ResetToIdle()
        {
            _timer.Stop();
            State = FocusState.Idle;
            _remainingSeconds = TotalSeconds;
            OnStateChanged();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (_remainingSeconds > 0)
            {
                _remainingSeconds--;
                OnTick();

                if (_remainingSeconds == 0)
                {
                    _timer.Stop();
                    State = FocusState.Finished;
                    OnStateChanged();
                    OnFinished();
                }
            }
        }

        /// <summary>状态变化（Idle/Running/Finished 切换）时触发。</summary>
        public event EventHandler StateChanged;
        /// <summary>每秒倒计时进度时触发。</summary>
        public event EventHandler Tick;
        /// <summary>专注自然完成时触发。</summary>
        public event EventHandler Finished;

        protected virtual void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
        protected virtual void OnTick() => Tick?.Invoke(this, EventArgs.Empty);
        protected virtual void OnFinished() => Finished?.Invoke(this, EventArgs.Empty);
    }
}
