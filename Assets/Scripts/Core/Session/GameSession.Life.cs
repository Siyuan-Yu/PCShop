using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>玩家的身体与能力。数值 0~100。</summary>
    public sealed class PlayerStats
    {
        public double Hunger = 80;
        private double _energy = 90;
        internal Action EnergyChanged;
        public double Energy { get => _energy; set { _energy = Math.Max(0, Math.Min(MaxEnergy, value)); EnergyChanged?.Invoke(); } }
        public const double EnergyConsumptionMultiplier=.8;
        public void SpendEnergy(double amount) { if(amount>0)Energy=Math.Max(0,Energy-amount*EnergyConsumptionMultiplier); }
        public double Health = 100;
        public const double HighStressThreshold = 70;
        public const double TiredEnergyThreshold = 25;
        public double Stress { get; private set; }
        public void ChangeStress(double amount) => Stress = Math.Max(0, Math.Min(100, Stress + amount));
        // Display-only diet records; they do not alter health, energy or any other simulation.
        public double Nutrition = 50, Mood = 50;

        /// <summary>欠下的睡眠（小时），会压低精力上限。</summary>
        public double SleepDebt;

        public int Intelligence = 10;
        public int Charm = 10;
        public int Skill = 10;
        public int Stamina = 50;

        public double MaxEnergy => Math.Max(50, 100 - SleepDebt * 6);
        public bool Tired => Energy < TiredEnergyThreshold;
    }

    public sealed partial class GameSession
    {
        public static readonly Money HospitalPerDay = Money.FromYuan(200);
        public const int HospitalDays = 1;
        public const string HospitalBusiness = "医院";
        public static readonly Money OutpatientFee = Money.FromYuan(150);

        public Place NearbyHospital()
        {
            var position = PlayerPosition;
            return City.PublicPlaces.Where(p => p.Business == HospitalBusiness && DistrictAccessBlocker(p) == null)
                .OrderBy(p => DistanceCity(position, StandOf(p))).FirstOrDefault();
        }

        public bool ReceiveOutpatientCare()
        {
            var hospital = CurrentPlace;
            if (hospital?.Business != HospitalBusiness || IsGameOver) return false;
            if (Player.Health >= 100 && _fluIllUntil <= Clock.Now.TotalMinutes) return Fail("健康已满，无需看诊。");
            if (!Ledger.TryDebit(Clock.Today, "医疗", OutpatientFee, hospital.Name + "门诊")) return Fail("现金不足。");
            RecoverAt(hospital, 60);
            _fluIllUntil = 0;
            Player.Health = Math.Min(100, Player.Health + 20);
            Show("门诊结束", $"已从{hospital.Name}出来。看诊用时 1 小时，健康恢复至 {Player.Health:0.#}。");
            return true;
        }

        private int _lastNewsReadDay = -1;

        public bool IsSleepTime => true;

        private readonly List<long[]> _sleepHistory = new List<long[]>();
        public double SleepHoursLast24 => _sleepHistory.Sum(span => Math.Max(0, Math.Min(Clock.Now.TotalMinutes, span[1]) - Math.Max(Clock.Now.TotalMinutes - 1440, span[0]))) / 60.0;

        private void RestoreSleepHistory(long[] spans)
        {
            _sleepHistory.Clear();
            if (spans != null)
                for (int i = 0; i + 1 < spans.Length; i += 2)
                    if (spans[i + 1] > spans[i]) _sleepHistory.Add(new[] { spans[i], spans[i + 1] });
        }

        public bool SleepUntil(int minuteOfDay)
        {
            if (minuteOfDay < 0 || minuteOfDay >= 1440) return Fail("请选择有效的起床时间。");
            int minutes = (minuteOfDay - Clock.MinuteOfDay + 1440) % 1440;
            if (minutes == 0) return Fail("起床时间不能与现在相同。");
            return SleepMinutes(minutes);
        }

        public bool SleepEightHours() => SleepMinutes(480);

        public bool SleepUntilTomorrowMorning() =>
            SleepMinutes(GameTimestamp.MinutesPerDay - Clock.MinuteOfDay + WakeUpMinute);

        private bool SleepMinutes(int minutes)
        {
            if (!CanSleepAt(CurrentPlace)) return Fail("只能在可入住的自家住宅睡觉。");
            SleepFor(minutes);
            Say($"睡了 {minutes / 60.0:0.#} 小时。过去24小时已睡 {SleepHoursLast24:0.#}/8 小时。");
            return true;
        }

        /// <summary>在家睡到下一个早上8:00；白天也可以选择任意睡眠时长。</summary>
        public bool Sleep()
        {
            if (!CanSleepAt(CurrentPlace)) return Fail("只能在可入住的自家住宅睡觉。");
            int minutes = NightWorkRest ? NightRestMinutes : Clock.MinutesUntil(WakeUpMinute);
            if (minutes <= 0) minutes = 480;
            SleepFor(minutes);
            double hours = minutes / 60.0;
            Say($"睡了 {hours:0.#} 小时。" + (Player.SleepDebt > 0.5 ? $"还欠 {Player.SleepDebt:0.#} 小时觉，精力上限降低。" : ""));
            return true;
        }

        /// <summary>在家小睡 2 小时。</summary>
        public bool Nap()
        {
            if (!CanSleepAt(CurrentPlace)) return Fail("只能在可入住的自家住宅小睡。");
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
            if (minutes <= 0) return;
            long start = Clock.Now.TotalMinutes;
            ClearEnergyExhaustion();
            _sleeping = true;
            AdvanceClock(minutes);
            _sleeping = false;
            RefreshDistrictNarrative();
            _sleepHistory.Add(new[] { start, Clock.Now.TotalMinutes });
            _sleepHistory.RemoveAll(span => span[1] <= Clock.Now.TotalMinutes - 1440);
            Player.SleepDebt = Math.Max(0, 8 - SleepHoursLast24);
            if (SleepHoursLast24 >= 8 - 1e-9) Player.Energy = Player.MaxEnergy;
            Player.Energy = Math.Min(Player.MaxEnergy, Player.Energy);
            FinishCityClimateSleep(minutes);
            SynchronizeEnergyExhaustion();
        }

        private void UpdateBody(int minutes)
        {
            double h = minutes / 60.0;
            PlayerStats p = Player;
            if (_sleeping)
            {
                p.Energy = Math.Min(p.MaxEnergy, p.Energy + 12.5 * h * HomeSleepQuality * CitySleepComfortFactor);
                p.Hunger = Math.Max(0, p.Hunger - 2 * h);
                p.ChangeStress(-2 * h);
            }
            else
            {
                var work = ActiveWorkDay;
                p.SpendEnergy(5.5 * h * (work != null && work.EnergyMultiplier > 0 ? work.EnergyMultiplier : 1));
                if (work != null) p.ChangeStress(work.StressPerHour * h);
                else if (p.Tired) p.ChangeStress(.5 * h);
                p.Hunger = Math.Max(0, p.Hunger - 6 * h);
            }

            if (p.Hunger <= 0) p.Health -= 3 * h;
            if (p.Energy <= 0) p.Health -= 2 * h;
            if (p.Hunger > 30 && p.Energy > 0) p.Health = Math.Min(100, p.Health + 0.5 * h);
            p.Health = Math.Max(0, p.Health);
        }

        private void MoveForCare(Place place)
        {
            InterruptBrandExpoForHealth();
            foreach (var a in _cityActivities.Where(a => a.Active && (a.State == CityActivityPhase.Running || a.Id == CityIntent?.ActivityId)).ToArray())
                EndCityActivity(a, CityActivityPhase.Interrupted, "因身体不适中断，仅保留已发生的消费和阶段", Clock.Now.TotalMinutes);
            StopTrip(); CancelCityTravel(); CitySleepRequested = false; _steering = false;
            InterruptPlayerWork("因身体不适中断打工，当天不计工资。请先休息恢复。");
            Trip = null;
            ClearEdge();
            if (InPlayerShop) PlayerShop.PlayerPresent = false;
            PlayerNodeId = place.NodeId;
            CurrentPlace = place;
        }

        private void Faint()
        {
            MoveForCare(Home);
            _sleeping = true;
            // The former 04:00 -> noon recovery was eight hours. Keep that duration
            // when exhaustion can happen in daytime, instead of forcing a 23-hour sleep.
            AdvanceClock(480);
            _sleeping = false;
            RefreshDistrictNarrative();
            Player.Health = Math.Max(0, Player.Health - 10);
            Player.SleepDebt = Math.Min(8, Player.SleepDebt + 3);
            Player.Energy = Math.Min(Player.MaxEnergy, 55);
            ClearEnergyExhaustion();
            Show("你晕倒了", "精力耗尽后连续清醒四小时，你因疲劳晕倒，被送回出租屋休息。身体和欠觉按原有规则恢复。");
        }

        private void Hospitalize()
        {
            var hospital = NearbyHospital();
            bool covered = HasHospitalCoverage;
            RecoverAt(hospital, HospitalDays * GameTimestamp.MinutesPerDay - Clock.MinuteOfDay + WakeUpMinute);
            Money bill = HospitalPerDay * HospitalDays;
            var medicalCharge = Ledger.ForceDebit(Clock.Today, "医疗", bill, (hospital?.Name ?? "") + "住院");
            var reimbursed = ReimburseEventHospital(medicalCharge, covered);
            Player.Health = 80;
            Player.Hunger = 70;
            Player.Energy = Player.MaxEnergy;
            Player.SleepDebt = 0;
            Show("住院了", $"长期挨饿、透支，你病倒了。住院 {HospitalDays} 天，医药费 {bill}。"
                + (reimbursed.IsPositive ? $"保险已报销 {reimbursed}，自付 {bill - reimbursed}。" : "")
                + (hospital == null ? "" : $"你现在在{hospital.Name}门口。"));
        }

        private void RecoverAt(Place hospital, int minutes)
        {
            MoveForCare(hospital ?? Home);
            _sleeping = true;
            try { AdvanceClock(minutes); }
            finally { _sleeping = false; }
            if (hospital != null)
            {
                CurrentPlace = null;
                _free = StandOf(hospital);
            }
            RefreshDistrictNarrative();
        }
    }
}
