using System;
using System.Linq;
using PCShopSell.Core.World;
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
        private void BuildPlannedCity()
        {
            BuildRoads(); BatchLakeRoads(); BuildCitySidewalks();
            foreach(var p in _city.Places) BuildPlannedPlace(p);
            var housing=new GameObject("ResidentialCompounds").transform;housing.SetParent(_root,false);
            foreach(var block in _city.Blocks)
            {
                // The configuration validates real footprints, roads and door aisles. Render every
                // configured building: broad proximity culling previously erased whole apartment rows.
                Vector3 at=new Vector3((float)block.x,0,(float)block.y);
                var root=new GameObject("Compound_"+block.street).transform;root.SetParent(housing,false);root.position=at;
                root.rotation=Quaternion.Euler(0,(float)block.heading,0);
                PlannedBuilding(root,block.model,block.tier,(float)block.width,(float)block.depth,null,(float)block.height,block.variant);
                BuildResidentialPlot(root,block);
            }
            // These buildings never move. Combine only static scenery, keeping interactive places separate.
            StaticBatchingUtility.Combine(housing.gameObject);
            BuildPlannedProps();
            BuildReferenceLandscape();
        }
        private void BuildResidentialPlot(Transform root,LayoutBlockData block)
        {
            if(block.model=="villa"||block.model=="apartment"||block.model=="luxury_apartment")return;
            float w=(float)block.width,d=(float)block.depth;
            Color paving=block.tier==0?ArtDirection.Hex("A7A294"):Sidewalk;
            Prims.Box(root,"PlotPaving",new Vector3(0,.015f,d*.5f),new Vector3(w+.7f,.025f,d+.5f),Mats.Lit(paving));
            Prims.Box(root,"EntryPath",new Vector3(0,.025f,-1),new Vector3(1.6f,.04f,2),Mats.Lit(Sidewalk));
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
        private void BuildPlannedPlace(Place p)
        {
            Vector3 node=NodeWorld(p.NodeId),door=node+new Vector3((float)p.EntranceX,0,(float)p.EntranceY);
            var root=new GameObject("Place_"+p.Id).transform;root.SetParent(_root,false);root.position=door;
            root.rotation=Quaternion.Euler(0,(float)p.Heading,0);
            var vis=new PlaceVisual{Place=p,Entrance=door,Root=root.gameObject};Places[p.Id]=vis;_cur=vis;
            float w=(float)p.Width,d=(float)p.Depth;
            bool venue=(p.Business??"").StartsWith("venue_");
            if(CityArchitecture.HasExterior(p))
            {
                var shell=new GameObject("Exterior").transform;shell.SetParent(root,false);
                CityArchitecture.Venue(shell,p,w,d);
                foreach(var collider in shell.GetComponentsInChildren<Collider>())collider.gameObject.AddComponent<PlaceMarker>().PlaceId=p.Id;
                var inside=new GameObject("VenueInterior").transform;inside.SetParent(root,false);
                CityVenueView.Build(inside,p,w,d);inside.gameObject.SetActive(false);
                vis.Exterior=shell.gameObject;vis.VenueInterior=inside.gameObject;_venueVisuals.Add(vis);
            }
            else if(venue)CityVenueView.Build(root,p,w,d);
            else if(p.Kind==PlaceKind.Station)PlannedStation(root,p.Name,w,d);
            else PlannedBuilding(root,p.Model,p.Tier,w,d,p);
            // The route node is on the road; its approach ends at the actual public door.
            var delta=door-node;
            var approach=Prims.Box(_root,"Approach_"+p.Id,(node+door)*.5f+Vector3.up*.08f,new Vector3(2,.12f,delta.magnitude),Mats.Lit(Sidewalk));
            if(delta.sqrMagnitude>.01f)approach.transform.rotation=Quaternion.LookRotation(delta);
            vis.LabelPos=root.TransformPoint(new Vector3(0,CityArchitecture.HasExterior(p)?CityArchitecture.VenueHeight(p)+1:!venue && p.Kind!=PlaceKind.Station?PlannedPlaceHeight(p)+1:5,d*.3f));
            vis.Sign=root.GetComponentInChildren<UnityEngine.UI.Text>();
        }
        private static float PlannedHeight(string model,int tier)=>model=="villa"?6:model=="oldhome"?6:model=="luxury_apartment"?22:model=="apartment"?14:model=="office"?16:model=="warehouse"?6:4.5f+tier*.65f;
        private static float PlannedPlaceHeight(Place p)
        {
            if(p.Model!="shop")return PlannedHeight(p.Model,p.Tier);
            int seed=StableCityHash(p.Id);
            return p.Size==PCShopSell.Core.Shop.ShopSize.Small?4.5f+(seed%3)*2:
                p.Size==PCShopSell.Core.Shop.ShopSize.Large?12+(seed%3)*4:8+(seed%3)*3;
        }
        private void PlannedBuilding(Transform root,string model,int tier,float w,float d,Place p,float configuredHeight=0,int variation=0)
        {
            int seed=StableCityHash(p?.Id??(root.name+"_"+variation));
            float h=configuredHeight>0?configuredHeight:PlannedHeight(model,tier);
            if(p!=null && model=="shop")h=PlannedPlaceHeight(p);
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
            Color wall=Facades[seed%Facades.Length];
            if(model=="oldhome")
            {
                Color[] oldWalls={ArtDirection.Hex("A09889"),ArtDirection.Hex("B2A48F"),ArtDirection.Hex("909A96"),ArtDirection.Hex("A28E80"),ArtDirection.Hex("B8B2A3")};
                wall=oldWalls[(seed&int.MaxValue)%oldWalls.Length];
            }
            if(model=="luxury_apartment")wall=ArtDirection.Cream;
            if(model=="warehouse")wall=ArtDirection.Blue;
            var material=IndustrialMaterials.Surface(model=="warehouse"?"metal":model=="oldhome"?"brick":"concrete",wall,new Vector2(Mathf.Max(1,w/3),Mathf.Max(1,h/3)));
            var body=Prims.Box(root,"Building",new Vector3(0,h/2,d/2),new Vector3(w,h,d),material,true);
            if(p!=null)body.AddComponent<PlaceMarker>().PlaceId=p.Id;
            Prims.Box(root,"Foundation",new Vector3(0,.18f,d/2),new Vector3(w+.5f,.36f,d+.5f),Mats.Lit(Sidewalk));
            Prims.Box(root,"Door",new Vector3(0,1.2f,-.08f),new Vector3(model=="warehouse"?w*.6f:1.6f,2.4f,.13f),Mats.Lit(ArtDirection.Ink));
            Prims.Box(root,"EntranceAwning",new Vector3(0,2.6f,-.5f),new Vector3(Mathf.Min(w-1,4),.2f,1.5f),Mats.Lit(tier>1?ArtDirection.Brass:ArtDirection.Sage));
            Prims.Box(root,"FlatRoof",new Vector3(0,h+.15f,d/2),new Vector3(w+.5f,.3f,d+.5f),Mats.Lit(ArtDirection.Ink));
            if(model=="oldhome")RoofClutter(root,new Vector3(0,h+.3f,d/2),w,d);
            if(model=="shop" || model=="oldhome" && variation%3!=0)
            {
                // Old mixed-use street: small shop fronts under unequal residential floors.
                float awningWidth=Mathf.Min(w-.5f,model=="oldhome"?w*.8f:w-.5f);
                int panels=Mathf.Max(4,Mathf.RoundToInt(awningWidth/.6f));
                for(int i=0;i<panels;i++)
                    Prims.Box(root,"StripedAwning",new Vector3(-awningWidth/2+(i+.5f)*awningWidth/panels,2.75f,-.55f),
                        new Vector3(awningWidth/panels,.12f,1.15f),Mats.Lit(i%2==0?ArtDirection.Blue:ArtDirection.Ink,.35f,.25f));
                foreach(int side in new[]{-1,1})
                    Prims.Box(root,"GroundFloorWindow",new Vector3(side*w*.3f,1.25f,-.15f),new Vector3(w*.22f,1.7f,.1f),Mats.Glow(ArtDirection.Hex("4A5962"),new Color(1.3f,.85f,.38f)));
            }
            CityArchitecture.Residence(root,model,tier,w,d,h,variation);
            CityArchitecture.ReferenceFacade(root,model,tier,w,d,h,seed);
            CityArchitecture.IndustrialDetails(root,model,w,d,h,seed);
            if(p!=null&&(model=="shop"||model=="warehouse"))
            {
                foreach(int side in new[]{-1,1})
                {
                    var lamp=new GameObject("Shopfront warm downlight");lamp.transform.SetParent(root,false);
                    lamp.transform.localPosition=new Vector3(side*w*.3f,2.9f,-.65f);
                    Prims.Box(lamp.transform,"Metal lamp shade",Vector3.zero,new Vector3(.48f,.12f,.32f),Mats.Lit(ArtDirection.Ink,.4f,.4f));
                    Prims.Box(lamp.transform,"Lamp diffuser",Vector3.down*.08f,new Vector3(.35f,.035f,.22f),Mats.Glow(ArtDirection.Cream,new Color(2.4f,1.5f,.6f)));
                    var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=6.5f;
                    light.color=new Color(1f,.72f,.38f);light.intensity=0;light.shadows=LightShadows.None;NightLights.Add(light);
                }
            }
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

            // Reuse tiled window materials rather than thousands of individual window objects.
            WindowFace(root,new Vector3(0,h*.58f,-.12f),w-.8f,h-3,wall,false);
            WindowFace(root,new Vector3(-w/2-.04f,h*.55f,d/2),d-.7f,h-2.5f,wall,true);
            var rear=new GameObject("RearFacades").transform;rear.SetParent(root,false);rear.localPosition=new Vector3(0,0,d);rear.localRotation=Quaternion.Euler(0,180,0);
            WindowFace(rear,new Vector3(0,h*.58f,-.12f),w-.8f,h-3,wall,false);
            WindowFace(rear,new Vector3(-w/2-.04f,h*.55f,d/2),d-.7f,h-2.5f,wall,true);
            if(p!=null)
            {
                Color fascia=p.Kind==PlaceKind.Agency?AgencyColor(p):tier==3?ArtDirection.Ink:SignColors[StableCityHash(p.Id)%SignColors.Length];
                Prims.Box(root,"Fascia",new Vector3(0,3.3f,-.2f),new Vector3(w-.4f,.9f,.16f),Mats.Lit(fascia));
                Prims.SignText(root,p.Name,new Vector3(0,3.3f,-.31f),w-.8f,.65f,ArtDirection.Cream);
                if(p.Kind==PlaceKind.Agency)Prims.SignText(root,_city.AgencyCoverageName(p)+" · 租售办理",new Vector3(0,2.25f,-.3f),w-1,.45f,ArtDirection.Cream);
                var open=Prims.Box(root,"OpenLamp",new Vector3(w*.38f,2.5f,-.24f),new Vector3(.4f,.25f,.08f),Mats.Unlit(UiGreen));
                var closed=Prims.Box(root,"ClosedLamp",new Vector3(w*.38f,2.5f,-.24f),new Vector3(.4f,.25f,.08f),Mats.Unlit(ArtDirection.Clay));
                Places[p.Id].OpenOnly.Add(open);Places[p.Id].ClosedOnly.Add(closed);
            }
        }
        private void PlannedStation(Transform root,string name,float w,float d)
        {
            var blue=Mats.Lit(ArtDirection.Blue);
            var baseGo=Prims.Box(root,"MetroEntrance",new Vector3(0,.12f,2.5f),new Vector3(w,.25f,5),Mats.Lit(ArtDirection.Ink),true);
            baseGo.AddComponent<PlaceMarker>().PlaceId=_cur.Place.Id;
            for(int i=0;i<6;i++)Prims.Box(root,"Stair",new Vector3(0,.2f+i*.12f,i*.65f),new Vector3(w-2,.18f,.7f),Mats.Lit(Sidewalk));
            foreach(int side in new[]{-1,1})Prims.Box(root,"Railing",new Vector3(side*(w/2-.5f),1,2.5f),new Vector3(.12f,1.7f,5),blue);
            Prims.Box(root,"MetroCanopy",new Vector3(0,2.8f,2.5f),new Vector3(w+.4f,.2f,6),blue);
            Prims.Box(root,"MetroPylon",new Vector3(-w/2+.6f,2,0),new Vector3(.5f,4,.4f),blue);
            Prims.Box(root,"MetroSymbol",new Vector3(-w/2+.6f,4,0),new Vector3(1.2f,1.1f,.2f),blue);
            Prims.SignText(root,"M",new Vector3(-w/2+.6f,4,-.16f),1,1,ArtDirection.Cream);
            Prims.SignText(root,"M  "+name,new Vector3(0,2.45f,-.55f),w,.7f,ArtDirection.Cream);
        }
        private static float PlannedRoadWidth(Vector3 a,Vector3 b)
        {
            bool horizontal=Mathf.Abs(a.z-b.z)<.01f;
            return horizontal?(Mathf.Abs(a.z/135f-Mathf.Round(a.z/135f))<.001f?8:5):7;
        }
        private void BuildRoadDetail(Transform parent,Vector3 a,Vector3 b,float width)
        {
            Vector3 delta=b-a;float length=delta.magnitude;
            if(length<.1f)return;
            Quaternion rotation=Quaternion.LookRotation(delta.normalized);
            Vector3 right=Vector3.Cross(Vector3.up,delta.normalized);
            foreach(int side in new[]{-1,1})
            {
                var curb=Prims.Box(parent,"StreetCurb",(a+b)*.5f+right*side*(width/2+.12f)+Vector3.up*.12f,
                    new Vector3(.24f,.24f,length),Mats.Lit(Sidewalk));curb.transform.localRotation=rotation;
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
            foreach(var street in _city.Streets)
            {
                var agency=_city.Places.FirstOrDefault(p=>p.StreetId==street.id && p.Kind!=PlaceKind.Station);
                if(agency==null)continue;
                Vector3 at=NodeWorld(agency.NodeId);
                Prims.SignText(props,street.name,at+new Vector3(0,5,-1),22,1,ArtDirection.Cream);
            }
        }
    }
}
