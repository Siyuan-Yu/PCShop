namespace PCShopSell.Core.Session
{
    public sealed partial class SaveData
    {
        public int cityClimateVersion, cityWeatherSeed, cityClimateMoodDay = -1, cityClimateTransition;
        public int cityFixedFacilityFeesFromDay;
        public double cityClimateDiscomfort;
        public double cityTimeFraction;
        public CityClimateZone[] cityClimateZones;
    }
}
