using PCShopSell.Core.Catalog;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    internal static class ProductPropArt
    {
        internal static void Goods(Transform root, int kind)
        {
            switch (kind)
            {
                case 0: CigarettePack(root); break;
                case 1: Bottle(root, true); break;
                case 2: Bottle(root, false); break;
                case 3: BiscuitBox(root); break;
                case 6: Takeout(root); break;
                default: Apple(root); break;
            }
        }

        private static void CigarettePack(Transform root)
        {
            var red = PropForms.Pigment("A54438");
            var ivory = PropForms.Pigment("DFD1A7");
            PropForms.Box(root, "Pack", 0, .064f, 0, .103f, .128f, .047f, red);
            PropForms.Box(root, "IvoryPrint", 0, .047f, -.024f, .098f, .073f, .006f, ivory);
            PropForms.Box(root, "FinePrint", 0, .044f, -.029f, .042f, .005f, .002f, red);
            PropForms.Box(root, "LidSeal", 0, .116f, -.026f, .104f, .007f, .006f, PropForms.Pigment("7C302B"));
            var flap = PropForms.Box(root, "OpenLid", new Vector3(0, .145f, .017f), new Vector3(.10f, .027f, .013f), red);
            flap.localRotation = Quaternion.Euler(-23, 0, 0);
            for (int i = 0; i < 4; i++)
                PropForms.Tube(root, "Filter", new Vector3(-.032f + i * .021f, .124f, -.007f),
                    new Vector3(-.032f + i * .021f, .146f, -.007f), .013f, PropForms.Pigment("BF9364"));
        }

        private static void Bottle(Transform root, bool liquor)
        {
            float h = liquor ? .37f : .265f;
            float radius = liquor ? .048f : .046f;
            Color body = ArtDirection.Hex(liquor ? "4D5C33" : "7CABB9");
            var shape = new[] { new Vector2(0, 0), new Vector2(.85f, 0), new Vector2(1, .045f),
                new Vector2(1, .49f), new Vector2(.96f, .69f), new Vector2(.63f, .79f),
                new Vector2(.39f, .82f), new Vector2(.39f, .965f), new Vector2(0, .965f) };
            PropForms.Revolve(root, "Bottle", Mats.Lit(body, liquor ? .37f : .30f), "shelf-bottle", Vector3.zero,
                new Vector3(radius, h, radius), shape);
            PropForms.Tube(root, "Seal", Vector3.up * (.927f * h), Vector3.up * h, radius * .94f,
                PropForms.Pigment(liquor ? "AE925E" : "3D678E"));
            float y = (liquor ? .42f : .51f) * h;
            PropForms.Revolve(root, "SleeveLabel", PropForms.Pigment(liquor ? "DBC8A2" : "5C82AA"), "bottle-label",
                new Vector3(0, y, 0), new Vector3(radius + .0006f, .064f, radius + .0006f),
                new Vector2(1, -.5f), new Vector2(1, .5f));
            PropForms.Box(root, "LabelMark", 0, y, -radius - .0036f, .042f, .022f, .003f,
                PropForms.Pigment(liquor ? "8B4734" : "D7E7E6"));
            if (!liquor)
                for (int i = 0; i < 3; i++)
                    PropForms.Revolve(root, "MouldedRib", Mats.Lit(body * .93f, .29f), "bottle-label",
                        new Vector3(0, .034f + i * .017f, 0), new Vector3(radius + .0013f, .005f, radius + .0013f),
                        new Vector2(1, -.5f), new Vector2(1, .5f));
        }

        private static void BiscuitBox(Transform root)
        {
            var paper = PropForms.Pigment("D3A65D");
            var top = PropForms.Pigment("F0D6A0");
            PropForms.Box(root, "BiscuitCarton", 0, .054f, 0, .142f, .108f, .105f, paper);
            PropForms.Box(root, "FrontPrint", 0, .061f, -.054f, .132f, .073f, .004f, top);
            PropForms.Box(root, "TerraCottaBand", 0, .027f, -.057f, .131f, .013f, .003f, PropForms.Pigment("A5523A"));
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = new Vector3(-.037f + i * .029f, .066f + (i % 2) * .011f, -.057f - i * .001f);
                PropForms.Tube(root, "BiscuitPrint", at, at + Vector3.back * .003f, .028f, PropForms.Pigment("B38249"));
            }
            PropForms.Box(root, "TuckFlap", 0, .11f, .015f, .132f, .004f, .022f, top);
        }

        private static void Apple(Transform root)
        {
            PropForms.Revolve(root, "AppleSkin", Mats.LitTex(ArtDirection.Hex("AD4E36"), Vector2.one, .24f), "shelf-apple",
                Vector3.zero, new Vector3(.071f, .123f, .067f), new Vector2(0, .07f), new Vector2(.45f, 0),
                new Vector2(.78f, .15f), new Vector2(.96f, .43f), new Vector2(1, .63f), new Vector2(.86f, .85f),
                new Vector2(.57f, .94f), new Vector2(.31f, .89f), new Vector2(0, .79f));
            PropForms.Tube(root, "Stem", new Vector3(0, .103f, 0), new Vector3(.009f, .151f, .002f), .008f, PropForms.Wood("775332"));
            var leaf = Prims.Shape(PrimitiveType.Sphere, root, "Leaf", new Vector3(.026f, .129f, .002f),
                new Vector3(.062f, .010f, .029f), PropForms.Pigment("656D3D"));
            leaf.transform.localRotation = Quaternion.Euler(0, -32, 20);
        }

        private static void Takeout(Transform root)
        {
            var kraft = PropForms.Pigment("B49367");
            PropForms.Box(root, "MealBase", 0, .028f, 0, .145f, .054f, .106f, kraft);
            PropForms.Box(root, "MealRim", 0, .059f, 0, .170f, .020f, .128f, kraft);
            PropForms.Box(root, "MealLid", 0, .075f, 0, .171f, .019f, .130f, PropForms.Cream);
            PropForms.Box(root, "PaperBand", 0, .085f, 0, .049f, .005f, .13f, PropForms.Pigment("9C594B"));
            PropForms.Box(root, "TamperSeal", 0, .063f, -.064f, .048f, .044f, .005f, PropForms.Pigment("9C594B"));
            for (int i = 0; i < 2; i++)
                PropForms.Tube(root, "Chopstick", new Vector3(-.074f, .093f, .022f + i * .009f),
                    new Vector3(.073f, .093f, .033f + i * .009f), .004f, PropForms.Wood("947047"));
        }

        internal static void Package(Transform root, PartCategory category, Vector3 dim, Color color)
        {
            float front = -.5f * dim.z;
            var stock = PropForms.Pigment("C2AD87");
            var ink = PropForms.Pigment(color * .68f);
            var paper = PropForms.Pigment("DFD5BC");
            PropForms.Box(root, "Carton", new Vector3(0, .5f * dim.y, 0), dim, stock);
            PropForms.Box(root, "PrintedFace", 0, .52f * dim.y, front - .0015f, .94f * dim.x, .86f * dim.y, .003f, ink);
            PropForms.Box(root, "GraphicPaper", -.07f * dim.x, .57f * dim.y, front - .004f, .69f * dim.x, .55f * dim.y, .002f, paper);
            PropForms.Box(root, "ColorSpine", .497f * dim.x, .5f * dim.y, 0, .003f, .79f * dim.y, .84f * dim.z, ink);
            PropForms.Box(root, "TuckFlap", 0, dim.y + .0007f, .071f * dim.z, .91f * dim.x, .002f, .064f * dim.z, paper);
            var print = PropForms.Group(root, "PrintedComponent", new Vector3(-.07f * dim.x, .57f * dim.y, front - .007f));
            float w = .49f * dim.x, h = .40f * dim.y;
            var chassis = PropForms.Pigment("443D38");
            switch (category)
            {
                case PartCategory.Gpu:
                    PropForms.Box(print, "CardSilhouette", 0, 0, 0, w, .65f * h, .002f, chassis);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var a = new Vector3(i * .24f * w, 0, -.002f);
                        PropForms.Tube(print, "FanPrint", a, a + Vector3.back * .002f, .54f * h, ink);
                    }
                    break;
                case PartCategory.Cpu:
                    PropForms.Box(print, "ProcessorPrint", 0, 0, 0, .70f * h, .70f * h, .003f, chassis);
                    PropForms.Box(print, "HeatSpreaderPrint", 0, 0, -.002f, .46f * h, .46f * h, .003f, ink);
                    break;
                case PartCategory.Memory:
                    for (int i = -1; i <= 1; i += 2)
                        PropForms.Box(print, "MemoryPrint", 0, i * .21f * h, 0, w, .26f * h, .002f, ink);
                    break;
                default:
                    PropForms.Box(print, "HardwarePrint", 0, 0, 0, w * .86f, h * .82f, .003f, chassis);
                    PropForms.Box(print, "InsetPrint", -.06f * w, .05f * h, -.002f, .57f * w, .43f * h, .003f, ink);
                    break;
            }
        }
    }
}
