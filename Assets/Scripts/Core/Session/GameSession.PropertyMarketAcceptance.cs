using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private void PreparePropertyMarketAcceptance()
        {
            _district.UnlockedTier = 3;
            DebugAddCash(6000000);

            // Quote samples for this explicit fixture, without replaying life or business days.
            int today = Clock.Today.TotalDays, minute = Clock.MinuteOfDay;
            _priceDays.Clear();
            _propertyPriceWeek = (today - 7) / 7;
            _nextPriceReportDay = today;
            try
            {
                for (int day = today - 7; day <= today; day++)
                {
                    Clock.Restore(new GameDate(day), minute);
                    TickPropertyMarket();
                    RecordPriceDay();
                }
            }
            finally { Clock.Restore(new GameDate(today), minute); }

            void BuyShop(string id)
            {
                var place = City.GetPlace(id);
                CurrentPlace = AgencyFor(place);
                if (!BuyProperty(id, false)) throw new InvalidOperationException("物业验收全款购铺失败：" + id);
            }
            BuyShop(MainShop.Place.Id);
            BuyShop("shop01");
            BuyShop("life_unit_s_market_0");
            BuyShop("life_unit_s_market_2");

            var residence = ResidencePlace(1);
            CurrentPlace = AgencyFor(residence);
            if (!BuyResidentialHome(residence.Id, false, true))
                throw new InvalidOperationException("物业验收全款购入自住房失败");
            var rentedHome = ResidentialPlaces.Where(p => p != Home && ResidentialTenants(p.Id).Length > 0)
                .OrderBy(ResidentialTier).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            if (rentedHome == null) throw new InvalidOperationException("物业验收缺少有实际NPC住户的住宅");
            CurrentPlace = AgencyFor(rentedHome);
            if (!BuyResidentialHome(rentedHome.Id, false, false))
                throw new InvalidOperationException("物业验收购入带住户住宅失败");

            SetFocus(MainShop);
            ArriveAtNode(MainShop.Place.NodeId);
            CurrentPlace = MainShop.Place;
            MainShop.Sim.PlayerPresent = true;
            Flags.Add("debug:property-market-acceptance");
            WeeklyPricesPending = WeeklyPricesSubscribed;
            Say("物业与物价验收：4处商铺、1套自住房、1套带NPC住户住宅；包含自用、出租、空置，全部全款。已准备8日房产报价样本，打开物价简报即可看四项涨跌；下次周更新仍走正常规则。");
        }
    }
}
