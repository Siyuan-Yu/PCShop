using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    // Decorative geometry is merged by material, with no colliders or gameplay state.
    internal sealed class MiniatureDetailMesh
    {
        internal sealed class Buffer
        {
            internal readonly List<Vector3> V = new List<Vector3>();
            internal readonly List<int> T = new List<int>();
        }
        private readonly Dictionary<Material, Buffer> _parts = new Dictionary<Material, Buffer>();
        internal static System.Action<Transform,Material,List<BakedDetailLayout.Part>> BakeModelSink;
        private static readonly Dictionary<int,Mesh> SharedDetails=new Dictionary<int,Mesh>();
        private static readonly Dictionary<string,Mesh[]> SharedDetailTrees=new Dictionary<string,Mesh[]>();
        private readonly Dictionary<Material,List<BakedDetailLayout.Part>> _instances=new Dictionary<Material,List<BakedDetailLayout.Part>>();
        private void Instance(Material material,Mesh mesh,Vector3 at,Vector3 scale,Quaternion rotation)
        {
            if(!_instances.TryGetValue(material,out var parts))_instances.Add(material,parts=new List<BakedDetailLayout.Part>());
            parts.Add(new BakedDetailLayout.Part {Mesh=mesh,Position=at,Scale=scale,Rotation=BakedDetailLayout.ValidRotation(rotation)});
        }
        private static Mesh DetailMesh(Buffer buffer)
        {
            var mesh=new Mesh {name="共用建筑细节",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(buffer.V);mesh.SetTriangles(buffer.T,0);mesh.RecalculateNormals();return mesh;
        }
        private static Mesh DetailTemplate(int kind,int seed,Material material)
        {
            seed=(seed&int.MaxValue)%1024;
            int key=kind*1024+seed;
            if(SharedDetails.TryGetValue(key,out var mesh)&&mesh!=null)return mesh;
            var sink=BakeModelSink;BakeModelSink=null;
            try
            {
                var detail=new MiniatureDetailMesh();
                if(kind==0)detail.Box(Vector3.zero,Vector3.one,material);
                else if(kind==1)detail.RoofTile(Vector3.zero,Vector3.one,material,Quaternion.identity);
                else detail.Lobe(Vector3.zero,Vector3.one,material,seed);
                mesh=DetailMesh(detail.Part(material));SharedDetails[key]=mesh;return mesh;
            }
            finally {BakeModelSink=sink;}
        }
        internal void Tree(Vector3 at,float height,int seed,Material leaf,Material tip,Material bark)
        {
            if(BakeModelSink==null)
            {
                var a=Part(leaf);var b=Part(tip);var c=Part(bark);
                CityVegetation.AppendTree(a.V,a.T,b.V,b.T,c.V,c.T,at,height,false,seed);return;
            }
            string key=height+":"+seed;
            if(!SharedDetailTrees.TryGetValue(key,out var meshes)||meshes[0]==null)
            {
                var buffers=new[]{new Buffer(),new Buffer(),new Buffer()};
                CityVegetation.AppendTree(buffers[0].V,buffers[0].T,buffers[1].V,buffers[1].T,buffers[2].V,buffers[2].T,Vector3.zero,height,false,seed);
                meshes=new[]{DetailMesh(buffers[0]),DetailMesh(buffers[1]),DetailMesh(buffers[2])};SharedDetailTrees[key]=meshes;
            }
            Instance(leaf,meshes[0],at,Vector3.one,Quaternion.identity);
            Instance(tip,meshes[1],at,Vector3.one,Quaternion.identity);
            Instance(bark,meshes[2],at,Vector3.one,Quaternion.identity);
        }

        internal Buffer Part(Material material)
        {
            if (!_parts.TryGetValue(material, out var part)) { part = new Buffer(); _parts.Add(material, part); }
            return part;
        }
        internal void Box(Vector3 at, Vector3 size, Material mat, Quaternion rotation = default)
        {
            rotation = BakedDetailLayout.ValidRotation(rotation);
            if(BakeModelSink!=null){Instance(mat,DetailTemplate(0,0,mat),at,size,rotation);return;}
            var b = Part(mat);
            Vector3 P(float x,float y,float z) => at + rotation * Vector3.Scale(new Vector3(x,y,z)*.5f,size);
            void Face(Vector3 a,Vector3 c,Vector3 d,Vector3 e)
            { int n=b.V.Count;b.V.Add(a);b.V.Add(c);b.V.Add(d);b.V.Add(e);b.T.Add(n);b.T.Add(n+1);b.T.Add(n+2);b.T.Add(n);b.T.Add(n+2);b.T.Add(n+3); }
            Face(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1));
            Face(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1));
            Face(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1));
            Face(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1));
            Face(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1));
            Face(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1));
        }
        internal void Lobe(Vector3 at,Vector3 radius,Material mat,int seed=0)
        {
            if(BakeModelSink!=null){Instance(mat,DetailTemplate(2,seed,mat),at,radius,Quaternion.identity);return;}
            var b=Part(mat);Lobe(b.V,b.T,at,radius,seed);
        }
        internal void RoofTile(Vector3 at,Vector3 size,Material material,Quaternion rotation)
        {
            rotation = BakedDetailLayout.ValidRotation(rotation);
            // The solid roof below closes the volume. Only expose the tile top and its lip.
            if(BakeModelSink!=null){Instance(material,DetailTemplate(1,0,material),at,size,rotation);return;}
            var b=Part(material);
            Vector3 P(float x,float y,float z)=>at+rotation*Vector3.Scale(new Vector3(x,y,z)*.5f,size);
            int n=b.V.Count;
            b.V.AddRange(new[]{P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1),
                P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1)});
            b.T.AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+4,n+5,n+6,n+4,n+6,n+7});
        }
        private static readonly Dictionary<int,Vector3[]> SharedLobeShapes = new Dictionary<int,Vector3[]>();
        internal static void Lobe(List<Vector3> v,List<int> t,Vector3 at,Vector3 radius,int seed=0,bool shared=false)
        {
            // World IDs may hash to billions; keep angular increments representable as floats.
            seed = (seed & int.MaxValue) % 1024;
            // Small flattened polygonal leaf masses, not a few oversized smooth spheres.
            const int sides=7,rings=4;
            // Shared and faceted foliage use identical unit points. Compute their
            // trigonometry once per seed, including flowers on every balcony.
            if(!SharedLobeShapes.TryGetValue(seed,out var shape))
            {
                shape=new Vector3[(rings+1)*(sides+1)];
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
                {
                    float phi=Mathf.PI*y/rings,a=x*Mathf.PI*2/sides+(y%2)*.18f+seed*.43f;
                    float r=1+.07f*Mathf.Sin(a*3+seed);
                    shape[y*(sides+1)+x]=new Vector3(Mathf.Cos(a)*Mathf.Sin(phi)*r,Mathf.Cos(phi),Mathf.Sin(a)*Mathf.Sin(phi)*r);
                }
                SharedLobeShapes.Add(seed,shape);
            }
            Vector3 Point(int y,int x)=>at+Vector3.Scale(shape[y*(sides+1)+x],radius);
            if(shared)
            {
                int start=v.Count;
                foreach(var point in shape)v.Add(at+Vector3.Scale(point,radius));
                for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
                { int a=start+y*(sides+1)+x,b=a+sides+1;
                  if(y>0){t.Add(a);t.Add(a+1);t.Add(b);}if(y<rings-1){t.Add(b);t.Add(a+1);t.Add(b+1);} }
                return;
            }
            void Tri(Vector3 a,Vector3 b,Vector3 c)
            { int n=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(n);t.Add(n+1);t.Add(n+2); }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
            {
                if(y>0)Tri(Point(y,x),Point(y,x+1),Point(y+1,x));
                if(y<rings-1)Tri(Point(y+1,x),Point(y,x+1),Point(y+1,x+1));
            }
        }
        internal static void Branch(List<Vector3> v,List<int> t,Vector3 from,Vector3 to,float bottom,float top)
        {
            var axis=(to-from).normalized;
            var right=Vector3.Cross(axis,Mathf.Abs(axis.y)>.95f?Vector3.forward:Vector3.up).normalized;
            var forward=Vector3.Cross(axis,right);const int sides=6;
            int start=v.Count;
            for(int i=0;i<=sides;i++)
            {
                var offset=right*Mathf.Cos(i*Mathf.PI*2/sides)+forward*Mathf.Sin(i*Mathf.PI*2/sides);
                v.Add(from+offset*bottom);v.Add(to+offset*top);
            }
            for(int i=0;i<sides;i++)
            { int a=start+i*2;t.AddRange(new[]{a,a+1,a+2,a+2,a+1,a+3}); }
        }
        internal void Build(Transform parent,string name)
        {
            var root=new GameObject(name).transform;root.SetParent(parent,false);
            var owner=root.gameObject.AddComponent<CityLandscapeResources>();
            foreach(var pair in _instances)
            {
                var target=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));target.transform.SetParent(root,false);
                target.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;
                BakeModelSink(target.transform,pair.Key,pair.Value);
            }
            foreach(var pair in _parts)
            {
                var b=pair.Value;if(b.V.Count==0)continue;
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(b.V);mesh.SetTriangles(b.T,0);
                mesh.RecalculateNormals();
                var normals=mesh.normals;var uv=new Vector2[b.V.Count];
                for(int i=0;i<uv.Length;i++)
                {
                    var n=normals[i];var p=b.V[i];
                    uv[i]=Mathf.Abs(n.y)>.6f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>.6f?new Vector2(p.z,p.y):new Vector2(p.x,p.y);
                }
                mesh.uv=uv;
                // Only normal-mapped surfaces consume tangents. Leaf, rail and flower
                // materials have no normal map and need no tangent reconstruction.
                if(pair.Key.IsKeywordEnabled("_NORMALMAP"))mesh.RecalculateTangents();
                mesh.RecalculateBounds();owner.Owned.Add(mesh);
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
        }
        internal void FlowerBed(Vector3 at,float width,float depth,int seed,bool box=true)
        {
            seed = (seed & int.MaxValue) % 1024;
            var wood=IndustrialMaterials.Surface("wood",ArtDirection.Hex("98724D"),Vector2.one);
            var dark=Mats.Lit(ArtDirection.Hex("3C643C"));var leaf=Mats.Lit(ArtDirection.Hex("799B3D"));
            var light=Mats.Lit(ArtDirection.Hex("A7B848"));
            var bloom=Mats.Lit(ArtDirection.Hex(seed%3==0?"EFC665":seed%3==1?"E4A4AB":"D8C5A2"));
            if(box)
            {
                Box(at+Vector3.up*.18f,new Vector3(width,.36f,depth),wood);
                foreach(int side in new[]{-1,1})
                    Box(at+new Vector3(0,.38f,side*depth*.48f),new Vector3(width+.06f,.09f,.09f),wood);
            }
            int count=Mathf.Clamp(Mathf.CeilToInt(width/.45f),3,40);
            for(int i=0;i<count;i++)
            {
                float x=((i+.5f)/count-.5f)*width;
                var p=at+new Vector3(x,(box?.42f:.18f)+(i%3)*.05f,Mathf.Sin(i*2.1f+seed)*depth*.19f);
                var radius=new Vector3(Mathf.Min(.42f,width/count*.8f),.30f,depth*.47f);
                Lobe(p,radius,i%3==0?dark:leaf,seed+i);
                Lobe(p+new Vector3(.06f,.18f,-.06f),radius*.65f,light,seed+i*3);
                if(i%2==0)for(int j=0;j<3;j++)
                    Lobe(p+new Vector3((j-1)*.10f,.34f,-depth*.24f),Vector3.one*(.065f+j*.012f),bloom,j);
            }
        }
        internal void Window(Vector3 at,float width,float height,Color trim,bool lit)
        {
            var stone=Mats.Lit(trim,.17f);var frame=Mats.Lit(ArtDirection.Hex("5B5148"));
            // Shadow reveal, recessed glass and projecting jambs make thickness readable at game zoom.
            Box(at+Vector3.back*.08f,new Vector3(width+.25f,height+.22f,.16f),frame);
            Box(at+Vector3.back*.17f,new Vector3(width-.12f,height-.12f,.06f),lit?IndustrialMaterials.LamplitWindow():Mats.Lit(ArtDirection.Hex("3F657A"),.48f));
            foreach(int side in new[]{-1,1})
            {
                Box(at+new Vector3(side*(width/2+.04f),0,-.24f),new Vector3(.14f,height+.35f,.33f),stone);
                Box(at+new Vector3(0,side*(height/2+.06f),-.24f),new Vector3(width+.34f,.14f,.33f),stone);
            }
            Box(at+Vector3.back*.24f,new Vector3(.07f,height,.08f),frame);
            Box(at+new Vector3(0,-height*.12f,-.24f),new Vector3(width,.07f,.08f),frame);
            Box(at+new Vector3(0,-height/2-.14f,-.29f),new Vector3(width+.48f,.13f,.55f),stone);
            if(lit)
            {
                var curtain=Mats.Lit(ArtDirection.Hex("C9AC80"));
                Box(at+new Vector3(-width*.38f,0,-.215f),new Vector3(width*.12f,height-.15f,.025f),curtain);
            }
        }
        internal void Balcony(Vector3 at,float width,int seed)
        {
            var stone=Mats.Lit(ArtDirection.Hex("D8C5A7"));var rail=Mats.Lit(ArtDirection.Hex("3E5157"),.32f);
            Box(at+new Vector3(0,0,-.38f),new Vector3(width+.3f,.16f,.96f),stone);
            Box(at+new Vector3(0,.75f,-.85f),new Vector3(width+.2f,.065f,.065f),rail);
            for(float x=-width/2;x<=width/2;x+=.32f)
                Box(at+new Vector3(x,.39f,-.85f),new Vector3(.045f,.72f,.045f),rail);
            foreach(int side in new[]{-1,1})
                Box(at+new Vector3(side*width*.5f,.75f,-.46f),new Vector3(.065f,.065f,.8f),rail);
            FlowerBed(at+new Vector3(0,.38f,-.80f),width*.82f,.34f,seed);
        }
    }
}
