using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>
    /// 数值校准用的"普通玩家"机器人：全程守店，靠 1788 网购补货，按固定顺序买升级，遇到事件选第一个可选项。
    /// 它不会跑档口、不会砍价、不会抓行情，所以结果大致是"一个不怎么动脑的玩家"能达到的水平——
    /// 用来给长线目标定门槛，不代表最优解。
    /// </summary>
    internal sealed class EconomyBot
    {
        public sealed class Snapshot
        {
            public int Day;
            public long NetWorth;
            public long Cash;
            public double Reputation;
        }

        private static readonly string[] UpgradeOrder =
        {
            ShopUpgrades.Shelf1, ShopUpgrades.Sign, ShopUpgrades.Phone, ShopUpgrades.Qr,
            ShopUpgrades.AirCon, ShopUpgrades.Cctv, ShopUpgrades.Shelf2, ShopUpgrades.SelfCheckout, ShopUpgrades.Shelf3,
        };

        private readonly GameSession _s;
        private readonly bool _events;
        private readonly bool _lazy;

        public int GameOverDay = -1;
        public string GameOverReason;
        public readonly List<Snapshot> Snapshots = new List<Snapshot>();

        public EconomyBot(ulong seed, bool events = true, bool lazy = false)
        {
            _lazy = lazy;
            string json = System.IO.File.ReadAllText("Assets/Resources/Data/city_layout.json");
            CityLayout city = CityLayout.FromData(UnityEngine.JsonUtility.FromJson<CityLayoutData>(json));
            _s = new GameSession(city, seed, Money.FromYuan(5000), GameTimestamp.From(new GameDate(0), GameSession.WakeUpMinute));
            _events = events;
            _s.EventsEnabled = events;
            _s.EventRaised += def => ResolveEvent();
        }

        public GameSession Session => _s;

        private void ResolveEvent()
        {
            // 事件在 AdvanceClock 内部弹出；机器人立刻选第一个可选项
            EventDef def = _s.PendingEvent;
            if (def == null) return;
            for (int i = 0; i < def.Choices.Count; i++)
            {
                if (!_s.IsChoiceEnabled(def.Choices[i])) continue;
                _s.ResolveEvent(i);
                return;
            }
        }

        private void Go(Place p, int minutes)
        {
            _s.Leave();
            _s.ArriveAtNode(p.NodeId);
            _s.Enter(p);
            _s.AdvanceClock(minutes);
        }

        public void Run(int days, params int[] snapshotDays)
        {
            var marks = new HashSet<int>(snapshotDays);
            for (int day = 0; day < days && !_s.IsGameOver; day++)
            {
                PlayDay();
                int today = _s.Clock.Today.TotalDays;
                if (marks.Contains(today)) Record(today);
                if (_s.IsGameOver)
                {
                    GameOverDay = _s.Clock.Today.TotalDays;
                    GameOverReason = _s.GameOverReason;
                }
            }
        }

        private void Record(int day)
        {
            Snapshots.Add(new Snapshot
            {
                Day = day,
                NetWorth = _s.NetWorth().Cents / 100,
                Cash = _s.Ledger.Balance.Cents / 100,
                Reputation = _s.PlayerShop.Reputation,
            });
        }

        private void PlayDay()
        {
            // 早上 8:00 在家 → 走到店
            if (_s.CurrentPlace != _s.Home) Go(_s.Home, 0);
            Go(_s.PlayerShopPlace, 10);

            ManageShop();
            while (_s.Clock.MinuteOfDay < 21 * 60 + 30 && !_s.IsGameOver)
            {
                _s.AdvanceClock(1);
                if (_s.PendingEvent != null) ResolveEvent();
                DrainQueue();
                if (_s.Clock.MinuteOfDay % 60 == 0) HandleOrders();
            }
            if (_s.IsGameOver) return;

            // 回家睡觉
            Go(_s.Home, 10);
            _s.Sleep();
            if (_s.PendingEvent != null) ResolveEvent();
        }

        private void DrainQueue()
        {
            var shop = _s.PlayerShop;
            for (int guard = 0; guard < 20 && shop.Queue.Count > 0; guard++)
            {
                int id = shop.Queue[0].Id;
                if (!_s.Checkout(id)) break;
                _s.AdvanceClock(shop.Settings.CheckoutMinutesCash);
            }
        }

        private void HandleOrders()
        {
            foreach (BuildOrder o in _s.PlayerShop.Orders.ToList())
            {
                if (_s.PlayerShop.MissingParts(o).Count > 0) continue;
                if (_s.CompleteBuild(o.Id, 0)) _s.AdvanceClock(15);
            }
        }

        private void ManageShop()
        {
            int today = _s.Clock.Today.TotalDays;
            _s.SetMarkup(1.15);

            // 懒玩家：不招人、不送礼、不扩建，只偶尔补货
            if (!_lazy) Invest(today);

            // 招人：第 3 天贴启事，来了就雇便宜的
            if (!_lazy && _s.Employee == null && !_s.EverHired)
            {
                if (_s.Candidates.Count > 0) _s.Hire(0);
                else if (today >= 3 && _s.JobAdDay < 0) _s.PostJobAd();
            }

            // 升级：按顺序买第一个买得起（留 ¥1500 周转）的
            foreach (string id in UpgradeOrder)
            {
                if (_lazy && id != ShopUpgrades.Shelf1 && id != ShopUpgrades.Sign) continue;
                if (_s.HasUpgrade(id)) continue;
                ShopUpgradeDef def = ShopUpgrades.All.First(u => u.Id == id);
                if (_s.UpgradeBlocker(def) != null) continue;
                if (_s.Ledger.Balance < def.Price + Money.FromYuan(1500)) break;
                _s.BuyUpgrade(id);
                break;
            }

            // 补货：每种配件保持目标库存
            if (_lazy && today % 4 != 0) return;
            int target = (_lazy ? 1 : 2) + (_s.HasUpgrade(ShopUpgrades.Shelf1) ? 1 : 0) + (_s.HasUpgrade(ShopUpgrades.Shelf2) ? 1 : 0) + (_s.HasUpgrade(ShopUpgrades.Shelf3) ? 1 : 0);
            var pending = new Dictionary<string, int>();
            foreach (var o in _s.Online.Pending)
                foreach (var l in o.Lines) pending[l.PartId] = (pending.TryGetValue(l.PartId, out int n) ? n : 0) + l.Count;

            var lines = new List<(PartDef, int)>();
            double free = _s.PlayerShop.Stock.FreeSpace - _s.Online.PendingSpace();
            int toMonth = 30 - (_s.Clock.Today.TotalDays % 30);
            Money budget = _s.Ledger.Balance - Money.FromYuan(1000 + (toMonth <= 3 ? 2500 : 0));
            foreach (PartDef p in PartCatalog.All)
            {
                int have = _s.PlayerShop.Stock.CountAll(p) + (pending.TryGetValue(p.Id, out int pn) ? pn : 0);
                int need = target - have;
                if (need <= 0) continue;
                for (int i = 0; i < need; i++)
                {
                    double space = PCShopSell.Core.Inventory.Stock.SpaceOf(p, true, 1);
                    Money price = _s.OnlinePrice(PCShopSell.Core.Trade.Online1788.GetStore("s_mid"), p);
                    if (space > free || price > budget) break;
                    free -= space;
                    budget -= price;
                    AddLine(lines, p);
                }
            }
            if (lines.Count > 0) _s.PlaceOnlineOrder("s_mid", lines);
        }

        private void Invest(int today)
        {
            // 换大铺子：中介那里有比现在大的、签得起并且还能留 ¥30,000 周转就搬
            Place agency = _s.City.FirstOfKind(PlaceKind.Agency);
            ShopListing bigger = agency == null ? null : _s.Listings
                .Where(l => l.Size > _s.PlayerShop.Size && _s.LeaseBlocker(l) == null && _s.Ledger.Balance >= _s.LeaseCost(l) + Money.FromYuan(30000))
                .OrderByDescending(l => l.Size).FirstOrDefault();
            if (bigger != null)
            {
                Go(agency, 20);
                _s.SignLease(bigger.PlaceId);
                Go(_s.PlayerShopPlace, 10);
            }

            // 每隔几天给认识的人送份礼（钱富余时）
            if (_s.Ledger.Balance > Money.FromYuan(8000))
            {
                foreach (NpcDef n in Npcs.All)
                {
                    string why = _s.GiftBlocker(n.Id);
                    if (why != null && why.StartsWith("没带") && _s.Ledger.TryDebit(_s.Clock.Today, "人情", n.GiftPrice, n.GiftName))
                    {
                        _s.Backpack.AddGift(n.GiftItemId, 1);
                        why = _s.GiftBlocker(n.Id);
                    }
                    if (why == null) _s.GiveGift(n.Id);
                }
            }
        }

        private static void AddLine(List<(PartDef, int)> lines, PartDef p)
        {
            int i = lines.FindIndex(l => l.Item1 == p);
            if (i < 0) lines.Add((p, 1));
            else lines[i] = (p, lines[i].Item2 + 1);
        }
    }
}
