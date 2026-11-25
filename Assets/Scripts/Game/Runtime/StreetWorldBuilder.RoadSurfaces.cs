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
            public bool Raised, GroundAtA, GroundAtB, Outskirts;
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
            public void Build(Transform root,string name,Material material,CityLandscapeResources owner)
            {
                if(vertices.Count==0)return;
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();owner.Owned.Add(mesh);
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
        }
        private readonly ForestSpatialIndex<Rect> _quarterYards=new();
        private const float OldTownLandscapeReach = 83f;
        private readonly ForestSpatialIndex<StreetSegment> _paintedStreets = new();

        private const float ForestTransition=92f;
        private float OldTownGroundCover(Vector3 world,float woodland=0)
        {
            var streets = _paintedStreets.At(world);
            if (streets == null) return 0;
            float cover = 0;
            foreach (var street in streets)
            {
                float radius = street.PavedWidth * .5f + OldTownLandscapeReach + woodland;
                float distance = Mathf.Sqrt(SegmentDistanceSquared(world, new Vector3(street.A.x,0,street.A.y),
                    new Vector3(street.B.x,0,street.B.y)));
                cover = Mathf.Max(cover, Mathf.SmoothStep(0, 1, (radius - distance) / 32f));
                if (cover >= 1) return 1;
            }
            return cover;
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
                    Outskirts = _outskirtsNodes?.Contains(n.Id) == true && _outskirtsNodes.Contains(e.To),
                    Raised = raisedA || raisedB, GroundAtA = !raisedA, GroundAtB = !raisedB });
            }
            // A raised access road owns its junction; the ground road terminates at that boundary.
            segments=segments.OrderByDescending(s=>s.Raised).ThenByDescending(s=>(s.B-s.A).sqrMagnitude).ToList();
            var roadIndex = _streetRoadEnvelopes = new RoadSurfaceGeometry.Index();
            var pavingIndex = new RoadSurfaceGeometry.Index();
            var oldPaving = new SurfaceMesh();
            var roads=new SurfaceMesh();var paving=new SurfaceMesh();var curb=new SurfaceMesh();var white=new SurfaceMesh();var yellow=new SurfaceMesh();
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
                s.AddSurface(roads, roadIndex.Cut(new List<Vector2[]>{p},p));roadIndex.Add(p);
            }
            void Pavement(Vector2[] p, StreetSegment street)
            {
                var parts=roadIndex.Cut(new List<Vector2[]>{p},p);parts=pavingIndex.Cut(parts,p);
                street.AddSurface(street.Outskirts ? oldPaving : paving, parts, alignToStreet:true);pavingIndex.Add(p);
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
                var approach = new StreetSegment { A = from, B = to, Outskirts = OutskirtsStreet(p.StreetId), PavedWidth = entranceWidth, Raised = raised, Deck = deck,
                    PavingIndex = _streetPavementEnvelopes.Polygons.Count };
                _streetPavementEnvelopes.Add(outline);
                Pavement(outline, approach);
                if (!raised && approach.Outskirts && (GameSession.IsCommercialPremises(p) || p.Model=="warehouse")
                    && (to-from).sqrMagnitude < 18f*18f)
                {
                    var facing=new Vector2(Mathf.Sin((float)p.Heading*Mathf.Deg2Rad),Mathf.Cos((float)p.Heading*Mathf.Deg2Rad));
                    float length=Vector2.Dot(to-from,facing);
                    if (length>2 && Vector2.Dot((to-from).normalized,facing)>.94f)
                    {
                        float width=GameSession.IsCommercialPremises(p) ? (float)PCShopSell.Core.Home.RoomSpec.ForShop(p.Size).Width : (float)p.Width;
                        Pavement(RoadSurfaceGeometry.Strip(to,to-facing*length,width+.6f),approach);
                    }
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
                foreach(int sign in new[]{-1,1})
                {
                    var offset=normal*sign*(s.Width*.5f-.35f);
                    Mark(RoadSurfaceGeometry.Strip(s.A+d*from+offset,s.A+d*to+offset,.08f),white);
                    if(!s.Raised)
                    {
                        var lip=normal*sign*(s.Width*.5f+.13f);
                        var kerb=RoadSurfaceGeometry.Strip(s.A+d*from+lip,s.A+d*to+lip,.25f);
                        s.AddSurface(curb,roadIndex.Cut(new List<Vector2[]>{kerb},kerb),.045f,alignToStreet:true);
                    }
                }
                float projection=Vector2.Dot(s.A,d);
                for(float at=Mathf.Floor(projection/5)*5-projection;at<length;at+=5)
                {
                    float start=Mathf.Max(from,at),end=Mathf.Min(at+2,to);
                    if(end>start+.05f)Mark(RoadSurfaceGeometry.Strip(s.A+d*start,s.A+d*end,.12f),yellow);
                }
            }

            foreach (var segment in segments)
            {
                if (!segment.Outskirts) continue;
                float radius = segment.PavedWidth * .5f + OldTownLandscapeReach;
                var min = Vector2.Min(segment.A,segment.B) - Vector2.one * radius;
                var max = Vector2.Max(segment.A,segment.B) + Vector2.one * radius;
                _paintedStreets.Add(Rect.MinMaxRect(min.x-ForestTransition,min.y-ForestTransition,max.x+ForestTransition,max.y+ForestTransition),segment);
            }

            oldPaving.Build(root, "Old town stone sidewalks", OldTownSurfaces.PanelMaterial("streetPavers"), resources);
            curb.Build(root,"路沿石",DistrictFinishes.Tile("cutStone",new Vector2(1.6f,.74f),ArtDirection.Hex("BAAE96")),resources);
            roads.Build(root,"RoadSurface",OldTownSurfaces.PanelMaterial("streetAsphalt"),resources);
            paving.Build(root,"PavementSurface",OldTownSurfaces.PanelMaterial("graniteWalk"),resources);
            white.Build(root,"RoadBoundaryLines",Mats.Lit(ArtDirection.Hex("AC9F83")),resources);
            yellow.Build(root,"LaneMarkings",Mats.Lit(ArtDirection.Brass),resources);
            yield return BuildStreetVerge(root,segments,pavingIndex);
        }

        private static GameObject _oldTownTree;
        private static GameObject OldTownTree => _oldTownTree != null ? _oldTownTree :
            (_oldTownTree = Resources.Load<GameObject>("OldTown/StreetTree"));

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
                float width=(float)p.Width,depth=(float)p.Depth;
                if(GameSession.IsCommercialPremises(p))
                {
                    var room=PCShopSell.Core.Home.RoomSpec.ForShop(p.Size);
                    width=Mathf.Max(width,(float)room.Width);depth=Mathf.Max(depth,(float)room.Depth);
                }
                Reserve(new Vector2(at.x+(float)p.EntranceX,at.z+(float)p.EntranceY),(float)p.Heading,width,depth);
            }
            foreach(var b in _city.Blocks) Reserve(new Vector2((float)b.x,(float)b.y),(float)b.heading,(float)b.width,(float)b.depth);
            foreach(var parcel in _city.LakeParcels) Reserve(new Vector2((float)parcel.x,(float)parcel.y),
                (float)parcel.heading,(float)parcel.width,(float)parcel.depth);
            var shore=SmoothShore();
            var details=new Dictionary<Vector2Int,ForestChunk>();
            var gardens=new Dictionary<Vector2Int,MiniatureDetailMesh>();
            var borders=new Dictionary<Vector2Int,StreetPlanting>();
            var treeLeaf=Mats.Lit(ArtDirection.Hex("577D35"),.04f);
            var treeTips=Mats.Lit(ArtDirection.Hex("A5B742"),.04f);
            var treeBark=Mats.Lit(ArtDirection.Hex("866342"),.06f);
            bool Overlaps(RoadSurfaceGeometry.Index index,Vector2[] probe) =>
                index.Nearby(probe).Any(id=>RoadSurfaceGeometry.Intersect(probe,index.Polygons[id]).Length>=3);
            // Small rear courts occupy only free dry land, outside every configured plot and approach.
            foreach(var place in _city.PublicPlaces.Where(BuildsPlace))
            {
                if(IsLakeAccessNode(place.NodeId)) continue;
                bool home=place.Model=="oldhome";
                bool market=place.Model=="warehouse";
                if(!home && !market && !GameSession.IsCommercialPremises(place)) continue;
                var node=NodeWorld(place.NodeId);
                var door=new Vector2(node.x+(float)place.EntranceX,node.z+(float)place.EntranceY);
                float rad=(float)place.Heading*Mathf.Deg2Rad;
                var back=new Vector2(Mathf.Sin(rad),Mathf.Cos(rad));
                var right=new Vector2(back.y,-back.x);
                int variant=StableCityHash(place.Id)%3;
                float depth=(float)place.Depth;
                if(GameSession.IsCommercialPremises(place)) depth=Mathf.Max(depth,(float)PCShopSell.Core.Home.RoomSpec.ForShop(place.Size).Depth);
                var rear=door+back*(depth+3.10f)+right*Mathf.Max(0,(float)place.Width*.5f-2.7f)*(variant==1?-1:1);
                float halfWidth=(float)place.Width*.5f;
                if(GameSession.IsCommercialPremises(place)) halfWidth=Mathf.Max(halfWidth,(float)PCShopSell.Core.Home.RoomSpec.ForShop(place.Size).Width*.5f);
                var sideOffset=right*(halfWidth+3.65f)*(variant==1?-1:1);
                var sideBase=door+back*2.0f;
                var mid=rear;
                Vector2[] probe=null;
                foreach(var candidate in new[]{sideBase+sideOffset,sideBase-sideOffset,rear})
                {
                    var rect=RoadSurfaceGeometry.Strip(candidate-back*2.10f,candidate+back*2.10f,5.20f);
                    if(Overlaps(reserved,rect)||Overlaps(paving,rect)||Overlaps(_streetRoadEnvelopes,rect)) continue;
                    if(rect.Any(v=>MountainHeight(v.x/LakeScale,v.y/LakeScale)>.055f ||
                        WithinLake(new Vector3(v.x,0,v.y)/LakeScale,shore))) continue;
                    mid=candidate;probe=rect;break;
                }
                if(probe==null) continue;
                var exclusion=Rect.MinMaxRect(probe.Min(v=>v.x)-1,probe.Min(v=>v.y)-1,probe.Max(v=>v.x)+1,probe.Max(v=>v.y)+1);
                _quarterYards.Add(exclusion,exclusion);
                reserved.Add(probe);
                var court=new GameObject("Service court "+place.Id).transform;
                court.SetParent(parent,false);
                court.position=new Vector3(mid.x,0,mid.y);
                court.rotation=Quaternion.Euler(0,(float)place.Heading,0);
                Prims.Box(court,"Yard paving",new Vector3(0,.023f,0),new Vector3(4.80f,.04f,3.68f),
                    OldTownSurfaces.Surface("paving",ArtDirection.Hex("BEAD91"),Vector2.one));
                QuarterModules.Add(court,"YardWall",new Vector3(0,.043f,1.78f),new Vector3(1.18f,1,1));
                if(market)
                {
                    QuarterModules.Add(court,"CargoPallet",new Vector3(-1.1f,.046f,.55f),Vector3.one);
                    QuarterModules.Add(court,"Crates",new Vector3(.65f,.046f,.54f),Vector3.one,6);
                }
                else if(variant==0 && (home || OutskirtsStreet(place.StreetId)))
                {
                    // A low service wing occupies only the already reserved side court.
                    var shell=QuarterModules.Add(court,"WallShell",new Vector3(-.16f,.06f,-.62f),new Vector3(.47f,.72f,.39f));
                    QuarterModules.Tint(shell,ArtDirection.Hex("DFD6B9"));
                    var roof=Prims.Box(court,"Lean-to roof",new Vector3(-.16f,2.36f,.48f),new Vector3(4.40f,.13f,2.62f),
                        DistrictFinishes.Tile("patina",new Vector2(1.35f,.90f)));
                    roof.transform.localRotation=Quaternion.Euler(-6,0,0);
                    QuarterModules.Add(court,"CargoBay",new Vector3(-.71f,.08f,-.63f),new Vector3(.57f,.70f,.72f));
                    QuarterModules.Add(court,"SideWindow",new Vector3(1.14f,.77f,-.64f),new Vector3(.72f,.68f,.73f));
                    QuarterModules.Add(court,"Crates",new Vector3(.94f,.07f,-1.22f),Vector3.one*.78f);
                    QuarterModules.Add(court,"BenchTools",new Vector3(-.86f,.07f,-1.27f),Vector3.one*.77f);
                }
                else if(home && variant!=0)
                {
                    QuarterModules.Add(court,"WashLine",new Vector3(-.37f,.046f,.83f),Vector3.one);
                    QuarterModules.Add(court,"Bench",new Vector3(1.82f,.046f,.02f),Vector3.one,270);
                }
                else
                {
                    QuarterModules.Add(court,"CycleShelter",new Vector3(-.34f,.046f,.06f),Vector3.one);
                    QuarterModules.Add(court,"Cycle",new Vector3(-.68f,.046f,.46f),Vector3.one,70);
                    QuarterModules.Add(court,"Cycle",new Vector3(.58f,.046f,.46f),Vector3.one,73);
                }
            }
            PlantOldHomesShade(parent,reserved,paving,shore);
            PlantVacantPlots(parent,reserved,paving,shore);
            PlantNeighbourhoodEdges(parent,reserved,paving,shore);
            // Road nodes often divide one straight frontage into several short segments.
            // Span from one actual frontage to the next, keeping driveways clear at ground level.
            var poles=new List<(string street,Vector2 foot,Vector2 right)>();
            foreach(var place in _city.PublicPlaces.Where(BuildsPlace))
            {
                if(!OutskirtsStreet(place.StreetId) || IsLakeAccessNode(place.NodeId) ||
                    !(place.Model=="shop" || place.Model=="warehouse" || place.Model=="oldhome" || place.Model=="station")) continue;
                var node=NodeWorld(place.NodeId);
                var door=new Vector2(node.x+(float)place.EntranceX,node.z+(float)place.EntranceY);
                float rad=(float)place.Heading*Mathf.Deg2Rad;
                var right=new Vector2(Mathf.Cos(rad),-Mathf.Sin(rad));var back=new Vector2(-right.y,right.x);
                float width=GameSession.IsCommercialPremises(place) ? (float)PCShopSell.Core.Home.RoomSpec.ForShop(place.Size).Width : (float)place.Width;
                var foot=door-right*(width*.5f-.23f)-back*2.29f;
                var pad=RoadSurfaceGeometry.Strip(foot-right*.33f,foot+right*.33f,.66f);
                if (Overlaps(reserved,pad) || Overlaps(_streetRoadEnvelopes,pad) ||
                    poles.Any(other=>(other.foot-foot).sqrMagnitude<25)) continue;
                reserved.Add(pad);
                poles.Add((place.StreetId,foot,right));
            }
            var used=new HashSet<int>();
            for(int i=0;i<poles.Count;i++)
            {
                var pole=poles[i];int nearest=-1;float limit=35f;
                for(int j=0;j<poles.Count;j++)
                {
                    var next=poles[j];var delta=next.foot-pole.foot;float dist=delta.magnitude;
                    if(next.street!=pole.street || dist<5 || dist>=limit || Vector2.Dot(pole.right,next.right)<.94f ||
                        Vector2.Dot(delta.normalized,pole.right)<.99f) continue;
                    var path=RoadSurfaceGeometry.Strip(pole.foot+delta.normalized,next.foot-delta.normalized,.66f);
                    if(Overlaps(_streetRoadEnvelopes,path)||Overlaps(reserved,path)) continue;
                    nearest=j;limit=dist;
                }
                if(nearest<0) continue;
                var other=poles[nearest];var axis=other.foot-pole.foot;
                float yaw=-Mathf.Atan2(axis.y,axis.x)*Mathf.Rad2Deg;
                QuarterModules.Add(parent,"PowerSpan",new Vector3(pole.foot.x,6.69f,pole.foot.y),new Vector3(limit/12,1,1),yaw);
                foreach(int end in new[]{i,nearest})
                    if(used.Add(end)) QuarterModules.Add(parent,"PowerPole",
                        new Vector3(poles[end].foot.x,.115f,poles[end].foot.y),Vector3.one,yaw);
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
                float spacing=12f;
                for(float along=4.8f+seed%4;along<street.Length-3.8f;along+=spacing)
                for(int side=-1;side<=1;side+=2)
                {
                    var pos=street.A+direction*along+across*side*(street.PavedWidth*.5f+3.0f+seed%3*.2f);
                    var world=new Vector3(pos.x,0,pos.y);
                    var lakePoint=world/LakeScale;
                    if(MountainHeight(lakePoint.x,lakePoint.z)>.04f||WithinLake(lakePoint,shore))continue;
                    var probe=RoadSurfaceGeometry.Strip(pos-direction*2.65f,pos+direction*2.65f,5.3f);
                    if(Overlaps(paving,probe)||Overlaps(reserved,probe))continue;
                    var key=new Vector2Int(Mathf.FloorToInt(pos.x/96),Mathf.FloorToInt(pos.y/96));
                    if(!details.TryGetValue(key,out var chunk)){chunk=new ForestChunk();details.Add(key,chunk);}
                    if(!gardens.TryGetValue(key,out var garden)){garden=new MiniatureDetailMesh();gardens.Add(key,garden);}
                    if (OldTownTree != null)
                    {
                        int treeSeed=seed+Mathf.RoundToInt(along)+side;
                        PaintedWoodland.Plant(parent,world,5+(treeSeed%4),treeSeed%11==0?2:treeSeed%3==0?3:0,treeSeed);
                    }
                    else garden.Tree(world,5.4f+seed%3*.35f,seed%12,treeLeaf,treeTips,treeBark);
                    if(!borders.TryGetValue(key,out var bed))borders.Add(key,bed=new StreetPlanting());
                    var axis=new Vector3(direction.x,0,direction.y);
                    var normal=new Vector3(across.x,0,across.y)*side;
                    for(int j=-2;j<=2;j++)
                    {
                        var flora=world+axis*(j*.82f)+normal*(.42f+.36f*Mathf.Sin(seed+j));
                        bed.Add(j==0?PlantSpecies.Broadleaf:j==1?PlantSpecies.GoldenBush:PlantSpecies.Grass,flora,
                            j==0?1.85f:1.26f,j==0?.94f:.45f+.13f*((seed+j+3)%3),seed+j);
                    }
                    var pole=Mats.Lit(ArtDirection.Hex("46515A"),.25f);
                    var lamp=world-new Vector3(across.x,0,across.y)*side*1.8f;
                    garden.Box(lamp+Vector3.up*.13f,new Vector3(.40f,.26f,.40f),pole);
                    garden.Box(lamp+Vector3.up*1.9f,new Vector3(.11f,3.8f,.11f),pole);
                    garden.Box(lamp+Vector3.up*3.90f,new Vector3(.42f,.55f,.42f),
                        Mats.Glow(ArtDirection.Hex("F4CA80"),new Color(2.8f,1.7f,.65f)));
                    garden.Box(lamp+Vector3.up*4.23f,new Vector3(.62f,.14f,.62f),pole);
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
            foreach(var bed in borders.Values)bed.Build(parent,"街边下层植被");
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
