using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public enum EmployeeRequestKind { None, Pay, ShorterShift, Leave, Transfer }
    public enum EmployeeRequestResponse { Pending, Promised, Deferred, Declined, Fulfilled }

    [Serializable]
    public sealed class EmployeeWorkRequest
    {
        public EmployeeRequestKind Kind;
        public EmployeeRequestResponse Response;
        public string Reason, OriginalPosition;
        public long TargetWage, LeaveFrom = -1, LeaveUntil = -1;
        public int CreatedAfterShift, DueAfterShift;
        public EmployeeRole PreferredRole;
        public bool DeferredOnce;
        public EmployeeWorkRequest Copy() => (EmployeeWorkRequest)MemberwiseClone();
        public bool Outstanding => Kind != EmployeeRequestKind.None && Response != EmployeeRequestResponse.Fulfilled;
    }

    [Serializable]
    public sealed class EmployeeWellbeingChange
    {
        public long At;
        public int Delta;
        public string[] Reasons;
        public EmployeeWellbeingChange Copy() => new EmployeeWellbeingChange {At=At,Delta=Delta,Reasons=(string[])Reasons?.Clone()};
    }

    [Serializable]
    public sealed class EmployeeWellbeing
    {
        public double Satisfaction = 65, Energy = 100;
        public long JoinedAt = -1, LastTickAt = -1, LastShiftEnd = -1;
        public long EmploymentEndedAt = -1;
        public long StartingWage, ExpectedWage, PendingWage = -1, PayEffectiveAt = -1;
        public long LeaveFrom = -1, LeaveUntil = -1;
        public bool LeaveCredited;
        public int CompletedShifts, LongShiftStreak, MismatchShifts, LowShifts;
        public int LastSettledDay = -1, NoticeAfterShift = -1;
        public string Position;
        public EmployeeWorkRequest Request;
        public EmployeeWellbeingChange[] Changes = Array.Empty<EmployeeWellbeingChange>();
        public EmployeeWellbeing Copy()
        {
            var copy = (EmployeeWellbeing)MemberwiseClone(); copy.Request = Request?.Copy();
            copy.Changes = (Changes ?? Array.Empty<EmployeeWellbeingChange>()).Select(x => x.Copy()).ToArray(); return copy;
        }
        public bool OnLeave(long at) => LeaveFrom >= 0 && at >= LeaveFrom && at < LeaveUntil;
    }

    public sealed partial class GameSession
    {
        public const int EmployeeInitialSatisfaction = 65;
        public const int EmployeeRequestGraceShifts = 3;
        public const int EmployeeNoticeShifts = 3;
        public const int EmployeeWellbeingMaximumShiftChange = 3;

        public EmployeeWellbeing EmployeeWellbeingOf(string id) => FindEmployee(id) is EmployeeAssignment row ? EnsureEmployeeWellbeing(row.Person, Clock.Now.TotalMinutes) : null;
        public static string EmployeeSatisfactionLabel(double value) => value >= 80 ? "满意" : value >= 60 ? "稳定" : value >= 40 ? "不满" : "需处理";
        public static string EmployeeRequestName(EmployeeRequestKind kind) => kind == EmployeeRequestKind.Pay ? "希望加薪"
            : kind == EmployeeRequestKind.ShorterShift ? "希望缩短班次" : kind == EmployeeRequestKind.Leave ? "申请带薪休假"
            : kind == EmployeeRequestKind.Transfer ? "希望调整岗位" : "暂无请求";
        public static string EmployeeResponseName(EmployeeRequestResponse response) => response == EmployeeRequestResponse.Promised ? "已同意 · 待执行"
            : response == EmployeeRequestResponse.Deferred ? "已推迟" : response == EmployeeRequestResponse.Declined ? "已拒绝 · 未改善"
            : response == EmployeeRequestResponse.Fulfilled ? "已兑现" : "待处理";
        public static double EmployeeWorkRate(Employee person)
        {
            var state = person?.Wellbeing; if (state == null) return 1;
            double morale = state.Satisfaction >= 80 ? 1.05 : state.Satisfaction >= 60 ? 1 : state.Satisfaction >= 40 ? .97 : .94;
            return Math.Max(.90, morale * (state.Energy < 30 ? .95 : 1));
        }
        public static int EmployeeTaskMinutes(int baseline, Employee employee) => Math.Max(1, (int)Math.Round(baseline / EmployeeWorkRate(employee), MidpointRounding.AwayFromZero));
        public bool EmployeeNeedsAttention(Employee employee) => employee != null && (employee.Wellbeing?.Satisfaction < 40
            || employee.Wellbeing?.Request?.Outstanding == true || employee.Wellbeing?.NoticeAfterShift >= 0);
        public string EmployeeAttentionLabel(Employee employee) => employee?.Wellbeing?.NoticeAfterShift>=0?"离职预告"
            :employee?.Wellbeing?.Request?.Outstanding==true?EmployeeRequestName(employee.Wellbeing.Request.Kind):"满意度偏低";
        public bool EmployeeHandoverPending(string id) => _employeeTrips.Any(trip=>trip.EmployeeId==id);

        private EmployeeWellbeing EnsureEmployeeWellbeing(Employee employee, long now)
        {
            if (employee.Wellbeing == null)
                employee.Wellbeing = new EmployeeWellbeing {JoinedAt=now,LastTickAt=now,StartingWage=Math.Max(1,employee.DailyWageYuan),ExpectedWage=Math.Max(1,employee.DailyWageYuan)};
            var state = employee.Wellbeing;
            state.Satisfaction = ClampEmployeeValue(state.Satisfaction, 65); state.Energy = ClampEmployeeValue(state.Energy,100);
            if (state.StartingWage <= 0) state.StartingWage = Math.Max(1,employee.DailyWageYuan);
            if (state.ExpectedWage <= 0) state.ExpectedWage = state.StartingWage;
            if (state.LastTickAt < 0 || state.LastTickAt > now) state.LastTickAt = now;
            state.Changes ??= Array.Empty<EmployeeWellbeingChange>();
            return state;
        }
        private static double ClampEmployeeValue(double value, double fallback) => double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(0,Math.Min(100,value));
        private string EmployeePositionKey(EmployeeAssignment row)
        {
            var post = CompanyPostOf(row.Person.Id);
            return post == null ? (row.Shop?.Place.Id ?? "standby") + ":" + row.Role
                : (post.ManagedStoreId ?? "department:" + post.Department) + ":" + post.Leader;
        }
        private long ExpectedEmployeeWage(EmployeeAssignment row)
        {
            var state = row.Person.Wellbeing; var post = CompanyPostOf(row.Person.Id);
            double factor = post?.ManagedStoreId != null ? 1.25 : post?.Leader == true ? 1.15 : 1;
            return Math.Max(1,(long)Math.Ceiling(state.StartingWage * factor));
        }

        private void PrepareEmployeeWellbeingPay(Employee employee, long at)
        {
            var state = EnsureEmployeeWellbeing(employee,at);
            if (state.PendingWage < 0 || state.PayEffectiveAt > at) return;
            employee.DailyWageYuan = state.PendingWage; state.PendingWage = -1; state.PayEffectiveAt = -1;
        }

        public bool SetEmployeeDailyWage(string id, long yuan)
        {
            if(EmployeeAuthorityBlocker(id) is string authority)return Fail(authority);
            var row = FindEmployee(id);
            if (IsGameOver || row == null || yuan < 1 || yuan > 1000000) return Fail("请输入 1–1,000,000 元的日薪。");
            var employee = row.Person; var state = EnsureEmployeeWellbeing(employee,Clock.Now.TotalMinutes);
            PrepareEmployeeShift(employee,Clock.Now.TotalMinutes);
            if (employee.TryShift(Clock.Now.TotalMinutes,out long start,out long end))
            {
                PayrollDay(employee,start,end); state.PendingWage=yuan;state.PayEffectiveAt=end;
                Say(employee.Name+"的新日薪 ¥"+yuan+" 从本班结束后生效，当前班工资不变。");
            }
            else { employee.DailyWageYuan=yuan;state.PendingWage=-1;state.PayEffectiveAt=-1;Say(employee.Name+"的下次班次日薪已调整为 ¥"+yuan+"。"); }
            return true;
        }

        public string EmployeeLeaveBlocker(string id)
        {
            if(EmployeeAuthorityBlocker(id) is string authority)return authority;
            var row = FindEmployee(id); if (IsGameOver || row == null) return "员工不可安排";
            var state = EnsureEmployeeWellbeing(row.Person,Clock.Now.TotalMinutes);
            if (state.LeaveUntil > Clock.Now.TotalMinutes) return "已有待执行或进行中的休假";
            if (EmployeeHandoverPending(id)) return "请先等采购人员和货物返店";
            if (BrandExpoPersonReserved(PersonIdOf(row.Person))) return "请先处理已安排的展会工作";
            return null;
        }
        public (long from, long until) EmployeeLeavePeriod(string id, int days = 1)
        {
            var employee = FindEmployee(id)?.Person; if (employee == null) return (-1,-1);
            long earliest = Clock.Now.TotalMinutes + 1;
            if (employee.TryShift(Clock.Now.TotalMinutes,out _,out long end)) earliest=Math.Max(earliest,end);
            int start = employee.Schedule?.PendingAt >= 0 ? employee.Schedule.PendingStart : employee.ShiftStart;
            long next = earliest / 1440 * 1440 + start; if (next < earliest) next+=1440;
            return (next,next+Math.Max(1,Math.Min(3,days))*1440L);
        }
        public bool ApproveEmployeeLeave(string id, int days = 1)
        {
            string blocker = EmployeeLeaveBlocker(id); if(blocker!=null)return Fail(blocker);
            var employee=FindEmployee(id).Person;var state=employee.Wellbeing;var period=EmployeeLeavePeriod(id,days);
            state.LeaveFrom=period.from;state.LeaveUntil=period.until;state.LeaveCredited=false;
            if (state.Request?.Outstanding == true && state.Request.Kind == EmployeeRequestKind.Leave)
            {state.Request.Response=EmployeeRequestResponse.Promised;state.Request.LeaveFrom=period.from;state.Request.LeaveUntil=period.until;}
            Say(employee.Name+"的带薪休假已安排，从下一可用班次开始；原岗位保留，请安排替班。"); return true;
        }

        public bool RespondEmployeeRequest(string id, EmployeeRequestResponse response)
        {
            if(EmployeeAuthorityBlocker(id) is string authority)return Fail(authority);
            var row=FindEmployee(id);if(IsGameOver||row==null)return false;
            var state=EnsureEmployeeWellbeing(row.Person,Clock.Now.TotalMinutes);var request=state.Request;
            if(request?.Outstanding!=true)return Fail("没有待处理请求。");
            if(response==EmployeeRequestResponse.Deferred)
            {
                if(request.DeferredOnce)return Fail("这项请求已经推迟过一次。");
                request.DeferredOnce=true;request.DueAfterShift=Math.Max(request.DueAfterShift,state.CompletedShifts)+2;
                request.Response=response;Say(row.Person.Name+"的请求已延后两个实际班次。");return true;
            }
            if(response==EmployeeRequestResponse.Declined)
            {request.Response=response;Say("已拒绝"+row.Person.Name+"的请求；原因和工作安排会继续影响满意度。");return true;}
            if(response!=EmployeeRequestResponse.Promised)return false;
            if(request.Kind==EmployeeRequestKind.Pay && !SetEmployeeDailyWage(id,Math.Max(request.TargetWage,row.Person.DailyWageYuan)))return false;
            if(request.Kind==EmployeeRequestKind.ShorterShift)
            {
                int start=row.Person.Schedule?.PendingAt>=0?row.Person.Schedule.PendingStart:row.Person.ShiftStart;
                if(!SetEmployeeShift(id,start,(start+600)%1440))return false;
            }
            if(request.Kind==EmployeeRequestKind.Leave)return ApproveEmployeeLeave(id);
            request.Response=response;
            Say(request.Kind==EmployeeRequestKind.Transfer ? "已同意换岗请求，请选择岗位；到新岗位实际工作后才算兑现。" : "已同意请求，执行后更新满意度。");
            return true;
        }

        private void RecordEmployeeWellbeingTime(Employee employee, long from, long to, int workMinutes)
        {
            var state=EnsureEmployeeWellbeing(employee,from);
            long elapsed=Math.Max(0,to-Math.Max(from,state.LastTickAt));if(elapsed==0)return;
            double work=Math.Min(elapsed,Math.Max(0,workMinutes));
            double drain=.085-Math.Max(0,Math.Min(100,employee.Stamina))*.0003;
            state.Energy=ClampEmployeeValue(state.Energy-work*drain+(elapsed-work)*.10,100);state.LastTickAt=to;
        }
        private static int RoleAptitude(Employee employee, EmployeeRole role) => role == EmployeeRole.Buyer || role == EmployeeRole.Appraiser ? employee.Intelligence
            : role == EmployeeRole.Salesperson ? EmployeeCharm(employee) : role == EmployeeRole.PetKeeper ? employee.Stamina
            : role == EmployeeRole.Assembler || role == EmployeeRole.Repairer ? (employee.Intelligence+employee.Dexterity)/2
            : role == EmployeeRole.Waiter ? (EmployeeCharm(employee)+employee.Dexterity)/2 : employee.Dexterity;
        private EmployeeRole BetterEmployeeRole(EmployeeAssignment row)
        {
            if(row.Shop==null||CompanyPostOf(row.Person.Id)!=null)return row.Role;
            var choices=new[]{EmployeeRole.Cashier,EmployeeRole.Salesperson,EmployeeRole.Buyer,EmployeeRole.Assembler,EmployeeRole.Chef,EmployeeRole.Waiter,EmployeeRole.Appraiser,EmployeeRole.PetKeeper,EmployeeRole.Repairer,EmployeeRole.ModelPainter};
            var best=choices.Where(role=>EmployeeRoleShopBlocker(row.Shop,role)==null).OrderByDescending(role=>RoleAptitude(row.Person,role)).First();
            return RoleAptitude(row.Person,row.Role)<40 && RoleAptitude(row.Person,best)>=RoleAptitude(row.Person,row.Role)+25 ? best : row.Role;
        }
        private bool EmployeeOverloaded(EmployeeAssignment row)
        {
            if(row.Shop==null)return false;
            if(row.Role==EmployeeRole.Cashier)return row.Shop.Sim.Queue.Count>=Math.Max(3,ShopStaff(row.Shop,EmployeeRole.Cashier).Count()*2);
            if(row.Role==EmployeeRole.PetKeeper)return PetGuests(row.Shop).Count>Math.Max(1,ShopStaff(row.Shop,EmployeeRole.PetKeeper).Count())*PetsPerKeeper;
            return false;
        }

        private void ObserveEmployeeWork(EmployeeAssignment row, EmployeePayrollDay pay, int minutes)
        {
            if(minutes<=0)return;
            var state=row.Person.Wellbeing;string position=EmployeePositionKey(row);
            if(!pay.WellbeingTracked)
            {
                pay.WellbeingTracked=true;pay.WellbeingRestMinutes=state.LastShiftEnd<0?600:(int)Math.Max(0,pay.Start-state.LastShiftEnd);
                pay.WellbeingExpectedCents=ExpectedEmployeeWage(row)*100;
                pay.WellbeingPosition=position;pay.WellbeingRole=row.Role;
            }
            state.ExpectedWage=ExpectedEmployeeWage(row);
            if(state.Position!=null&&state.Position!=position)pay.WellbeingChanges++;
            state.Position=position;pay.WellbeingPosition=position;pay.WellbeingRole=row.Role;
            pay.WellbeingMinutes+=minutes;
            if(EmployeeOverloaded(row))pay.WellbeingOverloadedMinutes+=minutes;
            if(BetterEmployeeRole(row)!=row.Role)pay.WellbeingMismatchMinutes+=minutes;
        }

        private bool RequestActuallyFulfilled(EmployeeWellbeing state, EmployeePayrollDay pay)
        {
            var request=state.Request;if(request?.Outstanding!=true||pay.WellbeingMinutes<=0)return false;
            if(request.Kind==EmployeeRequestKind.Pay)return pay.DailyCents>=request.TargetWage*100;
            if(request.Kind==EmployeeRequestKind.ShorterShift)return pay.End-pay.Start<=600 && pay.WellbeingMinutes<=600;
            if(request.Kind==EmployeeRequestKind.Transfer)return pay.WellbeingPosition!=request.OriginalPosition && pay.WellbeingRole==request.PreferredRole;
            return false;
        }
        private void RecordEmployeeChange(EmployeeWellbeing state, long at, int delta, IEnumerable<string> reasons)
        {
            state.Satisfaction=Math.Max(0,Math.Min(100,state.Satisfaction+delta));
            state.Changes=new[]{new EmployeeWellbeingChange{At=at,Delta=delta,Reasons=reasons.ToArray()}}.Concat(state.Changes).Take(6).ToArray();
        }
        private void FulfillEmployeeRequest(Employee employee, List<string> reasons)
        {
            var state=employee.Wellbeing;state.Request.Response=EmployeeRequestResponse.Fulfilled;
            state.NoticeAfterShift=-1;state.LowShifts=0;reasons.Add("已兑现："+EmployeeRequestName(state.Request.Kind));
            Say(employee.Name+"确认工作安排已改善，原请求已兑现。");
        }

        private void SettleEmployeeWellbeing(long at)
        {
            var due=_employeePayroll.Where(p=>p.WellbeingTracked&&!p.WellbeingSettled&&p.End<=at).ToLookup(p=>p.EmployeeId);
            foreach(var row in Employees.ToArray())
            {
                var employee=row.Person;var state=EnsureEmployeeWellbeing(employee,at);
                // 在班次结束这一刻兑现调薪；工资记录仍使用开班时的快照。
                PrepareEmployeeWellbeingPay(employee,at);
                foreach(var pay in due[employee.Id].OrderBy(p=>p.End))
                {
                    pay.WellbeingSettled=true;
                    if(pay.WellbeingMinutes<=0||pay.Workday<=state.LastSettledDay)continue;
                    state.LastSettledDay=pay.Workday;state.CompletedShifts++;
                    state.LongShiftStreak=pay.WellbeingMinutes>600?state.LongShiftStreak+1:0;
                    state.MismatchShifts=pay.WellbeingMismatchMinutes*2>=pay.WellbeingMinutes?state.MismatchShifts+1:0;
                    var reasons=new List<string>();double target=75;
                    double ratio=pay.DailyCents/(double)Math.Max(100,pay.WellbeingExpectedCents);
                    if(ratio<1){target-=Math.Min(30,(1-ratio)*70);reasons.Add("日薪低于岗位期望");}
                    else if(ratio>=1.15){target+=5;reasons.Add("薪酬高于岗位期望");}
                    if(state.LongShiftStreak>0){target-=Math.Ceiling((pay.WellbeingMinutes-600)/60.0)*4+Math.Min(15,state.LongShiftStreak*3);reasons.Add("连续长班 "+state.LongShiftStreak+" 班");}
                    else {if(pay.WellbeingMinutes<=480)target+=5;reasons.Add("班次合理");}
                    if(pay.WellbeingRestMinutes<600){target-=Math.Min(18,(600-pay.WellbeingRestMinutes)/30.0);reasons.Add("两班间休息不足");}
                    if(state.Energy<30){target-=10;reasons.Add("精力不足");}
                    if(pay.WellbeingOverloadedMinutes*3>=pay.WellbeingMinutes){target-=8;reasons.Add("排队或照护负荷偏高");}
                    if(state.MismatchShifts>=3){target-=8;reasons.Add("希望调整到更擅长的岗位");}
                    if(pay.WellbeingChanges>=2){target-=4;reasons.Add("班内反复调岗");}
                    bool fulfilled=RequestActuallyFulfilled(state,pay);
                    if(fulfilled){FulfillEmployeeRequest(employee,reasons);target=Math.Max(target,state.Satisfaction+3);}
                    else if(state.Request?.Outstanding==true)
                    {
                        if(state.CompletedShifts>state.Request.DueAfterShift){target-=Math.Min(25,(state.CompletedShifts-state.Request.DueAfterShift)*5);reasons.Add("请求逾期未改善");}
                        if(state.Request.Response==EmployeeRequestResponse.Declined){target-=5;reasons.Add("请求被拒绝");}
                    }
                    int delta=Math.Sign(target-state.Satisfaction)*(int)Math.Min(EmployeeWellbeingMaximumShiftChange,Math.Ceiling(Math.Abs(target-state.Satisfaction)*.2));
                    RecordEmployeeChange(state,pay.End,delta,reasons);state.LastShiftEnd=pay.End;
                    MaybeCreateEmployeeRequest(row,pay,reasons);
                    state.LowShifts=state.Satisfaction<30?state.LowShifts+1:0;
                    if(state.NoticeAfterShift<0&&state.LowShifts>=3&&state.Request?.Outstanding==true&&state.CompletedShifts>state.Request.DueAfterShift)
                    {state.NoticeAfterShift=state.CompletedShifts+EmployeeNoticeShifts;Say(employee.Name+"提出离职预告：还有三个实际班次可改善安排并准备交接。");}
                }
                if(state.LeaveUntil>=0&&at>=state.LeaveUntil&&!state.LeaveCredited)
                {
                    state.LeaveCredited=true;var reasons=new List<string>{"带薪休假已完成"};
                    if(state.Request?.Outstanding==true&&state.Request.Kind==EmployeeRequestKind.Leave&&state.Request.LeaveUntil==state.LeaveUntil)FulfillEmployeeRequest(employee,reasons);
                    RecordEmployeeChange(state,state.LeaveUntil,3,reasons);
                }
                if(state.NoticeAfterShift>=0&&state.Satisfaction>=40)
                {state.NoticeAfterShift=-1;Say(employee.Name+"撤回了离职预告。");}
                if(state.NoticeAfterShift>=0&&state.CompletedShifts>=state.NoticeAfterShift&&!EmployeeHandoverPending(employee.Id)&&!BrandExpoPersonReserved(PersonIdOf(employee)))
                {if(DismissEmployee(employee.Id))Say(employee.Name+"按预告结束雇佣，工资已结清，原岗位需要补人。");}
            }
        }

        private void MaybeCreateEmployeeRequest(EmployeeAssignment row, EmployeePayrollDay pay, List<string> reasons)
        {
            var state=row.Person.Wellbeing;
            if(state.Satisfaction>=60||state.CompletedShifts<2||state.Request?.Outstanding==true
                ||state.Request!=null&&state.CompletedShifts-state.Request.CreatedAfterShift<5)return;
            EmployeeRequestKind kind;EmployeeRole preferred=BetterEmployeeRole(row);
            if(pay.DailyCents<pay.WellbeingExpectedCents)kind=EmployeeRequestKind.Pay;
            else if(state.Energy<35||pay.WellbeingRestMinutes<480)kind=EmployeeRequestKind.Leave;
            else if(state.LongShiftStreak>=2)kind=EmployeeRequestKind.ShorterShift;
            else if(state.MismatchShifts>=3&&preferred!=row.Role)kind=EmployeeRequestKind.Transfer;
            else if(pay.WellbeingOverloadedMinutes*3>=pay.WellbeingMinutes)kind=EmployeeRequestKind.Leave;
            else return;
            state.Request=new EmployeeWorkRequest {Kind=kind,Reason=string.Join(" · ",reasons.Take(2)),OriginalPosition=state.Position,
                PreferredRole=preferred,TargetWage=state.ExpectedWage,CreatedAfterShift=state.CompletedShifts,DueAfterShift=state.CompletedShifts+EmployeeRequestGraceShifts};
            Say(row.Person.Name+"提出员工请求："+EmployeeRequestName(kind)+"。请到员工档案处理。");
        }

        private void SettleEmployeeExitPay(Employee employee)
        {
            if(employee.TryShift(Clock.Now.TotalMinutes,out long start,out long end))PayrollDay(employee,start,end);
            foreach(var pay in _employeePayroll.Where(p=>p.EmployeeId==employee.Id&&p.Start<=Clock.Now.TotalMinutes))
            {PayPayrollBase(pay);PayPayrollOvertime(pay);}
        }
        private void LoadEmployeeWellbeing()
        {
            foreach(var row in Employees)
            {
                var state=EnsureEmployeeWellbeing(row.Person,Clock.Now.TotalMinutes);
                state.JoinedAt=state.JoinedAt<0?Clock.Now.TotalMinutes:Math.Min(state.JoinedAt,Clock.Now.TotalMinutes);
                // 只恢复状态；不补扣旧存档过去的长班，也不在读档时立即离职。
                state.LastTickAt=Clock.Now.TotalMinutes;
            }
        }
    }
}
