using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    internal static class MasonryPatina
    {
        internal enum Detail { Plinth, Rain, Notice, Paving }
        private static readonly Mesh[] Patches=new Mesh[4];
        private static Material pigment;

        internal static void Place(Transform parent,Detail detail,Vector3 center,Vector2 span,Quaternion rotation)
        {
            var index=(int)detail;
            if(Patches[index]==null)
            {
                float x=index%2*.5f, y=(1-index/2)*.5f;
                var mesh=new Mesh{name="墙面细节 "+detail};
                mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};
                mesh.uv=new[]{new Vector2(x+.004f,y+.004f),new Vector2(x+.004f,y+.496f),new Vector2(x+.496f,y+.496f),new Vector2(x+.496f,y+.004f)};
                mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();Patches[index]=mesh;
            }
            if(pigment==null)
            {
                pigment=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="旧墙与路面修补",enableInstancing=true};
                pigment.SetTexture("_BaseMap",Resources.Load<Texture2D>("OldTown/Surfaces/streetWear"));
                pigment.SetFloat("_AlphaClip",1);pigment.SetFloat("_Cutoff",.34f);
                pigment.SetFloat("_Smoothness",.09f);pigment.EnableKeyword("_ALPHATEST_ON");
                pigment.SetOverrideTag("RenderType","TransparentCutout");pigment.renderQueue=(int)RenderQueue.AlphaTest;
            }
            var go=new GameObject(Patches[index].name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);go.transform.localPosition=center;
            go.transform.localRotation=rotation;go.transform.localScale=new Vector3(span.x,span.y,1);
            go.GetComponent<MeshFilter>().sharedMesh=Patches[index];var renderer=go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=pigment;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        internal static void Exterior(Transform root,float width,float depth,float height,int seed,bool older)
        {
            ForestFloorShade.Footprint(root,new Vector3(0,.105f,depth*.49f),width+5,depth+4,true);
            foreach(int side in new[]{-1,1})
            {
                var wall=Quaternion.Euler(0,-side*90,0);
                float x=side*(width*.5f+.032f);
                Place(root,Detail.Plinth,new Vector3(x,.48f,depth*(side<0?.37f:.66f)),new Vector2(depth*(older?.60f:.33f),.88f),wall);
                Place(root,Detail.Rain,new Vector3(x,height-.55f,depth*(side<0?.66f:.29f)),new Vector2(depth*.29f,.86f),wall);
            }
            if(older || seed%3==0)
                Place(root,Detail.Notice,new Vector3(-width*.5f-.035f,1.45f,depth*.19f),new Vector2(.46f,.58f),Quaternion.Euler(0,90,0));
        }
    }
}
