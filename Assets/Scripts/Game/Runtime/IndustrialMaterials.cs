using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Deterministic, reusable surface maps. Presentation only; no random simulation state.
    public static class IndustrialMaterials
    {
        private static readonly Dictionary<string,Texture2D> Textures=new Dictionary<string,Texture2D>();
        private static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();
        public static Material Surface(string kind,Color color,Vector2 repeat)
        {
            string key=kind+color+repeat;
            if(Materials.TryGetValue(key,out var found)&&found!=null)return found;
            var mat=new Material(kind=="foliage"?Resources.Load<Shader>("Shaders/PixelFoliage"):Shader.Find("Universal Render Pipeline/Lit")){name="Pixel town / "+kind};
            mat.SetColor("_BaseColor",color);
            if(kind!="foliage"){mat.SetTexture("_BaseMap",Texture(kind));mat.mainTextureScale=repeat;}
            mat.SetFloat("_Smoothness",kind=="metal"?.36f:kind=="wood"?.22f:.13f);
            mat.SetFloat("_Metallic",kind=="metal"?.55f:0);
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
                else if(kind=="wood")v*=.78f+.22f*Mathf.PerlinNoise(x*.15f,y*.006f);
                else if(kind=="asphalt")v*=.8f+.2f*noise;
                v=Mathf.Round(v*8)/8;
                byte b=(byte)(Mathf.Clamp01(v)*255);px[y*n+x]=new Color32(b,b,b,255);
            }
            var tex=new Texture2D(n,n,TextureFormat.RGBA32,true,true){name="Industrial "+kind,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point,anisoLevel=1};
            tex.SetPixels32(px);tex.Apply(true);Textures[kind]=tex;return tex;
        }
    }
}
