using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenCityRetailPanel() => OpenMarketView(2);
        private void BuildCompetitionMarket(Window w)
        {
                Line(w.Content, "本店与附近竞争动态", 19, Ui.Accent);
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

        }

        private string _modelSeries;
        public void OpenCityModelCatalog() => OpenMarketView(3);
        private void BuildModelCatalog(Window w)
        {
            var filters=Ui.Row(w.Content,36,5);
            Ui.Size(Ui.Button(filters,"全部系列",()=>{_modelSeries=null;w.Rebuild();},16,_modelSeries==null?Ui.ButtonHot:Ui.ButtonBg),-1,36,1);
            foreach(string value in ModelProducts.SeriesIds)
            {
                string series=value;Ui.Size(Ui.Button(filters,ModelProducts.SeriesName(series),()=>{_modelSeries=series;w.Rebuild();},16,_modelSeries==series?Ui.ButtonHot:Ui.ButtonBg),-1,36,1);
            }
            foreach(var product in ModelProducts.All.Where(p=>_modelSeries==null||p.ModelSeries==_modelSeries))
            {
                var item=product;var row=Ui.Row(w.Content,52,6);
                Ui.Size(Ui.Label(row,item.Name+" · "+item.Price+" · "+item.WeightKg.ToString("0.##")+"千克"+(item.LimitedEdition?" · 限定":""),17),-1,52,1);
                Ui.Size(Ui.Button(row,"详情",()=>OpenWorkspace("product-info",item.Id,item.Name,720,540,detail=>
                {
                    Para(detail.Content,item.Description,18);
                    foreach(var source in S.City.Places.Where(p=>p.Business==ModelProducts.Business&&!S.IsMyShop(p)))
                    {var place=source;Ui.Size(Ui.Button(detail.Content,"前往货源："+place.Name,()=>{CloseAllWindows();_ctrl.GoToPlace(place);},17),-1,38);}
                }),16),80,36);
            }
            Ui.Size(Ui.Button(w.Content,"漫展限定采购（需到场）",OpenCityModelExpo,17),-1,38);
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
