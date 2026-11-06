using System;
using System.Reflection;
using PCShopSell.Core.Session;

namespace PCShopSell.Tests
{
    // Set up the office visit for unrelated streaming/exhibition tests, then restore their location.
    internal static class McnSigningTest
    {
        internal static bool Sign(GameSession s,string brand,string person) => AtReception(s,person,()=>s.SignKolLive(brand,person));
        internal static bool Endorse(GameSession s,string brand,string person,string mode,bool exclusive) =>
            AtReception(s,person,()=>s.SignBrandDeal(brand,person,mode,exclusive));
        private static bool AtReception(GameSession s,string person,Func<bool> sign)
        {
            var place=s.CurrentPlace;var date=s.Clock.Today;int minute=s.Clock.MinuteOfDay;
            try
            {
                typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,s.City.GetPlace(GameSession.CommunityMcnId));
                s.Clock.Restore(date,Math.Max(540,Math.Min(1079,minute)));
                return s.BeginMcnRecruitment(BrandAgency.ForPerson(person).Id)&&sign();
            }
            finally
            {
                s.Clock.Restore(date,minute);
                typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,place);
            }
        }
    }
}
