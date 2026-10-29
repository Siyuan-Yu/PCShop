using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

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
        public string Title;
        public string Body;
        public int MinDay;
        public int CooldownDays = 10;
        public int Weight = 10;
        public Func<GameSession, bool> When;
        public List<EventChoice> Choices = new List<EventChoice>();
    }

    public sealed partial class GameSession
    {
        public const int EventMinute = 11 * 60;
        public const double EventDailyChance = 0.55;

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
            if (!EventsEnabled || IsGameOver || PendingEvent != null || _sleeping || !HasShop) return;
            if (today.TotalDays < 2) return;
            if (!Random.Chance(EventDailyChance)) return;

            var eligible = new List<EventDef>();
            foreach (EventDef def in EventCatalog.All)
            {
                if (today.TotalDays < def.MinDay) continue;
                if (_eventLastDay.TryGetValue(def.Id, out int last) && today.TotalDays - last < def.CooldownDays) continue;
                if (def.When != null && !def.When(this)) continue;
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
            PendingEvent = def;
            _eventLastDay[def.Id] = today.TotalDays;
            _eventCount[def.Id] = EventCountOf(def.Id) + 1;
            EventRaised?.Invoke(def);
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

        internal void EventRest(int minutes)
        {
            AdvanceClock(minutes);
        }

        private void SaveEvents(SaveData d)
        {
            d.eventIds = _eventLastDay.Keys.ToArray();
            d.eventLastDays = d.eventIds.Select(k => _eventLastDay[k]).ToArray();
            d.eventCounts = d.eventIds.Select(EventCountOf).ToArray();
        }

        private void RestoreEvents(SaveData d)
        {
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
