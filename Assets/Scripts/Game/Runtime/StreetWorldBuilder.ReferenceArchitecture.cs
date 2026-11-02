using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private void ReferenceResidence(Transform root,float w,float d,float h,int tier,int seed)
        {
            Color stone=ArtDirection.Hex(tier>1?"E5DDBB":"D4D5BA");
            Color blue=ArtDirection.Hex(tier>1?"458FB5":"6CA7C5");
            var masonry=IndustrialMaterials.Surface("concrete",stone,new Vector2(3,4));var glass=Mats.Lit(blue,.58f,.08f);
            Prims.Box(root,"ResidenceGarden",new Vector3(0,.04f,d/2),new Vector3(w,.08f,d),Mats.Lit(ArtDirection.Hex("78865D")));
            Prims.Box(root,"ResidenceForecourt",new Vector3(0,.10f,d*.15f),new Vector3(w*.86f,.12f,d*.30f),Mats.Lit(Sidewalk));
            Prims.Box(root,"Podium",new Vector3(0,1.05f,d*.59f),new Vector3(w*.87f,2.1f,d*.75f),masonry,true);
            int count=w>21?2:1;float towerW=count==2?w*.34f:w*.64f,towerD=d*.69f;
            for(int i=0;i<count;i++)
            {
                var tower=new GameObject("ResidentialTower").transform;tower.SetParent(root,false);
                tower.localPosition=new Vector3(count==2?(i==0?-1:1)*w*.235f:0,0,d*.59f);
                float top=h-(i==1?2.4f:0),height=top-2.1f;
                Prims.Box(tower,"TowerBody",new Vector3(0,2.1f+height/2,0),new Vector3(towerW,height,towerD),masonry,true);
                for(int side=0;side<4;side++)
                {
                    var face=new GameObject("TowerElevation").transform;face.SetParent(tower,false);face.localRotation=Quaternion.Euler(0,side*90,0);
                    float width=side%2==0?towerW:towerD,depth=side%2==0?towerD:towerW;
                    WindowFace(face,new Vector3(0,2.1f+height/2,-depth/2-.025f),width-.55f,height-.8f,stone,false);
                    foreach(int edge in new[]{-1,1})
                        Prims.Box(face,"StonePier",new Vector3(edge*(width/2-.17f),top/2,-depth/2-.10f),new Vector3(.27f,top,.20f),masonry);
                }
                for(float y=4.5f;y<top-.8f;y+=2.8f)
                {
                    Prims.Box(tower,"FloorCornice",new Vector3(0,y,0),new Vector3(towerW+.28f,.16f,towerD+.28f),masonry);
                    if(tier>1)
                    {
                        Prims.Box(tower,"BalconySlab",new Vector3(0,y,-towerD/2-.32f),new Vector3(towerW*.55f,.16f,.8f),masonry);
                        Prims.Box(tower,"BlueBalconyRail",new Vector3(0,y+.4f,-towerD/2-.7f),new Vector3(towerW*.55f,.64f,.07f),glass);
                        Prims.Box(tower,"BalconyRailCap",new Vector3(0,y+.74f,-towerD/2-.7f),new Vector3(towerW*.57f,.09f,.1f),masonry);
                    }
                }
                Prims.Box(tower,"CrownCornice",new Vector3(0,top+.1f,0),new Vector3(towerW+.65f,.28f,towerD+.65f),masonry);
                Prims.Box(tower,"SetbackPenthouse",new Vector3(0,top+.7f,towerD*.12f),new Vector3(towerW*.67f,1.2f,towerD*.58f),masonry);
                CityArchitecture.Roof(tower,new Vector3(0,top+1.32f,towerD*.12f),towerW*.71f,towerD*.64f,.65f,blue);
                foreach(int side in new[]{-1,1})
                    Prims.Box(tower,"RoofGarden",new Vector3(side*towerW*.37f,top+.38f,0),new Vector3(.6f,.35f,towerD*.68f),Mats.Lit(ArtDirection.Hex("5B714D")));
            }
            Prims.Box(root,"LobbyGlass",new Vector3(0,1.05f,d*.215f-.06f),new Vector3(w*.34f,1.85f,.1f),glass);
            Prims.Box(root,"LobbyCanopy",new Vector3(0,2.2f,d*.17f),new Vector3(w*.46f,.2f,d*.15f),masonry);
            Prims.Box(root,"EntryWalk",new Vector3(0,.13f,d*.10f),new Vector3(1.8f,.1f,d*.2f),Mats.Lit(Sidewalk));
            foreach(int side in new[]{-1,1})
            {
                CityVegetation.Build(root,new Vector3(side*w*.36f,0,d*.14f),seed+side);
                Prims.Box(root,"EntryFlowerBed",new Vector3(side*w*.26f,.24f,.45f),new Vector3(w*.23f,.45f,.65f),Mats.Lit(ArtDirection.Hex("62754E")));
                for(int j=-1;j<=1;j++)Prims.Shape(PrimitiveType.Sphere,root,"EntryFlowers",new Vector3(side*w*.26f+j*w*.06f,.53f,.45f),Vector3.one*.28f,Mats.Lit(ArtDirection.Hex("EAB24E")));
            }
        }
    }
}
