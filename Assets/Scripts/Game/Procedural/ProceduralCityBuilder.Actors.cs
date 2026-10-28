using UnityEngine;

namespace PCShopSell.Game.Procedural
{
    public sealed partial class ProceduralCityBuilder
    {
        // ------------------------------------------------------------------ 车辆

        private void BuildCars()
        {
            // 沿 X 方向的路（z = ±2.5）
            for (int i = 0; i < 8; i++)
            {
                float x = -70f + i * 17f + Rand(0f, 4f);
                if (Mathf.Abs(x) < 7f) x += 12f;
                Car(new Vector3(x, 0, i % 2 == 0 ? -2.5f : 2.5f), i % 2 == 0 ? 1 : -1, true);
            }

            // 沿 Z 方向的路（x = ±2.5）
            for (int i = 0; i < 7; i++)
            {
                float z = -65f + i * 19f + Rand(0f, 4f);
                if (Mathf.Abs(z) < 7f) z += 12f;
                Car(new Vector3(i % 2 == 0 ? 2.5f : -2.5f, 0, z), i % 2 == 0 ? 1 : -1, false);
            }
        }

        private void Car(Vector3 pos, int dir, bool alongX)
        {
            Color body = CarPalette[_rng.Next(CarPalette.Length)];
            bool van = _rng.NextDouble() < 0.2;
            bool taxi = !van && body.r > 0.85f && body.g > 0.7f;
            var bodyMat = Lit(body, 0.65f, 0.25f);
            var glassMat = Lit(new Color(0.08f, 0.10f, 0.14f), 0.9f);
            var dark = Lit(new Color(0.07f, 0.07f, 0.08f), 0.3f);

            var carRoot = new GameObject("Car").transform;
            carRoot.SetParent(_root, false);
            carRoot.position = pos;
            carRoot.rotation = Quaternion.Euler(0, alongX ? (dir > 0 ? 90f : -90f) : (dir > 0 ? 0f : 180f), 0);

            // 局部坐标：前进方向为 +Z
            if (van)
            {
                Cube(carRoot, "VanBody", new Vector3(0, 1.0f, -0.2f), new Vector3(1.95f, 1.5f, 4.4f), bodyMat, true);
                Cube(carRoot, "VanWindow", new Vector3(0, 1.45f, 1.5f), new Vector3(1.97f, 0.6f, 1.0f), glassMat, false);
            }
            else
            {
                Cube(carRoot, "CarBody", new Vector3(0, 0.65f, 0), new Vector3(1.9f, 0.8f, 4.3f), bodyMat, true);
                Cube(carRoot, "CarCabin", new Vector3(0, 1.3f, -0.2f), new Vector3(1.7f, 0.7f, 2.2f), glassMat, true);
                Cube(carRoot, "CarRoof", new Vector3(0, 1.67f, -0.2f), new Vector3(1.72f, 0.06f, 2.0f), bodyMat, false);
            }

            // 轮子、保险杠
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    var wheel = Cylinder("Wheel", Vector3.zero, new Vector3(0.7f, 0.12f, 0.7f), dark);
                    wheel.transform.SetParent(carRoot, false);
                    wheel.transform.localPosition = new Vector3(sx * 0.95f, 0.35f, sz * 1.4f);
                    wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
            }

            Cube(carRoot, "BumperF", new Vector3(0, 0.38f, 2.15f), new Vector3(1.95f, 0.22f, 0.15f), dark, false);
            Cube(carRoot, "BumperB", new Vector3(0, 0.38f, -2.15f), new Vector3(1.95f, 0.22f, 0.15f), dark, false);

