using UnityEngine;

namespace PCShopSell.Game.Procedural
{
    public sealed partial class ProceduralCityBuilder
    {
        private const float FloorH = 3.2f;
        private const float ColStep = 2.6f;

        // ------------------------------------------------------------------ 普通建筑

        private void BuildLotsAndBuildings()
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    for (int xi = 0; xi < 4; xi++)
                    {
                        for (int zi = 0; zi < 4; zi++)
                        {
                            // 给"电脑城"让位：(-,+) 象限最靠近路口的一排两格
                            if (sx == -1 && sz == 1 && zi == 0 && (xi == 0 || xi == 1)) continue;

                            float cx = sx * (BlockStart + CellSize / 2 + xi * CellSize);
                            float cz = sz * (BlockStart + CellSize / 2 + zi * CellSize);
                            float w = Rand(9f, 12f);
                            float d = Rand(9f, 12f);

                            // 越远离相机越高，形成层层升高的天际线
                            float far01 = Mathf.Clamp01((cx + cz + 45f) / 95f);
                            float h = 5f + far01 * 13f + Rand(0f, 5f);
                            Building(cx, cz, w, d, h, WallPalette[_rng.Next(WallPalette.Length)], true, true);
                        }
                    }
                }
            }
        }

        private void Building(float cx, float cz, float w, float d, float h, Color wall, bool groundShop, bool drawShop)
        {
            bool tiered = h > 12f && _rng.NextDouble() < 0.55;
            float h1 = tiered ? Mathf.Max(2f, Mathf.Round(h * 0.62f / FloorH)) * FloorH : h;
            bool balconies = _rng.NextDouble() < 0.35;

            Tower(cx, cz, w, d, 0f, h1, wall, groundShop, balconies, !tiered);
            if (tiered)
            {
                float h2 = h1 + Mathf.Max(2f, Mathf.Round((h - h1) / FloorH)) * FloorH;
                Tower(cx + w * 0.1f, cz + d * 0.1f, w * 0.74f, d * 0.74f, h1, h2, Tint(wall, 1.08f), false, false, true);
            }

            if (drawShop) ShopFront(cx, cz - d / 2f, w, false);
        }

        private void Tower(float cx, float cz, float w, float d, float y0, float y1, Color wall,
            bool groundShop, bool balconies, bool roofExtras)
        {
            float h = y1 - y0;
            float front = cz - d / 2f;
            float left = cx - w / 2f;

            var wallMat = LitTex(wall, _noiseTex, new Vector2(2f, 2f), 0.2f);
            Cube("Body", new Vector3(cx, (y0 + y1) / 2f, cz), new Vector3(w, h, d), wallMat, true);

            // 底座（兼作"接地阴影"）
            if (y0 < 0.01f)
                Cube("Plinth", new Vector3(cx, 0.5f, cz), new Vector3(w + 0.3f, 1f, d + 0.3f),
                    Lit(Tint(wall, 0.55f), 0.15f), true);

            // 楼层腰线
            var bandMat = Lit(Tint(wall, 0.86f), 0.2f);
            for (float y = y0 + FloorH; y < y1 - 0.6f; y += FloorH)
                Cube("Band", new Vector3(cx, y, cz), new Vector3(w + 0.14f, 0.16f, d + 0.14f), bandMat, false);

            // 转角立柱
            if (_rng.NextDouble() < 0.45)
            {
                Cube("Pillar", new Vector3(left, (y0 + y1) / 2f, front), new Vector3(0.7f, h, 0.7f),
                    Lit(Tint(wall, 0.78f), 0.2f), true);
            }

            // 檐口与屋顶
            Cube("Cornice", new Vector3(cx, y1 - 0.15f, cz), new Vector3(w + 0.5f, 0.35f, d + 0.5f),
                Lit(Tint(wall, 1.14f), 0.2f), true);
            Cube("Roof", new Vector3(cx, y1 + 0.2f, cz), new Vector3(w + 0.1f, 0.4f, d + 0.1f),
                Lit(new Color(0.18f, 0.19f, 0.22f), 0.2f), true);

            // 窗户（只做相机可见的 -Z 面和 -X 面）
            int colsZ = Mathf.Max(1, Mathf.FloorToInt((w - 1.2f) / ColStep));
            int colsX = Mathf.Max(1, Mathf.FloorToInt((d - 1.2f) / ColStep));
            bool sills = _rng.NextDouble() < 0.5;
            float firstY = y0 + ((groundShop && y0 < 0.01f) ? FloorH + 1.7f : 1.7f);
            int floorIndex = 0;

            for (float wy = firstY; wy + 1.3f < y1 - 0.3f; wy += FloorH, floorIndex++)
            {
                for (int c = 0; c < colsZ; c++)
                {
                    float x = cx - (colsZ - 1) * ColStep / 2f + c * ColStep;
                    Window(new Vector3(x, wy, front), Vector3.back, sills);
                }

                for (int c = 0; c < colsX; c++)
                {
                    float z = cz - (colsX - 1) * ColStep / 2f + c * ColStep;
                    Window(new Vector3(left, wy, z), Vector3.left, sills);
                }

                // 阳台（隔层出现）
                if (balconies && floorIndex % 2 == 1)
                {
                    float bw = colsZ * ColStep - 0.4f;
                    Cube("Balcony", new Vector3(cx, wy - 1.05f, front - 0.55f), new Vector3(bw, 0.2f, 1.1f),
                        Lit(Tint(wall, 0.9f), 0.2f), true);
                    Cube("Railing", new Vector3(cx, wy - 0.55f, front - 1.08f), new Vector3(bw, 0.9f, 0.06f),
                        Lit(new Color(0.12f, 0.12f, 0.14f), 0.4f, 0.3f), false);
                    if (_rng.NextDouble() < 0.7)
                        Cube("BalconyAC", new Vector3(cx + Rand(-bw / 2.4f, bw / 2.4f), wy - 0.55f, front - 0.5f),
                            new Vector3(0.9f, 0.6f, 0.5f), Lit(new Color(0.75f, 0.77f, 0.80f), 0.4f), false);
                }
            }

            if (roofExtras) RoofExtras(cx, cz, w, d, y1 + 0.4f);
        }

        private void Window(Vector3 wallPos, Vector3 normal, bool sill)
        {
            // 深色窗框
            var frame = Quad("WinFrame", wallPos + normal * 0.012f, normal, new Vector3(1.8f, 2.25f, 1f),
                Lit(new Color(0.09f, 0.09f, 0.11f), 0.3f));
            _ = frame;

            Material mat;
            if (_rng.NextDouble() < _look.WindowLitChance)
            {
                Color c = _rng.NextDouble() < 0.7
                    ? new Color(1.0f, 0.82f, 0.50f)
                    : new Color(0.65f, 0.85f, 1.0f);
                mat = Emissive(c, _look.EmissiveIntensity * Rand(0.6f, 1.2f));
            }
            else
            {
                mat = Lit(_look.UnlitGlassColor, 0.85f);
            }

            Quad("Window", wallPos + normal * 0.026f, normal, new Vector3(1.5f, 1.9f, 1f), mat);

            if (sill)
            {
                var sillMat = Lit(new Color(0.78f, 0.76f, 0.72f), 0.2f);
                if (normal.z < -0.5f)
                    Cube("Sill", wallPos + new Vector3(0, -1.08f, -0.15f), new Vector3(1.95f, 0.12f, 0.3f), sillMat, false);
                else
                    Cube("Sill", wallPos + new Vector3(-0.15f, -1.08f, 0), new Vector3(0.3f, 0.12f, 1.95f), sillMat, false);
            }
        }

        // ------------------------------------------------------------------ 店面

        private void ShopFront(float cx, float frontZ, float w, bool hero)
        {
            float shopW = w * (hero ? 0.82f : 0.7f);
            Color neon = NeonPalette[_rng.Next(NeonPalette.Length)];
            Color neon2 = NeonPalette[_rng.Next(NeonPalette.Length)];
            Color awning = AwningPalette[_rng.Next(AwningPalette.Length)];
            float glow = _look.EmissiveIntensity * (hero ? 0.95f : 0.6f);
            var warm = new Color(1.0f, 0.85f, 0.6f);

            // 橱窗
            Quad("ShopGlass", new Vector3(cx, 1.6f, frontZ - 0.03f), Vector3.back,
                new Vector3(shopW, 2.4f, 1f), Emissive(warm, glow));

            // 橱窗里的货架/商品剪影
            var silhouette = Lit(new Color(0.05f, 0.05f, 0.07f), 0.2f);
            int items = hero ? 9 : 6;
            for (int i = 0; i < items; i++)
            {
                float iw = Rand(0.5f, 1.4f);
                float ih = Rand(0.4f, 1.5f);
                float x = cx - shopW / 2f + (i + 0.5f) * shopW / items + Rand(-0.2f, 0.2f);
                Cube("Item", new Vector3(x, 0.35f + ih / 2f, frontZ - 0.1f), new Vector3(iw * 0.8f, ih, 0.1f), silhouette, false);
            }

            // 门
            float doorX = cx + shopW * (hero ? 0.28f : 0.32f);
            Cube("DoorFrame", new Vector3(doorX, 1.45f, frontZ - 0.1f), new Vector3(1.7f, 2.6f, 0.14f),
                Lit(new Color(0.08f, 0.08f, 0.10f), 0.3f), false);
            Quad("DoorGlass", new Vector3(doorX, 1.4f, frontZ - 0.19f), Vector3.back,
                new Vector3(1.4f, 2.3f, 1f), Emissive(new Color(1f, 0.95f, 0.8f), glow * 1.3f));

            // 条纹遮阳篷
            float awW = shopW + 0.6f;
            int stripes = 8;
            var white = Lit(new Color(0.92f, 0.92f, 0.90f), 0.3f);
            var colored = Lit(awning, 0.3f);
            for (int i = 0; i < stripes; i++)
            {
                float sw = awW / stripes;
                float x = cx - awW / 2f + (i + 0.5f) * sw;
                RotCube("Awning", new Vector3(x, 3.1f, frontZ - 0.8f), new Vector3(sw, 0.12f, 1.7f),
                    new Vector3(12f, 0f, 0f), i % 2 == 0 ? colored : white, true);
            }

            Cube("Valance", new Vector3(cx, 2.82f, frontZ - 1.6f), new Vector3(awW, 0.32f, 0.08f), colored, false);

            // 招牌
            Cube("SignBack", new Vector3(cx, 3.95f, frontZ - 0.05f), new Vector3(shopW * 0.86f, 0.9f, 0.16f),
                Lit(new Color(0.07f, 0.07f, 0.09f), 0.3f), false);
            Cube("Sign", new Vector3(cx, 3.95f, frontZ - 0.16f), new Vector3(shopW * 0.78f, 0.62f, 0.1f),
                Emissive(neon, _look.EmissiveIntensity * 1.1f), false);

            // 垂直挑牌（从侧面看得到）
            if (hero || _rng.NextDouble() < 0.4)
            {
                Cube("BladeSign", new Vector3(cx - shopW / 2f - 0.2f, 5.4f, frontZ - 0.8f), new Vector3(0.16f, 2.2f, 1.1f),
                    Emissive(neon2, _look.EmissiveIntensity * 1.2f), false);
                Cube("BladeArm", new Vector3(cx - shopW / 2f - 0.2f, 6.55f, frontZ - 0.35f), new Vector3(0.1f, 0.1f, 0.9f),
                    Lit(new Color(0.15f, 0.15f, 0.17f), 0.4f, 0.4f), false);
            }

            // 门口花盆
            var pot = Lit(new Color(0.55f, 0.35f, 0.25f), 0.2f);
            var leaf = Lit(new Color(0.22f, 0.55f, 0.28f), 0.15f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float px = cx + sx * (shopW / 2f + 0.5f);
                Cylinder("Pot", new Vector3(px, 0.62f, frontZ - 1.1f), new Vector3(0.7f, 0.32f, 0.7f), pot);
                Sphere("Plant", new Vector3(px, 1.35f, frontZ - 1.1f), new Vector3(1.0f, 0.9f, 1.0f), leaf);
            }

            // 立式小黑板
            if (_rng.NextDouble() < 0.55)
            {
                float bx = cx - shopW * 0.15f;
                var board = Emissive(new Color(1f, 0.9f, 0.6f), _look.EmissiveIntensity * 0.5f);
                RotCube("SandwichA", new Vector3(bx, 0.95f, frontZ - 2.5f), new Vector3(0.8f, 1.2f, 0.06f), new Vector3(-12f, 0f, 0f), board, false);
                RotCube("SandwichB", new Vector3(bx, 0.95f, frontZ - 2.9f), new Vector3(0.8f, 1.2f, 0.06f), new Vector3(12f, 0f, 0f),
                    Lit(new Color(0.18f, 0.14f, 0.1f), 0.2f), false);
            }

            // 门口摆摊式陈列（电脑城的感觉）
            if (hero || _rng.NextDouble() < 0.35)
            {
                float sx0 = cx - shopW * 0.25f;
                Cube("DisplayTable", new Vector3(sx0, 0.85f, frontZ - 2.3f), new Vector3(shopW * 0.34f, 0.12f, 0.9f),
                    Lit(new Color(0.25f, 0.2f, 0.16f), 0.2f), true);
                Cube("DisplayLegL", new Vector3(sx0 - shopW * 0.15f, 0.45f, frontZ - 2.3f), new Vector3(0.1f, 0.8f, 0.7f),
                    Lit(new Color(0.15f, 0.15f, 0.17f), 0.4f), false);
                Cube("DisplayLegR", new Vector3(sx0 + shopW * 0.15f, 0.45f, frontZ - 2.3f), new Vector3(0.1f, 0.8f, 0.7f),
                    Lit(new Color(0.15f, 0.15f, 0.17f), 0.4f), false);
                int screens = hero ? 5 : 3;
                for (int i = 0; i < screens; i++)
                {
                    float sx1 = sx0 - shopW * 0.14f + i * (shopW * 0.28f / Mathf.Max(1, screens - 1));
                    Color c = NeonPalette[_rng.Next(NeonPalette.Length)];
                    Cube("Monitor", new Vector3(sx1, 1.25f, frontZ - 2.3f), new Vector3(0.62f, 0.4f, 0.08f),
                        Lit(new Color(0.06f, 0.06f, 0.08f), 0.4f), false);
                    var scr = Quad("MonitorScreen", new Vector3(sx1, 1.25f, frontZ - 2.35f), new Vector3(-0.5f, 0.1f, -1f),
                        new Vector3(0.55f, 0.34f, 1f), Emissive(c, _look.EmissiveIntensity * 0.9f));
                    _ = scr;
                }
            }

            // 店门前补光
            if (_look.LampsOn && _shopLights < 9)
            {
                var l = new GameObject("ShopLight");
                l.transform.SetParent(_root, false);
                l.transform.position = new Vector3(cx, 2.4f, frontZ - 2.2f);
                var light = l.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.82f, 0.55f);
                light.range = 9f;
                light.intensity = _look.LampIntensity * 0.9f;
                light.shadows = LightShadows.None;
                _shopLights++;
            }
        }

        // ------------------------------------------------------------------ 屋顶

        private void RoofExtras(float cx, float cz, float w, float d, float y)
        {
            var metal = Lit(new Color(0.55f, 0.57f, 0.60f), 0.5f);
            var dark = Lit(new Color(0.14f, 0.15f, 0.17f), 0.4f, 0.4f);

            // 空调外机
            int units = _rng.Next(1, 4);
            for (int i = 0; i < units; i++)
            {
                float ox = Rand(-0.5f, 0.5f) * (w - 3f);
                float oz = Rand(-0.5f, 0.5f) * (d - 3f);
                Cube("AC", new Vector3(cx + ox, y + 0.5f, cz + oz), new Vector3(1.6f, 1f, 1.2f), metal, true);
                Cylinder("ACFan", new Vector3(cx + ox, y + 1.02f, cz + oz), new Vector3(0.8f, 0.03f, 0.8f), dark);
            }

            // 水塔
            if (_rng.NextDouble() < 0.3)
            {
                var tank = Lit(new Color(0.40f, 0.30f, 0.25f), 0.2f);
                Cylinder("Tank", new Vector3(cx + w * 0.25f, y + 1.2f, cz + d * 0.2f), new Vector3(1.8f, 1.2f, 1.8f), tank);
                Cylinder("TankTop", new Vector3(cx + w * 0.25f, y + 2.45f, cz + d * 0.2f), new Vector3(1.9f, 0.08f, 1.9f), dark);
            }

            // 楼梯间
            if (_rng.NextDouble() < 0.4)
            {
                Cube("Stair", new Vector3(cx - w * 0.2f, y + 1.1f, cz + d * 0.22f), new Vector3(2.6f, 2.2f, 2.4f),
                    Lit(new Color(0.70f, 0.68f, 0.64f), 0.2f), true);
                Cube("StairDoor", new Vector3(cx - w * 0.2f - 1.31f, y + 0.9f, cz + d * 0.22f), new Vector3(0.05f, 1.7f, 0.9f), dark, false);
            }

            // 天线
            if (_rng.NextDouble() < 0.35)
            {
                float ax = cx + Rand(-0.3f, 0.3f) * w;
                float az = cz + Rand(-0.3f, 0.3f) * d;
                Cylinder("Antenna", new Vector3(ax, y + 2.8f, az), new Vector3(0.1f, 2.8f, 0.1f), dark);
                Sphere("AntennaTip", new Vector3(ax, y + 5.7f, az), new Vector3(0.28f, 0.28f, 0.28f),
                    Emissive(new Color(1f, 0.1f, 0.1f), _look.LampsOn ? _look.EmissiveIntensity * 2.5f : 0.5f));
            }

            // 太阳能板
            if (_rng.NextDouble() < 0.25)
            {
                var solar = Lit(new Color(0.10f, 0.18f, 0.35f), 0.7f, 0.3f);
                for (int i = 0; i < 3; i++)
                    RotCube("Solar", new Vector3(cx + w * 0.15f, y + 0.55f, cz - d * 0.2f + i * 1.3f),
                        new Vector3(2.4f, 0.08f, 1.0f), new Vector3(-22f, 0f, 0f), solar, true);
            }

            // 楼顶广告牌
            if (_rng.NextDouble() < 0.25)
            {
                Color c = NeonPalette[_rng.Next(NeonPalette.Length)];
                float bx = cx;
                float bz = cz - d / 2f + 1.0f;
                Cube("BoardLegL", new Vector3(bx - w * 0.25f, y + 1.0f, bz), new Vector3(0.18f, 2f, 0.18f), dark, false);
                Cube("BoardLegR", new Vector3(bx + w * 0.25f, y + 1.0f, bz), new Vector3(0.18f, 2f, 0.18f), dark, false);
                Cube("Board", new Vector3(bx, y + 3.0f, bz), new Vector3(w * 0.6f, 2.2f, 0.16f),
                    Emissive(c, _look.EmissiveIntensity * 1.4f), false);
            }

            // 屋顶绿化
            if (_rng.NextDouble() < 0.18)
            {
                var grass = Lit(new Color(0.28f, 0.55f, 0.30f), 0.15f);
                Cube("RoofGarden", new Vector3(cx + w * 0.1f, y + 0.12f, cz + d * 0.1f), new Vector3(w * 0.45f, 0.24f, d * 0.4f), grass, false);
                for (int i = 0; i < 3; i++)
                    Sphere("RoofShrub", new Vector3(cx + w * 0.1f + Rand(-1.5f, 1.5f), y + 0.6f, cz + d * 0.1f + Rand(-1.2f, 1.2f)),
                        new Vector3(0.9f, 0.7f, 0.9f), Lit(new Color(0.20f, 0.50f, 0.26f), 0.1f));
            }
        }

        // ------------------------------------------------------------------ 主角：电脑城

        private void BuildHeroMall()
        {
            float cx = -(BlockStart + CellSize); // 与让出的两格对齐
            float cz = BlockStart + 7f;
            float w = 24f, d = 14f, h = 15f;

            Building(cx, cz, w, d, h, new Color(0.26f, 0.30f, 0.38f), true, false);

            float front = cz - d / 2f;
            HeroFrontCenter = new Vector3(cx, 0f, front);

            ShopFront(cx, front, w, true);

            // 顶部大招牌：双色霓虹条（暗示"电子城"）
            Cube("MallSignTop", new Vector3(cx, h - 1.2f, front - 0.25f), new Vector3(w * 0.85f, 1.8f, 0.35f),
                Emissive(NeonPalette[1], _look.EmissiveIntensity * 1.6f), false);
            Cube("MallSignBar", new Vector3(cx, h - 3.0f, front - 0.25f), new Vector3(w * 0.85f, 0.4f, 0.3f),
                Emissive(NeonPalette[0], _look.EmissiveIntensity * 1.6f), false);

            // 侧面竖向霓虹
            Cube("MallSideNeon", new Vector3(cx - w / 2f - 0.25f, h / 2f, front + 1.5f), new Vector3(0.3f, h * 0.9f, 0.5f),
                Emissive(NeonPalette[3], _look.EmissiveIntensity * 1.3f), false);

            // 大屏幕
            Quad("MallScreen", new Vector3(cx + w * 0.22f, 8.2f, front - 0.06f), Vector3.back,
                new Vector3(8f, 4.2f, 1f), Emissive(NeonPalette[2], _look.EmissiveIntensity * 0.9f));
            Cube("MallScreenFrame", new Vector3(cx + w * 0.22f, 8.2f, front - 0.04f), new Vector3(8.4f, 4.6f, 0.1f),
                Lit(new Color(0.06f, 0.06f, 0.08f), 0.4f), false);

            // 竖幅广告
            for (int i = 0; i < 3; i++)
            {
                Color c = NeonPalette[(i * 2 + 1) % NeonPalette.Length];
                Quad("Banner", new Vector3(cx - w * 0.38f + i * 2.1f, 9.2f, front - 0.06f), Vector3.back,
                    new Vector3(1.2f, 4.4f, 1f), Emissive(c, _look.EmissiveIntensity * 0.8f));
            }

            // 屋顶轮廓灯带
            var led = Emissive(NeonPalette[1], _look.EmissiveIntensity * 1.2f);
            Cube("LedFront", new Vector3(cx, h + 0.55f, front - 0.1f), new Vector3(w + 0.3f, 0.18f, 0.18f), led, false);
            Cube("LedLeft", new Vector3(cx - w / 2f - 0.1f, h + 0.55f, cz), new Vector3(0.18f, 0.18f, d + 0.2f),
                Emissive(NeonPalette[0], _look.EmissiveIntensity * 1.2f), false);

            // 屋顶卫星天线
            var dish = Lit(new Color(0.80f, 0.82f, 0.85f), 0.5f, 0.2f);
            Sphere("Dish", new Vector3(cx + w * 0.3f, h + 2.2f, cz + d * 0.1f), new Vector3(2.4f, 0.5f, 2.4f), dish);
            Cylinder("DishPole", new Vector3(cx + w * 0.3f, h + 1.2f, cz + d * 0.1f), new Vector3(0.12f, 0.9f, 0.12f),
                Lit(new Color(0.2f, 0.2f, 0.22f), 0.4f));

            // 入口雨棚与立柱
            Cube("Canopy", new Vector3(cx + w * 0.23f, 3.5f, front - 1.8f), new Vector3(w * 0.34f, 0.22f, 3.6f),
                Lit(new Color(0.15f, 0.17f, 0.22f), 0.5f, 0.4f), true);
            var pillar = Lit(new Color(0.78f, 0.80f, 0.84f), 0.4f, 0.3f);
            Cylinder("PillarL", new Vector3(cx + w * 0.07f, 1.75f, front - 3.4f), new Vector3(0.35f, 1.75f, 0.35f), pillar);
            Cylinder("PillarR", new Vector3(cx + w * 0.39f, 1.75f, front - 3.4f), new Vector3(0.35f, 1.75f, 0.35f), pillar);
        }
    }
}
