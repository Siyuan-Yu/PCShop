using UnityEngine;

namespace PCShopSell.Game.Procedural
{
    public sealed partial class ProceduralCityBuilder
    {
        // ------------------------------------------------------------------ 路灯与树

        private void BuildStreetFurniture()
        {
            var poleMat = Lit(new Color(0.22f, 0.23f, 0.26f), 0.4f, 0.3f);
            var bulbMat = Emissive(new Color(1f, 0.82f, 0.5f), _look.LampsOn ? _look.EmissiveIntensity * 2f : 0.2f);

            for (float t = -WorldHalf + 10; t < WorldHalf - 5; t += 14f)
            {
                if (Mathf.Abs(t) < RoadHalfWidth + 2f) continue;
                Lamp(new Vector3(t, 0, RoadHalfWidth + 0.6f), poleMat, bulbMat, -1f);
                Lamp(new Vector3(t + 7f, 0, -RoadHalfWidth - 0.6f), poleMat, bulbMat, 1f);
                Lamp(new Vector3(RoadHalfWidth + 0.6f, 0, t + 7f), poleMat, bulbMat, -1f);
                Lamp(new Vector3(-RoadHalfWidth - 0.6f, 0, t), poleMat, bulbMat, 1f);
            }

            var trunk = Lit(new Color(0.35f, 0.25f, 0.18f), 0.1f);
            for (float t = -WorldHalf + 14; t < WorldHalf - 8; t += 14f)
            {
                if (Mathf.Abs(t) < RoadHalfWidth + 3f) continue;
                Tree(new Vector3(t, 0.3f, RoadHalfWidth + 2.0f), trunk);
                Tree(new Vector3(t + 7f, 0.3f, -RoadHalfWidth - 2.0f), trunk);
            }
        }

        private void Lamp(Vector3 pos, Material pole, Material bulb, float armDir)
        {
            pos.y = 0.3f;
            Cylinder("LampPole", pos + new Vector3(0, 3.2f, 0), new Vector3(0.16f, 3.2f, 0.16f), pole);
            Cylinder("LampBase", pos + new Vector3(0, 0.35f, 0), new Vector3(0.4f, 0.35f, 0.4f), pole);

            // 灯臂伸向路面
            Vector3 armDirVec = Mathf.Abs(pos.z) > Mathf.Abs(pos.x) ? new Vector3(0, 0, armDir) : new Vector3(armDir, 0, 0);
            Vector3 armEnd = pos + new Vector3(0, 6.5f, 0) + armDirVec * 1.1f;
            Cube("LampArm", pos + new Vector3(0, 6.5f, 0) + armDirVec * 0.55f,
                armDirVec.z != 0 ? new Vector3(0.1f, 0.1f, 1.1f) : new Vector3(1.1f, 0.1f, 0.1f), pole, false);
            Sphere("LampBulb", armEnd - new Vector3(0, 0.15f, 0), new Vector3(0.7f, 0.35f, 0.7f), bulb);

            Blob(pos + new Vector3(0, 0, 0), 1.0f, 1.0f);

            if (_look.LampsOn && _lampLights < 18 && Mathf.Abs(pos.x) < 45f && Mathf.Abs(pos.z) < 45f)
            {
                var lightGo = new GameObject("LampLight");
                lightGo.transform.SetParent(_root, false);
                lightGo.transform.position = armEnd - new Vector3(0, 0.4f, 0);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.78f, 0.5f);
                l.range = 13f;
                l.intensity = _look.LampIntensity;
                l.shadows = LightShadows.None;
                _lampLights++;
            }
        }

        private void Tree(Vector3 pos, Material trunk)
        {
            float s = Rand(0.85f, 1.25f);
            Cylinder("Trunk", pos + new Vector3(0, 1.1f * s, 0), new Vector3(0.4f, 1.1f * s, 0.4f), trunk);
            Color baseLeaf = new Color(Rand(0.20f, 0.34f), Rand(0.46f, 0.62f), Rand(0.24f, 0.32f));
            var leaf = Lit(baseLeaf, 0.1f);
            Sphere("Canopy", pos + new Vector3(0, 3.5f * s, 0), new Vector3(3.2f * s, 2.8f * s, 3.2f * s), leaf);
            Sphere("CanopyB", pos + new Vector3(0.8f * s, 4.1f * s, 0.3f * s), new Vector3(2.2f * s, 2.0f * s, 2.2f * s),
                Lit(Tint(baseLeaf, 1.12f), 0.1f));
            Sphere("CanopyC", pos + new Vector3(-0.7f * s, 3.0f * s, -0.4f * s), new Vector3(2.0f * s, 1.8f * s, 2.0f * s),
                Lit(Tint(baseLeaf, 0.9f), 0.1f));
            Blob(pos, 3.6f * s, 3.6f * s);
        }

