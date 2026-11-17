using System;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    public sealed class RaceTrackView:MonoBehaviour
    {
        public RaceTrack Track {get;private set;}
        private Transform _moving,_player,_gate;private Transform[] _rivals=Array.Empty<Transform>();
        private readonly Renderer[] _startLamps = new Renderer[3];
        private TrailRenderer[] _skidMarks = Array.Empty<TrailRenderer>();
        private ParticleSystem _tyreSmoke;
        private int _visibleStartLights = -1, _previousRecoveries;
        private bool _wasDrifting;
        public void Build(RaceTrack track)
        {
            Track=track;transform.position=new Vector3((float)track.X,.65f,(float)track.Y);
            var scenery=new GameObject("CircuitScenery").transform;scenery.SetParent(transform,false);
            Prims.Box(scenery,"IslandBank",Vector3.down*.4f,new Vector3((float)track.Width+10,.7f,(float)track.Depth+10),Mats.Lit(ArtDirection.Hex("B9AD8B")));
            Prims.Box(scenery,"Infield",Vector3.zero,new Vector3((float)track.Width,.15f,(float)track.Depth),Mats.Lit(ArtDirection.Sage));
            var resources=gameObject.AddComponent<CityLandscapeResources>();
            Ribbon(scenery,(float)track.HalfRoad+1.3f,.14f,Mats.Lit(ArtDirection.Cream),resources);
            Ribbon(scenery,(float)track.HalfRoad,.18f,Mats.Lit(ArtDirection.Hex("39434A")),resources);
            BuildBarriers(scenery, resources);
            BuildStartingGantry(scenery);
            bool starter = track.Tier == 0;
            var stand = starter ? new Vector3(-35, 0, -19) : new Vector3(-10, 0, 0);
            var pit = starter ? new Vector3(-11.5f, 0, -14) : new Vector3(15, 0, 8);
            for (int i = 0; i < 4; i++)
                Prims.Box(scenery, "Grandstand", stand + new Vector3(0, .6f + i * .6f, i * 2),
                    new Vector3(24, .7f, 2), Mats.Lit(i % 2 == 0 ? ArtDirection.Wood : ArtDirection.Cream));
            Prims.Box(scenery, "PitOffice", pit + Vector3.up * 2.2f, new Vector3(12, 4.3f, 6), Mats.Lit(ArtDirection.Cream));
            Prims.Box(scenery, "PitRoof", pit + Vector3.up * 4.5f, new Vector3(14, .5f, 8), Mats.Lit(ArtDirection.Blue));
            if (starter) BuildStarterLandmarks(scenery, resources);
            if (track.Kerbs.Length > 0) BuildKerbs(scenery, resources);
            for(int i=0;i<4;i++){Prims.Box(scenery,"FloodlightPole",new Vector3((i%2==0?-1:1)*(float)track.Width*.43f,4,(i<2?-1:1)*(float)track.Depth*.43f),new Vector3(.3f,8,.3f),Mats.Lit(ArtDirection.Ink));}
            StaticBatchingUtility.Combine(scenery.gameObject);
            _moving=new GameObject("RaceVehicles").transform;_moving.SetParent(transform,false);_moving.gameObject.SetActive(false);
        }
        private Vector3 Point(int i){var p=Track.Points[i%Track.Points.Length];return new Vector3((float)p.X,0,(float)p.Y);}

        private void BuildStartingGantry(Transform scenery)
        {
            var gantry = new GameObject("StartingGantry").transform;
            gantry.SetParent(scenery, false);
            gantry.localPosition = Point(0);
            gantry.localRotation = Quaternion.LookRotation(Point(1) - Point(0));
            float halfRoad = (float)Track.HalfRoad;
            float postOffset = halfRoad + 1.65f;
            var metal = Mats.Lit(ArtDirection.Ink);
            if (Track.Tier == 0) BuildStarterGrid(gantry);
            else Prims.Box(gantry, "StartFinish", Vector3.up * .22f, new Vector3(2 * halfRoad, .04f, 1.2f), Mats.Lit(ArtDirection.Cream));
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * postOffset;
                Prims.Box(gantry, "GantryFoot", new Vector3(x, .21f, 0), new Vector3(.9f, .24f, 1.05f), metal);
                Prims.Box(gantry, "GantryPost", new Vector3(x, 2.75f, 0), new Vector3(.37f, 5.4f, .45f), metal);
            }
            Prims.Box(gantry, "GantryBeam", Vector3.up * 5.45f, new Vector3(2 * postOffset + .5f, .42f, .5f), metal);
            Prims.Box(gantry, "SignalHousing", new Vector3(0, 5.12f, -.33f), new Vector3(3.35f, 1.04f, .42f), metal);
            // Keep the changing signal materials outside the static circuit batch.
            var signals = new GameObject("StartSignals").transform;
            signals.SetParent(transform, false);
            signals.SetPositionAndRotation(gantry.position, gantry.rotation);
            for (int i = 0; i < _startLamps.Length; i++)
            {
                var lamp = Prims.Shape(PrimitiveType.Sphere, signals, $"StartSignal{i + 1}",
                    new Vector3((i - 1) * 1.02f, 5.12f, -.58f), new Vector3(.74f, .74f, .28f), Mats.Lit(ArtDirection.Ink));
                _startLamps[i] = lamp.GetComponent<Renderer>();
            }
        }

        private void BuildStarterGrid(Transform gantry)
        {
            const int columns = 16;
            float halfRoad = (float)Track.HalfRoad;
            float tile = 2 * halfRoad / columns;
            var white = Mats.Lit(ArtDirection.Cream);
            var dark = Mats.Lit(ArtDirection.Hex("526476"));
            for (int column = 0; column < columns; column++)
            for (int row = 0; row < 2; row++)
                Prims.Box(gantry, "StartFinish", new Vector3(-halfRoad + tile * (column + .5f), .194f, row * .6f - .3f),
                    new Vector3(tile, .02f, .6f), (column + row) % 2 == 0 ? white : dark);

            PaintSlot(0, (float)RaceSimulation.PlayerGridDistance, Mats.Lit(ArtDirection.Brass));
            for (int i = 0; i < RaceSimulation.RivalCount; i++)
                PaintSlot((float)RaceSimulation.RivalLaneOffset(i, halfRoad), (float)RaceSimulation.RivalGridDistance(i), white);

            void PaintSlot(float x, float z, Material color)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Prims.Box(gantry, "GridSlot", new Vector3(x + side, .194f, z), new Vector3(.1f, .02f, 3), color);
                    Prims.Box(gantry, "GridSlot", new Vector3(x, .194f, z + side * 1.5f), new Vector3(2.1f, .02f, .1f), color);
                }
            }
        }

        private static void BuildStarterLandmarks(Transform scenery, CityLandscapeResources resources)
        {
            var rubber = Mats.Lit(ArtDirection.Hex("33383D"));
            var hollow = Mats.Lit(ArtDirection.Hex("171E24"));
            foreach (var (x, z, radius, layers) in new[] { (54f, -22f, 1.45f, 2), (57.4f, -20.5f, 1.3f, 2),
                (56.5f, -26f, 1.6f, 3), (60f, -24f, 1.2f, 1) })
            {
                for (int layer = 0; layer < layers; layer++)
                    Prims.Shape(PrimitiveType.Cylinder, scenery, "OldTyre", new Vector3(x, .25f + layer * .3f, z),
                        new Vector3(radius * 2, .15f, radius * 2), rubber);
                Prims.Shape(PrimitiveType.Cylinder, scenery, "TyreWell", new Vector3(x, .395f + (layers - 1) * .3f, z),
                    new Vector3(radius * 1.15f, .012f, radius * 1.15f), hollow);
            }

            var arrow = new Mesh { name = "StarterDirectionArrow" };
            arrow.vertices = new[] { new Vector3(-.32f, 0, -1.7f), new Vector3(-.32f, 0, .25f),
                new Vector3(-1.06f, 0, .25f), new Vector3(0, 0, 1.65f), new Vector3(1.06f, 0, .25f),
                new Vector3(.32f, 0, .25f), new Vector3(.32f, 0, -1.7f) };
            arrow.triangles = new[] { 0, 1, 5, 0, 5, 6, 2, 3, 4 };
            arrow.RecalculateNormals();
            arrow.RecalculateBounds();
            resources.Owned.Add(arrow);
            foreach (var (x, z, yaw) in new[] { (0f, -47f, 90f), (76f, -22f, 0f), (19f, 21f, -34f),
                (-33f, 42f, -90f), (-76f, -6f, 180f) })
            {
                var mark = new GameObject("DirectionArrow", typeof(MeshFilter), typeof(MeshRenderer));
                mark.transform.SetParent(scenery, false);
                mark.transform.localPosition = new Vector3(x, .195f, z);
                mark.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                mark.GetComponent<MeshFilter>().sharedMesh = arrow;
                mark.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(ArtDirection.Cream);
            }
        }

        private void SyncStartingLights(int count)
        {
            if (_visibleStartLights == count) return;
            _visibleStartLights = count;
            var red = Mats.Glow(new Color(1f, .21f, .08f), new Color(3f, .43f, .16f), nightOnly: false);
            var green = Mats.Glow(new Color(.24f, 1f, .32f), new Color(.38f, 2.8f, .52f), nightOnly: false);
            var dark = Mats.Lit(new Color(.19f, .055f, .05f));
            for (int i = 0; i < _startLamps.Length; i++)
                _startLamps[i].sharedMaterial = count == 0 ? green : i < count ? red : dark;
        }

        private void BuildDriftEffects(MotorModel model)
        {
            bool scooter = model.Kind == VehicleKind.EBike;
            bool car = model.Kind == VehicleKind.Car;
            float axle = car ? -1.02f : -.7f;
            float trackWidth = car ? .78f : .65f;
            _skidMarks = new TrailRenderer[scooter ? 1 : 2];
            for (int i = 0; i < _skidMarks.Length; i++)
            {
                var contact = new GameObject("RearTyreMark").transform;
                contact.SetParent(_player, false);
                contact.localPosition = new Vector3(scooter ? 0 : (i == 0 ? -trackWidth : trackWidth), -.02f, axle);
                contact.localRotation = Quaternion.Euler(-90, 0, 0);
                var mark = contact.gameObject.AddComponent<TrailRenderer>();
                mark.sharedMaterial = Mats.Unlit(new Color(.055f, .065f, .08f, .68f));
                mark.alignment = LineAlignment.TransformZ;
                mark.shadowCastingMode = ShadowCastingMode.Off;
                mark.receiveShadows = false;
                mark.time = 4;
                mark.minVertexDistance = .18f;
                mark.startWidth = car ? .18f : .10f;
                mark.endWidth = .025f;
                mark.emitting = false;
                _skidMarks[i] = mark;
            }

            var smoke = new GameObject("TyreSmoke").transform;
            smoke.SetParent(_player, false);
            smoke.localPosition = new Vector3(0, .10f, axle);
            _tyreSmoke = smoke.gameObject.AddComponent<ParticleSystem>();
            _tyreSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _tyreSmoke.main;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.32f, .6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.20f, .38f);
            main.maxParticles = 14;
            var emission = _tyreSmoke.emission;
            emission.rateOverTime = car ? 20 : 13;
            emission.enabled = false;
            var shape = _tyreSmoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = car ? .30f : .15f;
            var size = _tyreSmoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .55f, 1, 1.6f));
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = _startLamps[0].GetComponent<MeshFilter>().sharedMesh;
            renderer.sharedMaterial = Mats.Unlit(new Color(.76f, .79f, .82f, .18f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void SyncDriftEffects(RaceSimulation race)
        {
            bool showing = race.Drifting && !race.Paused && !race.Finished;
            bool recovered = _previousRecoveries != race.Recoveries;
            bool newDrift = showing && !_wasDrifting;
            _previousRecoveries = race.Recoveries;
            _wasDrifting = race.Drifting;
            foreach (var mark in _skidMarks)
            {
                // A fresh slide must not draw a line back to the last corner.
                if (recovered || newDrift) mark.Clear();
                mark.emitting = showing;
            }
            if (race.Paused)
            {
                if (_tyreSmoke.isPlaying) _tyreSmoke.Pause();
                return;
            }
            if (recovered) _tyreSmoke.Clear();
            var emission = _tyreSmoke.emission;
            emission.enabled = showing;
            if ((showing || _tyreSmoke.isPaused) && !_tyreSmoke.isPlaying) _tyreSmoke.Play();
        }

        private void BuildBarriers(Transform parent, CityLandscapeResources resources)
        {
            int count = Track.Points.Length;
            var joins = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var incoming = (Point(i) - Point((i + count - 1) % count)).normalized;
                var outgoing = (Point(i + 1) - Point(i)).normalized;
                var nextNormal = Vector3.Cross(Vector3.up, outgoing);
                var bisector = (Vector3.Cross(Vector3.up, incoming) + nextNormal).normalized;
                // Both neighbouring segments use the same miter joint, with no overlapping end faces.
                joins[i] = bisector / Mathf.Max(.25f, Vector3.Dot(bisector, nextNormal));
            }
            foreach (int side in new[] { -1, 1 })
            {
                var vertices = new List<Vector3>();
                var triangles = new[] { new List<int>(), new List<int>() };
                void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int material)
                {
                    int start = vertices.Count;
                    vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                    bool forward = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) > 0;
                    int[] indices = forward ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
                    foreach (int index in indices) triangles[material].Add(start + index);
                }
                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % count;
                    float offset = side * ((float)Track.HalfRoad + 3.8f);
                    var a = Point(i) + joins[i] * (offset - .325f) + Vector3.up * .05f;
                    var b = Point(j) + joins[j] * (offset - .325f) + Vector3.up * .05f;
                    var c = Point(j) + joins[j] * (offset + .325f) + Vector3.up * .05f;
                    var d = Point(i) + joins[i] * (offset + .325f) + Vector3.up * .05f;
                    var height = Vector3.up * 1.1f;
                    var normal = Vector3.Cross(Vector3.up, (Point(j) - Point(i)).normalized);
                    Quad(a, b, b + height, a + height, -normal, i % 2);
                    Quad(d, c, c + height, d + height, normal, i % 2);
                    Quad(a + height, b + height, c + height, d + height, Vector3.up, i % 2);
                    Quad(a, b, c, d, Vector3.down, i % 2);
                }
                var mesh = new Mesh { name = "ContinuousRaceBarrier" };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(triangles[0], 0); mesh.SetTriangles(triangles[1], 1);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                resources.Owned.Add(mesh);
                var go = new GameObject("Barrier", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterials = new[] { Mats.Lit(ArtDirection.Clay), Mats.Lit(ArtDirection.Cream) };
            }
        }
        private Vector3 RoadNormal(int index)
        {
            var tangent = (Point(index + 1) - Point(index + Track.Points.Length - 1)).normalized;
            return Vector3.Cross(Vector3.up, tangent);
        }

        private void BuildKerbs(Transform parent, CityLandscapeResources resources)
        {
            var vertices = new List<Vector3>();
            var faces = new[] { new List<int>(), new List<int>() };
            const float stripeLength = 2.6f;
            foreach (var (first, last, side) in Track.Kerbs)
            {
                float inner = (float)Track.HalfRoad - .12f, outer = (float)Track.HalfRoad + 1.2f;
                float left = side > 0 ? inner : -outer, right = side > 0 ? outer : -inner;
                float travelled = 0;
                for (int i = first; i < last; i++)
                {
                    var a = Point(i) + Vector3.up * .193f;
                    var b = Point(i + 1) + Vector3.up * .193f;
                    var normalA = RoadNormal(i);
                    var normalB = RoadNormal(i + 1);
                    float length = Vector3.Distance(a, b);
                    float end = travelled + length;
                    for (int stripe = Mathf.FloorToInt(travelled / stripeLength); stripe * stripeLength < end; stripe++)
                    {
                        float from = (Mathf.Max(travelled, stripe * stripeLength) - travelled) / length;
                        float to = (Mathf.Min(end, (stripe + 1) * stripeLength) - travelled) / length;
                        if (to <= from) continue;
                        int start = vertices.Count;
                        vertices.Add(Vector3.Lerp(a + normalA * left, b + normalB * left, from));
                        vertices.Add(Vector3.Lerp(a + normalA * left, b + normalB * left, to));
                        vertices.Add(Vector3.Lerp(a + normalA * right, b + normalB * right, to));
                        vertices.Add(Vector3.Lerp(a + normalA * right, b + normalB * right, from));
                        faces[stripe % 2].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    }
                    travelled = end;
                }
            }
            var mesh = new Mesh { name = "RaceKerbs", subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(faces[0], 0);
            mesh.SetTriangles(faces[1], 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            resources.Owned.Add(mesh);
            var kerbs = new GameObject("Kerbs", typeof(MeshFilter), typeof(MeshRenderer));
            kerbs.transform.SetParent(parent, false);
            kerbs.GetComponent<MeshFilter>().sharedMesh = mesh;
            kerbs.GetComponent<MeshRenderer>().sharedMaterials = new[] { Mats.Lit(ArtDirection.Clay), Mats.Lit(ArtDirection.Cream) };
        }

        private void Ribbon(Transform parent,float half,float height,Material mat,CityLandscapeResources resources)
        {
            int n=Track.Points.Length;var v=new Vector3[n*2];var tri=new int[n*6];
            for(int i=0;i<n;i++){var normal=RoadNormal(i);var at=Point(i)+Vector3.up*height;v[i*2]=at-normal*half;v[i*2+1]=at+normal*half;int j=(i+1)%n;int k=i*6;tri[k]=i*2;tri[k+1]=j*2;tri[k+2]=i*2+1;tri[k+3]=i*2+1;tri[k+4]=j*2;tri[k+5]=j*2+1;}
            var mesh=new Mesh{name="RaceRoad"};mesh.vertices=v;mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateBounds();resources.Owned.Add(mesh);
            var go=new GameObject("RoadRibbon",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        public void Begin(RaceSimulation race)
        {
            foreach(Transform c in _moving)Destroy(c.gameObject);
            _moving.gameObject.SetActive(true);_player=MotorVisual.Build(_moving,race.Model,MotorVisual.Paint(race.Model.Id));
            BuildDriftEffects(race.Model);
            _previousRecoveries = race.Recoveries;
            _wasDrifting = false;
            if (race.WetSurface)
                for (int i = 0; i < race.Track.Points.Length; i += 6)
                {
                    var point = race.Track.Points[i];
                    Prims.Box(_moving, "WetPracticePatch", new Vector3((float)point.X, .055f, (float)point.Y),
                        new Vector3(3.2f, .015f, 2), Mats.Lit(new Color(.18f,.26f,.32f)));
                }
            if (race.SponsorSticker)
            {
                bool car = MotorCatalog.ClassOf(race.Model.Kind) == 2;
                Prims.Box(_player, "PhoneShopSponsorSticker", new Vector3(car ? .78f : .24f, car ? .85f : .64f, 0),
                    new Vector3(.025f, car ? .32f : .20f, car ? .65f : .42f), Mats.Lit(ArtDirection.Brass));
            }
            if (MotorCatalog.ClassOf(race.Model.Kind) != 2)
            {
                var helmet = _player.Find("Helmet");
                var jacket = _player.Find("RiderJacket");
                if (helmet != null && race.ProtectiveHelmet) helmet.GetComponent<Renderer>().sharedMaterial = Mats.Lit(ArtDirection.Blue);
                if (jacket != null && race.ProtectiveSuit) jacket.GetComponent<Renderer>().sharedMaterial = Mats.Lit(ArtDirection.Clay);
            }
            _rivals=new Transform[race.RivalProgress.Length];
            for(int i=0;i<_rivals.Length;i++)
            {
                var model = race.RivalMotor(i);
                var paint = Color.Lerp(MotorVisual.Paint(model.Id), i % 2 == 0 ? ArtDirection.Clay : ArtDirection.Blue, .30f + (i % 3) * .12f);
                _rivals[i] = MotorVisual.Build(_moving, model, paint);
                _rivals[i].name = i < race.Opponents.Count ? "Rival_" + race.Opponents[i].PersonId : "Rival_" + (i + 1);
            }
            _gate=Prims.Box(_moving,"NextCheckpoint",Vector3.zero,new Vector3((float)Track.HalfRoad*2,.12f,1),Mats.Unlit(new Color(.3f,1,.55f))).transform;
            Sync(race);
        }
        public void Sync(RaceSimulation r)
        {
            PlaceMotor(_player,r.X,r.Y,r.Heading);
            SyncStartingLights(r.StartLights);
            SyncDriftEffects(r);
            for(int i=0;i<_rivals.Length;i++){double h;var p=r.RivalPosition(i,out h);PlaceMotor(_rivals[i],p.X,p.Y,h);}
            var next=Point(r.Next);_gate.localPosition=next+Vector3.up*.27f;_gate.localRotation=Quaternion.LookRotation(Point(r.Next+1)-Point(r.Next));
            _gate.gameObject.SetActive(r.StartLights == 0 && !r.ShowGo);
        }
        private static void PlaceMotor(Transform t,double x,double y,double h){t.localPosition=new Vector3((float)x,.23f,(float)y);t.localRotation=Quaternion.Euler(0,(float)h*Mathf.Rad2Deg,0);}
        public void End()
        {
            SyncStartingLights(-1);
            _moving.gameObject.SetActive(false);
        }
    }
    public static class MotorVisual
    {
        public static Color Paint(string modelId)
        {
            switch(modelId)
            {
                case "e_city": return ArtDirection.Hex("A8D6AC");
                case "e_range": return ArtDirection.Hex("7796BE");
                case "e_turn": return ArtDirection.Hex("EE8A74");
                case "e_dash": return ArtDirection.Hex("F4B427");
                case "e_race": return ArtDirection.Hex("20CBC0");
                case "t_cargo": return ArtDirection.Hex("537B43");
                case "t_sport": return ArtDirection.Hex("2455DA");
                case "v_basic": return ArtDirection.Hex("DADCE2");
                case "v_cargo": return ArtDirection.Hex("E7BD55");
                case "c_city": return ArtDirection.Hex("80B9DD");
                case "c_su7": return ArtDirection.Hex("A0D7D2");
                case "c_718": return ArtDirection.Hex("ECE1CB");
                case "c_921": return ArtDirection.Hex("666973");
                case "c_lambo": return ArtDirection.Hex("A7E51F");
                case "c_fa": return ArtDirection.Hex("D73E38");
                default: return ArtDirection.Sage;
            }
        }
        public static Transform Build(Transform parent,MotorModel model,Color color,bool includeRider=true)
        {
            if(MotorCatalog.ClassOf(model.Kind)==2)
            {
                var car=Prims.ParkedCar(parent,Vector3.zero,color).transform;car.localRotation=Quaternion.identity;
                if(model.Price>=500000){car.localScale=new Vector3(1.12f,.75f,1.05f);Prims.Box(car,"RearWing",new Vector3(0,1.2f,-1.35f),new Vector3(1.9f,.12f,.45f),Mats.Lit(ArtDirection.Ink));}
                if(model.Kind==VehicleKind.Van)Prims.Box(car,"CargoCabin",new Vector3(0,1.3f,-.6f),new Vector3(1.5f,1.2f,2.2f),Mats.Lit(color));
                return car;
            }
            var root=new GameObject(model.Name).transform;root.SetParent(parent,false);bool three=model.Kind==VehicleKind.Tricycle;
            Prims.Box(root,"Frame",new Vector3(0,.5f,0),new Vector3(.45f,.3f,1.7f),Mats.Lit(color));
            Prims.Box(root,"Seat",new Vector3(0,.8f,-.35f),new Vector3(.55f,.16f,.7f),Mats.Lit(ArtDirection.Ink));
            Prims.Box(root,"Handlebar",new Vector3(0,1.1f,.65f),new Vector3(.9f,.12f,.12f),Mats.Lit(ArtDirection.Ink));
            for(int i=0;i<(three?3:2);i++)
            {
                var wheel=Prims.Shape(PrimitiveType.Cylinder,root,"Wheel",new Vector3(three&&i>0?(i==1?-.65f:.65f):0,.3f,i==0?.7f:-.7f),new Vector3(.58f,.12f,.58f),Mats.Lit(ArtDirection.Ink));wheel.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            if(three)Prims.Box(root,"CargoBed",new Vector3(0,.7f,-.8f),new Vector3(1.4f,.4f,1),Mats.Lit(color));
            if(!three)
            {
                Prims.Box(root,"FrontShield",new Vector3(0,.72f,.55f),new Vector3(.52f,.66f,.22f),Mats.Lit(color));
                Prims.Shape(PrimitiveType.Sphere,root,"Headlight",new Vector3(0,1.04f,.71f),Vector3.one*.23f,Mats.Lit(ArtDirection.Cream));
                if(model.Id=="e_range")Prims.Box(root,"StorageBox",new Vector3(0,1.02f,-.78f),new Vector3(.65f,.4f,.5f),Mats.Lit(color));
                if(model.Id=="e_dash"||model.Id=="e_race")Prims.Box(root,"Windscreen",new Vector3(0,1.27f,.63f),new Vector3(.46f,.42f,.08f),Mats.Lit(ArtDirection.Ink));
                if(model.Id=="e_turn")Prims.Box(root,"CreamTrim",new Vector3(0,.75f,.68f),new Vector3(.22f,.5f,.02f),Mats.Lit(ArtDirection.Cream));
            }
            if(!includeRider)return root;
            Prims.Box(root,"RiderJacket",new Vector3(0,1.15f,-.15f),new Vector3(.5f,.7f,.35f),Mats.Lit(ArtDirection.Ink));
            Prims.Shape(PrimitiveType.Sphere,root,"Helmet",new Vector3(0,1.7f,-.1f),Vector3.one*.48f,Mats.Lit(ArtDirection.Cream));return root;
        }
    }
}
