using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>挂在门上，点击时触发"出门"。</summary>
    public sealed class HomeFurniture : MonoBehaviour
    {
        public string Id;
    }

    /// <summary>
    /// 开局住的城中村出租屋（一间真实的小屋）：又破又旧。屋里的家具全部来自 <c>GameSession.HomeLayout</c>（可以摆放、更换），
    /// 这里只负责墙、地、窗、门、灯，以及按布局摆出家具、走路碰撞和"够得着的互动点"（床、衣柜、电饭锅、门）。
    /// </summary>
    public sealed class HomeRoomView
    {
        public const string Bed = "bed";
        public const string Wardrobe = "wardrobe";
        public const string Cooker = "cooker";
        public const string Seat = "seat";
        public const string Repair = "repair";
        public const string Storage = "storage";
        public const string Door = "door";

        public static readonly Vector3 Origin = new Vector3(600, 0, -470);
        public const float Width = 6f;
        public const float Depth = 4.6f;
        public const float DefaultZoom = 4.2f;
        public const float MinZoom = 2.8f;
        /// <summary>刚好看到整间屋子的镜头大小（缩放上限）。</summary>
        public static float CameraSize => Mathf.Max(Width, Depth) * 0.62f + 1.5f;

        /// <summary>家具的玩法角色对应的互动 ID。</summary>
        public static string RoleId(FurnitureRole role)
        {
            switch (role)
            {
                case FurnitureRole.Bed: return Bed;
                case FurnitureRole.Wardrobe: return Wardrobe;
                case FurnitureRole.Cooker: return Cooker;
                case FurnitureRole.Seat: return Seat;
                case FurnitureRole.Broken: return Repair;
                case FurnitureRole.Storage: return Storage;
                default: return null;
            }
        }

        private struct Spot
        {
            public string Id;
            public string Label;
            public Rect Area;
            public float Height;
        }

        private readonly Transform _root;
        private readonly GameObject _owner;
        private readonly List<Spot> _spots = new List<Spot>();
        private readonly Material _windowMat;
        private readonly Light _windowLight;
        private readonly RoomFurniture _furniture;
        private readonly Rect _doorArea = new Rect(Width - 0.2f, 0.5f, 0.2f, 1.0f);
        private bool _wasOn;
        private string _dressed;
        private GameObject _repairman;
        private Vector3 _repairWork;
        private bool _repairmanIn;
        private bool _sitting;
        private Vector3 _standPos;
        private Rect _seatArea;

        /// <summary>老板脚边最近的、够得着的互动点 ID；没有则为空。</summary>
        public string NearbyId { get; private set; }

        public string NearbyLabel { get; private set; }

        /// <summary>最近互动点头顶的世界坐标（给"可以交互"提示用）。</summary>
        public Vector3 NearbyWorld { get; private set; }

        public RoomFurniture Furniture => _furniture;
        public Transform Root => _root;
        public Vector3 Center => Origin + new Vector3(Width / 2, 0, Depth / 2);
        public Vector3 OwnerWorld => _owner.transform.position;

        public HomeRoomView(Transform parent)
        {
            _root = new GameObject("HomeRoom").transform;
            _root.SetParent(parent, false);
            _root.position = Origin;
            float w = Width, d = Depth;

            // ---------- 地面、墙 ----------
            Prims.Box(_root, "Floor", new Vector3(w / 2, -0.05f, d / 2), new Vector3(w, 0.1f, d), Mats.LitTex(new Color(0.52f, 0.47f, 0.41f), new Vector2(4, 3), 0.05f));
            Prims.Box(_root, "Outside", new Vector3(w / 2, -0.1f, d / 2), new Vector3(w + 30, 0.05f, d + 30), Mats.Lit(new Color(0.1f, 0.1f, 0.12f)));
            Stain("FloorStain1", new Vector3(2.9f, 0.007f, 2.1f), new Vector3(1.1f, 0.01f, 0.7f), new Color(0.40f, 0.36f, 0.31f), 20f);
            Stain("FloorStain2", new Vector3(4.8f, 0.007f, 3.0f), new Vector3(0.7f, 0.01f, 0.5f), new Color(0.43f, 0.39f, 0.33f), -30f);
            Stain("Crack1", new Vector3(3.6f, 0.008f, 1.0f), new Vector3(1.6f, 0.01f, 0.03f), new Color(0.25f, 0.22f, 0.2f), 25f);
            Stain("Crack2", new Vector3(1.4f, 0.008f, 2.5f), new Vector3(1.0f, 0.01f, 0.03f), new Color(0.25f, 0.22f, 0.2f), -50f);

            Color wallUpper = new Color(0.76f, 0.72f, 0.58f);
            Color wallDado = new Color(0.43f, 0.54f, 0.47f);
            Prims.Box(_root, "WallBack", new Vector3(w / 2, 1.5f, d + 0.05f), new Vector3(w + 0.2f, 3f, 0.1f), Mats.LitTex(wallUpper, new Vector2(3, 1), 0.05f));
            Prims.Box(_root, "WallRight", new Vector3(w + 0.05f, 1.5f, d / 2), new Vector3(0.1f, 3f, d + 0.2f), Mats.LitTex(wallUpper * 0.93f, new Vector2(3, 1), 0.05f));
            Prims.Box(_root, "DadoBack", new Vector3(w / 2, 0.55f, d - 0.01f), new Vector3(w, 1.1f, 0.02f), Mats.Lit(wallDado, 0.05f));
            Prims.Box(_root, "DadoRight", new Vector3(w - 0.01f, 0.55f, d / 2), new Vector3(0.02f, 1.1f, d), Mats.Lit(wallDado * 0.93f, 0.05f));
            Prims.Box(_root, "WallLeftLow", new Vector3(-0.05f, 0.3f, d / 2), new Vector3(0.1f, 0.6f, d), Mats.Lit(new Color(0.6f, 0.58f, 0.52f)));
            Prims.Box(_root, "WallFrontLow", new Vector3(w / 2, 0.3f, -0.05f), new Vector3(w, 0.6f, 0.1f), Mats.Lit(new Color(0.6f, 0.58f, 0.52f)));
            // 墙上的水渍、掉皮、霉斑
            Prims.Box(_root, "LeakStain", new Vector3(4.7f, 2.45f, d - 0.014f), new Vector3(0.9f, 0.8f, 0.01f), Mats.Lit(new Color(0.62f, 0.56f, 0.40f), 0.05f));
            Prims.Box(_root, "LeakStain2", new Vector3(4.7f, 2.75f, d - 0.016f), new Vector3(0.5f, 0.3f, 0.01f), Mats.Lit(new Color(0.55f, 0.49f, 0.34f), 0.05f));
            Prims.Box(_root, "Peeling", new Vector3(1.0f, 1.55f, d - 0.014f), new Vector3(0.6f, 0.5f, 0.01f), Mats.Lit(new Color(0.84f, 0.81f, 0.7f), 0.05f));
            Prims.Box(_root, "Mold", new Vector3(w - 0.014f, 2.3f, 1.9f), new Vector3(0.01f, 0.7f, 0.9f), Mats.Lit(new Color(0.48f, 0.5f, 0.4f), 0.05f));
            Prims.Box(_root, "Calendar", new Vector3(5.3f, 1.55f, d - 0.02f), new Vector3(0.32f, 0.45f, 0.02f), Mats.Lit(new Color(0.8f, 0.25f, 0.2f)));
            // 墙上钉的一根晾衣绳
            Prims.Box(_root, "Line", new Vector3(w * 0.5f, 2.1f, Depth - 0.06f), new Vector3(2.0f, 0.015f, 0.015f), Mats.Lit(new Color(0.2f, 0.2f, 0.2f)));

            // ---------- 灯 ----------
            Prims.Box(_root, "BulbWire", new Vector3(w * 0.5f, 2.78f, d * 0.5f), new Vector3(0.02f, 0.44f, 0.02f), Mats.Lit(new Color(0.1f, 0.1f, 0.1f)));
            Prims.Shape(PrimitiveType.Sphere, _root, "Bulb", new Vector3(w * 0.5f, 2.5f, d * 0.5f), Vector3.one * 0.17f, Mats.Glow(new Color(1f, 0.95f, 0.7f), new Color(4f, 3.4f, 2f), false));
            var bulbGo = new GameObject("BulbLight");
            bulbGo.transform.SetParent(_root, false);
            bulbGo.transform.localPosition = new Vector3(w * 0.5f, 2.3f, d * 0.5f);
            var bulb = bulbGo.AddComponent<Light>();
            bulb.type = LightType.Point;
            bulb.range = 8f;
            bulb.intensity = 1.1f;
            bulb.color = new Color(1f, 0.84f, 0.58f);

            // ---------- 窗（带铁栏杆） ----------
            Prims.Box(_root, "WindowFrame", new Vector3(3.15f, 1.75f, d - 0.02f), new Vector3(1.15f, 1.05f, 0.04f), Mats.Lit(new Color(0.25f, 0.27f, 0.3f), 0.3f, 0.3f));
            GameObject pane = Prims.Box(_root, "WindowPane", new Vector3(3.15f, 1.75f, d - 0.045f), new Vector3(1.0f, 0.9f, 0.02f), Mats.Glow(new Color(0.3f, 0.35f, 0.4f), new Color(0.2f, 0.25f, 0.35f), false));
            _windowMat = new Material(pane.GetComponent<Renderer>().sharedMaterial);
            pane.GetComponent<Renderer>().sharedMaterial = _windowMat;
            for (int i = 0; i < 4; i++)
                Prims.Box(_root, "Bar", new Vector3(2.8f + i * 0.23f, 1.75f, d - 0.09f), new Vector3(0.03f, 0.95f, 0.03f), Mats.Lit(new Color(0.18f, 0.18f, 0.2f), 0.3f, 0.5f));
            Prims.Box(_root, "BarCross", new Vector3(3.15f, 1.75f, d - 0.09f), new Vector3(1.05f, 0.03f, 0.03f), Mats.Lit(new Color(0.18f, 0.18f, 0.2f), 0.3f, 0.5f));
            var wl = new GameObject("WindowLight");
            wl.transform.SetParent(_root, false);
            wl.transform.localPosition = new Vector3(3.15f, 1.7f, d - 0.7f);
            _windowLight = wl.AddComponent<Light>();
            _windowLight.type = LightType.Point;
            _windowLight.range = 5f;
            _windowLight.color = new Color(0.7f, 0.82f, 1f);
            _windowLight.intensity = 0f;

            BuildDoor();

            _furniture = new RoomFurniture(_root);
            _owner = Prims.Figure(_root, "Owner", new Color(0.95f, 0.55f, 0.15f), new Color(0.95f, 0.8f, 0.65f), 1.05f);
            ResetOwner();
        }

        private void Stain(string name, Vector3 pos, Vector3 size, Color c, float yaw)
        {
            GameObject go = Prims.Box(_root, name, pos, size, Mats.Lit(c, 0.02f));
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        private GameObject Piece(string id, string name, Vector3 center, Vector3 size, Color c, float smooth = 0.1f, float metallic = 0f)
        {
            GameObject go = Prims.Box(_root, name, center, size, Mats.Lit(c, smooth, metallic), id != null);
            if (id != null) go.AddComponent<HomeFurniture>().Id = id;
            return go;
        }

        private void BuildDoor()
        {
            float w = Width;
            Piece(null, "DoorFrame", new Vector3(w - 0.03f, 1.05f, 1.0f), new Vector3(0.05f, 2.1f, 1.05f), new Color(0.3f, 0.22f, 0.15f));
            Piece(Door, "Door", new Vector3(w - 0.07f, 1.0f, 1.0f), new Vector3(0.06f, 2.0f, 0.9f), new Color(0.42f, 0.3f, 0.2f), 0.1f);
            Piece(null, "DoorPeel", new Vector3(w - 0.108f, 0.7f, 0.8f), new Vector3(0.01f, 0.6f, 0.35f), new Color(0.55f, 0.42f, 0.3f));
            Piece(null, "DoorHandle", new Vector3(w - 0.12f, 1.0f, 0.65f), new Vector3(0.05f, 0.05f, 0.1f), new Color(0.75f, 0.7f, 0.5f), 0.5f, 0.6f);
            Prims.Box(_root, "ExitMark", new Vector3(w - 0.11f, 2.3f, 1.0f), new Vector3(0.01f, 0.2f, 0.5f), Mats.Glow(new Color(0.1f, 0.4f, 0.2f), new Color(0.4f, 2f, 0.9f), false));
        }

        // ---------- 家具与互动点 ----------

        /// <summary>按当前布局重建家具，并重新算互动点。</summary>
        private void SyncFurniture(GameSession s)
        {
            if (!_furniture.Sync(s.HomeLayout, RoomSpec.Home)) return;
            _spots.Clear();
            bool hasBed = false;
            foreach (PlacedFurniture p in s.HomeLayout)
            {
                FurnitureDef def = FurnitureCatalog.Get(p.Id);
                string id = def == null ? null : RoleId(def.Role);
                if (id == null) continue;
                if (def.Role == FurnitureRole.Bed) hasBed = true;
                RectD f = RoomPlanner.Footprint(RoomSpec.Home, def, p);
                string label = def.Role == FurnitureRole.Bed ? "睡觉 / 小睡" : def.Role == FurnitureRole.Wardrobe ? "打开衣柜" : def.Role == FurnitureRole.Seat ? "坐下" : def.Role == FurnitureRole.Broken ? "坏了：约师傅上门修" : def.Role == FurnitureRole.Storage ? "纸箱：存取东西" : "电饭锅：做饭 / 吃饭";
                _spots.Add(new Spot
                {
                    Id = id,
                    Label = label,
                    Area = new Rect((float)f.MinX, (float)f.MinZ, (float)(f.MaxX - f.MinX), (float)(f.MaxZ - f.MinZ)),
                    Height = def.Role == FurnitureRole.Bed ? 1.1f : def.Role == FurnitureRole.Wardrobe ? 2.2f : def.Role == FurnitureRole.Seat || def.Role == FurnitureRole.Broken || def.Role == FurnitureRole.Storage ? 1.0f : 1.5f,
                });
            }
            // 没有床也能睡：打地铺
            if (!hasBed) _spots.Add(new Spot { Id = Bed, Label = "打地铺睡觉", Area = new Rect(2.2f, 2.0f, 1.6f, 0.6f), Height = 0.6f });
            _spots.Add(new Spot { Id = Door, Label = "出门，回到街上", Area = _doorArea, Height = 2.5f });
        }

        // ---------- 走动 ----------

        public bool IsSitting => _sitting;

        /// <summary>坐下 / 站起来：只是个动作，没有任何数值效果。坐下时挪到最近那个座位上，站起来回到原来站的地方。</summary>
        public void ToggleSit()
        {
            Transform tr = _owner.transform;
            if (_sitting)
            {
                StandUp();
                return;
            }
            if (NearbyId != Seat) return;
            _standPos = tr.localPosition;
            _sitting = true;
            Vector3 c = new Vector3(_seatArea.center.x, 0f, _seatArea.center.y);
            tr.localPosition = c;
            tr.localScale = new Vector3(1f, 0.72f, 1f);
            Vector3 toCenter = new Vector3(Width / 2 - c.x, 0, Depth / 2 - c.z);
            if (toCenter.sqrMagnitude > 1e-4f) tr.localRotation = Quaternion.LookRotation(toCenter);
        }

        private void StandUp()
        {
            if (!_sitting) return;
            _sitting = false;
            Transform tr = _owner.transform;
            tr.localScale = Vector3.one;
            tr.localPosition = _standPos;
        }

        public void ResetOwner()
        {
            _sitting = false;
            _owner.transform.localScale = Vector3.one;
            _owner.transform.localPosition = new Vector3(Width - 1.15f, 0, 1.3f);
            _owner.transform.localRotation = Quaternion.LookRotation(new Vector3(-1, 0, 0.3f));
        }

        public void SetActive(bool on)
        {
            _root.gameObject.SetActive(on);
            if (on && !_wasOn) ResetOwner();
            _wasOn = on;
        }

        /// <summary>WASD：按世界方向走一步，被家具挡住时沿边滑。</summary>
        public void MoveOwner(Vector3 worldDir, float realDt)
        {
            Vector3 d = new Vector3(worldDir.x, 0, worldDir.z);
            if (d.sqrMagnitude < 1e-4f) return;
            StandUp();
            d.Normalize();
            Transform tr = _owner.transform;
            Vector3 p = tr.localPosition;
            float step = 2.6f * realDt;
            Vector3 tx = p + new Vector3(d.x * step, 0, 0);
            if (CanStand(tx, p)) p = tx;
            Vector3 tz = p + new Vector3(0, 0, d.z * step);
            if (CanStand(tz, p)) p = tz;
            tr.localPosition = p;
            tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.LookRotation(d), 0.35f);
        }

        private bool CanStand(Vector3 to, Vector3 from)
        {
            if (to.x < 0.3f || to.x > Width - 0.3f || to.z < 0.3f || to.z > Depth - 0.3f) return false;
            if (!Blocked(to)) return true;
            return Blocked(from);
        }

        private bool Blocked(Vector3 p)
        {
            foreach (Rect r in _furniture.Blockers)
                if (r.Contains(new Vector2(p.x, p.z))) return true;
            return false;
        }

        private static float DistToRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public void Update(GameSession s, float realDt, float minuteOfDay)
        {
            if (_dressed != s.EquippedOutfit)
            {
                _dressed = s.EquippedOutfit;
                Prims.Dress(_owner, _dressed);
            }
            SyncFurniture(s);
            UpdateRepairman(s, realDt);

            // 窗外的天光：白天亮、夜里黑
            float hour = minuteOfDay / 60f;
            float day = Mathf.Clamp01(Mathf.Min(hour - 6f, 19f - hour) / 1.5f);
            _windowMat.SetColor("_EmissionColor", Color.Lerp(new Color(0.03f, 0.04f, 0.07f), new Color(1.3f, 1.55f, 1.9f), day));
            _windowLight.intensity = day * 0.9f;

            // 够得着的互动点
            Vector3 lp = _owner.transform.localPosition;
            Vector2 at = new Vector2(lp.x, lp.z);
            string best = null, label = null;
            Vector3 world = Vector3.zero;
            float bestDist = 0.75f;
            foreach (Spot sp in _spots)
            {
                float dd = DistToRect(at, sp.Area);
                if (dd < bestDist)
                {
                    bestDist = dd;
                    best = sp.Id;
                    label = sp.Label;
                    world = Origin + new Vector3(sp.Area.center.x, sp.Height, sp.Area.center.y);
                }
            }
            if (_sitting)
            {
                best = Seat;
                label = "站起来";
                world = _owner.transform.localPosition + new Vector3(0, 1.2f, 0) + Origin;
            }
            else if (best == Seat)
            {
                foreach (Spot sp in _spots)
                    if (sp.Id == Seat && DistToRect(at, sp.Area) <= bestDist + 1e-4f) _seatArea = sp.Area;
                label = "坐下";
            }
            if (best == Repair && s.RepairmanAtWork != null) label = "师傅正在修…";
            if (best == Door && s.LeaveBlocker != null) label = "师傅在修东西，暂时不能出门";
            NearbyId = best;
            NearbyLabel = label;
            NearbyWorld = world;
        }

        // ---------- 上门的师傅 ----------

        /// <summary>师傅进门干活：从门口走到坏掉的东西旁边，蹲着修一阵，修完走回门口离开。他不能交互。</summary>
        private void UpdateRepairman(GameSession s, float dt)
        {
            HomeVisit v = s.RepairmanAtWork;
            if (v == null)
            {
                if (_repairman != null) _repairman.SetActive(false);
                _repairmanIn = false;
                return;
            }
            Vector3 door = new Vector3(Width - 0.55f, 0, 1.0f);
            if (_repairman == null)
            {
                _repairman = Prims.Figure(_root, "Repairman", new Color(0.2f, 0.4f, 0.65f), new Color(0.9f, 0.75f, 0.6f), 1.05f);
                Prims.Shape(PrimitiveType.Sphere, _repairman.transform, "Hat", new Vector3(0, 1.45f, 0) * 1.05f, new Vector3(0.38f, 0.2f, 0.38f) * 1.05f, Mats.Lit(new Color(0.95f, 0.8f, 0.1f)));
                Prims.Box(_repairman.transform, "Toolbox", new Vector3(0.28f, 0.12f, 0.1f) * 1.05f, new Vector3(0.28f, 0.2f, 0.16f) * 1.05f, Mats.Lit(new Color(0.8f, 0.15f, 0.1f)));
            }
            if (!_repairmanIn)
            {
                _repairmanIn = true;
                _repairman.SetActive(true);
                _repairman.transform.localPosition = door;
                _repairman.transform.localScale = Vector3.one;
                _repairWork = new Vector3(Width / 2, 0, Depth / 2);
                foreach (Spot sp in _spots)
                    if (sp.Id == Repair)
                    {
                        // 站在坏东西朝屋里的那一侧
                        Vector3 c = new Vector3(sp.Area.center.x, 0, sp.Area.center.y);
                        Vector3 toRoom = new Vector3(Width / 2 - c.x, 0, Depth / 2 - c.z).normalized;
                        _repairWork = c + toRoom * (Mathf.Max(sp.Area.width, sp.Area.height) * 0.5f + 0.35f);
                    }
            }

            float progress = Mathf.Clamp01((float)(s.Clock.Now.TotalMinutes - v.WorkStart) / GameSession.RepairWorkMinutes);
            bool leaving = progress >= 0.9f;
            Vector3 target = leaving ? door : _repairWork;
            Transform tr = _repairman.transform;
            Vector3 delta = target - tr.localPosition;
            bool moving = delta.sqrMagnitude > 0.01f;
            if (moving)
            {
                tr.localPosition = Vector3.MoveTowards(tr.localPosition, target, 2.2f * dt);
                tr.localRotation = Quaternion.LookRotation(delta.normalized);
                tr.localScale = Vector3.one;
            }
            else if (!leaving)
            {
                // 蹲在东西前面敲敲打打
                tr.localScale = new Vector3(1f, 0.78f + 0.04f * Mathf.Sin(Time.time * 9f), 1f);
            }
        }

        public void Destroy() => Kill.It(_root.gameObject);
    }
}
