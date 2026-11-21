using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private const float StarterResidenceHeight = 3.5f;
        private static readonly Color[] OldResidenceWalls =
        {
            ArtDirection.Hex("B6AA98"), ArtDirection.Hex("ABA99D"), ArtDirection.Hex("B8AA9B"),
            ArtDirection.Hex("A3AAA6"), ArtDirection.Hex("BCB4A3")
        };

        private static void BuildOldResidence(Transform root, Place place, float width, float depth, float height,
            int seed, bool starter)
        {
            if (starter) height = StarterResidenceHeight;
            uint mixed = unchecked((uint)seed * 2654435761u);
            seed = (int)((mixed ^ (mixed >> 16)) & int.MaxValue);
            Color wall = starter ? ArtDirection.Hex("9BAAB0") : OldResidenceWalls[seed % OldResidenceWalls.Length];
            bool blueRoof = !starter && (seed >> 5) % 3 == 0;
            Color roof = ArtDirection.Hex(blueRoof ? "4B607E" : starter ? "A8634A" : "AB6748");
            Color trim = ArtDirection.Hex(blueRoof ? "414E61" : "725446");
            var shell = new Vector3(0, height / 2, depth / 2);
            var body = StarterPart(root, "Old residence plaster walls", shell, new Vector3(width, height, depth),
                wall, "old_plaster", solid: true);
            if (place != null) body.AddComponent<PlaceMarker>().PlaceId = place.Id;
            StarterPart(root, "Dark concrete plinth", new Vector3(0, .35f, depth / 2), new Vector3(width + .04f, .70f, depth + .04f),
                Color.Lerp(wall, ArtDirection.Hex("505A5D"), starter ? .68f : .47f), "old_plaster");
            StarterRoof(root, width, depth, height, starter ? 1.4f : Mathf.Min(1.65f, width * .16f),
                roof, wall, trim, oldResidence: true, repairSeed: starter || (seed >> 8) % 4 == 0 ? seed : -1);

            var details = new MiniatureDetailMesh();
            var concrete = Mats.Lit(ArtDirection.Hex("A59F92"));
            var frame = Mats.Lit(ArtDirection.Hex("545B5D"));
            var darkGlass = Mats.Lit(ArtDirection.Hex("3E515D"), .30f);
            var wood = IndustrialMaterials.Surface("wood", ArtDirection.Hex("69513E"), Vector2.one);

            void Window(Vector3 at, float w, float h, float yaw, bool lit)
            {
                var turn = Quaternion.Euler(0, yaw, 0);
                Vector3 Face(float y, float z) => at + turn * new Vector3(0, y, z);
                details.Box(Face(0, -.04f), new Vector3(w, h, .09f), frame, turn);
                details.Box(Face(0, -.095f), new Vector3(w - .13f, h - .12f, .035f),
                    lit ? IndustrialMaterials.LamplitWindow() : darkGlass, turn);
                details.Box(Face(0, -.12f), new Vector3(.065f, h, .045f), frame, turn);
                details.Box(Face(-h / 2 - .035f, -.12f), new Vector3(w + .16f, .095f, .24f), concrete, turn);
            }

            details.Box(new Vector3(0, 1.46f, -.075f), new Vector3(1.45f, 2.30f, .12f), wood);
            details.Box(new Vector3(.49f, 1.33f, -.15f), new Vector3(.07f, .12f, .07f), frame);
            details.Box(new Vector3(0, .08f, -.43f), new Vector3(1.90f, .16f, .84f), concrete);
            details.Box(new Vector3(0, .16f, -.23f), new Vector3(1.72f, .32f, .47f), concrete);

            float frontWindowWidth = Mathf.Min(1.85f, width * .20f);
            Window(new Vector3(width * .24f, 1.67f, 0), frontWindowWidth, 1.28f, 0, true);
            if (!starter) Window(new Vector3(-width * .24f, 1.67f, 0), frontWindowWidth, 1.28f, 0, (seed & 1) != 0);

            int facade = (seed >> 10) % 3;
            if (starter || facade == 0)
                details.Box(new Vector3(0, 2.72f, -.16f), new Vector3(1.74f, .13f, .36f), concrete);
            else if (facade == 1)
            {
                float canopyWidth = Mathf.Min(2.45f, width * .28f);
                var metal = IndustrialMaterials.Surface("metal", ArtDirection.Hex("778891"), Vector2.one);
                details.Box(new Vector3(0, 2.75f, -.53f), new Vector3(canopyWidth, .12f, 1.04f), metal, Quaternion.Euler(-9, 0, 0));
            }

            if (!starter)
            {
                float upper = height - 1.40f;
                Window(new Vector3(-width * .24f, upper, 0), frontWindowWidth, 1.35f, 0, true);
                float balconyX = width * .24f;
                Window(new Vector3(balconyX, upper, 0), frontWindowWidth, facade == 2 ? 1.85f : 1.35f, 0, false);
                if (facade == 2)
                {
                    float floor = upper - .96f;
                    details.Box(new Vector3(balconyX, floor, -.40f), new Vector3(2.55f, .14f, .82f), concrete);
                    details.Box(new Vector3(balconyX, floor + .86f, -.78f), new Vector3(2.46f, .065f, .065f), frame);
                    for (int i = -4; i <= 4; i++)
                        details.Box(new Vector3(balconyX + i * .29f, floor + .44f, -.78f), new Vector3(.048f, .83f, .05f), frame);
                    foreach (int side in new[] { -1, 1 })
                        details.Box(new Vector3(balconyX + side * 1.17f, floor + .86f, -.42f), new Vector3(.065f, .065f, .73f), frame);
                    if ((seed & 4) != 0)
                    {
                        details.Box(new Vector3(balconyX - .63f, floor + .61f, -.81f), new Vector3(.40f, .56f, .053f),
                            Mats.Lit(ArtDirection.Hex("B4AAA0")), Quaternion.Euler(0, 0, -4));
                        details.Box(new Vector3(balconyX + .03f, floor + .69f, -.81f), new Vector3(.43f, .38f, .05f),
                            Mats.Lit(ArtDirection.Hex("828D9B")));
                    }
                }
                Window(new Vector3(-width * .23f, upper, depth), frontWindowWidth, 1.21f, 180, false);
                Window(new Vector3(width * .23f, upper, depth), frontWindowWidth, 1.21f, 180, true);
            }

            foreach (int side in new[] { -1, 1 })
            {
                Window(new Vector3(side * width / 2, starter ? 1.64f : height - 1.52f, depth * .30f),
                    frontWindowWidth * .82f, 1.16f, -side * 90, side > 0);
                if (!starter)
                    Window(new Vector3(side * width / 2, height - 1.52f, depth * .70f),
                        frontWindowWidth * .82f, 1.16f, -side * 90, side < 0);
            }

            if (starter || (seed >> 3) % 4 == 0)
            {
                var repair = IndustrialMaterials.Surface("old_plaster", Color.Lerp(wall, ArtDirection.Hex("8B8175"), .57f), Vector2.one);
                float side = starter || (seed & 1) == 0 ? 1 : -1;
                float x = side * (width / 2 + .012f), z = depth * .77f;
                details.Box(new Vector3(x, .53f, z), new Vector3(.038f, .78f, 1.98f), repair);
                details.Box(new Vector3(x, 1.00f, z + .14f), new Vector3(.038f, .16f, 1.48f), repair);
                details.Box(new Vector3(x, 1.14f, z + .31f), new Vector3(.038f, .12f, .86f), repair);
            }
            details.Build(root, "Old residence doors, sills and railings");
        }
    }
}
