using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 进货渠道上的固定人物：认识档口老板、送礼、约他们晚上或周末出去玩、打听行情（消息有真有假）。
    /// 交情换来更低的进价和更大的每日进货额度。
    /// </summary>
    public sealed partial class GameSession
    {
        public const int EveningStartMinute = 19 * 60;
        public const int WeekendStartMinute = 10 * 60;
        public const int EveningBookDeadlineMinute = 17 * 60;
        public const int WeekendBookDeadlineMinute = 9 * 60;
        public const int OutingGraceMinutes = 240;
        public const int OutingCooldownDays = 4;
        public const int MinInviteRelation = 15;
        public const int MinTipRelation = 12;
        public const int TipCooldownDays = 3;
        public const int TipResolveDays = 5;
        public const int TipShockDelayDays = 2;
        public const int MaxTipsKept = 30;
        public static readonly Money TipCost = Money.FromYuan(20);

        private readonly List<Appointment> _appointments = new List<Appointment>();
        private readonly List<MarketTip> _tips = new List<MarketTip>();
        private readonly Dictionary<string, int> _outingDay = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _inviteTryDay = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _tipDay = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _tipRight = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _tipWrong = new Dictionary<string, int>();
        private readonly Dictionary<string, (int day, int count)> _stallBought = new Dictionary<string, (int, int)>();

        public IReadOnlyList<Appointment> Appointments => _appointments;
        public IReadOnlyList<MarketTip> Tips => _tips;

        // ---------- 认识档口老板 ----------

        private void MeetStallOwner(Place stall)
        {
            NpcDef owner = Npcs.OwnerOf(stall.Id);
            if (owner == null || IsMet(owner.Id)) return;
            Meet(owner.Id, 10);
            Popup?.Invoke("认识了" + owner.Name, (owner.FirstMeet ?? owner.Intro) + $"\n\n（{owner.Name}：{owner.Role}。去“目标与人脉”或档口面板里可以送礼、约出去玩、打听行情。）");
        }

        // ---------- 进价与额度 ----------

        /// <summary>档口老板给的进价折扣（没有老板或没认识则为 0）。</summary>
        public double StallOwnerDiscount(Place stall)
        {
            NpcDef owner = stall == null ? null : Npcs.OwnerOf(stall.Id);
            return owner == null ? 0 : NpcPerks.OwnerDiscount(Relation(owner.Id));
        }

        /// <summary>每样配件每天最多从这个档口进多少。没有老板的档口按陌生人算。</summary>
        public int StallQuota(Place stall)
        {
            NpcDef owner = stall == null ? null : Npcs.OwnerOf(stall.Id);
            return NpcPerks.StallQuota(owner == null ? 0 : Relation(owner.Id));
        }

        private static string BoughtKey(Place stall, PartDef part) => stall.Id + "|" + part.Id;

        public int StallBoughtToday(Place stall, PartDef part) =>
            _stallBought.TryGetValue(BoughtKey(stall, part), out var b) && b.day == Clock.Today.TotalDays ? b.count : 0;

        public int StallRemaining(Place stall, PartDef part) => Math.Max(0, StallQuota(stall) - StallBoughtToday(stall, part));

        private void RecordStallPurchase(Place stall, PartDef part, int count)
        {
            string key = BoughtKey(stall, part);
            _stallBought[key] = (Clock.Today.TotalDays, StallBoughtToday(stall, part) + count);
        }

        // ---------- 约出去玩 ----------

        public static string SlotName(OutingSlot slot) => slot == OutingSlot.Evening ? "晚上 19:00" : "周末白天 10:00";

        /// <summary>这个时段最近一个还能订的日期。</summary>
        public GameDate NextSlotDate(OutingSlot slot)
        {
            GameDate today = Clock.Today;
            int minute = Clock.MinuteOfDay;
            if (slot == OutingSlot.Evening)
                return minute < EveningBookDeadlineMinute ? today : today.AddDays(1);

            GameDate d = today;
            if (!(d.IsWeekend && minute < WeekendBookDeadlineMinute)) d = d.AddDays(1);
            while (!d.IsWeekend) d = d.AddDays(1);
            return d;
        }

        public string DescribeDay(int day)
        {
            int delta = day - Clock.Today.TotalDays;
            if (delta == 0) return "今天";
            if (delta == 1) return "明天";
            return $"第 {day + 1} 天";
        }

        public string InviteBlocker(string npcId, OutingSlot slot)
        {
            NpcDef n = Npcs.Get(npcId);
            if (n == null) return "没这个人";
            if (IsGameOver) return "游戏已经结束";
            if (!IsMet(npcId)) return "还没认识";
            if (Relation(npcId) < MinInviteRelation) return $"交情不够（要 {MinInviteRelation}）";
            if (_appointments.Any(a => a.NpcId == npcId)) return "已经约好了";
            if (_outingDay.TryGetValue(npcId, out int last) && Clock.Today.TotalDays - last < OutingCooldownDays)
                return $"刚见过面，{OutingCooldownDays - (Clock.Today.TotalDays - last)} 天后再约";
            if (_inviteTryDay.TryGetValue(npcId, out int tried) && tried == Clock.Today.TotalDays) return "今天被拒过了，明天再约";
            int day = NextSlotDate(slot).TotalDays;
            if (_appointments.Any(a => a.Day == day && a.Slot == slot)) return "那个时段已经有安排";
            return null;
        }

        /// <summary>对方答应的概率：交情越高、魅力越高越容易。</summary>
        public double InviteAcceptChance(string npcId) =>
            Math.Min(0.95, 0.40 + Relation(npcId) * 0.006 + EffectiveCharm * 0.005);

        public bool Invite(string npcId, OutingSlot slot)
        {
            string blocker = InviteBlocker(npcId, slot);
            if (blocker != null) return Fail(blocker);
            NpcDef n = Npcs.Get(npcId);
            if (!Random.Chance(InviteAcceptChance(npcId)))
            {
                _inviteTryDay[npcId] = Clock.Today.TotalDays;
                Say($"{n.Name}说：\"那天正好有事，下次吧。\"");
                return false;
            }
            int day = NextSlotDate(slot).TotalDays;
            _appointments.Add(new Appointment { NpcId = npcId, Slot = slot, Day = day });
            Say($"约好了：{DescribeDay(day)} {SlotName(slot)}，和{n.Name}。");
            return true;
        }

        public bool CancelAppointment(string npcId)
        {
            int i = _appointments.FindIndex(a => a.NpcId == npcId);
            if (i < 0) return false;
            _appointments.RemoveAt(i);
            AddRelation(npcId, -3);
            Say("推掉了约会，对方有点不高兴。");
            return true;
        }

        /// <summary>每次时间推进后看有没有到点的约会。要等玩家不在路上、没有待选事件、没睡觉才弹出。</summary>
        private void CheckAppointments(GameTimestamp now)
        {
            for (int i = _appointments.Count - 1; i >= 0; i--)
            {
                Appointment a = _appointments[i];
                long start = (long)a.Day * GameTimestamp.MinutesPerDay + a.StartMinute;
                if (now.TotalMinutes < start) continue;

                if (now.TotalMinutes > start + OutingGraceMinutes)
                {
                    _appointments.RemoveAt(i);
                    AddRelation(a.NpcId, -5);
                    Popup?.Invoke("错过了约定", $"你没能赴约，{Npcs.Get(a.NpcId).Name}等了一阵就走了。交情 -5。");
                    continue;
                }
                if (PendingEvent != null || _sleeping || Trip != null || IsGameOver) continue;

                _appointments.RemoveAt(i);
                RaiseAppointment(a);
                return;
            }
        }

        private void RaiseAppointment(Appointment a)
        {
            NpcDef n = Npcs.Get(a.NpcId);
            var def = new EventDef
            {
                Id = "date_" + a.NpcId,
                Title = "和" + n.Name + "出去",
                Body = a.Slot == OutingSlot.Evening
                    ? $"{n.Name}已经在约好的地方等你了。今晚想去哪儿？（会花几个小时，这段时间店里没人看着）"
                    : $"{n.Name}准时到了。周末白天想怎么玩？（会花大半天，这段时间店里没人看着）",
                CooldownDays = 0,
            };
            foreach (ActivityDef act in Activities.All.Where(x => x.Slot == a.Slot))
            {
                ActivityDef activity = act;
                string liked = n.Likes == act.Id ? "（他的最爱）" : "";
                def.Choices.Add(new EventChoice
                {
                    Text = $"{act.Name}{liked}（{act.Cost}）",
                    Enabled = s => s.Ledger.Balance >= activity.Cost,
                    DisabledReason = "钱不够",
                    Apply = s => s.DoOuting(n, activity),
                });
            }
            def.Choices.Add(new EventChoice
            {
                Text = "临时有事，放鸽子（交情 -8）",
                Apply = s =>
                {
                    s.AddRelation(n.Id, -8);
                    return $"你发消息说去不了。{n.Name}回了个“行吧”。交情 -8。";
                },
            });
            Raise(def, Clock.Today);
        }

        private string DoOuting(NpcDef n, ActivityDef act)
        {
            if (CurrentPlace != null) Leave();
            Ledger.ForceDebit(Clock.Today, "人情", act.Cost, $"和{n.Name}{act.Name}");

            double gain = act.BaseGain * (0.8 + EffectiveCharm * 0.02);
            if (n.Likes == act.Id) gain *= 1.5;
            AddRelation(n.Id, gain);
            _outingDay[n.Id] = Clock.Today.TotalDays;

            Player.Energy = Math.Max(0, Math.Min(Player.MaxEnergy, Player.Energy + act.EnergyChange));
            Player.Health = Math.Max(0, Math.Min(100, Player.Health + act.HealthChange));
            Player.Hunger = Math.Max(0, Math.Min(100, Player.Hunger + act.HungerChange));
            int rel = Relation(n.Id);

            string text = $"{act.Flavor}\n和{n.Name}{act.Name}，花了 {act.Cost}，交情 +{gain:0.#}。";
            if (n.IsTipster && Random.Chance(Math.Min(0.9, 0.35 + rel * 0.005 + act.TipBonus)))
                text += "\n\n" + CreateTip(n);

            AdvanceClock(act.DurationMinutes);
            return text;
        }

        // ---------- 行情消息 ----------

        public string TipBlocker(string npcId)
        {
            NpcDef n = Npcs.Get(npcId);
            if (n == null || !n.IsTipster) return "他不爱聊这个";
            if (!IsMet(npcId)) return "还没认识";
            if (Relation(npcId) < MinTipRelation) return $"交情不够（要 {MinTipRelation}）";
            if (_tipDay.TryGetValue(npcId, out int last) && Clock.Today.TotalDays - last < TipCooldownDays)
                return $"刚问过，{TipCooldownDays - (Clock.Today.TotalDays - last)} 天后再问";
            if (Backpack.CigaretteCount <= 0) return "没带烟（烟酒茶行买，或网购）";
            return null;
        }

        /// <summary>递根烟，打听点行情。他说的不一定是真的。</summary>
        public bool AskForTip(string npcId)
        {
            string blocker = TipBlocker(npcId);
            if (blocker != null) return Fail(blocker);
            NpcDef n = Npcs.Get(npcId);
            Backpack.TakeCigarette();
            ApplyCargoLimit();
            AdvanceClock(5);
            string text = CreateTip(n);
            Popup?.Invoke(n.Name + "的消息", text);
            return true;
        }

        /// <summary>这个人这次说真话的概率：本来的靠谱度 + 交情带来的信任。</summary>
        public double TipTruthChance(NpcDef n) => Math.Min(0.95, n.Reliability + Relation(n.Id) * 0.002);

        private string CreateTip(NpcDef n)
        {
            PartCategory cat = PickTipCategory(n);
            int dir = Random.Chance(0.55) ? 1 : -1;
            bool real = Random.Chance(TipTruthChance(n));

            var tip = new MarketTip
            {
                NpcId = n.Id,
                Category = cat,
                Direction = dir,
                IssuedDay = Clock.Today.TotalDays,
                ResolveDay = Clock.Today.TotalDays + TipResolveDays,
                IndexAtIssue = Market.Index(cat),
                Outcome = TipOutcome.Pending,
            };
            // 说真话 = 他真的知道要变价：行情在两天后朝那个方向走一波
            if (real) Market.ScheduleShock(Clock.Today.AddDays(TipShockDelayDays), cat, dir * Random.Range(0.10, 0.18));

            _tips.Add(tip);
            while (_tips.Count > MaxTipsKept) _tips.RemoveAt(0);
            _tipDay[n.Id] = Clock.Today.TotalDays;

            string name = PartCatalog.CategoryName(cat);
            return dir > 0
                ? $"{n.Name}压低声音：\"听我一句，这几天{name}要涨，能囤就囤点。\""
                : $"{n.Name}压低声音：\"{name}这几天要跌，手上有货的赶紧出，别囤了。\"";
        }

        private PartCategory PickTipCategory(NpcDef n)
        {
            if (n.StallId != null && City.HasPlace(n.StallId))
            {
                Place stall = City.GetPlace(n.StallId);
                if (stall.Sells.Count > 0) return stall.Sells[Random.Next(0, stall.Sells.Count)];
            }
            var all = PartCatalog.Categories;
            return all[Random.Next(0, all.Length)];
        }

        /// <summary>到期的消息对一下账：涨跌方向对且幅度够就算准。</summary>
        private void ResolveTips(GameDate today)
        {
            foreach (MarketTip t in _tips)
            {
                if (t.Outcome != TipOutcome.Pending || today.TotalDays < t.ResolveDay) continue;
                double change = Market.Index(t.Category) / t.IndexAtIssue - 1.0;
                bool right = t.Direction * change >= 0.02;
                t.Outcome = right ? TipOutcome.Right : TipOutcome.Wrong;
                Dictionary<string, int> book = right ? _tipRight : _tipWrong;
                book[t.NpcId] = (book.TryGetValue(t.NpcId, out int n) ? n : 0) + 1;
                NpcDef who = Npcs.Get(t.NpcId);
                Say($"{who.Name}说{PartCatalog.CategoryName(t.Category)}要{(t.Direction > 0 ? "涨" : "跌")}——{(right ? "应验了" : "没应验")}。");
            }
        }

        public int TipsRight(string npcId) => _tipRight.TryGetValue(npcId, out int n) ? n : 0;

        public int TipsWrong(string npcId) => _tipWrong.TryGetValue(npcId, out int n) ? n : 0;

        // ---------- 存档 ----------

        private void SaveSocial(SaveData d)
        {
            var s = new SaveSocial
            {
                apptNpc = _appointments.Select(a => a.NpcId).ToArray(),
                apptSlot = _appointments.Select(a => (int)a.Slot).ToArray(),
                apptDay = _appointments.Select(a => a.Day).ToArray(),

                tipNpc = _tips.Select(t => t.NpcId).ToArray(),
                tipCategory = _tips.Select(t => (int)t.Category).ToArray(),
                tipDirection = _tips.Select(t => t.Direction).ToArray(),
                tipIssued = _tips.Select(t => t.IssuedDay).ToArray(),
                tipResolve = _tips.Select(t => t.ResolveDay).ToArray(),
                tipIndex = _tips.Select(t => t.IndexAtIssue).ToArray(),
                tipOutcome = _tips.Select(t => (int)t.Outcome).ToArray(),

                outingIds = _outingDay.Keys.ToArray(),
                outingDays = _outingDay.Values.ToArray(),
                inviteTryIds = _inviteTryDay.Keys.ToArray(),
                inviteTryDays = _inviteTryDay.Values.ToArray(),
                tipDayIds = _tipDay.Keys.ToArray(),
                tipDays = _tipDay.Values.ToArray(),
                scoreIds = _tipRight.Keys.Union(_tipWrong.Keys).ToArray(),
                boughtKeys = _stallBought.Keys.ToArray(),
                boughtDays = _stallBought.Values.Select(v => v.day).ToArray(),
                boughtCounts = _stallBought.Values.Select(v => v.count).ToArray(),

                marketBias = Market.BiasValues(),
            };
            s.scoreRight = s.scoreIds.Select(TipsRight).ToArray();
            s.scoreWrong = s.scoreIds.Select(TipsWrong).ToArray();
            var shocks = Market.PendingShocks;
            s.shockDays = shocks.Select(x => x.day).ToArray();
            s.shockCategories = shocks.Select(x => (int)x.category).ToArray();
            s.shockDeltas = shocks.Select(x => x.delta).ToArray();
            d.social = s;
        }

        private void RestoreSocial(SaveData d)
        {
            SaveSocial s = d.social ?? new SaveSocial();

            _appointments.Clear();
            int n = MinLen(s.apptNpc, s.apptSlot, s.apptDay);
            for (int i = 0; i < n; i++)
                if (Npcs.Get(s.apptNpc[i]) != null)
                    _appointments.Add(new Appointment { NpcId = s.apptNpc[i], Slot = (OutingSlot)s.apptSlot[i], Day = s.apptDay[i] });

            _tips.Clear();
            n = MinLen(s.tipNpc, s.tipCategory, s.tipDirection, s.tipIssued, s.tipResolve, s.tipIndex, s.tipOutcome);
            for (int i = 0; i < n; i++)
            {
                if (Npcs.Get(s.tipNpc[i]) == null) continue;
                _tips.Add(new MarketTip
                {
                    NpcId = s.tipNpc[i], Category = (PartCategory)s.tipCategory[i], Direction = s.tipDirection[i],
                    IssuedDay = s.tipIssued[i], ResolveDay = s.tipResolve[i], IndexAtIssue = s.tipIndex[i],
                    Outcome = (TipOutcome)s.tipOutcome[i],
                });
            }

            ReadDict(_outingDay, s.outingIds, s.outingDays);
            ReadDict(_inviteTryDay, s.inviteTryIds, s.inviteTryDays);
            ReadDict(_tipDay, s.tipDayIds, s.tipDays);
            ReadDict(_tipRight, s.scoreIds, s.scoreRight);
            ReadDict(_tipWrong, s.scoreIds, s.scoreWrong);

            _stallBought.Clear();
            n = MinLen(s.boughtKeys, s.boughtDays, s.boughtCounts);
            for (int i = 0; i < n; i++) _stallBought[s.boughtKeys[i]] = (s.boughtDays[i], s.boughtCounts[i]);

            var shocks = new List<(int, PartCategory, double)>();
            n = MinLen(s.shockDays, s.shockCategories, s.shockDeltas);
            for (int i = 0; i < n; i++) shocks.Add((s.shockDays[i], (PartCategory)s.shockCategories[i], s.shockDeltas[i]));
            Market.RestoreBias(s.marketBias, shocks);
        }

        private static int MinLen(params Array[] arrays)
        {
            int n = int.MaxValue;
            foreach (Array a in arrays) n = Math.Min(n, a?.Length ?? 0);
            return n == int.MaxValue ? 0 : n;
        }

        private static void ReadDict(Dictionary<string, int> dict, string[] keys, int[] values)
        {
            dict.Clear();
            int n = MinLen(keys, values);
            for (int i = 0; i < n; i++) dict[keys[i]] = values[i];
        }
    }
}
