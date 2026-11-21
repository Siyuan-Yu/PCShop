using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class LeisurePlay
    {
        public string ActivityId;
        public LeisureGame Game;
        public PachinkoPlay Pachinko;
        public PokerPlay Poker;
        public GiftClawPlay Claw;
        public BilliardsPlay Billiards;
    }

    public sealed partial class GameSession
    {
        private LeisurePlay _leisurePlay;
        private readonly int[] _pachinkoWallet = new int[PachinkoTiers.Length];
        public const int ClawPriceYuan = 200;
        public const int PokerBuyInYuan = 400;
        internal const int PachinkoWalletLimit = 1000000;
        public static readonly (string Name, int Buy, int Sell)[] PachinkoTiers =
        {
            ("铜珠", 100, 70), ("银珠", 500, 375), ("金珠", 2500, 2000), ("紫晶珠", 10000, 8500)
        };

        public bool HasPachinkoBeads => Array.Exists(_pachinkoWallet, count => count > 0);
        public int PachinkoBeadCount(int tier) => _pachinkoWallet[tier];
        public int PachinkoBeadSpace(int tier) => PachinkoWalletLimit - _pachinkoWallet[tier];
        public Money PachinkoBeadValue
        {
            get
            {
                Money total = Money.Zero;
                for (int i = 0; i < _pachinkoWallet.Length; i++)
                    total += Money.FromYuan(PachinkoTiers[i].Sell) * _pachinkoWallet[i];
                return total;
            }
        }

        public string PachinkoTradeBlocker
        {
            get
            {
                if (CurrentPlace?.Id != CityLifeCatalog.PachinkoVenue || IsGameOver || IsWorking || _sleeping)
                    return "请到弹珠店换珠。";
                if (RunningCityActivity != null) return "请先结束当前活动。";
                return NpcVenueStaffingBlocker(CurrentPlace) ?? ClosedReason(CurrentPlace);
            }
        }

        public bool BuyPachinkoBead(int tier)
        {
            if (PachinkoTradeBlocker is string blocker) return Fail(blocker);
            if ((uint)tier >= _pachinkoWallet.Length || PachinkoBeadSpace(tier) == 0) return false;
            var kind = PachinkoTiers[tier];
            if (!Ledger.TryDebit(Clock.Today, "休闲活动", Money.FromYuan(kind.Buy), CurrentPlace.Name + "换珠 · " + kind.Name))
                return Fail("现金不足。");
            _pachinkoWallet[tier]++;
            return true;
        }

        public bool SellPachinkoBeads(int? tier)
        {
            if (PachinkoTradeBlocker is string blocker) return Fail(blocker);
            if (tier is int index && (uint)index >= _pachinkoWallet.Length) return false;
            Money proceeds = tier is int selected
                ? _pachinkoWallet[selected] > 0 ? Money.FromYuan(PachinkoTiers[selected].Sell) : Money.Zero
                : PachinkoBeadValue;
            if (!proceeds.IsPositive) return false;
            Ledger.Credit(Clock.Today, "休闲活动", proceeds, CurrentPlace.Name + "卖珠");
            if (tier is int sold) _pachinkoWallet[sold]--;
            else Array.Clear(_pachinkoWallet, 0, _pachinkoWallet.Length);
            return true;
        }

        private static readonly string[] ClawPrizes = {
            Gifts.Soda, Gifts.Snack, "model:harbor:skiff", "model:garden:mushroom",
            "model:starlight:lamp", "model:garden:sprout"
        };

        public LeisurePlay LiveLeisure => _leisurePlay != null
            && RunningCityActivity?.Id == _leisurePlay.ActivityId
            && CurrentPlace?.Id == RunningCityActivity.VenueId && !IsGameOver && !_sleeping && !IsWorking
                ? _leisurePlay : null;

        private string LeisureServiceBlocker(string venueId) =>
            CityLifeCatalog.LeisureAt(venueId) == LeisureGame.Poker && !Ledger.CanAfford(Money.FromYuan(PokerBuyInYuan))
                ? $"需要 {Money.FromYuan(PokerBuyInYuan)} 换筹码" : null;

        public bool IsLeisureVenueWorker(CityPersonState person) =>
            person?.Task == CityPersonTask.Working && person.Route == null
            && person.LocationPlaceId == person.WorkPlaceId
            && (CityLifeCatalog.LeisureAt(person.LocationPlaceId) != LeisureGame.None
                || person.LocationPlaceId == OldMarketVenueId && person.PersonId == OldMarketAttendantId)
            && CityPerson(person.PersonId) != null;

        private void StartLeisurePlay(CityActivity activity)
        {
            var game = CityLifeCatalog.LeisureService(activity);
            if (game == LeisureGame.None || _leisurePlay?.ActivityId == activity.Id) return;

            var state = new LeisurePlay { ActivityId = activity.Id, Game = game };
            _leisurePlay = state;
            switch (game)
            {
                case LeisureGame.Pachinko:
                    int tier = Array.FindIndex(_pachinkoWallet, count => count > 0);
                    state.Pachinko = new PachinkoPlay { Tier = Math.Max(0, tier), BoardTime = Random.Range(0, 12) };
                    break;
                case LeisureGame.Poker:
                    state.Poker = new PokerPlay();
                    state.Poker.Players[1].Name = ActivityCompanionName(activity) ?? "一号位";
                    state.Poker.Players[2].Name = "二号位";
                    state.Poker.Players[3].Name = "三号位";
                    if (LeisureDebit(activity.Id + ":buy-in", "德扑换筹码", PokerBuyInYuan))
                    {
                        activity.EntryCostCents += Money.FromYuan(PokerBuyInYuan).Cents;
                        state.Poker.OpenTable(Random, PokerBuyInYuan);
                    }
                    else
                        state.Poker.Message = "现金不足，未换筹码。";
                    break;
                case LeisureGame.Claw:
                    state.Claw = new GiftClawPlay();
                    int rotation = (Clock.Today.TotalDays / 3) % ClawPrizes.Length;
                    for (int i = 0; i < state.Claw.Gifts.Length; i++)
                        state.Claw.Gifts[i] = ClawPrizes[(rotation + i) % ClawPrizes.Length];
                    break;
                case LeisureGame.Billiards:
                    state.Billiards = new BilliardsPlay { OpponentName = ActivityCompanionName(activity) ?? "球友" };
                    break;
            }
        }

        private string ActivityCompanionName(CityActivity activity) => activity.Participants.Length == 0
            ? null : CityPerson(activity.Participants[0].PersonId)?.Name;

        private bool LeisureDebit(string id, string label, int yuan) => Ledger.TryDebit(Clock.Today, "休闲活动",
            Money.FromYuan(yuan), label, new LedgerAttribution(LedgerBusinessKind.Personal,
                activityId: _leisurePlay.ActivityId, sourceEventId: id, transactionKind: label));

        public bool CanFinishLeisure => LiveLeisure is LeisurePlay play
            && (play.Claw == null || !play.Claw.Animating)
            && (play.Pachinko == null || play.Pachinko.Balls.Count == 0)
            && (play.Poker == null || !play.Poker.HandActive)
            && (play.Billiards == null || !play.Billiards.Rolling);

        public bool FinishLeisure()
        {
            var state = LiveLeisure;
            if (!CanFinishLeisure || state == null) return false;
            var activity = RunningCityActivity;
            var (count, summary) = state.Game switch
            {
                LeisureGame.Pachinko => (state.Pachinko.Launched, $"发射 {state.Pachinko.Launched} 珠 · 返珠 {state.Pachinko.Returned}"),
                LeisureGame.Poker => (state.Poker.HandsPlayed, $"打完 {state.Poker.HandsPlayed} 手 · 带走筹码 ¥{state.Poker.Players[0].Stack}"),
                LeisureGame.Claw => (state.Claw.Attempts, $"抓取 {state.Claw.Attempts} 次 · 获得 {state.Claw.PrizesWon} 件"),
                LeisureGame.Billiards => (state.Billiards.Shots, $"你 {state.Billiards.PlayerScore} : {state.Billiards.OpponentScore} {state.Billiards.OpponentName}"),
                _ => (1, $"{VenueServiceName(CityActivityService(activity), 0)} · 已散场")
            };
            activity.EventSettlementNote = summary;
            int minutes = count == 0 ? 0 : activity.Duration;
            return SettleManualActivity(activity, minutes,
                count == 0 ? CityActivityPhase.Interrupted : CityActivityPhase.Completed, summary);
        }

        private void EndLeisurePlay(CityActivity activity)
        {
            var play = _leisurePlay;
            if (play?.ActivityId != activity.Id) return;
            if (play.Poker != null)
            {
                play.Poker.LeaveTable();
                int cash = play.Poker.Players[0].Stack;
                if (cash > 0)
                {
                    const string label = "德扑筹码兑回";
                    Ledger.Credit(Clock.Today, "休闲活动", Money.FromYuan(cash), label,
                        new LedgerAttribution(LedgerBusinessKind.Personal, activityId: play.ActivityId,
                            sourceEventId: play.ActivityId + ":cashout", transactionKind: label));
                }
            }
            _leisurePlay = null;
        }

        public void StepLeisure(double dt, bool held)
        {
            var play = LiveLeisure;
            if (play == null || !double.IsFinite(dt) || dt <= 0) return;
            dt = Math.Min(.10, dt);
            switch (play.Game)
            {
                case LeisureGame.Pachinko:
                    play.Pachinko.Step(dt, held, _pachinkoWallet);
                    break;
                case LeisureGame.Poker:
                    play.Poker.Step(dt, Random);
                    break;
                case LeisureGame.Billiards:
                    play.Billiards.Step(dt, held);
                    break;
                case LeisureGame.Claw:
                    if (play.Claw.Step(dt, Random) is string reward) GiveClawPrize(play.Claw, reward);
                    break;
            }
        }

        public void AimPachinko(double delta)
        {
            if (LiveLeisure?.Pachinko is PachinkoPlay state && double.IsFinite(delta))
                state.Aim = Math.Clamp(state.Aim + delta, -1, 1);
        }

        public void SelectPachinkoTier(int tier)
        {
            if (LiveLeisure?.Pachinko is PachinkoPlay state && (uint)tier < _pachinkoWallet.Length)
                state.Tier = tier;
        }

        public bool StartPachinkoMachine(int machine)
        {
            if (CurrentPlace?.Id != CityLifeCatalog.PachinkoVenue || (uint)machine >= PachinkoBoard.Machines.Length)
                return Fail("请进弹珠店，选择一台机台。");
            if (!HasPachinkoBeads) return Fail("请先买一颗弹珠。");
            var booking = PendingVenueActivity(CurrentPlace);
            if (booking != null && CityLifeCatalog.LeisureService(booking) != LeisureGame.Pachinko)
                return Fail("请先完成当前的预约。");
            bool started = booking?.ServiceConfirmed == true
                ? StartVenueAppointment(booking.Id)
                : SelectVenueService(CityLifeCatalog.PachinkoService, 0, booking?.Id);
            if (!started) return false;
            if (LiveLeisure?.Pachinko is not PachinkoPlay state)
                return Fail("本局尚未开始，请等同行者到齐后再选机台。");
            state.Machine = machine;
            return true;
        }

        public bool PokerMove(PokerAction action, int raiseTo = 0)
            => LiveLeisure?.Poker?.ActPlayer(action, raiseTo) == true;
        public void PokerNextHand() => LiveLeisure?.Poker?.NextHand(Random);

        public void AimBilliards(double x, double y)
        {
            if (LiveLeisure?.Billiards is BilliardsPlay pool && double.IsFinite(x) && double.IsFinite(y))
                pool.Aim(x, y);
        }

        public void CancelBilliardsCharge()
        {
            if (LiveLeisure?.Billiards is not { Charging: true } pool) return;
            pool.Charging = false;
            pool.Power = 0;
        }

        public void MoveClaw(double dx)
        {
            if (LiveLeisure?.Claw is GiftClawPlay claw && double.IsFinite(dx)) claw.Move(dx);
        }

        public bool PressClawButton()
        {
            if (LiveLeisure?.Claw is not GiftClawPlay claw) return false;
            if (claw.Sweeping) return claw.Drop();
            if (claw.Animating || !claw.HasGifts) return false;
            if (!LeisureDebit(_leisurePlay.ActivityId + ":claw:" + claw.Attempts, "抓娃娃", ClawPriceYuan))
                return Fail("现金不足，无法投币。");
            RunningCityActivity.EntryCostCents += Money.FromYuan(ClawPriceYuan).Cents;
            claw.Begin();
            return true;
        }

        private void GiveClawPrize(GiftClawPlay claw, string id)
        {
            var gift = Gifts.Get(id);
            if (gift == null) return;

            string prize = $"抓到了「{gift.Name}」";
            if (CanCarry(gift.WeightKg))
            {
                Backpack.AddGift(id, 1);
                ApplyCargoLimit();
                claw.PrizesWon++;
                claw.Message = prize + "，已放进背包。";
                return;
            }

            var item = new CourierItem { Kind = CourierItemKind.Gift, Id = id, Count = 1 };
            int newCars = NewCarsNeeded(item, toShop: false);
            Money freight = newCars > 0 ? CourierQuote(toShop: false).fee * newCars : Money.Zero;
            if (!Ledger.CanAfford(freight))
            {
                claw.Message = $"{prize}，现金不足以支付 {freight} 运费，已放弃这次的奖品。";
                return;
            }
            if (!Ship(null, "抓娃娃", Money.Zero, $"「{gift.Name}」", item, "抓到"))
            {
                claw.Message = prize + "，无法配送，已放弃这次的奖品。";
                return;
            }

            claw.PrizesWon++;
            claw.Message = freight.IsPositive
                ? $"{prize}，已付 {freight} 运费送到家。"
                : prize + "，已装上刚叫的车，一起送到家。";
        }

        private void SaveLeisurePlay(SaveData data)
        {
            data.leisurePlay = _leisurePlay;
            data.pachinkoWallet = (int[])_pachinkoWallet.Clone();
        }

        private void RestoreLeisurePlay(SaveData data)
        {
            _leisurePlay = data.leisurePlay;
            Array.Clear(_pachinkoWallet, 0, _pachinkoWallet.Length);
            if (data.pachinkoWallet is { Length: > 0 })
            {
                for (int i = 0; i < Math.Min(data.pachinkoWallet.Length, _pachinkoWallet.Length); i++)
                    _pachinkoWallet[i] = Math.Clamp(data.pachinkoWallet[i], 0, PachinkoWalletLimit);
            }
            else if (_leisurePlay?.Pachinko is PachinkoPlay legacy)
            {
                if (legacy.BuyInPaid && legacy.Beads > 0)
                    Ledger.Credit(Clock.Today, "休闲活动", Money.FromYuan(legacy.Beads), "旧档剩余弹珠兑回",
                        new LedgerAttribution(LedgerBusinessKind.Personal, activityId: _leisurePlay.ActivityId,
                            sourceEventId: _leisurePlay.ActivityId + ":legacy-beads", transactionKind: "旧档剩余弹珠兑回"));
                legacy.Beads = 0;
                legacy.BuyInPaid = false;
                legacy.Balls?.Clear();
            }
            if (_leisurePlay?.Pachinko is PachinkoPlay pachinko)
            {
                pachinko.Machine = Math.Clamp(pachinko.Machine, 0, PachinkoBoard.Machines.Length - 1);
                pachinko.Tier = Math.Clamp(pachinko.Tier, 0, _pachinkoWallet.Length - 1);
                pachinko.Balls ??= new System.Collections.Generic.List<PachinkoBall>();
                pachinko.Balls.RemoveAll(ball => ball == null || (uint)ball.Tier >= _pachinkoWallet.Length);
            }
            _leisurePlay?.Claw?.RestoreRound();
            // 旧档中已拿到并暂存在机台的奖品转入家中后仓。
            foreach (string gift in data.waitingClawGifts ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(gift) && Gifts.Get(gift) != null) HomeBox.AddGift(gift, 1);

            var running = RunningCityActivity;
            if (CityLifeCatalog.LeisureService(running) != LeisureGame.None) StartLeisurePlay(running);
            else _leisurePlay = null;
        }
    }

    public sealed partial class SaveData
    {
        public LeisurePlay leisurePlay;
        public int[] pachinkoWallet;
        public string[] waitingClawGifts; // 兼容旧档的机台暂存奖品。
    }
}
