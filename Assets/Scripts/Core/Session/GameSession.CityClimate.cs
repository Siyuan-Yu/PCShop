using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private int _weatherSeed=173971, _climateMoodDay=-1;
        private int _fixedFacilityFeesFromDay;
        // Compatibility surface only. No zones, meters, controllers or thermal state exist at runtime.
        public IReadOnlyCollection<CityClimateZone> CityClimateZones => Array.Empty<CityClimateZone>();
        public Money CityClimateAccrued => Money.Zero;
        public Money CityClimatePaymentsWithin(int days=7) => Money.Zero;
        public double CityOutdoorTemperature(long minute) => CityWeather.Temperature(_weatherSeed,minute);
        public double CityAirTemperature => CityOutdoorTemperature(Clock.Now.TotalMinutes);
        public double CityFeltTemperature => SimpleFeltAt(CurrentPlace?.Id,CityAirTemperature);
        public string WeatherSummary => CityWeather.Season(Clock.Now.TotalMinutes)+" · "+CityAirTemperature.ToString("0")+"℃";
        public string CityComfortSummary => SimpleComfortAt(CurrentPlace?.Id,OutfitComfortMin,OutfitComfortMax);
        public double CitySleepComfortFactor => 1;
        public bool HasSeasonalFan(string placeId) => FacilityFurniture(placeId).Any(p=>p.Id=="fan_fixed"||p.Id=="fan_new");
        public bool HasSeasonalAircon(string placeId)
        {
            if(string.IsNullOrEmpty(placeId))return false;
            var shop=ShopAt(placeId);
            if(shop?.Rented==true&&shop.Upgrades.Contains(ShopUpgrades.AirCon))return true;
            if(FacilityFurniture(placeId).Any(p=>p.Id=="aircon"||p.Id=="aircon_large"))return true;
            return City.HasPlace(placeId)&&new[]{"venue_restaurant","venue_ktv","venue_bar","venue_expo"}.Contains(City.GetPlace(placeId).Business);
        }
        private IEnumerable<PlacedFurniture> FacilityFurniture(string placeId)
        {
            if(string.IsNullOrEmpty(placeId))return Array.Empty<PlacedFurniture>();
            if(placeId==Home.Id)return _homeLayout;
            var shop=ShopAt(placeId);return shop?.Rented==true?(IEnumerable<PlacedFurniture>)shop.Layout:Array.Empty<PlacedFurniture>();
        }
        private double SimpleFeltAt(string placeId,double outdoor)
        {
            if(string.IsNullOrEmpty(placeId))return outdoor;
            if(HasSeasonalAircon(placeId))return Math.Max(20,Math.Min(26,outdoor));
            if(outdoor>26&&HasSeasonalFan(placeId))return outdoor-Math.Min(2.5,outdoor-25);
            return outdoor;
        }
        private string SimpleComfortAt(string placeId,double minimum,double maximum)
        {
            double felt=SimpleFeltAt(placeId,CityAirTemperature);
            return felt<minimum-1?"偏冷：普通厚衣更合适":felt>maximum+1?"偏热：轻薄衣物更合适":"体感舒适";
        }
        public string CityShopClimateSummary(string placeId)
        {
            bool ac=HasSeasonalAircon(placeId),fan=HasSeasonalFan(placeId);
            return (ac?"空调改善室内冷暖":fan?"风扇热天改善舒适":"设施随季节发挥作用")+" · "+SimpleComfortAt(placeId,18,29);
        }
        private double HomeSeasonalFacilityBonus
        {
            get {double outdoor=CityAirTemperature;return HasSeasonalAircon(Home.Id)&&(outdoor<18||outdoor>27)?.12:outdoor>27&&HasSeasonalFan(Home.Id)?.05:0;}
        }
        public void InitializeCityClimate() { } // Legacy callers only; deliberately no simulation.
        public bool SetCityClimateDevice(string placeId,string deviceId,CityClimateMode mode,double setpoint) => false;
        public string CityWeatherForecast
        {
            get {var lines=new List<string>();for(int day=0;day<3;day++){long start=(Clock.Now.TotalMinutes/1440+day)*1440;var ts=Enumerable.Range(0,24).Select(h=>CityOutdoorTemperature(start+h*60)).ToArray();lines.Add(new[]{"今天","明天","后天"}[day]+$" {ts.Min():0}—{ts.Max():0}℃ · "+(ts.Max()>27?"推荐轻薄衣物":ts.Min()<15?"推荐普通保暖衣物":"普通换季衣物"));}return string.Join("\n",lines);}
        }
        public void TickCityClimate(long from,long to)
        {
            if(to<=from)return;
            // Only a lightweight seasonal facility query for customer patience; no room integration.
            double outdoor=CityAirTemperature;
            foreach(var shop in _shops.Where(s=>s.Rented))
            {
                int basic=_basePatience+shop.Layout.Select(p=>FurnitureCatalog.Get(p.Id)?.PatienceBonus??0).Sum();
                double felt=SimpleFeltAt(shop.Place.Id,outdoor);bool suitable=felt>=18&&felt<=29;
                shop.Sim.Settings.PatienceMinutes=basic+(suitable&&HasSeasonalAircon(shop.Place.Id)&&(outdoor<18||outdoor>27)?5:!suitable?-(int)Math.Floor(basic*.1):0);
            }
        }
        public void SaveCityClimate(SaveData d)
        {
            d.cityTimeFraction=Time.FractionalMinute;d.cityClimateVersion=2;d.cityWeatherSeed=_weatherSeed;
            d.cityClimateMoodDay=_climateMoodDay;d.cityFixedFacilityFeesFromDay=_fixedFacilityFeesFromDay;
            d.cityClimateZones=Array.Empty<CityClimateZone>(); // old readings remain in historical ledger, not replayable meters.
        }
        public void LoadCityClimate(SaveData d)
        {
            Time.RestoreFractionalMinute(d.cityTimeFraction);
            _weatherSeed=d.cityClimateVersion>0?d.cityWeatherSeed:173971;_climateMoodDay=d.cityClimateMoodDay;
            _fixedFacilityFeesFromDay=d.cityClimateVersion==1?(Clock.Today.TotalDays/30+1)*30:Math.Max(0,d.cityFixedFacilityFeesFromDay);
        }
        private void FinishCityClimateSleep(int minutes)
        {
            if(minutes<120||_climateMoodDay==Clock.Today.TotalDays)return;
            _climateMoodDay=Clock.Today.TotalDays;
            double felt=SimpleFeltAt(CurrentPlace?.Id,CityAirTemperature);
            Player.Mood=Math.Max(0,Math.Min(100,Player.Mood+(felt<16||felt>29?-1:1)));
        }
    }
}
