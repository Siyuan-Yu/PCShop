using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _cityFinanceShop;
        private int _cityFinancePeriod, _cityFinancePage;

        public void OpenCityFinanceOverview()
        {
            _cityFinanceShop = null; _cityFinancePeriod = 0; _cityFinancePage = 0;
            OpenWindow("经营总览", 1160, 790, w => BuildCityFinanceOverview(w));
        }
        private void BuildCityFinanceOverview(Window w)
        {
            if (_cityFinanceShop != null && S.ShopAt(_cityFinanceShop) == null) _cityFinanceShop = null;
            var today = S.Clock.Today;
            int first = _cityFinancePeriod == 2 ? today.TotalDays / 30 * 30 : _cityFinancePeriod == 1 ? today.TotalDays / 7 * 7 : today.TotalDays;
            var from = new GameDate(first);
            var summary = S.GetCityFinance(from, today, _cityFinanceShop);
            var reserve = S.GetCityCashReserve(7);
            var links = Ui.Row(w.Content, 42, 8);
            Ui.Size(Ui.Button(links, "全部门店", () => { _cityFinanceShop = null; _cityFinancePage = 0; w.Rebuild(); }, 18, _cityFinanceShop == null ? Ui.ButtonHot : Ui.ButtonBg), 145);
            Ui.Size(Ui.Button(links, "原总账本", OpenLedger, 18), 140);
            Ui.Size(Ui.Button(links, "市场 / 模型", OpenCityRetailPanel, 18), 150);
            Ui.Size(Ui.Button(links, "直供 / 合同 / 其他业务", () => ShowBusinessPanel(false), 18), -1, -1, 1);
            var periods = Ui.Row(w.Content, 40, 8);
            var management=Ui.Row(w.Content,40,8);
            Ui.Size(Ui.Button(management,"统一经营采购",OpenCityProcurement,18),200);
            Ui.Size(Ui.Button(management,"具体物业",OpenCityProperties,18),160);
            Ui.Size(Ui.Button(management,"投资持仓",OpenCityInvestments,18),160);
            Ui.Size(Ui.Button(management,"城市生活",OpenCityLife,18),160);
            string[] names = { "今日", "本周", "本月" };
            for (int i = 0; i < names.Length; i++) { int period = i; Ui.Size(Ui.Button(periods, names[i], () => { _cityFinancePeriod = period; _cityFinancePage = 0; w.Rebuild(); }, 18, i == _cityFinancePeriod ? Ui.ButtonHot : Ui.ButtonBg), 110); }
            Line(w.Content, $"统计日期：{from} — {today} · {(_cityFinanceShop == null ? "全部门店" : S.ShopTitle(S.ShopAt(_cityFinanceShop)))}", 19, Ui.Accent);
            Line(w.Content, $"现金 {S.Ledger.Balance}   经营现金净额 {summary.OperatingNet}   销售收款 {summary.Sales}", 22, Ui.Good);
            Line(w.Content, $"未来七天已知必付 {reserve.KnownMandatory}   估算日常（含排班加班）{reserve.EstimatedDaily}   额外保护 {reserve.ExtraProtection}", 18);
            Line(w.Content, $"预留后可安排资金 {reserve.Available}（{reserve.From} — {reserve.To}）", 20, reserve.Available.IsNegative ? Ui.Warn : Ui.Accent);
            Para(w.Content, "现金净额不是利润：采购尚未售出的货仍占用资金。贷款、投资、押金和内部转账不计商品销售；无店铺信息的历史账与公共费用单列。工资和到期账单只预留一次。", 17, Ui.Dim);
            if (_cityFinanceShop == null)
            {
                Line(w.Content, "门店 / 营业状态 · 销售 / 经营支出 / 现金净额 · 库存占款", 18, Ui.Accent);
                foreach (var shop in S.MyShops)
                {
                    var row = S.GetCityFinance(from, today, shop.Place.Id);
                    Money capital = S.CityShopInventoryCapital(shop, out bool estimated);
                    string shopId = shop.Place.Id;
                    string staff = string.Join("、", S.Employees.Where(e => e.Shop == shop && S.CityEmployeeAvailable(e.Person, shop)).Select(e => e.Person.Name));
                    if (S.CityPlayerCanWork(shop)) staff = "老板" + (staff.Length > 0 ? "、" + staff : "");
                    string state = shop.Sim.IsOpen ? "营业" : shop.RenovationDay == today.TotalDays ? "歇业装修" : !shop.Sim.InHours(S.Clock.Now) ? "营业时间外" : "暂停服务 / 请查在岗人员或设施";
                    string attention = shop.Sim.Stock.Lines().Any() || shop.Sim.Stock.GoodsLines().Any() ? "" : " · 关注：没有现货";
                    string text = $"{S.ShopTitle(shop)} · {shop.Place.Name} · {state}\n销售 {row.Sales} / 支出 {row.OperatingOut} / 净额 {row.OperatingNet} · 库存 {capital}{(estimated ? "（部分估算）" : "（采购成本）")}\n当班：{(staff.Length > 0 ? staff : "无人")}{attention}";
                    Ui.Size(Ui.Button(w.Content, text+"\n"+S.CityShopClimateSummary(shop.Place.Id), () => { _cityFinanceShop = shopId; _cityFinancePage = 0; w.Rebuild(); }, 18), -1, 126);
                }
                if (S.MyShopCount == 0) Para(w.Content, "还没有门店。先去房产中介租下开局店铺；总览不要求先解锁后期业务。", 18);
                Line(w.Content, $"公共支出 / 历史未归属门店：流入 {summary.UnassignedOperatingIn} · 支出 {summary.UnassignedOperatingOut}", 18, Ui.Dim);
                Line(w.Content, $"个人生活净额 {summary.PersonalNet} · 资产投资净额 {summary.InvestmentNet} · 融资净额 {summary.FinancingNet}", 17, Ui.Dim);
                Line(w.Content, $"内部划转 {summary.InternalNet}（合并经营口径抵销） · 类别未知历史净额 {summary.HistoricalNet}", 17, Ui.Dim);
            }
            else
            {
                foreach (var group in S.Ledger.Entries.Where(e => e.ShopId == _cityFinanceShop && e.Date >= from && e.Date <= today && e.BusinessKind == LedgerBusinessKind.Operating).GroupBy(e => e.Category))
                    Line(w.Content, group.Key + "：" + Money.FromCents(group.Sum(e => e.Amount.Cents)), 18);
                var entries = S.Ledger.Entries.Where(e => e.ShopId == _cityFinanceShop && e.Date >= from && e.Date <= today).Reverse().ToList();
                _cityFinancePage = Math.Max(0, Math.Min(_cityFinancePage, Math.Max(0, (entries.Count - 1) / 15)));
                foreach (var e in entries.Skip(_cityFinancePage * 15).Take(15))
                    Para(w.Content, $"{e.Date} · {LedgerAttribution.KindName(e.BusinessKind)} / {e.Category} · {e.Amount}\n{e.Memo} · 余额 {e.BalanceAfter}", 17, e.Amount.IsNegative ? Ui.TextColor : Ui.Good);
                if (entries.Count == 0) Para(w.Content, "该日期区间还没有明确归属这家店的账目。旧账不凭备注推测店铺。", 18, Ui.Dim);
                var pages = Ui.Row(w.Content, 40, 8);
                Ui.Size(Ui.Button(pages, "上一页", () => { _cityFinancePage--; w.Rebuild(); }, 18, Ui.ButtonBg, _cityFinancePage > 0), 145);
                Ui.Size(Ui.Button(pages, "下一页", () => { _cityFinancePage++; w.Rebuild(); }, 18, Ui.ButtonBg, (_cityFinancePage + 1) * 15 < entries.Count), 145);
            }
        }
    }
}
