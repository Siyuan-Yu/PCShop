using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public static GameSession CreateCityLifePreset(CityLayout city,string scenario)
        {
            bool clothing=scenario=="clothing"||scenario.StartsWith("clothing_",StringComparison.Ordinal);
            int day=scenario=="racing"?21:scenario=="brand_expo"||scenario=="kol_live"||scenario=="quarter_expo"||scenario=="brand_ready_two"||scenario=="brand_ready_three"?68:scenario=="clothing_spring"?44:scenario=="clothing_summer"?134:scenario=="clothing_autumn"?224:clothing?279:scenario=="expo"?21:scenario=="assets"?29:16;
            var s=new GameSession(city,20261101,Money.FromYuan(scenario=="assets"?2500000:150000),GameTimestamp.From(new GameDate(day),scenario=="night"?20*60:9*60),true,true,true);
            s.IsCityLifeTest=true;s.EventsEnabled=false;
            s.Flags.Add("tutorial:issued:cashflow");
            foreach(var id in new[]{Npcs.Li,Npcs.Zhou,Rosters.AgentChen.Id,"life_ming","life_creator","life_enterprise","life_tenant"})s.Meet(id,25);
            if(scenario=="staff"||scenario=="assets"||scenario=="night")
            {
                s.MainShop.Employee=new Employee{Id="city_test_cashier",PersonId="life_ming",Name="许明",DailyWageYuan=150,CheckoutMinutes=2};
                s.MainShop.Assembler=new Employee{Id="city_test_assembler",PersonId="life_ning",Name="顾宁",DailyWageYuan=180,AssemblyMinutes=80};
                foreach(var e in new[]{s.MainShop.Employee,s.MainShop.Assembler})
                {
                    var person=s.CityPersonState(s.PersonIdOf(e));person.WorkPlaceId=s.MainShop.Place.Id;person.LocationPlaceId=s.MainShop.Place.Id;
                    var pos=s.StandOf(s.MainShop.Place);person.X=pos.X;person.Y=pos.Y;person.Task=CityPersonTask.Working;person.Route=null;
                }
            }
            if(scenario=="staff"&&city.HasPlace("life_unit_s_market_2"))
            {
                // A prepared second lease is a test asset, not a second source of free income.
                // Both stores still use the ordinary stock, staffing, service and save systems.
                var branchPlace=city.GetPlace("life_unit_s_market_2");var rent=s.ShopRentQuote(branchPlace);
                s.OpenNewShop(branchPlace,rent,RealEstate.Deposit(rent));
                var branch=s._focus;s.SetOpeningShopKind("computer");s.FitStarterShop();s.AddStarterStock();
                branch.Employee=new Employee{Id="city_test_branch_cashier",PersonId="life_qiao",Name="乔禾",DailyWageYuan=120,CheckoutMinutes=3};
                var person=s.CityPersonState(s.PersonIdOf(branch.Employee));var pos=s.StandOf(branch.Place);
                person.WorkPlaceId=branch.Place.Id;person.LocationPlaceId=branch.Place.Id;person.X=pos.X;person.Y=pos.Y;
                person.Task=CityPersonTask.Working;person.Route=null;s.Meet("life_qiao",25);
                s.SetFocus(s.MainShop);
            }
            s.ArriveAtNode(s.MainShop.Place.NodeId);s.CurrentPlace=s.MainShop.Place;s.MainShop.Sim.PlayerPresent=true;
            if(scenario=="night")
            {
                foreach(var e in new[]{s.MainShop.Employee,s.MainShop.Assembler}){e.ShiftStart=1200;e.ShiftEnd=480;e.DailyWageYuan=200;}
                s.Player.Energy=0;s.Say("夜班与精力测试档：20:00开始，员工排至次日08:00，日薪200；完整12小时预计280。玩家精力耗尽，清醒宽限240分钟；可自行睡觉或恢复精力。");
            }
            if(clothing)
            {
                s.MainShop.RetailKind="clothing";s.MainShop.Sim.RetailKind="clothing";
                s.MainShop.Layout.Clear();
                // Room coordinates start at the front-left corner, not at the centre.
                // Keep all displays inside the floor and leave the x=5 entrance aisle clear.
                for(int i=0;i<4;i++)s.MainShop.Layout.Add(new PlacedFurniture{Id=i%2==0?FurnitureCatalog.ClothingRack:FurnitureCatalog.ClothingFold,X=i%2==0?1.2:3.1,Z=i<2?5.1:2.7,Item="clothing_"+i});
                s.MainShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.ShopCounter,X=6.5,Z=3.0});
                s.MainShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.FittingMirror,X=7.1,Z=5.2});
                foreach(var product in ClothingProducts.All)s.MainShop.Sim.Stock.ForceAddGoods(product.Id,3);
                foreach(var id in new[]{"cotton_short","winter_basic","shirt","suit"})s.GrantOutfit(id);
                s.MainShop.Layout.Add(new PlacedFurniture{Id="aircon_large",X=4.8,Z=5.8});
                s.MainShop.Layout.Add(new PlacedFurniture{Id="fan_new",X=6.8,Z=1.9});
                s._homeLayout.Add(new PlacedFurniture{Id="aircon",X=1.8,Z=4.5});
                s.InitializeCityClimate();
                s.ApplyUpgrades();
            }
            if(scenario=="expo") {s.Leave();s.ArriveAtNode(city.GetPlace("life_expo").NodeId);}
            if(scenario=="assets")
            {
                s.PreparePropertyMarketAcceptance();
                s._regionalGarages.Add(new RegionalGarageLease{PlaceId="garage_0",RentedSpaces=6,NextRentDay=s.Clock.Today.TotalDays+30});
                s._regionalGarages.Add(new RegionalGarageLease{PlaceId="garage_1",OwnedSpaces=6,NextRentDay=-1});
                s.PlaceAcquiredMotor(s.NewMotor("e_city"),true,null);
                s.PlaceAcquiredMotor(s.NewMotor("c_su7"),true,null);
                s.PlaceAcquiredMotor(s.NewMotor("v_cargo"),false,null);
                s.PlaceAcquiredMotor(s.NewMotor("e_turn"),false,"garage_0");
                s.PlaceAcquiredMotor(s.NewMotor("t_sport"),false,"garage_0");
                s.PlaceAcquiredMotor(s.NewMotor("c_718"),false,"garage_1");
            }
#if UNITY_EDITOR
            if(scenario=="brand_expo")s.PrepareBrandExpoAcceptance();
            if(scenario=="kol_live")s.PrepareKolLiveAcceptance();
            if(scenario=="quarter_expo")s.PrepareQuarterlyExpoAcceptance();
            if(scenario=="brand_ready_two")s.PrepareBrandCreationAcceptance(2);
            if(scenario=="brand_ready_three")s.PrepareBrandCreationAcceptance(3);
            if(scenario=="racing")s.PrepareRacingAcceptance();
#endif
            return s;
        }
    }
}
