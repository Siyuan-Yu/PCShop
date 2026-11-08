using PCShopSell.Core.Home;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    internal static class PetSceneModels
    {
        private static readonly Color Wood = new Color(.72f,.53f,.35f);
        private static readonly Color Moss = new Color(.46f,.56f,.40f);
        private static readonly Color Linen = new Color(.83f,.68f,.48f);
        private static readonly Color Rose = new Color(.86f,.40f,.31f);
        private static readonly Color Ink = new Color(.17f,.15f,.14f);

        internal static void BuildFurniture(Transform root, FurnitureDef def, bool collider)
        {
            int count = PetShopCatalog.HouseCapacity(def.Id);
            if (count == 0)
            {
                Prims.Box(root,"ToyMat",new Vector3(0,.025f,0),new Vector3(.36f,.05f,.34f),Mats.Lit(Moss),collider);
                var toy = BuildToy(root, def.Id == PetShopCatalog.BallFixture);
                toy.transform.localPosition = new Vector3(0,.09f,0);
                return;
            }
            float w = (float)def.Width, d = (float)def.Depth;
            Prims.Box(root,"CareMat",new Vector3(0,.026f,0),new Vector3(w,.05f,d),Mats.Lit(Moss),collider);
            var frame = Mats.Lit(Wood);
            Prims.Box(root,"BackRail",new Vector3(0,.26f,d*.5f-.06f),new Vector3(w,.48f,.11f),frame,collider);
            for (int side = -1; side <= 1; side += 2)
            {
                Prims.Box(root,"SideRail",new Vector3(side*(w*.5f-.06f),.26f,0),new Vector3(.11f,.48f,d),frame,collider);
                Prims.Box(root,"FrontRail",new Vector3(side*(w+.86f)*.25f,.21f,-d*.5f+.06f),
                    new Vector3((w-.86f)*.5f,.38f,.11f),frame,collider);
            }
            int cols = count == 4 ? 4 : count / 2;
            for (int i = 0; i < count; i++)
            {
                float x = ((i%cols+.5f)/cols-.5f)*(w-.32f);
                float z = d*.5f-.40f-(i/cols)*.52f;
                Prims.Box(root,"BedFrame",new Vector3(x,.10f,z),new Vector3(.53f,.12f,.46f),frame);
                Prims.Box(root,"BedCushion",new Vector3(x,.17f,z),new Vector3(.46f,.06f,.38f),Mats.Lit(i%2==0?Linen:Rose));
            }
            Bowl("CatFood",-w*.35f,new Color(.66f,.47f,.72f),new Color(.48f,.28f,.16f));
            Bowl("DogFood",-w*.35f+.35f,Rose,new Color(.50f,.31f,.19f));
            Bowl("Water",w*.35f-.47f,new Color(.26f,.55f,.65f),new Color(.48f,.81f,.91f));
            var litter = Prims.Box(root,"Litter",new Vector3(w*.35f,.09f,-d*.31f),
                new Vector3(.44f,.10f,.34f),Mats.Lit(new Color(.40f,.39f,.46f)));
            Prims.Box(litter.transform,"Fill",new Vector3(0,.48f,0),new Vector3(.82f,.10f,.78f),Mats.Lit(Linen));
            Prims.Box(litter.transform,"Used",new Vector3(.10f,.5f,-.10f),new Vector3(.25f,.14f,.30f),Mats.Lit(new Color(.50f,.38f,.30f)));

            void Bowl(string name,float x,Color rim,Color contents)
            {
                var bowl = Prims.Shape(PrimitiveType.Cylinder,root,name,new Vector3(x,.081f,-d*.32f),
                    new Vector3(.29f,.044f,.29f),Mats.Lit(rim));
                Prims.Shape(PrimitiveType.Cylinder,bowl.transform,"Fill",new Vector3(0,.91f,0),
                    new Vector3(.69f,.12f,.69f),Mats.Lit(contents));
            }
        }

        internal static GameObject BuildToy(Transform root, bool ball)
        {
            if (ball) return Prims.Shape(PrimitiveType.Sphere,root,"SoftBall",Vector3.zero,Vector3.one*.14f,Mats.Lit(Rose));
            var wand = new GameObject("Wand");
            wand.transform.SetParent(root,false);
            Prims.Box(wand.transform,"Handle",new Vector3(0,0,.14f),new Vector3(.03f,.025f,.30f),Mats.Lit(Linen));
            Prims.Box(wand.transform,"Cord",new Vector3(0,-.055f,.29f),new Vector3(.012f,.13f,.012f),Mats.Lit(Ink));
            Prims.Shape(PrimitiveType.Sphere,wand.transform,"Feather",new Vector3(0,-.1f,.29f),
                new Vector3(.10f,.045f,.09f),Mats.Lit(Rose));
            return wand;
        }

        internal static GameObject BuildAnimal(Transform parent, bool dog, int coat, out Transform tail, out Transform[] feet)
        {
            var figure = new GameObject(dog?"BoardingDog":"BoardingCat");
            var root = figure.transform;
            root.SetParent(parent,false);
            Color fur = coat == 0 ? new Color(.84f,.56f,.30f) : coat == 1 ? new Color(.77f,.63f,.47f)
                : coat == 2 ? new Color(.55f,.56f,.55f) : new Color(.91f,.86f,.77f);
            Color muzzle = Color.Lerp(fur,Color.white,.45f);
            Color dark = Color.Lerp(fur,Ink,.35f);
            float height = dog ? .25f : .19f;
            float length = dog ? .51f : .39f;
            float width = dog ? .27f : .21f;
            Sphere("Body",new Vector3(0,height,0),new Vector3(width,height*.92f,length),fur);
            Sphere("Chest",new Vector3(0,height,.12f),new Vector3(width*.82f,height*.92f,.22f),muzzle);
            Sphere("Head",new Vector3(0,height+.10f,length*.46f),new Vector3(width*.86f,.21f,.20f),fur);
            Sphere("Muzzle",new Vector3(0,height+.055f,length*.66f),new Vector3(.13f,.075f,.11f),muzzle);
            Sphere("Nose",new Vector3(0,height+.074f,length*.75f),new Vector3(.046f,.033f,.031f),Ink);
            if (dog)
                Sphere("Saddle",new Vector3(0,height+.085f,-.065f),new Vector3(width*.78f,.07f,length*.48f),dark);
            for(int side=-1;side<=1;side+=2)
            {
                Sphere("Eye",new Vector3(side*width*.29f,height+.13f,length*.627f),new Vector3(.026f,.028f,.023f),Ink);
                var ear = Sphere("Ear",new Vector3(side*width*.36f,height+(dog?.115f:.214f),length*.43f),
                    dog?new Vector3(.066f,.15f,.08f):new Vector3(.072f,.116f,.056f),dark);
                ear.transform.localRotation = Quaternion.Euler(0,0,side*(dog?22f:-19f));
                if (!dog)
                {
                    var inner = Sphere("InnerEar",new Vector3(side*width*.36f,height+.217f,length*.48f),
                        new Vector3(.042f,.076f,.026f),Color.Lerp(Rose,fur,.4f));
                    inner.transform.localRotation = ear.transform.localRotation;
                    for (int i=0;i<2;i++)
                    {
                        var whisker = Prims.Box(root,"Whisker",new Vector3(side*.084f,height+.052f+i*.018f,length*.72f),
                            new Vector3(.082f,.006f,.006f),Mats.Lit(muzzle));
                        whisker.transform.localRotation = Quaternion.Euler(0,side*9,side*(i==0?-12:7));
                    }
                }
            }
            feet = new Transform[4];
            for(int i=0;i<feet.Length;i++)
                feet[i]=Sphere("Paw",new Vector3((i%2==0?-1:1)*width*.31f,height*.43f,(i<2?-1:1)*length*.28f),
                    new Vector3(.075f,height*.92f,.083f),dark).transform;
            tail = new GameObject("TailPivot").transform;
            tail.SetParent(root,false);
            tail.localPosition = new Vector3(0,height+.04f,-length*.46f);
            var curl = Prims.Shape(PrimitiveType.Sphere,tail,"Tail",new Vector3(0,.06f,-.09f),
                new Vector3(.055f,.065f,dog?.24f:.29f),Mats.Lit(dark));
            curl.transform.localRotation = Quaternion.Euler(31,0,0);
            return figure;

            GameObject Sphere(string name, Vector3 position, Vector3 size, Color color)
                => Prims.Shape(PrimitiveType.Sphere,root,name,position,size,Mats.Lit(color));
        }
    }
}
