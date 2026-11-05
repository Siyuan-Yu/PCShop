using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public long KolDailyWage(string person)=>200L+KolFollowers(person)/100+KolPopularity(person)*5L;
        private void AccrueKolWages(KolLiveContract c,long until)
        {
            if(c.Ended)return;
            long through=Math.Min(until,c.EndAt),from=Math.Max(c.SignedAt,c.LastPaidThrough);
            if(through<=from)return;
            long cents=(long)Math.Ceiling(KolDailyWage(c.PersonId)*100.0*(through-from)/1440);
            if(cents>0)Ledger.ForceDebit(Clock.Today,"KOL日薪",Money.FromCents(cents),
                (CityPerson(c.PersonId)?.Name??c.PersonId)+" · 按签约时长日结（暂停直播仍计薪）",
                new LedgerAttribution(LedgerBusinessKind.Operating,propertyId:c.BrandId,sourceEventId:"kol:wage:"+c.Id+":"+through));
            c.LastPaidThrough=through;c.WagesPaidCents+=cents;
        }
        private void SettleKolDailyWages(long until)
        {foreach(var c in _kol.Contracts)AccrueKolWages(c,until);}
    }
}
