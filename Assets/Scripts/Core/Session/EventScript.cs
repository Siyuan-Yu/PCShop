using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    /// <summary>事件脚本的执行：把 events.json 里的条件、效果、掷骰变成游戏里的行为。数据结构见 EventScriptModel.cs。</summary>
    public static partial class EventScript
    {
        internal const int CharmThreshold = 45;
        internal const int IntelligenceThreshold = 50;
        internal const int RelationThreshold = 35;

        internal static double ProgressiveChance(double chance, params (double Score, double Threshold, double PerPoint)[] stats)
        {
            foreach (var stat in stats)
            {
                if (stat.PerPoint == 0) continue;
                if (stat.Score < stat.Threshold) return 0;
                chance += (stat.Score - stat.Threshold) * stat.PerPoint;
            }
            return Math.Max(0, Math.Min(1, chance));
        }

        private static Money Y(long yuan) => Money.FromYuan(yuan);

        public static bool Test(GameSession s, EventCond c)
        {
            bool r;
            switch (c.type)
            {
                case "cash": r = s.Ledger.Balance >= Money.FromYuan((long)c.value); break;
                case "cashBelow": r = s.Ledger.Balance < Money.FromYuan((long)c.value); break;
                case "negativeCashDays": r = s.NegativeCashDays >= c.value; break;
                case "flag": r = s.Flags.Contains(c.key); break;
                case "upgrade": r = s.HasUpgrade(c.key); break;
                case "stock": r = !s.PlayerShop.Stock.IsEmpty; break;
                case "samePlace": r = s.CitySamePlace(c.key); break;
                case "met": r = s.IsMet(c.key); break;
                case "relationAtLeast": r = s.Relation(c.key) >= c.value; break;
                case "relationBelow": r = s.Relation(c.key) < c.value; break;
                case "rentHikesBelow": r = s.RentHikes < c.value; break;
                case "energyBelow": r = s.Player.Energy < c.value; break;
                case "sleepDebtAbove": r = s.Player.SleepDebt > c.value; break;
                case "healthBelow": r = s.Player.Health < c.value; break;
                case "hasPhone": r = s.HasSmartphone; break;
                case "bankDeposit": r = s.BankDeposit >= Money.FromYuan((long)c.value); break;
                case "hasLoan": r = s.AppLoans.Any(l => !l.Repaid); break;
                case "reputationAtLeast": r = s.PlayerShop.Reputation >= c.value; break;
                case "racingUnlocked": r = s.RacingUnlocked; break;
                case "racingWins": r = s.RacingWins >= c.value; break;
                case "netWorthAtLeast": r = s.NetWorth() >= Money.FromYuan((long)c.value); break;
                case "charmAtLeast": r = s.EffectiveCharm >= c.value; break;
                case "intelAtLeast": r = s.Player.Intelligence >= c.value; break;
                default: throw new InvalidOperationException("未知条件类型：" + c.type);
            }
            return c.not ? !r : r;
        }

        private static bool All(GameSession s, List<EventCond> list) { foreach (var c in list) if (!Test(s, c)) return false; return true; }
        private static bool Any(GameSession s, List<EventCond> list) { foreach (var c in list) if (Test(s, c)) return true; return false; }

        /// <summary>执行一串效果，把要给玩家看的文字追加进 lines。返回 false 表示被 stopIfNone 中止。</summary>
        private static bool Run(GameSession s, List<EventOp> ops, List<string> lines, ref string item)
        {
            foreach (var op in ops)
            {
                switch (op.type)
                {
                    case "text": lines.Add(Fmt(op.text, item)); break;
                    case "pay": s.EventPay(Y(op.yuan), op.memo); break;
                    case "gain": s.EventGain(Y(op.yuan), op.memo); break;
                    case "shopRep": s.PlayerShop.AdjustShopReputation(op.value); break;
                    case "rep": s.PlayerShop.Reputation += op.value; break;
                    case "charm": s.Player.Charm = Math.Max(0, Math.Min(100, s.Player.Charm + (int)op.value)); break;
                    case "health": s.Player.Health = Math.Max(0, Math.Min(100, s.Player.Health + op.value)); break;
                    case "energy": s.Player.SpendEnergy(op.value); break;
                    case "rest": s.EventRest((int)op.value); break;
                    case "flag": s.Flags.Add(op.key); break;
                    case "loseItem":
                        item = EventCatalog.LoseRandomItem(s);
                        if (item == null)
                        {
                            if (!string.IsNullOrEmpty(op.alt)) lines.Add(op.alt);
                            if (op.stopIfNone) return false;
                        }
                        else if (!string.IsNullOrEmpty(op.text)) lines.Add(Fmt(op.text, item));
                        break;
                    case "relation": s.AddRelation(op.npc, op.value); break;
                    case "meet": s.Meet(op.npc, op.value); break;
                    case "markup": s.PlayerShop.Markup += op.value; break;
                    case "lend": s.EventLendToNpc(Y(op.yuan), op.memo); break;
                    case "repay": lines.Add(s.ScheduleNpcRepayment(op.days, Y(op.yuan), op.memo)); break;
                    case "meeting": lines.Add(s.ScheduleLegacyCityMeeting(op.npc, op.kind, op.key + s.Clock.Today.TotalDays, op.flag)); break;
                    case "referral": lines.Add(s.OfferCityReferralContract(op.key, op.memo, op.count, op.flag)); break;
                    case "rentHike": s.AddRentHike(); break;
                    case "intel": s.Player.Intelligence = Math.Max(0, Math.Min(100, s.Player.Intelligence + (int)op.value)); break;
                    case "raceInvite": lines.Add(s.EventRaceInvite(op.npc, string.IsNullOrEmpty(op.key) ? "race_0" : op.key)); break;
                    case "interestFreeLoan": lines.Add(s.AcceptFriendLoan(op.npc, op.yuan, op.count)); break;
                    default: throw new InvalidOperationException("未知效果类型：" + op.type);
                }
            }
            return true;
        }

        private static string Fmt(string text, string item) => (text ?? "").Replace("{item}", item ?? "");

        public static double ChoiceChance(GameSession s, EventChoiceEntry c)
        {
            if (c.chance < 0) return 0;
            double chance = c.chance;
            foreach (var bonus in c.chanceBonus)
            {
                if (bonus.perPoint == 0)
                {
                    if (All(s, bonus.when)) chance += bonus.add;
                    continue;
                }
                var threshold = bonus.when[0];
                double score = threshold.type == "racingWins" ? s.RacingWins : s.Relation(threshold.key);
                if (score < threshold.value) return 0;
                chance += bonus.add + (score - threshold.value) * bonus.perPoint;
            }
            return ProgressiveChance(chance,
                (s.EffectiveCharm, CharmThreshold, c.chanceCharm),
                (s.Player.Intelligence, IntelligenceThreshold, c.chanceIntel),
                (string.IsNullOrEmpty(c.chanceNpc) ? 0 : s.Relation(c.chanceNpc), RelationThreshold, c.chanceRelation));
        }

        private static string Resolve(GameSession s, EventChoiceEntry c)
        {
            double chance = ChoiceChance(s, c);
            var lines = new List<string>(); string item = null;
            if (Run(s, c.effects, lines, ref item) && c.chance >= 0)
                Run(s, s.Random.Chance(chance) ? c.onHit : c.onMiss, lines, ref item);
            return string.Join("\n", lines);
        }

        public static EventChoice Compile(EventChoiceEntry c) => new EventChoice
        {
            Text = c.text,
            DisabledReason = c.disabledReason,
            Enabled = c.requires.Count == 0 ? (Func<GameSession, bool>)null : s => All(s, c.requires),
            Apply = s => Resolve(s, c),
        };

        public static EventDef Compile(EventEntry e)
        {
            var def = new EventDef { Id = e.id, Title = e.title, Body = e.body, Group = e.group,
                MinDay = Math.Max(0, e.minDay), MaxDay = e.maxDay, TriggerChance = e.triggerChance < 0 ? 1 : e.triggerChance,
                CooldownDays = e.cooldownDays < 0 ? 10 : e.cooldownDays, Weight = e.weight < 0 ? 10 : e.weight };
            if (e.when.Count > 0 || e.whenAny.Count > 0)
                def.When = s => All(s, e.when) && (e.whenAny.Count == 0 || Any(s, e.whenAny));
            foreach (var c in e.choices) def.Choices.Add(Compile(c));
            return def;
        }
    }
}
