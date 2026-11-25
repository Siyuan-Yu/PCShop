using System;
using System.Linq;
using System.Collections;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private readonly System.Collections.Generic.List<PlaceVisual> _venueVisuals=new System.Collections.Generic.List<PlaceVisual>();
        private string _venueFocus;
        public void SetVenueFocus(string placeId)
        {
            if(_venueFocus==placeId)return;_venueFocus=placeId;
            foreach(var v in _venueVisuals)
            {
                bool inside=v.Place.Id==placeId;
                v.Exterior.SetActive(!inside);v.VenueInterior.SetActive(inside);
            }
        }
        private Color AgencyColor(Place p)
        {
            string id=_city.AgencyDistrictFor(p.StreetId)?.id;
            return id=="outskirts"?ArtDirection.Hex("B58B32"):id=="community"?ArtDirection.Sage:id=="park"?ArtDirection.Blue:ArtDirection.Hex("82659C");
        }
        private IEnumerator BuildPlannedCity(Action<string> stage)
        {
            stage?.Invoke("loading_roads");
            yield return null;
            if(_city.IsLakeCity)yield return BuildContinuousStreetSurfaces();
            else {BuildRoads(); BatchLakeRoads(); BuildCitySidewalks();}
            ReportBuildProgress(.14f);
            stage?.Invoke("loading_buildings");
            yield return null;
            var housing=new GameObject("ResidentialCompounds").transform;housing.SetParent(_root,false);
            var residenceBlocks = _city.Blocks.Where(block => _city.HasPlace(block.homeId))
                .ToDictionary(block => block.homeId);
            var placesToBuild=_city.PublicPlaces.Where(BuildsPlace).ToArray();
            var blocksToBuild=_city.Blocks.Where(b=>!_city.HasPlace(b.homeId)&&BuildsStreet(b.street)).ToArray();
            int buildingsDone=0,buildingsTotal=placesToBuild.Length+blocksToBuild.Length;
            float buildingDeadline=Time.realtimeSinceStartup+.016f;
            foreach (var place in placesToBuild)
            {
                residenceBlocks.TryGetValue(place.Id, out var block);
                BuildPlannedPlace(place, block, block == null ? _root : housing);
                int district = ArtDistrict(place.StreetId);
                if (district > 0) DistrictFinishes.Apply(Places[place.Id].Root.transform, district);
                ReportBuildProgress(.14f+.20f*++buildingsDone/Mathf.Max(1,buildingsTotal));
                if(Time.realtimeSinceStartup>=buildingDeadline)
                { yield return null;buildingDeadline=Time.realtimeSinceStartup+.016f; }
            }
            foreach(var block in blocksToBuild)
            {
                // The configuration validates real footprints, roads and door aisles. Render every
                // configured building: broad proximity culling previously erased whole apartment rows.
                Vector3 at=new Vector3((float)block.x,0,(float)block.y);
                var root=new GameObject("Compound_"+block.street).transform;root.SetParent(housing,false);root.position=at;
                root.rotation=Quaternion.Euler(0,(float)block.heading,0);
                PlannedBuilding(root,block.model,block.tier,(float)block.width,(float)block.depth,null,(float)block.height,block.variant);
                BuildResidentialPlot(root,block);
                int district = ArtDistrict(block.street);
                if (district > 0) DistrictFinishes.Apply(root, district);
                ReportBuildProgress(.14f+.20f*++buildingsDone/Mathf.Max(1,buildingsTotal));
                if(Time.realtimeSinceStartup>=buildingDeadline)
                { yield return null;buildingDeadline=Time.realtimeSinceStartup+.016f; }
            }
            // Residential geometry is static even when the door and occupants are interactive.
            ReportBuildProgress(.34f);
            stage?.Invoke("loading_scenery");
            yield return null;
            BatchStatic(housing.gameObject);
            yield return null;
            BuildPlannedProps();
            yield return BuildReferenceLandscape();
            BuildStarterTrees();
            ReportBuildProgress(.96f);
        }
        private void BuildResidentialPlot(Transform root,LayoutBlockData block)
        {
            if(block.model=="villa"||block.model=="apartment"||block.model=="luxury_apartment")return;
            float w=(float)block.width,d=(float)block.depth;
            Color paving=block.tier==0?ArtDirection.Hex("C0AA90"):Sidewalk;
            Prims.Box(root,"PlotPaving",new Vector3(0,.015f,d*.5f),new Vector3(w+.7f,.025f,d+.5f),
                IndustrialMaterials.Surface("paving",paving,new Vector2(w/4,d/4)));
            Prims.Box(root,"EntryPath",new Vector3(0,.025f,-1),new Vector3(1.6f,.04f,2),
                IndustrialMaterials.Surface("paving",Sidewalk,new Vector2(.5f,.6f)));
            if(block.model=="oldhome") return;
            if(block.tier==0)
            {
                Prims.Box(root,"SmallPorch",new Vector3(-w*.3f,.25f,-.45f),new Vector3(1.4f,.5f,.8f),Mats.Lit(ArtDirection.Sage));
            }
            else
            {
                foreach(int side in new[]{-1,1})
                {
                    Prims.Box(root,"ResidentialFrontLawn",new Vector3(side*w*.29f,.07f,-.5f),new Vector3(w*.32f,.10f,.9f),Mats.Lit(ArtDirection.Hex("88A668")));
                }
                foreach(int side in new[]{-1,1})
                {
                    Prims.Box(root,"CourtyardPlanter",new Vector3(side*w*.35f,.24f,-.5f),new Vector3(1.3f,.48f,.65f),Mats.Lit(ArtDirection.Cream));
                    Prims.Box(root,"CourtyardShrub",new Vector3(side*w*.35f,.65f,-.5f),new Vector3(1.1f,.55f,.55f),Mats.Lit(ArtDirection.Sage));
                }
            }
        }
        private void BuildPlannedPlace(Place p, LayoutBlockData block, Transform parent)
        {
            Vector3 node=NodeWorld(p.NodeId),door=node+new Vector3((float)p.EntranceX,0,(float)p.EntranceY);
            if (_city.IsLakeCity && IsLakeAccessNode(p.NodeId)) door.y = LakeAccessHeight(_city);
            var root=new GameObject("Place_"+p.Id).transform;root.SetParent(parent,false);root.position=door;
            root.rotation=Quaternion.Euler(0,(float)p.Heading,0);
            var vis=new PlaceVisual{Place=p,Entrance=door,Root=root.gameObject,ResidentialBlock=block};Places[p.Id]=vis;_cur=vis;
            float w=(float)p.Width,d=(float)p.Depth;
            if (GameSession.IsCommercialPremises(p))
            {
                var room = RoomSpec.ForShop(p.Size);
                w = (float)room.Width;
                d = (float)room.Depth;
            }
            if(p.Kind==PlaceKind.Garage)
            {
                BuildRegionalGarage(root,p,node,door,vis);return;
            }
            var track=RaceTracks.Get(p.Id,_city);
            if(track!=null)
            {
                var circuit=new GameObject("Circuit_"+p.Id);circuit.transform.SetParent(root,false);circuit.AddComponent<RaceTrackView>().Build(track,!Baking,OutskirtsLawnFor(p));
                BuildRaceEntrance(root,door,track);
                var office=Prims.Box(root,"RaceReception",new Vector3(0,2,0),new Vector3(9,4,6),Mats.Lit(ArtDirection.Cream),true);
                var reception=new MiniatureDetailMesh();
                if(track.Tier<2)
                    CityArchitecture.Roof(root,new Vector3(0,4.1f,0),9.8f,6.8f,1.3f,ArtDirection.Hex(track.Tier==1?"507361":"57758A"));
                else
                {
                    var cap=new Vector3(0,4.21f,-.4f);
                    GardenStructures.Plate(root,"接待处悬挑檐",cap,new Vector3(10.1f,.27f,8.3f),Mats.Lit(ArtDirection.Hex("E3EAE8")));
                    if(track.Tier==3)GardenStructures.SolarFins(reception,cap+Vector3.up*.14f,6.8f,4.6f,4);
                }
                reception.Window(new Vector3(-2.5f,2.15f,-3.08f),2.1f,1.7f,ArtDirection.Cream,true);
                reception.Window(new Vector3(2.5f,2.15f,-3.08f),2.1f,1.7f,ArtDirection.Cream,true);
                reception.Box(new Vector3(0,1.4f,-3.10f),new Vector3(1.3f,2.8f,.13f),Mats.Lit(ArtDirection.Hex("405B68")));
                reception.FlowerBed(new Vector3(-3.2f,.05f,-3.7f),1.6f,.7f,2);
                reception.FlowerBed(new Vector3(3.2f,.05f,-3.7f),1.6f,.7f,3);
                reception.Build(root,"Race reception facade");
                office.AddComponent<PlaceMarker>().PlaceId=p.Id;vis.LabelPos=door+Vector3.up*6;
                return;
            }

            bool venue=(p.Business??"").StartsWith("venue_");
            if(TryBuildStarterArt(root,p,w,d)) { }
            else if(CityArchitecture.HasExterior(p))
            {
                var shell=new GameObject("Exterior").transform;shell.SetParent(root,false);
                if (HasStarterArt && UsesQuarterShop(p))
                {
                    QuarterShop(shell,p,w,d);
                    vis.LabelPos=root.TransformPoint(new Vector3(0,6f,d*.3f));
                }
                else
                {
                    CityArchitecture.Venue(shell,p,w,d);
                    int district = ArtDistrict(p.StreetId);
                    if (district > 0) DistrictVenueEntrance(shell,p,w,d,district);
                    foreach(var collider in shell.GetComponentsInChildren<Collider>())collider.gameObject.AddComponent<PlaceMarker>().PlaceId=p.Id;
                }
                var inside=new GameObject("VenueInterior").transform;inside.SetParent(root,false);
                CityVenueView.Build(inside,p,w,d);inside.gameObject.SetActive(false);
                vis.Exterior=shell.gameObject;vis.VenueInterior=inside.gameObject;_venueVisuals.Add(vis);
            }
            else if(venue)CityVenueView.Build(root,p,w,d,OutskirtsLawnFor(p));
            else if(p.Kind==PlaceKind.Station)StarterMetro(root,p,w,d);
            else PlannedBuilding(root,p.Model,p.Tier,w,d,p,(float)(block?.height ?? 0),block?.variant ?? 0);
            if (block != null) BuildResidentialPlot(root, block);
            else if (p.Id != StarterHome && p.Kind == PlaceKind.Home && p.Model == "oldhome")
                StarterPart(root, "Plain residence entry path", new Vector3(0, .035f, -1.65f), new Vector3(1.7f, .06f, 3.3f),
                    ArtDirection.Hex("BFB4A0"), "paving", new Vector2(.5f, .85f));
            // The route node is on the road; its approach ends at the actual public door.
            var delta=door-node;
            if(!_city.IsLakeCity)
            {
                var approach=Prims.Box(_root,"Approach_"+p.Id,(node+door)*.5f+Vector3.up*.08f,new Vector3(2,.12f,delta.magnitude),Mats.Lit(Sidewalk));
                if(delta.sqrMagnitude>.01f)approach.transform.rotation=Quaternion.LookRotation(delta);
            }
            if (vis.LabelPos == Vector3.zero)
                vis.LabelPos=root.TransformPoint(new Vector3(0,p.HousingForm!=ResidenceForm.Standard?PlannedPlaceHeight(p)+.5f:block!=null?3.3f:CityArchitecture.HasExterior(p)?CityArchitecture.VenueHeight(p)+1:!venue && p.Kind!=PlaceKind.Station?PlannedPlaceHeight(p)+1:5,d*.3f));
            vis.Sign=root.GetComponentInChildren<UnityEngine.UI.Text>();
        }
        private static float PlannedHeight(string model,int tier)=>model=="villa"?6:model=="oldhome"?6:model=="luxury_apartment"?22:model=="apartment"?14:model=="office"?16:model=="warehouse"?6:4.5f+tier*.65f;
        private static float PlannedPlaceHeight(Place p)
        {
            if (p.HousingForm == ResidenceForm.LaneHouse) return 3.95f;
            if (p.HousingForm == ResidenceForm.Walkup) return WalkupHeight;
            if (GameSession.IsCommercialPremises(p))
            {
                int variation = StableCityHash(p.Id) % 3;
                return p.Size switch
                {
                    ShopSize.Large => 12f + variation * 2f,
                    ShopSize.Medium => 8.2f + variation * 1.2f,
                    _ => 5.6f + variation * .6f
                };
            }
            if(p.Model=="villa")return 9.5f;
            if(p.Model=="apartment"||p.Model=="luxury_apartment")return ResidenceArtHeight(PlannedHeight(p.Model,p.Tier),p.Tier,StableCityHash(p.Id))+1.5f;
            if(p.Model!="shop")return PlannedHeight(p.Model,p.Tier);
            int seed=StableCityHash(p.Id);
            return p.Size==PCShopSell.Core.Shop.ShopSize.Small?4.5f+(seed%3)*2:
                p.Size==PCShopSell.Core.Shop.ShopSize.Large?12+(seed%3)*4:8+(seed%3)*3;
        }
        private void BuildShopDownlights(Transform root,float width,float height)
        {
            foreach(int side in new[]{-1,1})
            {
                var lamp=new GameObject("Shopfront warm downlight");lamp.transform.SetParent(root,false);
                lamp.transform.localPosition=new Vector3(side*width*.3f,height,-.65f);
                Prims.Box(lamp.transform,"Metal lamp shade",Vector3.zero,new Vector3(.48f,.12f,.32f),Mats.Lit(ArtDirection.Ink,.4f,.4f));
                Prims.Box(lamp.transform,"Lamp diffuser",Vector3.down*.08f,new Vector3(.35f,.035f,.22f),Mats.Glow(ArtDirection.Cream,new Color(2.4f,1.5f,.6f)));
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=6.5f;
                light.color=new Color(1f,.72f,.38f);light.intensity=0;light.shadows=LightShadows.None;NightLights.Add(light);
            }
        }

        private void PlannedBuilding(Transform root,string model,int tier,float w,float d,Place p,float configuredHeight=0,int variation=0)
        {
            int seed=StableCityHash(p?.Id??(root.name+"_"+variation));
            float h=configuredHeight>0?configuredHeight:PlannedHeight(model,tier);
            if(p!=null && model=="shop")h=PlannedPlaceHeight(p);
            if(model=="oldhome" && (p==null || p.Kind==PlaceKind.Home))
            {
                if (InPaintedQuarter(new Vector2(root.position.x,root.position.z),63f))
                {
                    Vector2 offset=new Vector2(root.position.x,root.position.z)-_quarterOrigin;
                    if (offset.sqrMagnitude<33*33) QuarterLaneHouse(root,p,w,d,h);
                    else
                    {
                        string form=p?.Id switch {
                            "residence_s_oldhomes_03" => "RouteGrayHouse",
                            "residence_s_oldhomes_04" => "RouteSageHouse",
                            "residence_s_oldhomes_05" => "RouteCreamHouse",
                            _ => (seed%3) switch {0=>"RouteGrayHouse",1=>"RouteSageHouse",_=>"RouteCreamHouse"}
                        };
                        var shell=QuarterModel(root,form,"Route");
                        shell.transform.localScale=new Vector3(w/8,h/5.4f,d/7.8f);
                        var hit=root.gameObject.AddComponent<BoxCollider>();
                        hit.center=new Vector3(0,h*.5f,d*.5f);hit.size=new Vector3(w,h,d);
                        if(p!=null)root.gameObject.AddComponent<PlaceMarker>().PlaceId=p.Id;
                    }
                }
                else BuildOldResidence(root,p,w,d,h,seed,starter:p?.Id==StarterHome);
                return;
            }
            if(model=="apartment"||model=="luxury_apartment")
            {
                ReferenceResidence(root,w,d,h,tier,seed);
                if(p!=null)foreach(var c in root.GetComponentsInChildren<Collider>())c.gameObject.AddComponent<PlaceMarker>().PlaceId=p.Id;
                return;
            }
            if(model=="villa")
            {
                CityArchitecture.GardenVilla(root,w,d,h,seed);
                if(p!=null)foreach(var c in root.GetComponentsInChildren<Collider>())c.gameObject.AddComponent<PlaceMarker>().PlaceId=p.Id;
                return;
            }
            if (p != null && ArtDistrict(p.StreetId) > 0)
            {
                DistrictBusiness(root,p,w,d,h,ArtDistrict(p.StreetId),seed);
                return;
            }
            Color wall=Facades[seed%Facades.Length];
            Color shopAccent=DistrictFacadeAccent(tier,seed);
            if(model=="oldhome")
            {
                Color[] oldWalls={ArtDirection.Hex("CBB298"),ArtDirection.Hex("D9BD97"),ArtDirection.Hex("A4B0A0"),ArtDirection.Hex("BC9980"),ArtDirection.Hex("D3BEA4")};
                wall=oldWalls[(seed&int.MaxValue)%oldWalls.Length];
            }
            if(model=="luxury_apartment")wall=ArtDirection.Cream;
            if(model=="warehouse")wall=ArtDirection.Hex("798E9D");
            string surface=model=="warehouse"?"metal":model=="oldhome"||model=="shop"&&tier==0?"brick":"concrete";
            var material=IndustrialMaterials.Surface(surface,wall,new Vector2(Mathf.Max(1,w/4),Mathf.Max(1,h/3)));
            var body=Prims.Box(root,"Building",new Vector3(0,h/2,d/2),new Vector3(w,h,d),material,true);
            if(p!=null)body.AddComponent<PlaceMarker>().PlaceId=p.Id;
            Prims.Box(root,"Foundation",new Vector3(0,.18f,d/2),new Vector3(w+.5f,.36f,d+.5f),Mats.Lit(StarterStone));
            Prims.Box(root,"Door",new Vector3(0,1.2f,-.08f),new Vector3(model=="warehouse"?w*.6f:1.6f,2.4f,.13f),Mats.Lit(ArtDirection.Ink));
            Prims.Box(root,"EntranceAwning",new Vector3(0,2.6f,-.5f),new Vector3(Mathf.Min(w-1,4),.2f,1.5f),Mats.Lit(tier>1?ArtDirection.Brass:ArtDirection.Sage));
            Prims.Box(root,"FlatRoof",new Vector3(0,h+.15f,d/2),new Vector3(w+.5f,.3f,d+.5f),
                IndustrialMaterials.Surface("metal",Color.Lerp(shopAccent,ArtDirection.Hex("466477"),.16f),new Vector2(w/5,d/6)));
            if(model=="oldhome")RoofClutter(root,new Vector3(0,h+.3f,d/2),w,d);
            if(model=="shop" || model=="oldhome" && variation%3!=0)
            {
                // Old mixed-use street: small shop fronts under unequal residential floors.
                float awningWidth=Mathf.Min(w-.5f,model=="oldhome"?w*.8f:w-.5f);
                int panels=Mathf.Clamp(Mathf.RoundToInt(awningWidth/.85f),4,14);
                for(int i=0;i<panels;i++)
                {
                    var strip=Prims.Box(root,"StripedAwning",new Vector3(-awningWidth/2+(i+.5f)*awningWidth/panels,2.83f,-.58f),
                        new Vector3(awningWidth/panels+.006f,.12f,1.24f),Mats.Lit(i%2==0?shopAccent:ArtDirection.Hex("E6D4B9"),.19f));
                    strip.transform.localRotation=Quaternion.Euler(-12,0,0);
                    Prims.Box(root,"Scalloped canvas front",new Vector3(-awningWidth/2+(i+.5f)*awningWidth/panels,2.66f,-1.17f),
                        new Vector3(awningWidth/panels+.006f,.25f,.06f),Mats.Lit(i%2==0?shopAccent:ArtDirection.Hex("E6D4B9"),.19f));
                }
                foreach(int side in new[]{-1,1})
                    StarterWindow(root,new Vector3(side*w*.3f,1.25f,0),w*.22f,1.7f,ArtDirection.Hex("654F43"),true);
            }
            CityArchitecture.Residence(root,model,tier,w,d,h,variation);
            if(model!="shop"&&model!="oldhome"&&model!="warehouse")
            {
                var serviceWindows=new MiniatureDetailMesh();
                foreach(int side in new[]{-1,1})
                    serviceWindows.Window(new Vector3(side*w*.30f,1.6f,-.04f),Mathf.Min(2.6f,w*.22f),1.9f,StarterStone,true);
                serviceWindows.Build(root,"Service building ground floor windows");
            }
            CityArchitecture.ReferenceFacade(root,model,tier,w,d,h,seed);
            CityArchitecture.IndustrialDetails(root,model,w,d,h,seed);
            if(model!="warehouse")CityArchitecture.StreetLife(root,w,d,h,seed,model=="shop"||model=="market");
            if(p!=null&&(model=="shop"||model=="warehouse")) BuildShopDownlights(root,w,2.9f);
            if(model=="office")
            {
                foreach(int side in new[]{-1,1})Prims.Box(root,"OfficeFrame",new Vector3(side*w*.42f,h/2,-.18f),new Vector3(.5f,h,.3f),Mats.Lit(ArtDirection.Cream));
                Prims.Box(root,"OfficeLobby",new Vector3(0,2,-.3f),new Vector3(w*.5f,4,.2f),Mats.Lit(ArtDirection.Blue));
                Prims.Box(root,"OfficeRoofPlant",new Vector3(w*.2f,h+.7f,d*.7f),new Vector3(w*.3f,1.2f,d*.25f),Mats.Lit(ArtDirection.Ink));
            }
            if(model=="market")
            {
                for(int i=-2;i<=2;i++)
                {
                    Color canvas=i%2==0?ArtDirection.Sage:ArtDirection.Clay;
                    Prims.Box(root,"MarketAwning",new Vector3(i*w*.17f,2.8f,-.6f),new Vector3(w*.16f,.2f,1.3f),Mats.Lit(canvas));
                    Prims.Box(root,"MarketProduceCrate",new Vector3(i*w*.17f,.4f,-.45f),new Vector3(w*.13f,.8f,.7f),Mats.Lit(ArtDirection.Brass));
                }
            }
            if(p!=null && (p.Business??"").Contains("模型"))
            {
                for(int i=-1;i<=1;i++)
                {
                    Color color=i==0?ArtDirection.Clay:i<0?ArtDirection.Blue:ArtDirection.Sage;
                    Prims.Box(root,"OriginalToyDisplay",new Vector3(i*w*.23f,1.1f,-.4f),new Vector3(.6f,.8f,.35f),Mats.Lit(color));
                    Prims.Shape(PrimitiveType.Sphere,root,"OriginalToyHead",new Vector3(i*w*.23f,1.7f,-.4f),Vector3.one*.48f,Mats.Lit(ArtDirection.Cream));
                }
            }

            // Real window frames on each elevation; fixed bays remain legible at player zoom.
            for(int face=0;face<4;face++)
            {
                var elevation=new GameObject("Street elevation").transform;elevation.SetParent(root,false);
                elevation.localPosition=new Vector3(0,0,d/2);elevation.localRotation=Quaternion.Euler(0,face*90,0);
                float width=face%2==0?w:d,depth=face%2==0?d:w;
                var detail=new MiniatureDetailMesh();
                int columns=Mathf.Clamp(Mathf.FloorToInt(width/3.1f),2,6);
                for(float y=4.9f;y<h-.65f;y+=2.65f)
                for(int col=0;col<columns;col++)
                {
                    float x=(col-(columns-1)*.5f)*width/(columns+.4f);
                    detail.Window(new Vector3(x,y,-depth/2-.025f),Mathf.Min(1.8f,width/columns*.60f),1.45f,
                        ArtDirection.Hex("DCC8AD"),(col+Mathf.RoundToInt(y)+seed)%4!=0);
                }
                detail.Build(elevation,"Sculpted window bays");
            }
            if(p!=null)
            {
                Color fascia=p.Business==GameSession.HospitalBusiness?ArtDirection.Sage:p.Kind==PlaceKind.Agency?AgencyColor(p):shopAccent;
                Prims.Box(root,"Fascia",new Vector3(0,3.3f,-.2f),new Vector3(w-.4f,.9f,.16f),Mats.Lit(fascia));
                Prims.SignText(root,p.Name,new Vector3(0,3.3f,-.31f),w-.8f,.65f,ArtDirection.Cream);
                if(p.Kind==PlaceKind.Agency)Prims.SignText(root,_city.AgencyCoverageName(p)+" · 租售办理",new Vector3(0,2.25f,-.3f),w-1,.45f,ArtDirection.Cream);
                var open=Prims.Box(root,"OpenLamp",new Vector3(w*.38f,2.5f,-.24f),new Vector3(.4f,.25f,.08f),Mats.Unlit(UiGreen));
                var closed=Prims.Box(root,"ClosedLamp",new Vector3(w*.38f,2.5f,-.24f),new Vector3(.4f,.25f,.08f),Mats.Unlit(ArtDirection.Clay));
                Places[p.Id].OpenOnly.Add(open);Places[p.Id].ClosedOnly.Add(closed);
            }
        }
        internal static Color DistrictFacadeAccent(int tier,int seed)
        {
            return ArtDirection.Hex(tier switch
            {
                0 => (seed%3) switch {0=>"3E5F81",1=>"A3573B",_=>"527666"},
                1 => (seed%4) switch {0=>"8A6097",1=>"BD7048",2=>"448F93",_=>"4E7CA1"},
                2 => (seed%3) switch {0=>"567A5D",1=>"446E85",_=>"86644E"},
                _ => (seed%3) switch {0=>"3E5D7C",1=>"567D83",_=>"6A597D"}
            });
        }
        private static float PlannedRoadWidth(Vector3 a,Vector3 b)
        {
            return (float)GameSession.CityRoadWidth(new MapPoint(a.x,a.z),new MapPoint(b.x,b.z));
        }
        private void BuildRoadDetail(Transform parent,Vector3 a,Vector3 b,float width)
        {
            Vector3 delta=b-a;float length=delta.magnitude;
            if(length<.1f)return;
            Quaternion rotation=Quaternion.LookRotation(delta.normalized);
            Vector3 right=Vector3.Cross(Vector3.up,delta.normalized);
            foreach(int side in new[]{-1,1})
            {
                // Paving and roadway meet at ground level; no raised curb.
                if(length>5)
                {
                    var edge=Prims.Box(parent,"RoadEdge",(a+b)*.5f+right*side*(width/2-.35f)+Vector3.up*.025f,
                        new Vector3(.08f,.015f,length),Mats.Lit(ArtDirection.Cream));edge.transform.localRotation=rotation;
                }
            }
            for(float along=2;along<length-1;along+=5)
            {
                var dash=Prims.Box(parent,"LaneDash",a+delta.normalized*along+Vector3.up*.025f,new Vector3(.12f,.015f,Mathf.Min(2,length-along)),Mats.Lit(ArtDirection.Brass));
                dash.transform.localRotation=rotation;
            }
        }
        private void BuildPlannedProps()
        {
            var props=new GameObject("NeighborhoodPublicSpace").transform;props.SetParent(_root,false);
            // Perpendicular intersections share visible crosswalks. Pure decoration never adds routes.
            foreach(var n in _city.Map.Nodes)
            {
                if(_city.IsLakeCity)continue; // Oriented crossings belong to the unified road surface.
                if(_city.Map.NeighborsOf(n.Id).Count<3)continue;
                Vector3 at=NodeWorld(n.Id);
                for(int i=-2;i<=2;i++)Prims.Box(props,"Crosswalk",at+new Vector3(i*.7f,.04f,2.6f),new Vector3(.4f,.02f,1.7f),Mats.Lit(ArtDirection.Cream));
            }
            for(int band=0;band<(_city.IsLakeCity?0:4);band++)
            {
                for(int x=-190;x<260;x+=22)
                {
                    var at=new Vector3(x,0,band*135-4.8f);Tree(props,at);
                    Prims.Box(props,"StreetLamp",at+new Vector3(3,2.2f,0),new Vector3(.13f,4.4f,.13f),Mats.Lit(ArtDirection.Ink));
                    Prims.Box(props,"LampHead",at+new Vector3(3,4.4f,.3f),new Vector3(.6f,.2f,1.2f),Mats.Glow(ArtDirection.Cream,new Color(3,2.6f,1.9f)));
                }
            }
            foreach(var street in _city.Streets.Where(street => BuildsStreet(street.id)))
            {
                var agency=_city.Places.FirstOrDefault(p=>p.StreetId==street.id && p.Kind!=PlaceKind.Station && BuildsPlace(p));
                if(agency==null)continue;
                Vector3 at=NodeWorld(agency.NodeId);
                if (street.id == "s_oldhomes" && Places.TryGetValue(StarterHome, out var home))
                {
                    var sign = new GameObject("旧居街路牌").transform;
                    sign.SetParent(props,false);
                    sign.position = home.Root.transform.TransformPoint(new Vector3(-4.8f,0,-2));
                    sign.rotation = home.Root.transform.rotation;
                    Prims.Box(sign,"Post",new Vector3(0,.91f,0),new Vector3(.07f,1.82f,.07f),Mats.Lit(ArtDirection.Ink));
                    Prims.Box(sign,"Enamel plate",new Vector3(0,1.72f,0),new Vector3(1.48f,.38f,.07f),Mats.Lit(ArtDirection.Hex("526B63")));
                    Prims.SignText(sign,street.name,new Vector3(0,1.72f,-.05f),1.41f,.30f,ArtDirection.Cream);
                }
                else Prims.SignText(props,street.name,at+new Vector3(0,5,-1),22,1,ArtDirection.Cream);
            }
        }
    }
}
