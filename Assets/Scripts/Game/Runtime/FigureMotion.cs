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
        private bool _seated;
        public string ActivityKind { get; set; }
        public bool Seated
        {
            get=>_seated;
            set{if(_seated==value)return;_seated=value;if(_visual!=null)ApplyPose();}
        }

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
        public void SetClothingCut(bool shortSleeve, bool shortPants, bool thick, Material skin)
        {
            for(int i=0;i<2;i++)
            {
                var sleeve=_sleeves[i].transform;
                sleeve.localPosition=new Vector3(0,shortSleeve?-.075f:-.175f,0);
                sleeve.localScale=new Vector3(thick?.20f:.15f,shortSleeve?.15f:.35f,thick?.28f:.23f);
                var forearm=_arms[i].Find("ClothingForearm");
                if(forearm==null)forearm=Prims.Box(_arms[i],"ClothingForearm",new Vector3(0,-.255f,0),new Vector3(.12f,.22f,.18f),skin).transform;
                forearm.gameObject.SetActive(shortSleeve);
                forearm.GetComponent<Renderer>().sharedMaterial=skin;
                if(shortPants)_pants[i*2+1].sharedMaterial=skin;
            }
        }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); ResetMotion(); }
        private void OnDisable() { Active.Remove(this); _sampled = false; }
        private void OnDestroy() => Active.Remove(this);

        // Called after all world/interior movement, including the editor smoke simulation.
        public bool PresentationAnimation {get;set;}
        public static void AdvanceAll(float dt, bool animateIdle)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var motion = Active[i];
                if (motion == null) { Active.RemoveAt(i); continue; }
                if (!motion.isActiveAndEnabled) { motion.ResetMotion(); continue; }
                motion.Advance(dt, animateIdle || motion.PresentationAnimation);
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
            if(_seated)
            {
                _visual.localPosition=Vector3.zero;Torso.localPosition=Vector3.zero;Torso.localRotation=Quaternion.Euler(-4,0,0);
                for(int i=0;i<2;i++)
                {
                    _hips[i].localRotation=Quaternion.Euler(-90,0,0);
                    _knees[i].localRotation=Quaternion.Euler(90,0,0);
                    _feet[i].localRotation=Quaternion.identity;
                    float gesture=string.IsNullOrEmpty(ActivityKind)?0:Mathf.Sin(_idleTime*2.2f+i*1.4f)*12f;
                    _arms[i].localRotation=Quaternion.Euler(-35+gesture,0,i==0?-8:8);
                }
                return;
            }
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
            if (!string.IsNullOrEmpty(ActivityKind) && _blend < .1f)
            {
                float gesture=Mathf.Sin(_idleTime*2.4f)*12f;
                // Both participants share a readable activity pose, rather than a walking loop.
                bool energetic=ActivityKind=="court" || ActivityKind=="golf" || ActivityKind=="hike";
                Torso.localRotation=Quaternion.Euler(energetic?8:0,Mathf.Sin(_idleTime*1.5f)*4,0);
                _arms[0].localRotation=Quaternion.Euler(ActivityKind=="ktv"?-100+gesture:-45+gesture,0,-12);
                _arms[1].localRotation=Quaternion.Euler(energetic?-65-gesture:-20-gesture,0,12);
            }
        }
    }
}