        // ------------------------------------------------------------------ 街道小设施

        private void BuildStreetProps()
        {
            // 红绿灯：四个路口角
            foreach (var corner in new[] {
                new Vector2(RoadHalfWidth + 0.5f, RoadHalfWidth + 0.5f), new Vector2(-RoadHalfWidth - 0.5f, RoadHalfWidth + 0.5f),
                new Vector2(RoadHalfWidth + 0.5f, -RoadHalfWidth - 0.5f), new Vector2(-RoadHalfWidth - 0.5f, -RoadHalfWidth - 0.5f) })
                TrafficLight(new Vector3(corner.x, 0.3f, corner.y));

            // 公交站
            BusStop(new Vector3(30f, 0.3f, 6.7f));
            BusStop(new Vector3(-48f, 0.3f, 6.7f));

            // 沿人行道散布：长椅、垃圾桶、消防栓、路牌
            for (int i = 0; i < 40; i++)
            {
                bool alongX = i % 2 == 0;
                float t = Rand(-WorldHalf + 8f, WorldHalf - 8f);
                if (Mathf.Abs(t) < RoadHalfWidth + 4f) continue;
                float side = (i % 4 < 2 ? 1 : -1) * (RoadHalfWidth + 0.9f);
                Vector3 pos = alongX ? new Vector3(t, 0.3f, side) : new Vector3(side, 0.3f, t);
                switch (_rng.Next(4))
                {
                    case 0: Bench(pos, alongX); break;
                    case 1: Bin(pos); break;
                    case 2: Hydrant(pos); break;
                    default: StreetSign(pos, alongX); break;
                }
            }

            // 电脑城门口的小摊车
            Cart(new Vector3(HeroFrontCenter.x + 9f, 0.3f, 6.2f), NeonPalette[2]);
            Cart(new Vector3(HeroFrontCenter.x - 10f, 0.3f, 6.2f), NeonPalette[4]);
        }

        private void TrafficLight(Vector3 pos)
        {
            var pole = Lit(new Color(0.18f, 0.19f, 0.22f), 0.4f, 0.4f);
            Cylinder("TLPole", pos + new Vector3(0, 2.6f, 0), new Vector3(0.14f, 2.6f, 0.14f), pole);
            Cube("TLBox", pos + new Vector3(0, 5.5f, 0), new Vector3(0.5f, 1.4f, 0.5f), pole, false);
            float k = _look.LampsOn ? _look.EmissiveIntensity * 1.8f : 0.8f;
            Sphere("TLRed", pos + new Vector3(-0.28f, 5.95f, -0.28f), new Vector3(0.3f, 0.3f, 0.3f), Emissive(new Color(1f, 0.1f, 0.1f), k));
            Sphere("TLGreen", pos + new Vector3(-0.28f, 5.1f, -0.28f), new Vector3(0.3f, 0.3f, 0.3f), Emissive(new Color(0.1f, 1f, 0.3f), k));
            Blob(pos, 1.0f, 1.0f);
        }

        private void BusStop(Vector3 pos)
        {
            var frame = Lit(new Color(0.20f, 0.22f, 0.26f), 0.5f, 0.5f);
            var glass = Lit(new Color(0.55f, 0.72f, 0.80f), 0.9f);
            Cube("BusRoof", pos + new Vector3(0, 3.0f, 0), new Vector3(4.2f, 0.18f, 1.8f), frame, true);
            Cube("BusPostL", pos + new Vector3(-1.95f, 1.5f, -0.8f), new Vector3(0.1f, 3f, 0.1f), frame, false);
            Cube("BusPostR", pos + new Vector3(1.95f, 1.5f, -0.8f), new Vector3(0.1f, 3f, 0.1f), frame, false);
            Cube("BusBack", pos + new Vector3(0, 1.5f, 0.85f), new Vector3(4.0f, 2.8f, 0.08f), glass, false);
            // 灯箱广告
            Quad("BusAd", pos + new Vector3(1.2f, 1.6f, 0.78f), Vector3.back, new Vector3(1.3f, 2.0f, 1f),
                Emissive(NeonPalette[_rng.Next(NeonPalette.Length)], _look.NeonBoost * _look.EmissiveIntensity * 0.9f));
            Cube("BusBench", pos + new Vector3(-0.5f, 0.55f, 0.35f), new Vector3(2.2f, 0.1f, 0.5f),
                Lit(new Color(0.55f, 0.38f, 0.22f), 0.2f), false);
            Cube("BusSign", pos + new Vector3(-2.4f, 2.2f, -0.8f), new Vector3(0.7f, 0.9f, 0.08f),
                Emissive(new Color(0.2f, 0.6f, 1f), _look.EmissiveIntensity * 0.7f), false);
            Blob(pos + new Vector3(0, 0, 0), 5.0f, 2.6f);
        }

