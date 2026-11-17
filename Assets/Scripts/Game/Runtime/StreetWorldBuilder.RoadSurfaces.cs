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

            public void AddSurface(SurfaceMesh mesh, List<Vector2[]> pieces, float lift = 0)
            {
                if (RampLength > 0)
                {
                    var direction = Direction;
                    var across = new Vector2(-direction.y, direction.x);
                    foreach (float distance in RampBoundaries())
                        pieces = RoadSurfaceGeometry.SplitAt(pieces, A + direction * distance, across);
                }
                mesh.Add(pieces, point => HeightAt(point) + lift);
            }
        }
        sealed class SurfaceMesh
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();readonly List<Vector2> uv=new List<Vector2>();
            public void Add(IEnumerable<Vector2[]> polygons, System.Func<Vector2, float> height)
            {
                foreach(var p in polygons)
                {
                    if(RoadSurfaceGeometry.Area(p)<.0001f)continue;
                    int offset=vertices.Count;
                    foreach(var v in p){vertices.Add(new Vector3(v.x,height(v),v.y));uv.Add(v/4);}
                    for(int i=1;i+1<p.Length;i++){triangles.Add(offset);triangles.Add(offset+i+1);triangles.Add(offset+i);}
                }
            }
            public void Build(Transform root,string name,Material material,CityLandscapeResources owner)
            {
                if(vertices.Count==0)return;
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();owner.Owned.Add(mesh);
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
        }
        private void BuildContinuousStreetSurfaces()
        {
            var root=new GameObject("ContinuousStreetSurfaces").transform;root.SetParent(_root,false);
            var resources=root.gameObject.AddComponent<CityLandscapeResources>();
            _lakeAccessSurfaces.Clear();
            _streetPavementEnvelopes = new RoadSurfaceGeometry.Index();
            float deck = LakeAccessHeight(_city);
            var segments=new List<StreetSegment>();var seen=new HashSet<string>();
            foreach(var n in _city.Map.Nodes)foreach(var e in _city.Map.NeighborsOf(n.Id))
            {
                string key=string.CompareOrdinal(n.Id,e.To)<0?n.Id+"|"+e.To:e.To+"|"+n.Id;if(!seen.Add(key))continue;
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
            var roads=new SurfaceMesh();var paving=new SurfaceMesh();var white=new SurfaceMesh();var yellow=new SurfaceMesh();
            foreach (var s in segments)
            {
                s.PavingIndex = _streetPavementEnvelopes.Polygons.Count;
                _streetPavementEnvelopes.Add(RoadSurfaceGeometry.Road(s.A, s.B, s.PavedWidth));
            }
            foreach (var s in segments)
            {
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
                var p=RoadSurfaceGeometry.Road(s.A,s.B,s.Width);
                s.AddSurface(roads, roadIndex.Cut(new List<Vector2[]>{p},p));roadIndex.Add(p);
            }
            void Pavement(Vector2[] p, StreetSegment street)
            {
                var parts=roadIndex.Cut(new List<Vector2[]>{p},p);parts=pavingIndex.Cut(parts,p);
                street.AddSurface(paving, parts);pavingIndex.Add(p);
            }
            foreach (var s in segments)
            {
                Pavement(_streetPavementEnvelopes.Polygons[s.PavingIndex], s);
                if (s.Raised) _lakeAccessSurfaces.Add(s);
            }
            foreach(var p in _city.PublicPlaces)
            {
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
                if (raised) _lakeAccessSurfaces.Add(approach);
            }
            var crossingBands = new RoadSurfaceGeometry.Index();
            var startClearance = new float[segments.Count];
            var endClearance = new float[segments.Count];
            for (int i = 0; i < segments.Count; i++)
            {
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
                foreach(int sign in new[]{-1,1})
                {
                    var offset=normal*sign*(s.Width*.5f-.35f);
                    Mark(RoadSurfaceGeometry.Strip(s.A+d*from+offset,s.A+d*to+offset,.08f),white);
                }
                float projection=Vector2.Dot(s.A,d);
                for(float at=Mathf.Floor(projection/5)*5-projection;at<length;at+=5)
                {
                    float start=Mathf.Max(from,at),end=Mathf.Min(at+2,to);
                    if(end>start+.05f)Mark(RoadSurfaceGeometry.Strip(s.A+d*start,s.A+d*end,.12f),yellow);
                }
            }
            roads.Build(root,"RoadSurface",IndustrialMaterials.Surface("asphalt",Asphalt,Vector2.one),resources);
            paving.Build(root,"PavementSurface",Mats.Lit(Sidewalk),resources);
            white.Build(root,"RoadBoundaryLines",Mats.Lit(ArtDirection.Cream),resources);
            yellow.Build(root,"LaneMarkings",Mats.Lit(ArtDirection.Brass),resources);
        }
    }
}
