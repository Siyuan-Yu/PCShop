using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    internal static partial class ShopFixtures
    {
        private static Material FixtureWood(int district)
        {
            switch (district)
            {
                case 1: return PropForms.Wood("AC815B");
                case 2: return PropForms.Wood("9D7452");
                case 3: return PropForms.Wood("705344");
                default: return PropForms.Wood("927353");
            }
        }

        private static Material FixtureEdge(int district)
        {
            switch (district)
            {
                case 1: return PropForms.Pigment("C9C5B8");
                case 2: return PropForms.Pigment("474749");
                case 3: return PropForms.Pigment("726F68");
                default: return PropForms.Pigment("5F6956");
            }
        }

        private static Material FixtureTop(int district)
        {
            if (district >= 2)
                return IndustrialMaterials.Surface("stone", ArtDirection.Hex(district == 3 ? "DFD0B9" : "D7C9AF"), new Vector2(1.8f, 1));
            return PropForms.Pigment(district == 1 ? "E4D8BC" : "B59A70");
        }

        private static Material FixtureLight() =>
            Mats.Glow(ArtDirection.Hex("D8AD73"), new Color(.75f, .43f, .21f), false);

        private static void BuildShelfFrame(Transform root, int district)
        {
            Material wood = FixtureWood(district);
            Material edge = district == 0 ? PropForms.Wood("765239") : FixtureEdge(district);
            if (district == 3) wood = PropForms.Pigment("C4C0B4");
            if (district == 0 || district == 3)
                PropForms.Box(root, "ShelfBack", 0, .93f, .173f, 1.36f, 1.78f, .024f, wood);
            foreach (float x in new[] { -.676f, .676f })
            {
                if (district == 2)
                {
                    foreach (float z in new[] { -.156f, .158f })
                        PropForms.Box(root, "ShelfPost", x, .923f, z, .047f, 1.846f, .045f, edge);
                    PropForms.Box(root, "SideCrossbar", x, 1.816f, 0, .046f, .045f, .36f, edge);
                }
                else
                    PropForms.Box(root, "CabinetShelfSide", x, .923f, 0, .047f, 1.846f, .375f, edge);
            }
            foreach (float y in BoardCenters)
            {
                PropForms.Box(root, "ShelfBoard", 0, y, 0, 1.31f, .04f, .36f, wood);
                PropForms.Box(root, "ShelfEdge", 0, y, -.18f, 1.31f, .03f, .016f, district == 3 ? wood : edge);
                if (district == 3 && y > .1f)
                    PropForms.Box(root, "ShelfWarmLight", 0, y - .023f, -.147f, 1.25f, .014f, .015f, FixtureLight());
            }
            PropForms.Box(root, "ShelfPlinth", 0, .044f, .008f, 1.31f, .085f, .29f, edge);
            if (district == 0)
                PropForms.Box(root, "RubbedPlinth", -.24f, .051f, -.14f, .48f, .014f, .006f, PropForms.Pigment("78806B"));
        }

        private static void BuildCounter(Transform root, float w, float d, int district)
        {
            Material wood = FixtureWood(district);
            Material edge = FixtureEdge(district);
            Material body = district switch
            {
                1 => PropForms.Pigment("74736C"), 2 => wood, 3 => PropForms.Pigment("BAB8AE"), _ => edge
            };
            PropForms.Box(root, "CounterCarcass", 0, .477f, 0, w - .035f, .86f, d - .045f, body);
            PropForms.Box(root, "CounterTop", 0, .96f, 0, w + .02f, .075f, d + .02f, district == 2 ? PropForms.Wood("B08B65") : FixtureTop(district));
            PropForms.Box(root, "CounterPlinth", 0, .057f, 0, w - .112f, .114f, d - .10f, PropForms.Ink);
            if (district == 0)
            {
                Material raised = PropForms.Pigment("727B65");
                for (float x = -.5f * w + .082f; x < .5f * w - .045f; x += .073f)
                    PropForms.Box(root, "PaintedFlute", x, .477f, -.5f * d + .014f, .048f, .683f, .021f, raised);
                PropForms.Box(root, "BrassPlate", .16f*w, .615f, -.5f*d - .006f, .269f, .063f, .023f, PropForms.Pigment("AE8850"));
            }
            else
            {
                Material inset = district == 2 ? wood : body;
                PropForms.Box(root, "CounterFront", 0, .49f, -.5f * d + .012f, w - .17f, .658f, .025f, inset);
                if (district >= 2)
                    PropForms.Box(root, "CounterWarmLight", 0, .901f, -.5f * d + .009f, w - .108f, .018f, .016f, FixtureLight());
            }
            PropForms.Box(root, "RegisterBase", .16f, 1.009f, .05f, .34f, .026f, .21f, PropForms.Ink);
            PropForms.Box(root, "MonitorNeck", .16f, 1.105f, .085f, .08f, .18f, .07f, PropForms.Ink);
            PropForms.Box(root, "Monitor", .16f, 1.248f, .05f, .51f, .325f, .069f, PropForms.Ink);
            PropForms.Box(root, "TerminalScreen", .16f, 1.253f, .09f, .456f, .277f, .01f, Mats.Lit(ArtDirection.Hex("3E6266"), .24f));
            PropForms.Box(root, "ReceiptPrinter", -.33f, 1.045f, -.02f, .22f, .105f, .19f, edge);
            PropForms.Box(root, "ReceiptSlot", -.33f, 1.066f, -.117f, .132f, .016f, .01f, PropForms.Ink);
            PropForms.Box(root, "TerminalKeypad", .43f, 1.02f, -.17f, .128f, .053f, .16f, PropForms.Ink);
        }

        private static void BuildWorkBench(Transform root, FixtureHandle handle, float w, float d, int district)
        {
            Material frame = PropForms.Pigment("505752");
            foreach (float x in new[] { -w / 2 + .079f, w / 2 - .079f })
                foreach (float z in new[] { -d / 2 + .061f, d / 2 - .061f })
                    PropForms.Box(root, "BenchLeg", x, .437f, z, .072f, .874f, .064f, frame);
            PropForms.Box(root, "RearStretcher", 0, .387f, d / 2 - .067f, w - .12f, .1f, .044f, frame);
            PropForms.Box(root, "BenchTop", 0, .901f, 0, w, .057f, d, FixtureWood(district));
            PropForms.Box(root, "ToolDrawer", -.31f * w, .773f, 0, .34f * w, .19f, d - .12f, FixtureEdge(district));
            PropForms.Box(root, "ToolDrawerPull", -.31f * w, .781f, -.5f * d + .045f, .14f, .022f, .033f, frame);
            PropForms.Box(root, "RepairMat", 0, .937f, -.09f, .61f * w, .014f, .53f * d, PropForms.Pigment("628277"));
            PropForms.Box(root, "BenchCase", -.31f * w, 1.145f, .065f, .31f, .43f, .31f, frame);
            PropForms.Box(root, "BenchRearRack", 0, 1.24f, .45f * d, w - .12f, .35f, .045f, frame);
            for (int i = 0; i < 5; i++)
                PropForms.Box(root, "BenchVent", -.31f * w, 1.01f + i * .029f, -.093f, .23f, .013f, .008f, PropForms.Ink);
            PropForms.Box(root, "BenchScreenHousing", .26f * w, 1.21f, .126f, .46f, .355f, .047f, PropForms.Ink);
            handle.Glow.Add(PropForms.Box(root, "BenchScreen", .26f * w, 1.213f, .099f, .41f, .309f, .01f,
                Mats.Glow(new Color(.09f, .14f, .14f), new Color(.20f, .86f, .55f), false)));
            PropForms.Box(root, "DriverTray", .19f * w, .94f, -.15f, .25f, .024f, .12f, frame);
        }
    }
}
