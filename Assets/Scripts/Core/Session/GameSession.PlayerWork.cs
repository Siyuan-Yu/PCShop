using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum WorkDayState { Pending, Working, Completed, Absent, Rest }
    public sealed class PlayerWorkOffer
    {
        public string Title;
        public int Start, End, Wage, HealthCost, MoodCost;
        public PlayerWorkOffer(string title, int start, int end, int wage, int health, int mood)
        { Title = title; Start = start * 60; End = end * 60; Wage = wage; HealthCost = health; MoodCost = mood; }
        public string Hours => GameSession.PlayerWorkHours(Start, End);
    }
    [Serializable]
    public sealed class PlayerWorkDay
    {
        public WorkDayState State;
        public int LateMinutes;
        public long EarnedYuan;
        public long BonusYuan;
        public double EnergyMultiplier, StressPerHour;
        public bool LunchServed;
        public bool WagePaid;
        public PlayerWorkDay Copy() => (PlayerWorkDay)MemberwiseClone();
    }
    [Serializable]
    public sealed class PlayerWorkContract
    {
        public string PlaceId;
        public int StartDay, ShiftStart, ShiftEnd, WorkingIndex = -1;
        public long DailyWageYuan = 150;
        public string JobTitle;
        public int HealthCost = 6, MoodCost = 8;
        public bool ReadyToPay, Paid;
        public string LastNote;
        public PlayerWorkDay[] Days = Enumerable.Range(0, 7).Select(_ => new PlayerWorkDay()).ToArray();
        public int EndDay => StartDay + 6;
        public int ScheduledShiftCount => Days.Count(day => day.State != WorkDayState.Rest);
        public bool IsNightShift => ShiftEnd >= 1440;
        public long LatePenaltyPerHalfHour => Math.Max(1, (long)Math.Ceiling(DailyWageYuan * 30.0 / (ShiftEnd - ShiftStart)));
        public long EarnedYuan => Days.Sum(d => d.EarnedYuan);
        public long DeductionsYuan => Days.Count(d => d.State == WorkDayState.Completed || d.State == WorkDayState.Absent) * DailyWageYuan
            - EarnedYuan + Days.Where(d => d.State == WorkDayState.Completed).Sum(d => d.BonusYuan);
        public PlayerWorkContract Copy()
        {
            var c = (PlayerWorkContract)MemberwiseClone();
            c.Days = Days.Select(d => d.Copy()).ToArray(); return c;
        }
    }

    public sealed partial class GameSession
    {
        private enum WorkAptitude { Charm, Dexterity, Stamina, Intelligence }
        public const int FatigueWorkPenalty = 50, HighStressWorkPenalty = 25;
        public const int WorkBonusThreshold = 50;
        public const int WeekendWorkHours = 8, WeekendWorkPay = 300;
        private long _weekendWorkEnds;
        public PlayerWorkContract WorkContract { get; private set; }
        private bool IsContractWorking => WorkContract != null && !WorkContract.Paid && WorkContract.WorkingIndex >= 0;
        public bool IsWeekendWorking => _weekendWorkEnds > 0;
        public bool IsWorking => IsContractWorking || IsWeekendWorking;
        public GameTimestamp WeekendWorkEnds => new GameTimestamp(_weekendWorkEnds);
        internal bool CanStartWeekendWork => false; // Retired event; existing saved shifts still finish.
        public bool HasWorkContract => WorkContract != null && !WorkContract.Paid;
        public Place WorkPlace => WorkContract == null || !City.HasPlace(WorkContract.PlaceId) ? null : City.GetPlace(WorkContract.PlaceId);
        public static string PlayerWorkHours(int start, int end) => $"{start / 60:00}:{start % 60:00}–" + (end >= 1440 ? "次日 " : "") + $"{end % 1440 / 60:00}:{end % 60:00}";
        public string WorkHours => WorkContract == null ? "" : PlayerWorkHours(WorkContract.ShiftStart, WorkContract.ShiftEnd);
        public int WorkMinutesLeft => IsWeekendWorking ? (int)Math.Max(0, Clock.Now.MinutesUntil(WeekendWorkEnds))
            : !IsContractWorking ? 0 : (int)Math.Max(0, (long)(WorkContract.StartDay + WorkContract.WorkingIndex) * 1440 + WorkContract.ShiftEnd - Clock.Now.TotalMinutes);

        public bool OffersPlayerWork(Place p) => p != null && p.Id != "life_expo" && p.Kind == PlaceKind.Shop && !IsMyShop(p) && !IsVacant(p) && VentureAt(p.Id) == null;
        public PlayerWorkOffer PlayerWorkFor(Place p)
        {
            switch (p?.Business)
            {
                case "奶茶": return new PlayerWorkOffer("奶茶店晚班", 11, 21, 150, 8, 10);
                case "服装": return new PlayerWorkOffer("服装导购白班", 10, 18, 135, 5, 6);
                case "理发": return new PlayerWorkOffer("理发店帮工白班", 10, 20, 150, 7, 9);
                case "手机": return new PlayerWorkOffer("手机店白班", 9, 18, 150, 6, 8);
                case "修鞋": return new PlayerWorkOffer("修鞋铺白班", 9, 17, 130, 5, 6);
                case "快餐": return new PlayerWorkOffer("快餐后厨长班", 10, 20, 160, 8, 10);
                case "面包": return new PlayerWorkOffer("面包工坊烘焙夜班", 22, 30, 175, 11, 12);
                case "药店": return new PlayerWorkOffer("药店理货夜班", 20, 28, 180, 12, 13);
                case "家具": return new PlayerWorkOffer("家具城长白班", 9, 19, 155, 8, 10);
                case "电脑": return new PlayerWorkOffer("电脑店帮工白班", 9, 18, 155, 6, 8);
                case "咖啡": return new PlayerWorkOffer("咖啡店午晚班", 12, 20, 140, 6, 7);
                case "农贸": return new PlayerWorkOffer("农贸市场早班", 6, 15, 145, 7, 8);
                case "彩票": return new PlayerWorkOffer("彩票店白班", 10, 18, 130, 5, 6);
                case "花店": return new PlayerWorkOffer("花店白班", 9, 17, 135, 5, 6);
                case "便利店": return new PlayerWorkOffer("便利店通宵班", 23, 31, 185, 12, 14);
                case "烟酒": return new PlayerWorkOffer("烟酒茶行晚班", 14, 22, 160, 7, 9);
                default: return new PlayerWorkOffer("店铺白班", 9, 18, 150, 6, 8);
            }
        }
        public int PlayerWorkStart(Place p) => PlayerWorkFor(p).Start;
        public int PlayerWorkEnd(Place p) => PlayerWorkFor(p).End;

        private static WorkAptitude WorkAptitudeFor(Place place) => place?.Business switch
        {
            "理发" or "修鞋" or "电脑" or "奶茶" or "快餐" or "面包" or "咖啡" or "裁缝" or "钟表维修" or "家具修复" => WorkAptitude.Dexterity,
            "农贸" or "超市" or "五金" or "药店" => WorkAptitude.Stamina,
            "企业服务" or "社区服务" or "宠物批发" => WorkAptitude.Intelligence,
            _ => WorkAptitude.Charm,
        };

        public (string attribute, int score, long bonus, double energyMultiplier, double stressPerHour, int fatiguePenalty, int stressPenalty)
            PlayerWorkImpact(Place place, long baseWage, int shiftEnd)
        {
            var aptitude = WorkAptitudeFor(place);
            var (attribute, score) = aptitude switch
            {
                WorkAptitude.Dexterity => ("动手能力", Player.Skill),
                WorkAptitude.Stamina => ("体能", Player.Stamina),
                WorkAptitude.Intelligence => ("智力", Player.Intelligence),
                _ => ("魅力", EffectiveCharm),
            };
            score = Math.Max(0, Math.Min(100, score));
            int fatiguePenalty = Player.Tired ? FatigueWorkPenalty : 0;
            int stressPenalty = Player.Stress >= PlayerStats.HighStressThreshold ? HighStressWorkPenalty : 0;
            double skillBonus = Math.Max(0, score - WorkBonusThreshold) / (100.0 - WorkBonusThreshold) * .25;
            long bonus = (long)Math.Floor(baseWage * skillBonus * (1 - fatiguePenalty / 100.0) * (1 - stressPenalty / 100.0));
            double energyMultiplier = aptitude == WorkAptitude.Stamina ? 1 + (50 - score) * .005 : 1;
            double stressPerHour = (shiftEnd >= 1440 ? 2.6 : 1.0) * (1 + (50 - score) * .01);
            return (attribute, score, bonus, energyMultiplier, stressPerHour, fatiguePenalty, stressPenalty);
        }

        private PlayerWorkDay ActiveWorkDay => IsContractWorking && Clock.Now.TotalMinutes >
            (long)(WorkContract.StartDay + WorkContract.WorkingIndex) * 1440 + WorkContract.ShiftStart
                ? WorkContract.Days[WorkContract.WorkingIndex] : null;

        public bool SignPlayerWork(Place p)
        {
            if (IsGameOver || HasWorkContract || CurrentPlace != p || !OffersPlayerWork(p)) return Fail("要到营业中的他人店铺签约，且同一时间只能签一份打工合同。");
            bool weekdaysOnly = SupplierClosesOnWeekend(p);
            if (weekdaysOnly && !IsWholesalerOpen) return Fail("请在批发市场营业时签约。");
            var offer = PlayerWorkFor(p);
            WorkContract = new PlayerWorkContract
            {
                PlaceId = p.Id, StartDay = Clock.Today.TotalDays + 1,
                ShiftStart = offer.Start, ShiftEnd = offer.End, JobTitle = offer.Title, DailyWageYuan = offer.Wage, HealthCost = offer.HealthCost, MoodCost = offer.MoodCost,
                LastNote = weekdaysOnly ? "已签一周合同，明天起算。工作日到店上班，周末休息，工资每天收工后到账。"
                    : "已签一周合同，明天开始。每天到店自动上班，每天收工后工资自动到账。"
            };
            SetPlayerWorkRestDays();
            Say(WorkContract.LastNote);
            return true;
        }

        public bool StartPlayerWork()
        {
            if(BrandExpoExclusive)return Fail("请先完成展会现场活动");
            var c = WorkContract;
            if (IsGameOver || !HasWorkContract || IsWorking || CurrentPlace != WorkPlace) return false;
            int index = WorkShiftToJoin();
            if (index < 0) return false;
            var day = c.Days[index];
            day.State = WorkDayState.Working;
            day.LateMinutes = (int)Math.Max(0, Clock.Now.TotalMinutes - ((long)(c.StartDay + index) * 1440 + c.ShiftStart));
            var impact = PlayerWorkImpact(WorkPlace, c.DailyWageYuan, c.ShiftEnd);
            day.BonusYuan = impact.bonus;
            day.EnergyMultiplier = impact.energyMultiplier;
            day.StressPerHour = impact.stressPerHour;
            c.WorkingIndex = index;
            SetSteer(0, 0); StopTrip();
            c.LastNote = day.LateMinutes == 0 ? "已到岗，按排班时间工作，结束后恢复操作。" : $"迟到 {day.LateMinutes} 分钟，按每 30 分钟扣 ¥{c.LatePenaltyPerHalfHour}（不足 30 分钟按 30 计）。";
            Say(c.LastNote);
            return true;
        }

        private void ServeWorkMeal()
        {
            if (!IsContractWorking) return;
            var day = WorkContract.Days[WorkContract.WorkingIndex];
            if (day.LunchServed) return;
            day.LunchServed = true;
            Player.Hunger = Math.Min(100, Player.Hunger + MealGain);
            Say("店里提供了午饭，不收钱，也不消耗背包里的饭。");
        }

        private void UpdatePlayerWork(GameTimestamp to)
        {
            if (IsWeekendWorking && to.TotalMinutes >= _weekendWorkEnds)
            {
                _weekendWorkEnds = 0;
                Ledger.Credit(to.Date, "打工", Money.FromYuan(WeekendWorkPay), "周末搬货兼职");
                double energyBefore = Player.Energy, healthBefore = Player.Health;
                Player.SpendEnergy(10);
                Player.Health = Math.Max(0, Player.Health - 2);
                Popup?.Invoke("搬货收工", $"工资已到账 ¥{WeekendWorkPay}。\n额外消耗精力 {energyBefore - Player.Energy:0.#}，健康 −{healthBefore - Player.Health:0.#}。");
            }
            if (!HasWorkContract) return;
            var c = WorkContract;
            SettleCompletedWorkPay(to.Date);
            if (IsContractWorking && to.TotalMinutes >= WorkMealAt) ServeWorkMeal();
            for (int i = 0; i < 7; i++)
            {
                var day = c.Days[i];
                long end = (long)(c.StartDay + i) * 1440 + c.ShiftEnd;
                if (end > to.TotalMinutes || day.State == WorkDayState.Completed || day.State == WorkDayState.Absent || day.State == WorkDayState.Rest) continue;
                if (day.State == WorkDayState.Working)
                {
                    day.State = WorkDayState.Completed;
                    long deduction = Math.Min(c.DailyWageYuan, ((day.LateMinutes + 29) / 30) * c.LatePenaltyPerHalfHour);
                    if (c.DailyWageYuan > 0) day.BonusYuan = day.BonusYuan * (c.DailyWageYuan - deduction) / c.DailyWageYuan;
                    day.EarnedYuan = c.DailyWageYuan - deduction + day.BonusYuan;
                    SettleCompletedWorkPay(to.Date);
                    c.WorkingIndex = -1;
                    Player.Hunger = Math.Min(100, Player.Hunger + 40); // A closing meal keeps a zero-cash worker able to finish the week.
                    Player.Health = Math.Max(0, Player.Health - c.HealthCost);
                    Player.Mood = Math.Max(0, Player.Mood - c.MoodCost);
                    if (day.LateMinutes == 0 && WorkAptitudeFor(WorkPlace) == WorkAptitude.Stamina && Player.Stamina < 85)
                        Player.Stamina++;
                    c.LastNote = $"收工到账 ¥{day.EarnedYuan} · 表现奖金 ¥{day.BonusYuan} · 迟到扣 ¥{deduction}。健康 −{c.HealthCost}，心情 −{c.MoodCost}。已提供收工餐。";
                    Say(c.LastNote);
                }
                else
                {
                    day.State = WorkDayState.Absent;
                    c.LastNote = $"第 {c.StartDay + i + 1} 天没有到岗，当天 ¥{c.DailyWageYuan} 工资全扣。";
                    Say(c.LastNote);
                }
            }
            if (to.TotalMinutes >= (long)c.EndDay * 1440 + c.ShiftEnd &&
                c.Days.All(d => d.State == WorkDayState.Completed || d.State == WorkDayState.Absent || d.State == WorkDayState.Rest))
            {
                c.ReadyToPay = false;
                c.Paid = true;
                c.LastNote = $"一周合同已结束，累计实发工资 ¥{c.EarnedYuan}，迟到/缺勤合计扣 ¥{c.DeductionsYuan}。工资已每日到账，可再签下一周。";
                Say(c.LastNote);
            }
        }

        internal void StartWeekendWork()
        {
            if (!CanStartWeekendWork) return;
            SetSteer(0, 0);
            _weekendWorkEnds = Clock.Now.AddMinutes(WeekendWorkHours * 60).TotalMinutes;
        }

        private bool SettleCompletedWorkPay(GameDate date)
        {
            var c = WorkContract;
            if (c == null || c.Paid) return false;
            bool settled = false;
            for (int i = 0; i < c.Days.Length; i++)
            {
                var day = c.Days[i];
                if (day.State != WorkDayState.Completed || day.WagePaid) continue;
                day.WagePaid = true;
                if (day.EarnedYuan > 0)
                    Ledger.Credit(date, "打工", Money.FromYuan(day.EarnedYuan),
                        (WorkPlace?.Name ?? "店铺") + "第 " + (i + 1) + " 班工资（日结）");
                settled = true;
            }
            return settled;
        }

        public bool ClaimPlayerWorkPay()
        {
            if (IsWorking || !SettleCompletedWorkPay(Clock.Today)) return Fail("工资每天收工后自动到账，没有待领取的工资。");
            return true;
        }

        private void RestorePlayerWork(PlayerWorkContract saved)
        {
            WorkContract = null;
            if (saved == null || !City.HasPlace(saved.PlaceId) || saved.Days == null || saved.Days.Length != 7 || saved.Days.Any(d => d == null)
                || saved.ShiftStart < 0 || saved.ShiftStart >= 1440 || saved.ShiftEnd <= saved.ShiftStart || saved.ShiftEnd > saved.ShiftStart + 1440 || saved.DailyWageYuan < 0) return;
            WorkContract = saved.Copy();
            SetPlayerWorkRestDays();
            var c = WorkContract;
            // Old weekly contracts already paid must never pay their completed shifts again.
            if (c.Paid)
                foreach (var day in c.Days) day.WagePaid = true;
            else if (c.Days.Any(d => d.State == WorkDayState.Completed && !d.WagePaid))
            {
                SettleCompletedWorkPay(Clock.Today);
                c.LastNote = "此前已完成班次的工资已补发，之后每天收工后自动到账。";
            }
            if (c.WorkingIndex < 0 || c.WorkingIndex >= 7 || c.Days[c.WorkingIndex].State != WorkDayState.Working) c.WorkingIndex = -1;
            if (c.WorkingIndex >= 0 && CurrentPlace != WorkPlace)
            { c.Days[c.WorkingIndex].State = WorkDayState.Pending; c.WorkingIndex = -1; }
            UpdatePlayerWork(Clock.Now);
        }

        private void SetPlayerWorkRestDays()
        {
            if (!SupplierClosesOnWeekend(WorkPlace)) return;
            var contract = WorkContract;
            for (int i = 0; i < contract.Days.Length; i++)
                if (contract.Days[i].State == WorkDayState.Pending && new GameDate(contract.StartDay + i).IsWeekend)
                    contract.Days[i].State = WorkDayState.Rest;
        }

        private void InterruptPlayerWork(string note)
        {
            if (IsWeekendWorking)
            {
                _weekendWorkEnds = 0;
                Say(note);
            }
            if (!IsContractWorking) return;
            var c = WorkContract;
            c.Days[c.WorkingIndex].State = WorkDayState.Absent;
            c.WorkingIndex = -1; c.LastNote = note;
            Say(note);
            UpdatePlayerWork(Clock.Now);
        }

        // A signed, attended contract gets a bounded grace period while working.
        private bool WorkPayGrace => HasWorkContract && Clock.Today.TotalDays <= WorkContract.EndDay + WorkContract.ShiftEnd / 1440 + 1
            && (Clock.Today.TotalDays <= WorkContract.StartDay || WorkContract.Days.Any(d => d.State == WorkDayState.Working || d.EarnedYuan > 0));

        private int WorkShiftToJoin()
        {
            if (!HasWorkContract || WorkContract.ReadyToPay || SupplierClosesOnWeekend(WorkPlace) && !IsWholesalerOpen) return -1;
            var c = WorkContract;
            for (int i = 0; i < 7; i++)
            {
                long anchor = (long)(c.StartDay + i) * 1440;
                if (c.Days[i].State == WorkDayState.Pending && Clock.Now.TotalMinutes >= anchor + c.ShiftStart - 60 && Clock.Now.TotalMinutes < anchor + c.ShiftEnd) return i;
            }
            return -1;
        }
        public bool CanStartPlayerWork => !IsWorking && CurrentPlace == WorkPlace && WorkShiftToJoin() >= 0;
        private bool WorkStaffEntry(Place p) => p != null && p == WorkPlace && (IsContractWorking || !IsWorking && WorkShiftToJoin() >= 0);
        private long WorkMealAt => !IsContractWorking ? long.MaxValue : (long)(WorkContract.StartDay + WorkContract.WorkingIndex) * 1440
            + WorkContract.ShiftStart + (WorkContract.ShiftEnd - WorkContract.ShiftStart) / 2;
        public bool NightWorkRest => WorkContract != null && WorkContract.IsNightShift && Clock.Today.TotalDays <= WorkContract.EndDay + 2;
        private int NightRestMinutes
        {
            get
            {
                long next = long.MaxValue;
                if (HasWorkContract)
                    for (int i = 0; i < 7; i++)
                    {
                        long start = (long)(WorkContract.StartDay + i) * 1440 + WorkContract.ShiftStart;
                        if (WorkContract.Days[i].State == WorkDayState.Pending && start > Clock.Now.TotalMinutes) next = Math.Min(next, start);
                    }
                return next == long.MaxValue ? 480 : (int)Math.Max(0, Math.Min(480, next - 60 - Clock.Now.TotalMinutes));
            }
        }

        private void AddWorkSchedule(System.Collections.Generic.List<ScheduleEntry> entries)
        {
            if (!HasWorkContract) return;
            var c = WorkContract;
            for (int i = 0; i < 7; i++)
                if (c.Days[i].State == WorkDayState.Pending || c.Days[i].State == WorkDayState.Working)
                    entries.Add(new ScheduleEntry { Minute = (long)(c.StartDay + i) * 1440 + c.ShiftStart, Day = DescribeDay(c.StartDay + i), TimeText = WorkHours,
                        Kind = "work", Text = "去" + (WorkPlace?.Name ?? "签约店铺") + "打工 · 日薪 ¥" + c.DailyWageYuan + "，迟到扣薪，收工自动日结" });
        }
    }
}
