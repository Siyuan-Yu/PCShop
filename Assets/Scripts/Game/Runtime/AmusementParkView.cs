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
        private static readonly Vector3 CabinEye = new Vector3(0, .74f, -.39f);
        internal static float WheelRadius(bool garden) => ((garden ? 164f : 88f) - EyeAtBottom) * .5f;

        internal static void Build(Transform root, Place place, float width, float depth)
        {
            bool garden = place.Id == CityLifeCatalog.GardenAmusementVenue;
            var pale = Mats.Lit(ArtDirection.Hex(garden?"E1EFEC":"DEDCD1"));
            var red = Mats.Lit(ArtDirection.Hex("A7514E"));
            var blue = Mats.Lit(ArtDirection.Hex("3F6874"));
            var gold = Mats.Glow(ArtDirection.Hex("CC9B53"), ArtDirection.Hex("FFD68E") * 1.7f);
            var floor = IndustrialMaterials.Surface("paving", ArtDirection.Hex("C7AE8B"), new Vector2(width / 5, depth / 5));
            var plot = Prims.Box(root, "AmusementPaving", new Vector3(0, .035f, depth * .5f), new Vector3(width, .1f, depth), floor, true);
            plot.AddComponent<PlaceMarker>().PlaceId = place.Id;
            Prims.Box(root, "TicketGate", new Vector3(0, 3.7f, .7f), new Vector3(11f, 1.1f, .65f), garden ? Mats.Lit(ArtDirection.Hex("4C7C8A")) : blue);
            Prims.SignText(root, place.Name, new Vector3(0, 3.7f, .34f), 10.3f, .85f, ArtDirection.Cream);
            foreach (int side in new[] { -1, 1 })
            {
                Prims.Box(root, "GateColumn", new Vector3(side * 4.8f, 1.75f, .72f), new Vector3(.6f, 3.5f, .75f), pale);
                Prims.Box(root, "EntryLamp", new Vector3(side * 4.8f, 4.4f, .72f), new Vector3(.5f, .65f, .5f), gold);
            }
            Prims.Box(root, "TicketCounter", new Vector3(garden ? -8f : 5.3f, .65f, 6), new Vector3(4.1f, 1.3f, 2.2f), blue);
            Prims.Box(root, "CounterTop", new Vector3(garden ? -8f : 5.3f, 1.34f, 6), new Vector3(4.4f, .15f, 2.4f), pale);

            var flowers=new StreetPlanting(maintained:true);
            var joinery=new MiniatureDetailMesh();
            var wood=DistrictFinishes.Tile("walnut",Vector2.one*.4f);
            float counterX=garden?-8f:5.3f;
            foreach(int side in new[]{-1,1})
            {
                joinery.Box(new Vector3(counterX+side*1.96f,1.8f,6.75f),new Vector3(.12f,3.4f,.13f),wood);
                QuarterModules.Add(root,"Bench",new Vector3(side*(width*.43f),.10f,3),Vector3.one);
                for(int row=0;row<8;row++)
                    flowers.Add(row%5==0?PlantSpecies.Spirea:row%5==4?PlantSpecies.Hedge:PlantSpecies.Hydrangea,
                        new Vector3(side*(width*.47f),.11f,2.5f+row*1.27f),1.65f,1.04f+(row%3)*.11f,18+side*6+row/4);
            }
            if(garden)
            {
                var cap=new Vector3(counterX,3.48f,6.68f);
                GardenStructures.Plate(root,"售票台白瓷顶",cap,new Vector3(5.4f,.23f,3.85f),DistrictFinishes.Tile("pearlCeramic",new Vector2(2,1)));
                GardenStructures.SolarFins(joinery,cap+Vector3.up*.12f,3.8f,2.3f,3);
            }
            else QuarterModules.Add(root,"StripedCanopy",new Vector3(counterX,3.34f,6.83f),new Vector3(.65f,1.1f,2.40f));
            joinery.Build(root,"售票亭木构");flowers.Build(root,"游园花境");
            float radius = WheelRadius(garden);
            // Clear the neighboring coaster as the larger garden wheel passes through its lower arc.
            var wheel = Child(root, "FerrisWheel", new Vector3(garden ? -26 : -11, radius + EyeAtBottom - CabinEye.y, garden ? 29 : 16));
            var rim = Child(wheel, "Rim", Vector3.zero);
            foreach (float z in new[] { -.85f, .85f })
            {
                for (int step = 0; step < 36; step++)
                {
                    Vector3 a = WheelOffset(radius, step / 36f), b = WheelOffset(radius, (step + 1) / 36f);
                    Beam(rim, "RimSection", a + Vector3.forward * z, b + Vector3.forward * z, garden ? .77f : .40f, pale);
                    if(garden)
                    {
                        Beam(rim,"InnerRim",a*.967f+Vector3.forward*z,b*.967f+Vector3.forward*z,.37f,pale);
                        Beam(rim,"RimTruss",a*.967f+Vector3.forward*z,b+Vector3.forward*z,.21f,gold);
                    }
                    if (step % 3 == 0)
                    {
                        Beam(rim, "Spoke", Vector3.forward * z, a + Vector3.forward * z, garden ? .47f : .31f, garden ? pale : blue);
                        Prims.Box(rim, "RimLight", a + Vector3.forward * z * 1.36f, Vector3.one * .72f, gold);
                    }
                }
            }
            // Mount the cabins on the scenic side of the wheel, with the support legs behind them.
            float cabinSide = garden ? 1f : -1f;
            float cabinOffset = garden ? 3.55f : -3.75f;
            var upholstery = Mats.Lit(ArtDirection.Hex("B8A08A"), .12f);
            var interior = Mats.Lit(ArtDirection.Hex("E1D4BF"), .2f);
            var glass = Mats.Unlit(new Color(.66f, .82f, .86f, .07f));
            for (int i = 0; i < Cabins; i++)
            {
                var carriage = Child(wheel, "Cabin" + i, WheelOffset(radius, i / (float)Cabins));
                var cabin = Child(carriage, "PassengerCabin", new Vector3(0, 0, cabinOffset));
                cabin.localRotation = Quaternion.Euler(0, garden ? -37f : -103f, 0);
                Beam(carriage, "CabinSuspension", new Vector3(0, 0, .85f * cabinSide),
                    new Vector3(0, 1.52f, cabinOffset), .24f, blue);
                Material body = garden ? pale : i % 3 == 0 ? blue : i % 3 == 1 ? red : pale;
                Prims.Box(cabin, "CarFloor", new Vector3(0, -.57f, 0), new Vector3(3.2f, .16f, 2.5f), body);
                Prims.Box(cabin, "CabinRoof", new Vector3(0, 1.46f, 0), new Vector3(3.38f, .25f, 2.72f), garden ? pale : interior);
                if(garden)
                {
                    Prims.Shape(PrimitiveType.Sphere,cabin,"弧面观景舱顶",new Vector3(0,1.55f,0),new Vector3(3.52f,.64f,2.82f),pale);
                    Prims.Box(cabin,"CabinFascia",new Vector3(0,1.29f,0),new Vector3(3.31f,.15f,2.64f),body);
                    Prims.Box(cabin,"CabinApron",new Vector3(0,-.41f,0),new Vector3(3.24f,.39f,2.55f),body);
                }
                foreach (float x in new[] { -1.52f, 1.52f })
                {
                    Prims.Box(cabin, "SideSill", new Vector3(x, -.31f, 0), new Vector3(.10f, .37f, 2.41f), body);
                    Prims.Box(cabin, "SideGlass", new Vector3(x, .63f, 0), new Vector3(.02f, 1.53f, 2.35f), glass);
                    foreach (float z in new[] { -1.2f, 1.2f })
                        Prims.Box(cabin, "WindowPost", new Vector3(x, .63f, z), new Vector3(.085f, 1.53f, .085f), interior);
                }
                Prims.Box(cabin, "PanoramicGlass", new Vector3(0, .63f, 1.2f), new Vector3(2.97f, 1.53f, .02f), glass);
                Prims.Box(cabin, "FrontSill", new Vector3(0, -.31f, 1.2f), new Vector3(3.1f, .37f, .10f), interior);
                Prims.Box(cabin, "RearWall", new Vector3(0, .44f, -1.2f), new Vector3(3.09f, 1.89f, .10f), body);
                Prims.Box(cabin, "BenchSeat", new Vector3(0, -.12f, -.76f), new Vector3(2.68f, .17f, .65f), upholstery);
                Prims.Box(cabin, "BenchBack", new Vector3(0, .22f, -1.06f), new Vector3(2.68f, .57f, .17f), upholstery);
                if (i == 0) Child(cabin, "SeatedEye", CabinEye);
            }
            // Both A-frames stay behind the boarding side. The feet, axle bearings and
            // high cross-braces give the wheel a readable silhouette from across the water.
            float spread=garden?15.2f:7.2f;
            var baseStone=DistrictFinishes.Tile("cutStone",Vector2.one);
            for(int frame=0;frame<2;frame++)
            {
                float z=-(2.45f+frame*(garden?6.2f:4.0f))*cabinSide;
                var bearing=new Vector3(0,0,z*.45f);
                Prims.Box(wheel,"轴承座",bearing,new Vector3(garden?3.6f:2.55f,garden?3.1f:2.1f,1.16f),blue);
                foreach(int side in new[]{-1,1})
                {
                    var foot=new Vector3(side*spread,-wheel.localPosition.y+.42f,z);
                    Beam(wheel,"SupportLeg",foot,bearing,garden?1.54f:1.03f,blue);
                    Prims.Box(wheel,"支脚基座",foot,new Vector3(garden?3.1f:2.1f,.82f,garden?3.6f:2.7f),baseStone);
                    var inner=Vector3.Lerp(foot,bearing,.42f);
                    Beam(wheel,"下支架",inner,new Vector3(0,-wheel.localPosition.y*.74f,z),garden?.54f:.37f,blue);
                }
            }
            Prims.Box(wheel, "Axle", Vector3.zero, new Vector3(2.7f, 2.7f, 2.6f), red);
            if (!garden) return;

            for (int i = 0; i < 72; i++)
            {
                Vector3 a = CoasterTrack(i / 72f), b = CoasterTrack((i + 1) / 72f);
                var tangent=(b-a).normalized;var right=Vector3.Cross(Vector3.up,tangent).normalized*.75f;
                Beam(root, "CoasterTrack", a+right, b+right, .25f, red);
                Beam(root, "CoasterTrack", a-right, b-right, .25f, red);
                Beam(root, "Sleeper", a-right*1.12f, a+right*1.12f,.18f,blue);
                if(i%6==0)
                {
                    var next=CoasterTrack((i+6)/72f);
                    Beam(root,"Track diagonal brace",new Vector3(a.x,.55f,a.z),next-Vector3.up*.45f,.17f,blue);
                }
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
        private readonly Transform _venue, _wheel, _wheelSeat, _carousel, _stall;
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
            _wheelSeat = _wheel?.Find("Cabin0/PassengerCabin/SeatedEye");
            _carousel = _venue.Find("Carousel/Turntable");
            _stall = _venue.Find("RingTossStall");
            Texture = new RenderTexture(1600, 900, 24) { name = "AmusementRideView" };
            _camera = new GameObject("AmusementRideCamera").AddComponent<Camera>();
            _camera.CopyFrom(worldCamera);
            _camera.enabled = false;
            _camera.cullingMask &= ~(1 << ShopBeacon.StreetPropertyEffectLayer);
            _camera.orthographic = false;
            _camera.fieldOfView = ride == AmusementRide.FerrisWheel ? 54f : 52f;
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
            _yaw = Mathf.Clamp(_yaw + delta.x, -72f, 72f);
            _pitch = Mathf.Clamp(_pitch + delta.y, -28f, 34f);
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
                at = _wheelSeat.position;
                angles = new Vector3(Mathf.Lerp(5f, _garden ? 16f : 14f, Mathf.Sin(progress * Mathf.PI)), _wheelSeat.eulerAngles.y, 0);
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
