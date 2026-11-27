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
        public const int OutpatientMinutes = 60;
        public const int OutpatientHealthRecovery = 20;

        public Place NearbyHospital()
        {
            var position = PlayerPosition;
            return City.PublicPlaces.Where(p => p.Business == HospitalBusiness && DistrictAccessBlocker(p) == null)
                .OrderBy(p => DistanceCity(position, StandOf(p))).FirstOrDefault();
        }

        public bool ReceiveOutpatientCare()
        {
            if (IsTimeAdvancing) return false;
            var hospital = CurrentPlace;
            if (hospital?.Business != HospitalBusiness || IsGameOver) return false;
            if (!Ledger.TryDebit(Clock.Today, "医疗", OutpatientFee, hospital.Name + "门诊")) return Fail($"现金不足，门诊需 {OutpatientFee}。");
            RecoverAt(hospital, OutpatientMinutes, () =>
            {
                _fluIllUntil = 0;
                Player.Health = Math.Min(100, Player.Health + OutpatientHealthRecovery);
                Show("门诊结束", $"已支付门诊费 {OutpatientFee}，用时 {OutpatientMinutes} 分钟。当前健康 {Player.Health:0.#}/100。");
            });
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
            if (IsTimeAdvancing) return false;
            if (!CanSleepAt(CurrentPlace)) return Fail("只能在可入住的自家住宅睡觉。");
            SleepFor(minutes, () => Say($"睡了 {minutes / 60.0:0.#} 小时。过去24小时已睡 {SleepHoursLast24:0.#}/8 小时。"));
            return true;
        }

        /// <summary>在家睡到下一个早上8:00；白天也可以选择任意睡眠时长。</summary>
        public bool Sleep()
        {
            if (IsTimeAdvancing) return false;
            if (!CanSleepAt(CurrentPlace)) return Fail("只能在可入住的自家住宅睡觉。");
            int minutes = NightWorkRest ? NightRestMinutes : Clock.MinutesUntil(WakeUpMinute);
            if (minutes <= 0) minutes = 480;
            double hours = minutes / 60.0;
            SleepFor(minutes, () => Say($"睡了 {hours:0.#} 小时。" + (Player.SleepDebt > 0.5 ? $"还欠 {Player.SleepDebt:0.#} 小时觉，精力上限降低。" : "")));
            return true;
        }

        /// <summary>在家小睡 2 小时。</summary>
        public bool Nap()
        {
            if (IsTimeAdvancing) return false;
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

        public event Action<long> ResidenceSleepFinished;

        // Core callers remain synchronous by default. The game controller explicitly opts in.
        public bool DeferRestAdvances { get; set; }
        public bool IsTimeAdvancing => _restAdvance != null;
        public string TimeAdvanceError { get; private set; }
        public string TimeAdvanceLabel { get; private set; }
        public double TimeAdvanceProgress => _restMinutes <= 0 ? 0 :
            Math.Min(1, (Clock.Now.TotalMinutes - _restStart) / (double)_restMinutes);
        public event Action TimeAdvanceStarted;
        private IEnumerator<int> _restAdvance;
        private Action _restFinished;
        private long _restStart;
        private int _restMinutes, _timeAdvanceGeneration;

        private void AdvanceRest(int minutes, string label, Action finished)
        {
            if (!DeferRestAdvances)
            {
                AdvanceClock(minutes);
                finished();
                return;
            }
            if (IsTimeAdvancing) throw new InvalidOperationException("休息结算期间不能重复启动结算。");
            _timeAdvanceGeneration++;
            _restStart = Clock.Now.TotalMinutes;
            _restMinutes = minutes;
            TimeAdvanceLabel = label;
            TimeAdvanceError = null;
            _restFinished = finished;
            _restAdvance = AdvanceClockSteps(minutes);
            TimeAdvanceStarted?.Invoke();
        }

        /// <summary>保持每分钟原有结算顺序；到达预算后让出主线程。单分钟不能截断。</summary>
        public void PumpTimeAdvance(double budgetMilliseconds)
        {
            if (_restAdvance == null || TimeAdvanceError != null) return;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                do
                {
                    if (!_restAdvance.MoveNext())
                    {
                        var finished = _restFinished;
                        // Keep saving blocked through the completion callback as well.
                        finished?.Invoke();
                        _restAdvance.Dispose();
                        _restAdvance = null;
                        _restFinished = null;
                        return;
                    }
                }
                while ((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency < Math.Max(0, budgetMilliseconds));
            }
            catch
            {
                // Do not allow a partly settled world to overwrite a valid save.
                TimeAdvanceError = "结算遇到异常，请重启游戏并读取最近存档。";
                throw;
            }
        }

        private void SleepFor(int minutes, Action finished = null)
        {
            if (minutes <= 0) return;
            var residence = CurrentPlace;
            long start = Clock.Now.TotalMinutes;
            ClearEnergyExhaustion();
            _sleeping = true;
            AdvanceRest(minutes, "正在休息", () =>
            {
                _sleeping = false;
                RefreshDistrictNarrative();
                _sleepHistory.Add(new[] { start, Clock.Now.TotalMinutes });
                _sleepHistory.RemoveAll(span => span[1] <= Clock.Now.TotalMinutes - 1440);
                RecordOpeningStaffRest();
                Player.SleepDebt = Math.Max(0, 8 - SleepHoursLast24);
                if (SleepHoursLast24 >= 8 - 1e-9) Player.Energy = Player.MaxEnergy;
                Player.Energy = Math.Min(Player.MaxEnergy, Player.Energy);
                FinishCityClimateSleep(minutes);
                SynchronizeEnergyExhaustion();
                long duration = Clock.Now.TotalMinutes - start;
                if (duration > 0 && !IsGameOver && CurrentPlace == residence && CanSleepAt(residence))
                    ResidenceSleepFinished?.Invoke(duration);
                finished?.Invoke();
            });
        }

        private void UpdateBody(int minutes)
        {
            double h = minutes / 60.0;
            PlayerStats p = Player;
            if (_sleeping)
            {
                p.Energy = Math.Min(p.MaxEnergy, p.Energy + 12.5 * h * HomeSleepQuality * CitySleepComfortFactor);
                p.Hunger = Math.Max(0, p.Hunger - h);
                p.ChangeStress(-2 * h);
            }
            else
            {
                var work = ActiveWorkDay;
                p.SpendEnergy(3.85 * h * (work != null && work.EnergyMultiplier > 0 ? work.EnergyMultiplier : 1));
                if (work != null) p.ChangeStress(work.StressPerHour * h);
                else if (p.Tired) p.ChangeStress(.5 * h);
                p.Hunger = Math.Max(0, p.Hunger - 3 * h);
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
            AdvanceRest(480, "正在恢复体力", () =>
            {
                _sleeping = false;
                RefreshDistrictNarrative();
                Player.Health = Math.Max(0, Player.Health - 10);
                Player.SleepDebt = Math.Min(8, Player.SleepDebt + 3);
                Player.Energy = Math.Min(Player.MaxEnergy, 55);
                ClearEnergyExhaustion();
                Show("你晕倒了", "精力耗尽后连续清醒四小时，你因疲劳晕倒，被送回出租屋休息。身体和欠觉按原有规则恢复。");
            });
        }

        private void Hospitalize()
        {
            var hospital = NearbyHospital();
            bool covered = HasHospitalCoverage;
            RecoverAt(hospital, HospitalDays * GameTimestamp.MinutesPerDay - Clock.MinuteOfDay + WakeUpMinute, () =>
            {
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
            });
        }

        private void RecoverAt(Place hospital, int minutes, Action finished)
        {
            MoveForCare(hospital ?? Home);
            _sleeping = true;
            AdvanceRest(minutes, "正在接受治疗", () =>
            {
                _sleeping = false;
                if (hospital != null)
                {
                    CurrentPlace = null;
                    _free = StandOf(hospital);
                }
                RefreshDistrictNarrative();
                finished();
            });
        }
    }
}
