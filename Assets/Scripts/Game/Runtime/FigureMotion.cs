using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>Procedural walking for the miniature figures. Only the visual rig moves; gameplay roots stay on the ground.</summary>
    public sealed class FigureMotion : MonoBehaviour
    {
        private static readonly List<FigureMotion> Active = new List<FigureMotion>();
        private Transform _visual;
        private readonly Transform[] _hips = new Transform[2], _knees = new Transform[2], _feet = new Transform[2], _arms = new Transform[2];
        private readonly List<Renderer> _pants = new List<Renderer>(), _sleeves = new List<Renderer>();
        private Vector3 _previousPosition;
        private float _phase, _blend, _idleTime, _scale;
        private bool _sampled, _locomotion = true;

        public Transform Torso { get; private set; }
        public float WalkBlend => _blend;
        public bool LocomotionEnabled
        {
            get => _locomotion;
            set { if (_locomotion == value) return; _locomotion = value; ResetMotion(); }
        }

        public void Initialize(Transform visual, Transform torso, float scale)
        {
            _visual = visual;
            Torso = torso;
            _scale = scale;
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "L" : "R";
                _hips[i] = visual.Find("Hip" + side);
                _knees[i] = _hips[i].Find("Knee" + side);
                _feet[i] = _knees[i].Find("Foot" + side);
                _arms[i] = torso.Find("Shoulder" + side);
                _pants.Add(_hips[i].Find("Leg" + side).GetComponent<Renderer>());
                _pants.Add(_knees[i].Find("Shin" + side).GetComponent<Renderer>());
                _sleeves.Add(_arms[i].Find("Sleeve" + side).GetComponent<Renderer>());
            }
            if (!Active.Contains(this)) Active.Add(this); // Also register in edit-mode rendering.
            ResetMotion();
        }

        public void RecolorLimbs(Material pants, Material sleeves)
        {
            foreach (var r in _pants) r.sharedMaterial = pants;
            foreach (var r in _sleeves) r.sharedMaterial = sleeves;
        }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); ResetMotion(); }
        private void OnDisable() { Active.Remove(this); _sampled = false; }
        private void OnDestroy() => Active.Remove(this);

        // Called after all world/interior movement, including the editor smoke simulation.
        public static void AdvanceAll(float dt, bool animateIdle)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var motion = Active[i];
                if (motion == null) { Active.RemoveAt(i); continue; }
                if (!motion.isActiveAndEnabled) { motion.ResetMotion(); continue; }
                motion.Advance(dt, animateIdle);
            }
        }

        public void ResetMotion()
        {
            _sampled = false;
            _phase = _blend = 0;
            if (_visual != null) ApplyPose();
        }

        public void Advance(float dt, bool animateIdle = true)
        {
            if (_visual == null || dt <= 0) return;
            Vector3 position = transform.position;
            if (!_sampled)
            {
                _previousPosition = position;
                _sampled = true;
            }
            Vector3 delta = position - _previousPosition;
            delta.y = 0;
            _previousPosition = position;
            float distance = delta.magnitude;
            float scale = Mathf.Max(.1f, _scale * Mathf.Abs(transform.lossyScale.x));
            // Loading, entering a room and teleporting must never become a giant stride.
            if (distance > Mathf.Max(2f, dt * 18f) * scale)
            {
                _blend = _phase = 0;
                ApplyPose();
                return;
            }
            bool moving = _locomotion && distance > .0001f * scale;
            if (!moving && !animateIdle) return; // Modal/game pause freezes the last pose.
            float blendDt = Mathf.Min(dt, .1f);
            _blend = Mathf.MoveTowards(_blend, moving ? 1 : 0, blendDt * (moving ? 9f : 5f));
            if (moving)
            {
                // Distance-driven steps, capped only for accelerated simulation to keep limbs readable.
                float cycles = Mathf.Min(distance / (1.05f * scale), dt * 3.2f);
                _phase = Mathf.Repeat(_phase + cycles * Mathf.PI * 2f, Mathf.PI * 2f);
            }
            if (animateIdle) _idleTime += blendDt;
            ApplyPose();
        }

        private void ApplyPose()
        {
            float bob = (-.045f + .004f * (1f - Mathf.Cos(_phase * 2f))) * _blend;
            _visual.localPosition = Vector3.up * (bob * _scale);
            Torso.localPosition = Vector3.up * (Mathf.Sin(_idleTime * 1.8f) * .003f * (1f - _blend));
            Torso.localRotation = Quaternion.Euler(3f * _blend, Mathf.Cos(_phase) * 2f * _blend, Mathf.Sin(_phase) * 1.5f * _blend);
            for (int i = 0; i < 2; i++)
            {
                float phase = _phase + i * Mathf.PI;
                float footZ = -Mathf.Sin(phase) * .21f * _blend;
                float lift = Mathf.Pow(Mathf.Max(0, -Mathf.Cos(phase)), 1.4f) * .13f * _blend;
                // Two equal leg segments solve to a level foot. The supporting shoe stays at ground height.
                float down = .56f + bob - (.08f + lift);
                float reach = Mathf.Clamp(Mathf.Sqrt(down * down + footZ * footZ), .05f, .4999f);
                float knee = 2f * Mathf.Acos(reach / .5f) * Mathf.Rad2Deg;
                float hip = Mathf.Atan2(-footZ, down) * Mathf.Rad2Deg - knee * .5f;
                _hips[i].localRotation = Quaternion.Euler(hip, 0, 0);
                _knees[i].localRotation = Quaternion.Euler(knee, 0, 0);
                _feet[i].localRotation = Quaternion.Euler(-hip - knee, 0, 0);
                float arm = -Mathf.Sin(phase) * 26f * _blend;
                _arms[i].localRotation = Quaternion.Euler(arm, 0, i == 0 ? -8f : 8f);
            }
        }
    }
}
