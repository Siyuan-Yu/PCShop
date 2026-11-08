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
            public Transform Tail;
            public Transform[] Feet;
            public bool Dog;
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
        private Vector3 _petActionStart, _petActionTarget;
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
            for (int i = 0; i < stays.Count; i++)
            {
                var stay = stays[i];
                if (!_petActors.TryGetValue(stay.Id,out var actor))
                {
                    var figure = PetSceneModels.BuildAnimal(_root,stay.Dog,stay.Coat,out var tail,out var feet);
                    actor = new PetActor { Figure = figure, Dog = stay.Dog, Tail = tail, Feet = feet };
                    _petActors.Add(stay.Id,actor);
                }
                UpdatePetPose(stay.Id,actor,i,realDt);
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
            }
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
            Mathf.Clamp(p.x,_petArea.xMin+.32f,_petArea.xMax-.32f),p.y,
            Mathf.Clamp(p.z,_petArea.yMin+.34f,_petArea.yMax-.34f));

        private void UpdatePetPose(long id, PetActor actor, int slot, float dt)
        {
            var figure = actor.Figure.transform;
            int columns = _petArea.width < 3.4f ? 3 : 4;
            float phase = _petTime*.82f + slot*1.94f;
            float x = Mathf.Lerp(_petArea.xMin+.48f,_petArea.xMax-.48f,(slot%columns+.5f)/columns);
            float z = Mathf.Lerp(_petArea.yMin+.44f,_petArea.yMax-.54f,(slot/columns+.5f)/3f);
            Vector3 next = ClampToPetArea(new Vector3(x+Mathf.Sin(phase)*.22f,.05f,z+Mathf.Cos(phase*.79f)*.20f));
            Vector3 direction = new Vector3(Mathf.Cos(phase),0,-Mathf.Sin(phase*.79f));
            if (id == _petActionId && PetActionPlaying)
            {
                float t = _petActionTime / _petActionDuration;
                if (_petAction == PetShopCatalog.Ball)
                {
                    float run = Mathf.Clamp01((t-.23f)/.34f);
                    float back = Mathf.Clamp01((t-.60f)/.40f);
                    next = Vector3.Lerp(Vector3.Lerp(_petActionStart,_petActionTarget,run),_petActionStart,back);
                    direction = back > 0 ? _petActionStart-_petActionTarget : _petActionTarget-_petActionStart;
                }
                else
                {
                    next = Vector3.Lerp(_petActionStart,_petActionTarget,Mathf.Sin(Mathf.PI*t));
                    next.y += Mathf.Sin(Mathf.PI*t)*(_petAction == PetShopCatalog.Wand ? .16f : .025f);
                    direction = _petActionTarget-_petActionStart;
                }
                phase = t * 13f;
            }
            figure.localPosition = next;
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                figure.localRotation = Quaternion.Slerp(figure.localRotation,Quaternion.LookRotation(direction),dt*12);
            actor.Tail.localRotation = Quaternion.Euler(0,Mathf.Sin(phase*(actor.Dog?5f:2.7f))*(actor.Dog?39f:19f),0);
            for (int i = 0; i < actor.Feet.Length; i++)
                actor.Feet[i].localRotation = Quaternion.Euler(Mathf.Sin(phase*4f+(i%3==0?0:Mathf.PI))*19f,0,0);
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
                    _petBeingDroppedOff = PetSceneModels.BuildAnimal(_root,offer.Dog,offer.Coat,out _,out _);
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
            _heldPetToy = toy != null ? PetSceneModels.BuildToy(_root,toy == PetShopCatalog.Ball) : null;
            if (station != null)
                foreach (var entry in _decor.Entries)
                    if (ReferenceEquals(entry.Placed,station))
                    {
                        _toyOnStand = entry.Go.transform.Find(toy == PetShopCatalog.Ball ? "SoftBall" : "Wand")?.gameObject;
                        if (_toyOnStand != null) _toyOnStand.SetActive(false);
                        break;
                    }
            UpdateHeldToy();
        }

        private Vector3 PetHand => _root.InverseTransformPoint(OwnerWorld)+new Vector3(.20f,.75f,.16f);

        private void UpdateHeldToy()
        {
            if (_heldPetToy == null) return;
            var hand = PetHand;
            var item = _heldPetToy.transform;
            item.localRotation = Quaternion.identity;
            item.localPosition = hand;
            if (!PetActionPlaying) return;
            float t = _petActionTime / _petActionDuration;
            if (_petAction == PetShopCatalog.Ball)
            {
                float toss = Mathf.Clamp01(t/.29f);
                var landing = _petActionTarget + Vector3.up*.12f;
                item.localPosition = t < .60f ? Vector3.Lerp(hand,landing,toss)+Vector3.up*Mathf.Sin(Mathf.PI*toss)*.58f
                    : Vector3.Lerp(landing,hand,Mathf.Clamp01((t-.60f)/.4f));
            }
            else if (_petAction == PetShopCatalog.Wand)
            {
                var feather = Vector3.Lerp(hand,_petActionTarget+Vector3.up*.42f,.65f);
                item.localPosition = feather+new Vector3(Mathf.Sin(t*21)*.18f,Mathf.Sin(t*16)*.10f,0);
                item.localRotation = Quaternion.Euler(12,Mathf.Sin(t*18)*38,-18);
            }
        }

        public void AnimatePetAction(long id, string toy)
        {
            if (!_petActors.TryGetValue(id,out var actor)) return;
            _petActionId = id;
            _petAction = toy;
            _petActionTime = 0;
            _petActionDuration = toy == PetShopCatalog.Ball ? 1.65f : toy == PetShopCatalog.Wand ? .95f : .65f;
            _petActionStart = actor.Figure.transform.localPosition;
            var hand = ClampToPetArea(PetHand);
            var middle = new Vector3(_petArea.center.x,.05f,_petArea.center.y);
            _petActionTarget = toy == PetShopCatalog.Ball
                ? ClampToPetArea(Vector3.Lerp(_petActionStart,middle,.65f)+new Vector3(_petActionStart.x > middle.x ? -.72f : .72f,0,.23f))
                : Vector3.Lerp(_petActionStart,new Vector3(hand.x,.05f,hand.z),toy == null ? .34f : .66f);
        }

        public void EndPetInteraction()
        {
            _petActionDuration = 0;
            HoldPetToy(null);
            MarkPet(-1);
        }
    }
}
