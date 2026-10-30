using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;

namespace PCShopSell.Game
{
    /// <summary>家具摆放模式：在家里或装修中的店里，拾起 / 摆放 / 旋转 / 收进背包。</summary>
    public sealed partial class GameController
    {
        private bool _placeShop;
        private bool _placeHeld;
        private string _placeId;
        private int _placeIndex = -1;
        private int _placeRot;
        private GameObject _ghost;
        private PlacementOutline _ghostOutline;
        private bool _ghostValid;
        private PlacedFurniture _ghostAt;

        /// <summary>正在摆放家具。</summary>
        public bool Placing { get; private set; }

        /// <summary>是在店里摆放（否则是在家里）。</summary>
        public bool PlacingShop => _placeShop;

        /// <summary>手里拿着的家具（来自背包或从屋里拾起的）；没有则为空。</summary>
        public string PlaceId => _placeId;

        /// <summary>手里这件是屋里原来就有的（可以收进背包）。</summary>
        public bool PlaceHoldsPlaced => _placeId != null && _placeIndex >= 0;

        /// <summary>当前这个位置摆不了的原因；能摆则为空。</summary>
        public string PlaceError { get; private set; }

        private RoomSpec PlaceRoom => _placeShop ? Session.ShopRoom : RoomSpec.Home;
        private Vector3 PlaceOrigin => _placeShop ? ShopInteriorView.Origin : HomeRoomView.Origin;
        private IReadOnlyList<PlacedFurniture> PlaceLayout => _placeShop ? Session.ShopLayout : Session.HomeLayout;
        private RoomFurniture PlaceVisuals => _placeShop ? Interior.Furniture : HomeRoom.Furniture;

        /// <summary>开始摆放。店里必须已经歇业装修（由界面先确认并调用 <c>BeginRenovation</c>）。</summary>
        public bool BeginPlacement(bool shop)
        {
            if (Placing || Session.IsGameOver) return false;
            if (shop ? !InInterior || !Session.IsRenovating : !InHome) return false;
            Ui.CloseAllWindows();
            _placeShop = shop;
            Session.BeginLayoutHistory(shop);
            Placing = true;
            _placeId = null;
            _placeIndex = -1;
            PlaceError = null;
            EnterEditView();
            if (!_placeHeld)
            {
                Session.Time.Hold();
                _placeHeld = true;
            }
            Ui.ShowPlacementPanel();
            return true;
        }

        public void EndPlacement()
        {
            if (!Placing) return;
            CancelGhost();
            Placing = false;
            Session.EndLayoutHistory();
            LeaveEditView();
            if (_placeHeld)
            {
                if (Session != null && Session.Time.IsHeld) Session.Time.Release();
                _placeHeld = false;
            }
            Ui.HidePlacementPanel();
            if (InInterior && Session.IsRenovating)
                Ui.Toast(Session.ShopFitted
                    ? "装修完了，今天店里歇业，明天正常开门。"
                    : $"店里还差：{Session.ShopFitGap()}。买来摆好，第二天才能开业。");
        }

        /// <summary>从背包里拿一件来摆。</summary>
        public void SelectStock(string id)
        {
            if (!Placing || Session.FurnitureInStock(id) <= 0) return;
            CancelGhost();
            _placeId = id;
            _placeIndex = -1;
            _placeRot = 0;
            MakeGhost();
            Ui.RefreshPlacementPanel();
        }

        public void UndoPlacement(bool redo = false)
        {
            if (!Placing) return;
            CancelGhost();
            if (redo) Session.RedoLayout(); else Session.UndoLayout();
            Ui.RefreshPlacementPanel();
        }

        public void RotateGhost()
        {
            if (!Placing || _placeId == null) return;
            FurnitureDef def = FurnitureCatalog.Get(_placeId);
            _placeRot = def.Slot == FurnitureSlot.Wall ? (_placeRot == 0 ? 1 : 0) : (_placeRot + 1) & 3;
        }

        /// <summary>把手里这件（屋里原有的）收进背包。</summary>
        public void StoreSelected()
        {
            if (!Placing || !PlaceHoldsPlaced) return;
            int index = _placeIndex;
            CancelGhost();
            Session.StoreFurniture(_placeShop, index);
            Ui.RefreshPlacementPanel();
        }

