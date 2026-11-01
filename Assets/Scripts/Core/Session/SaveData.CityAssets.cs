using System;

namespace PCShopSell.Core.Session
{
    public sealed partial class SaveData
    {
        public int cityAssetsSchema;
        public string[] cityOwnedShopPlaces;
        public CityEquityState[] cityEquities;
        public string cityEquityRandom;
        public int cityEquityLastDay;
        public long cityFundCostCents, cityFundRealizedCents;
        public bool cityFundCostEstimated;
        public long[] cityFundHistory;
        public string[] cityAssetEffects;
    }
}
