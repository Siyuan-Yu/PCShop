using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    internal enum PlantSpecies { Broadleaf, GoldenBush, Hedge, Spirea, Reeds, Grass, Cabbage, BeanTrellis, Hydrangea, Cattail, Boxwood, SilverGrass }

    // Leaf sprays and rounded shrubs share one atlas and one mesh per garden or shore sector.
    internal sealed class StreetPlanting
    {
        private readonly List<Vector3> _positions = new();
        private readonly List<Vector2> _uv = new();
        private readonly List<Color32> _shading = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<int> _indices = new();
        private static readonly Material[] Materials = new Material[2];
        private readonly bool _maintained;

        internal StreetPlanting(bool maintained=false) => _maintained=maintained;

        internal void Add(PlantSpecies species, Vector3 foot, float width, float height, int seed)
        {
            int index=(int)species;
            bool floweringHead=_maintained && species==PlantSpecies.Hydrangea;
            // Draw each flower head on its own volume; keep the underlying mound and sprays leafy.
            if(floweringHead) index=(int)PlantSpecies.Broadleaf;
            float u=(index%4)*.25f, v=(3-index/4)*.25f;
            bool mounded=_maintained && species is PlantSpecies.Hydrangea or PlantSpecies.Boxwood or
                PlantSpecies.Hedge or PlantSpecies.Spirea or PlantSpecies.Broadleaf or PlantSpecies.GoldenBush;
            if(mounded) AddShrubBody(foot,width,height,seed,u,v);
            if(floweringHead)
                for(int cluster=0;cluster<3;cluster++)
                {
                    float angle=seed*.17f+cluster*2.39f;
                    var center=foot+new Vector3(Mathf.Cos(angle)*width*.29f,height*(.76f+cluster*.074f),Mathf.Sin(angle)*width*.29f);
                    AddHydrangeaHead(center,width*(.15f+.016f*(cluster%2)),angle);
                }
            for(int plane=0;plane<3;plane++)
            {
                float angle=(seed*19+plane*60)*Mathf.Deg2Rad;
                var right=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var normal=new Vector3(-right.z,.75f,right.x).normalized;
                var basePoint=mounded ? foot+right*width*.18f+Vector3.up*height*.15f : foot;
                float sprayWidth=width*(mounded?.62f:1f);
                var tip=basePoint+Vector3.up*height*(mounded?.78f:1f)+right*(Mathf.Sin(seed+plane)*height*.055f);
                int start=_positions.Count;
                for(int row=0;row<3;row++)
                {
                    float t=row*.5f;
                    var middle=Vector3.Lerp(basePoint,tip,t)+new Vector3(normal.x,0,normal.z)*(row==1?width*.095f:0);
                    foreach(int side in new[]{-1,1})
                    {
                        _positions.Add(middle+right*side*(sprayWidth*.5f));
                        _uv.Add(new Vector2(u+(side<0?.002f:.248f),v+.001f+t*.248f));
                        _normals.Add((Vector3.up*.9f+right*(side*.25f)).normalized);
                        byte occlusion=(byte)(178+row*37);
                        _shading.Add(new Color32(occlusion,occlusion,occlusion,255));
                    }
                }
                for(int row=0;row<2;row++){int a=start+row*2;_indices.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
            }
        }

        // A convex leaf mass keeps short flowers and clipped hedges readable from above;
        // three smaller sprays break the outer contour while the top receives real sunlight.
        private void AddShrubBody(Vector3 foot,float width,float height,int seed,float u,float v)
        {
            const int sides=9;
            int start=_positions.Count;
            float turn=(seed%360)*Mathf.Deg2Rad;
            for(int band=0;band<3;band++)
            {
                float radius=band==0?.35f:band==1?.47f:.29f;
                float y=band==0?.025f:band==1?.45f:.81f;
                for(int i=0;i<sides;i++)
                {
                    float a=turn+i*(2*Mathf.PI/sides);
                    var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    float variant=1+.065f*Mathf.Sin(i*2.18f+seed*.23f);
                    _positions.Add(foot+radial*(width*radius*variant)+Vector3.up*(height*y*variant));
                    _normals.Add((radial+Vector3.up*(band==2?1.6f:.62f)).normalized);
                    float sampleX=.5f+.30f*Mathf.Cos(a*2);
                    _uv.Add(new Vector2(u+sampleX*.25f,v+(.18f+y*.70f)*.25f));
                    byte light=(byte)(169+band*36);
                    _shading.Add(new Color32(light,light,light,255));
                    if(band==2) continue;
                    int a0=start+band*sides+i,b0=start+band*sides+(i+1)%sides;
                    _indices.AddRange(new[]{a0,a0+sides,b0,b0,a0+sides,b0+sides});
                }
            }
            int cap=_positions.Count;
            _positions.Add(foot+Vector3.up*height*.94f);_normals.Add(Vector3.up);
            _uv.Add(new Vector2(u+.13f,v+.20f));_shading.Add(new Color32(249,249,249,255));
            for(int i=0;i<sides;i++) _indices.AddRange(new[]{cap,start+2*sides+(i+1)%sides,start+2*sides+i});
        }

        private void AddHydrangeaHead(Vector3 center,float radius,float twist)
        {
            const int sides=8;
            int first=_positions.Count;
            // Project the actual florets from the upper mophead of the hydrangea atlas cell.
            var flower=new Vector2(.13f,.451f);
            for(int ring=0;ring<2;ring++)
            for(int i=0;i<sides;i++)
            {
                float a=i*(2*Mathf.PI/sides)+twist;
                var side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                float spread=ring==0?.88f:.77f;
                _positions.Add(center+(side*spread+Vector3.up*(ring==0?-.30f:.53f))*radius);
                _normals.Add((side+Vector3.up*(ring==0?.08f:1.6f)).normalized);
                _uv.Add(flower+new Vector2(side.x*.023f,side.z*.014f)*spread);
                byte shade=(byte)(ring==0?183:240);_shading.Add(new Color32(shade,shade,shade,255));
                if(ring!=0)continue;
                int lo=first+i,next=first+(i+1)%sides;
                _indices.AddRange(new[]{lo,lo+sides,next,next,lo+sides,next+sides});
            }
            int cap=_positions.Count;
            _positions.Add(center+Vector3.up*radius*.78f);_normals.Add(Vector3.up);
            _uv.Add(flower);_shading.Add(new Color32(255,255,255,255));
            for(int i=0;i<sides;i++)_indices.AddRange(new[]{cap,first+sides+(i+1)%sides,first+sides+i});
        }

        internal void Build(Transform parent,string name)
        {
            if(_indices.Count==0)return;
            int palette=_maintained?1:0;
            var material=Materials[palette];
            if(material==null)
            {
                var source=Resources.Load<GameObject>("OldTown/StreetTree").transform.Find("Foliage").GetComponent<MeshRenderer>().sharedMaterial;
                material=new Material(source){name="手绘灌木、芦苇与菜畦",enableInstancing=true};
                material.SetTexture("_BaseMap",Resources.Load<Texture2D>(_maintained?"OldTown/Surfaces/summerBorders":"OldTown/Surfaces/gardenPlants"));
                material.SetFloat("_Cull",0);material.SetFloat("_Cutoff",.37f);
                material.SetColor("_Pigment",Color.white);
                Materials[palette]=material;
            }
            var mesh=new Mesh{name=name,indexFormat=_positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(_positions);mesh.SetNormals(_normals);mesh.SetColors(_shading);mesh.SetUVs(0,_uv);
            mesh.SetTriangles(_indices,0);mesh.RecalculateBounds();
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(CityLandscapeResources));
            go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;
            go.GetComponent<CityLandscapeResources>().Owned.Add(mesh);
        }
    }
}
