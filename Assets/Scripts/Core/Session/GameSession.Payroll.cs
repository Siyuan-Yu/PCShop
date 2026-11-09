using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class CityPendingDisplay { public string ShopId;public int Tier,Minutes; }
    [Serializable] public sealed class EmployeeSchedule
    {
        public long ActiveStart = -1, ActiveEnd = -1, NextNotBefore = -1, PendingAt = -1;
        public int PendingStart = -1, PendingEnd = -1;
        public string WorkTravelTarget;
        public EmployeeSchedule Copy() => (EmployeeSchedule)MemberwiseClone();
    }
    [Serializable] public sealed class EmployeeWorkMinutes
    {
        public string ShopId;
        public int Minutes, OvertimeMinutes;
        public long PaidOvertimeCents;
        public long AccruedOvertimeCents;
        public EmployeeWorkMinutes Copy() => (EmployeeWorkMinutes)MemberwiseClone();
    }
    [Serializable] public sealed class EmployeePayrollDay
    {
        public string EmployeeId, Name;
        public int Workday;
        public long Start, End, DailyCents;
        public int Minutes;
        public bool BasePaid;
        public long LastMinute = -1;
        public List<EmployeeWorkMinutes> Shops = new List<EmployeeWorkMinutes>();
        public long OvertimeCents => (long)Math.Round((decimal)DailyCents * Math.Max(0, Minutes - 600) / 300m, 0, MidpointRounding.AwayFromZero);
        public EmployeePayrollDay Copy() {var p=(EmployeePayrollDay)MemberwiseClone();p.Shops=(Shops??new List<EmployeeWorkMinutes>()).Where(x=>x!=null).Select(x=>x.Copy()).ToList();return p;}
    }
    public sealed partial class GameSession
    {
        private readonly List<EmployeePayrollDay> _employeePayroll = new List<EmployeePayrollDay>();
        public IReadOnlyList<EmployeePayrollDay> EmployeePayroll => _employeePayroll;
        private void LoadEnergyPayroll(SaveData d)
        {
            foreach(var p in d.pendingDisplays??Array.Empty<CityPendingDisplay>())
            {var shop=ShopAt(p.ShopId);if(shop!=null&&p.Tier>=0&&p.Tier<PCShopSell.Core.Catalog.PartCatalog.TierCount){shop.PendingDisplayTier=p.Tier;shop.PendingDisplayMinutes=Math.Max(0,p.Minutes);}}
            _employeePayroll.Clear();foreach(var p in d.payrollDays??Array.Empty<EmployeePayrollDay>())
                if(p!=null&&!string.IsNullOrEmpty(p.EmployeeId)&&!_employeePayroll.Any(x=>x.EmployeeId==p.EmployeeId&&x.Workday==p.Workday))_employeePayroll.Add(p.Copy());
            _energyExhaustedAt=Player.Energy<=0?(d.energyPayrollVersion>0&&d.exhaustedAt>=0?Math.Min(Clock.Now.TotalMinutes,d.exhaustedAt):Clock.Now.TotalMinutes):-1;
            _energyReminder=d.energyPayrollVersion>0?d.exhaustionReminder:1;_pendingFaint=false;
            // No past overtime is invented. The first counted minute is after migration.
            foreach(var row in Employees)PrepareEmployeeShift(row.Person,Clock.Now.TotalMinutes);
        }
        public static int ShiftMinutes(int start, int end) => (end - start + 1440) % 1440;
        private static bool EmployeeShiftOverlaps(Employee e,long from,long until)
        {
            if(e.TryShift(from,out _,out _))return true;
            for(long day=from/1440-1;day<=until/1440;day++)
                foreach(int minute in new[]{e.ShiftStart,e.Schedule?.PendingStart??-1})
                {
                    long candidate=day*1440+minute;
                    if(minute>=0&&candidate>=from&&candidate<until&&e.TryShift(candidate,out _,out _))return true;
                }
            return false;
        }
        public bool SetEmployeeExhibitionWork(string activityId,string employeeId,bool work)
        {
            var a=_cityActivities.FirstOrDefault(x=>x.Id==activityId&&x.State==CityActivityPhase.Scheduled&&x.DefinitionId.StartsWith("expo_",StringComparison.Ordinal));
            var row=FindEmployee(employeeId);if(a==null||row?.Shop==null)return Fail("只可在展会出发前明确安排员工展位工作。");
            var participant=a.Participants.FirstOrDefault(p=>p.PersonId==PersonIdOf(row.Person));if(participant==null)return false;
            participant.WorkForEmployer=work;if(work)a.EmployeeLeaveApproved=true;return true;
        }
        public static Money EstimateShiftPay(Employee e, int start, int end) => e.DailyWage + Money.FromCents((long)Math.Round((decimal)e.DailyWage.Cents * Math.Max(0, ShiftMinutes(start,end)-600)/300m,0,MidpointRounding.AwayFromZero));
        public string EmployeePaySummary(Employee e)
        {
            var schedule = e.Schedule ?? new EmployeeSchedule();
            int start=schedule.PendingAt>=0?schedule.PendingStart:e.ShiftStart, end=schedule.PendingAt>=0?schedule.PendingEnd:e.ShiftEnd;
            int duration=ShiftMinutes(start,end);
            var current=_employeePayroll.LastOrDefault(p=>p.EmployeeId==e.Id);
            return $"{start/60:00}:{start%60:00}—{(end<start?"次日":"")}{end/60:00}:{end%60:00} · 共{duration/60.0:0.##}小时\n标准10小时 · 加班{Math.Max(0,duration-600)/60.0:0.##}小时 · 基础日薪{e.DailyWage} · 基础时薪{Money.FromCents(e.DailyWage.Cents/10)}\n预计加班{EstimateShiftPay(e,start,end)-e.DailyWage} · 合计{EstimateShiftPay(e,start,end)}" +
                (schedule.PendingAt>=0?"\n新日常排班从下一班生效，当前班次保留。":"") +
                (current==null?"":$"\n最近工作日：实际{current.Minutes}分钟 · 应付合计{Money.FromCents(current.DailyCents+current.OvertimeCents)}\n基础工资{Money.FromCents(current.DailyCents)}（{(current.BasePaid?"已付":"待付")}）；加班{Money.FromCents(current.OvertimeCents)}，已付{Money.FromCents(current.Shops.Sum(x=>x.PaidOvertimeCents))}");
        }
        private void PrepareEmployeeShift(Employee e,long at)
        {
            var schedule=e.Schedule??(e.Schedule=new EmployeeSchedule());
            if(schedule.PendingAt>=0&&at>=schedule.PendingAt)
            {
                e.ShiftStart=schedule.PendingStart;e.ShiftEnd=schedule.PendingEnd;
                schedule.NextNotBefore=Math.Max(schedule.NextNotBefore,schedule.PendingAt);schedule.PendingAt=-1;
            }
            if(e.TryShift(at,out long start,out long end)) { schedule.ActiveStart=start;schedule.ActiveEnd=end; }
        }
        private EmployeePayrollDay PayrollDay(Employee e,long start,long end)
        {
            int day=(int)(start/1440);
            var p=_employeePayroll.FirstOrDefault(x=>x.EmployeeId==e.Id&&x.Workday==day);
            if(p==null)
            {
                p=new EmployeePayrollDay{EmployeeId=e.Id,Name=e.Name,Workday=day,Start=start,End=end,DailyCents=e.DailyWage.Cents};
                _employeePayroll.Add(p);
            }
            p.End=Math.Max(p.End,end);return p;
        }
        private bool EmployeeActuallyWorking(EmployeeAssignment row)
        {
            if(row.Shop==null)return false;
            if(EmployeeTrip(row.Person.Id)!=null)return true;
            var p=CityPersonState(PersonIdOf(row.Person));
            var workActivity=_cityActivities.FirstOrDefault(a=>a.Active&&a.Id==p?.ActivityId);
            if(workActivity!=null&&workActivity.Participants.Any(x=>x.PersonId==p.PersonId&&x.WorkForEmployer&&x.Phase!=CityParticipantPhase.NotDeparted&&x.Phase!=CityParticipantPhase.Left))return true;
            string workTarget=row.Person.Schedule?.WorkTravelTarget;
            if(p!=null&&workTarget!=null&&(p.Route!=null?p.Route.TargetPlaceId==workTarget:p.LocationPlaceId!=workTarget))return true;
            if(p?.Route==null&&row.Person.Schedule!=null&&p?.LocationPlaceId==workTarget)row.Person.Schedule.WorkTravelTarget=null;
            return p!=null&&p.Route==null&&p.LocationPlaceId==row.Shop.Place.Id&&string.IsNullOrEmpty(p.ActivityId);
        }
        private void TickEmployeePayroll(long from,long to)
        {
            foreach(var row in Employees)
            {
                var e=row.Person;PrepareEmployeeShift(e,from);
                if(!e.TryShift(from,out long start,out long end))continue;
                var p=PayrollDay(e,start,end);
                if(EmployeeActuallyWorking(row)&&p.LastMinute<from)
                {
                    int minutes=(int)Math.Max(0,Math.Min(to,end)-Math.Max(from,start));
                    var shop=p.Shops.FirstOrDefault(x=>x.ShopId==row.Shop.Place.Id);
                    if(shop==null){shop=new EmployeeWorkMinutes{ShopId=row.Shop.Place.Id};p.Shops.Add(shop);}
                    long priorCost=p.OvertimeCents;
                    int oldOvertime=Math.Max(0,p.Minutes-600);p.Minutes+=minutes;shop.Minutes+=minutes;
                    shop.OvertimeMinutes+=Math.Max(0,p.Minutes-600)-oldOvertime;p.LastMinute=from;
                    shop.AccruedOvertimeCents+=p.OvertimeCents-priorCost;
                }
                // The base remains on the original daily midnight cycle. Overtime is paid
                // at shift end (or subsequent midnight), never before it is actually worked.
                if(to>=end)PayPayrollOvertime(p);
                PrepareEmployeeShift(e,to);
            }
        }
        private void PayPayrollBase(EmployeePayrollDay p)
        {
            if(p.BasePaid)return;
            if(p.DailyCents>0)Ledger.ForceDebit(Clock.Today,"工资",Money.FromCents(p.DailyCents),p.Name+" 基础工资（工作日"+p.Workday+"）",
                LedgerAttribution.ForShop(p.End<=Clock.Now.TotalMinutes&&p.Shops.Count==1?p.Shops[0].ShopId:null,"payroll:base:"+p.EmployeeId+":"+p.Workday,"基础工资").Payroll(p.EmployeeId,p.Workday));
            p.BasePaid=true;
        }
        private void PayPayrollOvertime(EmployeePayrollDay p)
        {
            foreach(var shop in p.Shops)
            {
                long target=shop.AccruedOvertimeCents;
                long delta=target-shop.PaidOvertimeCents;if(delta<=0)continue;
                Ledger.ForceDebit(Clock.Today,"工资",Money.FromCents(delta),p.Name+" 加班工资（工作日"+p.Workday+"）",
                    LedgerAttribution.ForShop(shop.ShopId,"payroll:ot:"+p.EmployeeId+":"+p.Workday+":"+shop.ShopId+":"+target,"加班工资").Payroll(p.EmployeeId,p.Workday));
                shop.PaidOvertimeCents=target;
            }
        }
        private void PayEmployeeWorkday(GameDate day)
        {
            foreach(var p in _employeePayroll.Where(p=>p.Workday<=day.TotalDays)) {PayPayrollBase(p);PayPayrollOvertime(p);}
            foreach(var row in Employees)
            {
                var e=row.Person;
                var records=_employeePayroll.Where(p=>p.EmployeeId==e.Id&&p.Workday==day.TotalDays).ToArray();
                if(records.Length==0)
                {
                    // Daily wage entitlement (including unassigned staff) stays unchanged.
                    // A night shift that began yesterday owns today's early hours, not a new wage.
                    var p=PayrollDay(e,(long)day.TotalDays*1440+e.ShiftStart,(long)day.TotalDays*1440+e.ShiftStart+ShiftMinutes(e.ShiftStart,e.ShiftEnd));
                    records=new[]{p};
                }
                foreach(var p in records){PayPayrollBase(p);PayPayrollOvertime(p);}
            }
        }
        public Money EstimatedEmployeeOvertime(int days) => Money.FromCents(Employees.Sum(row=>EstimateShiftPay(row.Person,row.Person.Schedule?.PendingAt>=0?row.Person.Schedule.PendingStart:row.Person.ShiftStart,row.Person.Schedule?.PendingAt>=0?row.Person.Schedule.PendingEnd:row.Person.ShiftEnd).Cents-row.Person.DailyWage.Cents)*days);
    }
}
