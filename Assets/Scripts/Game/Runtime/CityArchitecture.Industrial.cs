using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static partial class CityArchitecture
    {
        // Dressing stays inside the existing lot, has no colliders or interactable markers.
        public static void IndustrialDetails(Transform parent,string model,float w,float d,float h,int seed)
        {
            var root=new GameObject("Industrial architectural finish").transform;root.SetParent(parent,false);
            Color steel=ArtDirection.Hex("394855"),silver=ArtDirection.Hex("9DA6A8"),ochre=ArtDirection.Hex("C0954F");
            void Steel(string name,Vector3 at,Vector3 size)=>Prims.Box(root,name,at,size,Mats.Lit(steel,.38f,.4f));
            void Pipe(Vector3 at,float height,float radius)
            {
                Prims.Shape(PrimitiveType.Cylinder,root,"Service pipe",at,new Vector3(radius*2,height/2,radius*2),Mats.Lit(silver,.38f,.55f));
                foreach(float y in new[]{-.36f,0,.36f})Prims.Shape(PrimitiveType.Cylinder,root,"Pipe collar",at+Vector3.up*height*y,new Vector3(radius*2.45f,.045f,radius*2.45f),Mats.Lit(steel,.42f,.45f));
            }
            if(model=="oldhome")
            {
                // Low-rise townhouses: dormer windows, gutters, service pipes and metal awnings.
                Steel("Front gutter",new Vector3(0,h+.32f,-.17f),new Vector3(w+.25f,.12f,.14f));
                Steel("Rainwater downpipe",new Vector3(w*.46f,h*.5f,-.17f),new Vector3(.10f,h,.10f));
                for(int side=-1;side<=1;side+=2)
                {
                    var at=new Vector3(side*w*.24f,h+.65f,d*.18f);
                    Box(root,"Dormer housing",at,new Vector3(w*.15f,.7f,d*.15f),ArtDirection.Blue);
                    Prims.Box(root,"Dormer glazing",at+new Vector3(0,0,-d*.076f),new Vector3(w*.10f,.45f,.025f),Mats.Glow(ArtDirection.Hex("45515B"),new Color(1.7f,1.1f,.45f)));
                    Steel("Dormer cap",at+Vector3.up*.39f,new Vector3(w*.17f,.1f,d*.17f));
                }
            }
            else if(model=="warehouse"||model=="office"||model=="shop")
            {
                if(model=="shop")
                {
                    for(float floor=3.3f;floor<h-.4f;floor+=2.7f)
                    {
                        Steel("Facade floor cornice",new Vector3(0,floor,-.15f),new Vector3(w+.15f,.11f,.22f));
                        Steel("Side cornice",new Vector3(w*.5f+.08f,floor,d*.5f),new Vector3(.18f,.11f,d));
                    }
                    foreach(int side in new[]{-1,1})
                        Steel("Masonry corner trim",new Vector3(side*(w*.5f-.08f),h*.5f,-.12f),new Vector3(.16f,h,.18f));
                }
                float y=h+(model=="warehouse"||model=="shop"?1.05f:.45f);
                var hvac=new Vector3(w*.24f,y+.45f,d*.68f);
                Prims.Box(root,"Roof air handling unit",hvac,new Vector3(w*.24f,.9f,d*.21f),IndustrialMaterials.Surface("metal",silver,new Vector2(2,1)));
                for(int i=0;i<4;i++)Steel("Vent grille",hvac+new Vector3((i-1.5f)*w*.043f,0,-d*.106f),new Vector3(.055f,.7f,.04f));
                Steel("Rooftop service duct",new Vector3(0,y+.18f,d*.68f),new Vector3(w*.28f,.35f,.45f));
                Pipe(new Vector3(-w*.31f,y+.85f,d*.73f),1.6f,.18f);
                Steel("Vent rain hood",new Vector3(-w*.31f,y+1.68f,d*.73f),new Vector3(.6f,.12f,.6f));
                if(model=="warehouse")
                {
                    // Existing wholesale buildings acquire industrial roof furniture, never new factories.
                    Pipe(new Vector3(-w*.25f,y+1.45f,d*.33f),2.6f,.62f);
                    Prims.Shape(PrimitiveType.Cylinder,root,"Tank cap",new Vector3(-w*.25f,y+2.78f,d*.33f),new Vector3(1.4f,.09f,1.4f),Mats.Lit(steel,.4f,.55f));
                    for(int i=0;i<5;i++)Box(root,"Safety fascia",new Vector3(-w*.39f+i*.32f,.45f,-.15f),new Vector3(.19f,.6f,.04f),i%2==0?ochre:steel);
                    for(int i=-2;i<=2;i++)Steel("Warehouse structural rib",new Vector3(i*w*.19f,h*.5f,-.18f),new Vector3(.15f,h,.20f));
                }
            }
            if(model=="shop"||model=="warehouse")
            {
                foreach(int side in new[]{-1,1})
                {
                    Steel("Shopfront steel jamb",new Vector3(side*w*.46f,1.4f,-.21f),new Vector3(.12f,2.8f,.16f));
                    Box(root,"Storefront stone sill",new Vector3(side*w*.29f,.26f,-.24f),new Vector3(w*.24f,.15f,.24f),silver);
                    Prims.Box(root,"Warm fascia light",new Vector3(side*w*.3f,3.87f,-.35f),new Vector3(w*.24f,.045f,.05f),Mats.Glow(ochre,new Color(2.1f,1.2f,.42f)));
                }
            }
        }
    }
}