            var head = Emissive(new Color(1f, 0.95f, 0.8f), _look.LampsOn ? _look.EmissiveIntensity * 2.2f : 0.5f);
            var tail = Emissive(new Color(1f, 0.1f, 0.08f), _look.LampsOn ? _look.EmissiveIntensity * 1.8f : 0.4f);
            Cube(carRoot, "HeadL", new Vector3(-0.65f, 0.7f, 2.16f), new Vector3(0.4f, 0.22f, 0.08f), head, false);
            Cube(carRoot, "HeadR", new Vector3(0.65f, 0.7f, 2.16f), new Vector3(0.4f, 0.22f, 0.08f), head, false);
            Cube(carRoot, "TailL", new Vector3(-0.65f, 0.75f, -2.16f), new Vector3(0.4f, 0.2f, 0.08f), tail, false);
            Cube(carRoot, "TailR", new Vector3(0.65f, 0.75f, -2.16f), new Vector3(0.4f, 0.2f, 0.08f), tail, false);

            if (taxi)
                Cube(carRoot, "TaxiSign", new Vector3(0, 1.8f, -0.2f), new Vector3(0.7f, 0.2f, 0.3f),
                    Emissive(new Color(1f, 0.9f, 0.4f), _look.EmissiveIntensity * 1.4f), false);

            Blob(pos, 2.8f, 5.2f);
        }

        // ------------------------------------------------------------------ 行人

        private void BuildPeople()
        {
            // 在电脑城门口聚一小群顾客
            float baseX = HeroFrontCenter.x;
            float baseZ = HeroFrontCenter.z - 3.2f;
            for (int i = 0; i < 12; i++)
            {
                float x = baseX - 9f + i * 1.6f + Rand(-0.6f, 0.6f);
                float z = baseZ - Rand(0f, 2.2f);
                Person(new Vector3(x, 0.3f, z));
            }

            // 人行道上的散步者
            for (int i = 0; i < 30; i++)
            {
                bool alongX = i % 2 == 0;
                float t = Rand(-WorldHalf + 6f, WorldHalf - 6f);
                if (Mathf.Abs(t) < RoadHalfWidth + 1f) t += 10f;
                float side = (i % 4 < 2 ? 1 : -1) * (RoadHalfWidth + 1.8f + Rand(0f, 1.0f));
                Vector3 pos = alongX ? new Vector3(t, 0.3f, side) : new Vector3(side, 0.3f, t);
                Person(pos);
            }
        }

        private void Person(Vector3 pos)
        {
            Color shirt = ShirtPalette[_rng.Next(ShirtPalette.Length)];
            Color pants = PantsPalette[_rng.Next(PantsPalette.Length)];
            Color hair = HairPalette[_rng.Next(HairPalette.Length)];
            var skin = Lit(new Color(Rand(0.70f, 0.92f), Rand(0.55f, 0.74f), Rand(0.42f, 0.60f)), 0.2f);
            var pantsMat = Lit(pants, 0.2f);

            Cube("LegL", pos + new Vector3(-0.13f, 0.4f, 0), new Vector3(0.2f, 0.8f, 0.22f), pantsMat, true);
            Cube("LegR", pos + new Vector3(0.13f, 0.4f, 0), new Vector3(0.2f, 0.8f, 0.22f), pantsMat, true);
            Capsule("Torso", pos + new Vector3(0, 1.15f, 0), new Vector3(0.55f, 0.42f, 0.4f), Lit(shirt, 0.2f));
            Sphere("Head", pos + new Vector3(0, 1.78f, 0), new Vector3(0.38f, 0.38f, 0.38f), skin);
            Sphere("Hair", pos + new Vector3(0, 1.88f, 0.03f), new Vector3(0.4f, 0.3f, 0.4f), Lit(hair, 0.3f));

            if (_rng.NextDouble() < 0.4)
                Cube("Backpack", pos + new Vector3(0, 1.15f, 0.26f), new Vector3(0.4f, 0.5f, 0.2f),
                    Lit(Tint(shirt, 0.55f), 0.2f), false);

            Blob(pos + new Vector3(0, 0, 0), 1.1f, 1.1f);
        }
    }
}