        private void MakeGhost()
        {
            FurnitureDef def = FurnitureCatalog.Get(_placeId);
            if (def == null) return;
            Transform parent = _placeShop ? Interior.Root : HomeRoom.Root;
            string shelfKind = _placeIndex >= 0 ? PlaceLayout[_placeIndex].Item : null;
            _ghost = FurnitureVisuals.Build(parent, def, false, shelfKind);
            foreach (Renderer r in _ghost.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghostOutline = PlacementOutline.Attach(_ghost);
            // Do not flash a preview at the room origin before the first pointer update.
            _ghost.SetActive(false);
            _ghostAt = null;
        }

        private void DestroyGhost()
        {
            if (_ghost != null)
            {
                _ghost.SetActive(false);
                Kill.It(_ghost);
            }
            _ghost = null;
            _ghostOutline = null;
            _ghostAt = null;
        }

        private void CancelGhost()
        {
            DestroyGhost();
            if (_placeIndex >= 0 && Placing) PlaceVisuals?.ShowAll();
            _placeId = null;
            _placeIndex = -1;
            PlaceError = null;
        }

        // ---------- 编辑模式（俯视图 + 网格 + 坐标） ----------

        private RoomEditOverlay _overlay;

        /// <summary>冒烟 / 调试：不用真鼠标，直接指定“鼠标”在房间里的位置（房间局部坐标）。</summary>
        public Vector3? DebugCursorLocal;

        /// <summary>鼠标现在指在房间里（不在界面上）。</summary>
        public bool EditHover { get; private set; }

        /// <summary>要显示的坐标点（房间局部，米）：手里有家具时是它吸附后的位置，否则是鼠标所在格。</summary>
        public Vector2 EditCursor { get; private set; }

        /// <summary>手里这件当前摆放的位置（没拿东西或鼠标不在房间里则为空）。</summary>
        public PlacedFurniture EditGhostAt => _ghostAt;

        /// <summary>编辑模式下的房间（不在摆放中为空）和它在世界里的原点。</summary>
        public RoomSpec EditRoom => Placing ? PlaceRoom : null;
        public Vector3 EditOrigin => PlaceOrigin;

        private void EnterEditView(bool snap = false)
        {
            _rig.SetEdit(true);
            _lighting?.SetEditView(true);
            RoomSpec room = PlaceRoom;
            float w = (float)room.Width, d = (float)room.Depth;
            // 右边有 390 像素宽的摆放面板，房间要整个放进剩下的地方里
            float usable = Mathf.Max(0.5f, 1f - 390f / Mathf.Max(1f, Ui.CanvasWidth));
            // 斜着看时进深在屏幕上缩成 sin(俯角)，再加上后墙（3 米高）露出来的那一截
            float pitch = CameraRig.EditPitch * Mathf.Deg2Rad;
            float vert = d * Mathf.Sin(pitch) + 3f * Mathf.Cos(pitch) + 2.2f;
            float size = Mathf.Max(vert / 2f, (w + 2.4f) / (2f * Cam.aspect * usable));
            _rig.MinSize = 2.5f;
            _rig.MaxSize = size * 1.6f;
            // 平时是镜头缓缓抬起来转到俯视；换场景重设镜头时才一步到位
            if (snap) _rig.Snap(EditFocus(size), size);
            else _rig.Size = size;
            _overlay?.Destroy();
            _overlay = new RoomEditOverlay(PlaceOrigin, room, _placeShop ? Interior.FixedBlockers() : null);
        }

        private void LeaveEditView()
        {
            _overlay?.Destroy();
            _overlay = null;
            EditHover = false;
            _rig.SetEdit(false);
            _lighting?.SetEditView(false);
            SnapCamera(true);
        }

        /// <summary>编辑镜头看的点：房间正中，再往右挪一点，让房间落在摆放面板左边的空地中央。</summary>
        private Vector3 EditFocus(float size)
        {
            RoomSpec room = PlaceRoom;
            Vector3 center = PlaceOrigin + new Vector3((float)room.Width / 2f, 0, (float)room.Depth / 2f);
            // 摆放面板（约 390 个界面单位宽）占了右边，房间整体往左挪半个面板宽
            center.x += 2f * size * Cam.aspect * (195f / Mathf.Max(1f, Ui.CanvasWidth));
            center.y = 0.8f;
            return center;
        }

        /// <summary>鼠标指到地面上的哪一点（房间局部坐标）。</summary>
        private bool TryFloorPoint(out Vector3 local)
        {
            if (DebugCursorLocal.HasValue)
            {
                local = DebugCursorLocal.Value;
                return true;
            }
            local = Vector3.zero;
            if (Cam == null) return false;
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, PlaceOrigin);
            if (!plane.Raycast(ray, out float dist)) return false;
            local = ray.GetPoint(dist) - PlaceOrigin;
            return true;
        }

        /// <summary>占地框（墙面家具画成贴墙的一条，比真实占地厚一点，看得见也点得到）。</summary>
        private static RectD EditRect(RoomSpec room, FurnitureDef def, PlacedFurniture p)
        {
            RectD f = RoomPlanner.Footprint(room, def, p);
            if (def.Slot != FurnitureSlot.Wall) return f;
            return p.Rot == 1
                ? new RectD(room.Width - 0.45, f.MinZ, room.Width, f.MaxZ)
                : new RectD(f.MinX, room.Depth - 0.45, f.MaxX, room.Depth);
        }

