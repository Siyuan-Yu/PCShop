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

        private static Mesh _catEarMesh;

        private static Mesh CatEarMesh()
        {
            if (_catEarMesh != null) return _catEarMesh;
            _catEarMesh = new Mesh { name = "CatEar" };
            _catEarMesh.vertices = new[]
            {
                new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(0, 1, .12f),
                new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(0, 1, -.12f),
            };
            _catEarMesh.triangles = new[] { 0, 1, 2, 4, 3, 5, 3, 4, 1, 3, 1, 0, 1, 4, 5, 1, 5, 2, 3, 0, 2, 3, 2, 5 };
            _catEarMesh.RecalculateNormals();
            _catEarMesh.RecalculateBounds();
            return _catEarMesh;
        }

        internal static GameObject BuildAnimal(Transform parent, bool dog, int coat, out Transform head, out Transform tail, out Transform[] legs)
        {
            var figure = new GameObject(dog?"BoardingDog":"BoardingCat");
            var root = figure.transform;
            root.SetParent(parent,false);
            Color fur = coat == 0 ? new Color(.84f,.56f,.30f) : coat == 1 ? new Color(.77f,.63f,.47f)
                : coat == 2 ? new Color(.55f,.56f,.55f) : new Color(.91f,.86f,.77f);
            Color muzzle = Color.Lerp(fur,Color.white,.45f);
            Color dark = Color.Lerp(fur,Ink,.35f);
            float height = dog ? .25f : .19f;
            float length = dog ? .50f : .43f;
            float width = dog ? .26f : .20f;
            Sphere(root,"Body",new Vector3(0,height,0),new Vector3(width,height*.95f,length),fur);
            Sphere(root,"Chest",new Vector3(0,height,.13f),new Vector3(width*.88f,height,.25f),muzzle);
            head = Joint(root, "PetHead", new Vector3(0, height + (dog ? .088f : .070f), length * .44f));
            Sphere(head,"Head",Vector3.zero,dog?new Vector3(.24f,.22f,.24f):new Vector3(.24f,.19f,.19f),fur);
            Sphere(head,"Muzzle",new Vector3(0,-.045f,dog?.113f:.085f),
                dog?new Vector3(.158f,.106f,.154f):new Vector3(.112f,.065f,.052f),muzzle);
            Sphere(head,"Nose",new Vector3(0,-.029f,dog?.179f:.112f),new Vector3(.041f,.030f,.029f),Ink);
            if (dog)
                Sphere(root,"Saddle",new Vector3(0,height+.084f,-.068f),new Vector3(width*.78f,.073f,length*.48f),dark);
            for(int side=-1;side<=1;side+=2)
            {
                Sphere(head,"Eye",new Vector3(side*.067f,.026f,dog?.099f:.074f),new Vector3(.024f,.028f,.020f),Ink);
                if (dog)
                {
                    var ear = Sphere(head,"FloppyEar",new Vector3(side*.108f,-.028f,-.021f),new Vector3(.079f,.174f,.102f),dark);
                    ear.transform.localRotation = Quaternion.Euler(13,0,side*17f);
                }
                else
                {
                    var ear = Joint(head,"Ear",new Vector3(side*.074f,.059f,-.01f));
                    ear.localRotation = Quaternion.Euler(-8,0,side*-15f);
                    AddEar(ear,"Fur",Vector3.zero,new Vector3(.101f,.115f,.071f),fur);
                    AddEar(ear,"InnerEar",new Vector3(0,.014f,.035f),new Vector3(.061f,.073f,.016f),Color.Lerp(Rose,fur,.28f));
                    for (int i=0;i<2;i++)
                    {
                        var whisker = Prims.Box(head,"Whisker",new Vector3(side*.083f,-.047f+i*.013f,.099f),
                            new Vector3(.085f,.003f,.004f),Mats.Lit(dark));
                        whisker.transform.localRotation = Quaternion.Euler(0,side*8,side*(i==0?-11:8));
                    }
                }
            }
            legs = new Transform[4];
            for(int i=0;i<legs.Length;i++)
            {
                float hip = height * .85f;
                var leg = Joint(root,"PetHip",new Vector3((i%2==0?-1:1)*width*.30f,hip,(i<2?-1:1)*length*.31f));
                legs[i] = leg;
                Sphere(leg,"Leg",new Vector3(0,-hip*.46f,0),new Vector3(dog?.084f:.065f,hip*.98f,.076f),fur);
                Sphere(leg,"Paw",new Vector3(0,-hip+.032f,.016f),new Vector3(dog?.083f:.065f,.063f,.103f),muzzle);
            }
            tail = Joint(root,"TailPivot",new Vector3(0,height+.025f,-length*.46f));
            var curl = Sphere(tail,"Tail",new Vector3(0,dog?.058f:.115f,dog?-.090f:-.078f),
                new Vector3(dog?.063f:.040f,dog?.065f:.045f,dog?.238f:.299f),dark);
            curl.transform.localRotation = Quaternion.Euler(dog ? 31 : 55,0,0);
            return figure;

            GameObject Sphere(Transform joint, string name, Vector3 position, Vector3 size, Color color)
                => Prims.Shape(PrimitiveType.Sphere,joint,name,position,size,Mats.Lit(color));
        }

        private static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent,false);
            joint.localPosition = position;
            return joint;
        }

        private static void AddEar(Transform parent, string name, Vector3 position, Vector3 size, Color color)
        {
            var ear = Joint(parent, name, position);
            ear.localScale = size;
            ear.gameObject.AddComponent<MeshFilter>().sharedMesh = CatEarMesh();
            ear.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(color);
        }
    }
}
