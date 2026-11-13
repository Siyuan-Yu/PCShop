using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum FishingPhase { Ready, Waiting, Bite, Landed, Ended, Charging, Casting, Fighting }

    [Serializable] public sealed class FishingState
    {
        public string ActivityId, PendingFishId, Message;
        public FishingPhase Phase;
        public int Spot, Bait, SpeciesRoll, Casts, Catches, Kept, Released, Gifts, Style;
        public int[] Baits = { 6, 4, 2 };
        public long BiteAt;
        public double CastPower, CastDistance, PhaseSeconds, FightSeconds, FishPosition = .5, ControlPosition = .5,
            ControlVelocity, CatchProgress, LineTension, MotionSeed;
        public int MechanicsVersion = 3;
        public string[] GiftedPeople = Array.Empty<string>();
        public FishingState Copy()
        {
            var copy = (FishingState)MemberwiseClone();
            copy.Baits = Baits?.ToArray() ?? new[] { 0, 0, 0 };
            copy.GiftedPeople = GiftedPeople?.ToArray() ?? Array.Empty<string>(); return copy;
        }
    }
    public sealed partial class GameSession
    {
        // A distinct facility inside the existing, reachable outskirts park keeps its route and old place ID.
        public const string FishingVenueId = "life_hike";
        private FishingState _fishing;
        private bool _fishingInterfaceOpen, _fishingDecisionHeld;
        private int _fishingExperience, _fishCaughtTotal;
        public FishingState CurrentFishing => _fishing?.Copy();
        public int FishingLevel => Math.Min(10, _fishingExperience / 5);
        public int FishCaughtTotal => _fishCaughtTotal;
        public static bool IsInteractiveFishingActivity(CityActivity activity) => activity?.VenueId == FishingVenueId
            && (activity.DefinitionId == "fishing" || activity.DefinitionId == "social_59" || activity.DefinitionId == "social_60" || activity.DefinitionId == "social_61");
        public bool FishingActive => _fishing != null && _fishing.Phase != FishingPhase.Ended
            && CurrentPlace?.Id == FishingVenueId && RunningCityActivity?.Id == _fishing.ActivityId && IsInteractiveFishingActivity(RunningCityActivity);
        public string FishingHint => !FishingActive ? "请到钓场选择一次垂钓服务。"
            : _fishing.Phase == FishingPhase.Charging ? "按住蓄力，松开甩竿；力度越大，落点越远。"
            : _fishing.Phase == FishingPhase.Casting ? "鱼线飞出，等待浮漂落水。"
            : _fishing.Phase == FishingPhase.Waiting ? "观察浮漂，咬钩后按一下提竿！"
            : _fishing.Phase == FishingPhase.Bite ? "咬钩了！立即按下提竿！"
            : _fishing.Phase == FishingPhase.Fighting ? "按住收线让绿区上升，松开放线让绿区下降；跟住鱼，注意张力。"
            : _fishing.Phase == FishingPhase.Landed ? "鱼已上岸，选择带走、放生或赠送。"
            : "选钓位和鱼饵，按住操作键蓄力，松开甩竿。";
        public double FishingControlSize => .24 + FishingLevel * .008
            + (_fishing?.Spot == _fishing?.Bait ? .025 : 0)
            + (RunningCityActivity?.Choices.Contains("fishing-advice") == true ? .02 : 0);
        public string[] FishingCompanions => FishingActive ? RunningCityActivity.Participants.Select(p => p.PersonId)
            .Where(person => CitySamePlace(person) && HasGreeted(person)).ToArray() : Array.Empty<string>();

        private void StartFishingActivity(CityActivity activity)
        {
            if (!IsInteractiveFishingActivity(activity)) return;
            ReleaseFishingDecision();
            _fishing = new FishingState { ActivityId = activity.Id, Phase = FishingPhase.Ready,
                Message = "本次包含租杆及 12 份鱼饵：面饵 6、蚯蚓 4、小鱼饵 2。" };
            MoveToFishingSpot(activity, 0);
        }
        private void EndFishingActivity(CityActivity activity)
        {
            if (_fishing?.ActivityId != activity.Id) return;
            ReleaseFishingDecision();
            _fishing.Phase = FishingPhase.Ended; _fishing.PendingFishId = null;
            _fishing.Message = $"本场结束，共钓获 {_fishing.Catches} 条；已带走的鱼保留在背包。";
            activity.EventSettlementNote = (activity.EventSettlementNote ?? "")
                + $"\n本次抛竿 {_fishing.Casts} 次，钓获 {_fishing.Catches} 条；带走 {_fishing.Kept}、放生 {_fishing.Released}、赠送 {_fishing.Gifts}。未收起的鱼已放回水中。";
        }
        private void TickFishing(long now)
        {
            if (!FishingActive) { ReleaseFishingDecision(); return; }
            if (_fishing.Phase == FishingPhase.Casting && !_fishingInterfaceOpen) _fishing.Phase = FishingPhase.Waiting;
            if (_fishing.Phase == FishingPhase.Waiting && now >= _fishing.BiteAt)
            {
                _fishing.Phase = FishingPhase.Bite; _fishing.PhaseSeconds = 0;
                _fishing.Message = FishingHint;
                FocusFishingDecision();
            }
            if (_fishing.Phase == FishingPhase.Bite && !_fishingInterfaceOpen && now > _fishing.BiteAt + 2)
                LoseFishingFish("没有及时提竿，鱼已经离开。");
        }
        public void SetFishingInterfaceOpen(bool open)
        {
            _fishingInterfaceOpen = open;
            if (open) FocusFishingDecision();
            else
            {
                if (_fishing?.Phase == FishingPhase.Charging) { _fishing.Phase = FishingPhase.Ready; _fishing.CastPower = 0; }
                if (_fishing?.Phase == FishingPhase.Fighting) LoseFishingFish("中断控线，鱼脱钩了；可以换饵重新抛竿。");
                ReleaseFishingDecision();
            }
        }
        private void FocusFishingDecision()
        {
            if (!_fishingInterfaceOpen || !FishingActive || _fishingDecisionHeld
                || _fishing.Phase != FishingPhase.Bite && _fishing.Phase != FishingPhase.Landed
                    && _fishing.Phase != FishingPhase.Charging && _fishing.Phase != FishingPhase.Casting && _fishing.Phase != FishingPhase.Fighting) return;
            Time.Hold(); _fishingDecisionHeld = true;
        }
        private void ReleaseFishingDecision()
        {
            if (!_fishingDecisionHeld) return;
            Time.Release(); _fishingDecisionHeld = false;
        }
        private void MoveToFishingSpot(CityActivity activity, int spot)
        {
            var venue = City.GetPlace(FishingVenueId); var door = DoorOf(venue);
            double yaw = venue.Heading * Math.PI / 180;
            double localX = new[] { -.10, .12, .34 }[spot] * venue.Width;
            double localZ = venue.Depth * .29;
            MapPoint Anchor(int seat) => new MapPoint(door.X + (localX + seat * .7) * Math.Cos(yaw) + localZ * Math.Sin(yaw),
                door.Y - (localX + seat * .7) * Math.Sin(yaw) + localZ * Math.Cos(yaw));
            CityRoute Route(MapPoint start, int seat) => new CityRoute { IntentId = activity.Id + ":fish:" + seat, TargetPlaceId = venue.Id,
                Transport = VehicleKind.Walk, Nodes = Array.Empty<string>(), DepartMinute = Clock.Now.TotalMinutes, ArriveMinute = Clock.Now.TotalMinutes + 2,
                X = new[] { start.X, Anchor(seat).X }, Y = new[] { start.Y, Anchor(seat).Y }, Meters = new[] { DistanceCity(start, Anchor(seat)) } };
            activity.PlayerVenueRoute = Route(PlayerPosition, 0);
            for (int i = 0; i < activity.Participants.Length; i++)
            {
                var participant = activity.Participants[i]; var person = CityPersonState(participant.PersonId);
                person.Route = Route(new MapPoint(person.X, person.Y), i + 1); participant.IntentId = person.Route.IntentId;
                person.TaskLabel = "到钓位一起垂钓";
            }
        }
        public bool SelectFishingSpot(int spot)
        {
            if (!FishingActive || _fishing.Phase != FishingPhase.Ready || spot < 0 || spot >= 3) return false;
            if (_fishing.Spot == spot) return true;
            _fishing.Spot = spot; MoveToFishingSpot(RunningCityActivity, spot);
            _fishing.Message = "正在前往" + FishingCatalog.Spots[spot] + "，到位后可以抛竿。"; return true;
        }
        public bool SelectFishingBait(int bait)
        {
            if (!FishingActive || _fishing.Phase != FishingPhase.Ready || bait < 0 || bait >= 3) return false;
            _fishing.Bait = bait; return true;
        }
        public string FishingCastBlocker => !FishingActive ? "请先到场开始垂钓服务"
            : _fishing.Phase != FishingPhase.Ready ? "请先处理当前鱼口或鱼获"
            : RunningCityActivity.PlayerVenueRoute?.ArriveMinute > Clock.Now.TotalMinutes ? "正在走向钓位"
            : RunningCityActivity.ActualStart + RunningCityActivity.Duration - Clock.Now.TotalMinutes < 15 ? "本场时间不足 15 分钟，请收竿或另约一场"
            : _fishing.Baits[_fishing.Bait] <= 0 ? "这种鱼饵已经用完，可换饵或购买补充"
            : Player.Energy < 5 ? "精力不足，请先休息" : null;
        public bool BeginFishingCast()
        {
            if (FishingCastBlocker is string reason) return Fail(reason);
            _fishing.Phase = FishingPhase.Charging; _fishing.PhaseSeconds = 0; _fishing.CastPower = 0;
            FocusFishingDecision(); return true;
        }
        public bool ReleaseFishingCast()
        {
            if (!FishingActive || _fishing.Phase != FishingPhase.Charging) return false;
            _fishing.CastDistance = 4 + 26 * _fishing.CastPower;
            _fishing.Baits[_fishing.Bait]--; _fishing.Casts++; Player.SpendEnergy(2);
            int wait = Random.Next(8, 13) - (_fishing.Spot == _fishing.Bait ? 2 : 0);
            _fishing.BiteAt = Clock.Now.TotalMinutes + wait;
            _fishing.SpeciesRoll = Random.Next(0, 10000); _fishing.MotionSeed = Random.Next(0, 10000) / 100.0;
            _fishing.Phase = FishingPhase.Casting; _fishing.PhaseSeconds = 0;
            _fishing.Message = $"甩出 {_fishing.CastDistance:0.0} 米，消耗一份{FishingCatalog.Baits[_fishing.Bait]}。";
            return true;
        }
        public bool HookFishingFish()
        {
            if (!FishingActive || _fishing.Phase != FishingPhase.Bite) return false;
            _fishing.PendingFishId = FishingCatalog.Pick(_fishing.CastDistance, _fishing.Spot, _fishing.Bait, _fishing.SpeciesRoll).Id;
            _fishing.Phase = FishingPhase.Fighting; _fishing.PhaseSeconds = 0; _fishing.FightSeconds = 0;
            _fishing.ControlPosition = .5; _fishing.ControlVelocity = 0; _fishing.FishPosition = .5;
            _fishing.CatchProgress = .3; _fishing.LineTension = .15;
            _fishing.Message = "鱼在挣扎！让绿区跟住鱼，张力太高就松手放线。"; FocusFishingDecision(); return true;
        }
        // Real seconds, independent of the world's clock speed. No random success roll.
        public void AdvanceFishingInteraction(double seconds, bool reelHeld)
        {
            if (!FishingActive || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
            double remaining = Math.Min(seconds, 1);
            while (remaining > .000001)
            {
                double dt = Math.Min(.02, remaining); remaining -= dt;
                _fishing.PhaseSeconds += dt;
                if (_fishing.Phase == FishingPhase.Charging)
                {
                    double cycle = _fishing.PhaseSeconds * .72 % 2;
                    _fishing.CastPower = cycle <= 1 ? cycle : 2 - cycle;
                }
                else if (_fishing.Phase == FishingPhase.Casting && _fishing.PhaseSeconds >= .8)
                { _fishing.Phase = FishingPhase.Waiting; _fishing.PhaseSeconds = 0; ReleaseFishingDecision(); }
                else if (_fishing.Phase == FishingPhase.Bite && _fishing.PhaseSeconds >= 3)
                    LoseFishingFish("提竿太迟，鱼吐出了钩；这一份鱼饵已经用掉。");
                else if (_fishing.Phase == FishingPhase.Fighting)
                {
                    _fishing.FightSeconds += dt;
                    var fish = FishingCatalog.Get(_fishing.PendingFishId);
                    double target = fish.Target(_fishing.FightSeconds, _fishing.MotionSeed);
                    double speed = fish.Speed(_fishing.FightSeconds, _fishing.MotionSeed);
                    _fishing.FishPosition += Math.Max(-dt * speed, Math.Min(dt * speed, target - _fishing.FishPosition));
                    _fishing.ControlVelocity = Math.Max(-.5, Math.Min(.5, _fishing.ControlVelocity + (reelHeld ? 1.3 : -1.05) * dt));
                    double half = FishingControlSize / 2;
                    _fishing.ControlPosition = Math.Max(half, Math.Min(1 - half, _fishing.ControlPosition + _fishing.ControlVelocity * dt));
                    if (_fishing.ControlPosition <= half || _fishing.ControlPosition >= 1 - half) _fishing.ControlVelocity = 0;
                    bool inside = Math.Abs(_fishing.FishPosition - _fishing.ControlPosition) <= half;
                    _fishing.CatchProgress = Math.Max(0, Math.Min(1, _fishing.CatchProgress + dt * (inside ? .115 : -.095)));
                    _fishing.LineTension = Math.Max(0, Math.Min(1, _fishing.LineTension + dt * (reelHeld ? (inside ? .045 : .17) : -.22)));
                    if (_fishing.LineTension >= 1) LoseFishingFish("收线太猛，鱼线拉断了；适时松开放线再试。");
                    else if (_fishing.CatchProgress <= 0 || _fishing.FightSeconds >= 60) LoseFishingFish("没有跟住鱼的挣扎，鱼脱钩了。");
                    else if (_fishing.CatchProgress >= 1) LandFishingFish();
                }
            }
        }
        private void LandFishingFish()
        {
            var fish = FishingCatalog.Get(_fishing.PendingFishId);
            _fishing.Phase = FishingPhase.Landed; _fishing.Catches++; _fishCaughtTotal++; _fishingExperience += 2;
            _fishing.Message = $"钓到{fish.Name} · {fish.Rarity} · {fish.Kg:0.##} 千克 · 回收 ¥{fish.SalePrice}"; FocusFishingDecision();
        }
        private void LoseFishingFish(string reason)
        {
            _fishing.Phase = FishingPhase.Ready; _fishing.PendingFishId = null; _fishing.Message = reason; ReleaseFishingDecision();
        }
        public bool KeepCaughtFish()
        {
            var fish = FishingActive && _fishing.Phase == FishingPhase.Landed ? FishingCatalog.Get(_fishing.PendingFishId) : null;
            if (fish == null) return false;
            if (!CanCarry(fish.Kg)) return false;
            Backpack.Food.AddIngredient(fish.Id, 1); _fishing.Kept++; ApplyCargoLimit();
            FinishFishDisposition("已把" + fish.Name + "放入背包食材，可做饭、储存、送人或到钓场回收。" ); return true;
        }
        public bool ReleaseCaughtFish()
        {
            if (!FishingActive || _fishing.Phase != FishingPhase.Landed) return false;
            _fishing.Released++; Player.Mood = Math.Min(100, Player.Mood + 1);
            FinishFishDisposition("鱼已放回水中，心情 +1。" ); return true;
        }
        public bool GiveCaughtFish(string personId)
        {
            var fish = FishingActive && _fishing.Phase == FishingPhase.Landed ? FishingCatalog.Get(_fishing.PendingFishId) : null;
            if (fish == null || !FishingCompanions.Contains(personId)) return false;
            var days = Npcs.Get(personId) != null ? _giftDays : _staffGiftDay;
            if (_fishing.GiftedPeople.Contains(personId) || days.TryGetValue(personId, out int last) && Clock.Today.TotalDays - last < 3)
                return Fail("这位同行者最近已收过礼物，换位同行者或自行带走。" );
            days[personId] = Clock.Today.TotalDays;
            _fishing.GiftedPeople = _fishing.GiftedPeople.Concat(new[] { personId }).ToArray();
            AddRelation(personId, Math.Min(5, 2 + fish.SalePrice / 200));
            RememberSocial(personId, "在青湾垂钓场当面收到你钓到的" + fish.Name + "。" );
            _fishing.Gifts++; FinishFishDisposition("已把" + fish.Name + "送给" + CityPerson(personId)?.Name + "，不会再加入背包。" ); return true;
        }
        private void FinishFishDisposition(string message)
        {
            _fishing.PendingFishId = null; _fishing.Phase = FishingPhase.Ready; _fishing.Message = message; ReleaseFishingDecision();
        }
        public bool BuyFishingBait(int bait)
        {
            if (!FishingActive || _fishing.Phase != FishingPhase.Ready || bait < 0 || bait >= 3) return false;
            int price = bait == 0 ? 100 : bait == 1 ? 200 : 500;
            if (!Ledger.TryDebit(Clock.Today, "钓鱼补饵", Money.FromYuan(price), FishingCatalog.Baits[bait] + " ×5")) return Fail("补饵现金不足");
            _fishing.Baits[bait] += 5; return true;
        }
        public bool SellFishingCatch(string fishId, int count)
        {
            var fish = FishingCatalog.Get(fishId);
            if (fish == null || count <= 0 || CurrentPlace?.Id != FishingVenueId || !IsPlaceOpen(CurrentPlace)
                || IsGameOver || Racing || IsWorking || FishingActive && _fishing.Phase != FishingPhase.Ready) return false;
            if (Backpack.Food.IngredientCount(fishId) < count) return Fail("背包里没有这么多鱼获");
            int remaining = count;
            foreach (var batch in Backpack.Food.Ingredient.Where(b => b.Id == fishId).ToArray())
            { int take = Math.Min(remaining, batch.Count); batch.Count -= take; remaining -= take; if (batch.Count == 0) Backpack.Food.Ingredient.Remove(batch); }
            Ledger.Credit(Clock.Today, "鱼获出售", Money.FromYuan((long)fish.SalePrice * count), fish.Name + " ×" + count);
            ApplyCargoLimit(); return true;
        }
        private void SaveFishing(SaveData data)
        {
            data.fishing = _fishing?.Copy(); data.fishingExperience = _fishingExperience; data.fishCaughtTotal = _fishCaughtTotal;
        }
        private void RestoreFishing(SaveData data)
        {
            _fishing = data.fishing?.Copy();
            _fishingExperience = Math.Max(0, data.fishingExperience); _fishCaughtTotal = Math.Max(0, data.fishCaughtTotal);
            if (_fishing != null && (_fishing.Baits.Length != 3 || !_cityActivities.Any(a => a.Id == _fishing.ActivityId && a.Active && IsInteractiveFishingActivity(a))))
                _fishing = null;
            _fishingInterfaceOpen = false; _fishingDecisionHeld = false;
            if (_fishing != null)
            {
                _fishing.Spot = Math.Max(0, Math.Min(2, _fishing.Spot)); _fishing.Bait = Math.Max(0, Math.Min(2, _fishing.Bait));
                for (int i = 0; i < 3; i++) _fishing.Baits[i] = Math.Max(0, _fishing.Baits[i]);
                if (_fishing.MechanicsVersion < 2 && _fishing.Phase != FishingPhase.Landed) _fishing.Phase = FishingPhase.Ready;
                if (_fishing.MechanicsVersion < 3 && (_fishing.Phase == FishingPhase.Waiting || _fishing.Phase == FishingPhase.Bite)) _fishing.SpeciesRoll *= 100;
                _fishing.MechanicsVersion = 3;
                if (_fishing.Phase == FishingPhase.Charging) _fishing.Phase = FishingPhase.Ready;
                if ((_fishing.Phase == FishingPhase.Landed || _fishing.Phase == FishingPhase.Fighting) && FishingCatalog.Get(_fishing.PendingFishId) == null) _fishing.Phase = FishingPhase.Ready;
            }
        }
    }
    public sealed partial class SaveData
    {
        public FishingState fishing;
        public int fishingExperience, fishCaughtTotal;
    }
}