        private void UpdateEditOverlay()
        {
            RoomSpec room = PlaceRoom;
            EditHover = false;
            Vector3 local = default;
            bool have = (DebugCursorLocal.HasValue || !GameUi.PointerOverUi) && TryFloorPoint(out local);
            if (have && local.x > -0.3f && local.x < room.Width + 0.3f && local.z > -0.3f && local.z < room.Depth + 0.3f)
            {
                EditHover = true;
                double g = RoomPlanner.Grid;
                EditCursor = new Vector2(
                    (float)(System.Math.Round(Mathf.Clamp(local.x, 0, (float)room.Width) / g) * g),
                    (float)(System.Math.Round(Mathf.Clamp(local.z, 0, (float)room.Depth) / g) * g));
            }

            RectD? foot = null;
            bool valid = true;
            if (_placeId != null && _ghost != null && _ghost.activeSelf && _ghostAt != null)
            {
                FurnitureDef def = FurnitureCatalog.Get(_placeId);
                foot = EditRect(room, def, _ghostAt);
                valid = PlaceError == null;
                EditCursor = def.Slot == FurnitureSlot.Wall
                    ? (_ghostAt.Rot == 1 ? new Vector2((float)room.Width, (float)_ghostAt.Z) : new Vector2((float)_ghostAt.X, (float)room.Depth))
                    : new Vector2((float)_ghostAt.X, (float)_ghostAt.Z);
                EditHover = true;
            }
            _overlay?.Update(EditHover ? EditCursor : (Vector2?)null, foot, valid, _rig.Size);
        }

        /// <summary>每帧：摆放模式下处理键盘和鼠标。</summary>
        private void UpdatePlacement()
        {
            if (!Placing) return;
            UpdatePlacementCore();
            if (Placing) UpdateEditOverlay();
        }

