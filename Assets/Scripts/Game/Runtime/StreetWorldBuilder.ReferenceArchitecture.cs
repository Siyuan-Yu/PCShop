using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private static float ResidenceArtHeight(float height,int tier,int seed)
            => Mathf.Clamp(Mathf.Round(height*.65f/2.8f),3,tier>1?5:4)*2.8f;

        private void ReferenceResidence(Transform root,float w,float d,float h,int tier,int seed)
        {
            // Compact residential blocks with real recessed bays and balconies replace repeated thin towers.
            float top=ResidenceArtHeight(h,tier,seed),bw=w*.83f,bd=d*.66f,center=d*.62f;
            Color wall=ArtDirection.Hex(seed%3==0?"E6DDD2":seed%3==1?"D7DCD8":"ECE5DB");
            Color trim=ArtDirection.Hex("E6E6DF"),blue=ArtDirection.Hex(tier>1?"527F91":"517791");
            var masonry=DistrictFinishes.Tile(DistrictFinishes.Masonry(tier),new Vector2(w/4,top/3.4f),Color.Lerp(Color.white,wall,.26f));
            Prims.Box(root,"ResidenceGarden",new Vector3(0,.035f,d/2),new Vector3(w,.07f,d),
                IndustrialMaterials.Surface("meadow",Grass,new Vector2(w/10,d/10)));
            Prims.Box(root,"Low-rise residential body",new Vector3(0,top/2,center),new Vector3(bw,top,bd),masonry,true);
            var detail=new MiniatureDetailMesh();var stone=Mats.Lit(trim);var roof=DistrictFinishes.Tile(DistrictFinishes.Roof(tier),Vector2.one*.27f);
            detail.Box(new Vector3(0,.18f,center),new Vector3(bw+.4f,.36f,bd+.4f),stone);
            detail.Box(new Vector3(0,top+.13f,center),new Vector3(bw+.65f,.26f,bd+.65f),stone);
            detail.Box(new Vector3(0,top+.28f,center),new Vector3(bw-.38f,.10f,bd-.38f),roof);
            foreach(int side in new[]{-1,1})
            {
                detail.Box(new Vector3(side*(bw/2-.08f),top+.50f,center),new Vector3(.20f,.60f,bd+.2f),stone);
                detail.Box(new Vector3(0,top+.50f,center+side*(bd/2-.08f)),new Vector3(bw,.60f,.20f),stone);
                detail.Box(new Vector3(side*(bw/2-.1f),top/2,center-bd/2-.11f),new Vector3(.28f,top,.23f),stone);
            }
            float accessW=Mathf.Min(3.2f,bw*.25f),accessD=Mathf.Min(3.0f,bd*.28f);
            detail.Box(new Vector3(bw*.20f,top+.86f,center+bd*.23f),new Vector3(accessW,1.15f,accessD),stone);
            detail.Box(new Vector3(bw*.20f,top+1.48f,center+bd*.23f),new Vector3(accessW+.24f,.13f,accessD+.24f),roof);
            int variant=(seed&int.MaxValue)%4;
            if(tier>=2 && variant!=2)
            {
                var lightwell=new Vector3(-bw*.20f,top+.59f,center-bd*.16f);
                detail.Box(lightwell,new Vector3(bw*.32f,.40f,bd*.22f),stone);
                detail.Box(lightwell+Vector3.up*.23f,new Vector3(bw*.29f,.10f,bd*.195f),
                    DistrictFinishes.Tile("civicGlazing",Vector2.one*.45f));
                for(int rib=0;rib<3;rib++)
                    detail.Box(lightwell+new Vector3((rib-1)*bw*.091f,.33f,0),new Vector3(.09f,.07f,bd*.22f),stone);
            }
            if(variant==1 || variant==3)
            {
                var roofPlants=new StreetPlanting(maintained:true);
                float planterX=-bw*.30f,planterZ=center+bd*.30f;
                detail.Box(new Vector3(planterX,top+.50f,planterZ),new Vector3(bw*.27f,.40f,1.15f),stone);
                for(int k=0;k<4;k++)
                    roofPlants.Add(k==2?PlantSpecies.Hydrangea:PlantSpecies.Boxwood,
                        new Vector3(planterX+(k-1.5f)*bw*.066f,top+.70f,planterZ),1.12f,.68f,seed+k);
                roofPlants.Build(root,"住宅屋面种植槽");
            }
            for(int face=0;face<4;face++)
            {
                var elevation=new GameObject("Residential framed elevation").transform;elevation.SetParent(root,false);
                elevation.localPosition=new Vector3(0,0,center);elevation.localRotation=Quaternion.Euler(0,face*90,0);
                float width=face%2==0?bw:bd,depth=face%2==0?bd:bw;
                var facade=new MiniatureDetailMesh();
                int columns=Mathf.Clamp(Mathf.FloorToInt(width/3.5f),2,5),floors=Mathf.RoundToInt(top/2.8f);
                float spacing=width/(columns+.25f),windowW=Mathf.Min(2.2f,spacing*.64f);
                for(int floor=0;floor<floors;floor++)
                {
                    float y=1.6f+floor*2.8f;
                    for(int col=0;col<columns;col++)
                    {
                        float x=(col-(columns-1)*.5f)*spacing;
                        if(floor==0&&face==0&&Mathf.Abs(x)<2)continue;
                        if ((col+floor+face)%3!=0)
                            QuarterModules.Add(elevation,"SideWindow",new Vector3(x,y-.79f,-depth/2-.02f),new Vector3(windowW/1.47f,1.37f,.82f));
                        else facade.Window(new Vector3(x,y,-depth/2-.015f),windowW,1.65f,trim,(seed+col+floor*3+face)%4!=0);
                        if(floor>0&&(face==0||face==2)&&col%2==(floor+seed)%2)
                            facade.Balcony(new Vector3(x,y-1.0f,-depth/2),windowW+.40f,seed+floor+col);
                    }
                    if(floor>0)facade.Box(new Vector3(0,y-1.40f,-depth/2-.05f),new Vector3(width+.15f,.12f,.2f),stone);
                }
                facade.Build(elevation,"Window reveals, balcony railings and flowers");
            }
            float front=center-bd/2;
            detail.Window(new Vector3(0,1.3f,front-.04f),1.65f,2.35f,trim,true);
            detail.Box(new Vector3(0,2.66f,front-.75f),new Vector3(3.2f,.16f,1.6f),roof);
            detail.Box(new Vector3(0,.08f,front/2),new Vector3(2,.12f,front+.7f),
                IndustrialMaterials.Surface("paving",Sidewalk,Vector2.one));
            foreach(int side in new[]{-1,1})
            {
                detail.FlowerBed(new Vector3(side*w*.28f,.02f,.75f),w*.28f,.85f,seed+side);
                detail.FlowerBed(new Vector3(side*w*.32f,.02f,front*.72f),w*.20f,.70f,seed+side+3,false);
                PaintedWoodland.Plant(root,new Vector3(side*w*.36f,0,front*.43f),5+seed%2,side<0 && seed%5==0?2:0,seed+side+4);
                // Knee-high garden wall with an open central route to the original door.
                detail.Box(new Vector3(side*(w*.27f+.5f),.26f,.10f),new Vector3(w*.46f-1,.52f,.22f),stone);
            }
            QuarterModules.Add(root,"Condenser",new Vector3(bw*.34f,top+.40f,center-bd*.21f),Vector3.one);
            QuarterModules.Add(root,tier==1?"StripedCanopy":"TinCanopy",new Vector3(0,2.83f,front-.09f),new Vector3(.48f,.8f,.81f));
            QuarterModules.Add(root,"Bench",new Vector3(-w*.28f,.08f,front*.64f),Vector3.one);
            detail.Build(root,"Residential architecture and front garden");
        }
    }
}
