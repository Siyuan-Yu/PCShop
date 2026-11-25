using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    internal enum PlantSpecies { Broadleaf, GoldenBush, Hedge, Spirea, Reeds, Grass, Cabbage, BeanTrellis, Hydrangea, Cattail, Boxwood, SilverGrass }

    // Each cluster consists of three intersecting, gently bent leaf sprays. One mesh per garden or shore sector.
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
            float u=(index%4)*.25f, v=(3-index/4)*.25f;
            for(int plane=0;plane<3;plane++)
            {
                float angle=(seed*19+plane*60)*Mathf.Deg2Rad;
                var right=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var normal=new Vector3(-right.z,.75f,right.x).normalized;
                var tip=foot+Vector3.up*height+right*(Mathf.Sin(seed+plane)*height*.055f);
                int start=_positions.Count;
                for(int row=0;row<3;row++)
                {
                    float t=row*.5f;
                    var middle=Vector3.Lerp(foot,tip,t)+new Vector3(normal.x,0,normal.z)*(row==1?width*.095f:0);
                    foreach(int side in new[]{-1,1})
                    {
                        _positions.Add(middle+right*side*(width*.5f));
                        _uv.Add(new Vector2(u+(side<0?.002f:.248f),v+.001f+t*.248f));
                        _normals.Add((Vector3.up*.9f+right*(side*.25f)).normalized);
                        byte occlusion=(byte)(178+row*37);
                        _shading.Add(new Color32(occlusion,occlusion,occlusion,255));
                    }
                }
                for(int row=0;row<2;row++){int a=start+row*2;_indices.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
            }
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
