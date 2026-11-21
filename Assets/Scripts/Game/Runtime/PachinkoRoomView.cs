using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    internal sealed class PachinkoRoomView
    {
        private const float WalkSpeed = 2.6f;
        private const float PlayerRadius = .3f;
        private static readonly Vector3 Door = new Vector3(0, 0, .45f);
        private readonly Transform _owner;
        private readonly Rect _walkArea;
        private readonly Transform[] _cabinets = new Transform[LeisureVenueEntrance.PachinkoCabinetCount];
        private readonly Transform _counter;
        private readonly List<Rect> _blockers = new List<Rect>();
        private List<Vector3> _doorPath;
        private int _doorStep;

        public Transform Root { get; }
        public Vector3 OwnerWorld => Root.TransformPoint(_owner.localPosition + Vector3.up * .09f);
        public Vector3 OwnerFacing => _owner.forward;
        public Vector3 DoorWorld => Root.TransformPoint(Door + Vector3.up * 1.15f);
        public Vector3 CounterWorld => _counter == null ? Root.position : _counter.position + Vector3.up * 1.45f;
        public bool NearDoor => NearDoorway(_owner.localPosition);
        public bool NearCounter => _counter != null && IsInFrontOf(_counter, 1.7f);

        public PachinkoRoomView(PlaceVisual visual, int? activeMachine)
        {
            Root = (visual.VenueInterior ?? visual.Root).transform;
            float width = (float)visual.Place.Width;
            float depth = (float)visual.Place.Depth;
            _walkArea = Rect.MinMaxRect(-width * .5f + .45f, .40f, width * .5f - .45f, depth - .45f);
            _owner = new GameObject("PachinkoPlayerPosition").transform;
            _owner.SetParent(Root, false);
            _owner.localPosition = new Vector3(0, 0, 1.85f);
            _counter = Root.Find(LeisureVenueEntrance.Counter);
            AddBlocker(_counter);
            for (int i = 0; i < _cabinets.Length; i++)
            {
                _cabinets[i] = LeisureVenueEntrance.PachinkoCabinet(Root, i);
                AddBlocker(_cabinets[i], frontExtension: .3f);
            }
            if (activeMachine is int machine && machine >= 0 && machine < _cabinets.Length && _cabinets[machine] != null)
                _owner.localPosition = InteractionSpot(_cabinets[machine]);
        }

        private void AddBlocker(Transform furniture, float frontExtension = 0)
        {
            if (furniture == null) return;
            var center = furniture.localPosition;
            var size = furniture.localScale;
            float halfWidth = size.x * .5f + PlayerRadius;
            float halfDepth = size.z * .5f + PlayerRadius;
            _blockers.Add(Rect.MinMaxRect(center.x - halfWidth, center.z - halfDepth - frontExtension,
                center.x + halfWidth, center.z + halfDepth));
        }

        private static Vector3 InteractionSpot(Transform furniture) =>
            new Vector3(furniture.localPosition.x, 0, furniture.localPosition.z - furniture.localScale.z * .5f - .78f);

        private bool IsInFrontOf(Transform furniture, float reach)
        {
            var at = _owner.localPosition;
            return at.z < furniture.localPosition.z - furniture.localScale.z * .5f
                && (at - InteractionSpot(furniture)).sqrMagnitude <= reach * reach;
        }

        private static bool NearDoorway(Vector3 point) => (point - Door).sqrMagnitude <= 1.13f * 1.13f;

        public bool TryNearbyMachine(out int machine, out Vector3 world)
        {
            machine = -1;
            world = default;
            float best = float.PositiveInfinity;
            for (int i = 0; i < _cabinets.Length; i++)
            {
                var cabinet = _cabinets[i];
                if (cabinet == null || !IsInFrontOf(cabinet, 1.25f)) continue;
                float distance = (_owner.localPosition - InteractionSpot(cabinet)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                machine = i;
                world = cabinet.position + Vector3.up * 1.8f;
            }
            return machine >= 0;
        }

        public void MoveOwner(Vector3 direction, float dt)
        {
            CancelDoorWalk();
            MoveLocal(Root.InverseTransformDirection(direction), dt);
        }

        private void MoveLocal(Vector3 direction, float dt)
        {
            direction.y = 0;
            if (dt <= 0 || direction.sqrMagnitude < .0001f) return;
            direction.Normalize();
            var current = _owner.localPosition;
            var step = direction * (WalkSpeed * dt);
            var x = new Vector3(Mathf.Clamp(current.x + step.x, _walkArea.xMin, _walkArea.xMax), 0, current.z);
            if (CityRoomRoute.CanTraverse(current, x, _blockers)) current = x;
            var z = new Vector3(current.x, 0, Mathf.Clamp(current.z + step.z, _walkArea.yMin, _walkArea.yMax));
            if (CityRoomRoute.CanTraverse(current, z, _blockers)) current = z;
            _owner.localPosition = current;
            _owner.localRotation = Quaternion.LookRotation(direction);
        }

        public void CancelDoorWalk()
        {
            _doorPath = null;
            _doorStep = 0;
        }

        public int WalkToDoor(float dt)
        {
            if (NearDoor) return 1;
            if (_doorPath == null)
            {
                _doorPath = CityRoomRoute.Find(_owner.localPosition, _walkArea, NearDoorway, _blockers);
                _doorStep = 0;
                if (_doorPath == null) return -1;
            }
            if (!CityRoomRoute.Advance(_doorPath, ref _doorStep, _owner, dt, WalkSpeed, MoveLocal)) return -1;
            return NearDoor ? 1 : _doorStep < _doorPath.Count ? 0 : -1;
        }

        public void Dispose()
        {
            if (_owner != null) Kill.It(_owner.gameObject);
        }
    }
}
