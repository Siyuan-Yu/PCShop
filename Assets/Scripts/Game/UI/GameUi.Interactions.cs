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

        private RectTransform ClockSelector(Transform parent,string label,int minute,Action<int> choose,int earliestMinute=0,bool showLabel=true)
        {
            minute=Math.Max(QuarterHourClock(minute),earliestMinute);
            var hours=Enumerable.Range(0,24).Where(h=>h*60+45>=earliestMinute).ToArray();
            var minuteOptions=new[]{0,15,30,45}.Where(m=>minute/60*60+m>=earliestMinute).ToArray();
            var row=Ui.Row(parent,44,6);
            if (showLabel) Ui.Size(Ui.Label(row,label,18),65);
            var hour=Ui.Choice(row,hours.Select(n=>n.ToString("00")),Array.IndexOf(hours,minute/60),h=>choose(Math.Max(earliestMinute,hours[h]*60+minute%60)));
            hour.name="ClockHour_"+label;Ui.Size(hour,85,42);
            Ui.Size(Ui.Label(row,":",20,TextAnchor.MiddleCenter),12);
            var minutes=Ui.Choice(row,minuteOptions.Select(n=>n.ToString("00")),Array.IndexOf(minuteOptions,minute%60),m=>choose(minute/60*60+minuteOptions[m]));
            minutes.name="ClockMinute_"+label;Ui.Size(minutes,85,42);
            return row;
        }
        private void OpenEmployeeShift(string id, Window parent)
        {
            var person=S.FindEmployee(id)?.Person;if(person==null)return;
            int start=person.Schedule?.PendingAt>=0?person.Schedule.PendingStart:person.ShiftStart;
            int end=person.Schedule?.PendingAt>=0?person.Schedule.PendingEnd:person.ShiftEnd;
            start=QuarterHourClock(start); end=QuarterHourClock(end);
            OpenWindow("排班 · "+person.Name,660,450,w=>
            {
                PersonSummary(w.Content, S.PersonIdOf(person), person.Name, "日薪 " + person.DailyWage);
                ClockSelector(w.Content,"上班",start,value=>{start=value;w.RequestRebuild();});
                ClockSelector(w.Content,"下班",end,value=>{end=value;w.RequestRebuild();});
                bool valid=start!=end;
                if(valid)
                {
                    int duration=GameSession.ShiftMinutes(start,end);
                    Line(w.Content,Ui.Hm(start)+"—"+(end<start?"次日":"")+Ui.Hm(end)+$" · 共{duration/60.0:0.##}小时",21,Ui.Accent);
                    Para(w.Content,$"预计工资 {GameSession.EstimateShiftPay(person,start,end)}（基础{person.DailyWage}＋加班{GameSession.EstimateShiftPay(person,start,end)-person.DailyWage}）",18);
                }
                else Ui.Notice(w.Content,"上下班时间不能相同");
                if(person.OnDuty(S.Clock.Now))Para(w.Content,"新时间从下一班生效，已发生工时和工资保留。",16,Ui.Dim);
                Ui.Size(Ui.Button(w.Content,"保存排班",()=>{if(S.SetEmployeeShift(id,start,end)){w.Close();parent?.Rebuild();}},18,Ui.ButtonHot,valid),-1,44);
            });
        }

        private static readonly EmployeeRole[] EmployeeRoleChoices = Enum.GetValues(typeof(EmployeeRole))
            .Cast<EmployeeRole>().OrderBy(role => role == EmployeeRole.Unassigned).ToArray();
        private static readonly Color UnassignedRoleColor = new Color32(63, 77, 97, 255);
        private static readonly Color UnassignedRoleSelectedColor = new Color32(80, 105, 143, 255);

        private static void BuildEmployeeRoleChoices(Transform parent, EmployeeRole selected, Action<EmployeeRole> select, bool venue = false)
        {
            var roles = Ui.Row(parent, 44);
            foreach (var role in EmployeeRoleChoices)
            {
                bool unassigned = role == EmployeeRole.Unassigned;
                bool active = role == selected;
                Color background = unassigned
                    ? (active ? UnassignedRoleSelectedColor : UnassignedRoleColor)
                    : (active ? Ui.ButtonHot : Ui.ButtonBg);
                bool cashier = role == EmployeeRole.Cashier;
                string label = venue && cashier ? "场馆值守" : GameSession.RoleName(role);
                Ui.Size(Ui.Button(roles, label, () => select(role), 18, background, !venue || cashier || unassigned), -1, -1, 1);
            }
        }

        private void OpenRecruitWork(Employee candidate)
        {
            EmployeeRole role = EmployeeRole.Cashier;
            string shopId = S.CurrentShop.Place.Id;
            OpenWindow("聘用与工作安排 · " + candidate.Name, 900, 650, w =>
            {
                PersonSummary(w.Content, S.PersonIdOf(candidate), candidate.Name, "日薪 " + candidate.DailyWage);
                Line(w.Content, S.CandidateWaitText(candidate), 17, Ui.Warn);
                BuildEmployeeRoleChoices(w.Content, role, choice => { role = choice; w.Rebuild(); });
                BuildRecruitRolePreview(w.Content, candidate, role, S.ShopAt(shopId));
                if (role != EmployeeRole.Unassigned)
                {
                    var shops = Ui.Scroll(w.Content, 220);
                    foreach (var shop in S.MyShops)
                    {
                        string target = shop.Place.Id;
                        Ui.Size(Ui.Button(shops, S.ShopTitle(shop) + " · " + S.City.StreetName(shop.Place.StreetId), () => { shopId = target; w.Rebuild(); }, 18,
                            shopId == target ? Ui.ButtonHot : Ui.ButtonBg), -1, 44);
                    }
                }
                string why = S.RecruitAssignmentBlocker(candidate.Id, shopId, role);
                if (why != null) Ui.Notice(w.Content, why);
                Ui.Size(Ui.Button(w.Content, "确认聘用并安排工作", () =>
                {
                    if (S.RecruitAndAssign(candidate.Id, shopId, role)) { w.Close(); RebuildDock(); }
                }, 19, Ui.ButtonHot, why == null), -1, 48);
            });
        }

        private void BuildRecruitRolePreview(Transform parent, Employee candidate, EmployeeRole role, OwnedShop shop)
        {
            var (duties, metric, detail) = EmployeeRolePreview(candidate, role, shop);
            var panel = Ui.Panel(parent, "RecruitRolePreview", Ui.PanelBg2).rectTransform;
            Ui.VList(panel, 3, 10);
            Line(panel, metric, 20, Ui.Accent);
            Para(panel, $"{duties}。{detail}", 16, Ui.Dim);
        }

        private string EmployeeRoleLabel(EmployeeRole role, OwnedShop shop) =>
            role == EmployeeRole.Cashier && S.IsDedicatedVenue(shop?.Place) ? "场馆值守员" : GameSession.RoleName(role);

        private (string duties, string metric, string detail) EmployeeRolePreview(Employee employee, EmployeeRole role, OwnedShop shop) =>
            role switch
            {
                EmployeeRole.Cashier when S.IsDedicatedVenue(shop?.Place) =>
                    ("接待到场顾客", "场馆值守 · 按班次到岗", "按时到岗即可维持营业，目前无个人能力修正。"),
                EmployeeRole.Cashier =>
                    ("给顾客结账", $"零售收银 · {GameSession.EmployeeCheckoutCycleMinutes(employee)} 分钟 / 单",
                        WorkDurationDetail("动手能力", employee.Dexterity, GameSession.EmployeeCheckoutCycleMinutes(employee), Math.Max(1, employee.CheckoutMinutes) + 1)),
                EmployeeRole.Assembler =>
                    ("组装订单电脑", $"装机 · {GameSession.AssemblyMinutesOf(employee)} 分钟 / 台",
                        WorkDurationDetail("动手能力", employee.Dexterity, GameSession.AssemblyMinutesOf(employee), Math.Max(45, employee.AssemblyMinutes - employee.BuildsCompleted / 2))
                        + $" 已装机 {employee.BuildsCompleted} 台，经验会缩短用时。"),
                EmployeeRole.Buyer =>
                    ("按计划外出进货", $"进货处理 · {GameSession.EmployeePurchaseMinutes(employee)} 分钟 / 型号",
                        WorkDurationDetail("智力", employee.Intelligence, GameSession.EmployeePurchaseMinutes(employee), Math.Max(1, employee.PurchaseMinutes)) + " 往返用时另计。"),
                EmployeeRole.Salesperson =>
                    ("向顾客推介商品", $"购买意愿 +{GameSession.EmployeeSalesBonus(employee) * 100:0.#} 个百分点", $"魅力 {GameSession.EmployeeCharm(employee)} · 在店上班时生效。"),
                EmployeeRole.Chef =>
                    ("备料、下锅、出锅和清锅", $"厨师 · 每步操作 {GameSession.RestaurantEmployeeOperationMinutes(employee, role)} 分钟",
                        WorkDurationDetail("动手能力", employee.Dexterity, GameSession.RestaurantEmployeeOperationMinutes(employee, role), GameSession.RestaurantOperationMinutes) + " 走动和等菜另计。"),
                EmployeeRole.Waiter =>
                    ("上菜和收桌", $"服务员 · 每步操作 {GameSession.RestaurantEmployeeOperationMinutes(employee, role)} 分钟",
                        WorkDurationDetail("体能", employee.Stamina, GameSession.RestaurantEmployeeOperationMinutes(employee, role), GameSession.RestaurantOperationMinutes) + " 走动用时另计。"),
                EmployeeRole.Appraiser =>
                    ("判断成色并收购旧物", $"识别真实成色 {GameSession.AppraiserAccuracy(employee.Intelligence) * 100:0.#}%", $"智力 {employee.Intelligence} · 未能识别时按卖家说法估价。"),
                EmployeeRole.PetKeeper =>
                    ("给宠物喂食、换水、清理和陪玩", $"照顾 {GameSession.PetsPerKeeper} 只宠物 · 每只状态每天 +{GameSession.PetKeeperCareBonus(employee)}", $"魅力 {GameSession.EmployeeCharm(employee)} · 到岗后开始照料，补粮和换猫砂需要库存。"),
                _ => ("暂未分配工作", "待安排", "待安排期间照常发薪。")
            };

        private static string WorkDurationDetail(string attribute, int score, int minutes, int baseline)
        {
            string effect = minutes == baseline ? "用时无变化" : $"用时 {minutes - baseline:+0;-0} 分钟（常规 {baseline} 分钟）";
            return $"{attribute} {score} · {effect}。";
        }



    }
}
