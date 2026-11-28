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
        private const int EmployeeWorkDialogHeight = 690;

        private static void BuildEmployeeRoleChoices(Transform parent, EmployeeRole? selected, Action<EmployeeRole> select,
            bool venue = false, bool recruiting = false, Action<EmployeeRole, Button> onBuilt = null)
        {
            const int columns = 6;
            for (int start = 0; start < EmployeeRoleChoices.Length; start += columns)
            {
                var row = Ui.Row(parent, 52, 8);
                int count = Math.Min(columns, EmployeeRoleChoices.Length - start);
                for (int i = 0; i < count; i++)
                {
                    var role = EmployeeRoleChoices[start + i];
                    bool active = role == selected;
                    string label = venue && role == EmployeeRole.Cashier ? "场馆值守" : GameSession.RoleName(role);
                    if (active && recruiting) label = "✓ " + label;
                    var button = Ui.Button(row, label, () => select(role), 20, Ui.SelectedButton(active));
                    Ui.Size(button, 0, -1, 1);
                    onBuilt?.Invoke(role, button);
                }
                for (int i = count; i < columns; i++) Ui.Size(Ui.Rect(row, "EmptyRoleColumn"), 0, -1, 1);
            }
        }

        private Window _openingRecruitWindow;
        private Button _openingRecruitTarget;
        private RectTransform _openingRecruitRoleArea;
        private readonly List<Button> _openingRecruitChoices = new List<Button>();
        private readonly List<Button> _openingRecruitHighlights = new List<Button>();
        private string _openingRecruitInstruction;

        private void OpenRecruitWork(Employee candidate, bool guided = false)
        {
            var hiringShop = S.CurrentShop;
            string shopId = hiringShop.Place.Id;
            EmployeeRole? selected = null;
            int destinationRows = guided ? 1 : Mathf.Clamp((S.MyShops.Count + 1) / 2, 1, 4);
            float height = guided ? 610 : EmployeeWorkDialogHeight + (destinationRows - 1) * 52;
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
                Button selectedButton = null;
                if (guided)
                {
                    _openingRecruitChoices.Clear();
                    _openingRecruitHighlights.Clear();
                }
                BuildEmployeeRoleChoices(roles, selected, choice =>
                {
                    if (choice == selected) return;
                    selected = choice;
                    w.Rebuild();
                }, venue: S.IsDedicatedVenue(shop?.Place), recruiting: true, onBuilt: guided ? RegisterGuidedRole : null);

                void RegisterGuidedRole(EmployeeRole role, Button button)
                {
                    _openingRecruitChoices.Add(button);
                    if (role == selected) selectedButton = button;
                    if (guidedRoles.Contains(role))
                    {
                        _openingRecruitHighlights.Add(button);
                        if (role != selected) Ui.SetButtonFill(button, Ui.PendingSurface);
                        Ui.FlatBorder(button.image, Ui.SelectionEdge, 1.7f);
                    }
                    else if (role == selected) Ui.SetButtonFill(button, Ui.ButtonBg);
                }

                string why = null;
                if (selected is EmployeeRole role)
                {
                    BuildEmployeeRolePreview(work, candidate, role, shop);
                    if (!guided)
                        BuildEmployeeDestinationChoices(work, role, shopId, target => { shopId = target; w.Rebuild(); });
                    why = S.RecruitAssignmentBlocker(candidate.Id, shopId, role);
                }

                var actions = EmployeeWorkFooter(w, ref footer, "RecruitFooter", why, Ui.Warn);
                var back = guided ? Ui.Size(Ui.Button(actions, "返回选人", w.Close, 17, Ui.ButtonSecondary), 140, 48) : null;
                var confirm = Ui.Size(Ui.Button(actions, "确认聘用并安排工作", () =>
                {
                    if (!selected.HasValue || !S.RecruitAndAssign(candidate.Id, shopId, selected.Value)) return;
                    w.Close();
                    if (guided) S.RefreshQuestProgress();
                    RebuildDock();
                }, 19, Ui.ButtonHot, why == null && selected.HasValue), -1, 48, 1);
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
                    _openingRecruitTarget = selectedButton ?? _openingRecruitHighlights.FirstOrDefault() ?? back;
                    _openingRecruitRoleArea = roles;
                    string guidance = _openingRecruitHighlights.Count > 0 ? "高亮为本店待补岗位\n" : "";
                    _openingRecruitInstruction = guidance + (why != null ? "当前岗位无法安排，请另选"
                        : selected.HasValue ? "选好后确认聘用" : "点击岗位查看工作效果");
                }
            }, closable: !guided, onClosed: guided ? () =>
            {
                _openingRecruitWindow = null;
                _openingRecruitTarget = null;
                _openingRecruitRoleArea = null;
                _openingRecruitChoices.Clear();
                _openingRecruitHighlights.Clear();
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
            var (duties, effect, attributes) = EmployeeRolePreview(employee, role, shop);
            var panel = Ui.FlatPanel(parent, "EmployeeRolePreview", Ui.FlatBand).rectTransform;
            Ui.VList(panel, 4, 10);
            Line(panel, EmployeeRoleLabel(role, shop) + " · " + duties, 18, Ui.Accent).fontStyle = FontStyle.Bold;
            var columns = Ui.Row(panel, -1, 10);
            if (attributes != null) AddColumn("岗位看重", attributes, 17, Ui.TextColor);
            AddColumn("预计上岗效果", effect.Replace(" · ", "\n"), 21, role == EmployeeRole.Unassigned ? Ui.TextColor : Ui.Good);

            void AddColumn(string title, string body, int fontSize, Color ink)
            {
                var column = Ui.Size(Ui.FlatPanel(columns, title, Ui.FlatWell), 0, -1, 1).rectTransform;
                Ui.VList(column, 4, 10);
                Para(column, title, 16, Ui.Accent).fontStyle = FontStyle.Bold;
                var value = Para(column, body, fontSize, ink);
                if (fontSize >= 20) value.fontStyle = FontStyle.Bold;
            }
        }

        private string EmployeeRoleLabel(EmployeeRole role, OwnedShop shop) =>
            role == EmployeeRole.Cashier && S.IsDedicatedVenue(shop?.Place) ? "场馆值守员" : GameSession.RoleName(role);

        private (string duties, string effect, string attributes) EmployeeRolePreview(Employee employee, EmployeeRole role, OwnedShop shop)
        {
            int charm = GameSession.EmployeeCharm(employee);
            string Dexterity(string impact) => Skill("动手能力", employee.Dexterity, impact);
            string Intelligence(string impact) => Skill("智力", employee.Intelligence, impact);
            string Charm(string impact) => Skill("魅力", charm, impact);
            static string Skill(string name, int score, string impact) => $"<b>{name} {score}</b> · {impact}";

            return role switch
            {
                EmployeeRole.Cashier when S.IsDedicatedVenue(shop?.Place) =>
                    ("接待到场顾客", "到岗值守，维持场馆营业", "<b>出勤</b> · 按排班到岗"),
                EmployeeRole.Cashier =>
                    ("给顾客结账", $"收银 {GameSession.EmployeeCheckoutCycleMinutes(employee)} 分钟 / 单", Dexterity("越高，结账越快")),
                EmployeeRole.Assembler =>
                    ("组装订单电脑", $"装机 {GameSession.AssemblyMinutesOf(employee)} 分钟 / 台 · 普通订单整机售价 +{GameSession.AssemblyPriceBonus(employee):0.#%}",
                        $"<b>动手能力 {employee.Dexterity} · 智力 {employee.Intelligence}</b>\n提高装机速度和普通整机售价\n<b>已装 {employee.BuildsCompleted} 台</b> · 经验缩短装机时间"),
                EmployeeRole.Buyer =>
                    ("按计划外出进货", $"进货价优惠 {GameSession.EmployeePurchaseDiscount(employee):0.#%}",
                        Intelligence("越高，进货折扣越大")),
                EmployeeRole.Salesperson =>
                    ("向顾客推介商品", $"在店上班时，购买意愿个人加成 +{GameSession.EmployeeSalesBonus(employee) * 100:0.##} 个百分点",
                        Charm("越高，顾客越愿意购买")),
                EmployeeRole.Chef =>
                    ("备料、下锅、出锅和清锅", $"每步厨房操作 {GameSession.RestaurantEmployeeOperationMinutes(employee, role)} 分钟", Dexterity("越高，厨房操作越快")),
                EmployeeRole.Waiter =>
                    ("上菜、桌边收款和收桌", $"每步服务 {GameSession.RestaurantEmployeeOperationMinutes(employee, role)} 分钟 · 小费概率 {GameSession.WaiterTipChance(charm):0.#%}（最高为菜价的 {GameSession.WaiterTipUpperRate(charm):0.#%}）",
                        Dexterity("影响服务速度") + "\n" + Charm("影响小费概率和上限")),
                EmployeeRole.Appraiser =>
                    ("判断成色并收购旧物", $"识别真实成色概率 {GameSession.AppraiserAccuracy(employee.Intelligence):0.#%}", Intelligence("越高，鉴定越准")),
                EmployeeRole.ModelPainter =>
                    ("在喷漆台处理客带模型", $"加工速度 ×{GameSession.ModelPainterSpeed(employee):0.##} · 品质 {GameSession.ModelPainterQuality(employee):0.#}/10",
                        Dexterity("同时提高加工速度和品质")),
                EmployeeRole.Repairer =>
                    ("在二手修复台修复旧物", $"修复速度 ×{GameSession.UsedRepairSpeed(employee):0.##} · 成功率 {GameSession.UsedRepairSuccessChance(employee):0.#%}",
                        Dexterity("更影响修复速度") + "\n" + Intelligence("更影响修复成功率")),
                EmployeeRole.PetKeeper =>
                    ("给宠物喂食、换水、清理和陪玩", $"最多照顾 {GameSession.PetsPerKeeper} 只宠物 · 每只状态每天 +{GameSession.PetKeeperCareBonus(employee)}",
                        $"<b>魅力 {charm} · 动手能力 {employee.Dexterity}</b>\n提高每日照料的状态加成"),
                _ => ("暂未分配工作", "待安排期间照常发薪", null)
            };
        }



    }
}
