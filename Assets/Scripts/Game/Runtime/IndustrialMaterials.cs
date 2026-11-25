using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Deterministic, reusable surface maps. Presentation only; no random simulation state.
    public static class IndustrialMaterials
    {
        private static readonly Dictionary<string,Texture2D> Textures=new Dictionary<string,Texture2D>();
        private static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();
        private static Material _lamplitGlass;
        private static readonly Dictionary<string,Texture2D> Normals=new Dictionary<string,Texture2D>();

        internal enum WaterBasin { Still, Pond, Pool }
        private static readonly Dictionary<WaterBasin,Material> Waters=new();
        internal static Material GardenWater(WaterBasin basin=WaterBasin.Still)
        {
            if(Waters.TryGetValue(basin,out var material) && material!=null)return material;
            var shader=Resources.Load<Shader>("Shaders/LakeWater");
            material=shader!=null?new Material(shader):Mats.Lit(ArtDirection.Hex("4099AF"),.7f);
            material.SetColor("_BaseColor",ArtDirection.Hex(basin==WaterBasin.Pool?"416F70":basin==WaterBasin.Pond?"344E47":"46655F"));
            if(shader!=null)
            {
                material.SetFloat("_Basin",(float)basin);
                if(basin==WaterBasin.Pool)material.SetTexture("_PoolLight",Resources.Load<Texture2D>("OldTown/Surfaces/poolLight"));
                material.SetFloat("_ReflectMask",basin==WaterBasin.Pond?1:0);
                material.SetTexture("_WaterMarks",Resources.Load<Texture2D>("OldTown/Surfaces/windRipples"));
                material.SetTexture("_WindField",Resources.Load<Texture2D>("OldTown/Surfaces/meadowVariation"));
            }
            Waters[basin]=material;
            return material;
        }
        public static Material LamplitWindow()
        {
            if (_lamplitGlass != null) return _lamplitGlass;
            _lamplitGlass = new Material(Mats.Glow(ArtDirection.Hex("C88A42"),new Color(1.5f,.66f,.19f),false))
            {
                name = "Miniature city / lit glass"
            };
            _lamplitGlass.SetTexture("_EmissionMap", Texture("lamplit_glass"));
            _lamplitGlass.SetTexture("_BaseMap", Texture("lamplit_glass"));
            _lamplitGlass.SetFloat("_Smoothness", .34f);
            return _lamplitGlass;
        }
        public static Material Surface(string kind,Color color,Vector2 repeat)
        {
            string key=kind+color+repeat;
            if(Materials.TryGetValue(key,out var found)&&found!=null)return found;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Industrial / "+kind};
            mat.SetColor("_BaseColor",color);mat.SetTexture("_BaseMap",Texture(kind));mat.mainTextureScale=repeat;
            mat.SetFloat("_Smoothness",kind=="metal"?.36f:kind=="wood"?.22f:.13f);
            mat.SetFloat("_Metallic",kind=="metal"?.55f:0);
            if(kind=="brick"||kind=="claytiles"||kind=="paving"||kind=="wood"||kind=="concrete"||kind=="meadow")
            {
                mat.SetTexture("_BumpMap",NormalMap(kind));mat.SetFloat("_BumpScale",kind=="meadow"?.35f:.75f);mat.EnableKeyword("_NORMALMAP");
            }
            mat.enableInstancing=true;Materials[key]=mat;return mat;
        }
        public static Texture2D Texture(string kind)
        {
            if(Textures.TryGetValue(kind,out var cached)&&cached!=null)return cached;
            const int n=256;var px=new Color32[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float noise=Mathf.PerlinNoise(x*.19f+4,y*.19f+7),broad=Mathf.PerlinNoise(x*.033f,y*.033f);
                float v=.84f+.12f*noise+.04f*broad;
                if(kind=="brick")
                {
                    int row=y/32,bx=(x+(row%2)*32)%64;
                    v*=y%32<2||bx<2?.56f:.88f+.12f*Mathf.PerlinNoise((x+(row%2)*32)/64, row*7.3f);
                    if(y%32==2||bx==2)v=1;
                }
                else if(kind=="metal")
                {
                    int rib=x%32;v*=rib<3?.62f:rib<6?1.08f:.93f;
                    if(y%128<2)v*=.58f;
                    if((x%32-10)*(x%32-10)+(y%128-9)*(y%128-9)<5)v=.48f;
                }
                else if(kind=="paving")
                {if(x%64<2||y%64<2)v*=.62f;else if(x%64==2||y%64==2)v=1;}
                else if(kind=="claytiles")
                {
                    int row=y/32, tileX=x%32, tileY=y%32;
                    float terracotta=.85f+.14f*Mathf.PerlinNoise((x/32)*2.8f,row*3.1f);
                    float crown=1f-.13f*Mathf.Pow(Mathf.Abs(tileX-16)/16f,1.5f);
                    v*=tileX<2||tileY<2?.57f:tileY<4?1.04f:terracotta*crown;
                }
                else if(kind=="lamplit_glass")
                {
                    float u=(x+.5f)/n, t=(y+.5f)/n;
                    // Soft light inside each pane and dark curtains at the outer jambs.
                    float curtain=Mathf.SmoothStep(.29f,1f,Mathf.Clamp01(Mathf.Min(u,1-u)*12));
                    float lamp=Mathf.Exp(-6*((u-.37f)*(u-.37f)+(t-.57f)*(t-.57f)));
                    v=curtain*(.38f+.60f*lamp)*(.91f+.09f*noise);
                }
                else if(kind=="wood")v*=.78f+.22f*Mathf.PerlinNoise(x*.15f,y*.006f);
                else if(kind=="old_plaster")
                {
                    float u=x/(float)n,t=y/(float)n;
                    float worn=WrappedNoise(u,t,3.0f)*.68f+WrappedNoise(u,t,8.0f)*.32f;
                    v=.83f+.13f*worn+.04f*noise;
                }
                else if(kind=="asphalt")v*=.8f+.2f*noise;
                else if(kind=="meadow")
                {
                    // Wrap both octaves to avoid visible edges across very large lawns.
                    float u=x/(float)n,t=y/(float)n;
                    float patch=WrappedNoise(u,t,4.0f)*.55f+WrappedNoise(u,t,11.0f)*.45f;
                    float blades=WrappedNoise(u,t,40.0f);
                    var grass=Color.Lerp(new Color(.50f,.65f,.36f),new Color(.89f,.92f,.62f),patch);
                    px[y*n+x]=(Color32)(grass*(.78f+blades*.22f));
                    continue;
                }
                byte b=(byte)(Mathf.Clamp01(v)*255);px[y*n+x]=new Color32(b,b,b,255);
            }
            var tex=new Texture2D(n,n,TextureFormat.RGBA32,true,true){name="Industrial "+kind,wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            tex.SetPixels32(px);tex.Apply(true);Textures[kind]=tex;return tex;
        }

        private static Texture2D NormalMap(string kind)
        {
            if(Normals.TryGetValue(kind,out var result)&&result!=null)return result;
            var source=Texture(kind);int n=source.width;var heights=source.GetPixels();var pixels=new Color[n*n];
            float H(int x,int y)=>heights[((y+n)%n)*n+(x+n)%n].grayscale;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                var normal=new Vector3((H(x-1,y)-H(x+1,y))*2.2f,(H(x,y-1)-H(x,y+1))*2.2f,1).normalized;
                pixels[y*n+x]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f,1);
            }
            result=new Texture2D(n,n,TextureFormat.RGBA32,true,true){name="Miniature surface relief / "+kind,wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            result.SetPixels(pixels);result.Apply(true);Normals[kind]=result;return result;
        }

        private static float WrappedNoise(float x,float y,float size)
        {
            float a=Mathf.Lerp(Mathf.PerlinNoise(17+x*size,19+y*size),Mathf.PerlinNoise(17+(x-1)*size,19+y*size),x);
            float b=Mathf.Lerp(Mathf.PerlinNoise(17+x*size,19+(y-1)*size),Mathf.PerlinNoise(17+(x-1)*size,19+(y-1)*size),x);
            return Mathf.Lerp(a,b,y);
        }
    }
}
