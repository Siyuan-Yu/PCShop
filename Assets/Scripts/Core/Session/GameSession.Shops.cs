using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 同时经营几家自营电脑店。每家店的货、货架、员工、升级、补货目标各管各的，知名度和现金共用。
    /// 会话里 PlayerShop / Lease / PlayerShopPlace / Employee 等指的都是“当前这家店”：人进了哪家店就切到哪家，
    /// 出门后保持最后进过的那家。不在当前的店照常营业（有员工值守才开门）。
    /// </summary>
    public sealed partial class GameSession
    {
        private static readonly string[] Digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        /// <summary>第 n 家店的中文序号（2 → 二，10 → 十，12 → 十二，23 → 二十三）。</summary>
        private static string ChineseNumber(int n)
        {
            if (n < 10) return Digits[n];
            if (n < 100) return (n / 10 == 1 ? "" : Digits[n / 10]) + "十" + (n % 10 == 0 ? "" : Digits[n % 10]);
            return n.ToString();
        }

        /// <summary>任何一家自营店来了顾客（街上让路人走进那家店的门）。</summary>
        public event Action<Place, ShopCustomer> ShopCustomerArrived;

        /// <summary>已经租下的自营店，第一家是主店。</summary>
        public IReadOnlyList<OwnedShop> MyShops => _shops.Where(s => s.Rented).ToList();

        public int MyShopCount => _shops.Count(s => s.Rented);

        /// <summary>当前这家店（还没租铺子时是一个占位，Rented 为 false）。</summary>
        public OwnedShop CurrentShop => _focus;

        public OwnedShop MainShop => _shops[0];

        public OwnedShop ShopAt(Place p) => p == null ? null : _shops.Find(s => s.Rented && s.Place == p);

        public OwnedShop ShopAt(string placeId) => _shops.Find(s => s.Rented && s.Place.Id == placeId);

        /// <summary>主店叫“我的电脑店”，之后开的叫“我的电脑店·二店”……</summary>
        public string ShopTitle(OwnedShop s)
        {
            int i = _shops.IndexOf(s);
            return i <= 0 ? MyShopName : $"{MyShopName}·{ChineseNumber(i + 1)}店";
        }

        /// <summary>开了不止一家时，提示里带上是哪家店。</summary>
        private string ShopTag => MyShopCount > 1 ? $"（{ShopTitle(_focus)}）" : "";

        private ShopSimulation CreateShopSim(OwnedShop shop)
        {
            var sim = new ShopSimulation(shop.Place.Id, shop.Place.Size, Clock, Ledger, Market, Random)
            {
                LocationFactor = ComputeLocationFactor(City, shop.Place),
                EmployeeOnDuty = t => (shop.Employee != null && shop.Employee.OnDuty(t)) || (shop.Assembler != null && shop.Assembler.OnDuty(t)),
            };
            sim.WorkMinute = now => WithShop(shop, () => UpdateEmployeeWork(now));
            sim.EmployeeBusy = () => shop.EmployeeBuilding;
            sim.DayClosed += r =>
            {
                if (shop.Rented) Notice?.Invoke((MyShopCount > 1 ? ShopTitle(shop) + " " : "") + DayReportText(r));
            };
            sim.CustomerArrived += c => ShopCustomerArrived?.Invoke(shop.Place, c);
            sim.CustomerLeft += c => WithShop(shop, () => OnShopCustomerLeft(c));
            sim.OrderCompleted += o => { Flags.Add("tutorial:build"); ContractOrderResult(o, true); };
            sim.OrderCancelled += o => ContractOrderResult(o, false);
            sim.OrderExpired += o => ContractOrderResult(o, false);
            sim.OrderPlaced += o => Notice?.Invoke($"{(MyShopCount > 1 ? ShopTitle(shop) : "")}新装机订单：{PartCatalog.TierName(o.Tier)}整机 {o.Price}，两天内交付。");
            sim.OrderExpired += o => Popup?.Invoke("订单取消", $"{(MyShopCount > 1 ? ShopTitle(shop) : "")}{PartCatalog.TierName(o.Tier)}整机订单超时，顾客取消了订单，知名度下降。");
            if (_shops.Count > 0 && _shops[0] != shop && _shops[0].Sim != null) sim.ShareReputationWith(_shops[0].Sim);
            return sim;
        }

        /// <summary>临时把“当前店”切到 s 做一件事（后台店的员工干活、自动补货、到货入库），做完切回来。</summary>
        private void WithShop(OwnedShop s, Action a)
        {
            if (s == null || s == _focus)
            {
                a();
                return;
            }
            OwnedShop keep = _focus;
            _focus = s;
            try { a(); }
            finally { _focus = keep; }
        }

        /// <summary>切换当前店（进了另一家自己的店）：店内场景按这家重建。</summary>
        private void SetFocus(OwnedShop s)
        {
            if (s == null || s == _focus) return;
            _focus = s;
            _layoutUndo.Clear();
            _layoutRedo.Clear();
            ApplyUpgrades();
            ShopExpanded?.Invoke(s.Sim.Size);
        }

        /// <summary>送到“店里”的货该进哪家店的库存：下单时的那家；那家退租了就进主店。</summary>
        private OwnedShop DeliveryShop(string placeId) => (placeId != null ? ShopAt(placeId) : null) ?? (MainShop.Rented ? MainShop : _focus);

        /// <summary>已经有店再签一间：开一家新店（不搬旧店）。新店是毛坯，变成当前店。</summary>
        private void OpenNewShop(Place place, Money rent, Money deposit)
        {
            var shop = new OwnedShop { Place = place };
            shop.Lease = new ShopLease { PlaceId = place.Id, Rent = rent, Deposit = deposit, StartDay = Clock.Today.TotalDays };
            shop.Sim = CreateShopSim(shop);
            shop.Sim.Markup = PlayerShop.Markup;
            _shops.Add(shop);
            _vacant.Remove(place.Id);
            _listings.RemoveAll(x => x.PlaceId == place.Id);
            SetFocus(shop);
        }

        /// <summary>为什么这家店现在退不了租；能退返回 null。退租在店里办，货要先叫货车搬走。</summary>
        public string CloseShopBlocker(OwnedShop s)
        {
            if (s == null || !s.Rented) return "这不是你的店";
            if (MyShopCount <= 1) return "这是你唯一的店。想换地方：先去中介再签一间，叫货车把货搬过去，再回这里退租";
            if (CurrentPlace != s.Place) return "要在这家店里办退租";
            if (s.Sim.Orders.Count > 0) return "还有没交付的装机单";
            if (s.Sim.DisplayPcs.Count > 0) return "展示台上还摆着整机，先拆回库存";
            if (!s.Sim.Stock.IsEmpty) return "店里还有货，先打电话叫货车搬走";
            if (_couriers.Any(r => r.ToShop && r.ShopId == s.Place.Id)) return "还有车在往这家店送货";
            if (Online.Pending.Any(o => !o.ToHome && o.ShopId == s.Place.Id) || _directShipments.Any(x => x.ShopId == s.Place.Id)) return "还有货在往这家店寄";
            return null;
        }

        /// <summary>
        /// 在店里退掉这家店：摆的家具收回“我的”，这家店的员工辞退，押金退还，人走到门口，铺子重新挂牌。
        /// 退的是主店时，下一家顶上当主店。
        /// </summary>
        public bool CloseShop(string placeId)
        {
            OwnedShop s = ShopAt(placeId);
            string why = CloseShopBlocker(s);
            if (why != null) return Fail(why + "。");
            string title = ShopTitle(s);
            Leave();
            if (CurrentPlace == s.Place) return false;
            double reputation = MainShop.Sim.Reputation;
            s.Sim.ClearCustomers();
            foreach (PlacedFurniture f in s.Layout) AddFurniture(f.Id, 1);
            if (s.Lease.Deposit.IsPositive) Ledger.Credit(Clock.Today, "押金", s.Lease.Deposit, $"退还{title}押金");
            bool hadStaff = s.Employee != null || s.Assembler != null;
            Money rent = s.Lease.Rent;
            bool wasMain = s == MainShop;
            _shops.Remove(s);
            s.Sim.Dispose();
            if (wasMain)
            {
                ShopSimulation main = MainShop.Sim;
                main.ShareReputationWith(main);
                main.Reputation = reputation;
                foreach (OwnedShop o in _shops.Skip(1)) o.Sim.ShareReputationWith(main);
            }
            if (_focus == s) SetFocus(MainShop);
            _vacant.Add(placeId);
            if (PropertyAt(placeId) == null) AddListing(City.GetPlace(placeId), Clock.Today.TotalDays, Clock.Today.TotalDays + 45, rent);
            WithShop(MainShop, ApplyUpgrades);
            Show("退租了", $"{title}（{s.Place.Name}）退租了，押金 {s.Lease.Deposit} 退回，店里摆的家具收回“我的”" + (hadStaff ? "，这家店的员工也辞退了" : "")
                + (wasMain ? $"。现在{ShopTitle(MainShop)}（{MainShop.Place.Name}）是主店。" : "。"));
            return true;
        }

        // ---------- 存档 ----------

        private SaveShop SaveShopOf(OwnedShop s)
        {
            ShopSimulation sim = s.Sim;
            return new SaveShop
            {
                place = s.Place.Id,
                rentCents = s.Lease.Rent.Cents,
                depositCents = s.Lease.Deposit.Cents,
                startDay = s.Lease.StartDay,
                stock = Lines(sim.Stock),
                goodsIds = sim.Stock.GoodsLines().Select(kv => kv.Key).ToArray(),
                goodsCounts = sim.Stock.GoodsLines().Select(kv => kv.Value).ToArray(),
                markup = sim.Markup,
                openMinute = sim.OpenMinute,
                closeMinute = sim.CloseMinute,
                orders = sim.Orders.Select(ToSave).ToArray(),
                nextOrderId = sim.NextOrderId,
                displayPcs = sim.DisplayPcs.Concat(sim.Queue.Where(c => c.DisplayTier >= 0).Select(c => c.DisplayTier)).ToArray(),
                partMarkupIds = sim.PartMarkups.Keys.ToArray(),
                partMarkupValues = sim.PartMarkups.Values.ToArray(),
                hasEmployee = s.Employee != null,
                employee = s.Employee != null ? ToSave(s.Employee) : null,
                hasAssembler = s.Assembler != null,
                assembler = s.Assembler != null ? ToSave(s.Assembler) : null,
                employeeTrained = s.EmployeeTrained,
                autoBuild = s.AutoBuild,
                layout = ToSave(s.Layout),
                upgrades = s.Upgrades.ToArray(),
                restockIds = s.Restock.Keys.ToArray(),
                restockTargets = s.Restock.Values.ToArray(),
                renovationDay = s.RenovationDay,
            };
        }

        private void RestoreShop(SaveShop d)
        {
            if (d == null || !City.HasPlace(d.place) || _shops.Any(x => x.Place.Id == d.place)) return;
            Place place = City.GetPlace(d.place);
            var shop = new OwnedShop { Place = place };
            shop.Lease = new ShopLease { PlaceId = place.Id, Rent = Money.FromCents(d.rentCents), Deposit = Money.FromCents(d.depositCents), StartDay = d.startDay };
            shop.Sim = CreateShopSim(shop);
            ShopSimulation sim = shop.Sim;
            Fill(sim.Stock, d.stock);
            string[] goods = d.goodsIds ?? Array.Empty<string>();
            for (int i = 0; i < goods.Length && d.goodsCounts != null && i < d.goodsCounts.Length; i++) sim.Stock.ForceAddGoods(goods[i], d.goodsCounts[i]);
            sim.Markup = d.markup;
            if (d.closeMinute > 0) sim.SetHours(d.openMinute, d.closeMinute);
            sim.RestoreOrders((d.orders ?? Array.Empty<SaveOrder>()).Select(FromSave), d.nextOrderId);
            sim.RestoreDisplay(d.displayPcs);
            string[] mids = d.partMarkupIds ?? Array.Empty<string>();
            double[] mvals = d.partMarkupValues ?? Array.Empty<double>();
            var marks = new List<KeyValuePair<string, double>>();
            for (int i = 0; i < mids.Length && i < mvals.Length; i++) marks.Add(new KeyValuePair<string, double>(mids[i], mvals[i]));
            sim.RestorePartMarkups(marks);
            shop.Employee = d.hasEmployee && d.employee != null ? FromSave(d.employee) : null;
            if (shop.Employee != null) sim.Settings.EmployeeCheckoutMinutes = shop.Employee.CheckoutMinutes;
            shop.Assembler = d.hasAssembler && d.assembler != null ? FromSave(d.assembler) : null;
            shop.EmployeeTrained = d.employeeTrained;
            shop.AutoBuild = d.autoBuild;
            shop.Layout.AddRange(FromSave(d.layout));
            foreach (string id in d.upgrades ?? Array.Empty<string>())
                if (ShopUpgrades.Exists(id)) shop.Upgrades.Add(id);
            string[] rids = d.restockIds ?? Array.Empty<string>();
            int[] rt = d.restockTargets ?? Array.Empty<int>();
            for (int i = 0; i < rids.Length && i < rt.Length; i++)
                if (PartCatalog.Get(rids[i]) != null && rt[i] > 0) shop.Restock[rids[i]] = Math.Min(RestockMaxTarget, rt[i]);
            shop.RenovationDay = d.renovationDay;
            sim.ForcedClosed = shop.RenovationDay == Clock.Today.TotalDays;
            _shops.Add(shop);
            _vacant.Remove(place.Id);
            WithShop(shop, ApplyUpgrades);
        }

        private static SaveOrder ToSave(BuildOrder o) => new SaveOrder
        {
            id = o.Id, contractId = o.ContractId, tier = o.Tier, priceCents = o.Price.Cents, placedAt = o.PlacedAt.TotalMinutes,
            dueAt = o.DueAt.TotalMinutes, partsReserved = o.PartsReserved, mistakes = o.Mistakes, employeeMinutesLeft = o.EmployeeMinutesLeft,
        };

        private static BuildOrder FromSave(SaveOrder o) =>
            new BuildOrder(o.id, o.tier, Money.FromCents(o.priceCents), new GameTimestamp(o.placedAt), new GameTimestamp(o.dueAt))
            {
                ContractId = o.contractId, PartsReserved = o.partsReserved, Mistakes = Math.Max(0, o.mistakes), EmployeeMinutesLeft = Math.Max(0, o.employeeMinutesLeft),
            };
    }
}
