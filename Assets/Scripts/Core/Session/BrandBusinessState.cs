using System;
namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class BrandBusinessState
    {
        public int Version = 1, LastDay = -1;
        public BrandRecord[] Brands = Array.Empty<BrandRecord>();
        public BrandQualification[] Qualifications = Array.Empty<BrandQualification>();
        public BrandDeal[] Deals = Array.Empty<BrandDeal>();
        public BrandExhibit[] Exhibits = Array.Empty<BrandExhibit>();
        public BrandOrder[] Orders = Array.Empty<BrandOrder>();
        public BrandTender[] Tenders = Array.Empty<BrandTender>();
        public BrandResource[] Resources = Array.Empty<BrandResource>();
        public BrandCareer[] Careers = Array.Empty<BrandCareer>();
        public BrandBusinessState Copy() => new BrandBusinessState {Version=Version,LastDay=LastDay,
            Brands=Array.ConvertAll(Brands??Array.Empty<BrandRecord>(),x=>x.Copy()),
            Qualifications=Array.ConvertAll(Qualifications??Array.Empty<BrandQualification>(),x=>x.Copy()),
            Deals=Array.ConvertAll(Deals??Array.Empty<BrandDeal>(),x=>x.Copy()),
            Exhibits=Array.ConvertAll(Exhibits??Array.Empty<BrandExhibit>(),x=>x.Copy()),
            Orders=Array.ConvertAll(Orders??Array.Empty<BrandOrder>(),x=>x.Copy()),
            Tenders=Array.ConvertAll(Tenders??Array.Empty<BrandTender>(),x=>x.Copy()),
            Resources=Array.ConvertAll(Resources??Array.Empty<BrandResource>(),x=>x.Copy()),
            Careers=Array.ConvertAll(Careers??Array.Empty<BrandCareer>(),x=>x.Copy())};
    }
    [Serializable] public sealed class BrandRecord
    {
        public string Id, Name, Kind, Position;
        public int Founded, Influence, Reputation = 50;
        public string[] ShopIds=Array.Empty<string>();
        public BrandRecord Copy(){var c=(BrandRecord)MemberwiseClone();c.ShopIds=(string[])ShopIds.Clone();return c;}
    }
    [Serializable] public sealed class BrandQualification
    {
        public string Kind; public int LeadingDays, CommunityDays; public bool Invited;
        public BrandQualification Copy()=>(BrandQualification)MemberwiseClone();
    }
    [Serializable] public sealed class BrandDeal
    {
        public string Id, BrandId, PersonId, Mode, Response="continue";
        public int Start, End, Severity, NewsDay=-1, LastNegotiationDay=-2; public long Fee, SharePaid;
        public bool Exclusive, Ended;
        public BrandDeal Copy()=>(BrandDeal)MemberwiseClone();
    }
    [Serializable] public sealed class BrandCareer
    {
        public string PersonId, News="暂无新动态"; public int Severity, Momentum, Day=-1;
        public BrandCareer Copy()=>(BrandCareer)MemberwiseClone();
    }
    [Serializable] public sealed class BrandExhibit
    {
        public string Id, BrandId, ShopId, ProductId, Focus, StaffPersonId, GuestPersonId;
        public int Day, Stage, Score; public bool Finished, Missed;
        public long Started;
        public BrandExhibit Copy()=>(BrandExhibit)MemberwiseClone();
    }
    [Serializable] public sealed class BrandOrder
    {
        public string Id, BrandId, ShopId, ProductId, Source; public int Count, Due, Influence;
        public long UnitCents, Arrival; public bool Shipped, Done, Failed;
        public BrandOrder Copy()=>(BrandOrder)MemberwiseClone();
    }
    [Serializable] public sealed class BrandTender
    {
        public string Id, BrandId, ShopId, ProductId, Offer="balanced", Result;
        public int Deadline, Stage, Intel, RivalScore=72, LastRound=-1; public bool Partner;
        public BrandTender Copy()=>(BrandTender)MemberwiseClone();
    }
    [Serializable] public sealed class BrandResource
    {
        public string BrandId, Institution, Mode; public int Start, End; public long Fee;
        public BrandResource Copy()=>(BrandResource)MemberwiseClone();
    }
    public sealed partial class SaveData { public BrandBusinessState brandBusiness; }
}
