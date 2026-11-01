using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildRetailLinks(Transform parent)
        {
            Line(parent, "经营类型：" + RetailShopTypes.Name(S.RetailKind), 18, Ui.Accent);
            var row = Ui.Row(parent, 38);
            Ui.Size(Ui.Button(row, "进货渠道", OpenRetailSources, 17, Ui.ButtonHot), -1, -1, 1);
            Ui.Size(Ui.Button(row, "背包/纸箱商品上架", OpenRetailListing, 17), -1, -1, 1);
            Ui.Size(Ui.Button(row, "店铺类型", OpenRetailTypeChooser, 17), -1, -1, 1);
        }
        private void OpenRetailListing()
        {
            OpenWindow("商品上架", 820, 700, w =>
            {
                if (!S.InPlayerShop) { w.Close(); return; }
                Para(w.Content, "将背包或本店纸箱里的商品放入后仓；摆好并设置相应品类的货架后，顾客才能买到。", 18);
                var items = Gifts.All.Where(g => S.GoodsAvailableToList(g.Id) > 0 && RetailShopTypes.Sells(S.RetailKind, g.Id)).ToArray();
                var list = Ui.Scroll(w.Content, 510);
                if (items.Length == 0) Para(list, "手边没有可上架的商品。可先去批发进货，或让采购员直接送入后仓。", 18, Ui.Dim);
                foreach (var product in items)
                {
                    string id = product.Id;
                    int have = S.GoodsAvailableToList(id);
                    Line(list, product.Name + $" · 手边 {have} 件 · 每件 {product.WeightKg:0.##} 千克", 18, Ui.Accent);
                    var row = Ui.Row(list, 38);
                    Ui.Size(Ui.Button(row, "上架 1 件", () => { S.ListGoods(id, 1); w.Rebuild(); RebuildDock(); }, 17), -1, -1, 1);
                    Ui.Size(Ui.Button(row, "全部上架", () => { S.ListGoods(id, have); w.Rebuild(); RebuildDock(); }, 17, Ui.ButtonHot), -1, -1, 1);
                }
            });
        }
        private void OpenRetailTypeChooser()
        {
            OpenWindow("店铺经营类型", 760, 650, w =>
            {
                Para(w.Content, "开局主店经营电脑。第 15 天起，签新分店时可选择花店、生鲜店或食品店。", 19, Ui.Accent);
                foreach (string id in RetailShopTypes.All)
                {
                    string kind = id;
                    Line(w.Content, RetailShopTypes.Name(kind) + (S.RetailKind == kind ? " · 当前" : ""), 20);
                    Para(w.Content, RetailShopTypes.Description(kind), 16, Ui.Dim);
                    if (S.InPlayerShop && S.CurrentShop != S.MainShop && kind != S.RetailKind)
                        Ui.Size(Ui.Button(w.Content, "改为" + RetailShopTypes.Name(kind), () => ConfirmBusiness("更换经营类型",
                            "需先清空库存、展示机、订单和在途货物。更换后当天歇业装修，明天营业；请设置相应商品货架。", () =>
                            { if (S.SetRetailKind(kind)) { w.Rebuild(); RebuildDock(); } }), 17, Ui.ButtonBg,
                            S.RetailTypeBlocker(kind) == null), -1, 36);
                }
            });
        }
        private void OpenRetailSources()
        {
            OpenWindow("进货渠道", 820, 720, w =>
            {
                Para(w.Content, "去对应店铺打开“批发进货”，可放背包带回上架，也可叫货车送入指定店铺后仓。员工管理的采购清单同样包含这些货源。", 18);
                var list = Ui.Scroll(w.Content, 520);
                foreach (var p in S.City.Places.Where(p => S.HasRetailWholesale(p)))
                {
                    var source = p;
                    Line(list, source.Name + " · " + S.OpeningHoursText(source), 19, Ui.Accent);
                    Para(list, string.Join("、", Gifts.All.Where(g => g.SoldAt == source.Business).Select(g => g.Name)), 16);
                    Ui.Size(Ui.Button(list, "前往进货", () => { CloseAllWindows(); _ctrl.GoToPlace(source); }, 17), -1, 36);
                }
                Ui.Size(Ui.Button(w.Content, "安排采购员", OpenEmployeesPanel, 18, Ui.ButtonHot), -1, 42);
            });
        }
        private void OpenRetailWholesale(Place source)
        {
            bool toBag = true;
            string dest = S.CurrentShop.Place.Id;
            OpenWindow(source.Name + " · 批发进货", 940, 860, w =>
            {
                Line(w.Content, "批发商品 · " + S.OpeningHoursText(source), 21, Ui.Accent);
                Para(w.Content, "购买后可在自己的店铺“库存”中上架，并为货架选择对应品类。生鲜和食品按批次保留到期日，过期自动清理。", 16, Ui.Dim);
                var mode = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Button(mode, "放入背包", () => { toBag = true; w.Rebuild(); }, 18, toBag ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
                Ui.Size(Ui.Button(mode, "叫货车送到店", () => { toBag = false; w.Rebuild(); }, 18, !toBag ? Ui.ButtonHot : Ui.ButtonBg, S.HasShop), -1, -1, 1);
                if (!toBag)
                {
                    foreach (var shop in S.MyShops)
                    {
                        string id = shop.Place.Id;
                        Ui.Size(Ui.Button(w.Content, S.ShopTitle(shop), () => { dest = id; w.Rebuild(); }, 17,
                            dest == id ? Ui.ButtonHot : Ui.ButtonBg), -1, 34);
                    }
                    Para(w.Content, "货车运费按路程另外结算，到货自动进入所选店的后仓。", 15, Ui.Dim);
                }
                Line(w.Content, $"现金 {S.Ledger.Balance} · 背包余量 {S.FreeKg:0.##} 千克", 17);
                var list = Ui.Scroll(w.Content, 480);
                foreach (var item in Gifts.All.Where(g => g.SoldAt == source.Business))
                {
                    var product = item;
                    string key = "retail_" + source.Id + "_" + product.Id;
                    int count = _onlineQuantities.TryGetValue(key, out int qty) ? qty : 1;
                    Line(list, product.Name + " · 批发 " + product.WholesalePrice + "/件", 18, Ui.Accent);
                    Line(list, $"每件 {product.WeightKg:0.##} 千克 · 零售参考价 {product.Price}" + (product.ShelfDays > 0 ? $" · 保鲜 {product.ShelfDays} 天" : ""), 15, Ui.Dim);
                    var row = Ui.Row(list, 38);
                    QuantityStepper(w, row, key, count, 1, 100, 1, v => _onlineQuantities[key] = (int)v);
                    Ui.Size(Ui.Button(row, (toBag ? "买入背包 · " : "购入并送店 · ") + product.WholesalePrice * count, () =>
                    { if (S.BuyRetailWholesale(source.Id, product.Id, count, toBag, dest)) w.Rebuild(); }, 17, Ui.ButtonHot,
                        S.CurrentPlace == source && S.IsPlaceOpen(source)), -1, -1, 1);
                }
            });
        }
    }
}
