using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private void PlantVacantPlots(Transform parent,RoadSurfaceGeometry.Index occupied,RoadSurfaceGeometry.Index paving,List<Vector3> shore)
        {
            bool Overlap(RoadSurfaceGeometry.Index idx,Vector2[] outline) =>
                idx.Nearby(outline).Any(i=>RoadSurfaceGeometry.Intersect(outline,idx.Polygons[i]).Length>=3);
            var centers=new List<Vector2>();
            foreach(var p in _city.PublicPlaces.Where(BuildsPlace))
            {
                int seed=StableCityHash(p.Id);
                if(IsLakeAccessNode(p.NodeId) || (p.Model!="oldhome" && p.Model!="shop" && p.Model!="apartment" &&
                    p.Model!="luxury_apartment" && p.Model!="villa"))continue;
                bool rural=OutskirtsStreet(p.StreetId);
                if(!rural && p.Model=="villa" && seed%2!=0)continue;
                var door=NodeWorld(p.NodeId)+new Vector3((float)p.EntranceX,0,(float)p.EntranceY);
                var q=Quaternion.Euler(0,(float)p.Heading,0);
                var back=q*Vector3.forward;var right=q*Vector3.right;
                float depth=(float)p.Depth,width=(float)p.Width;
                if(GameSession.IsCommercialPremises(p))
                {
                    var room=RoomSpec.ForShop(p.Size);depth=Mathf.Max(depth,(float)room.Depth);width=Mathf.Max(width,(float)room.Width);
                }
                float fw=rural?10+seed%4:6.5f,fd=rural?6.4f:4.4f;
                foreach(var at in new[]{door+back*(depth+fd*.5f+1.1f),door+back*depth*.69f+right*(width*.5f+fw*.5f+1.2f),
                    door+back*depth*.69f-right*(width*.5f+fw*.5f+1.2f),door+back*(depth+fd*1.7f)})
                {
                    var center=new Vector2(at.x,at.z);float spacing=rural?16:11;
                    if(centers.Any(c=>(c-center).sqrMagnitude<spacing*spacing))continue;
                    var axis=new Vector2(back.x,back.z)*(fd*.5f+.4f);
                    var rect=RoadSurfaceGeometry.Strip(center-axis,center+axis,fw+.8f);
                    if(Overlap(occupied,rect)||Overlap(paving,rect)||Overlap(_streetRoadEnvelopes,rect))continue;
                    if (rect.Append(center).Any(v=>MountainHeight(v.x/LakeScale,v.y/LakeScale)>.05f ||
                        WithinLake(new Vector3(v.x,0,v.y)/LakeScale,shore)))continue;
                    centers.Add(center);occupied.Add(rect);
                    var bounds=Rect.MinMaxRect(rect.Min(v=>v.x)-.6f,rect.Min(v=>v.y)-.6f,rect.Max(v=>v.x)+.6f,rect.Max(v=>v.y)+.6f);
                    _quarterYards.Add(bounds,bounds);
                    var plot=new GameObject((rural?"菜园 ":"街角花圃 ")+p.Id).transform;plot.SetParent(parent,false);
                    plot.localPosition=at;plot.localRotation=q;
                    BuildPlantedCourt(plot,fw,fd,rural,seed);
                    break;
                }
            }
        }

        private void PlantOldHomesShade(Transform parent,RoadSurfaceGeometry.Index reserved,RoadSurfaceGeometry.Index paving,List<Vector3> shore)
        {
            // The elder trees frame the first computer shop and the lean-to on its right.
            var place=_city.PublicPlaces.FirstOrDefault(p=>p.Id==StarterShop && BuildsPlace(p));
            if(place==null)return;
            var door=NodeWorld(place.NodeId)+new Vector3((float)place.EntranceX,0,(float)place.EntranceY);
            var rotation=Quaternion.Euler(0,(float)place.Heading,0);
            foreach(var tree in new[]{(11.6f,.8f,8),(-9f,7.5f,7),(9.6f,15f,9)})
            {
                var pos=door+rotation*new Vector3(tree.Item1,0,(float)place.Depth*.25f+tree.Item2);
                var xy=new Vector2(pos.x,pos.z);
                var patch=RoadSurfaceGeometry.Strip(xy-Vector2.up*.54f,xy+Vector2.up*.54f,1.08f);
                bool Obstructed(RoadSurfaceGeometry.Index index)=>index.Nearby(patch)
                    .Any(i=>RoadSurfaceGeometry.Intersect(patch,index.Polygons[i]).Length>=3);
                if(Obstructed(reserved)||Obstructed(paving)||Obstructed(_streetRoadEnvelopes)||WithinLake(pos/LakeScale,shore))continue;
                reserved.Add(patch);
                var bounds=Rect.MinMaxRect(xy.x-3.6f,xy.y-3.6f,xy.x+3.6f,xy.y+3.6f);
                _quarterYards.Add(bounds,bounds);
                PaintedWoodland.Plant(parent,pos,tree.Item3,0,tree.Item3*11);
            }
        }

        // Side and rear gardens occupy only land left over after buildings, entrance paving and allotments.
        private void PlantNeighbourhoodEdges(Transform parent,RoadSurfaceGeometry.Index occupied,RoadSurfaceGeometry.Index paving,List<Vector3> shore)
        {
            bool Touches(RoadSurfaceGeometry.Index index,Vector2[] footprint)=>
                index.Nearby(footprint).Any(i=>RoadSurfaceGeometry.Intersect(footprint,index.Polygons[i]).Length>=3);
            foreach(var place in _city.PublicPlaces.Where(BuildsPlace))
            {
                if(IsLakeAccessNode(place.NodeId) || place.Model=="racetrack" || place.Model=="station")continue;
                int district=ArtDistrict(place.StreetId), seed=StableCityHash(place.Id)%7919;
                var node=NodeWorld(place.NodeId);
                var entrance=new Vector2(node.x+(float)place.EntranceX,node.z+(float)place.EntranceY);
                float angle=(float)place.Heading*Mathf.Deg2Rad;
                var back=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));var across=new Vector2(back.y,-back.x);
                float w=(float)place.Width,d=(float)place.Depth;
                if(GameSession.IsCommercialPremises(place))
                {
                    var shop=RoomSpec.ForShop(place.Size);w=Mathf.Max(w,(float)shop.Width);d=Mathf.Max(d,(float)shop.Depth);
                }
                float length=Mathf.Clamp(d*.88f,6.2f,13.5f), width=district==0?3.55f:3.85f;
                int planted=0;
                foreach(int edge in new[]{-1,1,0})
                {
                    var axis=edge==0?across:back;
                    var center=edge==0?entrance+back*(d+width*.5f+1.7f):
                        entrance+back*(d*.58f)+across*(edge*(w*.5f+width*.5f+1.55f));
                    var rect=RoadSurfaceGeometry.Strip(center-axis*(length*.5f),center+axis*(length*.5f),width);
                    if(Touches(occupied,rect)||Touches(paving,rect)||Touches(_streetRoadEnvelopes,rect))continue;
                    if(rect.Any(p=>MountainHeight(p.x/LakeScale,p.y/LakeScale)>.04f ||
                        WithinLake(new Vector3(p.x,0,p.y)/LakeScale,shore)))continue;
                    occupied.Add(rect);
                    var bounds=Rect.MinMaxRect(rect.Min(p=>p.x),rect.Min(p=>p.y),rect.Max(p=>p.x),rect.Max(p=>p.y));
                    _quarterYards.Add(bounds,bounds);
                    var yard=new GameObject("街坊庭地 "+place.Id).transform;yard.SetParent(parent,false);
                    yard.localPosition=new Vector3(center.x,0,center.y);
                    yard.localRotation=Quaternion.LookRotation(new Vector3(axis.x,0,axis.y));
                    DressNeighbourhoodEdge(yard,width,length,district,seed+edge+1);
                    if(++planted>=2)break;
                }
            }
        }

        private static void DressNeighbourhoodEdge(Transform yard,float width,float length,int district,int seed)
        {
            var planting=new StreetPlanting();var fittings=new MiniatureDetailMesh();
            var stone=DistrictFinishes.Tile("cutStone",Vector2.one*.42f);
            var wood=DistrictFinishes.Tile("walnut",Vector2.one*.42f);
            float side=seed%2==0?1:-1;
            if(district==0 || district==1)
            {
                Prims.Box(yard,"院内小路",new Vector3(-side*width*.24f,.025f,0),new Vector3(width*.39f,.046f,length),
                    DistrictFinishes.Tile(district==0?"cultivatedSoil":"graniteWalk",new Vector2(.65f,length/2.5f)));
                if(district==1 && seed%3!=1)
                {
                    QuarterModules.Add(yard,"CycleShelter",new Vector3(0,.05f,length*.25f),Vector3.one*.71f,90);
                    QuarterModules.Add(yard,"Cycle",new Vector3(-side*.32f,.05f,length*.25f),Vector3.one*.86f,65);
                }
                if(district==0 && seed%3==1)QuarterModules.Add(yard,"Crates",new Vector3(-side*.78f,.06f,length*.28f),Vector3.one*.85f,8);
            }
            if(district==0 && seed%3!=1)
            {
                // A low service shed occupies the rear end of an already cleared yard.
                float z=length*.25f, run=Mathf.Min(3.1f,length*.39f),span=width*.70f;
                var wall=DistrictFinishes.Tile("ivoryPlaster",Vector2.one*.65f);
                fittings.Box(new Vector3(0,.98f,z),new Vector3(span,1.88f,run),wall);
                fittings.Box(new Vector3(0,1.97f,z),new Vector3(span+.34f,.13f,run+.32f),
                    DistrictFinishes.Tile(seed%2==0?"quarterClay":"patina",Vector2.one),Quaternion.Euler(-4,0,0));
                QuarterModules.Add(yard,"CargoBay",new Vector3(0,.05f,z-run*.5f-.025f),new Vector3(span/4.3f,.60f,.58f));
                QuarterModules.Add(yard,"Crates",new Vector3(-side*width*.27f,.06f,z-run*.5f-.44f),Vector3.one*.65f,9);
            }
            if(district>=2 && seed%3!=1)
            {
                var pergola=new Vector3(-side*.34f,0,length*.20f);
                foreach(int end in new[]{-1,1})foreach(int leg in new[]{-1,1})
                    fittings.Box(pergola+new Vector3(leg*.82f,1.12f,end*1.35f),new Vector3(.11f,2.24f,.11f),wood);
                for(int slat=-3;slat<=3;slat++)
                    fittings.Box(pergola+new Vector3(0,2.27f,slat*.45f),new Vector3(1.98f,.13f,.17f),wood);
                QuarterModules.Add(yard,"Bench",pergola+Vector3.up*.05f,Vector3.one*.83f,90);
            }
            float x=side*width*.39f;
            if(district==0)
            {
                for(float z=-length*.46f;z<=length*.46f;z+=2f)
                    fittings.Box(new Vector3(x,.46f,z),new Vector3(.11f,.92f,.11f),wood);
                fittings.Box(new Vector3(x,.57f,0),new Vector3(.08f,.12f,length*.92f),wood);
            }
            if(district==3 || district==1 && seed%3==2)
            {
                fittings.Box(new Vector3(x,.39f,length*.09f),new Vector3(.24f,.78f,length*.72f),stone);
                fittings.Box(new Vector3(x,.81f,length*.09f),new Vector3(.31f,.08f,length*.73f),stone);
            }
            if(district==1 && seed%4==0)
                MasonryPatina.Place(yard,MasonryPatina.Detail.Paving,new Vector3(-side*width*.24f,.064f,-length*.29f),
                    new Vector2(.88f,1.32f),Quaternion.Euler(90,seed%17,0));
            int clumps=Mathf.FloorToInt(length/1.10f);
            for(int j=0;j<clumps;j++)
            {
                float z=(j+.5f)/clumps*length-length*.5f;
                var species=district==0?PlantSpecies.Grass:district==2?PlantSpecies.SilverGrass:PlantSpecies.Hydrangea;
                var at=new Vector3(x-side*(.55f+.34f*Mathf.Sin(j+seed)),.06f,z);
                planting.Add(j%4==0?PlantSpecies.Boxwood:species,at,1.38f+j%3*.26f,.70f+j%3*.18f,seed+j);
            }
            PaintedWoodland.Plant(yard,new Vector3(side*.40f,0,-length*.22f),5+seed%3,district!=0 && seed%6==1?2:seed%3==0?3:0,seed);
            planting.Build(yard,"沿边花木");fittings.Build(yard,"庭院界面");
        }

        private static void BuildPlantedCourt(Transform root,float w,float d,bool food,int seed)
        {
            var plants=new StreetPlanting();
            var carpentry=new MiniatureDetailMesh();
            var timber=DistrictFinishes.Tile("walnut",Vector2.one*.36f);
            var stone=DistrictFinishes.Tile("cutStone",Vector2.one*.32f);
            var earth=DistrictFinishes.Tile("cultivatedSoil",Vector2.one*.45f);
            Prims.Box(root,"Garden earth",new Vector3(0,.032f,0),new Vector3(w,.055f,d),
                DistrictFinishes.Tile(food?"cultivatedSoil":"graniteWalk",new Vector2(w/3,d/3)));
            int rows=food?4:2;
            for(int row=0;row<rows;row++)
            {
                float z=(row-(rows-1)*.5f)*(d-.85f)/rows;
                float rowWidth=w-1.05f;
                carpentry.Box(new Vector3(0,.11f,z),new Vector3(rowWidth,.17f,(d-.85f)/rows*.68f),earth);
                for(int i=0;i<Mathf.FloorToInt(rowWidth/.75f);i++)
                {
                    int k=row*17+i+seed%997;
                    var pos=new Vector3(-rowWidth*.5f+.46f+i*.75f,.17f,z+.10f*Mathf.Sin(k));
                    var herb=food?(row==rows-1 || row==0 && seed%3==0?PlantSpecies.BeanTrellis:PlantSpecies.Cabbage):
                        (row==0?PlantSpecies.Spirea:PlantSpecies.Broadleaf);
                    if(herb==PlantSpecies.BeanTrellis && i%2!=0)continue;
                    float h=herb==PlantSpecies.BeanTrellis?1.52f:food?.47f:.82f;
                    plants.Add(herb,pos,food?.87f:1.17f,h*(.86f+k%5*.054f),k);
                }
            }
            // Short fence on the rear and one side. Open edges meet the grass without boxing in every plot.
            for(int i=0;i<=4;i++)
                carpentry.Box(new Vector3((i/4f-.5f)*w,.52f,d*.49f),new Vector3(.11f,1.04f,.11f),timber);
            foreach(float y in new[]{.39f,.75f})
                carpentry.Box(new Vector3(0,y,d*.49f),new Vector3(w,.085f,.09f),timber);
            carpentry.Box(new Vector3(-w*.49f,.16f,0),new Vector3(.17f,.30f,d),stone);
            for(int i=0;i<8;i++)
                plants.Add(i%3==0?PlantSpecies.GoldenBush:PlantSpecies.Grass,
                    new Vector3((i/7f-.5f)*w,.04f,d*.49f+.18f),1.18f,.45f+i%3*.18f,i+seed%401);
            if(food)
            {
                // A low cold frame occupies part of one existing bed.
                if(seed%3==1)
                {
                    float run=w*.38f;
                    var cover=DistrictFinishes.Tile("sageCloth",Vector2.one*.65f,ArtDirection.Hex("D6D8BB"));
                    float x=w*.21f-.2f,z=-d*.27f;
                    for(int panel=0;panel<4;panel++)
                    {
                        float a=(panel+.5f)*Mathf.PI/4;
                        carpentry.Box(new Vector3(x,.18f+.63f*Mathf.Sin(a),z+.70f*Mathf.Cos(a)),
                            new Vector3(run,.07f,.56f),cover,Quaternion.Euler(90-a*Mathf.Rad2Deg,0,0));
                    }
                    foreach(int end in new[]{-1,1})
                        carpentry.Box(new Vector3(x+end*run*.48f,.32f,z),new Vector3(.10f,.50f,1.45f),timber);
                }
                else QuarterModules.Add(root,"Crates",new Vector3(-w*.39f,.09f,d*.29f),Vector3.one*.7f,8);
                for(int j=0;j<9;j++)
                {
                    float z=(j/8f-.5f)*d;
                    plants.Add(j%3==0?PlantSpecies.GoldenBush:PlantSpecies.Grass,new Vector3(-w*.49f,.16f,z),.78f+j%3*.17f,.37f+j%4*.13f,j+seed%271);
                }
            }
            if(!food)QuarterModules.Add(root,"Bench",new Vector3(w*.25f,.08f,-d*.49f),Vector3.one);
            else if(seed%3==0)QuarterModules.Add(root,"Crates",new Vector3(w*.43f,.09f,-d*.39f),Vector3.one*.7f);
            plants.Build(root,"种植带");carpentry.Build(root,"田埂和矮篱");
        }
    }
}
