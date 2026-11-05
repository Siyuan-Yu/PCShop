using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed class ExpoProductMarker:MonoBehaviour {public string ProductId;}
    public static class ExpoProductVisual
    {
        // Use the same miniature materials and geometry as the city. Product identity comes
        // from the reserved inventory, never from a separately authored exhibition catalogue.
        public static GameObject Build(Transform parent,string id)
        {
            var root=new GameObject("ExpoProduct_"+id);root.transform.SetParent(parent,false);root.AddComponent<ExpoProductMarker>().ProductId=id;
            var dark=Mats.Lit(ArtDirection.Ink,.4f);var board=Mats.Lit(ArtDirection.Sage);var gold=Mats.Lit(ArtDirection.Brass);
            void Box(string name,Vector3 at,Vector3 size,Material mat)=>Prims.Box(root.transform,name,at,size,mat);
            if(PartCatalog.Exists(id))
            {
                var category=PartCatalog.Get(id).Category;
                if(category==PartCategory.PowerCase)
                {
                    Box("Case",new Vector3(0,.68f,0),new Vector3(.65f,1.36f,.8f),dark);
                    Box("GlassPanel",new Vector3(0,.68f,-.41f),new Vector3(.5f,1.1f,.03f),Mats.Lit(ArtDirection.Blue));
                    for(int n=0;n<3;n++)Prims.Shape(PrimitiveType.Cylinder,root.transform,"CaseFan",new Vector3(0,.35f+n*.3f,-.44f),new Vector3(.22f,.025f,.22f),gold).transform.localRotation=Quaternion.Euler(90,0,0);
                }
                else if(category==PartCategory.Gpu)
                {
                    Box("GraphicsCard",new Vector3(0,.15f,0),new Vector3(1.3f,.22f,.62f),dark);
                    for(int n=-1;n<=1;n++)Prims.Shape(PrimitiveType.Cylinder,root.transform,"GpuFan",new Vector3(n*.39f,.29f,0),new Vector3(.32f,.035f,.32f),gold);
                    Box("Connector",new Vector3(0,.10f,.34f),new Vector3(.75f,.08f,.08f),gold);
                }
                else
                {
                    Vector3 size=category==PartCategory.Motherboard?new Vector3(1.1f,.08f,1.2f):category==PartCategory.Memory?new Vector3(1.1f,.10f,.22f):new Vector3(.65f,.12f,.65f);
                    Box("CircuitBoard",Vector3.up*.09f,size,board);
                    Box("Processor",new Vector3(0,.2f,0),new Vector3(size.x*.48f,.15f,size.z*.48f),dark);
                    for(int n=-2;n<=2;n++)Box("GoldContacts",new Vector3(n*size.x/6,.09f,-size.z/2),new Vector3(.06f,.06f,.1f),gold);
                }
            }
            else if(ClothingProducts.IsClothing(id))
            {
                var fabric=Mats.Lit(CrowdView.PersonClothing(id));
                Box("MannequinFoot",Vector3.up*.05f,new Vector3(.75f,.1f,.5f),dark);
                Box("MannequinStand",Vector3.up*.6f,new Vector3(.07f,1.2f,.07f),gold);
                Box("Garment",Vector3.up*1.13f,new Vector3(.65f,.85f,.22f),fabric);
                Box("Sleeves",Vector3.up*1.42f,new Vector3(1.05f,.27f,.25f),fabric);
                Prims.Shape(PrimitiveType.Sphere,root.transform,"MannequinHead",Vector3.up*1.8f,Vector3.one*.28f,Mats.Lit(ArtDirection.Cream));
            }
            else
            {
                Prims.Shape(PrimitiveType.Cylinder,root.transform,"ModelPlinth",Vector3.up*.06f,new Vector3(.9f,.06f,.9f),gold);
                if(id.Contains("rover")||id.Contains("tram"))
                {var car=Prims.ParkedCar(root.transform,new Vector3(0,.13f,0),ArtDirection.Blue);car.transform.localScale=Vector3.one*.32f;}
                else if(id.Contains("tower")||id.Contains("greenhouse"))
                {for(int n=0;n<3;n++)Box("ModelArchitecture",new Vector3(0,.23f+n*.25f,0),new Vector3(.7f-n*.12f,.22f,.65f-n*.1f),n%2==0?board:gold);}
                else{var figure=Prims.Figure(root.transform,"CollectibleFigure",CrowdView.PersonClothing(id),ArtDirection.Cream,.6f);figure.transform.localPosition=Vector3.up*.13f;figure.GetComponent<FigureMotion>().LocomotionEnabled=false;}
            }
            return root;
        }
    }
}
