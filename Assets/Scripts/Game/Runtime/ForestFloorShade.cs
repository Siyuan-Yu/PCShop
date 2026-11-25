using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    // Low-frequency occlusion survives the overview camera's shadow-distance cutoff.
    internal static class ForestFloorShade
    {
        private static Material material,foundation;
        private static Mesh footprint;
        private static Material Shade(bool square)
        {
            if(material==null)material=new Material(Resources.Load<Shader>("OldTown/CanopyShade")){name="林底漫射遮蔽"};
            if(!square)return material;
            if(foundation==null){foundation=new Material(material){name="建筑基脚遮蔽"};foundation.SetFloat("_Squared",1);}
            return foundation;
        }
        internal static void Footprint(Transform parent,Vector3 center,float w,float d,bool square=false)
        {
            if(footprint==null)
            {
                footprint=new Mesh{name="地面接触阴影"};
                footprint.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
                footprint.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
                footprint.triangles=new[]{0,1,2,0,2,3};footprint.RecalculateBounds();
            }
            var go=new GameObject("接地阴翳",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);go.transform.localPosition=center;go.transform.localScale=new Vector3(w,1,d);
            go.GetComponent<MeshFilter>().sharedMesh=footprint;var renderer=go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=Shade(square);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        internal static void Build(Transform parent,BakedCityMap.ForestChunk chunk,CityLandscapeResources owner)
        {
            var positions=new List<Vector3>();
            var uv=new List<Vector2>();
            var indices=new List<int>();
            foreach(var part in chunk.Parts)
            {
                // Mountain meshes do not follow a horizontal footprint.
                if(part.Position.y>.04f)continue;
                float radius=part.Mesh.bounds.size.y*.50f;
                Vector3 center=part.Position+new Vector3(.10f*radius,.035f,.09f*radius);
                int a=positions.Count;
                positions.Add(center+new Vector3(-radius,0,-radius));
                positions.Add(center+new Vector3(-radius,0,radius));
                positions.Add(center+new Vector3(radius,0,radius));
                positions.Add(center+new Vector3(radius,0,-radius));
                uv.Add(Vector2.zero);uv.Add(Vector2.up);uv.Add(Vector2.one);uv.Add(Vector2.right);
                indices.AddRange(new[]{a,a+1,a+2,a,a+2,a+3});
            }
            if(positions.Count==0)return;
            var mesh=new Mesh{name="林缘阴翳",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(positions);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=Shade(false);
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;owner.Owned.Add(mesh);
        }
    }
}
