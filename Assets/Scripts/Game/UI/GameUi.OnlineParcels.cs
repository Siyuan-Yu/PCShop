using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 网购的家具 / 店里摆设 / 衣服 / 礼品 / 大米：和配件一样，左边挑东西、右边是待下单侧栏，
    /// 点一次“下单”把侧栏里的东西一起买下。
    /// </summary>
    public sealed partial class GameUi
    {
        private readonly Dictionary<string, int> _parcelCart = new Dictionary<string, int>();

        private sealed class ParcelEntry
        {
            public string Key, Id, Name, Detail, Have, Blocked;
            public ParcelKind Kind;
            public double Kg;
            public Money Price;
            public bool Single;
        }

        private ParcelEntry[] OnlineParcelEntries(int tab)
        {
            switch (tab)
            {
                case 0:
                case 1:
                    return FurnitureCatalog.Purchasable(tab == 1).Select(f => new ParcelEntry
                    {
                        Key = "furn:" + f.Id, Id = f.Id, Kind = ParcelKind.Furniture, Name = f.Name, Detail = f.Description, Kg = f.WeightKg,
                        Price = S.OnlinePrice(ParcelKind.Furniture, f.Id), Have = S.FurnitureInStock(f.Id) > 0 ? $"背包 ×{S.FurnitureInStock(f.Id)}" : "",
                    }).ToArray();
                case 2:
                    return Outfits.All.Where(x => x.Id != Outfits.Default).Select(o => new ParcelEntry
                    {
                        Key = "outfit:" + o.Id, Id = o.Id, Kind = ParcelKind.Outfit, Name = o.Name, Single = true,
                        Detail = $"魅力 +{o.CharmBonus} · {o.Description}", Price = S.OnlinePrice(ParcelKind.Outfit, o.Id),
                        Blocked = S.OwnedOutfits.Contains(o.Id) ? "已有" : S.Parcels.Any(p => p.Kind == ParcelKind.Outfit && p.Id == o.Id) ? "在路上" : null,
                    }).ToArray();
                case 3:
                    return Gifts.All.Where(g => !RetailProducts.IsFood(g) && !g.LimitedEdition && !ClothingProducts.IsClothing(g.Id)).Select(g => new ParcelEntry
                    {
                        Key = "gift:" + g.Id, Id = g.Id, Kind = ParcelKind.Gift, Name = g.Name, Detail = g.Description, Kg = g.WeightKg,
                        Price = S.OnlinePrice(ParcelKind.Gift, g.Id), Have = S.Backpack.GiftCount(g.Id) > 0 ? $"背包 ×{S.Backpack.GiftCount(g.Id)}" : "",
                    }).ToArray();
                default:
                    return new[]
                    {
                        new ParcelEntry
                        {
                            Key = "rice", Id = "rice", Kind = ParcelKind.Rice, Name = "大米", Kg = GameSession.RiceBagKg,
                            Detail = "也可以去农贸市场按千克买，更便宜。", Price = S.OnlinePrice(ParcelKind.Rice, "rice"),
                        },
                    };
            }
        }

        private void BuildOnlineParcels(Window w, OwnedShop destination)
        {
            var entries = OnlineParcelEntries(_onlineTab);
            // 已经有了 / 在路上的衣服不能再买：把它们从待下单里清掉。
            foreach (var blocked in entries.Where(e => e.Blocked != null)) _parcelCart.Remove(blocked.Key);
            BuildStoreSplit(w.Content, out var goods, out var side);
            var splitSize = goods.parent.GetComponent<LayoutElement>();
            splitSize.flexibleHeight = 0; splitSize.minHeight = splitSize.preferredHeight = 640;

            Ui.Size(Ui.Label(goods, $"现金 {S.Ledger.Balance} · 家具和礼品享门店价 85 折" + (_onlineTab == 3 ? "　" + (destination == null ? "送到家里的礼品会放进背包" : "礼品送达后放进所选门店后仓") : ""), 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 24);
            var list = GoodsList(goods, 200);
            int NumberOf(ParcelEntry e) => _parcelCart.TryGetValue(e.Key, out int n) ? n : 0;
            foreach (var entry in entries)
            {
                var e = entry;
                int n = NumberOf(e);
                var row = Ui.Row(list, 62, 10);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                if (n > 0) row.gameObject.AddComponent<Image>().color = Ui.FlatRow;
                CompactProduct(row, e.Name, e.Kg, e.Detail, false, e.Single ? "穿戴物品，不计背包负重" : null);
                Ui.Size(Ui.Label(row, e.Price.ToString(), 18, TextAnchor.MiddleLeft, Ui.Good), 100);
                if (e.Single)
                {
                    if (e.Blocked != null) Ui.Size(Ui.Label(row, e.Blocked, 16, TextAnchor.MiddleCenter, Ui.Dim), 184);
                    else Ui.Size(Ui.Button(row, n > 0 ? "已选 ✓" : "加入待下单", () =>
                    {
                        if (n > 0) _parcelCart.Remove(e.Key); else _parcelCart[e.Key] = 1;
                        w.Rebuild();
                    }, 16, n > 0 ? Ui.ButtonHot : Ui.ButtonBg), 184, 34);
                }
                else
                {
                    Ui.Size(Ui.Label(row, e.Have ?? "", 14, TextAnchor.MiddleLeft, Ui.Dim), 76);
                    QuantityStepper(w, row, "parcel_" + e.Key, n, 0, 100, 1, value => { if (value <= 0) _parcelCart.Remove(e.Key); else _parcelCart[e.Key] = (int)value; });
                }
            }

            var picked = entries.Where(e => NumberOf(e) > 0).ToArray();
            Money total = Money.Zero;
            foreach (var e in picked) total += e.Price * NumberOf(e);
            var head = Ui.Row(side, 34, 8);
            Ui.Size(Ui.Label(head, picked.Length == 0 ? "<b>待下单</b>　还是空的" : $"<b>待下单</b>　{picked.Length} 样", 20, TextAnchor.MiddleLeft, Ui.Accent), -1, -1, 1);
            if (picked.Length > 0) Ui.Size(Ui.Button(head, "清空", () => { foreach (var e in entries) _parcelCart.Remove(e.Key); w.Rebuild(); }, 15, Ui.ButtonBg), 64, 30);
            var picks = GoodsList(side, 120);
            if (picked.Length == 0) Para(picks, "在左边商品点“＋”选数量。\n下单后几天送到所选地点。", 16, Ui.Dim);
            foreach (var item in picked)
            {
                var e = item; int count = NumberOf(e);
                var row = Ui.Row(picks, 46, 6);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                var label = Ui.Label(row, $"{e.Name} <color=#E8BE62>×{count}</color>\n<size=14><color=#AAB4BB>{e.Price * count}</color></size>", 17, TextAnchor.MiddleLeft);
                label.verticalOverflow = VerticalWrapMode.Truncate;
                Ui.Size(label, -1, -1, 1);
                Ui.Size(Ui.Button(row, "移除", () => { _parcelCart.Remove(e.Key); w.Rebuild(); }, 15, Ui.ButtonBg), 58, 34);
            }
            Ui.Size(Ui.Label(side, $"送到 {S.CourierDestName(destination)} · {GameSession.DeliveryDays} 天后送达", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 28);
            Ui.Size(Ui.Label(side, $"合计 <b><color=#E8C77A>{total}</color></b>", 28, TextAnchor.MiddleRight), -1, 40);
            var order = Ui.Button(side, "下单", () =>
            {
                foreach (var e in picked)
                {
                    if (!S.OrderOnline(e.Kind, e.Id, e.Single ? 1 : NumberOf(e), destination)) break;
                    _parcelCart.Remove(e.Key);
                }
                w.Rebuild();
            }, 22, Ui.ButtonHot, picked.Length > 0);
            order.name = "OnlineOrder";
            Ui.Size(order, -1, 54);

            if (S.Parcels.Count > 0)
                Line(w.Content, "在路上：" + string.Join("、", S.Parcels.Select(p => $"{ParcelName(p)}（{Mathf.Max(0, p.DueDay - S.Clock.Today.TotalDays)} 天 · {S.ParcelDestinationName(p)}）")), 15, Ui.Dim);
        }
    }
}
