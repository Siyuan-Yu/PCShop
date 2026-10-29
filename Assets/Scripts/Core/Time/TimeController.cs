using System;

namespace PCShopSell.Core.Time
{
    /// <summary>
    /// 把真实时间换算成游戏分钟：暂停、倍速，以及"打开面板自动暂停"。
    /// 每次 Tick 返回本次应推进世界时钟的整分钟数，不足一分钟的部分累积到下一次。
    /// </summary>
    public sealed class TimeController
    {
        public static readonly int[] Speeds = { 1, 2, 4, 8 };

        /// <summary>单次 Tick 最多计入的真实秒数，避免卡顿一帧后时间猛跳。</summary>
        public const double MaxTickSeconds = 0.25;

        /// <summary>
        /// 1× 下 1 游戏分钟的真实秒数。要和出行时的时钟速度（画面速度 × 路程折算）大致一致，
        /// 否则“走路时时间慢、站着不动时间飞快”。步行约 1.4 游戏分钟/秒，所以取 0.7。
        /// </summary>
        public const double DefaultSecondsPerMinute = 0.7;

        private double _secondsPerMinute;
        private double _fraction;
        private int _holds;

        /// <summary>1× 下一个游戏分钟对应的真实秒数（一天 3~5 分钟约为 0.19~0.31）。</summary>
        public double SecondsPerMinuteAt1x
        {
            get => _secondsPerMinute;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                _secondsPerMinute = value;
            }
        }

        public int Speed { get; private set; } = 1;
        public bool IsUserPaused { get; private set; }

        /// <summary>有面板打开（Hold 次数大于 0）时自动暂停。</summary>
        public bool IsHeld => _holds > 0;

        public bool IsRunning => !IsUserPaused && !IsHeld;

        /// <summary>上一次 Tick 推进的游戏分钟（含小数），用于画面平滑移动。</summary>
        public double LastDeltaMinutes { get; private set; }

        public TimeController(double secondsPerMinuteAt1x = DefaultSecondsPerMinute)
        {
            SecondsPerMinuteAt1x = secondsPerMinuteAt1x;
        }

        public void SetSpeed(int speed)
        {
            if (Array.IndexOf(Speeds, speed) < 0)
                throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
            IsUserPaused = false;
        }

        public void SetPaused(bool paused) => IsUserPaused = paused;

        public void TogglePause() => IsUserPaused = !IsUserPaused;

        public void Hold() => _holds++;

        public void Release()
        {
            if (_holds == 0) throw new InvalidOperationException("Release 次数多于 Hold。");
            _holds--;
        }

        public int Tick(double realSeconds)
        {
            if (!IsRunning || realSeconds <= 0)
            {
                LastDeltaMinutes = 0;
                return 0;
            }

            double minutes = Math.Min(realSeconds, MaxTickSeconds) * Speed / _secondsPerMinute;
            LastDeltaMinutes = minutes;
            _fraction += minutes;
            int whole = (int)Math.Floor(_fraction);
            _fraction -= whole;
            return whole;
        }
    }
}
