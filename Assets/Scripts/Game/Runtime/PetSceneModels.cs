using PCShopSell.Core.Home;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    internal static partial class PetSceneModels
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
                Prims.Box(root, "ToyMat", new Vector3(0, .025f, 0), new Vector3(.36f, .05f, .34f), PropForms.Pigment(Moss), collider);
                bool ball = PetShopCatalog.IsDogToy(def.Id);
                int tier = PetShopCatalog.ToyTier(def.Id);
                if (ball)
                {
                    BuildToy(root, true, tier).transform.localPosition = Vector3.up * .117f;
                }
                else
                {
                    var standWood = PropForms.Wood("886B50");
                    PropForms.Box(root, "WandStandFoot", .031f, .059f, .012f, .19f, .033f, .18f, standWood);
                    PropForms.Tube(root, "WandStand", new Vector3(.036f, .072f, -.063f),
                        new Vector3(.036f, .572f, -.063f), .027f, standWood);
                    var wand = BuildToy(root, false, tier).transform;
                    wand.localPosition = new Vector3(.036f, .515f, -.131f);
                    wand.localRotation = Quaternion.Euler(13, -18, 0);
                }
                return;
            }
            float w = (float)def.Width, d = (float)def.Depth;
            Prims.Box(root,"CareMat",new Vector3(0,.026f,0),new Vector3(w,.05f,d),Mats.Lit(Moss),collider);
            var frame = PropForms.Wood(count == 4 ? "A17A51" : count == 8 ? "917355" : "B4946B");
            Rail("BackRail", new Vector3(0, .26f, d * .5f - .06f), new Vector3(w, .48f, .11f), false);
            for (int side = -1; side <= 1; side += 2)
            {
                Rail("SideRail", new Vector3(side * (w * .5f - .06f), .26f, 0), new Vector3(.11f, .48f, d), true);
                Rail("FrontRail", new Vector3(side * (w + .86f) * .25f, .21f, -d * .5f + .06f),
                    new Vector3((w - .86f) * .5f, .38f, .11f), false);
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

            void Rail(string name, Vector3 center, Vector3 size, bool alongZ)
            {
                var rail = PropForms.Group(root, name, center);
                if (collider) rail.gameObject.AddComponent<BoxCollider>().size = size;
                Vector3 axis = alongZ ? Vector3.forward : Vector3.right;
                float length = alongZ ? size.z : size.x;
                var slat = size;
                slat.y = .10f;
                foreach (int level in new[] {-1, 1})
                    PropForms.Box(rail, "WoodSlat", Vector3.up * (.105f * level), slat, frame);
                int posts = Mathf.Max(2, Mathf.CeilToInt(length / 1.25f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    Vector3 at = axis * ((i / (posts - 1f) - .5f) * (length - .085f));
                    PropForms.Box(rail, "JoineryPost", at, new Vector3(.078f, size.y, .078f), frame);
                    PropForms.Box(rail, "PostCap", at + Vector3.up * (.5f * size.y), new Vector3(.094f,.029f,.094f), frame);
                }
            }

            void Bowl(string name,float x,Color rim,Color contents)
            {
                var bowl = Prims.Shape(PrimitiveType.Cylinder,root,name,new Vector3(x,.081f,-d*.32f),
                    new Vector3(.29f,.044f,.29f),Mats.Lit(rim));
                Prims.Shape(PrimitiveType.Cylinder,bowl.transform,"Fill",new Vector3(0,.91f,0),
                    new Vector3(.69f,.12f,.69f),Mats.Lit(contents));
            }
        }

        internal static GameObject BuildToy(Transform root, bool ball, int tier)
        {
            Color color = tier == 2 ? new Color(.66f,.42f,.81f) : tier == 1 ? new Color(.23f,.65f,.78f) : Rose;
            if (ball) return Prims.Shape(PrimitiveType.Sphere,root,"SoftBall",Vector3.zero,Vector3.one*.14f,Mats.Lit(color));
            var wand = new GameObject("Wand");
            wand.transform.SetParent(root,false);
            Prims.Box(wand.transform,"Handle",new Vector3(0,0,.14f),new Vector3(.03f,.025f,.30f),Mats.Lit(Linen));
            Prims.Box(wand.transform,"Cord",new Vector3(0,-.055f,.29f),new Vector3(.012f,.13f,.012f),Mats.Lit(Ink));
            Prims.Shape(PrimitiveType.Sphere,wand.transform,"Feather",new Vector3(0,-.1f,.29f),
                new Vector3(.10f,.045f,.09f),Mats.Lit(color));
            return wand;
        }

    }
}
