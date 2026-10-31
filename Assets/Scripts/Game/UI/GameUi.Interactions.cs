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

        private void OpenEmployeeShift(string id, Window parent)
        {
            var person = S.FindEmployee(id)?.Person;
            if (person == null) return;
            OpenWindow("选择班次 · " + person.Name, 500, 290, w =>
            {
                foreach (var shift in new[] { (540, 1260), (540, 1020), (780, 1260) })
                {
                    var value = shift;
                    bool current = person.ShiftStart == value.Item1 && person.ShiftEnd == value.Item2;
                    Ui.Size(Ui.Button(w.Content, Ui.Hm(value.Item1) + "–" + Ui.Hm(value.Item2) + (current ? " ✓" : ""), () =>
                    {
                        if (S.SetEmployeeShift(id, value.Item1, value.Item2)) { w.Close(); parent.Rebuild(); }
                    }, 19, current ? Ui.ButtonHot : Ui.ButtonBg), -1, 48);
                }
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

        private void StartQuickPlacement()
        {
            if (S.BeginQuickShopEdit() && !_ctrl.BeginPlacement(true)) S.EndQuickShopEdit();
        }

        private void OpenAmountPicker(string title, double max, string unit, Action<double> apply)
        {
            double amount = Math.Min(1, max);
            OpenWindow(title + " · 数量", 520, 270, picker =>
            {
                Para(picker.Content, $"最多 {max:0.#} {unit}；输入数量后选择应用。", 17);
                Ui.Size(Ui.TextInput(picker.Content, amount.ToString("0.#"), "数量", text =>
                {
                    if (double.TryParse(text, out double n) && !double.IsNaN(n) && !double.IsInfinity(n))
                        amount = Math.Max(0, Math.Min(max, unit == "件" ? Math.Floor(n) : n));
                }), -1, 42);
                var actions = Ui.Row(picker.Content, 44);
                Ui.Size(Ui.Button(actions, "全部", () => { picker.Close(); apply(max); }, 18), -1, -1, 1);
                Ui.Size(Ui.Button(actions, "应用数量", () => { if (amount > 0) { picker.Close(); apply(amount); } }, 18, Ui.ButtonHot), -1, -1, 1);
            });
        }
    }
}
