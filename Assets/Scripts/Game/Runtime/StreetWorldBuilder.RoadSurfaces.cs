using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private readonly List<StreetSegment> _lakeAccessSurfaces = new List<StreetSegment>();
        private RoadSurfaceGeometry.Index _streetPavementEnvelopes;
        private RoadSurfaceGeometry.Index _streetRoadEnvelopes;
        private const float GroundStreetY = .025f;

        internal static bool IsLakeAccessNode(string id) => id.StartsWith("race_", System.StringComparison.Ordinal)
            || id.StartsWith("motor_", System.StringComparison.Ordinal);

        public Vector3 StreetPosition(MapPoint point) => ToWorld(point, StreetHeightAt((float)point.X, (float)point.Y));

        internal float StreetHeightAt(float x, float z)
        {
            var point = new Vector2(x, z);
            float height = 0;
            foreach (var segment in _lakeAccessSurfaces)
            {
                var delta = segment.B - segment.A;
                float along = Mathf.Clamp01(Vector2.Dot(point - segment.A, delta) / delta.sqrMagnitude);
                if ((point - (segment.A + delta * along)).sqrMagnitude <= segment.PavedWidth * segment.PavedWidth * .25f)
                {
                    float surface = segment.HeightAt(point);
                    if (surface > GroundStreetY) height = Mathf.Max(height, surface);
                }
            }
            return height;
        }

        sealed class StreetSegment
        {
            public Vector2 A, B;
            public float Width, PavedWidth, Deck, DryStart, DryEnd, RampLength;
            public bool Raised, GroundAtA, GroundAtB;
            public int PavingIndex;
            public Vector2 Direction => (B - A).normalized;
            public float Length => (B - A).magnitude;

            public IEnumerable<float> RampBoundaries()
            {
                if (RampLength <= 0) yield break;
                if (GroundAtA)
                {
                    yield return DryStart;
                    yield return DryStart + RampLength;
                }
                if (GroundAtB)
                {
                    yield return Length - DryEnd - RampLength;
                    yield return Length - DryEnd;
                }
            }

            public float HeightAt(Vector2 point)
            {
                if (!Raised) return GroundStreetY;
                if (!GroundAtA && !GroundAtB) return Deck;
                if (RampLength <= 0) return GroundStreetY;
                float along = Vector2.Dot(point - A, Direction);
                float fraction = 1;
                if (GroundAtA) fraction = Mathf.Min(fraction, Mathf.Clamp01((along - DryStart) / RampLength));
                if (GroundAtB) fraction = Mathf.Min(fraction, Mathf.Clamp01((Length - along - DryEnd) / RampLength));
                return Mathf.Lerp(GroundStreetY, Deck, fraction);
            }

            public void AddSurface(SurfaceMesh mesh, List<Vector2[]> pieces, float lift = 0, bool alignToStreet = false)
            {
                if (RampLength > 0)
                {
                    var direction = Direction;
                    var across = new Vector2(-direction.y, direction.x);
                    foreach (float distance in RampBoundaries())
                        pieces = RoadSurfaceGeometry.SplitAt(pieces, A + direction * distance, across);
                }
                mesh.Add(pieces, point => HeightAt(point) + lift, alignToStreet ? Direction : Vector2.right);
            }
        }
        sealed class SurfaceMesh
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();readonly List<Vector2> uv=new List<Vector2>();
            public void Add(IEnumerable<Vector2[]> polygons, System.Func<Vector2, float> height, Vector2 tileAlong)
            {
                var tileAcross = new Vector2(-tileAlong.y,tileAlong.x);
                foreach(var p in polygons)
                {
                    if(RoadSurfaceGeometry.Area(p)<.0001f)continue;
                    int offset=vertices.Count;
                    foreach(var v in p)
                    {
                        vertices.Add(new Vector3(v.x,height(v),v.y));
                        uv.Add(new Vector2(Vector2.Dot(v,tileAlong),Vector2.Dot(v,tileAcross))/4);
                    }
                    for(int i=1;i+1<p.Length;i++){triangles.Add(offset);triangles.Add(offset+i+1);triangles.Add(offset+i);}
                }
            }
            public void Build(Transform root,string name,Material material,CityLandscapeResources owner,
                System.Func<Vector3,Vector4> frontageCoordinates = null, bool castsShadows = true)
            {
                if(vertices.Count==0)return;
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();owner.Owned.Add(mesh);
                if (frontageCoordinates != null)
                    mesh.SetUVs(2,vertices.ConvertAll(v => frontageCoordinates(v)));
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=material;
                if (!castsShadows) renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        private IEnumerator BuildContinuousStreetSurfaces()
        {
            var root=new GameObject("ContinuousStreetSurfaces").transform;root.SetParent(_root,false);
            var resources=root.gameObject.AddComponent<CityLandscapeResources>();
            _lakeAccessSurfaces.Clear();
            _streetPavementEnvelopes = new RoadSurfaceGeometry.Index();
            float deck = LakeAccessHeight(_city);
            float deadline = Time.realtimeSinceStartup + .008f;
            var segments=new List<StreetSegment>();var seen=new HashSet<string>();
            foreach(var n in _city.Map.Nodes)foreach(var e in _city.Map.NeighborsOf(n.Id))
            {
                string key=string.CompareOrdinal(n.Id,e.To)<0?n.Id+"|"+e.To:e.To+"|"+n.Id;if(!seen.Add(key))continue;
                if (!BuildsRoad(n.Id, e.To)) continue;
                var a=NodeWorld(n.Id);var b=NodeWorld(e.To);if((b-a).sqrMagnitude<.01f)continue;
                bool raisedA = IsLakeAccessNode(n.Id), raisedB = IsLakeAccessNode(e.To);
                float width = PlannedRoadWidth(a, b);
                segments.Add(new StreetSegment { A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), Width = width,
                    PavedWidth = width + (float)GameSession.CityPavingWidth * 2, Deck = deck,
                    Raised = raisedA || raisedB, GroundAtA = !raisedA, GroundAtB = !raisedB });
            }
            // A raised access road owns its junction; the ground road terminates at that boundary.
            segments=segments.OrderByDescending(s=>s.Raised).ThenByDescending(s=>(s.B-s.A).sqrMagnitude).ToList();
            var roadIndex = _streetRoadEnvelopes = new RoadSurfaceGeometry.Index();
            var pavingIndex = new RoadSurfaceGeometry.Index();
            var townRoads = new SurfaceMesh();
            var townWalks = new SurfaceMesh();
            var quarterRoads = new SurfaceMesh();
            var quarterPaving = new SurfaceMesh();
            var quarterEdges = new SurfaceMesh();
            var quarterDashes = new SurfaceMesh();
            var roads=new SurfaceMesh();var paving=new SurfaceMesh();var white=new SurfaceMesh();var yellow=new SurfaceMesh();
            foreach (var s in segments)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    yield return null;
                    deadline = Time.realtimeSinceStartup + .008f;
                }
                s.PavingIndex = _streetPavementEnvelopes.Polygons.Count;
                _streetPavementEnvelopes.Add(RoadSurfaceGeometry.Road(s.A, s.B, s.PavedWidth));
            }
            foreach (var s in segments)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    yield return null;
                    deadline = Time.realtimeSinceStartup + .008f;
                }
                if (!s.Raised || !(s.GroundAtA || s.GroundAtB)) continue;
                // Keep the full shared junction at street level, then rise after the intersecting pavement ends.
                var footprint = _streetPavementEnvelopes.Polygons[s.PavingIndex];
                if (s.GroundAtA) s.DryStart = s.PavedWidth * .5f;
                if (s.GroundAtB) s.DryEnd = s.PavedWidth * .5f;
                foreach (int i in _streetPavementEnvelopes.Nearby(footprint))
                {
                    if (segments[i].Raised) continue;
                    var overlap = RoadSurfaceGeometry.Intersect(footprint, _streetPavementEnvelopes.Polygons[i]);
                    var direction = s.Direction;
                    foreach (var point in overlap)
                    {
                        float along = Vector2.Dot(point - s.A, direction);
                        if (s.GroundAtA && along < s.Length * .5f) s.DryStart = Mathf.Max(s.DryStart, along);
                        if (s.GroundAtB && along > s.Length * .5f) s.DryEnd = Mathf.Max(s.DryEnd, s.Length - along);
                    }
                }
                int ends = (s.GroundAtA ? 1 : 0) + (s.GroundAtB ? 1 : 0);
                s.RampLength = Mathf.Clamp((s.Length - s.DryStart - s.DryEnd) / ends, 0, 8);
            }
            foreach(var s in segments)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    yield return null;
                    deadline = Time.realtimeSinceStartup + .008f;
                }
                var p=RoadSurfaceGeometry.Road(s.A,s.B,s.Width);
                s.AddSurface(InPaintedQuarter((s.A+s.B)*.5f,55f) ? quarterRoads : OuterDistrictWeight((s.A+s.B)*.5f) > .5f ? townRoads : roads, roadIndex.Cut(new List<Vector2[]>{p},p));roadIndex.Add(p);
            }
            void Pavement(Vector2[] p, StreetSegment street)
            {
                var parts=roadIndex.Cut(new List<Vector2[]>{p},p);parts=pavingIndex.Cut(parts,p);
                street.AddSurface(InPaintedQuarter((street.A+street.B)*.5f,55f) ? quarterPaving : OuterDistrictWeight((street.A+street.B)*.5f) > .5f ? townWalks : paving, parts, alignToStreet:true);pavingIndex.Add(p);
            }
            foreach (var s in segments)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    yield return null;
                    deadline = Time.realtimeSinceStartup + .008f;
                }
                Pavement(_streetPavementEnvelopes.Polygons[s.PavingIndex], s);
                if (s.Raised) _lakeAccessSurfaces.Add(s);
            }
            foreach(var p in _city.PublicPlaces.Where(BuildsPlace))
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    yield return null;
                    deadline = Time.realtimeSinceStartup + .008f;
                }
                var a=NodeWorld(p.NodeId);var b=a+new Vector3((float)p.EntranceX,0,(float)p.EntranceY);
                if ((a-b).sqrMagnitude <= .01f) continue;
                var from = new Vector2(a.x, a.z);
                var to = new Vector2(b.x, b.z);
                bool raised = IsLakeAccessNode(p.NodeId);
                const float entranceWidth = 2f;
                var outline = raised ? RoadSurfaceGeometry.Road(from, to, entranceWidth) : RoadSurfaceGeometry.Strip(from, to, entranceWidth);
                var approach = new StreetSegment { A = from, B = to, PavedWidth = entranceWidth, Raised = raised, Deck = deck,
                    PavingIndex = _streetPavementEnvelopes.Polygons.Count };
                _streetPavementEnvelopes.Add(outline);
                Pavement(outline, approach);
                if (p.Id == StarterHome || p.Id == "expansion_shop_s_oldhomes_02")
                {
                    var frontage = RoadSurfaceGeometry.Strip(from,to,(float)p.Width+.4f);
                    Pavement(frontage,approach);
                }
                if (raised) _lakeAccessSurfaces.Add(approach);
            }
            var crossingBands = new RoadSurfaceGeometry.Index();
            var startClearance = new float[segments.Count];
            var endClearance = new float[segments.Count];
            for (int i = 0; i < segments.Count; i++)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    yield return null;
                    deadline = Time.realtimeSinceStartup + .008f;
                }
                var street = segments[i];
                startClearance[i] = MarkCrossing(street, i, street.A, street.Direction);
                endClearance[i] = MarkCrossing(street, i, street.B, -street.Direction);
            }

            float MarkCrossing(StreetSegment street, int index, Vector2 junction, Vector2 forward)
            {
                float probeLength = Mathf.Min(25, street.Length * .45f);
                var probe = RoadSurfaceGeometry.Strip(junction, junction + forward * probeLength, street.Width);
                var neighborhood = RoadSurfaceGeometry.Strip(junction - Vector2.right * 3, junction + Vector2.right * 3, 6);
                var directions = new List<Vector2>();
                float clearance = street.Width * .5f;
                foreach (int id in roadIndex.Nearby(neighborhood))
                {
                    var arm = segments[id];
                    if (arm.Length < 2) continue;
                    Vector2 direction;
                    if ((arm.A - junction).sqrMagnitude < 9) direction = (arm.B - junction).normalized;
                    else if ((arm.B - junction).sqrMagnitude < 9) direction = (arm.A - junction).normalized;
                    else continue;
                    if (!directions.Any(value => Vector2.Dot(value, direction) > .94f)) directions.Add(direction);
                    if (id == index || Vector2.Dot(direction, forward) > .94f) continue;
                    foreach (var point in RoadSurfaceGeometry.Intersect(probe, roadIndex.Polygons[id]))
                        clearance = Mathf.Max(clearance, Vector2.Dot(point - junction, forward));
                }
                if (directions.Count < 3) return 0;
                const float bandLength = 1.7f;
                float start = clearance + .7f;
                if (street.Length <= 18 || start + bandLength > probeLength) return Mathf.Min(clearance, probeLength);
                var bandStart = junction + forward * start;
                var normal = new Vector2(-forward.y, forward.x);
                for (float across = -street.Width * .5f + .65f; across < street.Width * .5f - .4f; across += .7f)
                {
                    var from = bandStart + normal * across;
                    var stripe = RoadSurfaceGeometry.Strip(from, from + forward * bandLength, .4f);
                    var pieces = roadIndex.Cut(new List<Vector2[]> { stripe }, stripe,
                        id => id != index && Mathf.Abs(Vector2.Dot(segments[id].Direction, forward)) < .85f);
                    street.AddSurface(white, crossingBands.Cut(pieces, stripe), .015f);
                }
                crossingBands.Add(RoadSurfaceGeometry.Strip(bandStart, bandStart + forward * bandLength, street.Width));
                return start + bandLength + .25f;
            }

            var markIndex = new RoadSurfaceGeometry.Index();
            for(int i=0;i<segments.Count;i++)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    yield return null;
                    deadline = Time.realtimeSinceStartup + .008f;
                }
                var s=segments[i];var delta=s.B-s.A;float length=delta.magnitude;if(length<.1f)continue;
                float from = startClearance[i], to = length - endClearance[i];
                if (to <= from + .05f) continue;
                var d=delta/length;var normal=new Vector2(-d.y,d.x);
                void Mark(Vector2[] p,SurfaceMesh buffer)
                {
                    var parts=roadIndex.Cut(new List<Vector2[]>{p},p,id=>id!=i&&Mathf.Abs(Vector2.Dot(segments[id].Direction,d))<.85f);
                    parts = crossingBands.Cut(parts, p);
                    parts=markIndex.Cut(parts,p);s.AddSurface(buffer,parts,.015f);markIndex.Add(p);
                }
                bool paint = InPaintedQuarter((s.A+s.B)*.5f,55f);
                foreach(int sign in new[]{-1,1})
                {
                    var offset=normal*sign*(s.Width*.5f-.35f);
                    Mark(RoadSurfaceGeometry.Strip(s.A+d*from+offset,s.A+d*to+offset,.08f),paint ? quarterEdges : white);
                }
                float projection=Vector2.Dot(s.A,d);
                float spacing = paint ? 4.2f : 5f;
                for(float at=Mathf.Floor(projection/spacing)*spacing-projection;at<length;at+=spacing)
                {
                    float start=Mathf.Max(from,at),end=Mathf.Min(at+(paint ? 1.47f : 2f),to);
                    if(end>start+.05f)Mark(RoadSurfaceGeometry.Strip(s.A+d*start,s.A+d*end,paint ? .145f : .12f),paint ? quarterDashes : yellow);
                }
            }
            quarterRoads.Build(root, "Old quarter asphalt", QuarterMaterial("Road"), resources);
            if (HasStarterArt)
            {
                Matrix4x4 home = FrontageProjection(StarterHome);
                const string nearbyShop = "expansion_shop_s_oldhomes_02";
                Matrix4x4 shop = _city.HasPlace(nearbyShop) ? FrontageProjection(nearbyShop) : home;
                Vector4 WearCoordinates(Vector3 world)
                {
                    Vector3 a = home.MultiplyPoint3x4(world), b = shop.MultiplyPoint3x4(world);
                    return new Vector4(.5f+a.x,-a.z,.5f+b.x,-b.z);
                }
                quarterPaving.Build(root,"Old quarter paving",QuarterMaterial("Pavement"),resources,WearCoordinates);
                quarterEdges.Build(root,"Old quarter road edges",QuarterMaterial("Edge"),resources,castsShadows:false);
                quarterDashes.Build(root,"Old quarter lane paint",QuarterMaterial("Dash"),resources,castsShadows:false);
            }
            townRoads.Build(root,"Town asphalt",OldTownSurfaces.PanelMaterial("streetAsphalt"),resources);
            townWalks.Build(root,"Town flagstones",OldTownSurfaces.PanelMaterial("graniteWalk"),resources);
            roads.Build(root,"RoadSurface",IndustrialMaterials.Surface("asphalt",Asphalt,Vector2.one),resources);
            paving.Build(root,"PavementSurface",IndustrialMaterials.Surface("paving",Sidewalk,new Vector2(1.25f,1.25f)),resources);
            white.Build(root,"RoadBoundaryLines",Mats.Lit(ArtDirection.Cream),resources);
            yellow.Build(root,"LaneMarkings",Mats.Lit(ArtDirection.Brass),resources);
            yield return BuildStreetVerge(root,segments,pavingIndex);
        }

        private Matrix4x4 FrontageProjection(string placeId)
        {
            var place = _city.GetPlace(placeId);
            var offset = new Vector3((float)place.EntranceX,0,(float)place.EntranceY);
            var scale = new Vector3(Mathf.Max(1,(float)place.Width+.4f),1,Mathf.Max(1,offset.magnitude));
            return Matrix4x4.TRS(NodeWorld(place.NodeId)+offset,Quaternion.Euler(0,(float)place.Heading,0),scale).inverse;
        }

        private IEnumerator BuildStreetVerge(Transform parent,List<StreetSegment> streets,RoadSurfaceGeometry.Index paving)
        {
            var reserved=new RoadSurfaceGeometry.Index();
            void Reserve(Vector2 at,float heading,float width,float depth)
            {
                var angle=heading*Mathf.Deg2Rad;
                var back=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
                reserved.Add(RoadSurfaceGeometry.Strip(at-back*.8f,at+back*(depth+.8f),width+1.7f));
            }
            foreach(var p in _city.PublicPlaces.Where(BuildsPlace))
            {
                var at=NodeWorld(p.NodeId);
                Reserve(new Vector2(at.x+(float)p.EntranceX,at.z+(float)p.EntranceY),
                    (float)p.Heading,(float)p.Width,(float)p.Depth);
            }
            foreach(var b in _city.Blocks) Reserve(new Vector2((float)b.x,(float)b.y),(float)b.heading,(float)b.width,(float)b.depth);
            foreach(var parcel in _city.LakeParcels) Reserve(new Vector2((float)parcel.x,(float)parcel.y),
                (float)parcel.heading,(float)parcel.width,(float)parcel.depth);
            var shore=SmoothShore();
            var details=new Dictionary<Vector2Int,ForestChunk>();
            var gardens=new Dictionary<Vector2Int,MiniatureDetailMesh>();
            bool Overlaps(RoadSurfaceGeometry.Index index,Vector2[] probe) =>
                index.Nearby(probe).Any(id=>RoadSurfaceGeometry.Intersect(probe,index.Polygons[id]).Length>=3);
            // Segments without a street id inherit the palette of the nearest occupied frontage.
            var frontages=_city.PublicPlaces.Where(p=>!IsLakeAccessNode(p.NodeId)).Select(p=>(at:DoorXZ(p),district:ArtDistrict(p.StreetId))).ToArray();
            int DistrictAt(Vector2 at)
            {
                float best=float.PositiveInfinity;
                int district=0;
                foreach(var frontage in frontages)
                {
                    float distance=(frontage.at-at).sqrMagnitude;
                    if(distance<best){best=distance;district=frontage.district;}
                }
                return district;
            }
            float deadline = Time.realtimeSinceStartup + .008f;
            foreach(var street in streets)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    yield return null;
                    deadline = Time.realtimeSinceStartup + .008f;
                }
                if(street.Raised||street.Length<8)continue;
                var direction=street.Direction;
                var across=new Vector2(-direction.y,direction.x);
                int seed=(Mathf.RoundToInt(street.A.x*5)^Mathf.RoundToInt(street.A.y*13))&1023;
                int district=DistrictAt((street.A+street.B)*.5f);
                float spacing=district==0 ? 12f : 9.5f;
                for(float along=4.8f+seed%4;along<street.Length-3.8f;along+=spacing)
                for(int side=-1;side<=1;side+=2)
                {
                    var pos=street.A+direction*along+across*side*(street.PavedWidth*.5f+3.0f+seed%3*.2f);
                    var world=new Vector3(pos.x,0,pos.y);
                    var lakePoint=world/LakeScale;
                    if(MountainHeight(lakePoint.x,lakePoint.z)>.04f||WithinLake(lakePoint,shore))continue;
                    float clearance=district==0?2.65f:1.25f;
                    var probe=RoadSurfaceGeometry.Strip(pos-direction*clearance,pos+direction*clearance,clearance*2);
                    if(Overlaps(paving,probe)||Overlaps(reserved,probe))continue;
                    var key=new Vector2Int(Mathf.FloorToInt(pos.x/96),Mathf.FloorToInt(pos.y/96));
                    if(!details.TryGetValue(key,out var chunk)){chunk=new ForestChunk();details.Add(key,chunk);}
                    if(!gardens.TryGetValue(key,out var garden)){garden=new MiniatureDetailMesh();gardens.Add(key,garden);}
                    if (InPaintedQuarter(pos) || OnOutskirtsLawn(world))
                        QuarterTree(parent, world, 1f + seed%3 * .075f, seed%360);
                    else
                    {
                        PaintedWoodland.Plant(parent,world,6+seed%3,seed%9==0?2:0,seed);
                        garden.FlowerBed(world+new Vector3(.35f,0,.4f),2.2f,1.35f,seed,false);
                    }
                    var pole=Mats.Lit(ArtDirection.Hex("46515A"),.25f);
                    var lamp=world-new Vector3(across.x,0,across.y)*side*1.8f;
                    garden.Box(lamp+Vector3.up*.13f,new Vector3(.40f,.26f,.40f),pole);
                    garden.Box(lamp+Vector3.up*1.9f,new Vector3(.11f,3.8f,.11f),pole);
                    if(district>=2)
                    {
                        var arm=new Vector3(across.x,0,across.y)*(-side);
                        var rotation=Quaternion.LookRotation(arm);
                        var top=lamp+Vector3.up*3.84f;
                        var alloy=Mats.Lit(ArtDirection.Hex(district==3?"C4D7D8":"A8B6B5"),.36f);
                        garden.Box(top+arm*.33f,new Vector3(.24f,.14f,.95f),alloy,rotation);
                        garden.Box(top+arm*.52f-Vector3.up*.085f,new Vector3(.21f,.04f,.48f),
                            Mats.Glow(ArtDirection.Hex("C5E4E1"),new Color(.68f,.90f,.92f)),rotation);
                        if(district==3)
                            garden.Box(lamp+Vector3.up*1.35f+arm*.068f,new Vector3(.045f,1.35f,.018f),
                                Mats.Lit(ArtDirection.Hex("75BBB9")),rotation);
                    }
                    else
                    {
                        garden.Box(lamp+Vector3.up*3.90f,new Vector3(.42f,.55f,.42f),
                            Mats.Glow(ArtDirection.Hex("F4CA80"),new Color(2.8f,1.7f,.65f)));
                        garden.Box(lamp+Vector3.up*4.23f,new Vector3(.62f,.14f,.62f),pole);
                    }
                    if(seed%3==0)
                    {
                        var bench=world+new Vector3(direction.x,0,direction.y)*1.6f;
                        var rotation=Quaternion.LookRotation(new Vector3(across.x*side,0,across.y*side));
                        var timber=Mats.Lit(ArtDirection.Hex("B48B55"));
                        for(int slat=0;slat<3;slat++)
                            garden.Box(bench+Vector3.up*.48f+rotation*new Vector3(0,0,(slat-1)*.18f),new Vector3(1.65f,.095f,.14f),timber,rotation);
                        garden.Box(bench+Vector3.up*.82f+rotation*new Vector3(0,0,.30f),new Vector3(1.65f,.53f,.10f),timber,rotation);
                        foreach(int leg in new[]{-1,1})garden.Box(bench+Vector3.up*.23f+rotation*new Vector3(leg*.62f,0,0),new Vector3(.13f,.46f,.48f),pole,rotation);
                    }
                    for(int j=0;j<3;j++)
                    {
                        float angle=seed*.45f+j*2.39996f;
                        var at=world+new Vector3(Mathf.Sin(angle),.025f,Mathf.Cos(angle))*(.13f+j*.08f);
                        var width=new Vector3(Mathf.Cos(angle),0,-Mathf.Sin(angle))*.10f;
                        var tip=at+Vector3.up*(.20f+j*.055f)+width*.58f;
                        var vertices=chunk.Vertices[j%2];var triangles=chunk.Triangles[j%2];
                        int start=vertices.Count;
                        vertices.Add(at-width);vertices.Add(at+width);vertices.Add(tip);
                        vertices.Add(at+width);vertices.Add(at-width);vertices.Add(tip);
                        for(int k=0;k<6;k++)triangles.Add(start+k);
                        if(j==1&&seed%3==0)
                            AddCrown(chunk.Vertices[2],chunk.Triangles[2],tip,
                                new Vector3(.065f,.05f,.065f),false);
                    }
                    seed=(seed*113+91)&1023;
                }
            }
            foreach(var pair in gardens)
            {
                pair.Value.Build(parent,$"Street planting {pair.Key.x}/{pair.Key.y}");
                yield return null;
            }
            string[] colors={"54793D","9DAA58","E2B66C"};
            foreach(var pair in details)
                for(int i=0;i<colors.Length;i++)
                    if(pair.Value.Vertices[i].Count>0)
                        LandscapeMesh(parent,$"Verge_{pair.Key.x}_{pair.Key.y}_{i}",pair.Value.Vertices[i],pair.Value.Triangles[i],
                            Mats.Lit(ArtDirection.Hex(colors[i])));
        }
    }
}
