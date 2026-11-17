using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static partial class CityArchitecture
    {
        internal static void StreetApron(Transform root,float width)
        {
            Prims.Box(root,"Stone entrance forecourt",new Vector3(0,.075f,-1.65f),new Vector3(width+.6f,.09f,3.6f),
                IndustrialMaterials.Surface("paving",ArtDirection.Hex("DDD0B5"),new Vector2(width/4,.9f)));
        }
        internal static void SculptedRoofTiles(Transform parent,Vector3 at,float w,float d,float rise,Color color,bool hip)
        {
            var b=new MiniatureDetailMesh();
            var mats=new[]{Mats.Lit(color,.14f),Mats.Lit(Color.Lerp(color,ArtDirection.Hex("DBA77D"),.045f),.12f),
                Mats.Lit(Color.Lerp(color,ArtDirection.Hex("76564D"),.04f),.14f)};
            int nx=Mathf.Clamp(Mathf.CeilToInt(w/.53f),8,44),nz=Mathf.Clamp(Mathf.CeilToInt(d/.64f),8,56);
            float sx=w/nx,sz=d/nz;
            float H(float x,float z) => Mathf.Max(0,hip?Mathf.Min(rise*(1-Mathf.Abs(x)/(w/2)),rise*(d/2-Mathf.Abs(z))/(d*.23f)):rise*(1-Mathf.Abs(x)/(w/2)));
            for(int row=0;row<nz;row++)for(int col=0;col<nx;col++)
            {
                float x=(col+.5f)*sx-w/2,z=(row+.5f)*sz-d/2;
                float dx=(H(x+.02f,z)-H(x-.02f,z))/.04f,dz=(H(x,z+.02f)-H(x,z-.02f))/.04f;
                var tilt=Quaternion.FromToRotation(Vector3.up,new Vector3(-dx,1,-dz).normalized);
                b.RoofTile(at+new Vector3(x,H(x,z)+.10f,z),new Vector3(sx*.96f,.075f,sz*.94f),mats[(((row*73856093)^(col*19349663))&int.MaxValue)%3],tilt);
            }
            float ridgeLength=hip?d*.55f:d;
            for(float z=-ridgeLength/2;z<ridgeLength/2;z+=.54f)
                b.Box(at+new Vector3(0,rise+.16f,z+.25f),new Vector3(.30f,.17f,.53f),mats[0]);
            b.Build(parent,"Overlapping dimensional roof tiles");
        }

        internal static void StreetLife(Transform root,float w,float d,float h,int seed,bool retail)
        {
            StreetApron(root,w);
            var details=new MiniatureDetailMesh();var wood=Mats.Lit(ArtDirection.Hex("93704D"));
            var metal=Mats.Lit(ArtDirection.Hex("405363"),.25f);
            foreach(int side in new[]{-1,1})
            {
                details.FlowerBed(new Vector3(side*w*.35f,0,-.60f),Mathf.Min(2.25f,w*.22f),.78f,seed+side);
                // Downpipes and stone corner blocks break up otherwise featureless side walls.
                details.Box(new Vector3(side*(w/2-.18f),h/2,-.17f),new Vector3(.09f,h,.09f),metal);
                if(retail)
                {
                    var lamp=Mats.Glow(ArtDirection.Hex("FFD58E"),new Color(1.7f,1.0f,.38f));
                    details.Box(new Vector3(side*(w/2-.5f),2.16f,-.32f),new Vector3(.29f,.42f,.23f),lamp);
                    details.Box(new Vector3(side*(w/2-.5f),2.42f,-.32f),new Vector3(.43f,.10f,.37f),metal);
                }
            }
            if(retail&&w>8)
            {
                float x=-w*.30f;
                details.Box(new Vector3(x,.55f,-1.18f),new Vector3(1.1f,1.1f,.12f),wood,Quaternion.Euler(-8,0,0));
                details.Box(new Vector3(x,.63f,-1.28f),new Vector3(.89f,.78f,.025f),metal,Quaternion.Euler(-8,0,0));
                for(int i=0;i<3;i++)details.Box(new Vector3(x,.82f-i*.16f,-1.35f),new Vector3(.57f-i*.1f,.035f,.018f),Mats.Lit(ArtDirection.Cream));
                details.FlowerBed(new Vector3(w*.29f,0,-1.12f),1.3f,.75f,seed+4);
            }
            details.Build(root,"Storefront furniture, lamps and planting");
        }
    }
}
