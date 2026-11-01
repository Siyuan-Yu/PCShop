using System;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private long _energyExhaustedAt = -1;
        private int _energyReminder;
        private int _bodyInterruptVersion;
        public bool EnergyExhausted => !_sleeping && Player.Energy <= 0;
        public int EnergyAwakeMinutesLeft => !EnergyExhausted ? 240 : (int)Math.Max(0, 240 - (Clock.Now.TotalMinutes - (_energyExhaustedAt < 0 ? Clock.Now.TotalMinutes : _energyExhaustedAt)));
        public string EnergyExhaustionText => EnergyExhausted ? $"精力耗尽｜剩余清醒时间：{EnergyAwakeMinutesLeft / 60}小时{EnergyAwakeMinutesLeft % 60}分钟" : "";
        private void ClearEnergyExhaustion() { _energyExhaustedAt = -1; _energyReminder = 0; _pendingFaint = false; }
        private void SynchronizeEnergyExhaustion()
        {
            if (_sleeping || Player.Energy > 0) { ClearEnergyExhaustion(); return; }
            if (_energyExhaustedAt < 0)
            {
                _energyExhaustedAt = Clock.Now.TotalMinutes; _energyReminder = 1;
                Notice?.Invoke("精力已耗尽。若保持清醒且精力没有恢复，将在4小时后晕倒。");
            }
        }
        private void TickEnergyExhaustion()
        {
            SynchronizeEnergyExhaustion();
            if (!EnergyExhausted) return;
            int left = EnergyAwakeMinutesLeft;
            if (left <= 60 && _energyReminder < 2) { _energyReminder = 2; Notice?.Invoke("精力仍耗尽，剩余清醒时间不足1小时，请安排休息。"); }
            if (left <= 30 && _energyReminder < 3) { _energyReminder = 3; Notice?.Invoke("精力仍耗尽，剩余清醒时间不足30分钟。"); }
            if (left <= 0) _pendingFaint = true;
        }
    }
}
