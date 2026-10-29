using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    /// <summary>玩家的身体与能力。数值 0~100。</summary>
    public sealed class PlayerStats
    {
        public double Hunger = 80;
        public double Energy = 90;
        public double Health = 100;

        /// <summary>欠下的睡眠（小时），会压低精力上限。</summary>
        public double SleepDebt;

        public int Intelligence = 10;
        public int Charm = 10;
        public int Skill = 10;

        public double MaxEnergy => Math.Max(50, 100 - SleepDebt * 6);
        public bool Tired => Energy < 25;
    }

    public sealed partial class GameSession
    {
        public static readonly Money HospitalPerDay = Money.FromYuan(200);
        public const int HospitalDays = 2;

        private int _lastNewsReadDay = -1;

        public bool IsSleepTime => Clock.MinuteOfDay >= 18 * 60 || Clock.MinuteOfDay < WakeUpMinute;

        /// <summary>在家睡到早上 8:00（白天睡不着，只能小睡）。</summary>
        public bool Sleep()
        {
            if (CurrentPlace != Home) return Fail("只能回家睡觉。");
            if (!IsSleepTime) return Fail("白天睡不着，可以小睡一会儿。");
            int minutes = Clock.MinutesUntil(WakeUpMinute);
            SleepFor(minutes);
            double hours = minutes / 60.0;
            Say($"睡了 {hours:0.#} 小时。" + (Player.SleepDebt > 0.5 ? $"还欠 {Player.SleepDebt:0.#} 小时觉，精力上限降低。" : ""));
            return true;
        }

        /// <summary>在家小睡 2 小时。</summary>
        public bool Nap()
        {
            if (CurrentPlace != Home) return Fail("只能在家小睡。");
            SleepFor(120);
            return true;
        }

        /// <summary>读新闻：每天第一次看行情时智力 +1。</summary>
        public void ReadNews()
        {
            int today = Clock.Today.TotalDays;
            if (_lastNewsReadDay == today) return;
            _lastNewsReadDay = today;
            Player.Intelligence = Math.Min(100, Player.Intelligence + 1);
        }

        private void SleepFor(int minutes)
        {
            _sleeping = true;
            AdvanceClock(minutes);
            _sleeping = false;
            double hours = minutes / 60.0;
            if (hours >= 3)
            {
                Player.SleepDebt = Math.Max(0, Player.SleepDebt + (7 - hours));
                Player.SleepDebt = Math.Min(8, Player.SleepDebt);
            }
            Player.Energy = Math.Min(Player.MaxEnergy, Player.Energy);
        }

        private void UpdateBody(int minutes)
        {
            double h = minutes / 60.0;
            PlayerStats p = Player;
            if (_sleeping)
            {
                p.Energy = Math.Min(p.MaxEnergy, p.Energy + 12.5 * h * HomeSleepQuality);
                p.Hunger = Math.Max(0, p.Hunger - 2 * h);
            }
            else
            {
                p.Energy = Math.Max(0, p.Energy - 5.5 * h);
                p.Hunger = Math.Max(0, p.Hunger - 6 * h);
            }

            if (p.Hunger <= 0) p.Health -= 3 * h;
            if (p.Energy <= 0) p.Health -= 2 * h;
            if (p.Hunger > 30 && p.Energy > 0) p.Health = Math.Min(100, p.Health + 0.5 * h);
            p.Health = Math.Max(0, p.Health);
        }

        private void SendHome()
        {
            Trip = null;
            ClearEdge();
            if (InPlayerShop) PlayerShop.PlayerPresent = false;
            PlayerNodeId = Home.NodeId;
            CurrentPlace = Home;
        }

        private void Faint()
        {
            SendHome();
            _sleeping = true;
            AdvanceClock(Clock.MinutesUntil(12 * 60));
            _sleeping = false;
            Player.Health = Math.Max(0, Player.Health - 10);
            Player.SleepDebt = Math.Min(8, Player.SleepDebt + 3);
            Player.Energy = Math.Min(Player.MaxEnergy, 55);
            Show("你晕倒了", "凌晨 4 点还没睡，你晕倒在路边，被好心人送回了出租屋。一觉睡到中午，身体变差了。");
        }

        private void Hospitalize()
        {
            SendHome();
            _sleeping = true;
            AdvanceClock(HospitalDays * GameTimestamp.MinutesPerDay - Clock.MinuteOfDay + WakeUpMinute);
            _sleeping = false;
            Money bill = HospitalPerDay * HospitalDays;
            Ledger.ForceDebit(Clock.Today, "医疗", bill, "住院");
            Player.Health = 80;
            Player.Hunger = 70;
            Player.Energy = Player.MaxEnergy;
            Player.SleepDebt = 0;
            Show("住院了", $"长期挨饿、透支，你病倒了。住院 {HospitalDays} 天，医药费 {bill}。店也关了两天。");
        }
    }
}
