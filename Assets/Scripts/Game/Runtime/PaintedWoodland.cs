using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    internal static class PaintedWoodland
    {
        private static readonly Dictionary<(int height,int species,int aspect,bool detailed),Mesh> Trees=new();
        private static Material _paint;

        internal static Mesh Template(int height,int species,int aspect,bool detailed=false)
        {
            var key=(height,species,aspect,detailed);
            if(Trees.TryGetValue(key,out var mesh) && mesh!=null)return mesh;
            var positions=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color32>();
            var normals=new List<Vector3>();var indices=new List<int>();
            var right=Vector3.right;var up=Vector3.up;
            float u=(species%2)*.5f,v=(1-species/2)*.5f;
            int Vertex(Vector3 pos,Vector3 normal,Vector2 tex,float shade)
            {
                int index=positions.Count;positions.Add(pos*height);normals.Add(normal.normalized);uv.Add(tex);
                byte light=(byte)(255*Mathf.Clamp01(shade));colors.Add(new Color32(light,light,light,255));return index;
            }
            Vector2 Leaf(float x,float y)=>new Vector2(u+.009f+x*.482f,v+.104f+y*.382f);
            void Quad(int a,int b,int c,int d){indices.Add(a);indices.Add(b);indices.Add(c);indices.Add(a);indices.Add(c);indices.Add(d);}
            void Limb(Vector3 from,Vector3 to,float footRadius,float tipRadius)
            {
                var axis=(to-from).normalized;
                var n=Vector3.Cross(axis,Vector3.forward).normalized;var b=Vector3.Cross(axis,n);
                int first=positions.Count;
                const int sides=5;
                for(int i=0;i<=sides;i++)
                {
                    float a=i*(2*Mathf.PI/sides);
                    var radial=n*Mathf.Cos(a)+b*Mathf.Sin(a);
                    float x=.04f+i*.082f;
                    Vertex(from+radial*footRadius,radial,new Vector2(x,.013f),.78f);
                    Vertex(to+radial*tipRadius,radial,new Vector2(x,.083f),.95f);
                    if(i>0){int j=first+i*2;Quad(j-2,j-1,j+1,j);}
                }
            }
            float turn=aspect*.87f;bool willow=species==1;
            var joint=new Vector3(.028f*Mathf.Cos(turn),.31f,.028f*Mathf.Sin(turn));
            var leader=joint+new Vector3(-.033f,.29f,.014f);
            Limb(Vector3.zero,joint,.035f,.024f);Limb(joint,leader,.024f,.009f);
            float breadth=(willow?1.01f:1.06f)*(.92f+(aspect%4)*.043f);
            for(int branch=0;branch<3;branch++)
            {
                float a=turn+branch*2.4f;
                var tip=new Vector3(Mathf.Cos(a)*.20f,.49f+branch*.047f,Mathf.Sin(a)*.20f);
                Limb(joint+up*(branch*.025f),tip,.015f,.0055f);
            }
            var cardOffsets=new List<Vector2>(positions.Count+16);
            var tangents=new List<Vector4>(positions.Count+16);
            for(int j=0;j<positions.Count;j++){cardOffsets.Add(Vector2.zero);tangents.Add(new Vector4(1,0,0,1));}
            // Fixed 3D trunks and branches carry several independent clumps. Each clump turns about
            // its own world-space centre, so the lower and upper crowns retain parallax on a camera orbit.
            void Clump(Vector3 center,float width,float length,bool flip,float brightness)
            {
                int start=positions.Count;
                for(int row=0;row<2;row++)for(int col=0;col<2;col++)
                {
                    float x=(col-.5f)*width,y=(row-.5f)*length;
                    Vertex(center+new Vector3(x,y,0),up,Leaf(flip?1-col:col,row),brightness);
                    cardOffsets.Add(new Vector2(x*height,y*height));tangents.Add(new Vector4(1,0,0,1));
                }
                Quad(start,start+2,start+3,start+1);
            }
            Clump(new Vector3(0,.672f,0),breadth,.70f,false,1f);
            if(detailed)
            {
                for(int j=0;j<3;j++)
                {
                    float a=turn+j*2.39996f;
                    var center=new Vector3(Mathf.Cos(a)*breadth*.23f,.58f+.045f*j,Mathf.Sin(a)*breadth*.23f);
                    Clump(center,breadth*.51f,willow?.48f:.39f,j%2==0,.87f+j*.04f);
                }
            }

            mesh=new Mesh{name="树冠 "+height+"/"+species+"/"+aspect};
            mesh.SetVertices(positions);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetNormals(normals);
            mesh.SetUVs(2,cardOffsets);mesh.SetTangents(tangents);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(new Vector3(0,height*.20f,height*1.10f));mesh.bounds=bounds;Trees[key]=mesh;return mesh;
        }


        private static Material CanopyPaint()
        {
            if(_paint==null)
            {
                var source=Resources.Load<Shader>("OldTown/Crown");
                _paint=new Material(source){name="远山杂树林",enableInstancing=true};
                _paint.SetTexture("_BaseMap",Resources.Load<Texture2D>("OldTown/Surfaces/townCanopies"));
                _paint.SetFloat("_Cutoff",.32f);
            }
            return _paint;
        }

        internal static MeshRenderer Plant(Transform parent,Vector3 foot,int height,int species,int seed)
        {
            var go=new GameObject("庭荫树",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);go.transform.localPosition=foot;
            go.GetComponent<MeshFilter>().sharedMesh=Template(height,species,(seed & 0x7fffffff)%6,true);
            ForestFloorShade.Footprint(parent,foot+Vector3.up*.055f,height*1.28f,height*1.12f);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=CanopyPaint();
            return renderer;
        }

        internal static void Build(Transform root,BakedCityMap.ForestChunk chunk,CityLandscapeResources owner)
        {
            var mesh=new Mesh{name=chunk.Name,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(chunk.Parts.Select(p=>new CombineInstance{mesh=p.Mesh,transform=Matrix4x4.Translate(p.Position)}).ToArray());
            var go=new GameObject(chunk.Name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial=CanopyPaint();owner.Owned.Add(mesh);
            ForestFloorShade.Build(root,chunk,owner);
        }
    }
}
