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
        private readonly List<Renderer> _ghostRenderers = new List<Renderer>();
        private bool _ghostValid;
        private Material _ghostOk, _ghostBad;
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
            Placing = true;
            _placeId = null;
            _placeIndex = -1;
            PlaceError = null;
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
            if (_placeHeld)
            {
                if (Session != null && Session.Time.IsHeld) Session.Time.Release();
                _placeHeld = false;
            }
            Ui.HidePlacementPanel();
            if (InInterior && Session.IsRenovating) Ui.Toast("装修完了，今天店里歇业，明天正常开门。");
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
            if (_ghostOk == null)
            {
                _ghostOk = Mats.Unlit(new Color(0.3f, 0.9f, 0.45f));
                _ghostBad = Mats.Unlit(new Color(0.95f, 0.3f, 0.25f));
            }
            Transform parent = _placeShop ? Interior.Root : HomeRoom.Root;
            _ghost = FurnitureVisuals.Build(parent, def, false);
            _ghostRenderers.Clear();
            _ghostRenderers.AddRange(_ghost.GetComponentsInChildren<Renderer>());
            foreach (Renderer r in _ghostRenderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.sharedMaterial = _ghostOk;
            }
            _ghostAt = null;
        }

        private void CancelGhost()
        {
            if (_ghost != null) Kill.It(_ghost);
            _ghost = null;
            _ghostRenderers.Clear();
            if (_placeIndex >= 0 && Placing) PlaceVisuals?.ShowAll();
            _placeId = null;
            _placeIndex = -1;
            PlaceError = null;
        }

        /// <summary>每帧：摆放模式下处理键盘和鼠标。</summary>
        private void UpdatePlacement()
        {
            if (!Placing) return;
            bool wrongRoom = _placeShop ? !InInterior : !InHome;
            if (wrongRoom || Session.IsGameOver || (_placeShop && !Session.IsRenovating))
            {
                EndPlacement();
                return;
            }

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

            bool over = GameUi.PointerOverUi;
            if (!over && TryPointerOnRoom(def, out Vector3 local))
            {
                PlacedFurniture at = RoomPlanner.Snap(PlaceRoom, def, local.x, local.z, _placeRot);
                _ghostAt = at;
                FurnitureVisuals.Place(_ghost, def, at, PlaceRoom);
                _ghost.SetActive(true);
                PlaceError = Session.PlacementError(_placeShop, def, at, _placeIndex, _placeShop ? Interior.FixedBlockers() : null);
                _ghostValid = PlaceError == null;
                Material m = _ghostValid ? _ghostOk : _ghostBad;
                foreach (Renderer r in _ghostRenderers) r.sharedMaterial = m;

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
                if (_ghost != null) Kill.It(_ghost);
                _ghost = null;
                _ghostRenderers.Clear();
                _placeId = null;
                _placeIndex = -1;
                PlaceError = null;
            }
            Ui.RefreshPlacementPanel();
        }

        /// <summary>点屋里已经摆好的家具：拿起来挪。</summary>
        private void TryPickUp()
        {
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;
            var pv = hit.collider.GetComponentInParent<PlacedVisual>();
            if (pv == null) return;
            IReadOnlyList<PlacedFurniture> layout = PlaceLayout;
            if (pv.Index < 0 || pv.Index >= layout.Count) return;
            PlacedFurniture p = layout[pv.Index];
            _placeId = p.Id;
            _placeIndex = pv.Index;
            _placeRot = p.Rot;
            PlaceVisuals.SetHidden(pv.Index, true);
            MakeGhost();
            Ui.RefreshPlacementPanel();
        }

        /// <summary>鼠标指到房间里的哪一点（房间局部坐标）。墙面家具指到墙上。</summary>
        private bool TryPointerOnRoom(FurnitureDef def, out Vector3 local)
        {
            local = Vector3.zero;
            if (Cam == null) return false;
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            Vector3 origin = PlaceOrigin;
            RoomSpec room = PlaceRoom;
            Plane plane;
            if (def.Slot == FurnitureSlot.Wall)
                plane = _placeRot == 1
                    ? new Plane(Vector3.left, origin + new Vector3((float)room.Width, 0, 0))
                    : new Plane(Vector3.back, origin + new Vector3(0, 0, (float)room.Depth));
            else
                plane = new Plane(Vector3.up, origin);
            if (!plane.Raycast(ray, out float dist)) return false;
            local = ray.GetPoint(dist) - origin;
            return true;
        }
    }
}
