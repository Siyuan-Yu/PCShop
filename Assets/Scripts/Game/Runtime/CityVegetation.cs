using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static class CityVegetation
    {
        private static Mesh _crown;
        private static Mesh _cone;
        private static Mesh Crown
        {
            get
            {
                if(_crown!=null)return _crown;
                _crown=RoundedCrown();return _crown;
            }
        }
        public static void Build(Transform parent,Vector3 at,int variant=0)
        {
            int seed=variant&int.MaxValue;float scale=.9f+(seed%5)*.08f;
            Prims.Box(parent,"TreeTrunk",at+Vector3.up*1.25f*scale,new Vector3(.24f,2.5f,.24f)*scale,Mats.Lit(ArtDirection.Hex("9A7446")));
            Color leaf=seed%3==0?ArtDirection.Hex("79A843"):seed%3==1?ArtDirection.Hex("98B951"):ArtDirection.Hex("55904D");
            if(seed%4==0)
            {
                if(_cone==null)_cone=ConiferCrown();
                for(int i=0;i<3;i++)CrownPart(parent,_cone,at+new Vector3(0,1.5f+i*.65f,0)*scale,new Vector3(1.1f-i*.23f,1.55f,1.1f-i*.23f)*scale,Color.Lerp(leaf,ArtDirection.Hex("B3C75F"),i*.10f));
            }
            else
            {
                CrownPart(parent,Crown,at+new Vector3(-.35f,2.55f,0)*scale,new Vector3(1.22f,1.4f,1.22f)*scale,leaf);
                CrownPart(parent,Crown,at+new Vector3(.48f,3.1f,.1f)*scale,new Vector3(.95f,1.3f,1.0f)*scale,Color.Lerp(leaf,ArtDirection.Hex("B9CD68"),.20f));
                CrownPart(parent,Crown,at+new Vector3(.5f,2.25f,-.35f)*scale,new Vector3(.85f,.85f,.9f)*scale,leaf);
            }
            Prims.Shape(PrimitiveType.Cylinder,parent,"TreeBed",at+new Vector3(0,.055f,0),new Vector3(1.35f,.055f,1.35f),Mats.Lit(ArtDirection.Hex("8FA459")));
        }
        private static void CrownPart(Transform parent,Mesh mesh,Vector3 at,Vector3 scale,Color color)
        {
            var go=new GameObject("SculptedCanopy",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=at;go.transform.localScale=scale;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=Mats.Lit(color,.12f);
        }
        private static Mesh RoundedCrown()
        {
            const int rings=6,sides=10;var vertices=new Vector3[(rings+1)*(sides+1)];var indices=new int[rings*sides*6];int t=0;
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
            {
                float phi=Mathf.PI*y/rings,theta=2*Mathf.PI*x/sides;
                vertices[y*(sides+1)+x]=new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta));
            }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
            {
                int a=y*(sides+1)+x,b=a+sides+1;
                indices[t++]=a;indices[t++]=a+1;indices[t++]=b;indices[t++]=b;indices[t++]=a+1;indices[t++]=b+1;
            }
            var mesh=new Mesh{name="RoundedMiniatureCanopy"};mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh ConiferCrown()
        {
            const int sides=10;var vertices=new Vector3[sides*3];var indices=new int[vertices.Length];
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                vertices[i*3]=new Vector3(0,1,0);vertices[i*3+1]=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));vertices[i*3+2]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            }
            for(int i=0;i<indices.Length;i++)indices[i]=i;
            var mesh=new Mesh{name="LayeredConiferCanopy"};mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
