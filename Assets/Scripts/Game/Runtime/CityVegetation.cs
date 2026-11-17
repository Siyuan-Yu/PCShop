using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static class CityVegetation
    {
        private static readonly Dictionary<int,Mesh[]> Trees=new Dictionary<int,Mesh[]>();
        public static void Build(Transform parent,Vector3 at,int variant=0,bool warmPalette=false)
        {
            int seed=(variant&int.MaxValue)%12;
            bool blossom=seed==3||seed==9;
            if(!Trees.TryGetValue(seed,out var meshes)||meshes[0]==null)
            {
                var v=new[]{new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
                var t=new[]{new List<int>(),new List<int>(),new List<int>()};
                AppendTree(v[0],t[0],v[1],t[1],v[2],t[2],Vector3.zero,4.2f,seed==8,seed);
                meshes=new Mesh[3];
                for(int i=0;i<3;i++)
                { var m=new Mesh{name="Miniature tree "+seed+"/"+i};m.SetVertices(v[i]);m.SetTriangles(t[i],0);m.RecalculateNormals();if(i<2)SoftenLeafNormals(m);m.RecalculateBounds();meshes[i]=m; }
                Trees[seed]=meshes;
            }
            Color bottom=ArtDirection.Hex(blossom?"B97891":seed%3==0?"537C34":"608535");
            Color top=ArtDirection.Hex(blossom?"E9ADC2":warmPalette?"B4BC43":"A5B742");
            var materials=new[]{Mats.Lit(bottom,.04f),Mats.Lit(top,.04f),Mats.Lit(ArtDirection.Hex("866342"),.06f)};
            var root=new GameObject("Branched garden tree").transform;root.SetParent(parent,false);root.localPosition=at;
            root.localScale=Vector3.one*(.92f+seed%5*.06f);
            root.localRotation=Quaternion.Euler(0,seed*53,0);
            for(int i=0;i<3;i++)
            {
                var go=new GameObject(i==2?"Branch structure":"Layered leaf clusters",typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=meshes[i];go.GetComponent<Renderer>().sharedMaterial=materials[i];
            }
        }
        private static void SoftenLeafNormals(Mesh mesh)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;
            var sums=new Dictionary<Vector3Int,Vector3>();
            for(int i=0;i<vertices.Length;i++)
            { var key=Vector3Int.RoundToInt(vertices[i]*10000);sums.TryGetValue(key,out var sum);sums[key]=sum+normals[i]; }
            for(int i=0;i<vertices.Length;i++)
                normals[i]=Vector3.Lerp(normals[i],sums[Vector3Int.RoundToInt(vertices[i]*10000)].normalized,.56f).normalized;
            mesh.normals=normals;
        }
        // Nearby garden trees and spatially batched woodland share the same silhouette.
        internal static void AppendTree(List<Vector3> v,List<int> t,List<Vector3> tips,List<int> tipIndices,
            List<Vector3> trunks,List<int> trunkIndices,Vector3 p,float height,bool conifer,int seed,bool woodland=false)
        {
            seed = (seed & int.MaxValue) % 1024;
            Vector3 bend=p+new Vector3(.035f*height,height*.35f,0);
            MiniatureDetailMesh.Branch(trunks,trunkIndices,p,bend,height*.047f,height*.028f);
            MiniatureDetailMesh.Branch(trunks,trunkIndices,bend,p+new Vector3(-.025f*height,height*.81f,.02f*height),height*.028f,height*.009f);
            for(int j=0;j<3;j++)
            {
                float a=j*2.1f+seed;
                MiniatureDetailMesh.Branch(trunks,trunkIndices,p+new Vector3(Mathf.Cos(a)*height*.075f,.02f,Mathf.Sin(a)*height*.075f),
                    p+Vector3.up*height*.13f,height*.018f,height*.035f);
            }
            int lobes=conifer?17:21;
            for(int j=0;j<lobes;j++)
            {
                int group=j/3;
                float a=group*2.39996f+seed*.61f;
                float level=group%3*.12f;
                Vector3 center;
                float size;
                if(conifer)
                {
                    float t0=j/(float)(lobes-1);
                    center=p+height*new Vector3(Mathf.Cos(j*2.4f)*(.26f*(1-t0)+.02f),.44f+t0*.44f,Mathf.Sin(j*2.4f)*(.26f*(1-t0)+.02f));
                    size=height*(.10f+(1-t0)*.06f);
                }
                else
                {
                    float radial=group==6?.05f:.24f;
                    center=p+height*new Vector3(Mathf.Cos(a)*radial,.54f+level,Mathf.Sin(a)*radial);
                    float da=a+(j%3)*2.1f;
                    center+=height*new Vector3(Mathf.Cos(da)*.085f,(j%3)*.027f,Mathf.Sin(da)*.085f);
                    if(group==6)center.y=p.y+height*.84f;
                    size=height*(.14f+(j%3)*.018f);
                }
                var radius=new Vector3(size,size*.88f,size*.94f);
                bool lit=(group%3!=0&&j%3!=0)||group==6;
                MiniatureDetailMesh.Lobe(lit?tips:v,lit?tipIndices:t,center,radius,j+seed,woodland);
                if(j%3==0)
                    MiniatureDetailMesh.Branch(trunks,trunkIndices,p+Vector3.up*height*(.32f+level*.6f),center,
                        height*.020f,height*.006f);
            }

        }
    }
}
