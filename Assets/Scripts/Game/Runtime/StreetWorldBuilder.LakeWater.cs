using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private void BuildLakeWater(Transform root,List<Vector3> shore,Material material)
        {
            // Distances travel with the shoreline mesh at every zoom, including a baked map.
            var center=new Vector3(-25,LakeWaterLevel,90);
            var vertices=new List<Vector3>();
            var distances=new List<Vector2>();
            var triangles=new List<int>();
            float[] rings={0,1.6f,5,13,32};
            for(int ring=0;ring<rings.Length;ring++)
            {
                for(int j=0;j<shore.Count;j++)
                {
                    var rim=new Vector3(shore[j].x,LakeWaterLevel,shore[j].z);
                    var inward=(center-rim).normalized;
                    float irregular=(Mathf.PerlinNoise(rim.x*.09f,rim.z*.09f)-.5f)*1.1f;
                    vertices.Add(rim+inward*(irregular+rings[ring]));
                    distances.Add(new Vector2(rings[ring]*LakeScale,0));
                    if(ring==0) continue;
                    int a=(ring-1)*shore.Count+j,b=(ring-1)*shore.Count+(j+1)%shore.Count;
                    triangles.AddRange(new[]{a,a+shore.Count,b,b,a+shore.Count,b+shore.Count});
                }
            }
            int end=vertices.Count;vertices.Add(center);distances.Add(new Vector2(300,0));
            int start=(rings.Length-1)*shore.Count;
            for(int j=0;j<shore.Count;j++)triangles.AddRange(new[]{end,start+(j+1)%shore.Count,start+j});
            var lake=LandscapeMesh(root,"CentralLake",vertices,triangles,material);
            lake.GetComponent<MeshFilter>().sharedMesh.SetUVs(1,distances);
            lake.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
