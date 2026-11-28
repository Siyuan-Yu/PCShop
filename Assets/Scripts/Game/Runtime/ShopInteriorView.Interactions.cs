using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class ShopInteriorView
    {
        private GameSession _interactionSession;
        private GameObject _selfCheckoutTerminal;
        private int _selfCheckoutLayoutVersion = -1;
        private Visitor _stagedPayer;
        private readonly List<GameObject> _salesGreeters = new();
        private readonly Queue<(OwnedShop Shop, Employee Keeper, PetCareAction Action)> _petCareInteractions = new();

        private void HookShopInteractions(GameSession session)
        {
            if (_interactionSession == session) return;
            if (_interactionSession != null)
            {
                _interactionSession.PetCounterHandled -= OnPetCounterHandled;
                _interactionSession.PetCarePerformed -= OnPetCarePerformed;
                _interactionSession.ShopStationWorked -= OnShopStationWorked;
            }
            _petCareInteractions.Clear();
            _interactionSession = session;
            if (session != null)
            {
                session.PetCounterHandled += OnPetCounterHandled;
                session.PetCarePerformed += OnPetCarePerformed;
                session.ShopStationWorked += OnShopStationWorked;
            }
        }

        private void OnPetCounterHandled(OwnedShop shop, bool intake, bool player)
        {
            if (shop == _interactionSession.CurrentShop)
                PlayCounterInteraction(_interactionSession, intake ? FigureInteraction.Register : FigureInteraction.Checkout, player);
        }

        private void OnPetCarePerformed(OwnedShop shop, Employee keeper, PetCareAction action)
        {
            if (shop == _interactionSession.CurrentShop && PetHousePresent)
                _petCareInteractions.Enqueue((shop, keeper, action));
        }

        private void UpdatePetCareInteraction(GameSession session)
        {
            if (_petCareInteractions.Count == 0) return;
            var (shop, keeper, action) = _petCareInteractions.Peek();
            if (shop != session.CurrentShop || !PetHousePresent)
            {
                _petCareInteractions.Dequeue();
                return;
            }
            var prop = _petCareProps[(int)action];
            GameObject actor = _owner;
            if (prop == null || keeper != null && !_staffFigures.TryGetValue(keeper.Id, out actor))
            {
                _petCareInteractions.Dequeue();
                return;
            }
            var motion = actor.GetComponent<FigureMotion>();
            if (motion.InteractionPlaying || keeper == null && PetActionPlaying) return;
            _petCareInteractions.Dequeue();
            var interaction = action switch
            {
                PetCareAction.Water => FigureInteraction.PetWater,
                PetCareAction.Litter => FigureInteraction.PetClean,
                _ => FigureInteraction.PetFeed
            };
            var target = prop.position + Vector3.up * (action == PetCareAction.Litter ? .20f : .29f);
            if (PlanarDistance(actor.transform.position, target) <= 2.1f)
            {
                Vector3 toward = target - actor.transform.position;
                toward.y = 0;
                target = actor.transform.position + Vector3.ClampMagnitude(toward, .45f) + Vector3.up * .36f;
                if (keeper == null) motion.ClearReachPose();
                FaceInteraction(actor.transform, target, interaction);
            }
        }

        private void OnShopStationWorked(OwnedShop shop, Employee worker, EmployeeRole role)
        {
            if (shop != _interactionSession.CurrentShop || !_staffFigures.TryGetValue(worker.Id, out var actor)) return;
            var action = role switch
            {
                EmployeeRole.Repairer => FigureInteraction.Repair,
                EmployeeRole.ModelPainter => FigureInteraction.Paint,
                _ => FigureInteraction.Inspect
            };
            var station = actor.transform.position + actor.transform.forward * .38f + Vector3.up * .94f;
            actor.GetComponent<FigureMotion>().PlayInteraction(action, station);
        }

        private void PlayCounterInteraction(GameSession session, FigureInteraction action, bool player)
        {
            if (!_hasCounter) return;
            if (player)
            {
                FaceInteraction(_owner.transform, CounterHandPoint(_owner.transform), action);
                return;
            }

            var shop = session.CurrentShop;
            if (shop == null) return;
            GameObject closest = null;
            float nearest = 1.20f;
            bool staffed = FindWorker(EmployeeRole.Cashier);
            if (!staffed && action == FigureInteraction.Checkout) FindWorker(EmployeeRole.Assembler);
            if (closest != null) FaceInteraction(closest.transform, CounterHandPoint(closest.transform), action);

            bool FindWorker(EmployeeRole role)
            {
                bool onDuty = false;
                foreach (var worker in session.ShopStaff(shop, role))
                {
                    if (!session.CityEmployeeAvailable(worker, shop)) continue;
                    onDuty = true;
                    if (!_staffFigures.TryGetValue(worker.Id, out var actor)) continue;
                    float distance = PlanarDistance(actor.transform.position, CounterHandPoint(actor.transform));
                    if (distance >= nearest) continue;
                    closest = actor;
                    nearest = distance;
                }
                return onDuty;
            }
        }

        private void ReleaseStagedPayer()
        {
            if (_stagedPayer == null) return;
            _stagedPayer.PendingPayment = null;
            _stagedPayer.PendingEmployeeCheckout = false;
            if (string.IsNullOrEmpty(_stagedPayer.PersonId)) SendOut(_stagedPayer);
            else
            {
                Kill.It(_stagedPayer.Go);
                _leaving.Remove(_stagedPayer);
            }
            _stagedPayer = null;
        }

        private Vector3 CounterPoint(float x, float y, float z) =>
            Origin + new Vector3(_counterPos.x, 0, _counterPos.y) + _counterRot * new Vector3(x, y, z);

        private Vector3 CounterHandPoint(Transform actor)
        {
            Vector3 local = Quaternion.Inverse(_counterRot) * (actor.position - CounterPoint(0, 0, 0));
            float x = Mathf.Clamp(local.x * .80f, -.43f * _counterSize.x, .43f * _counterSize.x);
            float nearSide = Mathf.Abs(local.z) > .07f ? Mathf.Sign(local.z) : 1f;
            return CounterPoint(x, 1.055f, nearSide * (_counterSize.y * .43f));
        }

        private Vector3 SelfCheckoutPoint() => CounterPoint(-_counterSize.x * .34f, 1.19f, -_counterSize.y * .30f);
        private Vector3 SelfCheckoutStand() => ClampIndoorNpc(CounterPoint(-_counterSize.x * .34f, 0, -_counterSize.y * .5f - .43f));

        private void SyncSelfCheckout(ShopSimulation shop)
        {
            bool active = _hasCounter && (shop.Accepted & PaymentMethods.SelfCheckout) != 0;
            if (!active)
            {
                if (_selfCheckoutTerminal != null && _selfCheckoutTerminal.activeSelf)
                    _selfCheckoutTerminal.SetActive(false);
                return;
            }
            if (_selfCheckoutTerminal == null)
            {
                _selfCheckoutTerminal = new GameObject("SelfCheckoutTerminal");
                _selfCheckoutTerminal.transform.SetParent(_root, false);
                var housing = Mats.Lit(ArtDirection.Ink);
                Prims.Box(_selfCheckoutTerminal.transform, "Base", new Vector3(0, -.14f, 0),
                    new Vector3(.20f, .075f, .15f), housing);
                Prims.Box(_selfCheckoutTerminal.transform, "Reader", new Vector3(0, -.045f, 0),
                    new Vector3(.17f, .16f, .095f), housing);
                Prims.Box(_selfCheckoutTerminal.transform, "Display", new Vector3(0, -.024f, -.05f),
                    new Vector3(.106f, .060f, .012f), Mats.Lit(ArtDirection.Sage));
            }
            if (_selfCheckoutLayoutVersion != _fxVersion)
            {
                _selfCheckoutLayoutVersion = _fxVersion;
                _selfCheckoutTerminal.transform.SetPositionAndRotation(SelfCheckoutPoint(), _root.rotation * _counterRot);
            }
            if (!_selfCheckoutTerminal.activeSelf) _selfCheckoutTerminal.SetActive(true);
        }

        private void PlayShelfInteraction(Visitor visitor, FigureInteraction action)
        {
            if (_shelfSpots.Count == 0) return;
            Vector3 pathPoint = action == FigureInteraction.Look ? visitor.Target : visitor.PickSpot;
            int nearest = 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < _shelfSpots.Count; i++)
            {
                float candidate = (_shelfSpots[i] - pathPoint).sqrMagnitude;
                if (candidate >= distance) continue;
                nearest = i;
                distance = candidate;
            }
            Vector3 position = visitor.Go.transform.position;
            Vector3 fixture = nearest < _shelves.Count ? Origin + _shelves[nearest].Pos
                : _tables.Count > 0 ? Origin + new Vector3(_tables[0].x, 0, _tables[0].y) : pathPoint;
            Vector3 toward = fixture - position;
            toward.y = 0;
            Vector3 target = position + Vector3.ClampMagnitude(toward, .40f) + Vector3.up * .94f;
            FaceInteraction(visitor.Go.transform, target, action);
        }

        internal void PlayStockBox(string goodsId = null)
        {
            if (goodsId != null)
            {
                foreach (var shelf in _shelves)
                {
                    var kind = ShelfKinds.Get(shelf.KindId);
                    if (kind?.IsGoods != true || !kind.GiftIds.Contains(goodsId)) continue;
                    Vector3 toShelf = Origin + shelf.Pos - OwnerWorld;
                    toShelf.y = 0;
                    if (toShelf.sqrMagnitude > 1.35f * 1.35f) continue;
                    Vector3 target = OwnerWorld + Vector3.ClampMagnitude(toShelf, .38f) + Vector3.up * .88f;
                    FaceInteraction(_owner.transform, target, FigureInteraction.StockBox);
                    return;
                }
            }
            if (NearBox) FaceInteraction(_owner.transform, NearBoxWorld - Vector3.up * .66f, FigureInteraction.StockBox);
            else _owner.GetComponent<FigureMotion>().PlayInteraction(FigureInteraction.StockBox);
        }

        internal bool PlayOwnerInteraction(FigureInteraction action, Vector3 target) =>
            FaceInteraction(_owner.transform, target, action);

        internal void PlayOwnerCounterInteraction(FigureInteraction action)
        {
            if (NearCounter) FaceInteraction(_owner.transform, CounterHandPoint(_owner.transform), action);
        }

        internal bool GreetPerson(string personId)
        {
            if (_petOwners.TryGetValue(personId, out var owner) && GreetPair(_owner.transform, owner.transform))
                return true;
            foreach (var actor in _staffFigures.Values)
                if (actor.GetComponent<CityPersonMarker>()?.PersonId == personId && GreetPair(_owner.transform, actor.transform))
                    return true;
            foreach (var visitor in _visitors.Values)
                if (visitor.PersonId == personId && GreetPair(_owner.transform, visitor.Go.transform))
                    return true;
            return false;
        }

        private void GreetArrivingCustomer(Visitor visitor)
        {
            if (visitor.Greeted || visitor.LookedAtShelf || visitor.Phase != 0 || visitor.Via.Count != 0) return;
            foreach (var actor in _salesGreeters)
            {
                if (PlanarDistance(visitor.Go.transform.position, actor.transform.position) > 1.35f) continue;
                var motion = actor.GetComponent<FigureMotion>();
                if (motion.InteractionPlaying || !FaceInteraction(actor.transform, visitor.Go.transform.position, FigureInteraction.Greet))
                    continue;
                visitor.Greeted = true;
                break;
            }
        }

        internal static bool GreetPair(Transform player, Transform other)
        {
            if (!other.gameObject.activeInHierarchy || PlanarDistance(player.position, other.position) > 2.2f) return false;
            FaceInteraction(player, other.position, FigureInteraction.Greet);
            FaceInteraction(other, player.position, FigureInteraction.Greet);
            return true;
        }

        internal static bool FaceInteraction(Transform actor, Vector3 target, FigureInteraction action)
        {
            Vector3? handTarget = action == FigureInteraction.Greet ? null : target;
            if (!actor.TryGetComponent<FigureMotion>(out var motion) || !motion.PlayInteraction(action, handTarget)) return false;
            Vector3 direction = target - actor.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .0025f) actor.rotation = Quaternion.LookRotation(direction);
            return true;
        }

        internal static float PlanarDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
