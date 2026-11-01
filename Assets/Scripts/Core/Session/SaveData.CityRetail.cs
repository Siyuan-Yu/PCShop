using System;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class CityCompetition
    {
        public string Id, Kind, ShopId, SupplierId, ProductId, Response;
        public int StartDay, EndDay;
        public bool Announced, Ended;
        public CityCompetition Copy() => (CityCompetition)MemberwiseClone();
    }
    [Serializable]
    public sealed class CityRetailCounter { public string key; public int count; }
    public sealed partial class SaveData
    {
        public int cityRetailVersion, cityCompetitionSlot;
        public CityCompetition[] cityCompetitions;
        public CityRetailCounter[] citySupplierPurchases, cityExpoModelPurchases;
    }
}
