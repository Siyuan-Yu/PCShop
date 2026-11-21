using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.Game.Runtime
{
    internal static class AmusementParkView
    {
        private const int Cabins = 12;
        private const float EyeAtBottom = 2.4f;
        internal static float WheelRadius(bool garden) => ((garden ? 82f : 44f) - EyeAtBottom) * .5f;

        internal static void Build(Transform root, Place place, float width, float depth)
        {
            bool garden = place.Id == CityLifeCatalog.GardenAmusementVenue;
            var pale = Mats.Lit(ArtDirection.Hex("EBDAC0"));
            var red = Mats.Lit(ArtDirection.Hex("A7514E"));
            var blue = Mats.Lit(ArtDirection.Hex("3F6874"));
            var gold = Mats.Glow(ArtDirection.Hex("CC9B53"), ArtDirection.Hex("FFD68E") * 1.7f);
            var floor = IndustrialMaterials.Surface("paving", ArtDirection.Hex("C7AE8B"), new Vector2(width / 5, depth / 5));
            var plot = Prims.Box(root, "AmusementPaving", new Vector3(0, .035f, depth * .5f), new Vector3(width, .1f, depth), floor, true);
            plot.AddComponent<PlaceMarker>().PlaceId = place.Id;
            Prims.Box(root, "TicketGate", new Vector3(0, 3.7f, .7f), new Vector3(11f, 1.1f, .65f), garden ? red : blue);
            Prims.SignText(root, place.Name, new Vector3(0, 3.7f, .34f), 10.3f, .85f, ArtDirection.Cream);
            foreach (int side in new[] { -1, 1 })
            {
                Prims.Box(root, "GateColumn", new Vector3(side * 4.8f, 1.75f, .72f), new Vector3(.6f, 3.5f, .75f), pale);
                Prims.Box(root, "EntryLamp", new Vector3(side * 4.8f, 4.4f, .72f), new Vector3(.5f, .65f, .5f), gold);
            }
            Prims.Box(root, "TicketCounter", new Vector3(garden ? -8f : 5.3f, .65f, 6), new Vector3(4.1f, 1.3f, 2.2f), blue);
            Prims.Box(root, "CounterTop", new Vector3(garden ? -8f : 5.3f, 1.34f, 6), new Vector3(4.4f, .15f, 2.4f), pale);

            float radius = WheelRadius(garden);
            var wheel = Child(root, "FerrisWheel", new Vector3(garden ? -18 : -11, radius + EyeAtBottom, garden ? 29 : 16));
            var rim = Child(wheel, "Rim", Vector3.zero);
            foreach (float z in new[] { -.85f, .85f })
            {
                for (int step = 0; step < 36; step++)
                {
                    Vector3 a = WheelOffset(radius, step / 36f), b = WheelOffset(radius, (step + 1) / 36f);
                    Beam(rim, "RimSection", a + Vector3.forward * z, b + Vector3.forward * z, garden ? .48f : .32f, pale);
                    if (step % 3 == 0)
                    {
                        Beam(rim, "Spoke", Vector3.forward * z, a + Vector3.forward * z, .26f, blue);
                        Prims.Box(rim, "RimLight", a + Vector3.forward * z * 1.36f, Vector3.one * .72f, gold);
                    }
                }
            }
            for (int i = 0; i < Cabins; i++)
            {
                var cabin = Child(wheel, "Cabin" + i, WheelOffset(radius, i / (float)Cabins));
                Material body = i % 3 == 0 ? blue : i % 3 == 1 ? red : pale;
                Prims.Box(cabin, "CarBase", new Vector3(0, -.57f, 1.18f), new Vector3(2.55f, 1.1f, 2.85f), body);
                Prims.Box(cabin, "CurvedRoof", new Vector3(0, 1.18f, 1.18f), new Vector3(2.62f, .23f, 2.91f), body);
                foreach (float x in new[] { -1.22f, 1.22f })
                    foreach (float z in new[] { -.17f, 2.5f })
                        Prims.Box(cabin, "WindowPost", new Vector3(x, .58f, z), new Vector3(.12f, 1.2f, .12f), body);
                Prims.Box(cabin, "CabinLamp", new Vector3(0, 1.07f, 2.62f), new Vector3(.62f, .15f, .16f), gold);
            }
            foreach (float side in new[] { -1f, 1f })
                Beam(wheel, "SupportLeg", new Vector3(side * (garden ? 10f : 6f), -wheel.localPosition.y, -2.4f),
                    new Vector3(0, 0, -.85f), garden ? 1.25f : .85f, blue);
            Prims.Box(wheel, "Axle", Vector3.zero, new Vector3(2.7f, 2.7f, 2.6f), red);
            if (!garden) return;

            for (int i = 0; i < 72; i++)
            {
                Vector3 a = CoasterTrack(i / 72f), b = CoasterTrack((i + 1) / 72f);
                Beam(root, "CoasterTrack", a, b, .49f, red);
                if (i % 6 == 0) Beam(root, "CoasterSupport", new Vector3(a.x, .1f, a.z), a, .65f, pale);
            }
            var carousel = Child(root, "Carousel", new Vector3(6, 0, 11));
            var platform = Child(carousel, "Turntable", Vector3.zero);
            Prims.Shape(PrimitiveType.Cylinder, platform, "CarouselDeck", new Vector3(0, .35f, 0), new Vector3(10f, .35f, 10f), red);
            Prims.Shape(PrimitiveType.Cylinder, carousel, "Canopy", new Vector3(0, 4.45f, 0), new Vector3(10.6f, .45f, 10.6f), red);
            Prims.Box(carousel, "CenterColumn", new Vector3(0, 2.3f, 0), new Vector3(.8f, 4.5f, .8f), pale);
            for (int i = 0; i < 10; i++)
            {
                var seat = Child(platform, "Seat" + i, Quaternion.Euler(0, 36 * i, 0) * new Vector3(0, 0, 3.65f));
                seat.localEulerAngles = new Vector3(0, 36 * i + 90, 0);
                Prims.Box(seat, "BrassPole", new Vector3(0, 2.4f, 0), new Vector3(.09f, 4f, .09f), gold);
                if (i % 5 == 0) Prims.Box(seat, "Carriage", new Vector3(0, 1.2f, 0), new Vector3(1.5f, .95f, 1.7f), blue);
                else
                {
                    Prims.Box(seat, "HorseBody", new Vector3(0, 1.2f, 0), new Vector3(.57f, .6f, 1.4f), pale);
                    Prims.Box(seat, "HorseHead", new Vector3(0, 1.63f, .59f), new Vector3(.32f, .63f, .34f), pale);
                    Prims.Box(seat, "Saddle", new Vector3(0, 1.52f, -.12f), new Vector3(.62f, .17f, .67f), i % 2 == 0 ? red : blue);
                }
                Prims.Box(carousel, "CanopyLight", seat.localPosition * 1.38f + Vector3.up * 4.35f, Vector3.one * .32f, gold);
            }
            var stall = Child(root, "RingTossStall", new Vector3(26, 0, 9));
            Prims.Box(stall, "Awning", new Vector3(0, 3.1f, 1f), new Vector3(6f, .4f, 4f), red);
            Prims.SignText(stall, "投圈摊", new Vector3(0, 3.1f, -1.08f), 3.4f, .54f, ArtDirection.Cream);
            Prims.Box(stall, "PrizeShelf", new Vector3(0, .68f, 1.75f), new Vector3(5.5f, 1.32f, 2.2f), pale);
            foreach (int x in new[] { -1, 0, 1 })
                Prims.Shape(PrimitiveType.Cylinder, stall, "TossBottle", new Vector3(x * 1.5f, 1.63f, 1.84f), new Vector3(.32f, .29f, .32f), x == 0 ? red : blue);
        }

        internal static Vector3 CoasterTrack(float loops)
        {
            float a = loops * Mathf.PI * 2;
            return new Vector3(25 + Mathf.Sin(a) * 13, 7.6f - 4.7f * Mathf.Cos(a) - 1.7f * Mathf.Sin(a * 2), 34 - Mathf.Cos(a) * 16);
        }

        internal static Vector3 WheelOffset(float radius, float progress)
        {
            float a = progress * Mathf.PI * 2;
            return new Vector3(Mathf.Sin(a) * radius, -Mathf.Cos(a) * radius, 0);
        }

        private static Transform Child(Transform parent, string name, Vector3 at)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = at;
            return child;
        }

        private static void Beam(Transform root, string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var beam = Prims.Box(root, name, (a + b) * .5f, new Vector3(width, width, (b - a).magnitude + width * .5f), material);
            beam.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.forward);
        }

        internal static void TurnWheel(Transform wheel, float radius, float progress)
        {
            wheel.Find("Rim").localRotation = Quaternion.Euler(0, 0, 360 * progress);
            for (int i = 0; i < Cabins; i++) wheel.Find("Cabin" + i).localPosition = WheelOffset(radius, progress + i / (float)Cabins);
        }
    }

    internal sealed class AmusementRideCamera : System.IDisposable
    {
        private readonly Camera _camera, _worldCamera;
        private readonly Transform _venue, _wheel, _carousel, _stall;
        private readonly AmusementRide _ride;
        private readonly bool _garden;
        private readonly int _seat;
        private float _yaw, _pitch;
        internal readonly RenderTexture Texture;

        internal AmusementRideCamera(PlaceVisual venue, Camera worldCamera, AmusementRide ride, int seat)
        {
            _worldCamera = worldCamera;
            _venue = venue.Root.transform;
            _garden = venue.Place.Id == CityLifeCatalog.GardenAmusementVenue;
            _ride = ride;
            _seat = seat;
            _wheel = _venue.Find("FerrisWheel");
            _carousel = _venue.Find("Carousel/Turntable");
            _stall = _venue.Find("RingTossStall");
            Texture = new RenderTexture(1600, 900, 24) { name = "AmusementRideView" };
            _camera = new GameObject("AmusementRideCamera").AddComponent<Camera>();
            _camera.CopyFrom(worldCamera);
            _camera.enabled = false;
            _camera.cullingMask &= ~(1 << ShopBeacon.StreetPropertyEffectLayer);
            _camera.orthographic = false;
            _camera.fieldOfView = 52;
            _camera.nearClipPlane = .12f;
            _camera.farClipPlane = 3300;
            _camera.targetTexture = Texture;
            _camera.aspect = 16f / 9f;
            var options = _camera.GetUniversalAdditionalCameraData();
            var source = worldCamera.GetUniversalAdditionalCameraData();
            options.renderPostProcessing = source.renderPostProcessing;
            options.antialiasing = source.antialiasing;
        }

        internal void Look(Vector2 delta)
        {
            _yaw = Mathf.Repeat(_yaw + delta.x + 180, 360) - 180;
            _pitch = Mathf.Clamp(_pitch + delta.y, -44f, 50f);
        }

        internal void ResetLook() => _yaw = _pitch = 0;

        internal Vector2 RingViewport(float aim, float power) => _camera.WorldToViewportPoint(
            _stall.TransformPoint(new Vector3(aim * (1.5f / .58f), 1.89f, 1.84f + (power - .6f) * 3.6f)));

        internal void Render(float progress)
        {
            Vector3 at, angles;
            if (_ride == AmusementRide.FerrisWheel)
            {
                float radius = AmusementParkView.WheelRadius(_garden);
                AmusementParkView.TurnWheel(_wheel, radius, progress);
                at = _wheel.TransformPoint(AmusementParkView.WheelOffset(radius, progress) + Vector3.forward * 2.75f);
                angles = new Vector3(Mathf.Lerp(3f, _garden ? 17f : 24f, Mathf.Sin(progress * Mathf.PI)), _garden ? 143f : -103f, 0);
            }
            else if (_ride == AmusementRide.RollerCoaster)
            {
                float track = Mathf.Repeat(progress - (_seat == 0 ? 0 : .031f), 1f);
                Vector3 position = AmusementParkView.CoasterTrack(track);
                at = _venue.TransformPoint(position + Vector3.up * 1.5f);
                Vector3 target = _venue.TransformPoint(AmusementParkView.CoasterTrack(track + .014f) + Vector3.up * 1.2f);
                angles = Quaternion.LookRotation(target - at).eulerAngles;
                angles.z = Mathf.Sin(track * 2 * Mathf.PI) * 14;
            }
            else if (_ride == AmusementRide.Carousel)
            {
                _carousel.localRotation = Quaternion.Euler(0, progress * 720, 0);
                float facing = _seat == 0 ? 36 : 0;
                var offset = Quaternion.Euler(0, facing, 0) * new Vector3(0, _seat == 0 ? 2.45f + .21f * Mathf.Sin(progress * 14 * Mathf.PI) : 2.1f, 3.7f);
                at = _carousel.TransformPoint(offset);
                angles = new Vector3(3, _carousel.eulerAngles.y + facing + 35, 0);
            }
            else
            {
                at = _stall.TransformPoint(new Vector3(0, 2.75f, -4.9f));
                angles = Quaternion.LookRotation(_stall.TransformPoint(new Vector3(0, 1.5f, 1.5f)) - at).eulerAngles;
            }
            _camera.transform.SetPositionAndRotation(at, Quaternion.Euler(angles.x + _pitch, angles.y + _yaw, angles.z));
            _camera.backgroundColor = _worldCamera.backgroundColor;
            _camera.Render();
        }

        public void Dispose()
        {
            if (_wheel != null) AmusementParkView.TurnWheel(_wheel, AmusementParkView.WheelRadius(_garden), 0);
            if (_carousel != null) _carousel.localRotation = Quaternion.identity;
            if (_camera != null) Kill.It(_camera.gameObject);
            Texture.Release();
            Kill.It(Texture);
        }
    }
}
