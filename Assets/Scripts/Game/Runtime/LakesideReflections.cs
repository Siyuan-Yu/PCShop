using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    // Mirrored silhouettes retain offscreen trees and bridge railings. The lake writes the stencil
    // only on visible water, so the reflection never crosses a street, an island or a foreground object.
    internal sealed class LakesideReflections
    {
        private readonly Transform _root;
        private readonly Matrix4x4 _mirror;
        private readonly bool _parkPond;
        private readonly Dictionary<(Vector2Int,Material),List<CombineInstance>> _parts=new();
        private static readonly Dictionary<(Material,bool),Material> Inks=new();

        internal LakesideReflections(Transform root,float level,bool parkPond=false)
        {
            _root=root;
            _parkPond=parkPond;
            _mirror=Matrix4x4.TRS(Vector3.up*(2*level),Quaternion.identity,new Vector3(1,-1,1))*root.worldToLocalMatrix;
        }

        internal void Add(MeshRenderer renderer)
        {
            var filter=renderer.GetComponent<MeshFilter>();
            if(filter==null || filter.sharedMesh==null || !filter.sharedMesh.isReadable || !renderer.sharedMaterial)return;
            var at=_root.InverseTransformPoint(renderer.bounds.center);
            var key=(new Vector2Int(Mathf.FloorToInt(at.x/80),Mathf.FloorToInt(at.z/80)),renderer.sharedMaterial);
            if(!_parts.TryGetValue(key,out var group)) _parts.Add(key,group=new List<CombineInstance>());
            group.Add(new CombineInstance{mesh=filter.sharedMesh,transform=_mirror*renderer.localToWorldMatrix});
        }
        internal void Build()
        {
            var owner=_root.GetComponent<CityLandscapeResources>()??_root.gameObject.AddComponent<CityLandscapeResources>();
            foreach(var entry in _parts)
            {
                var source=entry.Key.Item2;
                var key=(source,_parkPond);
                if(!Inks.TryGetValue(key,out var ink) || ink==null)
                {
                    ink=new Material(Resources.Load<Shader>("OldTown/ShoreReflection")){name="岸边倒影 "+source.name};
                    if(source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap")!=null)
                        ink.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));
                    ink.SetColor("_Pigment",source.HasProperty("_Pigment")?source.GetColor("_Pigment"):
                        source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white);
                    ink.SetFloat("_Cutoff",source.HasProperty("_Cutoff")?source.GetFloat("_Cutoff"):.01f);
                    ink.SetFloat("_LayeredCrown",source.shader.name=="PCShop/Layered town canopy"?1:0);
                    ink.SetFloat("_ParkPond",_parkPond?1:0);
                    Inks[key]=ink;
                }
                var mesh=new Mesh{name="岸边倒影",indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(entry.Value.ToArray(),true,true,false);owner.Owned.Add(mesh);
                var go=new GameObject("岸边倒影",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(_root,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=ink;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
        }
    }
}
