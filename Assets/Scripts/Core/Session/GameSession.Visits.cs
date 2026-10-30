using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    /// <summary>约好的师傅上门：到点来，家里没人就修不了。</summary>
    public sealed class HomeVisit
    {
        /// <summary>要修的东西（家具 id，比如坏电风扇）。</summary>
        public string TargetId;

        /// <summary>约好的开始时间（游戏总分钟数）。</summary>
        public long StartMinute;

        /// <summary>说好的上门费（晚上已经含加价）。</summary>
        public Money Price;

        /// <summary>师傅进门开始干活的时间（游戏总分钟数）；还没进门是 -1。</summary>
        public long WorkStart = -1;

        public bool Working => WorkStart >= 0;
    }

    /// <summary>日程里的一条：给“日程”界面看的。</summary>
    public sealed class ScheduleEntry
    {
        public long Minute;
        public bool HasTime = true;
        public string Day;
        public string TimeText;
        public string Text;
        public string Kind;
        public int Index = -1;
    }

    /// <summary>约师傅上门修东西，以及汇总所有安排的日程。</summary>
    public sealed partial class GameSession
    {
        public const int RepairLeadMinutes = 60;
        public const int RepairGraceMinutes = 15;
        /// <summary>师傅进门到修好走人要多久。</summary>
        public const int RepairWorkMinutes = 30;
        public const int RepairBookDays = 3;
        public const int RepairFirstHour = 7;
        public const int RepairLastHour = 23;
        public const double RepairNightMultiplier = 1.5;

        private readonly List<HomeVisit> _visits = new List<HomeVisit>();

        public IReadOnlyList<HomeVisit> HomeVisits => _visits;

        /// <summary>晚上（18:00 – 次日 6:00）师傅上门要加价。</summary>
        public static bool IsRepairNightMinute(int minuteOfDay) => minuteOfDay >= 18 * 60 || minuteOfDay < 6 * 60;

        public bool IsRepairNight => IsRepairNightMinute(Clock.MinuteOfDay);

        /// <summary>约在这个时间上门要多少钱（按上门那一刻算，晚上加收 50%）。</summary>
        public Money RepairPrice(FurnitureDef d, long startMinute)
        {
            int mod = (int)(startMinute % GameTimestamp.MinutesPerDay);
            return IsRepairNightMinute(mod) ? d.RepairCost.Scale(RepairNightMultiplier) : d.RepairCost;
        }

        /// <summary>家里坏了的东西的家具 id（去重，按摆放顺序）。</summary>
        public IEnumerable<string> BrokenHomeItems()
        {
            var seen = new HashSet<string>();
            foreach (PlacedFurniture p in _homeLayout)
            {
                FurnitureDef d = FurnitureCatalog.Get(p.Id);
                if (d != null && d.RepairInto != null && seen.Add(d.Id)) yield return d.Id;
            }
        }

        public HomeVisit VisitFor(string targetId) => _visits.FirstOrDefault(v => v.TargetId == targetId);

        /// <summary>师傅正在屋里修东西（这期间不能取消、不能再约）。</summary>
        /// <summary>师傅正在屋里修东西时不能出门；能出门返回 null。</summary>
        public string LeaveBlocker
        {
            get
            {
                if (CurrentPlace == null || CurrentPlace != Home) return null;
                HomeVisit v = RepairmanAtWork;
                if (v == null) return null;
                FurnitureDef d = FurnitureCatalog.Get(v.TargetId);
                return $"师傅正在屋里修{(d != null ? "「" + d.Name + "」" : "东西")}，还要约 {RepairWorkMinutesLeft(v)} 分钟，修好之前不能出门。";
            }
        }

        public HomeVisit RepairmanAtWork => _visits.FirstOrDefault(v => v.Working);

        public long RepairWorkMinutesLeft(HomeVisit v) => Math.Max(0, v.WorkStart + RepairWorkMinutes - Clock.Now.TotalMinutes);

        public string RepairBookBlocker(string targetId, long startMinute)
        {
            FurnitureDef d = FurnitureCatalog.Get(targetId);
            if (d == null || d.RepairInto == null) return "这个没坏";
            if (!BrokenHomeItems().Contains(targetId)) return "屋里没有这个";
            if (VisitFor(targetId) != null) return "已经约了师傅";
            if (_visits.Count > 0) return "师傅今天没空再接一单";
            if (startMinute < Clock.Now.TotalMinutes + RepairLeadMinutes) return "太急了，师傅至少要提前 1 小时约";
            int day = (int)(startMinute / GameTimestamp.MinutesPerDay);
            if (day > Clock.Today.TotalDays + RepairBookDays - 1) return "只能约 3 天内的";
            int hour = (int)(startMinute % GameTimestamp.MinutesPerDay) / 60;
            if (hour < RepairFirstHour || hour > RepairLastHour) return "师傅这个钟点不上门";
            return null;
        }

        /// <summary>约师傅：定一个时间，到点才有人来。下单就付钱。</summary>
        public bool BookRepair(string targetId, long startMinute)
        {
            string blocker = RepairBookBlocker(targetId, startMinute);
            if (blocker != null) return Fail(blocker);
            FurnitureDef d = FurnitureCatalog.Get(targetId);
            Money price = RepairPrice(d, startMinute);
            if (!Ledger.TryDebit(Clock.Today, "维修", price, d.Name + "上门维修（预付）")) return Fail("钱不够，师傅要先付上门费。");
            _visits.Add(new HomeVisit { TargetId = targetId, StartMinute = startMinute, Price = price });
            Say($"约好了：{DescribeDay((int)(startMinute / GameTimestamp.MinutesPerDay))} {ClockText((int)(startMinute % GameTimestamp.MinutesPerDay))} 师傅上门修「{d.Name}」，已预付 {price}。到时候人要在家。");
            return true;
        }

        public bool WaitForRepair()
        {
            if (CurrentPlace != Home) return Fail("需要在家等候。");
            var work = _visits.Where(v => v.Working).ToList();
            if (work.Count == 0) return false;
            int minutes = (int)Math.Max(1, work.Max(v => v.WorkStart + RepairWorkMinutes) - Clock.Now.TotalMinutes);
            AdvanceClock(minutes + 1);
            return true;
        }

        public bool CancelRepair(string targetId)
        {
            HomeVisit v = VisitFor(targetId);
            if (v == null) return false;
            if (v.Working) return Fail("师傅已经在屋里干活了，取消不了。");
            _visits.Remove(v);
            Say($"取消了师傅上门。上门费 {v.Price} 预付了，不退。");
            return true;
        }

        public static string ClockText(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";

        /// <summary>
        /// 每次时间推进后看师傅：到点人在家就进门干活（30 分钟），干完修好、走人；
        /// 等了 15 分钟家里还没人就走了。上门费下单时已经付过了，取消或白跑都不退。
        /// </summary>
        private void CheckHomeVisits(GameTimestamp from, GameTimestamp to)
        {
            for (int i = _visits.Count - 1; i >= 0; i--)
            {
                HomeVisit v = _visits[i];
                FurnitureDef d = FurnitureCatalog.Get(v.TargetId);
                int idx = _homeLayout.FindIndex(p => p.Id == v.TargetId);
                if (d == null || d.RepairInto == null || idx < 0)
                {
                    _visits.RemoveAt(i);
                    Ledger.Credit(to.Date, "维修", v.Price, "师傅上门取消（退款）");
                    continue;
                }

                if (!v.Working)
                {
                    if (to.TotalMinutes < v.StartMinute) continue;
                    bool atHome = CurrentPlace == Home;
                    bool wasThere = atHome && (from.TotalMinutes <= v.StartMinute || to.TotalMinutes <= v.StartMinute + RepairGraceMinutes);
                    if (wasThere)
                    {
                        v.WorkStart = from.TotalMinutes <= v.StartMinute ? v.StartMinute : to.TotalMinutes;
                        Say($"师傅到了，开始修「{d.Name}」，大约 {RepairWorkMinutes} 分钟。");
                    }
                    else if (to.TotalMinutes > v.StartMinute + RepairGraceMinutes)
                    {
                        _visits.RemoveAt(i);
                        Popup?.Invoke("师傅白跑了", $"师傅准时上门，敲了半天门没人，等了 {RepairGraceMinutes} 分钟走了。预付的上门费 {v.Price} 不退，东西还是坏的。");
                        continue;
                    }
                    else continue;
                }

                if (to.TotalMinutes >= v.WorkStart + RepairWorkMinutes)
                {
                    _visits.RemoveAt(i);
                    _homeLayout[idx].Id = d.RepairInto;
                    Popup?.Invoke("师傅走了", $"师傅把「{d.Name}」修好了，收拾工具走了（上门费 {v.Price} 下单时已付）。");
                }
            }
        }

        // ---------- 日程 ----------

        /// <summary>日历 app 提前多少分钟提醒。</summary>
        public const int ReminderLeadMinutes = 30;

        /// <summary>
        /// 只有智能手机的日历会提醒：约会、师傅上门前 30 分钟弹一句。
        /// 没有智能手机，只有笔记本，得自己记得看。
        /// </summary>
        private void CheckCalendarReminders(GameTimestamp from, GameTimestamp to)
        {
            if (!HasCalendar) return;
            long f = from.TotalMinutes, t = to.TotalMinutes;
            foreach (ScheduleEntry e in Schedule())
            {
                if (e.Kind != "appointment" && e.Kind != "repair") continue;
                long at = e.Minute - ReminderLeadMinutes;
                if (f < at && at <= t && t <= e.Minute)
                    Say($"日历提醒：{e.TimeText} {e.Text}（还有 {e.Minute - t} 分钟）");
            }
        }

        /// <summary>未来要发生的事，按时间排好：约会、师傅上门、焖饭好了、快递到货。</summary>
        public List<ScheduleEntry> Schedule()
        {
            var l = new List<ScheduleEntry>();
            long now = Clock.Now.TotalMinutes;
            for (int i = 0; i < _appointments.Count; i++)
            {
                Appointment a = _appointments[i];
                long start = (long)a.Day * GameTimestamp.MinutesPerDay + a.StartMinute;
                l.Add(new ScheduleEntry
                {
                    Minute = start, Day = DescribeDay(a.Day), TimeText = ClockText(a.StartMinute), Kind = "appointment", Index = i,
                    Text = $"和{Npcs.Get(a.NpcId).Name}出去（{(a.Slot == OutingSlot.Evening ? "晚上" : "周末白天")}）",
                });
            }
            foreach (HomeVisit v in _visits)
            {
                FurnitureDef d = FurnitureCatalog.Get(v.TargetId);
                if (v.Working)
                {
                    long end = v.WorkStart + RepairWorkMinutes;
                    l.Add(new ScheduleEntry
                    {
                        Minute = end, Day = DescribeDay((int)(end / GameTimestamp.MinutesPerDay)), TimeText = ClockText((int)(end % GameTimestamp.MinutesPerDay)), Kind = "repairing",
                        Text = $"师傅正在修「{d?.Name ?? v.TargetId}」，还要 {RepairWorkMinutesLeft(v)} 分钟",
                    });
                    continue;
                }
                int day = (int)(v.StartMinute / GameTimestamp.MinutesPerDay);
                l.Add(new ScheduleEntry
                {
                    Minute = v.StartMinute, Day = DescribeDay(day), TimeText = ClockText((int)(v.StartMinute % GameTimestamp.MinutesPerDay)), Kind = "repair", Index = 0,
                    Text = $"师傅上门修「{d?.Name ?? v.TargetId}」（{v.Price} 已付，要在家）",
                });
            }
            if (Pantry.Cooking != null)
            {
                long ready = Pantry.Cooking.ReadyMinute;
                l.Add(new ScheduleEntry
                {
                    Minute = ready, Day = DescribeDay((int)(ready / GameTimestamp.MinutesPerDay)), TimeText = ClockText((int)(ready % GameTimestamp.MinutesPerDay)),
                    Kind = "cook", Text = $"电饭锅焖好{Pantry.Cooking.Batch.Name}（{Pantry.Cooking.Batch.Count} 碗）",
                });
            }
            foreach (Parcel p in _parcels)
            {
                l.Add(new ScheduleEntry
                {
                    Minute = (long)Math.Max(p.DueDay, Clock.Today.TotalDays) * GameTimestamp.MinutesPerDay, HasTime = false,
                    Day = DescribeDay(Math.Max(p.DueDay, Clock.Today.TotalDays)), TimeText = "", Kind = "parcel", Text = "快递到家：" + ParcelDescription(p),
                });
            }
            l.Sort((a, b) => a.Minute != b.Minute ? a.Minute.CompareTo(b.Minute) : string.CompareOrdinal(a.Kind, b.Kind));
            return l;
        }

        private static string ParcelDescription(Parcel p)
        {
            switch (p.Kind)
            {
                case ParcelKind.Furniture: return (FurnitureCatalog.Get(p.Id)?.Name ?? p.Id) + (p.Count > 1 ? "×" + p.Count : "");
                case ParcelKind.Outfit: return Outfits.Get(p.Id)?.Name ?? p.Id;
                case ParcelKind.Gift: return (Gifts.Get(p.Id)?.Name ?? p.Id) + (p.Count > 1 ? "×" + p.Count : "");
                default: return "大米 10 斤" + (p.Count > 1 ? "×" + p.Count : "");
            }
        }
    }
}