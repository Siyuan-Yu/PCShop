using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class CityClimateTests
    {
        private static GameSession Make() => new GameSession(CityLayout.FromData(GameSessionTests.Layout()),812,Money.FromYuan(100000),GameTimestamp.From(new GameDate(100),900),false);
        [Test] public void SeasonalAcceptancePresetsCreateIndependentDatesWithoutAdvancingAnExistingGame()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            string[] keys={"clothing_spring","clothing_summer","clothing_autumn","clothing"};
            string[] seasons={"春","夏","秋","冬"};int[] dates={44,134,224,279};
            for(int i=0;i<keys.Length;i++)using(var s=GameSession.CreateCityLifePreset(city,keys[i]))
            {
                Assert.True(s.IsCityLifeTest);Assert.AreEqual(dates[i],s.Clock.Today.TotalDays);
                Assert.AreEqual(seasons[i],CityWeather.Season(s.Clock.Now.TotalMinutes));
                Assert.AreEqual("clothing",s.MainShop.RetailKind);Assert.AreEqual(0,s.CityActivities.Count);
                Assert.AreEqual(Money.FromYuan(150000),s.Ledger.Balance,"The start date does not back-charge nine months of bills");
            }
        }
        [Test] public void WeatherIsSeededSmoothAndCyclicAcrossYearBoundary()
        {
            for(long minute=0;minute<520000;minute+=137)
            {
                double a=CityWeather.Temperature(918,minute);
                Assert.AreEqual(a,CityWeather.Temperature(918,minute));
                Assert.Less(Math.Abs(a-CityWeather.Temperature(918,minute+1)),.1);
            }
            Assert.AreEqual("春",CityWeather.Season(0));Assert.AreEqual("夏",CityWeather.Season(90*1440));
            Assert.AreEqual("秋",CityWeather.Season(180*1440));Assert.AreEqual("冬",CityWeather.Season(270*1440));
        }
        [Test] public void NoRoomOrMeterSimulationCanBeCreatedByReadingOrAdvancing()
        {
            using(var s=Make())
            {
                var cash=s.Ledger.Balance;s.InitializeCityClimate();s.TickCityClimate(0,1440*30);
                Assert.AreEqual(0,s.CityClimateZones.Count);Assert.AreEqual(cash,s.Ledger.Balance);
                Assert.AreEqual(Money.Zero,s.CityClimateAccrued);Assert.False(s.SetCityClimateDevice(s.Home.Id,"any",CityClimateMode.Manual,18));
                Assert.False(s.WeatherSummary.Contains("内"));Assert.False(s.CityShopClimateSummary(s.MainShop.Place.Id).Contains("kWh"));
            }
        }
        [Test] public void FansAreLocalHotOnlyCappedAndDoNotChangeAirTemperature()
        {
            using(var s=Make())
            {
                s.Clock.Restore(new GameDate(150),900);var layout=(System.Collections.Generic.List<PlacedFurniture>)s.HomeLayout;
                double air=s.CityAirTemperature;layout.Add(new PlacedFurniture{Id="fan_broken"});Assert.False(s.HasSeasonalFan(s.Home.Id));
                layout.Add(new PlacedFurniture{Id="fan_new"});double felt=s.CityFeltTemperature;
                Assert.That(air-felt,Is.GreaterThan(0).And.LessThanOrEqualTo(2.5));Assert.AreEqual(air,s.CityAirTemperature);
                for(int i=0;i<10;i++)layout.Add(new PlacedFurniture{Id="fan_new"});Assert.AreEqual(felt,s.CityFeltTemperature);
                s.Leave();Assert.AreEqual(air,s.CityFeltTemperature);
                s.ArriveAtNode(s.Home.NodeId);Assert.True(s.Enter(s.Home));s.Clock.Restore(new GameDate(310),180);
                Assert.AreEqual(s.CityAirTemperature,s.CityFeltTemperature,"A fan cannot warm winter air");
            }
        }
        [Test] public void InstalledAirconWorksOnlyInItsOwnPlaceAndRemoteOverviewCannotMoveComfort()
        {
            using(var s=Make())
            {
                s.Clock.Restore(new GameDate(150),900);var layout=(System.Collections.Generic.List<PlacedFurniture>)s.HomeLayout;
                layout.Add(new PlacedFurniture{Id="aircon"});Assert.AreEqual(26,s.CityFeltTemperature);
                s.GetCityCashReserve();Assert.AreEqual(26,s.CityFeltTemperature);Assert.AreEqual(s.Home,s.CurrentPlace);
                s.Leave();Assert.AreEqual(s.CityAirTemperature,s.CityFeltTemperature);
                layout.Clear();s.ArriveAtNode(s.Home.NodeId);Assert.True(s.Enter(s.Home));Assert.AreEqual(s.CityAirTemperature,s.CityFeltTemperature);
            }
        }
        [Test] public void LegacyThermalSaveKeepsAssetsAndCashButDiscardsControllersAndDefersFixedFee()
        {
            using(var s=Make())
            {
                var layout=(System.Collections.Generic.List<PlacedFurniture>)s.HomeLayout;long basic=s.HomeUtilities.Cents;
                layout.Add(new PlacedFurniture{Id="fan_fixed"});layout.Add(new PlacedFurniture{Id="aircon"});
                var old=s.CreateSave();old.cityClimateVersion=1;old.cityClimateZones=new[]{new CityClimateZone{PlaceId=s.Home.Id,Air=80,Devices=new System.Collections.Generic.List<CityClimateDevice>{new CityClimateDevice{Kind="aircon",Kwh=999,Mode=CityClimateMode.Manual,Installed=true}}}};
                var cash=s.Ledger.Balance;s.LoadCityClimate(old);Assert.AreEqual(cash,s.Ledger.Balance);Assert.AreEqual(basic,s.HomeUtilities.Cents);
                Assert.True(s.HasSeasonalFan(s.Home.Id));Assert.True(s.HasSeasonalAircon(s.Home.Id));Assert.AreEqual(0,s.CityClimateZones.Count);
                var save=s.CreateSave();Assert.AreEqual(2,save.cityClimateVersion);Assert.AreEqual(0,save.cityClimateZones.Length);
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save))))
                {Assert.AreEqual(cash,loaded.Ledger.Balance);Assert.True(loaded.HasSeasonalAircon(loaded.Home.Id));loaded.Clock.Restore(new GameDate(119),1439);loaded.AdvanceClock(1);Assert.AreEqual(basic+5000,loaded.Ledger.Entries.Last(e=>e.Category=="水电").Amount.Cents*-1);}
            }
        }
        [Test] public void SeasonalSleepKeepsEightHourGuaranteeAndUsesNoDayOutfitForBedding()
        {
            using(var a=Make())using(var b=Make())
            {
                b.GrantOutfit("winter_basic");Assert.True(b.Equip("winter_basic"));
                foreach(var s in new[]{a,b}){s.EventsEnabled=false;s.Clock.Restore(new GameDate(279),60);Assert.True(s.SleepEightHours());Assert.AreEqual(s.Player.MaxEnergy,s.Player.Energy);}
                Assert.AreEqual(a.Player.Energy,b.Player.Energy);Assert.AreEqual(a.Player.Mood,b.Player.Mood);
            }
        }
        [Test] public void ForecastDoesNotConsumeEconomicRandomState()
        {
            using(var s=Make()){ulong state=s.Random.State;string forecast=s.CityWeatherForecast;Assert.AreEqual(forecast,s.CityWeatherForecast);Assert.AreEqual(state,s.Random.State);}
        }
    }
}
