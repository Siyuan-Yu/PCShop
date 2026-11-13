using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class ShopInteriorView
    {
        private readonly List<GameObject> _shopCeilingLampMeshes = new List<GameObject>();
        private bool _ceilingMeshesHidden;

        public void SetCeilingMeshesHidden(bool hidden)
        {
            if (_ceilingMeshesHidden == hidden) return;
            _ceilingMeshesHidden = hidden;
            foreach (var mesh in _shopCeilingLampMeshes) mesh.SetActive(!hidden);
        }

        private sealed class PetActor
        {
            public GameObject Figure;
            public Transform Head, Tail;
            public Transform[] Legs;
            public bool Dog;
            public float HeadRestY, WalkPhase, WalkWeight;
        }

        private readonly Dictionary<long, PetActor> _petActors = new Dictionary<long, PetActor>();
        private readonly List<long> _retiredPets = new List<long>();
        private readonly Dictionary<string, GameObject> _petOwners = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, NpcRoute> _petOwnerRoutes = new Dictionary<string, NpcRoute>();
        private readonly Dictionary<string,PetStay> _petOwnersHere = new Dictionary<string,PetStay>();
        private readonly List<string> _retiredPetOwners = new List<string>();
        private Rect _petArea;
        private float _petTime, _petActionTime, _petActionDuration;
        private string _petAction;
        private long _petActionId;
        private Vector3 _petActionStart, _petActionMeeting, _petActionTarget, _petBallLaunch;
        private bool _petBallReleased;
        private bool _petReachLeft;
        private FigureMotion _petOwnerMotion;
        private GameObject _heldPetToy, _petSelection, _toyOnStand, _petBeingDroppedOff;
        private long _dropOffId = -1;
        private int _petCareDecorVersion = -1;
        private readonly Transform[] _petCareProps = new Transform[4], _petCareFill = new Transform[4];
        private Transform _usedLitter;

        public bool PetHousePresent { get; private set; }
        public bool NearPetHouse => PetHousePresent && PetHouseDistance(OwnerWorld) < 1.13f;
        public Vector3 PetHouseWorld => _root.TransformPoint(new Vector3(_petArea.center.x,.5f,_petArea.center.y));
        public float PetCameraSize => Mathf.Clamp(Mathf.Max(_petArea.width*.55f,_petArea.height*.72f),1.85f,3.65f);
        public bool PetActionPlaying => _petActionTime < _petActionDuration;

        public void SetPetReachSide(Vector3 towardsCamera)
        {
            Vector3 facing = FlatPetDirection(PetHouseWorld - OwnerWorld);
            _petReachLeft = Vector3.Dot(towardsCamera, Vector3.Cross(Vector3.up, facing)) < 0;
        }

        private float PetHouseDistance(Vector3 world)
        {
            Vector3 p = _root.InverseTransformPoint(world);
            var closest = new Vector3(Mathf.Clamp(p.x,_petArea.xMin,_petArea.xMax),0,Mathf.Clamp(p.z,_petArea.yMin,_petArea.yMax));
            return Vector3.Distance(new Vector3(p.x,0,p.z),closest);
        }

        private void UpdatePetBoardingView(GameSession session, float realDt)
        {
            var shop = session.CurrentShop;
            if (!session.IsPetShop(shop) && _petActors.Count == 0 && _petOwners.Count == 0 && !PetHousePresent) return;
            var house = session.InPlayerShop ? session.PetHouse(shop) : null;
            PetHousePresent = house != null;
            if (house == null)
            {
                foreach (var actor in _petActors.Values) Kill.It(actor.Figure);
                foreach (var owner in _petOwners.Values) Kill.It(owner);
                _petActors.Clear();
                _petOwners.Clear();
                _petOwnerRoutes.Clear();
                _petOwnersHere.Clear();
                RemoveDropOffVisual();
                EndPetInteraction();
                return;
            }
            var bounds = RoomPlanner.Footprint(session.ShopRoom,FurnitureCatalog.Get(house.Id),house);
            _petArea = Rect.MinMaxRect((float)bounds.MinX,(float)bounds.MinZ,(float)bounds.MaxX,(float)bounds.MaxZ);
            var stays = session.PetGuests(shop);
            UpdatePetCareProps(session,house,shop);
            _petTime += session.Time.IsRunning ? realDt : 0;
            _petActionTime = Mathf.Min(_petActionDuration,_petActionTime+realDt);
            float idleDt = session.Time.IsRunning ? realDt : 0;
            for (int i = 0; i < stays.Count; i++)
            {
                var stay = stays[i];
                if (!_petActors.TryGetValue(stay.Id,out var actor))
                {
                    var figure = PetSceneModels.BuildAnimal(_root,stay.Dog,stay.Coat,out var head,out var tail,out var legs);
                    figure.transform.localPosition = PetIdleTarget(i);
                    actor = new PetActor { Figure = figure, Dog = stay.Dog, Head = head, HeadRestY = head.localPosition.y, Tail = tail, Legs = legs };
                    _petActors.Add(stay.Id,actor);
                }
                UpdatePetPose(stay.Id,actor,i,realDt,idleDt);
            }
            _retiredPets.Clear();
            foreach (long id in _petActors.Keys)
            {
                bool exists = false;
                foreach (var stay in stays) if (stay.Id == id) { exists = true; break; }
                if (!exists) _retiredPets.Add(id);
            }
            foreach (long id in _retiredPets)
            {
                Kill.It(_petActors[id].Figure);
                _petActors.Remove(id);
                if (id == _petActionId) _petActionDuration = 0;
            }
            UpdatePetOwnerPose(realDt);
            UpdateHeldToy();
            UpdatePetOwners(session,shop,stays,realDt);
        }

        private void UpdatePetCareProps(GameSession session,PlacedFurniture house,OwnedShop shop)
        {
            if (_petCareDecorVersion != _decor.Version)
            {
                _petCareDecorVersion = _decor.Version;
                foreach (var entry in _decor.Entries)
                {
                    if (!ReferenceEquals(entry.Placed,house)) continue;
                    foreach (var action in GameSession.PetCareActions)
                    {
                        int i = (int)action;
                        _petCareProps[i] = entry.Go.transform.Find(action.ToString());
                        _petCareFill[i] = _petCareProps[i]?.Find("Fill");
                    }
                    _usedLitter = _petCareProps[(int)PetCareAction.Litter]?.Find("Used");
                    break;
                }
            }
            foreach (var action in GameSession.PetCareActions)
            {
                int i = (int)action;
                bool done = session.PetCareDoneToday(shop,action);
                if (action == PetCareAction.Litter)
                {
                    bool used = !done && session.HasPetsForCare(shop,action);
                    if (_usedLitter != null && _usedLitter.gameObject.activeSelf != used) _usedLitter.gameObject.SetActive(used);
                }
                else if (_petCareFill[i] != null && _petCareFill[i].gameObject.activeSelf != done)
                    _petCareFill[i].gameObject.SetActive(done);
            }
        }

        public bool TryPickPetCare(Ray ray,out PetCareAction action,out float nearest)
        {
            action = default;
            nearest = float.MaxValue;
            foreach (var candidate in GameSession.PetCareActions)
            {
                var prop = _petCareProps[(int)candidate];
                if (prop == null) continue;
                float width = candidate == PetCareAction.Litter ? .46f : .32f;
                if (!new Bounds(prop.position,new Vector3(width,.16f,width)).IntersectRay(ray,out float distance) || distance >= nearest) continue;
                action = candidate;
                nearest = distance;
            }
            return nearest < float.MaxValue;
        }

        private Vector3 ClampToPetArea(Vector3 p) => new Vector3(
            Mathf.Clamp(p.x,_petArea.xMin+.33f,_petArea.xMax-.33f),p.y,
            Mathf.Clamp(p.z,_petArea.yMin+.40f,_petArea.yMax-.54f));

        private static float PetEase(float t, float start, float end) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, t));

        private static Vector3 FlatPetDirection(Vector3 direction)
        {
            direction.y = 0;
            return direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
        }

        private Vector3 PetIdleTarget(int slot)
        {
            int columns = _petArea.width < 3.4f ? 3 : 4;
            float x = Mathf.Lerp(_petArea.xMin+.42f,_petArea.xMax-.42f,(slot%columns+.5f)/columns);
            float z = Mathf.Lerp(_petArea.yMin+.55f,_petArea.yMax-.63f,(slot/columns+.5f)/3f);
            float angle = slot * 2.4f + Mathf.Floor(_petTime * .53f + slot * .31f) * 2.4f;
            return ClampToPetArea(new Vector3(x + Mathf.Cos(angle) * .19f, .052f, z + Mathf.Sin(angle) * .13f));
        }

        private void UpdatePetPose(long id, PetActor actor, int slot, float dt, float idleDt)
        {
            var figure = actor.Figure.transform;
            var previous = figure.localPosition;
            bool acting = id == _petActionId && PetActionPlaying;
            float t = acting ? _petActionTime / _petActionDuration : 0;
            float paw = 0, headPitch = 0, headTilt = 0;
            Vector3 idleTarget = PetIdleTarget(slot);
            float moveDt = idleDt;
            if (!acting && PetActionPlaying)
            {
                Vector3 away = idleTarget - _petActionMeeting;
                if (away.sqrMagnitude < .68f * .68f)
                {
                    idleTarget = ClampToPetArea(_petActionMeeting + FlatPetDirection(away) * .72f);
                    moveDt = dt;
                }
            }
            Vector3 next = Vector3.MoveTowards(previous, idleTarget, moveDt * (actor.Dog ? .74f : .60f));
            Vector3 direction = next - previous;
            if (acting)
            {
                Vector3 owner = _owner.transform.localPosition;
                if (_petAction == PetShopCatalog.Ball)
                {
                    Vector3 chase = FlatPetDirection(_petActionTarget - _petActionStart);
                    Vector3 pickup = ClampToPetArea(_petActionTarget - chase * .36f);
                    float returning = PetEase(t, .66f, .94f);
                    next = Vector3.Lerp(Vector3.Lerp(_petActionStart, pickup, PetEase(t, .23f, .60f)), _petActionMeeting, returning);
                    direction = t < .66f ? chase : FlatPetDirection(owner - next);
                    headPitch = 32f * PetEase(t, .51f, .62f) * (1 - PetEase(t, .64f, .75f));
                }
                else
                {
                    next = Vector3.Lerp(_petActionStart, _petActionMeeting, PetEase(t, 0, .34f));
                    direction = owner - next;
                    if (_petAction == PetShopCatalog.Wand)
                    {
                        Vector3 sideways = Vector3.Cross(Vector3.up, FlatPetDirection(_petActionMeeting - owner));
                        float track = PetEase(t, .26f, .40f) * (1 - PetEase(t, .87f, 1));
                        next += sideways * (Mathf.Sin(t * 17f) * .13f * track);
                        paw = Mathf.Sin(t * 26f) * track;
                        next.y += Mathf.Abs(paw) * .043f;
                        headPitch = -13f * track;
                    }
                    else
                    {
                        float touch = PetEase(t, .31f, .46f) * (1 - PetEase(t, .78f, 1));
                        headPitch = -9f * touch;
                        headTilt = Mathf.Sin(t * 16f) * 8f * touch;
                    }
                }
                next = ClampToPetArea(next);
            }
            figure.localPosition = next;
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                figure.localRotation = Quaternion.RotateTowards(figure.localRotation, Quaternion.LookRotation(direction), dt * (acting ? 650f : 350f));
            float distance = Vector2.Distance(new Vector2(next.x, next.z), new Vector2(previous.x, previous.z));
            actor.WalkPhase = Mathf.Repeat(actor.WalkPhase + distance * (actor.Dog ? 13f : 17f), Mathf.PI * 2);
            float poseDt = acting ? dt : moveDt;
            float walk = acting && _petAction == PetShopCatalog.Wand && t >= .34f ? 0
                : Mathf.Clamp01(distance / Mathf.Max(.001f, poseDt * .45f));
            actor.WalkWeight = Mathf.MoveTowards(actor.WalkWeight, walk, poseDt * 9);
            float idle = _petTime + slot * .7f;
            var headPosition = actor.Head.localPosition;
            headPosition.y = actor.HeadRestY - (actor.Dog ? Mathf.Max(0, headPitch) * .002f : 0);
            actor.Head.localPosition = headPosition;
            actor.Head.localRotation = Quaternion.Euler(headPitch + Mathf.Sin(idle * 2.1f) * 2f, 0, headTilt);
            float wagTime = acting ? _petActionTime : idle;
            actor.Tail.localRotation = Quaternion.Euler(Mathf.Sin(idle * 2f) * 5f,
                Mathf.Sin(wagTime * (actor.Dog ? (acting ? 18f : 7f) : 3f)) * (actor.Dog ? (acting ? 39f : 17f) : 12f), 0);
            for (int i = 0; i < actor.Legs.Length; i++)
            {
                float stride = Mathf.Sin(actor.WalkPhase + (i == 0 || i == 3 ? 0 : Mathf.PI)) * 23f * actor.WalkWeight;
                float swipe = i == 2 ? Mathf.Max(0, paw) : i == 3 ? Mathf.Max(0, -paw) : 0;
                actor.Legs[i].localRotation = Quaternion.Euler(stride - swipe * 67f, 0, swipe * (i == 2 ? -9f : 9f));
            }
        }

        private void UpdatePetOwners(GameSession session, OwnedShop shop, IReadOnlyList<PetStay> stays, float dt)
        {
            _petOwnersHere.Clear();
            var offer = session.PetBoarding(shop)?.Offer;
            int index = 0;
            if (session.PetOwnerHere(shop,offer)) Show(offer);
            foreach (var stay in stays)
                if (session.Clock.Now.TotalMinutes >= stay.NextVisitAt && session.PetOwnerHere(shop,stay)) Show(stay);
            _retiredPetOwners.Clear();
            foreach (var id in _petOwners.Keys) if (!_petOwnersHere.ContainsKey(id)) _retiredPetOwners.Add(id);
            foreach (var id in _retiredPetOwners)
            {
                Kill.It(_petOwners[id]);
                _petOwners.Remove(id);
                _petOwnerRoutes.Remove(id);
            }
            if (offer != null && _petOwnersHere.ContainsKey(offer.OwnerId))
            {
                if (_dropOffId != offer.Id)
                {
                    RemoveDropOffVisual();
                    _petBeingDroppedOff = PetSceneModels.BuildAnimal(_root,offer.Dog,offer.Coat,out _,out _,out _);
                    _dropOffId = offer.Id;
                }
                var owner = _petOwners[offer.OwnerId].transform;
                _petBeingDroppedOff.transform.position = ClampIndoorNpc(owner.position + owner.rotation*Vector3.right*.36f);
                _petBeingDroppedOff.transform.localRotation = owner.localRotation;
            }
            else RemoveDropOffVisual();

            void Show(PetStay stay)
            {
                _petOwnersHere.Add(stay.OwnerId,stay);
                if (!_petOwners.TryGetValue(stay.OwnerId,out var actor))
                {
                    actor = Prims.Figure(_root,"PetOwner_"+stay.OwnerId,new Color(.43f,.56f,.67f),new Color(.91f,.74f,.58f),.93f);
                    actor.AddComponent<CityPersonMarker>().PersonId = stay.OwnerId;
                    CityNpcClothing.Apply(actor,session,stay.OwnerId);
                    actor.transform.localPosition = new Vector3(_doorX,0,1.15f);
                    _petOwners.Add(stay.OwnerId,actor);
                    _petOwnerRoutes.Add(stay.OwnerId,new NpcRoute());
                }
                Vector3 counter = _root.InverseTransformPoint(CounterWorld);
                var target = new Vector3(Mathf.Clamp(counter.x+.55f+index*.42f,.45f,Width-.45f),0,Mathf.Clamp(counter.z-.95f,.62f,Depth-.62f));
                MoveNpc(actor.transform,_petOwnerRoutes[stay.OwnerId],_root.TransformPoint(target),dt*3,true);
                index++;
            }
        }

        public PetStay NearbyPetOwner(out Vector3 world)
        {
            world = default;
            PetStay nearest = null;
            float limit = 1.3f*1.3f;
            foreach (var pair in _petOwnersHere)
            {
                Vector3 position = _petOwners[pair.Key].transform.position;
                float distance = (OwnerWorld-position).sqrMagnitude;
                if (distance >= limit) continue;
                nearest = pair.Value;
                world = position+Vector3.up*1.35f;
                limit = distance;
            }
            return nearest;
        }

        private void RemoveDropOffVisual()
        {
            if (_petBeingDroppedOff != null) Kill.It(_petBeingDroppedOff);
            _petBeingDroppedOff = null;
            _dropOffId = -1;
        }

        public long PickPet(Ray ray,out float nearest)
        {
            long hit = -1;
            nearest = float.MaxValue;
            foreach (var pair in _petActors)
            {
                var bounds = new Bounds(pair.Value.Figure.transform.position+Vector3.up*.25f,new Vector3(.68f,.62f,.70f));
                if (bounds.IntersectRay(ray,out float distance) && distance < nearest) { nearest = distance; hit = pair.Key; }
            }
            return hit;
        }

        public void MarkPet(long id)
        {
            if (!_petActors.TryGetValue(id,out var actor))
            {
                if (_petSelection != null) _petSelection.SetActive(false);
                return;
            }
            if (_petSelection == null)
                _petSelection = Prims.Shape(PrimitiveType.Cylinder,_root,"SelectedPet",
                    Vector3.zero,new Vector3(.54f,.006f,.54f),Mats.Lit(new Color(.84f,.65f,.29f)));
            _petSelection.SetActive(true);
            _petSelection.transform.localPosition = new Vector3(actor.Figure.transform.localPosition.x,.059f,actor.Figure.transform.localPosition.z);
        }

        public void HoldPetToy(string toy, PlacedFurniture station = null)
        {
            if (_toyOnStand != null) _toyOnStand.SetActive(true);
            _toyOnStand = null;
            if (_heldPetToy != null) Kill.It(_heldPetToy);
            _heldPetToy = toy != null ? PetSceneModels.BuildToy(_root, PetShopCatalog.IsDogToy(toy), PetShopCatalog.ToyTier(toy)) : null;
            if (station != null)
                foreach (var entry in _decor.Entries)
                    if (ReferenceEquals(entry.Placed,station))
                    {
                        _toyOnStand = entry.Go.transform.Find(PetShopCatalog.IsDogToy(toy) ? "SoftBall" : "Wand")?.gameObject;
                        if (_toyOnStand != null) _toyOnStand.SetActive(false);
                        break;
                    }
            UpdateHeldToy();
        }

        private FigureMotion PetOwnerMotion => _petOwnerMotion ??= _owner.GetComponent<FigureMotion>();

        private void UpdatePetOwnerPose(float dt)
        {
            if (!PetActionPlaying || !_petActors.TryGetValue(_petActionId, out var actor))
            {
                if (_heldPetToy != null)
                {
                    var tr = _owner.transform;
                    PetOwnerMotion.SetReachPose(tr.TransformPoint(new Vector3(_petReachLeft ? -.24f : .24f, .82f, .36f)),
                        .88f, 0, _petReachLeft);
                }
                else _petOwnerMotion?.ClearReachPose();
                return;
            }

            float t = _petActionTime / _petActionDuration;
            Transform owner = _owner.transform;
            Vector3 target = _petActionMeeting - owner.localPosition;
            target.y = 0;
            if (t < .25f && target.magnitude > .72f)
                MoveOwner(_root.TransformDirection(target), Mathf.Min(dt, (target.magnitude - .72f) / OwnerWalkSpeed));

            Vector3 look = _petAction == PetShopCatalog.Ball && t < .64f ? _petActionTarget : _petActionMeeting;
            Vector3 forward = FlatPetDirection(look - owner.localPosition);
            float stanceAngle = _petAction == PetShopCatalog.Ball ? 0 : _petReachLeft ? 20 : -20;
            Vector3 stance = Quaternion.AngleAxis(stanceAngle, Vector3.up) * forward;
            owner.localRotation = Quaternion.RotateTowards(owner.localRotation, Quaternion.LookRotation(stance), dt * 620f);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 handSide = _petReachLeft ? -right : right;
            Vector3 handTarget = owner.localPosition + handSide * .22f + forward * .36f + Vector3.up * .84f;
            float crouch = 0, weight = .9f;
            switch (_petAction)
            {
                case PetShopCatalog.Ball:
                    float throwSwing = PetEase(t, .10f, .25f);
                    var release = owner.localPosition + forward * Mathf.Lerp(-.27f, .55f, throwSwing) + handSide * .28f
                        + Vector3.up * Mathf.Lerp(1.13f, .94f, throwSwing);
                    handTarget = Vector3.Lerp(handTarget, release, PetEase(t, 0, .10f) * (1 - PetEase(t, .29f, .49f)));
                    float receive = PetEase(t, .77f, .90f) * (1 - PetEase(t, .96f, 1));
                    handTarget = Vector3.Lerp(handTarget, _root.InverseTransformPoint(actor.Head.TransformPoint(new Vector3(0,-.055f,.17f))), receive);
                    crouch = .82f * receive;
                    break;
                case PetShopCatalog.Wand:
                    float tease = PetEase(t, .16f, .35f) * (1 - PetEase(t, .84f, 1));
                    Vector3 wandHand = _petActionMeeting - forward * .40f + right * (Mathf.Sin(t * 17f) * .24f)
                        + Vector3.up * (.52f + Mathf.Sin(t * 26f) * .045f);
                    handTarget = Vector3.Lerp(handTarget, wandHand, tease);
                    crouch = .76f * tease;
                    break;
                default:
                    float touch = PetEase(t, .11f, .37f) * (1 - PetEase(t, .79f, 1));
                    float stroke = Mathf.Sin(PetEase(t, .38f, .79f) * Mathf.PI * 4);
                    handTarget = _root.InverseTransformPoint(actor.Head.TransformPoint(new Vector3(
                        _petReachLeft ? .025f : -.025f, .12f + .014f * Mathf.Abs(stroke), -.015f + .065f * stroke)));
                    weight = touch;
                    crouch = 1;
                    break;
            }
            if (weight > 0) PetOwnerMotion.SetReachPose(_root.TransformPoint(handTarget), weight, crouch, _petReachLeft);
            else PetOwnerMotion.ClearReachPose();
        }

        private void UpdateHeldToy()
        {
            if (_heldPetToy == null) return;
            var handTransform = PetOwnerMotion.ReachingHand;
            Vector3 hand = _root.InverseTransformPoint(handTransform.position);
            Vector3 forward = _owner.transform.localRotation * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (_heldPetToy.name == "SoftBall") hand += forward * .07f;
            var item = _heldPetToy.transform;
            item.localPosition = hand;
            item.localRotation = Quaternion.LookRotation(forward + Vector3.down * .15f);
            if (!PetActionPlaying) return;
            float t = _petActionTime / _petActionDuration;
            if (_petAction == PetShopCatalog.Ball)
            {
                if (t < .21f) return;
                if (!_petBallReleased)
                {
                    _petBallReleased = true;
                    _petBallLaunch = hand;
                }
                var landing = _petActionTarget + Vector3.up * .07f;
                float toss = Mathf.InverseLerp(.21f, .43f, t);
                if (t < .59f)
                {
                    item.localPosition = Vector3.Lerp(_petBallLaunch, landing, toss) + Vector3.up * (Mathf.Sin(Mathf.PI * toss) * .37f);
                    return;
                }
                if (!_petActors.TryGetValue(_petActionId, out var dog)) return;
                Vector3 mouth = _root.InverseTransformPoint(dog.Head.TransformPoint(new Vector3(0, -.055f, .17f)));
                item.localPosition = Vector3.Lerp(Vector3.Lerp(landing, mouth, PetEase(t, .59f, .67f)), hand, PetEase(t, .89f, .99f));
            }
            else if (_petAction == PetShopCatalog.Wand)
            {
                forward = FlatPetDirection(_petActionMeeting - _owner.transform.localPosition);
                right = Vector3.Cross(Vector3.up, forward);
                float tease = PetEase(t, .13f, .32f) * (1 - PetEase(t, .86f, 1));
                item.localRotation = Quaternion.LookRotation(forward + right * (Mathf.Sin(t * 17f) * .55f * tease)
                    + Vector3.down * (.15f + .2f * tease));
            }
        }

        public void AnimatePetAction(long id, string toy)
        {
            if (!_petActors.TryGetValue(id,out var actor)) return;
            _petActionId = id;
            _petAction = toy;
            _petActionTime = 0;
            _petActionDuration = toy == PetShopCatalog.Ball ? 2.7f : toy == PetShopCatalog.Wand ? 2.25f : 1.6f;
            _petBallReleased = false;
            _petActionStart = actor.Figure.transform.localPosition;
            var owner = _owner.transform.localPosition;
            owner.y = .052f;
            _petActionMeeting = ClampToPetArea(owner);
            if ((_petActionMeeting - owner).sqrMagnitude < .12f)
                _petActionMeeting = ClampToPetArea(owner + FlatPetDirection(_petActionStart - owner) * .64f);
            var middle = new Vector3(_petArea.center.x,.052f,_petArea.center.y);
            Vector3 inward = FlatPetDirection(middle - owner);
            Vector3 nearSide = Vector3.Cross(Vector3.up, inward) * (_petReachLeft ? -1 : 1);
            _petActionMeeting = ClampToPetArea(_petActionMeeting + nearSide * .24f);
            _petActionTarget = ClampToPetArea(middle + inward * .64f + Vector3.Cross(Vector3.up, inward) * .23f);
        }

        public void EndPetInteraction()
        {
            _petActionDuration = 0;
            _petOwnerMotion?.ClearReachPose();
            HoldPetToy(null);
            MarkPet(-1);
        }
    }
}
