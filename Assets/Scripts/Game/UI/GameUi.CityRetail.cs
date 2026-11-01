using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenCityRetailPanel()
        {
            OpenWindow("市场变化与原创模型", 1020, 760, w =>
            {
                Line(w.Content, "市场变化有明确期限，不直接扣除资产。你的库存、定价和实际值守仍决定能否成交。", 18, Ui.Accent);
                foreach (var e in S.CityCompetitions.OrderByDescending(e => e.StartDay).Take(9))
                {
                    var item = e;
                    bool active = !item.Ended && S.Clock.Today.TotalDays >= item.StartDay && S.Clock.Today.TotalDays < item.EndDay;
                    Line(w.Content, S.CityCompetitionTitle(item) + (active ? " · 进行中" : " · 已结束"), 20, active ? Ui.Warn : Ui.Dim);
                    Line(w.Content, $"{new GameDate(item.StartDay)} — {new GameDate(item.EndDay - 1)} · 目标：{(S.ShopAt(item.ShopId) != null ? S.ShopTitle(S.ShopAt(item.ShopId)) : item.ShopId)}", 16);
                    Para(w.Content, S.CityCompetitionTerms(item), 17);
                    if (active)
                    {
                        var actions = Ui.Row(w.Content, 42, 8);
                        void Choose(string title, string response) => Ui.Size(Ui.Button(actions, title + (item.Response == response ? " ✓" : ""), () => { S.RespondCityCompetition(item.Id, response); w.Rebuild(); }, 17), -1, 42, 1);
                        if (item.Kind == "promotion") { Choose("维持定位", "maintain"); Choose("突出特色陈列", "position"); }
                        else if (item.Kind == "terms") { Choose("接受3件起订", "batch"); Choose("小批次 / 价格+8%", "small"); }
                        else Choose("改看其他已知货源", "alternative");
                    }
                }
                if (S.CityCompetitions.Count == 0) Para(w.Content, "目前没有市场变化。第15天起按固定节奏出现，每次持续3天，结束后临时影响解除。", 17, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "原创模型商品目录", OpenCityModelCatalog, 18, Ui.ButtonHot), -1, 44);
            });
        }

        public void OpenCityModelCatalog()
        {
            OpenWindow("原创模型 / 手办店", 960, 760, w =>
            {
                Para(w.Content, "第15天起可在中介签模型手办分店。四个原创系列，共12款明确现货：8款常规批发、4款漫展限定。顾客按兴趣系列及预算挑选；没有货架或库存、无人收银时不会凭空形成收入。", 18);
                foreach (var source in S.City.Places.Where(p => p.Business == ModelProducts.Business && !S.IsMyShop(p)))
                {
                    var place = source;
                    Ui.Size(Ui.Button(w.Content, "前往货源：" + place.Name, () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 18), -1, 44);
                }
                foreach (var series in ModelProducts.SeriesIds)
                {
                    Line(w.Content, ModelProducts.SeriesName(series), 22, Ui.Accent);
                    foreach (var product in ModelProducts.All.Where(p => p.ModelSeries == series))
                        Para(w.Content, product.Name + " · 零售参考 " + product.Price + " · " + product.WeightKg.ToString("0.##") + "千克\n" + product.Description, 17);
                }
                Ui.Size(Ui.Button(w.Content, "漫展限定采购（需到场）", OpenCityModelExpo, 18), -1, 44);
            });
        }

        public void OpenCityModelExpo()
        {
            if(!S.CityComicExpoOpen){Toast("请在漫展开放时到会展中心现场选购。");return;}
            var place=S.CurrentPlace;
            CloseStoreWindows();
            OpenPlaceWindow(place,(w,p)=>BuildCarryCatalog(w,p,"expo"),1120,960,holds:true,leaveOnClose:false);
        }
    }
}
