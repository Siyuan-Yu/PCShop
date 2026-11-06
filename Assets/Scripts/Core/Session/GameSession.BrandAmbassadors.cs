using System;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        // BrandDeal is a compatibility/story reference, never a second paid contract.
        private void LinkAmbassador(KolLiveContract contract, BrandDeal deal = null)
        {
            if (string.IsNullOrEmpty(contract.BrandId)) return;
            if (deal == null) deal = _brands.Deals.LastOrDefault(d => d.KolContractId == contract.Id
                && d.BrandId == contract.BrandId && (!d.Ended || contract.Ended));
            if (deal == null)
            {
                string id = "ambassador:" + contract.Id;
                if (_brands.Deals.Any(d => d.Id == id)) id += ":" + contract.ScheduleVersion;
                deal = new BrandDeal { Id = id, BrandId = contract.BrandId,
                    PersonId = contract.PersonId, Mode = "endorse", Fee = contract.FeeCents };
                _brands.Deals = AppendBrand(_brands.Deals, deal);
            }
            deal.KolContractId = contract.Id;
            deal.Start = (int)(contract.SignedAt / 1440);
            deal.End = (int)((contract.EndAt + 1439) / 1440);
            deal.Exclusive = true;
            deal.Ended = contract.Ended;
        }

        private void RestoreBrandAmbassadors()
        {
            long now = Clock.Now.TotalMinutes;
            foreach (var d in _brands.Deals.Where(d => d.Mode == "endorse" && !d.Ended && d.End > BrandDay).ToArray())
            {
                if (Brand(d.BrandId) == null || KolMarket.Get(d.PersonId) == null) continue;
                if (!string.IsNullOrEmpty(d.KolContractId))
                {
                    var existing = FindKolContract(d.KolContractId);
                    // A linked projection cannot resurrect a removed/invalid authoritative contract.
                    d.Ended = existing == null || existing.Ended || existing.BrandId != d.BrandId;
                    continue;
                }
                var c = _kol.Contracts.FirstOrDefault(x => x.BrandId == d.BrandId && x.PersonId == d.PersonId && KolContractCurrent(x, now));
                if (c == null)
                {
                    c = new KolLiveContract { Id = "legacy-ambassador:" + d.Id, BrandId = d.BrandId, PersonId = d.PersonId,
                        SignedAt = d.Start * 1440L, EndAt = d.End * 1440L, FeeCents = d.Fee,
                        LastProcessedAt = now, LastPaidThrough = now, ScheduleEffectiveAt = now,
                        FirstDay = BrandDay, Enabled = d.Response != "pause" };
                    _kol.Contracts = AppendBrand(_kol.Contracts, c);
                }
                else c.EndAt = Math.Max(c.EndAt, d.End * 1440L);
                LinkAmbassador(c, d);
                EnsureKolCareer(d.PersonId);
            }
            foreach (var c in _kol.Contracts) LinkAmbassador(c);
        }
    }
}
