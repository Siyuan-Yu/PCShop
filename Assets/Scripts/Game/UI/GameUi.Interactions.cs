using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

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
        private static Button BuildEmployeeRoleChoices(Transform parent, EmployeeRole? selected, Action<EmployeeRole> select,
            bool venue = false, bool recruiting = false, IReadOnlyCollection<EmployeeRole> guidedRoles = null,
            List<Button> guidedButtons = null)
        {
            bool guided = guidedButtons != null;
            int columns = guided ? 3 : 6;
            var choices = guided ? EmployeeRoleChoices.Where(guidedRoles.Contains).ToArray() : EmployeeRoleChoices;
            Button first = null, selectedButton = null;
            for (int start = 0; start < choices.Length; start += columns)
            {
                var row = Ui.Row(parent, 52, 8);
                int count = Math.Min(columns, choices.Length - start);
                for (int i = 0; i < count; i++)
                {
                    var role = choices[start + i];
                    bool active = role == selected;
                    string label = venue && role == EmployeeRole.Cashier ? "场馆值守" : GameSession.RoleName(role);
                    if (active && recruiting) label = "✓ " + label;
                    var button = Ui.Button(row, label, () => select(role), 20, Ui.SelectedButton(active));
                    Ui.Size(button, 0, -1, 1);
                    if (!guided) continue;
                    guidedButtons.Add(button);
                    first ??= button;
                    if (active) selectedButton = button;
                }
                for (int i = count; i < columns; i++) Ui.Size(Ui.Rect(row, "EmptyRoleColumn"), 0, -1, 1);
            }
            return selectedButton ?? first;
        }

        private Window _openingRecruitWindow;
        private Button _openingRecruitTarget;
        private RectTransform _openingRecruitRoleArea;
        private readonly List<Button> _openingRecruitChoices = new List<Button>();
        private string _openingRecruitInstruction;

        private void OpenRecruitWork(Employee candidate, bool guided = false)
        {
            var hiringShop = S.CurrentShop;
            string shopId = hiringShop.Place.Id;
            EmployeeRole? selected = null;
            int destinationRows = guided ? 1 : Mathf.Clamp((S.MyShops.Count + 1) / 2, 1, 4);
            float height = guided ? 510 : 630 + (destinationRows - 1) * 52;
            RectTransform footer = null;
            OpenWindow("聘用与工作安排 · " + candidate.Name, 1040, height, w =>
            {
                var work = w.Content;
                BuildEmployeeWorkHeader(work, candidate);
                Line(work, S.CandidateWaitText(candidate), 17, Ui.Warn);
                var shop = S.ShopAt(shopId);
                var guidedRoles = guided ? S.MissingOpeningStaffRoles(hiringShop)
                    .Where(role => S.RecruitAssignmentBlocker(candidate.Id, shopId, role) == null).ToArray() : null;
                Line(work, "选择岗位", 20);
                var roles = Ui.Rect(work, "RecruitRoleChoices");
                Ui.VList(roles, 8, 0);
                if (guided) _openingRecruitChoices.Clear();
                var roleButton = BuildEmployeeRoleChoices(roles, selected, choice =>
                {
                    if (choice == selected) return;
                    selected = choice;
                    w.Rebuild();
                }, venue: S.IsDedicatedVenue(shop?.Place), recruiting: true, guidedRoles: guidedRoles,
                    guidedButtons: guided ? _openingRecruitChoices : null);
                string why = null;
                if (selected is EmployeeRole role)
                {
                    BuildEmployeeRolePreview(work, candidate, role, shop);
                    if (!guided)
                        BuildEmployeeDestinationChoices(work, role, shopId, target => { shopId = target; w.Rebuild(); });
                    why = S.RecruitAssignmentBlocker(candidate.Id, shopId, role);
                }

                var actions = EmployeeWorkFooter(w, ref footer, "RecruitFooter", why, Ui.Warn);
                var back = guided ? Ui.Size(Ui.Button(actions, "返回选人", w.Close, 17), 140, 48) : null;
                var confirm = Ui.Size(Ui.Button(actions, "确认聘用并安排工作", () =>
                {
                    if (!selected.HasValue || !S.RecruitAndAssign(candidate.Id, shopId, selected.Value)) return;
                    w.Close();
                    if (guided) S.RefreshQuestProgress();
                    RebuildDock();
                }, 19, selected.HasValue ? Ui.ButtonHot : Ui.ButtonBg, why == null && selected.HasValue), -1, 48, 1);
                if (guided)
                {
                    if (confirm.IsInteractable()) _openingRecruitChoices.Add(confirm);
                    _openingRecruitChoices.Add(back);
                    for (int i = 0; i < _openingRecruitChoices.Count; i++)
                    {
                        var previous = i > 0 ? _openingRecruitChoices[i - 1] : null;
                        var next = i + 1 < _openingRecruitChoices.Count ? _openingRecruitChoices[i + 1] : null;
                        _openingRecruitChoices[i].navigation = new Navigation
                        {
                            mode = Navigation.Mode.Explicit,
                            selectOnUp = previous, selectOnLeft = previous,
                            selectOnDown = next, selectOnRight = next,
                        };
                    }
                    _openingRecruitWindow = w;
                    _openingRecruitTarget = roleButton ?? back;
                    _openingRecruitRoleArea = roleButton != null ? roles : null;
                    _openingRecruitInstruction = roleButton == null ? "这位应聘者暂无可安排岗位\n点「返回选人」"
                        : selected.HasValue ? "可在高亮岗位之间切换\n选好后点「确认聘用并安排工作」"
                        : "点一个高亮岗位\n查看这位应聘者的工作效果";
                }
            }, closable: !guided, onClosed: guided ? () =>
            {
                _openingRecruitWindow = null;
                _openingRecruitTarget = null;
                _openingRecruitRoleArea = null;
                _openingRecruitChoices.Clear();
                _openingRecruitInstruction = null;
            } : null);
        }

        private static RectTransform EmployeeWorkFooter(Window w, ref RectTransform footer, string name, string status, Color color)
        {
            const int footerTop = 112;
            if (footer == null)
            {
                var scrollFrame = (RectTransform)w.Content.parent.parent;
                scrollFrame.offsetMin = new Vector2(0, footerTop);
                footer = Ui.Rect(w.Fitter.transform, name).Anchor(0, 0, 1, 0, 16, 16, 30, -footerTop);
                Ui.VList(footer, 6, 0);
            }
            else Ui.Clear(footer);
            Ui.Trim(footer, "WorkFooterRule", Ui.FlatCardEdge, 0, top: true);
            var text = Ui.Size(Ui.Label(footer, status ?? "", 16, color: color), -1, 40);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (status != null) text.Tip(status);
            return Ui.Row(footer, 48, 8);
        }

        private void BuildEmployeeWorkHeader(Transform parent, Employee employee)
        {
            var header = PersonSummary(parent, S.PersonIdOf(employee), employee.Name, "日薪 " + employee.DailyWage);
            Ui.Size(BuildWorkAttributeRow(header,
                (employee.Intelligence, GameSession.EmployeeCharm(employee), employee.Dexterity, employee.Stamina)), 600, 48);
        }

        private void BuildEmployeeDestinationChoices(Transform parent, EmployeeRole role, string selected, Action<string> select,
            bool scrollLongList = true)
        {
            if (role == EmployeeRole.Unassigned) return;
            var shops = S.MyShops.Select(shop => (shop, reason: S.EmployeeRoleShopBlocker(shop, role)))
                .OrderBy(row => row.reason != null).ToArray();
            Line(parent, "工作门店", 18).Tip(GameSession.EmployeeRoleShopHint(role));
            if (shops.Length == 0)
                Line(parent, "目前没有可安排的门店。", 17, Ui.Warn);
            Transform list = scrollLongList && shops.Length > 8 ? Ui.Scroll(parent, 204) : parent;
            for (int start = 0; start < shops.Length; start += 2)
            {
                var row = Ui.Row(list, 44, 8);
                for (int index = start; index < Math.Min(start + 2, shops.Length); index++)
                {
                    var (shop, reason) = shops[index];
                    string target = shop.Place.Id;
                    string label = S.ShopTitle(shop) + " · " + S.City.StreetName(shop.Place.StreetId);
                    if (reason != null) label += "\n<size=14>" + reason + "</size>";
                    Ui.Size(Ui.Button(row, label, () => select(target), 16,
                        Ui.SelectedButton(target == selected && reason == null), reason == null), 0, 44, 1);
                }
                if (start + 1 == shops.Length && start > 0)
                    Ui.Size(Ui.Rect(row, "EmptySpace"), 0, -1, 1);
            }
        }

        private void BuildEmployeeRolePreview(Transform parent, Employee employee, EmployeeRole role, OwnedShop shop)
        {
            var (_, effect, attributes) = EmployeeRolePreview(employee, role, shop);
            var panel = Ui.Panel(parent, "EmployeeRolePreview", Ui.PanelBg2).rectTransform;
            Ui.VList(panel, 3, 10);
            Para(panel, effect, 18, Ui.TextColor).fontStyle = FontStyle.Bold;
            if (attributes != null) Para(panel, "相关能力 · " + attributes, 15, Ui.Dim);
        }

        private string EmployeeRoleLabel(EmployeeRole role, OwnedShop shop) =>
            role == EmployeeRole.Cashier && S.IsDedicatedVenue(shop?.Place) ? "场馆值守员" : GameSession.RoleName(role);

        private (string duties, string effect, string attributes) EmployeeRolePreview(Employee employee, EmployeeRole role, OwnedShop shop) =>
            role switch
            {
                EmployeeRole.Cashier when S.IsDedicatedVenue(shop?.Place) =>
                    ("接待到场顾客", "到岗值守，维持场馆营业", "出勤"),
                EmployeeRole.Cashier =>
                    ("给顾客结账", $"收银 {GameSession.EmployeeCheckoutCycleMinutes(employee)} 分钟 / 单", "动手能力"),
                EmployeeRole.Assembler =>
                    ("组装订单电脑", $"装机 {GameSession.AssemblyMinutesOf(employee)} 分钟 / 台 · 普通订单整机售价 +{GameSession.AssemblyPriceBonus(employee):0.#%}",
                        $"动手能力、智力、装机经验（已装 {employee.BuildsCompleted} 台）"),
                EmployeeRole.Buyer =>
                    ("按计划外出进货", $"进货价优惠 {GameSession.EmployeePurchaseDiscount(employee):0.#%}",
                        "智力影响进货价"),
                EmployeeRole.Salesperson =>
                    ("向顾客推介商品", $"在店上班时，购买意愿个人加成 +{GameSession.EmployeeSalesBonus(employee) * 100:0.##} 个百分点",
                        "魅力"),
                EmployeeRole.Chef =>
                    ("备料、下锅、出锅和清锅", $"每步厨房操作 {GameSession.RestaurantEmployeeOperationMinutes(employee, role)} 分钟", "动手能力"),
                EmployeeRole.Waiter =>
                    ("上菜、桌边收款和收桌", $"每步服务 {GameSession.RestaurantEmployeeOperationMinutes(employee, role)} 分钟 · 小费概率 {GameSession.WaiterTipChance(GameSession.EmployeeCharm(employee)):0.#%}（最高为菜价的 {GameSession.WaiterTipUpperRate(GameSession.EmployeeCharm(employee)):0.#%}）",
                        "动手能力影响服务速度，魅力影响小费"),
                EmployeeRole.Appraiser =>
                    ("判断成色并收购旧物", $"识别真实成色概率 {GameSession.AppraiserAccuracy(employee.Intelligence):0.#%}", "智力"),
                EmployeeRole.ModelPainter =>
                    ("在喷漆台处理客带模型", $"加工速度 ×{GameSession.ModelPainterSpeed(employee):0.##} · 品质 {GameSession.ModelPainterQuality(employee):0.#}/10",
                        "动手能力"),
                EmployeeRole.Repairer =>
                    ("在二手修复台修复旧物", $"修复速度 ×{GameSession.UsedRepairSpeed(employee):0.##} · 成功率 {GameSession.UsedRepairSuccessChance(employee):0.#%}",
                        "动手能力、智力"),
                EmployeeRole.PetKeeper =>
                    ("给宠物喂食、换水、清理和陪玩", $"最多照顾 {GameSession.PetsPerKeeper} 只宠物 · 每只状态每天 +{GameSession.PetKeeperCareBonus(employee)}",
                        "魅力、动手能力"),
                _ => ("暂未分配工作", "待安排期间照常发薪", null)
            };



    }
}
