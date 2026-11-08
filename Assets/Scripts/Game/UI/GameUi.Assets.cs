using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _assetPage;
        private string _assetStreet, _assetKind, _assetStoreId, _assetPropertyId;
        private readonly AssetPropertyMap.ViewState _assetMapView = new AssetPropertyMap.ViewState();
        private GameSession _assetSession;
        private bool _assetFocusProperty;
        private readonly HashSet<string> _assetCollapsedStreets = new HashSet<string>();
        private readonly HashSet<string> _assetCollapsedKinds = new HashSet<string>();

        private void OpenAssetWorkspace(int page = 0, string storeId = null, string propertyId = null)
        {
            if (_assetSession != S)
            {
                _assetSession = S; _assetStreet = _assetKind = _assetStoreId = _assetPropertyId = null;
                _assetMapView.Initialized = false;
                _assetCollapsedStreets.Clear(); _assetCollapsedKinds.Clear();
                _assetPropertyListTab = 0;
            }
            _assetPage = page;
            if (storeId != null)
            {
                _assetStoreId = storeId;
                var report = S.AssetStoreReports().FirstOrDefault(r => r.Id == storeId);
                _assetStreet = report?.StreetId; _assetKind = report?.Kind;
            }
            if (propertyId != null) { _assetPropertyId = propertyId; _assetFocusProperty = true; }
            OpenWorkspace("assets", "overview", "资产", 1280, 900, w =>
            {
                var tabs = Ui.Row(w.Content, 42, 8);
                for (int i = 0; i < 3; i++)
                {
                    int choice = i;
                    Ui.Size(Ui.Button(tabs, new[]{"经营中的店铺","我的物业","车产"}[i], () => { _assetPage = choice; w.Rebuild(); }, 20,
                        _assetPage == i ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1).name = "AssetPage_" + i;
                }
                if (_assetPage == 0) BuildAssetStores(w); else if(_assetPage==1) BuildAssetProperties(w);else BuildAssetVehicles(w);
            });
        }

        private void BuildAssetStores(Window w)
        {
            S.VisitQuestTarget(QuestTarget.Finance);
            var reports = S.AssetStoreReports();
            if (_assetStoreId != null && !reports.Any(r => r.Id == _assetStoreId)) _assetStoreId = null;
            if (!reports.Any(r => r.StreetId == _assetStreet)) _assetStreet = null;
            if (!reports.Any(r => r.Kind == _assetKind && (_assetStreet == null || r.StreetId == _assetStreet))) _assetKind = null;
            var visible = reports.Where(r => (_assetStreet == null || r.StreetId == _assetStreet) && (_assetKind == null || r.Kind == _assetKind)).ToArray();
            var scope = _assetStoreId == null ? visible : reports.Where(r => r.Id == _assetStoreId).ToArray();
            var layout = Ui.Rect(w.Content, "AssetStoreLayout"); Ui.Size(layout, -1, 744);
            var side = Ui.Rect(layout, "StreetTree"); side.Anchor(0, 0, 0, 1, 0, 0, -198, 0); Ui.VList(side, 3, 0);
            var tree = Ui.Scroll(side, 730);
            void Choice(string title, string street, string kind, string id, int indent, bool active, Action toggle = null, bool expanded = true)
            {
                Transform parent = tree;
                if (toggle != null)
                {
                    var row = Ui.Row(tree, 35, 2);
                    if (indent > 0) Ui.Size(Ui.Rect(row,"TreeIndent"),indent*9,32);
                    var arrow = Ui.Size(Ui.Button(row,expanded?"▾":"▸",toggle,16,Ui.PanelBg2),26,33);
                    arrow.name="AssetFold_"+(street??"all")+"_"+(kind??"street");
                    arrow.Tip(expanded?"收起这一组":"展开这一组");
                    parent = row;
                }
                var button = Ui.Size(Ui.Button(parent, (toggle == null ? new string(' ', indent) : "") + title, () =>
                    { _assetStreet = street; _assetKind = kind; _assetStoreId = id; w.Rebuild(); }, 16, active ? Ui.ButtonHot : Ui.PanelBg2), -1, id == null ? 35 : 43);
                if(toggle!=null)Ui.Size(button,-1,-1,1);
                var text = button.GetComponentInChildren<Text>(); text.alignment = TextAnchor.MiddleLeft; text.rectTransform.Fill(5);
                button.name = "AssetFilter_" + (id ?? kind ?? street ?? "all");
            }
            Choice("全部门店 · " + reports.Length + "家", null, null, null, 0, _assetStreet == null && _assetStoreId == null);
            foreach (var street in reports.GroupBy(r => r.StreetId))
            {
                bool streetExpanded=!_assetCollapsedStreets.Contains(street.Key);
                Choice(S.City.StreetName(street.Key), street.Key, null, null, 0, _assetStreet == street.Key && _assetKind == null && _assetStoreId == null,
                    ()=>{if(!_assetCollapsedStreets.Add(street.Key))_assetCollapsedStreets.Remove(street.Key);w.Rebuild();},streetExpanded);
                if(!streetExpanded)continue;
                foreach (var kind in street.GroupBy(r => r.Kind))
                {
                    string kindKey=street.Key+"\u001f"+kind.Key;
                    bool kindExpanded=!_assetCollapsedKinds.Contains(kindKey);
                    Choice(kind.Key + " · " + kind.Count() + "家", street.Key, kind.Key, null, 1, _assetStreet == street.Key && _assetKind == kind.Key && _assetStoreId == null,
                        ()=>{if(!_assetCollapsedKinds.Add(kindKey))_assetCollapsedKinds.Remove(kindKey);w.Rebuild();},kindExpanded);
                    if(!kindExpanded)continue;
                    foreach (var report in kind) Choice(report.Name, street.Key, kind.Key, report.Id, 2, _assetStoreId == report.Id);
                }
            }
            var main = Ui.Rect(layout, "StoreReport"); main.Anchor(0, 0, 1, 1, 216, 0, 0, 0); Ui.VList(main, 6, 0);
            var head = Ui.Row(main, 32);
            string title = _assetStoreId != null ? scope.FirstOrDefault()?.Name : (_assetKind ?? (_assetStreet == null ? "全部门店" : S.City.StreetName(_assetStreet))) + " · " + scope.Length + "家";
            Ui.Size(Ui.Label(head, title ?? "门店收支", 21, TextAnchor.MiddleLeft, Ui.Accent), -1, 32, 1);
            var from = new GameDate(Math.Max(0, S.Clock.Today.TotalDays - 6));
            Ui.Size(Ui.Label(head, $"最近7天 · {from.Month}/{from.Day}—{S.Clock.Today.Month}/{S.Clock.Today.Day}", 15, TextAnchor.MiddleRight, Ui.Dim), 220);
            if (reports.Length == 0) { Line(main, "还没有经营中的门店，先去所属片区中介租铺。", 19); return; }
            var income = MergeAssetSources(scope, true); var expense = MergeAssetSources(scope, false);
            long totalIn = scope.Sum(r => r.IncomeCents), totalOut = scope.Sum(r => r.ExpenseCents);
            var cards = Ui.Row(main, 160, 8);
            BuildAssetMoneyCard(cards, "收入", totalIn, Ui.Good, income);
            BuildAssetMoneyCard(cards, "支出", totalOut, new Color32(233, 178, 150, 255), expense);
            var net = Ui.Panel(cards, "AssetNet", Ui.PanelBg2).rectTransform; Ui.Size(net, -1, 160, 1); Ui.VList(net, 3, 10);
            Line(net, "收支差额", 16, Ui.Dim);
            var netAmount=Line(net,(totalIn>=totalOut?"+":"−")+Money.FromCents(Math.Abs(totalIn-totalOut)),27,Ui.Accent);
            netAmount.resizeTextForBestFit=true;netAmount.resizeTextMinSize=17;netAmount.resizeTextMaxSize=27;
            Line(net, "收入 − 支出", 15, Ui.Dim); Line(net, "按实际收付统计", 15, Ui.Dim);
            var legend = Ui.Row(main, 22); Ui.Size(Ui.Label(legend, "每日收支", 17), -1, 22, 1);
            Ui.Size(Ui.Label(legend, "● 收入", 15, TextAnchor.MiddleRight, Ui.Good), 80); Ui.Size(Ui.Label(legend, "● 支出  ·  元", 15, TextAnchor.MiddleRight, new Color32(233,178,150,255)), 135);
            int dayCount = S.Clock.Today.TotalDays - from.TotalDays + 1;
            long[] ins = Enumerable.Range(0, dayCount).Select(i => scope.Sum(r => r.IncomeDays[i])).ToArray();
            long[] outs = Enumerable.Range(0, dayCount).Select(i => scope.Sum(r => r.ExpenseDays[i])).ToArray();
            BuildAssetTrend(main, ins, outs, from.TotalDays, 155);
            Ui.Size(Ui.Button(main, "查看收支来源明细", () => OpenAssetSources(scope, from, S.Clock.Today), 15), -1, 31);
            var labels = Ui.Row(main, 25);
            Ui.Size(Ui.Label(labels, "门店", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 25, 1);
            foreach (string text in new[] { "收入", "支出", "差额", "7天走势" }) Ui.Size(Ui.Label(labels, text, 15, TextAnchor.MiddleRight, Ui.Dim), text == "7天走势" ? 100 : 127);
            var rows = Ui.Scroll(main, 144);
            foreach (var report in visible)
            {
                var row = Ui.Row(rows, 59, 4);
                var select = Ui.Size(Ui.Button(row, report.Name + "\n<size=13>" + report.State + "</size>", () =>
                    { _assetStoreId = report.Id; _assetStreet = report.StreetId; _assetKind = report.Kind; w.Rebuild(); }, 17,
                    _assetStoreId == report.Id ? Ui.ButtonHot : Ui.ButtonBg), -1, 57, 1);
                select.name = "AssetStore_" + report.Id;
                foreach (var pair in new[] { (report.IncomeCents, Ui.Good), (report.ExpenseCents, (Color)new Color32(233,178,150,255)), (report.IncomeCents-report.ExpenseCents, Ui.Accent) })
                    Ui.Size(Ui.Label(row, Money.FromCents(pair.Item1).ToString(), 17, TextAnchor.MiddleRight, pair.Item2), 127, 55);
                BuildAssetTrend(row, report.IncomeDays, report.ExpenseDays, report.FirstDay, 34, true);
            }
            var foot = Ui.Row(main, 32);
            Ui.Size(Ui.Label(foot, "含进货付款；收支差额不等于利润。托管营业额包含门店周转金账户。", 14, TextAnchor.MiddleLeft, Ui.Dim), -1, 32, 1);
            if (_assetStoreId != null && scope[0].Managed)
                Ui.Size(Ui.Button(foot, "管理门店", () => OpenBusinessSection(3, _assetStoreId), 16), 116, 32);
        }

        private static Dictionary<string, long> MergeAssetSources(IEnumerable<AssetStoreReport> reports, bool income)
        {
            return reports.SelectMany(r => income ? r.IncomeSources : r.ExpenseSources).GroupBy(p => p.Key)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Value));
        }

        private void BuildAssetMoneyCard(Transform parent, string title, long cents, Color color, Dictionary<string, long> sources)
        {
            var card = Ui.Panel(parent, "Asset" + title, Ui.PanelBg2).rectTransform; Ui.Size(card, -1, 160, 1); Ui.VList(card, 2, 8);
            Ui.Size(Ui.Label(card,title,16,TextAnchor.MiddleLeft,Ui.Dim),-1,22);
            var amount=Ui.Size(Ui.Label(card,Money.FromCents(cents).ToString(),27,TextAnchor.MiddleLeft,color),-1,35);
            amount.resizeTextForBestFit=true;amount.resizeTextMinSize=17;amount.resizeTextMaxSize=27;
            var sorted = sources.OrderByDescending(p => p.Value).ToArray();
            for (int i = 0; i < Math.Min(3, sorted.Length); i++) Ui.Size(Ui.Label(card,sorted[i].Key+"  "+Money.FromCents(sorted[i].Value),14),-1,19);
            if (sorted.Length > 3) Ui.Size(Ui.Label(card,"其余 "+(sorted.Length-3)+"项  "+Money.FromCents(sorted.Skip(3).Sum(p=>p.Value)),14,TextAnchor.MiddleLeft,Ui.Dim),-1,19);
            if (sorted.Length == 0) Line(card, "本期暂无" + title, 15, Ui.Dim);
        }

        private void OpenAssetSources(AssetStoreReport[] scope, GameDate from, GameDate to)
        {
            OpenWindow("门店收支来源", 850, 690, w =>
            {
                Line(w.Content, (scope.Length == 1 ? scope[0].Name : "所选" + scope.Length + "家门店") + " · " + from + "—" + to, 20, Ui.Accent);
                foreach (bool income in new[] { true, false })
                {
                    var sources = MergeAssetSources(scope, income);
                    Line(w.Content, income ? "收入来源" : "支出去向", 21, income ? Ui.Good : Ui.Accent);
                    foreach (var pair in sources.OrderByDescending(p => p.Value))
                    {
                        var row = Ui.Row(w.Content, 31);
                        Ui.Size(Ui.Label(row, pair.Key, 18), -1, 31, 1);
                        Ui.Size(Ui.Label(row, Money.FromCents(pair.Value).ToString(), 18, TextAnchor.MiddleRight), 200, 31);
                    }
                    if (sources.Count == 0) Line(w.Content, "本期暂无记录", 17, Ui.Dim);
                }
                Line(w.Content, "借款、押金、购房与内部划转不计入门店经营收支。", 16, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "全部现金流水", OpenLedger, 17), -1, 36);
            });
        }

        private static void BuildAssetTrend(Transform parent, long[] income, long[] expense, int firstDay, float height, bool compact = false)
        {
            var rect = Ui.Rect(parent, compact ? "AssetSparkline" : "AssetIncomeExpenseTrend"); Ui.Size(rect, compact ? 100 : -1, height);
            var trend = rect.gameObject.AddComponent<AssetTrendGraphic>(); trend.Initialize(income, expense, firstDay, compact);
        }
    }
}
