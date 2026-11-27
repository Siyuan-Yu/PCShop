using System;
using System.Diagnostics;

namespace PCShopSell.Core.Common
{
    /// <summary>可选诊断回调；未启用时不计时、不分配，不依赖 Unity。</summary>
    public static class RuntimePerformanceProbe
    {
        public static Action<string, double> Listener;
        public static Scope Measure(string name) => new Scope(name, Listener);

        public readonly struct Scope : IDisposable
        {
            private readonly string _name;
            private readonly Action<string, double> _listener;
            private readonly long _started;
            internal Scope(string name, Action<string, double> listener)
            {
                _name = name; _listener = listener;
                _started = listener == null ? 0 : Stopwatch.GetTimestamp();
            }
            public void Dispose()
            {
                _listener?.Invoke(_name, (Stopwatch.GetTimestamp() - _started) * 1000.0 / Stopwatch.Frequency);
            }
        }
    }
}
