using PCShopSell.Core.Common;
using System.Linq;
#if UNITY_EDITOR
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
#endif

namespace PCShopSell.Core.Session
{
    /// <summary>验收用的快捷操作（只有编辑器里的作弊工具会调用）。</summary>
    public sealed partial class GameSession
    {
        private const string InvitationTestEnabledFlag="debug:invitation-test-person";
        private const string HomeWorkbenchTestFlag="debug:home-workbench";
        public bool DebugHomeWorkbenchEnabled
        {
            get
            {
#if UNITY_EDITOR
                return Flags.Contains(HomeWorkbenchTestFlag);
#else
                return false;
#endif
            }
        }
        public void DebugSetHomeWorkbench(bool enabled)
        {
#if UNITY_EDITOR
            if(enabled)Flags.Add(HomeWorkbenchTestFlag);else Flags.Remove(HomeWorkbenchTestFlag);
#endif
        }
        public bool DebugInvitationPersonEnabled
        {
            get
            {
#if UNITY_EDITOR
                return Flags.Contains(InvitationTestEnabledFlag);
#else
                return false;
#endif
            }
        }

        public void DebugSetInvitationPerson(bool visible)
        {
            if(visible)Flags.Add(InvitationTestEnabledFlag); else Flags.Remove(InvitationTestEnabledFlag);
            InitializeCityPeople();
            Say(DebugInvitationPersonEnabled?"小游已出现在安居房产中介附近，好感已满，可用于邀约与聊天验收。":"小游已隐藏；相关验收邀约已结束，历史记录保留。");
        }

        private void RemoveInvitationTestPerson()
        {
            string id=CityLifeCatalog.InvitationTestPersonId;
            foreach(var activity in _cityActivities.Where(a=>a.Active && a.Participants.Any(p=>p.PersonId==id)).ToArray())
                EndCityActivity(activity,CityActivityPhase.Cancelled,"已通过作弊工具移除验收伙伴，结束本次验收安排",Clock.Now.TotalMinutes);
            _cityPeople.Remove(id);
            _relations.Remove(id);
            Flags.Remove(MetFlag(id)); Flags.Remove("greeted:"+id); Flags.Remove("heard:"+id);
        }

        public void DebugAddCash(long yuan)
        {
            Ledger.Credit(Clock.Today, "作弊", Money.FromYuan(yuan), "作弊工具");
            Say($"作弊：现金 +¥{yuan:N0}。");
        }

        /// <summary>把当前任务直接记为完成，照常发奖励。</summary>
        public bool DebugCompleteQuest()
        {
            QuestDef q = CurrentQuest();
            if (q == null) return false;
            Flags.Add("tutorial:issued:" + q.Id);
            Flags.Add("tutorial:" + q.Id);
            q.Grant?.Invoke(this);
            Say($"作弊：任务「{q.Title}」直接完成，奖励：{q.RewardText(this)}。");
            return true;
        }

        public void DebugAddReputation(double delta)
        {
            if (!HasShop) return;
            PlayerShop.Reputation += delta;
            Say($"作弊：名望 {PlayerShop.Reputation:0.0}。");
        }

        /// <summary>所有有交情的人都认识、交情 +delta；店员和街坊的熟络度也 +delta。</summary>
        public void DebugRaiseAllRelations(double delta)
        {
            InitializeCityPeople();
            var staffIds = Rosters.All.Select(staff => staff.Id)
                .Concat(Residents.All.Select(resident => resident.Person.Id)).ToHashSet();
            var personIds = Npcs.All.Select(npc => npc.Id)
                .Concat(CityPeople.Select(person => person.PersonId))
                .Concat(staffIds).Distinct();
            foreach (string id in personIds)
            {
                Meet(id, 0);
                if (staffIds.Contains(id)) AddStaffRapport(id, delta);
                else AddRelation(id, delta);
            }
            Say($"作弊：所有人交情 / 熟络 +{delta:0}。");
        }

        /// <summary>时间往后拨几个小时（照常结算营业、饥饿等）。</summary>
        public void DebugSkipHours(int hours)
        {
            if (hours > 0) AdvanceClock(hours * 60);
        }

#if UNITY_EDITOR
        private const int RetiredRelativeEventNumber = 102;
        public const int DebugLastEventNumber = 126;
        public bool DebugEventAcceptanceSession => IsCityLifeTest && Flags.Contains("debug:event-acceptance");
        public bool DebugEventAcceptanceReady => DebugEventAcceptanceSession && PendingEvent == null && !IsGameOver
            && !Racing && !IsWorking && !_sleeping && !CityActivityExclusive && !BrandExpoExclusive;

        public static EventDef DebugNumberedEvent(int number)
        {
            if (number < 1 || number > DebugLastEventNumber) return null;
            if (number == RetiredRelativeEventNumber) return null;
            if (number == 125) return new EventDef { Id = LeisureInvitationId, Title = "熟人约你出去走走" };
            if (number == 126) return new EventDef { Id = "friend-bailout", Title = "林远航来了" };
            int index = number - 1 - (number > RetiredRelativeEventNumber ? 1 : 0);
            return EventCatalog.All.ElementAtOrDefault(index);
        }

