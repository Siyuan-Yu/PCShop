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
        public const int ClawPriceYuan = 20;
        public const int PokerBuyInYuan = 400;
        public const int PachinkoBuyInBalls = 50;

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

        private void StartLeisurePlay(CityActivity activity)
        {
            var game = CityLifeCatalog.LeisureService(activity);
            if (game == LeisureGame.None || _leisurePlay?.ActivityId == activity.Id) return;

            var state = new LeisurePlay { ActivityId = activity.Id, Game = game };
            _leisurePlay = state;
            switch (game)
            {
                case LeisureGame.Pachinko:
                    state.Pachinko = new PachinkoPlay { Beads = PachinkoBuyInBalls, Power = .58, BuyInPaid = activity.EntryCostCents > 0 };
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
                LeisureGame.Pachinko => (state.Pachinko.Launched, $"发射 {state.Pachinko.Launched} 珠 · 返珠 {state.Pachinko.Returned}"
                    + (state.Pachinko.BuyInPaid ? $" · 兑回 ¥{state.Pachinko.Beads}" : "")),
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
            int cash = play.Pachinko?.BuyInPaid == true ? play.Pachinko.Beads : 0;
            if (play.Poker != null)
            {
                play.Poker.LeaveTable();
                cash = play.Poker.Players[0].Stack;
            }
            if (cash > 0)
            {
                string label = play.Poker != null ? "德扑筹码兑回" : "柏青哥弹珠兑回";
                Ledger.Credit(Clock.Today, "休闲活动", Money.FromYuan(cash), label,
                    new LedgerAttribution(LedgerBusinessKind.Personal, activityId: play.ActivityId,
                        sourceEventId: play.ActivityId + ":cashout", transactionKind: label));
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
                    play.Pachinko.Step(dt, held, PachinkoPayoutForMachine(play.Pachinko.Machine), Random);
                    break;
                case LeisureGame.Poker:
                    play.Poker.Step(dt, Random);
                    break;
                case LeisureGame.Billiards:
                    play.Billiards.Step(dt, held);
                    break;
                case LeisureGame.Claw:
                    if (play.Claw.Step(dt) is string reward) GiveClawPrize(play.Claw, reward);
                    break;
            }
        }

        public void AimPachinko(double power)
        {
            if (LiveLeisure?.Pachinko is PachinkoPlay state && double.IsFinite(power))
                state.Power = Math.Clamp(power, 0, 1);
        }

        public void SwitchPachinkoMachine(int machine)
        {
            if (LiveLeisure?.Pachinko is PachinkoPlay state && state.Balls.Count == 0 && machine is >= 0 and < 3)
                state.Machine = machine;
        }

        public bool PokerMove(PokerAction action, int raiseTo = 0)
            => LiveLeisure?.Poker?.ActPlayer(action, raiseTo) == true;
        public void PokerNextHand() => LiveLeisure?.Poker?.NextHand(Random);

        public void AimBilliards(double x, double y)
        {
            if (LiveLeisure?.Billiards is BilliardsPlay pool && double.IsFinite(x) && double.IsFinite(y))
                pool.Aim(x, y);
        }

        public void MoveClaw(double dx, double dz)
        {
            if (LiveLeisure?.Claw is GiftClawPlay claw && double.IsFinite(dx) && double.IsFinite(dz))
                claw.Move(dx, dz);
        }

        public bool DropClaw()
        {
            if (LiveLeisure?.Claw is not GiftClawPlay claw || claw.Animating || !claw.HasGifts) return false;
            if (!LeisureDebit(_leisurePlay.ActivityId + ":claw:" + claw.Attempts, "抓娃娃", ClawPriceYuan))
                return Fail("现金不足，无法投币。");
            RunningCityActivity.EntryCostCents += Money.FromYuan(ClawPriceYuan).Cents;
            claw.Drop(Random);
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

        private void SaveLeisurePlay(SaveData data) => data.leisurePlay = _leisurePlay;

        private void RestoreLeisurePlay(SaveData data)
        {
            _leisurePlay = data.leisurePlay;
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
        public string[] waitingClawGifts; // 兼容旧档的机台暂存奖品。
    }
}
