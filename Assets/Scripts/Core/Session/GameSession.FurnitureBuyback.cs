using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class FurnitureBuybackLine
    {
        public string FurnitureId;
        public int Count;
        public long UnitCents;
        public string Name => FurnitureCatalog.Get(FurnitureId)?.Name ?? FurnitureId;
        public Money Amount => Money.FromCents(UnitCents) * Count;
        internal FurnitureBuybackLine Copy() => (FurnitureBuybackLine)MemberwiseClone();
    }

    [Serializable]
    public sealed class FurnitureBuybackOrder
    {
        public long Id, DueMinute, PayoutCents, FreightCents;
        public string ShopId, ShopName, MallId, MallName;
        public int Trucks;
        public double Kg;
        public bool Delivered;
        public FurnitureBuybackLine[] Lines = Array.Empty<FurnitureBuybackLine>();
        public int Count => Lines.Sum(line => line.Count);
        public Money Payout => Money.FromCents(PayoutCents);
        public Money Freight => Money.FromCents(FreightCents);
        public Money Net => Payout - Freight;

        internal FurnitureBuybackOrder Copy()
        {
            var copy = (FurnitureBuybackOrder)MemberwiseClone();
            copy.Lines = (Lines ?? Array.Empty<FurnitureBuybackLine>()).Where(line => line != null)
                .Select(line => line.Copy()).ToArray();
            return copy;
        }
    }

    public sealed partial class GameSession
    {
        public const double FurnitureBuybackRate = .40;
        private readonly List<FurnitureBuybackOrder> _furnitureBuybacks = new List<FurnitureBuybackOrder>();
        private long _nextFurnitureBuybackId = 1;
        public IReadOnlyList<FurnitureBuybackOrder> FurnitureBuybacks => _furnitureBuybacks;

        public static Money FurnitureBuybackPrice(FurnitureDef furniture) => furniture.Price.Scale(FurnitureBuybackRate);

        public FurnitureBuybackOrder QuoteFurnitureBuyback(string shopId, IReadOnlyDictionary<string, int> selection, out string blocker)
        {
            var mall = CurrentPlace;
            var shop = ShopAt(shopId);
            blocker = null;
            if (!AtMall || !IsPlaceOpen(mall))
                blocker = "请在家具城营业时办理回收。";
            else if (IsWorking || IsGameOver || Racing || CityActivityExclusive)
                blocker = "请先结束当前活动。";
            else if (shop == null)
                blocker = "请选择一家自己的门店。";
            else if (selection == null || selection.Count == 0)
                blocker = "请选择要回收的家具。";
            if (blocker != null) return null;

            var lines = new List<FurnitureBuybackLine>();
            var loads = new List<(bool rice, double unitKg, double amount)>();
            foreach (var pair in selection.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (pair.Value == 0) continue;
                var definition = FurnitureCatalog.Get(pair.Key);
                if (pair.Value < 0 || definition == null || definition.Id != pair.Key)
                { blocker = "请选择后仓中有效的家具和数量。"; return null; }
                if (shop.Locker.FurnitureCount(definition.Id) < pair.Value)
                { blocker = $"后仓的{definition.Name}数量不足，请重新选择。"; return null; }

                lines.Add(new FurnitureBuybackLine
                {
                    FurnitureId = definition.Id, Count = pair.Value, UnitCents = FurnitureBuybackPrice(definition).Cents
                });
                loads.Add((false, definition.WeightKg, pair.Value));
            }
            if (lines.Count == 0) { blocker = "请选择要回收的家具。"; return null; }

            int trucks = CarsFor(loads, free: 0);
            if (trucks < 0) { blocker = $"有家具单件超过 {CourierCapacityKg:0} 千克，面包车装不下。"; return null; }
            var route = Pathfinder.FindPath(City.Map, shop.Place.NodeId, mall.NodeId);
            if (!route.Found) { blocker = "这家门店到家具城之间没有可用的货运路线。"; return null; }

            var (fee, minutes) = CourierQuoteForDistance(route.Distance);
            Money freight = fee * trucks;
            if (!Ledger.CanAfford(freight)) blocker = $"叫车需要先支付 {freight}，现金不足。";
            return new FurnitureBuybackOrder
            {
                ShopId = shop.Place.Id, ShopName = ShopTitle(shop), MallId = mall.Id, MallName = mall.Name,
                Trucks = trucks, Kg = loads.Sum(line => line.unitKg * line.amount),
                FreightCents = freight.Cents, PayoutCents = lines.Sum(line => line.Amount.Cents),
                DueMinute = Clock.Now.TotalMinutes + minutes, Lines = lines.ToArray()
            };
        }

        public FurnitureBuybackOrder DispatchFurnitureBuyback(string shopId, IReadOnlyDictionary<string, int> selection)
        {
            var order = QuoteFurnitureBuyback(shopId, selection, out string blocker);
            if (blocker != null) { Fail(blocker); return null; }

            order.Id = _nextFurnitureBuybackId;
            string route = order.ShopName + " → " + order.MallName;
            if (!Ledger.TryDebit(Clock.Today, "运费", order.Freight, $"家具回收 · {route} · {order.Trucks} 辆",
                LedgerAttribution.ForShop(order.ShopId, $"furniture-buyback:{order.Id}:freight", "物流")))
            {
                Fail("现金不足以支付本次运费。");
                return null;
            }

            var locker = ShopAt(shopId).Locker;
            foreach (var line in order.Lines) locker.TakeFurniture(line.FurnitureId, line.Count);
            _furnitureBuybacks.Add(order);
            _nextFurnitureBuybackId++;
            Say($"已叫 {order.Trucks} 辆车从{order.ShopName}后仓取走 {order.Count} 件家具，运费 {order.Freight} 已支付。");
            return order;
        }

        private void DeliverFurnitureBuybacks(GameTimestamp now)
        {
            for (int i = 0; i < _furnitureBuybacks.Count;)
            {
                var order = _furnitureBuybacks[i];
                if (order.DueMinute > now.TotalMinutes) { i++; continue; }
                if (LifeQuietBlock(order.MallId))
                {
                    order.DueMinute = now.TotalMinutes + (420 - now.MinuteOfDay + 1440) % 1440;
                    i++;
                    continue;
                }

                if (order.Payout.IsPositive)
                    Ledger.Credit(now.Date, "家具回收", order.Payout, $"{order.ShopName} · {order.Count} 件",
                        new LedgerAttribution(LedgerBusinessKind.AssetInvestment, shopId: order.ShopId,
                            sourceEventId: $"furniture-buyback:{order.Id}:payout", transactionKind: "设施回收"));
                order.Delivered = true;
                _furnitureBuybacks.RemoveAt(i);
                RecordLifeNoise(order.MallId);
                Say($"{order.MallName}已收到 {order.Count} 件家具，回收款 {order.Payout} 已到账。");
            }
        }

        private void RestoreFurnitureBuybacks(SaveHome saved)
        {
            _furnitureBuybacks.Clear();
            foreach (var order in saved?.furnitureBuybacks ?? Array.Empty<FurnitureBuybackOrder>())
                if (order != null && !order.Delivered && order.Id > 0)
                    _furnitureBuybacks.Add(order.Copy());
            _nextFurnitureBuybackId = Math.Max(saved?.nextFurnitureBuybackId ?? 1,
                _furnitureBuybacks.Select(order => order.Id).DefaultIfEmpty(0).Max() + 1);
        }
    }
}