        private void PrepareEventAcceptance()
        {
            // Keep all effects in the isolated test session. Mainline introductions remain locked.
            DebugRentStarterShop(computerScenario: true);
            Flags.Add("debug:event-acceptance");
            SetOpeningShopKind("computer");
            var shop = MainShop;
            SetFocus(shop);
            shop.Layout.Clear();
            FitStarterShop();
            shop.RenovationDay = -1;
            shop.Sim.ForcedClosed = false;
            shop.Sim.SetHours(0, 1440);
            shop.Sim.Reputation = 25;
            Player.Charm = 55;
            Player.Energy = 100;
            Player.Health = 100;
            Clock.Restore(new GameDate(19), 11 * 60); // Saturday, so the weekend work event can also be tried.
            ArriveAtNode(shop.Place.NodeId);
            CurrentPlace = shop.Place;
            shop.Sim.PlayerPresent = true;
            Meet(Npcs.Zhang, 45);
            Meet(ClassmateId, 60);
            Flags.Add(RacingAccessFlag);
            Flags.Add(RacingIntroShownFlag);
            PrepareEventStock(shop);
        }

        private void PrepareEventStock(OwnedShop shop)
        {
            foreach (var part in new[] { PartCatalog.Get("ram0"), PartCatalog.Get("ram1") })
            {
                int missing = 12 - shop.Sim.Stock.Count(part, true);
                if (missing > 0) shop.Sim.Stock.ForceAdd(part, true, missing);
            }
            var capacities = PartCatalog.Categories.ToDictionary(category => ShelfKinds.ForCategory(category).Id, _ => 60d);
            shop.Sim.SetShelfCapacity(capacities);
        }

        public bool DebugRaiseNumberedEvent(int number)
        {
            if (!DebugEventAcceptanceReady || DebugNumberedEvent(number) is not EventDef template) return false;
            if (number >= 31 && number <= 40) PrepareRacingBatchAcceptance(number);
            if (number >= 41 && number <= 50) PrepareSocialBatchAcceptance(number);
            if (number >= 61 && number <= 70) _district.UnlockedTier = 3;
            if (number >= 73 && number <= 77) _district.UnlockedTier = 3;
            var shop = MainShop;
            if (number is 2 or 4 or 5 or 6 or 11)
            {
                if (shop.EmergencyLighting.PowerOutAt(Clock.Now.TotalMinutes))
                    AdvanceClock((int)(shop.EmergencyLighting.OutageEndsAt - Clock.Now.TotalMinutes));
                if (IsGameOver) return false;
                PrepareEventStock(shop);
            }
            if (number == 13) Player.Health = System.Math.Min(75, Player.Health);
            if (template.Id == "money_parttime_offer" && !CanStartWeekendWork)
            {
                if (Trip != null) StopTrip();
                int day = Clock.Today.TotalDays;
                if (Clock.MinuteOfDay > 15 * 60 + 59) day++;
                while (!new GameDate(day).IsWeekend) day++;
                long start = GameTimestamp.From(new GameDate(day), 11 * 60).TotalMinutes;
                if (start > Clock.Now.TotalMinutes) AdvanceClock((int)(start - Clock.Now.TotalMinutes));
            }
            if (number == 6 && EventPromotion() == null)
            {
                // A prepared promotion uses a named shop from the real map even if the test shop
                // is outside the distance that would normally make it a candidate.
                var rival = LocalPromotionRival(shop) ?? City.Places.FirstOrDefault(p =>
                    p.Kind == PlaceKind.Shop && p.Business == "电脑" && ShopAt(p) == null && !IsVacant(p));
                if (rival != null)
                {
                    int day = Clock.Today.TotalDays;
                    _cityCompetitions.RemoveAll(competition => competition.Id == "debug:event-promotion");
                    _cityCompetitions.Add(new CityCompetition { Id = "debug:event-promotion", Kind = "promotion",
                        ShopId = shop.Place.Id, RetailKind = shop.RetailKind, RivalPlaceId = rival.Id, RivalName = rival.Name,
                        ProductId = "ram0", StartDay = day, EndDay = day + 3 });
                    ApplyCityRetailDemand();
                }
            }
            if (number == 125)
            {
                var friend = CityPerson(Npcs.Zhang);
                var invitation = friend == null ? null : BuildLeisureInvitation(friend);
                if (invitation != null) Raise(invitation, Clock.Today);
                return invitation != null;
            }
            if (number == 126)
            {
                Meet(Npcs.Lin, NpcPerks.LinBailout);
                return TryFriendBailout();
            }
            Raise(template, Clock.Today);
            return PendingEvent != null;
        }
#endif
    }
}
