using UnityEngine;
using PCShopSell.Core.World;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private const float WalkupHeight = 10.06f;
        private readonly System.Collections.Generic.List<Vector3> _quarterTrees = new System.Collections.Generic.List<Vector3>();
        private Rect? _quarterBounds;
        private Rect _mainlandLawn, _starterCircuit;
        private Vector2 _quarterAlong, _quarterAcross, _quarterOrigin;

        private Vector2 LawnCoordinates(Vector2 world)
        {
            var offset = world - _quarterOrigin;
            return new Vector2(Vector2.Dot(offset, _quarterAlong), Vector2.Dot(offset, _quarterAcross));
        }

        internal bool IsOldQuarterStreet(Vector3 position) => InPaintedQuarter(new Vector2(position.x,position.z));

        private void FindResidentialBounds()
        {
            var home = _city.GetPlace(StarterHome);
            _quarterOrigin = DoorXZ(home);
            float angle = (float)home.Heading * Mathf.Deg2Rad;
            _quarterAlong = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle));
            _quarterAcross = new Vector2(-_quarterAlong.y, _quarterAlong.x);
            var min = Vector2.zero;
            var max = Vector2.zero;
            var mainlandMin = Vector2.zero;
            var mainlandMax = Vector2.zero;
            foreach (var place in _city.Places)
            {
                var point = LawnCoordinates(DoorXZ(place));
                if (_city.AgencyDistrictFor(place.StreetId)?.id == "outskirts" && !IsLakeAccessNode(place.NodeId))
                {
                    mainlandMin = Vector2.Min(mainlandMin, point);
                    mainlandMax = Vector2.Max(mainlandMax, point);
                }
                if (place.HousingForm == ResidenceForm.Standard && place.Id != RouteGrocery) continue;
                min = Vector2.Min(min,point);
                max = Vector2.Max(max,point);
            }
            _quarterBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            var circuit = PCShopSell.Core.Session.RaceTracks.Get("race_0", _city);
            _starterCircuit = new Rect((float)(circuit.X-circuit.Width/2),(float)(circuit.Y-circuit.Depth/2),
                (float)circuit.Width,(float)circuit.Depth);
            _mainlandLawn = Rect.MinMaxRect(mainlandMin.x, mainlandMin.y, mainlandMax.x, mainlandMax.y);
        }

        private bool OnOutskirtsLawn(Vector3 world)
        {
            if (!HasStarterArt) return false;
            if (!_quarterBounds.HasValue) FindResidentialBounds();
            return _mainlandLawn.Contains(LawnCoordinates(new Vector2(world.x, world.z)));
        }

        internal float OuterDistrictWeight(Vector2 world)
        {
            if (!HasStarterArt) return 0;
            if (!_quarterBounds.HasValue) FindResidentialBounds();
            var point = LawnCoordinates(world);
            var closest = Vector2.Max(_mainlandLawn.min, Vector2.Min(_mainlandLawn.max, point));
            float distance = Vector2.Distance(point, closest);
            var island = Vector2.Max(_starterCircuit.min, Vector2.Min(_starterCircuit.max, world));
            distance = Mathf.Min(distance, Vector2.Distance(world, island));
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(65, 160, distance));
        }

        private Vector2 DoorXZ(Place place)
        {
            var node = NodeWorld(place.NodeId);
            return new Vector2(node.x + (float)place.EntranceX, node.z + (float)place.EntranceY);
        }

        internal Vector3 ClosestOldQuarterPoint(Vector3 position)
        {
            if (!_quarterBounds.HasValue) FindResidentialBounds();
            var bounds = _quarterBounds.Value;
            var d = new Vector2(position.x, position.z) - _quarterOrigin;
            var point = _quarterOrigin + _quarterAlong * Mathf.Clamp(Vector2.Dot(d, _quarterAlong), bounds.xMin, bounds.xMax)
                + _quarterAcross * Mathf.Clamp(Vector2.Dot(d, _quarterAcross), bounds.yMin, bounds.yMax);
            return new Vector3(point.x,0,point.y);
        }

        private bool InPaintedQuarter(Vector2 point, float radius = 33f)
        {
            if (!HasStarterArt) return false;
            var p=new Vector3(point.x,0,point.y);
            return (p-ClosestOldQuarterPoint(p)).sqrMagnitude<radius*radius;
        }
        private static Material QuarterMaterial(string name)
        {
            return Resources.Load<Material>("OldQuarter/" + name + "Paint");
        }

        private Material OutskirtsLawnFor(Place place) =>
            HasStarterArt && _city.AgencyDistrictFor(place.StreetId)?.id == "outskirts"
                ? QuarterMaterial("DistrictGrass") : null;

        private static GameObject QuarterModel(Transform parent, string model,string paint="Atlas")
        {
            var go = Object.Instantiate(Resources.Load<GameObject>("OldQuarter/" + model), parent, false);
            go.name = model;
            var material = QuarterMaterial(paint);
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterial = material;
            return go;
        }

        private static void MarkQuarterBuilding(Transform root, Place place, float width, float depth, float height)
        {
            var hit = root.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0, height / 2, depth / 2);
            hit.size = new Vector3(width, height, depth);
            root.gameObject.AddComponent<PlaceMarker>().PlaceId = place.Id;
        }

        private void BuildQuarterResidence(Transform root, Place place, float width, float depth)
        {
            bool tall = place.HousingForm == ResidenceForm.Walkup;
            int variation = StableCityHash(place.Id) % 3;
            string paint = tall && variation > 0 ? "Walkup" + variation : "Atlas";
            var model = QuarterModel(root, tall ? "Walkup" : "LaneHouse", paint).transform;
            if (!tall)
            {
                width = (float)place.ExteriorWidth;
                depth = (float)place.ExteriorDepth;
            }
            model.localScale = new Vector3(width / 9.65f, 1, depth / (tall ? 7.50f : 9.65f));
            MarkQuarterBuilding(root, place, width, depth, tall ? WalkupHeight : 3.95f);
            if (!tall)
            {
                var rim = Mats.Lit(ArtDirection.Hex("A88D6A"), .12f);
                foreach (float side in new[]{-1f,1f})
                {
                    Prims.Box(model,"Roof flashing",new Vector3(side*4.65f,3.964f,4.825f),new Vector3(.070f,.02f,9.38f),rim);
                    Prims.Box(model,"Roof flashing",new Vector3(0,3.964f,4.825f+side*4.65f),new Vector3(9.37f,.02f,.070f),rim);
                }
            }
            if (!tall && (place.Id == StarterHome || variation != 0))
            {
                var canvas = ArtDirection.Hex(variation == 2 && place.Id != StarterHome ? "AB7958" : "748776");
                FitDoorAwning(model, canvas);
                if (place.Id == StarterHome)
                {
                    var sideDoor = new GameObject("Courtyard door canopy").transform;
                    sideDoor.SetParent(model,false);
                    sideDoor.localPosition=new Vector3(4.825f,0,5.355f);
                    sideDoor.localRotation=Quaternion.Euler(0,-90,0);
                    FitDoorAwning(sideDoor, canvas);
                    PlaceClayPot(sideDoor, new Vector3(-1.21f,0,-.37f));
                }
            }
            if (variation != 0 || place.Id == StarterHome)
            {
                float side = variation == 1 ? -1 : 1;
                if (tall) StarterPlanter(model, new Vector3(side * 2.1f, 0, -.49f), variation == 1 ? 1.15f : .79f, variation);
                else
                {
                    PlaceClayPot(model,new Vector3(side * 2.08f,0,-.42f));
                }
            }
        }

        private static void FitDoorAwning(Transform door, Color canvas)
        {
            var awning = StarterPart(door, "Canvas over door", new Vector3(0, 2.70f, -.48f),
                new Vector3(1.65f, .09f, .99f), canvas, "fabric");
            awning.transform.localRotation = Quaternion.Euler(-13, 0, 0);
            for (int rib = -1; rib <= 1; rib++)
                StarterPart(awning.transform, "Canvas seam", new Vector3(rib*.28f, .52f, 0),
                    new Vector3(.014f, .06f, .96f), Color.Lerp(canvas, StarterTimber, .22f));
            StarterPart(door, "Canvas hem", new Vector3(0, 2.55f, -.95f), new Vector3(1.68f, .14f, .06f), canvas);
            foreach (float x in new[]{-.69f,.69f})
                StarterPart(door, "Awning bracket", new Vector3(x, 2.55f, -.41f), new Vector3(.04f, .05f, .8f), StarterTimber);
        }

        private static void PlaceClayPot(Transform parent, Vector3 at)
        {
            var clay = Mats.Lit(ArtDirection.Hex("AD7756"), .14f);
            Prims.Shape(PrimitiveType.Cylinder, parent, "Terracotta pot", at+Vector3.up*.20f, new Vector3(.48f,.20f,.48f), clay);
            Prims.Shape(PrimitiveType.Cylinder, parent, "Fired lip", at+Vector3.up*.40f, new Vector3(.55f,.048f,.55f), clay);
            QuarterLeafCluster(parent,"CrownB",at+Vector3.up*.68f,new Vector2(.73f,.73f),"RouteLeaves");
        }

        private static void QuarterShop(Transform root, Place place, float width, float depth)
        {
            var shop = QuarterModel(root, "Shopfront").transform;
            shop.localScale = new Vector3(width / 9.65f, 1, depth / 9.65f);
            MarkQuarterBuilding(root, place, width, depth, 5.20f);
            Prims.SignText(root, place.Name, new Vector3(0, 2.65f, -.12f), Mathf.Min(width-.4f,6.6f), .34f, ArtDirection.Cream);
        }

        private static void QuarterLeafCluster(Transform parent, string shape, Vector3 center, Vector2 extent,string paint="Canopy")
        {
            var card = new GameObject("Leaf cluster", typeof(MeshFilter), typeof(MeshRenderer));
            card.transform.SetParent(parent,false);
            card.transform.localPosition = center;
            card.transform.localScale = new Vector3(extent.x,extent.y,extent.x);
            var source = Resources.Load<GameObject>("OldQuarter/" + shape);
            card.GetComponent<MeshFilter>().sharedMesh = source.GetComponentInChildren<MeshFilter>().sharedMesh;
            var renderer = card.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = QuarterMaterial(paint);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        }

        private void QuarterTree(Transform parent, Vector3 world, float scale, float angle,bool blossom=false)
        {
            _quarterTrees.Add(world);
            var tree = QuarterModel(parent, "CourtyardTree").transform;
            tree.position = world;
            tree.rotation = Quaternion.Euler(0, angle, 0);
            tree.localScale = Vector3.one * scale;
            // One large painted crown carries the silhouette. A smaller, offset crown
            // overlaps the fork in depth as the camera moves around the trunk.
            int form = Mathf.Abs(Mathf.RoundToInt(angle / 17f)) % 3;
            bool round = form == 0;
            string paint=blossom ? "RouteBlossom" : "RouteLeaves";
            QuarterLeafCluster(tree,round ? "CrownA" : "CrownB",new Vector3(.10f,3.38f,.08f),
                round ? new Vector2(4.05f,3.23f) : new Vector2(3.68f,3.03f),paint);
            float lateral = form == 2 ? .85f : -.78f;
            QuarterLeafCluster(tree,round ? "CrownB" : "CrownA",new Vector3(lateral,3.02f,.49f),new Vector2(1.86f,1.53f),paint);
        }

        private static void QuarterLaneHouse(Transform root,Place place,float width,float depth,float plotHeight)
        {
            bool upstairs = plotHeight >= 7f;
            var house = QuarterModel(root,upstairs ? "YardWalkup" : "LaneHouse");
            float height = upstairs ? plotHeight : 3.7f;
            house.transform.localScale = new Vector3(width / 9.65f,upstairs ? height/7.63f : 1,depth / 9.65f);
            QuarterLeafCluster(root,"CrownB",new Vector3(-width*.46f,.39f,depth+.29f),new Vector2(1.37f,.79f));
            QuarterLeafCluster(root,"CrownA",new Vector3(width*.49f,.31f,depth+.32f),new Vector2(.90f,.63f));
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0,height*.5f,depth/2);
            collider.size = new Vector3(width,height,depth);
            if (place != null) root.gameObject.AddComponent<PlaceMarker>().PlaceId = place.Id;
        }

        private void QuarterGround(Transform parent, Transform home)
        {
            var lawn = new GameObject("Old quarter lawn",typeof(MeshFilter),typeof(MeshRenderer));
            lawn.transform.SetParent(parent,false);
            lawn.transform.position = home.position + Vector3.up * .004f;
            var mesh = new Mesh { name = "Old quarter lawn" };
            if (!_quarterBounds.HasValue) FindResidentialBounds();
            var study = _quarterBounds.Value;
            const float fringe = 105;
            Vector3 Corner(float x, float z)
            {
                var off = _quarterAlong * x + _quarterAcross * z;
                return new Vector3(off.x, 0, off.y);
            }
            var min = Vector2.Min(study.min, _mainlandLawn.min) - Vector2.one * fringe;
            var max = Vector2.Max(study.max, _mainlandLawn.max) + Vector2.one * fringe;
            var corners = new[] { min, new Vector2(min.x, max.y), max, new Vector2(max.x, min.y) };
            mesh.vertices = System.Array.ConvertAll(corners, point => Corner(point.x, point.y));
            mesh.uv = new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right };
            // Keep the original hand-painted garden strokes registered to the same houses.
            var studyMin = study.min - Vector2.one * fringe;
            var studySize = study.size + Vector2.one * (2 * fringe);
            mesh.uv3 = System.Array.ConvertAll(corners, p => new Vector2(
                (p.x - studyMin.x) / studySize.x, (p.y - studyMin.y) / studySize.y));
            mesh.triangles = new[] {0,1,2,0,2,3};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            lawn.GetComponent<MeshFilter>().sharedMesh = mesh;
            lawn.GetComponent<MeshRenderer>().sharedMaterial = QuarterMaterial("RouteGrass");
            lawn.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lawn.AddComponent<CityLandscapeResources>().Owned.Add(mesh);
        }
    }
}
