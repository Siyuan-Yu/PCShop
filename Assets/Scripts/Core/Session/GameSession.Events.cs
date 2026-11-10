using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    public sealed class EventChoice
    {
        public string Text;

        /// <summary>这个选项现在能不能选（比如钱够不够）。为 null 表示总能选。</summary>
        public Func<GameSession, bool> Enabled;

        /// <summary>灰掉时显示的原因。</summary>
        public string DisabledReason;

        /// <summary>执行选择，返回结果文字。</summary>
        public Func<GameSession, string> Apply;
    }

    public sealed class EventDef
    {
        public string Id;
        /// <summary>分组，目前只有 npc（围绕固定人物）用到。</summary>
        public string Group;
        public string Title;
        public string Body;
        public int MinDay;
        /// <summary>最晚触发的游戏天数；-1 = 没有上限。</summary>
        public int MaxDay = -1;
        /// <summary>每次轮到它时先掷一次，掷中才有资格被抽（0–1，默认 1）。</summary>
        public double TriggerChance = 1;
        public int CooldownDays = 10;
        public int Weight = 10;
        public Func<GameSession, bool> When;
        public List<EventChoice> Choices = new List<EventChoice>();
    }

    public sealed partial class GameSession
    {
        /// <summary>每天的抽取时刻：11:00、15:00、20:00。每个时刻各掷一次，没有未处理的事件才会抽。</summary>
        public static readonly int[] EventMinutes = { 11 * 60, 15 * 60, 20 * 60 };
        /// <summary>每个时刻出事件的概率（一天三次，满足条件的事件够多时约 1.2 个/天，实际受条件限制会少一些）。</summary>
        public const double EventSlotChance = 0.4;
        /// <summary>频率保障：每天最多 EventDayCap 个，任意连续 7 天最多 EventWeekCap 个；前 EventEarlyDays 天（第 0–13 天）概率只有一半。
        /// 只统计随机抽出来的事件，约会、剧情等不算。</summary>
        public const int EventDayCap = 3, EventWeekCap = 10, EventEarlyDays = 14;
        public const double EventEarlyFactor = 0.5;
        private readonly List<int> _randomEventDays = new List<int>();
        private readonly Dictionary<string, int> _eventLastDay = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _eventCount = new Dictionary<string, int>();
        private int _basePatience;

        /// <summary>随机事件开关。游戏运行时打开，单元测试默认关闭，保证老测试的数值不受影响。</summary>
        public bool EventsEnabled { get; set; }

        /// <summary>等玩家做选择的事件。有它时不能存档。</summary>
        public EventDef PendingEvent { get; private set; }

        public event Action<EventDef> EventRaised;

        public int EventCountOf(string id) => _eventCount.TryGetValue(id, out int n) ? n : 0;

        private void RollDailyEvent(GameDate today)
        {
            if (!EventsEnabled || IsGameOver || PendingEvent != null || _sleeping || !HasShop || BrandExpoExclusive) return;
            if (today.TotalDays < 2) return;
            int day = today.TotalDays;
            _randomEventDays.RemoveAll(d => day - d >= 7);
            if (_randomEventDays.Count(d => d == day) >= EventDayCap) return;
            if (_randomEventDays.Count >= EventWeekCap) return;
            double chance = day < EventEarlyDays ? EventSlotChance * EventEarlyFactor : EventSlotChance;
            if (!Random.Chance(chance)) return;

            var eligible = new List<EventDef>();
            foreach (EventDef def in EventCatalog.All)
            {
                if (today.TotalDays < def.MinDay) continue;
                if (def.MaxDay >= 0 && today.TotalDays > def.MaxDay) continue;
                if (_eventLastDay.TryGetValue(def.Id, out int last) && today.TotalDays - last < def.CooldownDays) continue;
                if (def.When != null && !def.When(this)) continue;
                if (def.TriggerChance < 1 && !Random.Chance(def.TriggerChance)) continue;
                eligible.Add(def);
            }
            if (eligible.Count == 0) return;

            int total = eligible.Sum(e => e.Weight);
            int roll = Random.Next(0, total);
            foreach (EventDef def in eligible)
            {
                roll -= def.Weight;
                if (roll >= 0) continue;
                Raise(def, today);
                if (PendingEvent != null) _randomEventDays.Add(day);
                return;
            }
        }

        /// <summary>直接触发某个事件（测试和脚本用）。</summary>
        public void RaiseEvent(string id)
        {
            EventDef def = EventCatalog.All.First(e => e.Id == id);
            Raise(def, Clock.Today);
        }

        private void Raise(EventDef def, GameDate today)
        {
            if(def.Id=="review"||def.Id=="regulars"||def.Id=="students")
            {
                def=BindRetailStory(def);
                if(def==null)return;
            }
            if(def.Id=="rival")
            {
                def=BindPriceWar(def);
                if(def==null)return;
            }
            PendingEvent = def;
            _eventLastDay[def.Id] = today.TotalDays;
            _eventCount[def.Id] = EventCountOf(def.Id) + 1;
            EventRaised?.Invoke(def);
        }

        private bool HasRetailStoryGoods(OwnedShop shop,string eventId)
        {
            if(!IsCompetingRetailShop(shop))return false;
            if(eventId!="review" && shop.RetailKind!="computer")return false;
            if(eventId=="students")return shop.Sim.OnShelf(PartCatalog.Get("ram0"))>=5;
            if(shop.RetailKind=="computer")return shop.Sim.DisplayPcs.Count>0 || ShelfKinds.PartKinds.Any(k=>shop.Sim.ShelfFillOf(k).Total>0);
            return shop.Sim.GoodsOnShelves().Any(g=>g.Value>0 && RetailShopTypes.Sells(shop.RetailKind,g.Key));
        }

        internal OwnedShop RetailStoryTarget(string eventId)=>MyShops
            .Where(shop=>HasRetailStoryGoods(shop,eventId) && shop.Sim.Reputation>=(eventId=="review"?5:eventId=="regulars"?3:2))
            .OrderBy(shop=>shop==CurrentShop?0:1).ThenBy(shop=>shop.Place.Id,StringComparer.Ordinal).FirstOrDefault();

        private EventDef BindRetailStory(EventDef template)
        {
            var target=RetailStoryTarget(template.Id);if(target==null)return null;
            string kind=target.RetailKind;
            var bound=new EventDef{Id=template.Id,Title=template.Title,Body=ShopTitle(target)+"："+template.Body,
                MinDay=template.MinDay,CooldownDays=template.CooldownDays,Weight=template.Weight};
            if(template.Id=="review")
            {
                string accusation=kind=="computer"?"配件是翻新的，冒充新品销售":kind=="clothing"?"衣服的吊牌与实际面料不符":kind=="models"?"模型手办是仿品，冒充正版销售":"商品的质量和来源有问题";
                bound.Title=kind=="computer"?template.Title:kind=="clothing"?"网上有人质疑你家服装质量":kind=="models"?"网上有人质疑你家模型手办的正版来源":"网上有人质疑你家的商品";
                bound.Body=$"有人在本地论坛发帖，质疑{ShopTitle(target)}的{accusation}，帖子下面已经有人跟帖。";
            }
            foreach(var original in template.Choices)
            {
                var choice=original;
                bound.Choices.Add(new EventChoice{Text=choice.Text,DisabledReason=choice.DisabledReason,
                    Enabled=session=>
                    {
                        if(choice.Enabled==null)return true;
                        bool enabled=false;session.WithShop(target,()=>enabled=choice.Enabled(session));return enabled;
                    },
                    Apply=session=>
                    {
                        if(session.ShopAt(target.Place.Id)!=target || target.RetailKind!=kind || !session.HasRetailStoryGoods(target,template.Id))
                            return "这家店已停业、退租、改换主售或不再陈列相关商品，本次事件不再结算，其他店铺未受影响。";
                        string result=null;session.WithShop(target,()=>result=choice.Apply(session));return result;
                    }});
            }
            return bound;
        }

        public bool IsChoiceEnabled(EventChoice c) => c.Enabled == null || c.Enabled(this);

        /// <summary>做出选择。选项灰掉时返回 false。结果通过 Popup 告诉玩家。</summary>
        public bool ResolveEvent(int choiceIndex)
        {
            EventDef def = PendingEvent;
            if (def == null || choiceIndex < 0 || choiceIndex >= def.Choices.Count) return false;
            EventChoice choice = def.Choices[choiceIndex];
            if (!IsChoiceEnabled(choice)) return false;
            PendingEvent = null;
            string result = choice.Apply(this);
            if (!string.IsNullOrEmpty(result)) Popup?.Invoke(def.Title, result);
            return true;
        }

        // ---------- 给事件目录用的小工具 ----------

        internal void EventGain(Money amount, string memo)
        {
            if (amount.IsPositive) Ledger.Credit(Clock.Today, "事件", amount, memo);
        }

        internal void EventPay(Money amount, string memo)
        {
            if (amount.IsPositive) Ledger.ForceDebit(Clock.Today, "事件", amount, memo);
        }

        internal void EventLendToNpc(Money amount, string borrower) =>
            Ledger.ForceDebit(Clock.Today, "借款借出", amount, "借给" + borrower);

        internal string ScheduleNpcRepayment(int days, Money amount, string memo) =>
            $"{ScheduleCredit(days, amount, memo)} 00:00 自动到账 {amount}，已加入日程。";

        internal void EventRest(int minutes)
        {
            AdvanceClock(minutes);
        }

        private void SaveEvents(SaveData d)
        {
            d.randomEventDays = _randomEventDays.ToArray();
            d.eventIds = _eventLastDay.Keys.ToArray();
            d.eventLastDays = d.eventIds.Select(k => _eventLastDay[k]).ToArray();
            d.eventCounts = d.eventIds.Select(EventCountOf).ToArray();
        }

        private void RestoreEvents(SaveData d)
        {
            _randomEventDays.Clear();
            if (d.randomEventDays != null) _randomEventDays.AddRange(d.randomEventDays);
            _eventLastDay.Clear();
            _eventCount.Clear();
            int n = d.eventIds?.Length ?? 0;
            for (int i = 0; i < n; i++)
            {
                _eventLastDay[d.eventIds[i]] = d.eventLastDays[i];
                _eventCount[d.eventIds[i]] = d.eventCounts[i];
            }
            PendingEvent = null;
        }
    }
}
