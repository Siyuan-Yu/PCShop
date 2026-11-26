using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    internal static class PaintedWoodland
    {
        private static readonly Dictionary<(int height,int species,int aspect,bool detailed,bool boughs),Mesh> Trees=new();
        private static Material _paint;

        internal static Mesh Template(int height,int species,int aspect,bool detailed=false,bool boughs=false)
        {
            var key=(height,species,aspect,detailed,boughs);
            if(Trees.TryGetValue(key,out var mesh) && mesh!=null)return mesh;
            var positions=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color32>();
            var normals=new List<Vector3>();var indices=new List<int>();
            var up=Vector3.up;
            int Vertex(Vector3 pos,Vector3 normal,Vector2 tex,float shade)
            {
                int index=positions.Count;positions.Add(pos*height);normals.Add(normal.normalized);uv.Add(tex);
                byte light=(byte)(255*Mathf.Clamp01(shade));colors.Add(new Color32(light,light,light,255));return index;
            }
            Vector2 Leaf(int slot,float x,float y)=>new Vector2((slot%2)*.5f+.009f+x*.482f,(1-slot/2)*.5f+.104f+y*.382f);
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
            float breadth=(willow?1.01f:1.06f)*(.92f+(aspect%4)*.043f);
            var cardOffsets=new List<Vector2>();
            var tangents=new List<Vector4>();
            // Fixed 3D trunks and branches carry several independent clumps. Each clump turns about
            // its own world-space centre, so the lower and upper crowns retain parallax on a camera orbit.
            void Clump(Vector3 center,float width,float length,bool flip,float brightness,int slot)
            {
                int start=positions.Count;
                for(int row=0;row<2;row++)for(int col=0;col<2;col++)
                {
                    float x=(col-.5f)*width,y=(row-.5f)*length;
                    Vertex(center+new Vector3(x,y,0),up,Leaf(slot,flip?1-col:col,row),brightness);
                    cardOffsets.Add(new Vector2(x*height,y*height));
                }
                Quad(start,start+2,start+3,start+1);
            }
            void CompleteBarkOffsets()
            {
                while(cardOffsets.Count<positions.Count) cardOffsets.Add(Vector2.zero);
            }
            if(boughs)
            {
                Vector3 Radial(float angle)=>new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 lean=Radial(turn);
                Vector3 fork=up*(.32f+aspect%3*.028f)+lean*.038f;
                Vector3 leader=fork+up*.21f-lean*.053f;
                Vector3 top=up*(.851f+(aspect%3)*.028f)-lean*.075f;
                // Single tapering trunk with buttresses flowing into the ground and a slight lean.
                const int sides=9;
                for(int ring=0;ring<4;ring++)
                {
                    Vector3 axis=ring==0 ? Vector3.zero : ring==1 ? up*.068f+lean*.003f : ring==2 ? fork*.51f-lean*.013f : fork;
                    for(int side=0;side<=sides;side++)
                    {
                        float azimuth=side*Mathf.PI*2/sides;
                        var radial=Radial(azimuth+turn);
                        float flare=.5f+.5f*Mathf.Cos(azimuth*3+.35f);
                        float radius=ring==0 ? .042f+.031f*flare : ring==1 ? .039f+.004f*flare : ring==2 ? .031f : .026f;
                        int vertex=Vertex(axis+radial*radius,radial,new Vector2(.028f+side*.049f,.013f+ring*.023f),.81f+ring*.045f);
                        if(ring>0 && side>0) Quad(vertex-sides-2,vertex-1,vertex,vertex-sides-1);
                    }
                }
                Limb(fork,leader,.029f,.020f);
                Limb(leader,top,.020f,.005f);

                var tips=new Vector3[5];
                for(int branch=0;branch<tips.Length;branch++)
                {
                    Vector3 outward=Radial(turn+branch*2.40f);
                    Vector3 basePoint=Vector3.Lerp(fork,leader,branch*.20f);
                    Vector3 elbow=basePoint+outward*(.11f*breadth)+up*(.104f+branch*.010f);
                    tips[branch]=outward*((.25f+(branch%3)*.025f)*breadth)+up*(.563f+branch*.048f);
                    Limb(basePoint,elbow,.020f-branch*.0015f,.012f);
                    Limb(elbow,tips[branch],.012f,.004f);
                    Vector3 offshoot=Vector3.Lerp(elbow,tips[branch],.70f)+up*.075f
                        +Vector3.Cross(up,outward)*.058f;
                    Limb(elbow,offshoot,.008f,.002f);
                }
                CompleteBarkOffsets();
                for(int branch=0;branch<tips.Length;branch++)
                    Clump(tips[branch]+up*.033f,breadth*(.47f+branch%2*.055f),.395f+branch%3*.027f,
                        (branch+aspect)%2==0,.81f+branch*.036f,(branch+aspect)%4);
                Clump(top+lean*.145f-up*.085f,breadth*.50f,.41f,true,.93f,(aspect+2)%4);
                Clump(top,breadth*.53f,.41f,false,1f,(aspect+1)%4);
            }
            else
            {
                var joint=new Vector3(.028f*Mathf.Cos(turn),.31f,.028f*Mathf.Sin(turn));
                var leader=joint+new Vector3(-.033f,.29f,.014f);
                Limb(Vector3.zero,joint,.035f,.024f);Limb(joint,leader,.024f,.009f);
                for(int branch=0;branch<3;branch++)
                {
                    float a=turn+branch*2.4f;
                    var tip=new Vector3(Mathf.Cos(a)*.20f,.49f+branch*.047f,Mathf.Sin(a)*.20f);
                    Limb(joint+up*(branch*.025f),tip,.015f,.0055f);
                }
                CompleteBarkOffsets();
                Clump(new Vector3(0,.672f,0),breadth,.70f,false,1f,species);
                if(detailed)
                {
                    for(int j=0;j<3;j++)
                    {
                        float a=turn+j*2.39996f;
                        var center=new Vector3(Mathf.Cos(a)*breadth*.23f,.58f+.045f*j,Mathf.Sin(a)*breadth*.23f);
                        Clump(center,breadth*.51f,willow?.48f:.39f,j%2==0,.87f+j*.04f,species);
                    }
                }
            }
            for(int j=0;j<positions.Count;j++) tangents.Add(new Vector4(1,0,0,1));

            mesh=new Mesh{name="树冠 "+height+"/"+species+"/"+aspect};
            mesh.SetVertices(positions);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetNormals(normals);
            mesh.SetUVs(2,cardOffsets);mesh.SetTangents(tangents);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(new Vector3(0,height*.20f,height*1.10f));mesh.bounds=bounds;Trees[key]=mesh;return mesh;
        }


        private static Material _summer;
        private static readonly Material[] StreetBoughs=new Material[4];
        private static Material CanopyPaint(bool summer=true,int crownDistrict=0)
        {
            bool boughs=crownDistrict>0;
            var material=boughs ? StreetBoughs[crownDistrict] : summer ? _summer : _paint;
            if (material != null) return material;
            material=new Material(Resources.Load<Shader>("OldTown/Crown"))
            {
                name=boughs ? "庭荫阔叶 / "+crownDistrict : summer ? "夏日香樟、垂柳与花树" : "远山杂树林", enableInstancing=true
            };
            string atlas=boughs ? "streetBoughs" : summer ? "summerCanopies" : "townCanopies";
            material.SetTexture("_BaseMap",Resources.Load<Texture2D>("OldTown/Surfaces/"+atlas));
            material.SetFloat("_Cutoff",boughs ? .43f : .32f);
            if(boughs)
            {
                var tint=crownDistrict==3 ? new Vector4(.77f,.92f,1.14f,1) :
                    crownDistrict==2 ? new Vector4(.83f,1f,1.06f,1) : new Vector4(1f,.98f,.86f,1);
                material.SetVector("_LeafTone",tint);
                material.SetFloat("_PaintedBough",1);
                StreetBoughs[crownDistrict]=material;
            }
            else if(summer)_summer=material;else _paint=material;
            return material;
        }

        internal static MeshRenderer Plant(Transform parent,Vector3 foot,int height,int species,int seed,bool summer=true,int district=0)
        {
            var go=new GameObject("庭荫树",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);go.transform.localPosition=foot;
            bool boughs=district>0 && summer && (species==0 || species==3);
            go.GetComponent<MeshFilter>().sharedMesh=Template(height,species,(seed & 0x7fffffff)%6,true,boughs);
            ForestFloorShade.Footprint(parent,foot+Vector3.up*.055f,height*(boughs?.88f:1.28f),height*(boughs?.78f:1.12f));
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=CanopyPaint(summer,boughs ? district : 0);
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
