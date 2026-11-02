using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private static readonly Rect CommunityGarden=new Rect(-108,191,26,23);

        private void BuildReferenceLandscape()
        {
            var root=new GameObject("ReferenceNeighborhoodLandscape").transform;root.SetParent(_root,false);
            Garden(root,CommunityGarden,false);
            // The lake is part of the existing, interactive life_park property.
            var occupied=new List<Rect>{CommunityGarden};
            foreach(var b in _city.Blocks)
                occupied.Add(Rect.MinMaxRect((float)b.x-(float)b.width/2-2,(float)b.y-2,
                    (float)b.x+(float)b.width/2+2,(float)(b.y+b.depth)+2));
            foreach(var p in _city.Places)
            {
                Vector3 door=NodeWorld(p.NodeId)+new Vector3((float)p.EntranceX,0,(float)p.EntranceY);
                Quaternion rotation=Quaternion.Euler(0,(float)p.Heading,0);
                Vector3 lo=door,hi=door;
                foreach(int side in new[]{-1,1})foreach(int back in new[]{0,1})
                {
                    Vector3 corner=door+rotation*new Vector3(side*(float)p.Width/2,0,back*(float)p.Depth);
                    lo=Vector3.Min(lo,corner);hi=Vector3.Max(hi,corner);
                }
                occupied.Add(Rect.MinMaxRect(lo.x-2,lo.z-2,hi.x+2,hi.z+2));
                var node=NodeWorld(p.NodeId);
                occupied.Add(Rect.MinMaxRect(Mathf.Min(node.x,door.x)-2,Mathf.Min(node.z,door.z)-2,
                    Mathf.Max(node.x,door.x)+2,Mathf.Max(node.z,door.z)+2));
            }
            var roads=new List<Rect>();
            foreach(var n in _city.Map.Nodes)foreach(var edge in _city.Map.NeighborsOf(n.Id))
            {
                if(string.CompareOrdinal(n.Id,edge.To)>=0)continue;
                Vector3 a=NodeWorld(n.Id),b=NodeWorld(edge.To);
                float clearance=PlannedRoadWidth(a,b)/2+3;
                roads.Add(Rect.MinMaxRect(Mathf.Min(a.x,b.x)-clearance,Mathf.Min(a.z,b.z)-clearance,
                    Mathf.Max(a.x,b.x)+clearance,Mathf.Max(a.z,b.z)+clearance));
            }
            // Deterministic planting only in unused land: no RNG, path changes or extra colliders.
            int index=0;
            for(int z=10;z<535;z+=9)for(int x=-192;x<255;x+=9)
            {
                int hash=(x*73856093)^(z*19349663);
                var point=new Vector2(x+((hash&7)-3)*.35f,z+(((hash>>4)&7)-3)*.35f);bool clear=true;
                foreach(var rect in occupied)if(rect.Contains(point)){clear=false;break;}
                if(clear)foreach(var rect in roads)if(rect.Contains(point)){clear=false;break;}
                if(!clear)continue;
                CityVegetation.Build(root,new Vector3(point.x,0,point.y),index++);
            }
            StaticBatchingUtility.Combine(root.gameObject);
        }
        private void Garden(Transform parent,Rect area,bool lake)
        {
            var root=new GameObject(lake?"ApartmentLakeGarden":"CommunityFountainGarden").transform;
            root.SetParent(parent,false);root.localPosition=new Vector3(area.center.x,0,area.center.y);
            float w=area.width,d=area.height;
            var stone=Mats.Lit(ArtDirection.Hex("D4CDB0"));
            Prims.Box(root,"GardenLawn",new Vector3(0,.04f,0),new Vector3(w,.08f,d),Mats.Lit(ArtDirection.Hex("88A668")));
            foreach(int side in new[]{-1,1})
            {
                Prims.Box(root,"PerimeterWalk",new Vector3(0,.10f,side*(d/2-1)),new Vector3(w, .08f,1.5f),stone);
                Prims.Box(root,"PerimeterWalk",new Vector3(side*(w/2-1),.10f,0),new Vector3(1.5f,.08f,d),stone);
                for(int i=-1;i<=1;i++)
                {
                    CityVegetation.Build(root,new Vector3(i*w*.3f,0,side*(d/2-3)),i+5);
                    Prims.Box(root,"ParkBench",new Vector3(i*w*.26f,.55f,side*(d/2-2)),new Vector3(2,.2f,.6f),Mats.Lit(ArtDirection.Wood));
                }
            }
            if(lake)
            {
                Prims.Shape(PrimitiveType.Cylinder,root,"LakeStoneBank",new Vector3(0,.12f,0),new Vector3(w*.68f,.14f,d*.63f),stone);
                Prims.Shape(PrimitiveType.Cylinder,root,"LakeWater",new Vector3(0,.27f,0),new Vector3(w*.63f,.025f,d*.57f),Mats.Lit(ArtDirection.Hex("479FB3")));
                Prims.Box(root,"LakesideDeck",new Vector3(w*.21f,.36f,-d*.23f),new Vector3(5,.18f,3),Mats.Lit(ArtDirection.Wood));
            }
            else
            {
                Prims.Box(root,"GardenCrossPath",new Vector3(0,.13f,0),new Vector3(w,.07f,1.5f),stone);
                Prims.Box(root,"GardenCrossPath",new Vector3(0,.13f,0),new Vector3(1.5f,.07f,d),stone);
                Prims.Shape(PrimitiveType.Cylinder,root,"FountainBasin",new Vector3(0,.3f,0),new Vector3(5,.35f,5),stone);
                Prims.Shape(PrimitiveType.Cylinder,root,"FountainWater",new Vector3(0,.67f,0),new Vector3(4.3f,.025f,4.3f),Mats.Lit(ArtDirection.Hex("5FB4C7")));
                Prims.Shape(PrimitiveType.Cylinder,root,"FountainPedestal",new Vector3(0,1,0),new Vector3(.6f,.6f,.6f),stone);
                Prims.Shape(PrimitiveType.Sphere,root,"FountainCrown",new Vector3(0,1.65f,0),new Vector3(1.5f,.3f,1.5f),stone);
            }
        }
    }
}
