using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _employeeFocus, _employeeToolId, _employeeTool;
        private bool _employeeAttentionOnly, _companyAttentionOnly;
        private string _employeeWageDraft, _employeeShiftStartDraft, _employeeShiftEndDraft, _employeeTodoStamp;
        private readonly List<EmployeeWellbeingWidgets> _employeeWellbeingWidgets = new List<EmployeeWellbeingWidgets>();

        private sealed class EmployeeWellbeingWidgets
        {
            public Employee Employee;
            public Window Window;
            public Text Caption, EnergyCaption;
            public Image Fill, EnergyFill;
            public string Stamp;
            public bool Compact;
        }
        private static Color EmployeeMoraleColor(double value) => value >= 60 ? Ui.Good : value >= 40 ? Ui.Accent : Ui.Warn;
        private string EmployeeWellbeingStamp(Employee employee)
        {
            var s=employee.Wellbeing;return s==null?"":$"{s.Changes.FirstOrDefault()?.At}:{s.Request?.Kind}:{s.Request?.Response}:{s.CompletedShifts}:{s.NoticeAfterShift}:{s.LeaveFrom}:{s.LeaveUntil}:{s.OnLeave(S.Clock.Now.TotalMinutes)}:{s.LeaveCredited}:{employee.DailyWageYuan}:{s.PendingWage}:{s.EmploymentEndedAt}";
        }
        private static Image EmployeeGauge(Transform parent, string name, Color color)
        {
            var track=Ui.Panel(parent,name,Ui.PanelBg).rectTransform;Ui.Size(track,-1,6);
            return Ui.Panel(track,"Fill",color).rectTransform.Anchor(0,0,0,1).GetComponent<Image>();
        }
        private void BuildEmployeeMorale(Transform parent, Employee employee, Window window, bool energy = false, bool compact = false)
        {
            var s=S.EmployeeWellbeingOf(employee.Id);if(s==null)return;
            var caption=Ui.Size(Ui.Label(parent,"",15,color:EmployeeMoraleColor(s.Satisfaction)),-1,23);
            var fill=EmployeeGauge(parent,"EmployeeSatisfaction_"+employee.Id,EmployeeMoraleColor(s.Satisfaction));
            Text energyText=null;Image energyFill=null;
            if(energy)
            {
                energyText=Ui.Size(Ui.Label(parent,"",14,color:Ui.Dim),-1,22);
                energyFill=EmployeeGauge(parent,"EmployeeEnergy_"+employee.Id,Ui.Good);
            }
            var view=new EmployeeWellbeingWidgets{Employee=employee,Window=window,Caption=caption,Fill=fill,EnergyCaption=energyText,EnergyFill=energyFill,Compact=compact,Stamp=EmployeeWellbeingStamp(employee)};
            _employeeWellbeingWidgets.Add(view);RefreshEmployeeMorale(view);
        }
        private void RefreshEmployeeMorale(EmployeeWellbeingWidgets view)
        {
            var state=view.Employee.Wellbeing;if(state==null)return;
            int value=(int)Math.Round(state.Satisfaction),delta=state.Changes.FirstOrDefault()?.Delta??0;
            view.Caption.text=state.EmploymentEndedAt>=0?"已结束雇佣":$"{(view.Compact?"满意":"满意度")} {value} · {GameSession.EmployeeSatisfactionLabel(value)}"+(delta==0?"":delta>0?$" ↑{delta}":$" ↓{-delta}");
            view.Caption.color=EmployeeMoraleColor(value);view.Fill.color=view.Caption.color;
            view.Fill.rectTransform.anchorMax=new Vector2((float)state.Satisfaction/100,1);
            if(view.EnergyCaption!=null)
            {
                view.EnergyCaption.text=$"精力 {state.Energy:0} · 当前工作效率 {GameSession.EmployeeWorkRate(view.Employee):P0}";
                view.EnergyFill.rectTransform.anchorMax=new Vector2((float)state.Energy/100,1);view.EnergyFill.color=state.Energy<30?Ui.Warn:Ui.Good;
            }
        }
        private void UpdateEmployeeWellbeingWidgets()
        {
            for(int i=_employeeWellbeingWidgets.Count-1;i>=0;i--)
            {
                var view=_employeeWellbeingWidgets[i];if(view.Caption==null){_employeeWellbeingWidgets.RemoveAt(i);continue;}
                RefreshEmployeeMorale(view);string stamp=EmployeeWellbeingStamp(view.Employee);
                if(stamp!=view.Stamp){view.Stamp=stamp;if(view.Window!=null&&!view.Window.Closed)view.Window.RequestRebuild();}
            }
            string todo=string.Join("|",S.Employees.Where(e=>S.EmployeeNeedsAttention(e.Person)).Select(e=>e.Person.Id+EmployeeWellbeingStamp(e.Person)));
            if(_employeeTodoStamp!=todo)
            { _employeeTodoStamp=todo;if(_homeWorkbench!=null&&!_homeWorkbench.Closed&&_workbenchTab==0)_homeWorkbench.RequestRebuild(); }
        }

        private void ShowEmployeeProfile(string id)
        {
            var company=S.CompanyOfEmployee(id);
            if(company!=null)
            {
                OpenManagementPanel(2);_workspaceCompany=company.BrandId;_companyPage=2;_companyPerson=id;_companyUnit=null;
                _companyManagers=S.CompanyPostOf(id)?.Leader==true;_companyAttentionOnly=false;_managementWindow.Rebuild();
            }
            else {_employeeFocus=id;_employeeAttentionOnly=false;OpenEmployeesPanel();}
        }
        private void SelectEmployeeTool(Employee employee, string tool, Window window)
        {
            bool same=_employeeToolId==employee.Id&&_employeeTool==tool;_employeeToolId=same?null:employee.Id;_employeeTool=same?null:tool;
            _employeeWageDraft=(employee.Wellbeing?.PendingWage>=0?employee.Wellbeing.PendingWage:employee.DailyWageYuan).ToString();
            _employeeShiftStartDraft=Ui.Hm(employee.Schedule?.PendingAt>=0?employee.Schedule.PendingStart:employee.ShiftStart);
            _employeeShiftEndDraft=Ui.Hm(employee.Schedule?.PendingAt>=0?employee.Schedule.PendingEnd:employee.ShiftEnd);
            window.RequestRebuild();
        }
        private void BuildEmployeeWellbeingDetails(Transform parent, EmployeeAssignment row, Window window)
        {
            var employee=row.Person;var state=S.EmployeeWellbeingOf(employee.Id);if(state==null)return;
            BuildEmployeeMorale(parent,employee,window,true);
            var last=state.Changes.FirstOrDefault();
            Line(parent,last==null?"完成实际班次后记录变化":"本班反馈 · "+string.Join(" · ",last.Reasons??Array.Empty<string>()),15,last?.Delta<0?Ui.Warn:Ui.Dim);
            if(state.NoticeAfterShift>=0)Line(parent,$"离职预告 · 还有 {Math.Max(0,state.NoticeAfterShift-state.CompletedShifts)} 个实际班次可改善并交接",16,Ui.Warn);
            if(state.NoticeAfterShift>=0&&state.NoticeAfterShift<=state.CompletedShifts&&S.EmployeeHandoverPending(employee.Id))Line(parent,"待人员与货物返店后交接",14,Ui.Warn);
            if(state.PendingWage>=0)Line(parent,$"新日薪 ¥{state.PendingWage:N0} · 当前班结束后生效",15,Ui.Good);
            if(state.LeaveUntil>S.Clock.Now.TotalMinutes)Line(parent,$"带薪休假 · {new GameTimestamp(state.LeaveFrom)} → {new GameTimestamp(state.LeaveUntil)}",15,Ui.Good);
            if(!S.PlayerManagesEmployee(employee.Id))
            {if(state.Request?.Outstanding==true)Line(parent,GameSession.EmployeeRequestName(state.Request.Kind)+" · 公司管理层处理中",16,Ui.Dim);return;}
            if(state.Request?.Outstanding==true)
            {
                var request=state.Request;var card=Ui.Panel(parent,"EmployeeRequest_"+employee.Id,Ui.PanelBg2).rectTransform;Ui.VList(card,4,8);
                Line(card,GameSession.EmployeeRequestName(request.Kind)+" · "+GameSession.EmployeeResponseName(request.Response),17,Ui.Accent);
                string desired=request.Kind==EmployeeRequestKind.Pay?$"期望日薪 ¥{request.TargetWage:N0}":request.Kind==EmployeeRequestKind.ShorterShift?"希望日常班次不超过 10 小时":request.Kind==EmployeeRequestKind.Transfer?"希望尝试 "+GameSession.RoleName(request.PreferredRole):"希望休一个班次日";
                Line(card,desired+" · 剩余 "+Math.Max(0,request.DueAfterShift-state.CompletedShifts)+" 个实际班次",14,Ui.Dim);
                Line(card,request.Reason,14,Ui.Dim);
                var buttons=Ui.Row(card,34,6);
                string accept=request.Kind==EmployeeRequestKind.Pay?"同意加薪":request.Kind==EmployeeRequestKind.Leave?"批准休假":request.Kind==EmployeeRequestKind.Transfer?"同意并选岗":"同意调班";
                Ui.Size(Ui.Button(buttons,accept,()=>
                {
                    if(!S.RespondEmployeeRequest(employee.Id,EmployeeRequestResponse.Promised))return;
                    if(request.Kind==EmployeeRequestKind.Transfer)
                    {var company=S.CompanyOfEmployee(employee.Id);if(company==null)OpenEmployeeWork(employee.Id,window);else OpenCompanyPostOptions(window,company,row);}
                    window.RequestRebuild();
                },15,_employeeToolId==employee.Id?Ui.ButtonSecondary:Ui.ButtonHot,request.Response!=EmployeeRequestResponse.Promised),0,34,1).name="EmployeeRequestAccept_"+employee.Id;
                Ui.Size(Ui.Button(buttons,"推迟两班",()=>{S.RespondEmployeeRequest(employee.Id,EmployeeRequestResponse.Deferred);window.RequestRebuild();},15,Ui.ButtonSecondary,!request.DeferredOnce),0,34,1);
                Ui.Size(Ui.Button(buttons,"拒绝…",()=>ConfirmAction("拒绝员工请求",employee.Name+"的工作原因仍会影响满意度，可之后重新调整安排。","确认拒绝",()=>{S.RespondEmployeeRequest(employee.Id,EmployeeRequestResponse.Declined);window.RequestRebuild();},danger:true),15,Ui.ButtonDanger,request.Response!=EmployeeRequestResponse.Declined),0,34,1);
            }
            var actions=Ui.Row(parent,34,6);
            Ui.Size(Ui.Button(actions,"调薪",()=>SelectEmployeeTool(employee,"pay",window),16,Ui.ButtonSecondary),0,34,1).name="EmployeePay_"+employee.Id;
            Ui.Size(Ui.Button(actions,"调班",()=>SelectEmployeeTool(employee,"shift",window),16,Ui.ButtonSecondary),0,34,1).name="EmployeeShift_"+employee.Id;
            Ui.Size(Ui.Button(actions,"休假",()=>SelectEmployeeTool(employee,"leave",window),16,Ui.ButtonSecondary),0,34,1).name="EmployeeLeave_"+employee.Id;
            if(_employeeToolId==employee.Id)BuildEmployeeWellbeingTool(parent,row,window);
            if(last!=null)Ui.Size(Ui.Button(actions,"变化记录",()=>SelectEmployeeTool(employee,"history",window),15,Ui.ButtonSecondary),0,34,1);
        }
        private void BuildEmployeeWellbeingTool(Transform parent, EmployeeAssignment row, Window window)
        {
            var e=row.Person;var state=S.EmployeeWellbeingOf(e.Id);
            var box=Ui.Panel(parent,"EmployeeInline_"+_employeeTool,Ui.PanelBg2).rectTransform;Ui.VList(box,5,8);
            void Saved(){_employeeToolId=_employeeTool=null;window.RequestRebuild();}
            if(_employeeTool=="pay")
            {
                Line(box,$"当前日薪 {e.DailyWage} · 岗位期望 ¥{state.ExpectedWage:N0}",15,Ui.Dim);
                var edit=Ui.Row(box,34,6);var input=Ui.Size(Ui.TextInput(edit,_employeeWageDraft,"日薪",s=>_employeeWageDraft=s),0,34,1);
                input.contentType=InputField.ContentType.IntegerNumber;
                Ui.Size(Ui.Button(edit,"保存日薪",()=>{if(!long.TryParse(input.text,out long pay)){Toast("请输入有效日薪。");return;}if(S.SetEmployeeDailyWage(e.Id,pay))Saved();},15,Ui.ButtonHot),130,34);
                Line(box,"当前班次工资保留，新日薪从之后的班次执行。",13,Ui.Dim);
            }
            else if(_employeeTool=="shift")
            {
                Line(box,"日常班次 · HH:mm",15,Ui.Dim);var edit=Ui.Row(box,34,6);
                var startInput=Ui.Size(Ui.TextInput(edit,_employeeShiftStartDraft,"上班 HH:mm",s=>_employeeShiftStartDraft=s),0,34,1);
                var endInput=Ui.Size(Ui.TextInput(edit,_employeeShiftEndDraft,"下班 HH:mm",s=>_employeeShiftEndDraft=s),0,34,1);
                Ui.Size(Ui.Button(edit,"保存班次",()=>
                {if(!TryEmployeeTime(startInput.text,out int a)||!TryEmployeeTime(endInput.text,out int b)){Toast("请输入 HH:mm。");return;}if(S.SetEmployeeShift(e.Id,a,b))Saved();},15,Ui.ButtonHot),130,34);
                Line(box,"当前班保留；跨午夜按次日计算，加班费沿用原规则。",13,Ui.Dim);
            }
            else if(_employeeTool=="leave")
            {
                var period=S.EmployeeLeavePeriod(e.Id);Line(box,"下一班起带薪休假一天",16,Ui.Accent);
                Line(box,new GameTimestamp(period.from)+" → "+new GameTimestamp(period.until),14,Ui.Dim);
                Line(box,"原岗位保留、工资照常；请安排替班。休完后恢复满意度。",14,Ui.Dim);
                Ui.Size(Ui.Button(box,"确认安排休假",()=>{if(S.ApproveEmployeeLeave(e.Id))Saved();},15,Ui.ButtonHot,S.EmployeeLeaveBlocker(e.Id)==null).Tip(S.EmployeeLeaveBlocker(e.Id)),-1,34);
            }
            else if(_employeeTool=="history")
            {
                foreach(var change in state.Changes)
                    Line(box,$"{new GameTimestamp(change.At)}  {(change.Delta>=0?"+":"")}{change.Delta} · "+string.Join(" · ",change.Reasons??Array.Empty<string>()),14,change.Delta<0?Ui.Warn:Ui.Good);
            }
        }
        private static bool TryEmployeeTime(string text, out int minute)
        {
            minute=0;var pieces=(text??"").Trim().Split(':','：');
            if(pieces.Length!=2||!int.TryParse(pieces[0],out int h)||!int.TryParse(pieces[1],out int m)||h<0||h>23||m<0||m>59)return false;
            minute=h*60+m;return true;
        }
    }
}
