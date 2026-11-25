using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static class CityVegetation
    {
        public static void Build(Transform parent,Vector3 at,int variant=0,bool warmPalette=false)
        {
            int seed=variant & 0x3fff;
            var tree=QuarterModules.Tree(parent,at,.79f+seed%5*.063f,seed*43);
            float spread=.88f+seed%7*.045f;
            tree.localScale=Vector3.Scale(tree.localScale,new Vector3(spread,1.05f,spread));
            var foliage=tree.Find("Foliage");
            var pigment=ArtDirection.Hex(seed%6==0?"EAD1AA":seed%3==0?"CCD5B5":warmPalette?"ECE1B5":"E5E3CA");
            QuarterModules.Tint(foliage,pigment);
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
