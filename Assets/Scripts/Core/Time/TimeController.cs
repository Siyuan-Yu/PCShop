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
        public const int DefaultSpeed = 2;

        /// <summary>界面以默认档为 1×，内部档位沿用世界时钟和移动的倍率。</summary>
        public static string SpeedLabel(int speed) =>
            (speed / (double)DefaultSpeed).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "×";

        /// <summary>单次 Tick 最多计入的真实秒数，避免卡顿一帧后时间猛跳。</summary>
        public const double MaxTickSeconds = 0.25;

        /// <summary>世界相对最初（1 游戏分钟 = 0.7 秒）的倍率：原 3 倍速度降低 30% 为 2.1 倍。时钟与出行共用倍率，保持每段路的游戏耗时；赛车使用独立真实时间。</summary>
        public const double WorldRate = 2.1;

        /// <summary>
        /// 内部倍率为 1（界面 0.5×）时，1 游戏分钟的真实秒数。所有实时状态共用此时钟，
        /// 不根据走路距离、地图压缩比例或是否停下来重新计时。
        /// </summary>
        public const double DefaultSecondsPerMinute = 0.7 / WorldRate;

        private double _secondsPerMinute;
        private double _fraction;
        private int _holds;
        /// <summary>Sub-minute progress belongs to the saved world clock, including a journey saved between minutes.</summary>
        public double FractionalMinute => _fraction;
        public void RestoreFractionalMinute(double fraction)
        {
            _fraction=double.IsNaN(fraction)||double.IsInfinity(fraction)?0:Math.Max(0,Math.Min(.999999999999,fraction));
            LastDeltaMinutes=0;
        }

        /// <summary>内部倍率为 1 时每游戏分钟的真实秒数，默认 1/3 秒。界面 1× 使用内部倍率 2。</summary>
        public double SecondsPerMinuteAt1x
        {
            get => _secondsPerMinute;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                _secondsPerMinute = value;
            }
        }

        public int Speed { get; private set; } = DefaultSpeed;
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
