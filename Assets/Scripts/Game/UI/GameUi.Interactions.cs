using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static string CategoryName(PartCategory category)
        {
            switch (category)
            {
                case PartCategory.Cpu: return "CPU";
                case PartCategory.Motherboard: return "主板";
                case PartCategory.Memory: return "内存";
                case PartCategory.Gpu: return "显卡";
                case PartCategory.Storage: return "硬盘";
                default: return "机箱电源";
            }
        }
        private void OpenPurchaseCategory(Action<string> choose)
        {
            OpenWindow("筛选品类", 420, 460, w =>
            {
                foreach (string name in new[] { "全部", "CPU", "主板", "内存", "显卡", "硬盘", "机箱电源" })
                {
                    string value = name;
                    Ui.Size(Ui.Button(w.Content, value, () => { w.Close(); choose(value); }), -1, 42);
                }
            });
        }

        private static int QuarterHourClock(int minute) => ((minute + 14) / 15 * 15) % 1440;

        private void ClockSelector(Transform parent,string label,int minute,Action<int> choose,int earliestMinute=0)
        {
            minute=Math.Max(QuarterHourClock(minute),earliestMinute);
            var hours=Enumerable.Range(0,24).Where(h=>h*60+45>=earliestMinute).ToArray();
            var minuteOptions=new[]{0,15,30,45}.Where(m=>minute/60*60+m>=earliestMinute).ToArray();
            var row=Ui.Row(parent,44,6);
            Ui.Size(Ui.Label(row,label,18),65);
            var hour=Ui.Choice(row,hours.Select(n=>n.ToString("00")),Array.IndexOf(hours,minute/60),h=>choose(Math.Max(earliestMinute,hours[h]*60+minute%60)));
            hour.name="ClockHour_"+label;Ui.Size(hour,85,42);
            Ui.Size(Ui.Label(row,":",20,TextAnchor.MiddleCenter),12);
            var minutes=Ui.Choice(row,minuteOptions.Select(n=>n.ToString("00")),Array.IndexOf(minuteOptions,minute%60),m=>choose(minute/60*60+minuteOptions[m]));
            minutes.name="ClockMinute_"+label;Ui.Size(minutes,85,42);
        }
        private void OpenEmployeeShift(string id, Window parent)
        {
            var person=S.FindEmployee(id)?.Person;if(person==null)return;
            int start=person.Schedule?.PendingAt>=0?person.Schedule.PendingStart:person.ShiftStart;
            int end=person.Schedule?.PendingAt>=0?person.Schedule.PendingEnd:person.ShiftEnd;
            start=QuarterHourClock(start); end=QuarterHourClock(end);
            OpenWindow("排班 · "+person.Name,660,450,w=>
            {
                Para(w.Content,"选择每天的上班和下班时间。下班早于上班时，按次日计算。",17,Ui.Dim);
                ClockSelector(w.Content,"上班",start,value=>{start=value;w.RequestRebuild();});
                ClockSelector(w.Content,"下班",end,value=>{end=value;w.RequestRebuild();});
                bool valid=start!=end;
                if(valid)
                {
                    int duration=GameSession.ShiftMinutes(start,end);
                    Line(w.Content,Ui.Hm(start)+"—"+(end<start?"次日":"")+Ui.Hm(end)+$" · 共{duration/60.0:0.##}小时",21,Ui.Accent);
                    Para(w.Content,$"预计工资 {GameSession.EstimateShiftPay(person,start,end)}（基础{person.DailyWage}＋加班{GameSession.EstimateShiftPay(person,start,end)-person.DailyWage}）",18);
                }
                else Line(w.Content,"上班与下班时间不能相同。",17,Ui.Warn);
                if(person.OnDuty(S.Clock.Now))Para(w.Content,"新时间从下一班生效，已发生工时和工资保留。",16,Ui.Dim);
                Ui.Size(Ui.Button(w.Content,"保存排班",()=>{if(S.SetEmployeeShift(id,start,end)){w.Close();parent?.Rebuild();}},18,Ui.ButtonHot,valid),-1,44);
            });
        }

        private void OpenRecruitWork(Employee candidate)
        {
            EmployeeRole role = EmployeeRole.Cashier;
            string shopId = S.CurrentShop.Place.Id;
            OpenWindow("聘用与工作安排 · " + candidate.Name, 900, 650, w =>
            {
                Line(w.Content, candidate.Name + " · 日薪 " + candidate.DailyWage, 22, Ui.Accent);
                Para(w.Content, GameSession.EmployeeAbilities(candidate), 17, Ui.Dim);
                if (role == EmployeeRole.PetKeeper)
                    Line(w.Content,"饲养员 · 照顾 2 只宠物 · 每日照顾状态 +"+GameSession.PetKeeperCareBonus(candidate),17,Ui.Good);
                Line(w.Content, S.CandidateWaitText(candidate), 17, Ui.Warn);
                Para(w.Content, "选好后统一确认。关闭此窗口不会聘用；选择待安排仍会按日发薪。", 17);
                var roles = Ui.Row(w.Content, 44);
                foreach (EmployeeRole option in Enum.GetValues(typeof(EmployeeRole)))
                {
                    var choice = option;
                    Ui.Size(Ui.Button(roles, GameSession.RoleName(choice), () => { role = choice; w.Rebuild(); }, 18,
                        role == choice ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
                }
                if (role != EmployeeRole.Unassigned)
                {
                    var shops = Ui.Scroll(w.Content, 220);
                    foreach (var shop in S.MyShops)
                    {
                        string target = shop.Place.Id;
                        Ui.Size(Ui.Button(shops, S.ShopTitle(shop) + "（" + shop.Place.Name + "）", () => { shopId = target; w.Rebuild(); }, 18,
                            shopId == target ? Ui.ButtonHot : Ui.ButtonBg), -1, 44);
                    }
                }
                string why = S.RecruitAssignmentBlocker(candidate.Id, shopId, role);
                if (why != null) Para(w.Content, why, 17, Ui.Warn);
                Ui.Size(Ui.Button(w.Content, "确认聘用并安排工作", () =>
                {
                    if (S.RecruitAndAssign(candidate.Id, shopId, role)) { w.Close(); RebuildDock(); }
                }, 19, Ui.ButtonHot, why == null), -1, 48);
            });
        }



    }
}
