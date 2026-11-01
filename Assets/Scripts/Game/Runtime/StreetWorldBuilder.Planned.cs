using System;
using System.Linq;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private void BuildPlannedCity()
        {
            BuildRoads(); BuildCitySidewalks();
            foreach(var p in _city.Places) BuildPlannedPlace(p);
            var housing=new GameObject("ResidentialCompounds").transform;housing.SetParent(_root,false);
            foreach(var block in _city.Blocks)
            {
                // The configuration validates real footprints, roads and door aisles. Render every
                // configured building: broad proximity culling previously erased whole apartment rows.
                Vector3 at=new Vector3((float)block.x,0,(float)block.y);
                var root=new GameObject("Compound_"+block.street).transform;root.SetParent(housing,false);root.position=at;
                PlannedBuilding(root,block.model,block.tier,(float)block.width,(float)block.depth,null,(float)block.height,block.variant);
                BuildResidentialPlot(root,block);
            }
            // These buildings never move. Combine only static scenery, keeping interactive places separate.
            StaticBatchingUtility.Combine(housing.gameObject);
            BuildPlannedProps();
        }
        private void BuildResidentialPlot(Transform root,LayoutBlockData block)
        {
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
            if(venue||p.Business=="服装")
            {
                CityVenueView.Build(root,p,w,d);
                if(p.Model=="expo")
                {
                    // Blue industrial roof and truss portico; leave the front activity aisle visible.
                    foreach(int side in new[]{-1,1})
                    {
                        var roof=Prims.Box(root,"ExhibitionRoof",new Vector3(side*w*.25f,7.5f,d*.72f),new Vector3(w*.53f,.35f,d*.6f),Mats.Lit(ArtDirection.Blue));
                        roof.transform.localRotation=Quaternion.Euler(0,0,-side*12);
                    }
                    for(int i=-2;i<=2;i++)Prims.Box(root,"HallColumn",new Vector3(i*w/5,3.5f,d*.38f),new Vector3(.35f,7,.35f),Mats.Lit(ArtDirection.Cream));
                    Prims.SignText(root,"电子展 · 原创漫展",new Vector3(0,6,d*.37f),w-2,1,ArtDirection.Cream);
                }
                if(p.Model=="golf")
                {
                    foreach(float x in new[]{-w/2,w/2})Prims.Box(root,"PracticeFence",new Vector3(x,3,d*.5f),new Vector3(.12f,6,d),Mats.Lit(ArtDirection.Sage));
                    Prims.Box(root,"DrivingCanopy",new Vector3(0,3.8f,1),new Vector3(w,.2f,3),Mats.Lit(ArtDirection.Sage));
                }
                if(p.Model=="restaurant"||p.Model=="ktv"||p.Model=="bar")
                {
                    var trim=Mats.Lit(p.Tier>=2?ArtDirection.Brass:ArtDirection.Clay);
                    Prims.Box(root,"FacadeLeft",new Vector3(-w/2+.25f,2,d/2),new Vector3(.5f,4,d),trim);
                    Prims.Box(root,"FacadeRight",new Vector3(w/2-.25f,2,d/2),new Vector3(.5f,4,d),trim);
                    Prims.Box(root,"VenueCanopy",new Vector3(0,4.2f,.8f),new Vector3(w+.5f,.25f,2.5f),trim);
                    if(p.Tier>=2)foreach(float x in new[]{-w*.4f,w*.4f})Tree(root,new Vector3(x,0,-2));
                }
            }
            else if(p.Kind==PlaceKind.Station)PlannedStation(root,p.Name,w,d);
            else PlannedBuilding(root,p.Model,p.Tier,w,d,p);
            // The route node is on the road; its approach ends at the actual public door.
            var delta=door-node;
            var approach=Prims.Box(_root,"Approach_"+p.Id,(node+door)*.5f+Vector3.up*.08f,new Vector3(2,.12f,delta.magnitude),Mats.Lit(Sidewalk));
            if(delta.sqrMagnitude>.01f)approach.transform.rotation=Quaternion.LookRotation(delta);
            vis.LabelPos=root.TransformPoint(new Vector3(0,!venue && p.Kind!=PlaceKind.Station?PlannedPlaceHeight(p)+1:5,d*.3f));
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
            Color wall=Facades[seed%Facades.Length];
            if(model=="oldhome")
            {
                float gray=.57f+(variation%5)*.045f;
                wall=new Color(gray,gray*.95f,gray*.86f);
            }
            if(model=="luxury_apartment")wall=ArtDirection.Cream;
            if(model=="warehouse")wall=ArtDirection.Blue;
            var material=Mats.Lit(wall);
            var body=Prims.Box(root,"Building",new Vector3(0,h/2,d/2),new Vector3(w,h,d),material,true);
            if(p!=null)body.AddComponent<PlaceMarker>().PlaceId=p.Id;
            Prims.Box(root,"Foundation",new Vector3(0,.18f,d/2),new Vector3(w+.5f,.36f,d+.5f),Mats.Lit(Sidewalk));
            Prims.Box(root,"Door",new Vector3(0,1.2f,-.08f),new Vector3(model=="warehouse"?w*.6f:1.6f,2.4f,.13f),Mats.Lit(ArtDirection.Ink));
            Prims.Box(root,"EntranceAwning",new Vector3(0,2.6f,-.5f),new Vector3(Mathf.Min(w-1,4),.2f,1.5f),Mats.Lit(tier>1?ArtDirection.Brass:ArtDirection.Sage));
            if(model=="villa")
            {
                // Paired pitched roof planes make villas distinct from flat-roof rental blocks.
                foreach(int side in new[]{-1,1})
                {
                    var roof=Prims.Box(root,"PitchedRoof",new Vector3(side*w*.25f,h+1,d*.5f),new Vector3(w*.56f,.3f,d+1),Mats.Lit(ArtDirection.Clay));
                    roof.transform.localRotation=Quaternion.Euler(0,0,-side*23);
                }
                Prims.Box(root,"Balcony",new Vector3(w*.26f,3.5f,-.7f),new Vector3(w*.38f,.22f,1.8f),Mats.Lit(ArtDirection.Cream));
                foreach(int side in new[]{-1,1})
                {
                    Prims.Box(root,"GardenHedge",new Vector3(side*(w/2+.8f),.65f,d*.5f),new Vector3(.7f,1.3f,d),Mats.Lit(ArtDirection.Sage));
                }
            }
            else
            {
                if(model=="warehouse")
                {
                    foreach(int side in new[]{-1,1})
                    {
                        var roof=Prims.Box(root,"WarehouseRoof",new Vector3(side*w*.25f,h+.6f,d/2),new Vector3(w*.53f,.2f,d+.5f),Mats.Lit(ArtDirection.Blue));
                        roof.transform.localRotation=Quaternion.Euler(0,0,-side*9);
                    }
                }
                Prims.Box(root,"FlatRoof",new Vector3(0,h+.15f,d/2),new Vector3(w+.5f,.3f,d+.5f),Mats.Lit(ArtDirection.Ink));
                if(model=="oldhome")RoofClutter(root,new Vector3(0,h+.3f,d/2),w,d);
                if(model=="luxury_apartment")
                    foreach(float x in new[]{-w*.38f,w*.38f})Prims.Box(root,"VerticalTrim",new Vector3(x,h/2,-.13f),new Vector3(.55f,h,.2f),Mats.Lit(ArtDirection.Brass));
            }
            if(model=="shop" || model=="oldhome" && variation%3!=0)
            {
                // Old mixed-use street: small shop fronts under unequal residential floors.
                float awningWidth=Mathf.Min(w-.5f,model=="oldhome"?w*.8f:w-.5f);
                int panels=Mathf.Max(4,Mathf.RoundToInt(awningWidth/.6f));
                for(int i=0;i<panels;i++)
                    Prims.Box(root,"StripedAwning",new Vector3(-awningWidth/2+(i+.5f)*awningWidth/panels,2.75f,-.55f),
                        new Vector3(awningWidth/panels,.12f,1.15f),Mats.Lit(i%2==0?ArtDirection.Cream:ArtDirection.Clay));
                foreach(int side in new[]{-1,1})
                    Prims.Box(root,"GroundFloorWindow",new Vector3(side*w*.3f,1.25f,-.15f),new Vector3(w*.22f,1.7f,.1f),Mats.Lit(ArtDirection.Blue));
            }
            // Reuse tiled window materials rather than thousands of individual window objects.
            WindowFace(root,new Vector3(0,h*.58f,-.12f),w-.8f,h-3,wall,false);
            WindowFace(root,new Vector3(-w/2-.04f,h*.55f,d/2),d-.7f,h-2.5f,wall,true);
            if(p!=null)
            {
                Color fascia=p.Kind==PlaceKind.Agency?ArtDirection.Sage:tier==3?ArtDirection.Ink:SignColors[StableCityHash(p.Id)%SignColors.Length];
                Prims.Box(root,"Fascia",new Vector3(0,3.3f,-.2f),new Vector3(w-.4f,.9f,.16f),Mats.Lit(fascia));
                Prims.SignText(root,p.Name,new Vector3(0,3.3f,-.31f),w-.8f,.65f,ArtDirection.Cream);
                if(p.Kind==PlaceKind.Agency)Prims.SignText(root,"本街房源 · 租售办理",new Vector3(0,2.25f,-.3f),w-1,.45f,ArtDirection.Cream);
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
            Prims.SignText(root,"M  "+name,new Vector3(0,2.45f,-.55f),w,.7f,ArtDirection.Cream);
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
            for(int band=0;band<4;band++)
            {
                for(int x=-190;x<260;x+=22)
                {
                    var at=new Vector3(x,0,band*135-4.8f);Tree(props,at);
                    Prims.Box(props,"StreetLamp",at+new Vector3(3,2.2f,0),new Vector3(.13f,4.4f,.13f),Mats.Lit(ArtDirection.Ink));
                    Prims.Box(props,"LampHead",at+new Vector3(3,4.4f,.3f),new Vector3(.6f,.2f,1.2f),Mats.Lit(ArtDirection.Cream));
                }
            }
            foreach(var street in _city.Streets)
            {
                var agency=_city.OfKind(PlaceKind.Agency).FirstOrDefault(p=>p.StreetId==street.id);
                if(agency==null)continue;
                Vector3 at=NodeWorld(agency.NodeId);
                Prims.SignText(props,street.name,at+new Vector3(0,5,-1),22,1,ArtDirection.Cream);
            }
        }
    }
}