        private void Bench(Vector3 pos, bool alongX)
        {
            var wood = Lit(new Color(0.50f, 0.34f, 0.20f), 0.2f);
            var metal = Lit(new Color(0.15f, 0.15f, 0.17f), 0.4f, 0.4f);
            Vector3 size = alongX ? new Vector3(1.8f, 0.1f, 0.5f) : new Vector3(0.5f, 0.1f, 1.8f);
            Cube("BenchSeat", pos + new Vector3(0, 0.55f, 0), size, wood, true);
            Cube("BenchBack", pos + new Vector3(alongX ? 0 : 0.25f, 0.9f, alongX ? 0.25f : 0),
                alongX ? new Vector3(1.8f, 0.4f, 0.08f) : new Vector3(0.08f, 0.4f, 1.8f), wood, false);
            Cube("BenchLegA", pos + (alongX ? new Vector3(-0.75f, 0.28f, 0) : new Vector3(0, 0.28f, -0.75f)),
                new Vector3(0.1f, 0.55f, 0.1f), metal, false);
            Cube("BenchLegB", pos + (alongX ? new Vector3(0.75f, 0.28f, 0) : new Vector3(0, 0.28f, 0.75f)),
                new Vector3(0.1f, 0.55f, 0.1f), metal, false);
            Blob(pos, 2.4f, 1.2f);
        }

        private void Bin(Vector3 pos)
        {
            Cylinder("Bin", pos + new Vector3(0, 0.45f, 0), new Vector3(0.55f, 0.45f, 0.55f),
                Lit(new Color(0.20f, 0.35f, 0.25f), 0.3f));
            Cylinder("BinLid", pos + new Vector3(0, 0.93f, 0), new Vector3(0.6f, 0.04f, 0.6f),
                Lit(new Color(0.12f, 0.12f, 0.14f), 0.3f));
            Blob(pos, 1.1f, 1.1f);
        }

        private void Hydrant(Vector3 pos)
        {
            var red = Lit(new Color(0.78f, 0.15f, 0.12f), 0.4f);
            Cylinder("Hydrant", pos + new Vector3(0, 0.4f, 0), new Vector3(0.32f, 0.4f, 0.32f), red);
            Sphere("HydrantCap", pos + new Vector3(0, 0.85f, 0), new Vector3(0.36f, 0.3f, 0.36f), red);
            Blob(pos, 0.9f, 0.9f);
        }

        private void StreetSign(Vector3 pos, bool alongX)
        {
            var pole = Lit(new Color(0.55f, 0.57f, 0.60f), 0.5f, 0.5f);
            Cylinder("SignPole", pos + new Vector3(0, 1.4f, 0), new Vector3(0.07f, 1.4f, 0.07f), pole);
            Color c = _rng.NextDouble() < 0.5 ? new Color(0.15f, 0.40f, 0.80f) : new Color(0.85f, 0.65f, 0.10f);
            Cube("SignPlate", pos + new Vector3(0, 2.7f, 0), alongX ? new Vector3(0.8f, 0.8f, 0.05f) : new Vector3(0.05f, 0.8f, 0.8f),
                Lit(c, 0.3f), false);
        }

        private void Cart(Vector3 pos, Color neon)
        {
            var body = Lit(new Color(0.85f, 0.85f, 0.88f), 0.3f);
            var dark = Lit(new Color(0.12f, 0.12f, 0.14f), 0.4f);
            Cube("CartBody", pos + new Vector3(0, 0.95f, 0), new Vector3(2.0f, 1.0f, 1.2f), body, true);
            Cube("CartTop", pos + new Vector3(0, 1.5f, 0), new Vector3(2.1f, 0.1f, 1.3f), dark, false);
            Cube("CartStripe", pos + new Vector3(0, 0.95f, -0.62f), new Vector3(2.02f, 0.25f, 0.04f),
                Emissive(neon, _look.NeonBoost * _look.EmissiveIntensity * 1.2f), false);
            Cylinder("CartWheelL", pos + new Vector3(-0.7f, 0.35f, 0), new Vector3(0.7f, 0.06f, 0.7f), dark).transform.rotation = Quaternion.Euler(0, 0, 90);
            Cylinder("CartWheelR", pos + new Vector3(0.7f, 0.35f, 0), new Vector3(0.7f, 0.06f, 0.7f), dark).transform.rotation = Quaternion.Euler(0, 0, 90);
            Cylinder("UmbrellaPole", pos + new Vector3(0, 2.2f, 0), new Vector3(0.06f, 1.2f, 0.06f), dark);
            Sphere("Umbrella", pos + new Vector3(0, 3.35f, 0), new Vector3(3.0f, 0.7f, 3.0f), Lit(Tint(neon, 0.9f), 0.3f));
            Blob(pos, 3.4f, 2.2f);
        }
    }
}
