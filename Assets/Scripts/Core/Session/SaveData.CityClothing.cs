using System;
namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class CityClothingPreset { public string occasion, outfitId; }
    public sealed partial class SaveData
    {
        public int cityClothingVersion;
        public bool cityClothingAuto;
        public string cityClothingOccasion;
        public CityClothingPreset[] cityClothingPresets;
    }
}
