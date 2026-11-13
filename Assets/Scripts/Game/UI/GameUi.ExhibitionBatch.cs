using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _exhibitionModelId = "model:garden:greenhouse";
        private void BuildExhibitionActivity(Window w, CityActivity activity)
        {
            var s = S.OutdoorActivityState(activity.Id); if (s == null) return;
            if (s.Number is 71 or 72)
                Ui.Size(Ui.Button(w.Content, "打开旧物市场 · 看货 / 检测 / 购买 / 维修", () => OpenCityVenue(S.City.GetPlace(GameSession.OldMarketVenueId)), 18, Ui.ButtonHot), -1, 44);
            if (s.Number == 73)
                Ui.Size(Ui.Button(w.Content, "查看真实采购渠道", OpenCityProcurement, 18, Ui.ButtonHot, S.HasOpenedComputerShop), -1, 42);
            if (s.Number == 74 && activity.ServiceDefinitionId == "social_74_work")
                Line(w.Content, $"已解决 {s.Correct}/3 个展台；完整完成后劳务费 ¥1,500", 17, Ui.Accent);
            var models = S.ExhibitionModels(activity.Id); if (models.Length == 0) return;
            int index = Array.FindIndex(models, p => p.Id == _exhibitionModelId); if (index < 0) index = 0;
            _exhibitionModelId = models[index].Id;
            Line(w.Content, "展位现货 · 可选择购买，也可继续参观", 19, Ui.Accent);
            Ui.Size(Ui.Choice(w.Content, models.Select(p => p.Name + (p.LimitedEdition ? " · 展期限定" : " · 常规款")), index,
                i => { _exhibitionModelId = models[i].Id; w.RequestRebuild(); }), -1, 42);
            var product = models[index]; var price = S.ExhibitionModelPrice(product.Id);
            Para(w.Content, product.Description, 16, Ui.Dim);
            Ui.Size(Ui.Button(w.Content, "购买一件 · " + price, () =>
            { S.BuyExhibitionModel(activity.Id, product.Id); w.RequestRebuild(); }, 18, Ui.ButtonHot, s.Pending == null), -1, 42);
            if (s.Number == 77)
                Ui.Size(Ui.Button(w.Content, "把背包中这件模型送给沈晓", () =>
                { S.GiftExhibitionModel(activity.Id, product.Id); w.RequestRebuild(); }, 18, Ui.ButtonHot,
                    !s.Delivered && s.Pending == null && S.Backpack.GiftCount(product.Id) > 0), -1, 42);
        }
        private void BuildEventChangeRefunds(Window w)
        {
            if (!S.ChangeRefundDebt.IsPositive) return;
            Line(w.Content, "顾客待退找零 · " + S.ChangeRefundDebt, 18, Ui.Accent);
            foreach (var item in S.PendingChangeRefunds)
            {
                var receipt = item;
                Para(w.Content, (S.City.GetPlace(receipt.ShopId)?.Name ?? receipt.ShopId) + " · " + receipt.Product + " · " + Money.FromCents(receipt.ChangeCents), 16);
                Ui.Size(Ui.Button(w.Content, "补还这笔差额", () => { S.RefundEventChange(receipt.Key); w.RequestRebuild(); }, 17, Ui.ButtonHot,
                    S.ShopAt(receipt.ShopId) == null || S.CurrentPlace?.Id == receipt.ShopId), -1, 36);
            }
        }
    }
}
