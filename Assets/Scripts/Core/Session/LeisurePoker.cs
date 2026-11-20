using System;
using System.Linq;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    public enum PokerAction { Fold, Call, Raise }

    [Serializable]
    public sealed class PokerSeat
    {
        public string Name, Action;
        public int Stack, StreetBet, Invested;
        public int First = -1, Second = -1;
        public bool Folded, Pending, MayRaise;
    }

    [Serializable]
    public sealed class PokerPlay
    {
        public const int SmallBlind = 10, BigBlind = 20;
        public PokerSeat[] Players = { new PokerSeat { Name = "你" }, new PokerSeat(), new PokerSeat(), new PokerSeat() };
        public int[] Deck = new int[52], Community = { -1, -1, -1, -1, -1 };
        public int Dealer = -1, Cursor, Street, Turn = -1, HighBet, RaiseStep = BigBlind, HandsPlayed;
        public int LastPot;
        public double Wait;
        public bool HandActive, Showdown;
        public string Message = "等待入座。";

        public int Pot => Players.Sum(p => p.Invested);
        public int ToCall => Math.Min(Players[0].Stack, Math.Max(0, HighBet - Players[0].StreetBet));
        public int MaximumRaise => Players[0].Stack + Players[0].StreetBet;
        public int MinimumRaise => Math.Min(MaximumRaise, HighBet + RaiseStep);
        public bool PlayerTurn => HandActive && Turn == 0;
        public bool CanRaise => PlayerTurn && CanRaiseSeat(0);
        public bool CanPlayNextHand => !HandActive && Players[0].Stack > 0;

        internal void OpenTable(GameRandom random, int buyIn)
        {
            foreach (var seat in Players) seat.Stack = buyIn;
            NextHand(random);
        }

        internal void NextHand(GameRandom random)
        {
            if (!CanPlayNextHand) return;
            for (int i = 1; i < Players.Length; i++)
                if (Players[i].Stack == 0) Players[i].Stack = GameSession.PokerBuyInYuan;
            Dealer = (Dealer + 1) % Players.Length;
            for (int i = 0; i < Deck.Length; i++) Deck[i] = i;
            for (int i = Deck.Length - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (Deck[i], Deck[j]) = (Deck[j], Deck[i]);
            }
            Cursor = Street = LastPot = 0;
            Showdown = false;
            HandActive = true;
            for (int i = 0; i < Community.Length; i++) Community[i] = -1;
            foreach (var seat in Players)
            {
                seat.StreetBet = seat.Invested = 0;
                seat.Folded = false;
                seat.Pending = seat.MayRaise = true;
                seat.Action = "";
                seat.First = Deck[Cursor++];
                seat.Second = Deck[Cursor++];
            }
            int small = (Dealer + 1) % Players.Length, big = (Dealer + 2) % Players.Length;
            Commit(Players[small], SmallBlind);
            Commit(Players[big], BigBlind);
            Players[small].Action = $"小盲 ¥{Players[small].StreetBet}";
            Players[big].Action = $"大盲 ¥{Players[big].StreetBet}";
            HighBet = BigBlind;
            RaiseStep = BigBlind;
            Message = "已发两张手牌。";
            FindNext(big);
        }

        internal bool ActPlayer(PokerAction action, int raiseTo)
            => PlayerTurn && Act(0, action, raiseTo);

        private bool CanRaiseSeat(int seat)
        {
            var player = Players[seat];
            return player.MayRaise && player.Stack + player.StreetBet > HighBet
                && Players.Where((p, i) => i != seat).Any(p => !p.Folded && p.Stack > 0);
        }

        private bool Act(int index, PokerAction action, int raiseTo)
        {
            var seat = Players[index];
            if (!HandActive || Turn != index || seat.Stack <= 0 || seat.Folded || !seat.Pending) return false;
            switch (action)
            {
                case PokerAction.Fold:
                    seat.Folded = true;
                    seat.Action = "弃牌";
                    break;
                case PokerAction.Call:
                    int call = Math.Max(0, HighBet - seat.StreetBet);
                    Commit(seat, call);
                    seat.Action = call == 0 ? "过牌" : seat.Stack == 0 ? "全下跟注" : "跟注";
                    break;
                case PokerAction.Raise:
                    int max = seat.StreetBet + seat.Stack;
                    if (!CanRaiseSeat(index) || raiseTo > max || raiseTo < Math.Min(max, HighBet + RaiseStep)) return false;
                    int raise = raiseTo - HighBet;
                    bool reopens = raise >= RaiseStep;
                    if (reopens) RaiseStep = raise;
                    Commit(seat, raiseTo - seat.StreetBet);
                    HighBet = seat.StreetBet;
                    seat.Action = seat.Stack == 0 ? $"全下 ¥{HighBet}" : $"加注到 ¥{HighBet}";
                    for (int i = 0; i < Players.Length; i++)
                    {
                        if (i == index) continue;
                        var other = Players[i];
                        if (other.Folded || other.Stack == 0) continue;
                        other.Pending = other.StreetBet < HighBet;
                        if (reopens) other.MayRaise = true;
                    }
                    break;
                default:
                    return false;
            }
            seat.Pending = seat.MayRaise = false;
            Message = $"{seat.Name} · {seat.Action}";
            FindNext(index);
            return true;
        }

        internal void Step(double dt, GameRandom random)
        {
            if (!HandActive || Turn <= 0) return;
            Wait += dt;
            if (Wait < .42) return;
            int index = Turn;
            var player = Players[index];
            int need = Math.Max(0, HighBet - player.StreetBet);
            int first = player.First % 13 + 2, second = player.Second % 13 + 2;
            bool strong = first == second || first + second >= 23;
            if (Street > 0)
                strong = Best(player.First, player.Second, Community) >= 2 * 759375L;
            if (CanRaiseSeat(index) && strong && random.Chance(.23))
            {
                Act(index, PokerAction.Raise, Math.Min(player.Stack + player.StreetBet, HighBet + RaiseStep));
                return;
            }
            if (need > 0 && !strong && need > Math.Max(BigBlind, Pot / 5) && random.Chance(.68))
                Act(index, PokerAction.Fold, 0);
            else
                Act(index, PokerAction.Call, 0);
        }

        internal void LeaveTable()
        {
            // If an external interruption ends the visit mid-hand, only the uncommitted player stack is redeemed.
            if (!HandActive) return;
            Players[0].Folded = true;
            FinishHand(false);
        }

        private void FindNext(int after)
        {
            Wait = 0;
            var active = Players.Where(p => !p.Folded).ToArray();
            if (active.Length <= 1) { FinishHand(false); return; }

            int funded = active.Count(p => p.Stack > 0);
            for (int offset = 1; offset <= Players.Length; offset++)
            {
                int index = (after + offset) % Players.Length;
                var seat = Players[index];
                if (!seat.Pending || seat.Folded || seat.Stack == 0 || funded <= 1 && seat.StreetBet >= HighBet) continue;
                Turn = index;
                return;
            }

            if (Street == 3 || funded <= 1)
            {
                Reveal(5);
                FinishHand(true);
                return;
            }
            Street++;
            Reveal(Street + 2);
            HighBet = 0;
            RaiseStep = BigBlind;
            foreach (var seat in Players)
            {
                seat.StreetBet = 0;
                seat.Pending = seat.MayRaise = !seat.Folded && seat.Stack > 0;
                if (seat.Pending) seat.Action = "";
            }
            FindNext(Dealer);
        }

        private void Reveal(int count)
        {
            for (int i = 0; i < count; i++)
                if (Community[i] < 0) Community[i] = Deck[Cursor++];
        }

        private void FinishHand(bool showdown)
        {
            if (!HandActive) return;
            LastPot = Pot;
            Showdown = showdown;
            HandActive = false;
            Turn = -1;
            HandsPlayed++;
            int[] winners = new int[Players.Length];
            long[] scores = Players.Select(s => !s.Folded && showdown ? Best(s.First, s.Second, Community) : 0).ToArray();
            int lower = 0;
            foreach (int level in Players.Select(s => s.Invested).Where(amount => amount > 0).Distinct().OrderBy(amount => amount))
            {
                var contributed = Enumerable.Range(0, Players.Length).Where(i => Players[i].Invested >= level).ToArray();
                var eligible = contributed.Where(i => !Players[i].Folded).ToArray();
                int amount = (level - lower) * contributed.Length;
                if (eligible.Length == 0)
                {
                    foreach (int i in contributed) Players[i].Stack += level - lower;
                }
                else
                {
                    long best = eligible.Max(i => scores[i]);
                    var tied = eligible.Where(i => scores[i] == best)
                        .OrderBy(i => (i - Dealer - 1 + Players.Length) % Players.Length).ToArray();
                    for (int i = 0; i < tied.Length; i++)
                    {
                        int share = amount / tied.Length + (i < amount % tied.Length ? 1 : 0);
                        Players[tied[i]].Stack += share;
                        winners[tied[i]] += share;
                    }
                }
                lower = level;
            }
            foreach (var seat in Players) seat.Invested = seat.StreetBet = 0;
            Message = winners[0] > 0 ? $"你赢得 ¥{winners[0]} 筹码。"
                : string.Join("、", Players.Where((seat, i) => winners[i] > 0).Select(seat => seat.Name)) + "拿下这一手。";
        }

        private static void Commit(PokerSeat seat, int amount)
        {
            amount = Math.Min(seat.Stack, amount);
            seat.Stack -= amount;
            seat.StreetBet += amount;
            seat.Invested += amount;
        }

        private static long Best(int first, int second, int[] community)
        {
            var cards = new[] { first, second }.Concat(community.Where(c => c >= 0)).ToArray();
            long best = 0;
            int n = cards.Length;
            for (int a = 0; a < n - 4; a++)
            for (int b = a + 1; b < n - 3; b++)
            for (int c = b + 1; c < n - 2; c++)
            for (int d = c + 1; d < n - 1; d++)
            for (int e = d + 1; e < n; e++)
                best = Math.Max(best, Rank(new[] { cards[a], cards[b], cards[c], cards[d], cards[e] }));
            return best;
        }

        private static long Rank(int[] cards)
        {
            int[] ranks = cards.Select(c => c % 13 + 2).OrderByDescending(r => r).ToArray();
            bool flush = cards.All(c => c / 13 == cards[0] / 13);
            bool consecutive = ranks.Distinct().Count() == 5 && ranks[0] - ranks[4] == 4;
            int straight = consecutive ? ranks[0] : ranks.SequenceEqual(new[] { 14, 5, 4, 3, 2 }) ? 5 : 0;
            var groups = ranks.GroupBy(r => r).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToArray();
            int first = groups[0].Count(), second = groups[1].Count();
            if (flush && straight > 0) return Encode(8, new[] { straight });
            if (first == 4) return Encode(7, groups.Select(g => g.Key));
            if (first == 3 && second == 2) return Encode(6, groups.Select(g => g.Key));
            if (flush) return Encode(5, ranks);
            if (straight > 0) return Encode(4, new[] { straight });
            if (first == 3) return Encode(3, groups.Select(g => g.Key));
            if (first == 2 && second == 2) return Encode(2, groups.Select(g => g.Key));
            if (first == 2) return Encode(1, groups.Select(g => g.Key));
            return Encode(0, ranks);
        }

        private static long Encode(int group, System.Collections.Generic.IEnumerable<int> tiebreakers)
        {
            long result = group;
            foreach (int rank in tiebreakers.Concat(new int[5]).Take(5)) result = result * 15 + rank;
            return result;
        }
    }
}