        private void UpdatePlacementCore()
        {
            if (!Placing) return;
            bool wrongRoom = _placeShop ? !InInterior : !InHome;
            if (wrongRoom || Session.IsGameOver || (_placeShop && !Session.IsRenovating))
            {
                EndPlacement();
                return;
            }

            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Z)) { UndoPlacement(); return; }
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Y)) { UndoPlacement(true); return; }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_placeId != null)
                {
                    CancelGhost();
                    Ui.RefreshPlacementPanel();
                }
                else
                {
                    EndPlacement();
                }
                return;
            }
            if (_placeId != null && Input.GetKeyDown(KeyCode.R)) RotateGhost();
            if (PlaceHoldsPlaced && (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Delete))) StoreSelected();

            if (_placeId == null)
            {
                if (Input.GetMouseButtonDown(0) && !GameUi.PointerOverUi) TryPickUp();
                return;
            }

            FurnitureDef def = FurnitureCatalog.Get(_placeId);
            if (Input.GetMouseButtonDown(1) && !GameUi.PointerOverUi)
            {
                CancelGhost();
                Ui.RefreshPlacementPanel();
                return;
            }

            bool over = GameUi.PointerOverUi && !DebugCursorLocal.HasValue;
            if (!over && TryPointerOnRoom(def, out Vector3 local))
            {
                PlacedFurniture at = RoomPlanner.Snap(PlaceRoom, def, local.x, local.z, _placeRot);
                _ghostAt = at;
                FurnitureVisuals.Place(_ghost, def, at, PlaceRoom);
                _ghost.SetActive(true);
                PlaceError = Session.PlacementError(_placeShop, def, at, _placeIndex, _placeShop ? Interior.FixedBlockers() : null);
                _ghostValid = PlaceError == null;
                _ghostOutline.SetValid(_ghostValid);

                if (Input.GetMouseButtonDown(0) && _ghostValid) ConfirmPlace(def, at);
            }
            else if (_ghost != null)
            {
                _ghost.SetActive(false);
                _ghostAt = null;
                PlaceError = null;
            }
        }

        private void ConfirmPlace(FurnitureDef def, PlacedFurniture at)
        {
            IReadOnlyList<RectD> extra = _placeShop ? Interior.FixedBlockers() : null;
            bool ok;
            bool keep = false;
            if (_placeIndex >= 0)
            {
                ok = Session.MoveFurniture(_placeShop, _placeIndex, at, extra);
            }
            else
            {
                ok = Session.PlaceFromStock(_placeShop, def.Id, at, extra);
                keep = ok && Session.FurnitureInStock(def.Id) > 0;
            }
            if (!ok) return;
            if (keep)
            {
                _ghostAt = null;
            }
            else
            {
                if (_placeIndex >= 0) PlaceVisuals.ShowAll();
                DestroyGhost();
                _placeId = null;
                _placeIndex = -1;
                PlaceError = null;
            }
            Ui.RefreshPlacementPanel();
        }

        /// <summary>鼠标射线打到后墙 / 右墙的墙面上（离地 0.4~3.2 米）：找挂在那一段墙上的家具（海报、灯屏等没有碰撞体，只能这样点）。</summary>
        private int PickWallFurniture(Ray ray, IReadOnlyList<PlacedFurniture> layout)
        {
            RoomSpec room = PlaceRoom;
            Vector3 o = ray.origin - PlaceOrigin;
            Vector3 dir = ray.direction;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int wall = 0; wall < 2; wall++)
            {
                // wall 0 = 后墙（z = 进深），wall 1 = 右墙（x = 宽）
                float planeAt = wall == 0 ? (float)room.Depth : (float)room.Width;
                float o1 = wall == 0 ? o.z : o.x, d1 = wall == 0 ? dir.z : dir.x;
                if (Mathf.Abs(d1) < 1e-4f) continue;
                float t = (planeAt - o1) / d1;
                if (t < 0) continue;
                Vector3 p = o + dir * t;
                if (p.y < 0.4f || p.y > 3.2f) continue;
                float along = wall == 0 ? p.x : p.z;
                for (int i = 0; i < layout.Count; i++)
                {
                    FurnitureDef d = FurnitureCatalog.Get(layout[i].Id);
                    if (d == null || d.Slot != FurnitureSlot.Wall || layout[i].Rot != wall) continue;
                    RectD r = EditRect(room, d, layout[i]);
                    float lo = (float)(wall == 0 ? r.MinX : r.MinZ) - 0.1f, hi = (float)(wall == 0 ? r.MaxX : r.MaxZ) + 0.1f;
                    if (along < lo || along > hi) continue;
                    float mid = Mathf.Abs(along - (lo + hi) / 2f);
                    if (t + mid * 0.01f < bestDist)
                    {
                        bestDist = t + mid * 0.01f;
                        best = i;
                    }
                }
            }
            return best;
        }

        /// <summary>冒烟 / 调试：从镜头朝房间里某点（房间局部坐标）看过去，点到的墙面家具编号（-1 = 没有）。</summary>
        public int DebugPickWallAt(Vector3 local)
        {
            Vector3 world = PlaceOrigin + local;
            Ray ray = Cam.ScreenPointToRay(Cam.WorldToScreenPoint(world));
            return PickWallFurniture(ray, PlaceLayout);
        }

        /// <summary>冒烟 / 调试：编辑模式下转视角。</summary>
        public void DebugOrbit(float dYaw, float dPitch) => _rig.Orbit(dYaw, dPitch);

        /// <summary>点屋里已经摆好的家具：拿起来挪。</summary>
        private void TryPickUp()
        {
            IReadOnlyList<PlacedFurniture> layout = PlaceLayout;
            int index = -1;
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                var pv = hit.collider.GetComponentInParent<PlacedVisual>();
                if (pv != null && pv.Index >= 0 && pv.Index < layout.Count)
                {
                    index = pv.Index;
                    break;
                }
            }
            if (index < 0) index = PickWallFurniture(ray, layout);
            if (index < 0 && TryFloorPoint(out Vector3 local))
            {
                // 俯视图里墙面家具只剩一条边、点不到：按占地框来找
                RoomSpec room = PlaceRoom;
                double best = double.MaxValue;
                for (int i = 0; i < layout.Count; i++)
                {
                    FurnitureDef d = FurnitureCatalog.Get(layout[i].Id);
                    if (d == null) continue;
                    RectD r = EditRect(room, d, layout[i]);
                    if (local.x < r.MinX || local.x > r.MaxX || local.z < r.MinZ || local.z > r.MaxZ) continue;
                    double area = (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ);
                    if (area < best)
                    {
                        best = area;
                        index = i;
                    }
                }
            }
            if (index < 0) return;
            PickUpFurniture(index);
        }

        private void PickUpFurniture(int index)
        {
            PlacedFurniture p = PlaceLayout[index];
            _placeId = p.Id;
            _placeIndex = index;
            _placeRot = p.Rot;
            PlaceVisuals.SetHidden(index, true);
            MakeGhost();
            Ui.RefreshPlacementPanel();
        }

        /// <summary>鼠标指到房间里的哪一点（房间局部坐标）。墙面家具指到墙上。</summary>
        private bool TryPointerOnRoom(FurnitureDef def, out Vector3 local)
        {
            if (!TryFloorPoint(out local)) return false;
            if (def.Slot == FurnitureSlot.Wall)
            {
                // 俯视图里直接按鼠标在地面上的位置：离后墙近就挂后墙，离右墙近就挂右墙
                RoomSpec room = PlaceRoom;
                _placeRot = room.Depth - local.z <= room.Width - local.x ? 0 : 1;
            }
            return true;
        }
    }
}
