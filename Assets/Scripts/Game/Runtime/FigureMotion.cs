using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public enum FigureWorkAction { None, Cook, Plate, Serve, Clear, Assemble }

    /// <summary>Procedural walking for the miniature figures. Only the visual rig moves; gameplay roots stay on the ground.</summary>
    public sealed class FigureMotion : MonoBehaviour
    {
        private const float WalkCycleDistance = .88f;
        private const float WalkSupportFraction = .60f;
        private static readonly List<FigureMotion> Active = new List<FigureMotion>();
        private Transform _visual;
        private readonly Transform[] _hips = new Transform[2], _knees = new Transform[2], _feet = new Transform[2], _arms = new Transform[2], _hands = new Transform[2];
        private readonly Transform[] _elbows = new Transform[2];
        private Vector3 _previousPosition;
        private float _phase, _blend, _idleTime, _scale;
        private bool _sampled, _locomotion = true;
        private bool _seated;
        private float _furnitureSeatHeight, _furnitureSeatBlend;
        private Vector3 _reachTarget;
        private float _reachWeight, _reachCrouch;
        private float _kickFollowThrough;
        private int _reachArm = 1;
        private FigureWorkAction _workAction;
        private GameObject _spatula, _screwdriver;
        public string ActivityKind { get; set; }
        public int ActivityOption { get; set; } = -1;
        public FigureWorkAction WorkAction
        {
            get => _workAction;
            set
            {
                if (_workAction == value) return;
                _workAction = value;
                if (value == FigureWorkAction.Cook && _spatula == null) _spatula = CreateWorkTool(true);
                if (value == FigureWorkAction.Assemble && _screwdriver == null) _screwdriver = CreateWorkTool(false);
                ApplyPose();
            }
        }
        public bool Seated
        {
            get=>_seated;
            set{if(_seated==value)return;_seated=value;if(_visual!=null)ApplyPose();}
        }

        public Transform Torso { get; private set; }
        internal FigureAppearance Appearance { get; private set; }
        public Transform ReachingHand => _hands[_reachArm];

        internal void SetFurnitureSeatPose(float height, float blend)
        {
            _furnitureSeatHeight = height;
            _furnitureSeatBlend = blend;
            ApplyPose();
        }

        public float WalkBlend => _blend;
        public bool LocomotionEnabled
        {
            get => _locomotion;
            set { if (_locomotion == value) return; _locomotion = value; ResetMotion(); }
        }

        internal void Initialize(Transform visual, Transform torso, float scale, FigureAppearance appearance)
        {
            _visual = visual;
            Torso = torso;
            _scale = scale;
            Appearance = appearance;
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "L" : "R";
                _hips[i] = visual.Find("Hip" + side);
                _knees[i] = _hips[i].Find("Knee" + side);
                _feet[i] = _knees[i].Find("Foot" + side);
                _arms[i] = torso.Find("Shoulder" + side);
                _elbows[i] = _arms[i].Find("Elbow");
                _hands[i] = _elbows[i].Find("Hand");
            }
            if (!Active.Contains(this)) Active.Add(this); // Also register in edit-mode rendering.
            ResetMotion();
        }

        public void SetReachPose(Vector3 target, float weight, float crouch, bool leftHand)
        {
            _reachTarget = target;
            _reachWeight = weight;
            _reachCrouch = crouch;
            _reachArm = leftHand ? 0 : 1;
            ApplyPose();
        }

        internal void SetKickPose(float followThrough)
        {
            if (Mathf.Approximately(_kickFollowThrough, followThrough)) return;
            _kickFollowThrough = followThrough;
            ApplyPose();
        }

        private GameObject CreateWorkTool(bool cooking)
        {
            var tool = new GameObject(cooking ? "CookingSpatula" : "AssemblyScrewdriver");
            tool.transform.SetParent(_hands[1], false);
            var steel = Mats.Lit(new Color(.73f, .76f, .79f));
            if (cooking)
            {
                Prims.Box(tool.transform, "Handle", new Vector3(0, -.1f, -.055f), new Vector3(.055f, .30f, .055f),
                    Mats.Lit(ArtDirection.Wood)).transform.localRotation = Quaternion.Euler(18, 0, 0);
                Prims.Box(tool.transform, "Blade", new Vector3(0, -.25f, -.105f), new Vector3(.135f, .12f, .035f), steel);
            }
            else
            {
                Prims.Box(tool.transform, "Handle", new Vector3(0, -.02f, .04f), new Vector3(.075f, .075f, .12f),
                    Mats.Lit(new Color(.9f, .5f, .14f)));
                Prims.Box(tool.transform, "Bit", new Vector3(0, -.02f, -.07f), new Vector3(.033f, .033f, .13f), steel);
            }
            return tool;
        }

        public void ClearReachPose()
        {
            if (_reachWeight <= 0) return;
            _reachWeight = 0;
            ResetMotion();
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
                float cycles = Mathf.Min(distance / (WalkCycleDistance * scale), dt * 3.2f);
                _phase = Mathf.Repeat(_phase + cycles, 1f);
            }
            if (animateIdle) _idleTime += blendDt;
            ApplyPose();
        }

        private void ApplyPose()
        {
            bool working = !_seated && _furnitureSeatBlend <= 0 && _reachWeight <= 0 && _blend < .1f;
            const float hipHeight = PlayerCharacterModel.HipHeight;
            const float segment = PlayerCharacterModel.LegSegment;
            const float ankle = PlayerCharacterModel.AnkleHeight;
            if (_spatula != null) _spatula.SetActive(working && _workAction == FigureWorkAction.Cook);
            if (_screwdriver != null) _screwdriver.SetActive(working && _workAction == FigureWorkAction.Assemble);
            if (_furnitureSeatBlend > 0)
            {
                ApplyFurnitureSeatPose();
                return;
            }
            if(_seated)
            {
                float seatOffset = hipHeight - segment - ankle - .23f;
                _visual.localPosition = Vector3.down * (seatOffset * _scale);
                Torso.localPosition=Vector3.zero;Torso.localRotation=Quaternion.Euler(-4,0,0);
                for(int i=0;i<2;i++)
                {
                    _hips[i].localRotation=Quaternion.Euler(-90,0,0);
                    _knees[i].localRotation=Quaternion.Euler(90,0,0);
                    _feet[i].localRotation=Quaternion.identity;
                    float gesture=string.IsNullOrEmpty(ActivityKind)?0:Mathf.Sin(_idleTime*2.2f+i*1.4f)*12f;
                    _arms[i].localRotation=Quaternion.Euler(-35+gesture,0,i==0?-8:8);
                    SetElbow(i, 26 + gesture * .4f);
                }
                return;
            }
            Vector2 leftAnkle = WalkAnkle(_phase) * _blend;
            Vector2 rightAnkle = WalkAnkle(Mathf.Repeat(_phase + .5f, 1f)) * _blend;
            // Set the hip height from the feet so the supporting leg can straighten as it passes under the body.
            float legReach = 2 * segment - .003f * _blend;
            float pelvisHeight = ankle + Mathf.Min(
                leftAnkle.y + Mathf.Sqrt(legReach * legReach - leftAnkle.x * leftAnkle.x),
                rightAnkle.y + Mathf.Sqrt(legReach * legReach - rightAnkle.x * rightAnkle.x));
            _visual.localPosition = Vector3.up * ((pelvisHeight - hipHeight) * _scale);
            float gaitAngle = _phase * 2f * Mathf.PI;
            Torso.localRotation = Quaternion.Euler(2f * _blend, -Mathf.Cos(gaitAngle) * 3f * _blend, Mathf.Sin(gaitAngle) * _blend);
            Vector3 walkPelvis = Vector3.up * hipHeight;
            Torso.localPosition = walkPelvis - Torso.localRotation * walkPelvis
                + Vector3.up * (Mathf.Sin(_idleTime * 1.8f) * .003f * (1f - _blend));
            for (int i = 0; i < 2; i++)
            {
                Vector2 foot = i == 0 ? leftAnkle : rightAnkle;
                float down = pelvisHeight - ankle - foot.y;
                float reach = Mathf.Min(Mathf.Sqrt(down * down + foot.x * foot.x), 2 * segment);
                float knee = 2f * Mathf.Acos(reach / (2 * segment)) * Mathf.Rad2Deg;
                float hip = Mathf.Atan2(-foot.x, down) * Mathf.Rad2Deg - knee * .5f;
                _hips[i].localRotation = Quaternion.Euler(hip, 0, 0);
                _knees[i].localRotation = Quaternion.Euler(knee, 0, 0);
                _feet[i].localRotation = Quaternion.Euler(-hip - knee, 0, 0);
                float armSwing = Mathf.Cos(gaitAngle + i * Mathf.PI);
                const float armSpread = 1.5f;
                _arms[i].localRotation = Quaternion.Euler(armSwing * 14f * _blend, 0, i == 0 ? -armSpread : armSpread);
                SetElbow(i, 6 + (4 - 3 * armSwing) * _blend);
            }
            if (_kickFollowThrough > 0)
            {
                float kick = _kickFollowThrough;
                _hips[1].localRotation = Quaternion.Euler(-62 * kick, 0, 0);
                _knees[1].localRotation = Quaternion.Euler(12 * kick, 0, 0);
                _feet[1].localRotation = Quaternion.Euler(-13 * kick, 0, 0);
                _arms[0].localRotation = Quaternion.Euler(-28 * kick, 0, -22 * kick);
                _arms[1].localRotation = Quaternion.Euler(17 * kick, 0, 24 * kick);
                return;
            }
            if (_reachWeight > 0)
            {
                float bend = 68f * _reachCrouch * _reachWeight;
                _visual.localPosition = Vector3.down * (2 * segment * (1f - Mathf.Cos(bend * Mathf.Deg2Rad)) * _scale);
                Torso.localRotation = Quaternion.Euler(19f * _reachCrouch * _reachWeight, 0, 0);
                // Hips and torso are sibling bones. Keep the waist attached as the torso leans.
                Vector3 pelvis = Vector3.up * hipHeight;
                Torso.localPosition = pelvis - Torso.localRotation * pelvis;
                if (bend > 0)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        _hips[i].localRotation = Quaternion.Euler(-bend, 0, 0);
                        _knees[i].localRotation = Quaternion.Euler(2 * bend, 0, 0);
                        _feet[i].localRotation = Quaternion.Euler(-bend, 0, 0);
                    }
                }
                int other = 1 - _reachArm;
                SetElbow(other, 24 * _reachCrouch * _reachWeight);
                SetElbow(_reachArm, 4);
                _arms[other].localRotation = Quaternion.Slerp(_arms[other].localRotation,
                    Quaternion.Euler(-24, 0, other == 0 ? -17 : 17), _reachWeight * _reachCrouch);
                var arm = _arms[_reachArm];
                Vector3 direction = Torso.InverseTransformPoint(_reachTarget) - arm.localPosition;
                if (direction.sqrMagnitude > .0001f)
                {
                    Vector3 rest = _elbows[_reachArm].localPosition;
                    arm.localRotation = Quaternion.Slerp(arm.localRotation, Quaternion.FromToRotation(rest, direction), _reachWeight);
                }
                return;
            }
            if (working && _workAction != FigureWorkAction.None)
            {
                ApplyWorkPose();
                return;
            }
            if (!string.IsNullOrEmpty(ActivityKind) && _blend < .1f)
            {
                if (ActivityKind == "court")
                {
                    float release = Mathf.Pow((1 + Mathf.Sin(_idleTime * 3.5f)) * .5f, 3f);
                    bool shooting = ActivityOption == 1;
                    Torso.localRotation = Quaternion.Euler(shooting ? 6 - 13 * release : 5 + 9 * release, 0, 0);
                    _visual.localPosition += Vector3.up * (shooting ? .09f * release * _scale : 0);
                    _arms[0].localRotation = Quaternion.Euler(shooting ? -67 - 83 * release : -44 - 48 * release, 0, -10);
                    _arms[1].localRotation = Quaternion.Euler(shooting ? -60 - 62 * release : -44 - 48 * release, 0, 10);
                    SetElbow(0, shooting ? 66 - 48 * release : 30);
                    SetElbow(1, shooting ? 57 - 36 * release : 34);
                    return;
                }
                if (ActivityKind == "golf")
                {
                    float swing = Mathf.Sin(_idleTime * (ActivityOption == 1 ? 2.5f : 1.6f));
                    Torso.localRotation = Quaternion.Euler(16, swing * 35, 0);
                    _arms[0].localRotation = Quaternion.Euler(-42 - 24 * swing, swing * 28, -12);
                    _arms[1].localRotation = Quaternion.Euler(-42 - 24 * swing, swing * 28, 12);
                    return;
                }
                float gesture=Mathf.Sin(_idleTime*2.4f)*12f;
                // Both participants share a readable activity pose, rather than a walking loop.
                bool energetic=ActivityKind=="hike";
                Torso.localRotation=Quaternion.Euler(energetic?8:0,Mathf.Sin(_idleTime*1.5f)*4,0);
                _arms[0].localRotation=Quaternion.Euler(ActivityKind=="ktv"?-100+gesture:-45+gesture,0,-12);
                _arms[1].localRotation=Quaternion.Euler(energetic?-65-gesture:-20-gesture,0,12);
                SetElbow(0, 31 + gesture);
                SetElbow(1, 18 - gesture * .5f);
            }
        }

        private static Vector2 WalkAnkle(float phase)
        {
            float halfTravel = WalkCycleDistance * WalkSupportFraction * .5f;
            // During support, the foot moves backwards relative to the body at the body's forward speed.
            if (phase < WalkSupportFraction)
                return new Vector2(Mathf.Lerp(halfTravel, -halfTravel, phase / WalkSupportFraction), 0);

            float swing = Mathf.InverseLerp(WalkSupportFraction, 1f, phase);
            float forward = Mathf.SmoothStep(-halfTravel, halfTravel, swing);
            float lift = Mathf.Sin(swing * Mathf.PI);
            return new Vector2(forward, .10f * lift * lift);
        }

        private void ApplyFurnitureSeatPose()
        {
            const float thighBend = 79f;
            const float segment = PlayerCharacterModel.LegSegment;
            const float ankle = PlayerCharacterModel.AnkleHeight;
            const float hip = PlayerCharacterModel.HipHeight;
            float blend = _furnitureSeatBlend;
            float seatedHip = _furnitureSeatHeight / _scale + .025f;
            float thighDrop = segment * Mathf.Cos(thighBend * Mathf.Deg2Rad);
            float shinBend = Mathf.Acos(Mathf.Clamp01((seatedHip - ankle - thighDrop) / segment)) * Mathf.Rad2Deg;
            float thigh = thighBend * blend;
            float shin = shinBend * blend;
            float pelvisHeight = ankle + segment * (Mathf.Cos(thigh * Mathf.Deg2Rad) + Mathf.Cos(shin * Mathf.Deg2Rad));
            float seatCorrection = seatedHip - (ankle + thighDrop + segment * Mathf.Cos(shinBend * Mathf.Deg2Rad));
            _visual.localPosition = Vector3.up * ((pelvisHeight - hip + seatCorrection * blend) * _scale);

            // Lean during the transition, then settle upright with the hands over the thighs.
            float lean = 4f * blend + 15f * Mathf.Sin(Mathf.PI * blend);
            Torso.localRotation = Quaternion.Euler(lean, 0, 0);
            Vector3 pelvis = Vector3.up * hip;
            Torso.localPosition = pelvis - Torso.localRotation * pelvis;
            for (int i = 0; i < 2; i++)
            {
                _hips[i].localRotation = Quaternion.Euler(-thigh, 0, 0);
                _knees[i].localRotation = Quaternion.Euler(thigh - shin, 0, 0);
                _feet[i].localRotation = Quaternion.Euler(shin, 0, 0);
                _arms[i].localRotation = Quaternion.Euler(-28f * blend, 0, (i == 0 ? -1 : 1) * (1.5f + 4f * blend));
                SetElbow(i, 6f + 19f * blend);
            }
        }

        private void ApplyWorkPose()
        {
            SetElbow(0, 12);
            SetElbow(1, 12);
            switch (_workAction)
            {
                case FigureWorkAction.Cook:
                    float stir = _idleTime * 6f;
                    Torso.localRotation = Quaternion.Euler(10, Mathf.Sin(stir) * 7, 0);
                    _arms[0].localRotation = Quaternion.Euler(-91, -7, 12);
                    _arms[1].localRotation = Quaternion.Euler(-100 + Mathf.Cos(stir) * 14, -21 + Mathf.Sin(stir) * 22, -13);
                    break;
                case FigureWorkAction.Assemble:
                    float turn = _idleTime * 7f;
                    float press = Mathf.Pow((1 + Mathf.Sin(turn * .5f)) * .5f, 2);
                    Torso.localRotation = Quaternion.Euler(13 + 7 * press, -4, -3);
                    _arms[0].localRotation = Quaternion.Euler(-99 + 4 * press, -8, 13);
                    _arms[1].localRotation = Quaternion.Euler(-112 + 11 * press, -18 + Mathf.Sin(turn) * 5, -12);
                    _screwdriver.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(turn) * 35);
                    break;
                case FigureWorkAction.Plate:
                case FigureWorkAction.Serve:
                case FigureWorkAction.Clear:
                    float lift = Mathf.Sin(_idleTime * 4f);
                    float reach = _workAction == FigureWorkAction.Clear ? -53 : -72;
                    Torso.localRotation = Quaternion.Euler(10 + lift * 4, 0, 0);
                    _arms[0].localRotation = Quaternion.Euler(reach + lift * 13, 0, 12);
                    _arms[1].localRotation = Quaternion.Euler(reach + lift * 13, 0, -12);
                    break;
            }
        }

        private void SetElbow(int side, float degrees)
        {
            if (_elbows[side] != null)
                _elbows[side].localRotation = Quaternion.Euler(-degrees, 0, 0);
        }
    }
}
