using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class OldMarketState
    {
        public int StockDay = -1, Remaining;
        public string Pending, GoodsId, Message;
        public int Choice;
        public long LastMinute;
        public string[] Bought = Array.Empty<string>(), Inspected = Array.Empty<string>();
        public OldMarketState Copy() { var s = (OldMarketState)MemberwiseClone(); s.Bought = (Bought ?? Array.Empty<string>()).ToArray(); s.Inspected = (Inspected ?? Array.Empty<string>()).ToArray(); return s; }
    }
    public sealed partial class GameSession
    {
        public const string OldMarketVenueId = "life_oldmarket";
        private OldMarketState _oldMarket = new OldMarketState();
        public OldMarketState OldMarketProgress => _oldMarket.Copy();
        public bool AtOldMarket => CurrentPlace?.Id == OldMarketVenueId && ClosedReason(CurrentPlace) == null;
        public int OldMarketRemaining(string id) => OldMarketCatalog.Get(id) == null ? 0 : _oldMarket.StockDay != Clock.Today.TotalDays || !_oldMarket.Bought.Contains(id) ? 1 : 0;
        public bool OldMarketInspected(string id) => _oldMarket.Inspected.Contains(id);
        public Money OldMarketPrice(string id) { var g = OldMarketCatalog.Get(id); return g == null ? Money.Zero : Gifts.Get(OldMarketCatalog.ItemId(g)).Price; }
        public bool InspectOldMarketGoods(string id)
        {
            if (!AtOldMarket || OldMarketCatalog.Get(id) == null || _oldMarket.Pending != null) return Fail("请到营业中的旧物市场，先完成当前操作。");
            _oldMarket.Pending = "inspect"; _oldMarket.GoodsId = id; _oldMarket.Remaining = 5; _oldMarket.LastMinute = Clock.Now.TotalMinutes;
            _oldMarket.Message = "现场查看实物五分钟；收起面板后时间继续。"; return true;
        }
        public bool BuyOldMarketGoods(string id)
        {
            var g = OldMarketCatalog.Get(id);
            if (!AtOldMarket || g == null || _oldMarket.Pending != null || OldMarketRemaining(id) == 0) return Fail("请到营业中的旧物市场购买，或该件今日已售出。");
            if (!CanCarry(g.Weight)) return false;
            if (!Ledger.TryDebit(Clock.Today, "旧物市场购买", OldMarketPrice(id), g.Name)) return Fail("钱不够买这件旧物。");
            if (_oldMarket.StockDay != Clock.Today.TotalDays) { _oldMarket.StockDay = Clock.Today.TotalDays; _oldMarket.Bought = Array.Empty<string>(); }
            _oldMarket.Bought = _oldMarket.Bought.Concat(new[] { id }).ToArray(); Backpack.AddGift(OldMarketCatalog.ItemId(g), 1); ApplyCargoLimit();
            Say("买下了" + g.Name + "。"); return true;
        }
        public bool AskOldMarketRepair(string id)
        {
            var g = OldMarketCatalog.Get(id);
            if (!AtOldMarket || g?.Faulty != true || _oldMarket.Pending != null
                || Backpack.GiftCount(OldMarketCatalog.ItemId(g)) < 1) return Fail("请带着待修旧物到市场。");
            if (!Ledger.TryDebit(Clock.Today, "旧物修理", Money.FromYuan(g.RepairFee), g.Name)) return Fail("修理费不够。");
            Backpack.TakeGift(OldMarketCatalog.ItemId(g)); ApplyCargoLimit();
            _oldMarket.Pending = "repair"; _oldMarket.GoodsId = id; _oldMarket.Choice = g.CorrectRepair;
            _oldMarket.Remaining = 15; _oldMarket.LastMinute = Clock.Now.TotalMinutes;
            _oldMarket.Message = "师傅正在修" + g.Name + "，需要15分钟。"; Say(_oldMarket.Message); return true;
        }
        public bool RepairOldMarketGoods(string id, int choice)
        {
            var g = OldMarketCatalog.Get(id);
            if (!AtOldMarket || g?.Faulty != true || _oldMarket.Pending != null || choice < 1 || choice > 3
                || Backpack.GiftCount(OldMarketCatalog.ItemId(g)) < 1) return Fail("请带着这件待修旧物到市场修理台。");
            if (!OldMarketInspected(id)) return Fail("先花五分钟检测，查看具体故障，再选择处理方式。");
            if (!Ledger.TryDebit(Clock.Today, "旧物维修耗材", Money.FromYuan(g.RepairFee), g.Name + " · " + OldMarketCatalog.RepairChoices[choice - 1])) return Fail("现金不足以支付本次耗材。");
            // Hold the actual faulty item at the workbench, preventing gifts/sales from duplicating it during repair.
            Backpack.TakeGift(OldMarketCatalog.ItemId(g)); ApplyCargoLimit();
            _oldMarket.Pending = "repair"; _oldMarket.GoodsId = id; _oldMarket.Choice = choice; _oldMarket.Remaining = 10; _oldMarket.LastMinute = Clock.Now.TotalMinutes;
            _oldMarket.Message = "实物已放在市场修理台，耗材已支付；在场操作十分钟。离开时暂停，回来可继续。"; return true;
        }
        public Money OldMarketBuybackPrice(string itemId) => Gifts.Get(itemId) is GiftDef g && itemId.StartsWith("oldmarket:", StringComparison.Ordinal) ? g.Price.Scale(.55) : Money.Zero;
        public bool SellOldMarketGoods(string itemId)
        {
            var price = OldMarketBuybackPrice(itemId);
            if (!AtOldMarket || !price.IsPositive || _oldMarket.Pending != null || !Backpack.TakeGift(itemId)) return false;
            Ledger.Credit(Clock.Today, "旧物市场转出", price, Gifts.Get(itemId).Name); ApplyCargoLimit(); Say("卖掉了" + Gifts.Get(itemId).Name + "，收到" + price + "。"); return true;
        }
        private void TickOldMarket(long now)
        {
            if (_oldMarket.Pending == null) { _oldMarket.LastMinute = now; return; }
            int elapsed = (int)Math.Max(0, Math.Min(1, now - _oldMarket.LastMinute)); _oldMarket.LastMinute = now;
            if (!AtOldMarket) return;
            _oldMarket.Remaining -= elapsed; if (_oldMarket.Remaining > 0) return;
            var g = OldMarketCatalog.Get(_oldMarket.GoodsId); if (g == null) { _oldMarket.Pending = null; return; }
            string task = _oldMarket.Pending; _oldMarket.Pending = null;
            if (task == "inspect")
            {
                _oldMarket.Inspected = _oldMarket.Inspected.Union(new[] { g.Id }).ToArray();
                _oldMarket.Message = g.Name + "：" + (g.Symptom ?? g.Description);
            }
            else
            {
                bool right = _oldMarket.Choice == g.CorrectRepair;
                Backpack.AddGift(OldMarketCatalog.ItemId(g, right), 1); ApplyCargoLimit();
                if (!right) Player.SpendEnergy(3);
                _oldMarket.Message = right ? g.Name + "修好了，已放回背包。" : "没修好，精力 −3，旧物已放回背包。";
            }
            Say(_oldMarket.Message);
        }
        internal bool CanOfferOldMarketEvent(int n)
        {
            string person = n == 71 ? "resident_guwen" : n == 72 ? "resident_leiming" : null;
            return person != null && CityPersonState(person) != null && City.HasPlace(OldMarketVenueId)
                && DistrictAccessBlocker(City.GetPlace(OldMarketVenueId)) == null && !_cityActivities.Any(a => a.Active && a.DefinitionId == "social_" + n);
        }
        private EventDef BindOldMarketEvent(EventDef template, int n)
        {
            if (!CanOfferOldMarketEvent(n)) return null;
            string person = n == 71 ? "resident_guwen" : "resident_leiming", definition = "social_" + n;
            long start = SuggestCityActivityStartAt(definition, person, 0, OldMarketVenueId);
            string reason = CityActivityBlockerAt(definition, new[] { person }, start, false, OldMarketVenueId);
            return EventScene(template, n == 71 ? "顾文约你去青湾旧物市场看钟表，聊聊老物件。遇到喜欢的，可以自己买下。"
                : "雷鸣约你去青湾旧物市场淘旧电子。坏了的可以买下请师傅修，修好后留着或卖掉。",
                Choose("约好时间 · " + new PCShopSell.Core.Time.GameTimestamp(start), s =>
                {
                    var a = s.ScheduleCityActivity(definition, new[] { person }, start);
                    if (a == null) return "日程已变化，未预约。";
                    a.HostId = person; a.ServiceConfirmed = true; a.ServiceChoice = 0;
                    return "已约在青湾旧物市场，实际赴约后继续。";
                }, _ => reason == null, reason),
                Choose("改天再去", _ => "本次没有预约或费用。"));
        }
        private bool TryOldMarketActivityChoice(CityActivity a, OutdoorBatchState state, int choice, out string reply)
        {
            reply = null;
            if (state.Pending != null) return Fail("请先完成当前行走或观察。");
            if (choice == 1) { if (state.Visited >= 3) return Fail("三个摊位已走完。"); QueueOutdoorWalk(a, state, state.Visited); reply = "正在走向下一个旧物摊位。"; return true; }
            if (state.Tested) return Fail("本场主要学习已经完成，可以继续看货、购买或等活动结束。");
            state.Pending = "oldmarket_talk"; state.Mode = choice; state.ReadyAt = Clock.Now.TotalMinutes + 5;
            reply = a.DefinitionId == "social_71" ? "顾文与你现场观察机芯、核对走时或查问来历，五分钟后完成。" : "雷鸣在旧电子摊旁演示检测方法，五分钟后完成。购买和维修另选实物处理。"; return true;
        }
        private void SaveOldMarket(SaveData d) => d.oldMarket = _oldMarket.Copy();
        private void RestoreOldMarket(SaveData d) => _oldMarket = d.oldMarket?.Copy() ?? new OldMarketState();
    }
    public sealed partial class SaveData { public OldMarketState oldMarket; }
}
