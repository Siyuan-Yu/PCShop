using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static partial class CityArchitecture
    {
        private static readonly Color Glass = ArtDirection.Hex("526977");
        private static readonly Color Limestone = ArtDirection.Hex("CEC9BA");
        private static readonly Color Lawn = ArtDirection.Hex("7B8C60");
        private static readonly Color Hedge = ArtDirection.Hex("617747");

        // Closed roof volume: no open triangles or intersecting wafer-thin roof slabs.
        private static void HipRoof(Transform root,Vector3 at,float w,float d,float rise,Color color)
        {
            var go=new GameObject("HippedTileRoof",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(root,false);go.transform.localPosition=at;
            Vector3[] p={new Vector3(-w/2,0,-d/2),new Vector3(w/2,0,-d/2),
                new Vector3(w/2,0,d/2),new Vector3(-w/2,0,d/2),
                new Vector3(0,rise,-d*.27f),new Vector3(0,rise,d*.27f)};
            int[] faces={0,4,1, 1,4,5, 1,5,2, 2,5,3, 3,5,4, 3,4,0, 0,1,2, 0,2,3};
            var vertices=new Vector3[faces.Length];var triangles=new int[faces.Length];
            for(int i=0;i<faces.Length;i++){vertices[i]=p[faces[i]];triangles[i]=i;}
            var uv=new Vector2[vertices.Length];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vertices[i].x*.9f,vertices[i].z*1.4f);
            var mesh=new Mesh{name="VillaHipRoof"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.uv=uv;
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=Mats.TileRoof(color);
            Box(root,"RoofRidge",at+Vector3.up*rise,new Vector3(.16f,.14f,d*.57f),color);
        }
        public static void Roof(Transform root,Vector3 at,float w,float d,float rise,Color color)=>HipRoof(root,at,w,d,rise,color);
        private static void VillaWindow(Transform parent,Vector3 at,float width,float height,float yaw=0)
        {
            var root=new GameObject("FramedVillaWindow").transform;root.SetParent(parent,false);
            root.localPosition=at;root.localRotation=Quaternion.Euler(0,yaw,0);
            Box(root,"StoneSurround",Vector3.zero,new Vector3(width+.23f,height+.23f,.15f),Limestone);
            bool lit=(Mathf.RoundToInt(at.x*7+at.y*11+at.z*3)&3)!=0;
            Prims.Box(root,"BlueGlass",new Vector3(0,0,-.09f),new Vector3(width,height,.04f),lit?Mats.Glow(Glass,new Color(1.3f,.88f,.42f)):Mats.Lit(Glass,.5f));
            Box(root,"Mullion",new Vector3(0,0,-.12f),new Vector3(.065f,height,.05f),Limestone);
            Box(root,"Transom",new Vector3(0,height*.1f,-.12f),new Vector3(width,.065f,.05f),Limestone);
        }
        public static void GardenVilla(Transform root,float w,float d,float configuredHeight,int seed)
            =>ModernGardenVilla(root,w,d,configuredHeight,seed);
        public static void ReferenceFacade(Transform root,string model,int tier,float w,float d,float h,int seed)
        {
            if(model=="oldhome" && h<=8)
                HipRoof(root,new Vector3(0,h+.32f,d/2),w+.45f,d+.45f,Mathf.Min(1.1f,w*.13f),ArtDirection.Hex("8F705B"));
            if(model=="luxury_apartment"||model=="apartment")
            {
                // Cream piers, blue bay glazing and stepped crowns follow the reference's towers.
                Color trim=tier>1?Limestone:ArtDirection.Cream;
                for(int side=-1;side<=1;side+=2)
                {
                    Box(root,"ResidentialCornerPier",new Vector3(side*(w*.5f-.28f),h/2,-.18f),new Vector3(.5f,h,.35f),trim);
                    Box(root,"BayGlazing",new Vector3(side*w*.27f,h*.56f,-.16f),new Vector3(w*.18f,h-3,.08f),Glass);
                }
                for(float y=3.1f;y<h;y+=2.8f)
                    Box(root,"ResidentialFloorBand",new Vector3(0,y,d/2),new Vector3(w+.17f,.15f,d+.17f),trim);
                Box(root,"SteppedRoofCrown",new Vector3(0,h+.55f,d*.57f),new Vector3(w*.65f,1,d*.65f),trim);
                Box(root,"CrownCap",new Vector3(0,h+1.12f,d*.57f),new Vector3(w*.7f,.16f,d*.7f),ArtDirection.Blue);
            }
            if(model=="shop" && tier==0)
            {
                HipRoof(root,new Vector3(0,h+.32f,d/2),w+.5f,d+.5f,1.15f,ArtDirection.Hex("53606A"));
                foreach(int side in new[]{-1,1})
                {
                    var at=new Vector3(side*w*.25f,h+.97f,d*.24f);
                    Box(root,"ShopDormer",at,new Vector3(w*.15f,.78f,d*.14f),ArtDirection.Blue);
                    VillaWindow(root,at+new Vector3(0,0,-d*.075f),w*.10f,.45f);
                    Box(root,"DormerSteelCap",at+Vector3.up*.44f,new Vector3(w*.18f,.13f,d*.17f),ArtDirection.Ink);
                }
            }
            if(model=="shop" && tier>0)
            {
                for(int side=-1;side<=1;side+=2)
                    Box(root,"ShopStonePier",new Vector3(side*(w/2-.3f),h/2,-.15f),new Vector3(.48f,h,.35f),Limestone);
                Box(root,"RetailCornice",new Vector3(0,h+.32f,d/2),new Vector3(w+.3f,.25f,d+.3f),Limestone);
                foreach(int side in new[]{-1,1})
                {
                    Box(root,"RetailRoofParapet",new Vector3(side*(w/2-.12f),h+.66f,d/2),new Vector3(.24f,.65f,d),Limestone);
                    Box(root,"RetailRoofParapet",new Vector3(0,h+.66f,d/2+side*(d/2-.12f)),new Vector3(w,.65f,.24f),Limestone);
                    Box(root,"RoofPlanter",new Vector3(side*w*.34f,h+.65f,d*.65f),new Vector3(w*.17f,.5f,d*.35f),Hedge);
                    Box(root,"ShopFrontPlanter",new Vector3(side*(w/2-.6f),.4f,-.4f),new Vector3(.8f,.8f,.7f),Limestone);
                    Prims.Shape(PrimitiveType.Sphere,root,"ShopFrontShrub",new Vector3(side*(w/2-.6f),.9f,-.4f),Vector3.one*.8f,Mats.Lit(Hedge));
                }
                Box(root,"RooftopAccess",new Vector3(w*.15f,h+1.1f,d*.73f),new Vector3(w*.22f,1.8f,d*.22f),Limestone);
                Box(root,"RooftopAccessCap",new Vector3(w*.15f,h+2.05f,d*.73f),new Vector3(w*.25f,.16f,d*.25f),ArtDirection.Hex("526271"));
            }
            if(model=="warehouse")
            {
                Color blue=ArtDirection.Hex("4F6477");
                HipRoof(root,new Vector3(0,h+.35f,d/2),w+.55f,d+.55f,.8f,blue);
                foreach(int side in new[]{-1,1})
                    Box(root,"WarehouseCornerColumn",new Vector3(side*(w/2-.25f),h/2,-.12f),new Vector3(.4f,h,.25f),Limestone);
                Box(root,"LoadingApron",new Vector3(0,.1f,-.7f),new Vector3(w,.15f,1.2f),Limestone);
            }
        }
    }
}
