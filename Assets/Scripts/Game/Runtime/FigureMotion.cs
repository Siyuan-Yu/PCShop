using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public enum FigureWorkAction { None, Cook, Plate, Serve, Clear, Assemble }

    /// <summary>Procedural poses for the miniature figures. Only the visual rig moves; gameplay roots stay on the ground.</summary>
    public sealed class FigureMotion : MonoBehaviour
    {
        private const float WalkCycleDistance = .88f;
        private const float WalkSupportFraction = .60f;
        private const float ChopstickReach = .18f;
        private static readonly Vector3 ChopstickGrip = new Vector3(0, -.033f, 0);
        private static readonly List<FigureMotion> Active = new List<FigureMotion>();
        private Transform _visual, _head;
        private bool _paintedSample;
        private PlayerCharacterModel _character;
        private Transform _hairTailL, _hairTailR;
        private Transform _garmentHem;
        private readonly Transform[] _hips = new Transform[2], _knees = new Transform[2], _feet = new Transform[2], _arms = new Transform[2], _hands = new Transform[2];
        private readonly Transform[] _elbows = new Transform[2];
        private Vector3 _previousPosition;
        private float _phase, _blend, _idleTime, _scale;
        private Vector3 _baseVisualScale;
        private float _baseScale, _quarterWeight;
        private bool _sampled, _locomotion = true;
        private bool _seated;
        private Transform _scooter;
        private float _furnitureSeatHeight, _furnitureSeatBlend;
        private (float progress, float surface, Vector3 braceHand, Vector3 standing, int braceArm)? _bedWakePose;
        private Vector3? _diningFoodPosition;
        private float _diningPhase;
        private GameObject _diningChopsticks;
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

        internal static float WakeEase(float progress, float start, float end) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, progress));

        internal void SetBedWakePose(float progress, float surface, Vector3 braceHand, Vector3 standing, int braceArm)
        {
            _bedWakePose = (progress, surface, braceHand, standing, braceArm);
            ApplyPose();
        }

        internal void ClearBedWakePose()
        {
            if (!_bedWakePose.HasValue) return;
            _hands[_bedWakePose.Value.braceArm].localRotation = Quaternion.identity;
            _bedWakePose = null;
            _head.localRotation = Quaternion.identity;
            ApplyPose();
        }

        internal void SetDiningSeatPose(float height, Vector3? foodPosition, int guestId)
        {
            if (_furnitureSeatHeight == height && _furnitureSeatBlend == 1f && _diningFoodPosition == foodPosition) return;
            _diningFoodPosition = foodPosition;
            _diningPhase = Mathf.Repeat(guestId * .618034f, 1f);
            if (foodPosition.HasValue && _diningChopsticks == null)
            {
                _diningChopsticks = new GameObject("DiningChopsticks");
                _diningChopsticks.transform.SetParent(_hands[1], false);
                _diningChopsticks.transform.localPosition = ChopstickGrip;
                var wood = Mats.Lit(ArtDirection.Wood);
                foreach (float x in new[] { -.011f, .011f })
                    Prims.Box(_diningChopsticks.transform, "Chopstick", new Vector3(x, 0, ChopstickReach * .5f),
                        new Vector3(.009f, .009f, ChopstickReach), wood);
            }
            if (_diningChopsticks != null) _diningChopsticks.SetActive(foodPosition.HasValue);
            if (!foodPosition.HasValue) _hands[1].localRotation = Quaternion.identity;
            SetFurnitureSeatPose(height, 1f);
        }

        internal void SetScooterPose(Transform scooter)
        {
            if (ReferenceEquals(_scooter, scooter)) return;
            _scooter = scooter;
            if (scooter == null)
                foreach (var hand in _hands) hand.localRotation = Quaternion.identity;
            ResetMotion();
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
            _head = torso.Find("Head");
            _character = GetComponent<PlayerCharacterModel>();
            _paintedSample = _character.PaintedSample;
            if (_paintedSample)
            {
                _hairTailL = _head.Find("HairTailL");
                _hairTailR = _head.Find("HairTailR");
                _garmentHem = visual.Find("GarmentHem");
            }
            _baseScale = _scale = scale;
            _baseVisualScale = visual.localScale;
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
            if (_paintedSample) tool.transform.localScale = Vector3.one * .78f;
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
        public static void AdvanceAll(float dt, bool animateIdle, Vector3? paintedStreet = null)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var motion = Active[i];
                if (motion == null) { Active.RemoveAt(i); continue; }
                if (!motion.isActiveAndEnabled) { motion.ResetMotion(); continue; }
                motion.ApplyStreetProportions(paintedStreet);
                motion.Advance(dt, animateIdle || motion.PresentationAnimation);
            }
        }

        private void ApplyStreetProportions(Vector3? center)
        {
            if (_visual == null) return;
            if (_paintedSample) return;
            float weight = 0;
            if (center.HasValue && !_seated && _scooter == null)
            {
                var delta = transform.position - center.Value;
                weight = Mathf.SmoothStep(1,0,Mathf.InverseLerp(22*22,36*36,delta.sqrMagnitude));
            }
            if (Mathf.Approximately(weight,_quarterWeight)) return;
            _quarterWeight = weight;
            // Readable heads and sleeves at street distance. Keep footsteps and
            // the stride solver on the same scale as the visible legs.
            float height = 1+.13f*weight;
            float width = 1+.18f*weight;
            _scale = _baseScale*height;
            _visual.localScale = Vector3.Scale(_baseVisualScale,new Vector3(width,height,width));
            _head.localScale = Vector3.one*(1+.16f*weight);
        }

        public void ResetMotion()
        {
            _sampled = false;
            _phase = _blend = 0;
            if (_visual != null) ApplyPose();
        }

        public void Advance(float dt, bool animateIdle = true)
        {
            if (_visual == null || _scooter != null || dt <= 0) return;
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
            ApplySkeletonPose();
            if (_garmentHem != null)
            {
                float seated = _seated ? 1 : _furnitureSeatBlend;
                float forwardLeg = Mathf.Min(0, Mathf.Min(Mathf.DeltaAngle(0, _hips[0].localEulerAngles.x), Mathf.DeltaAngle(0, _hips[1].localEulerAngles.x)));
                float lift = Mathf.Lerp(forwardLeg * .80f - 5f * _blend - 12f * _reachCrouch, -78f, seated);
                _garmentHem.localPosition = new Vector3(0,PlayerCharacterModel.HipHeight,.025f*_blend*(1-seated));
                _garmentHem.localRotation = Quaternion.Euler(lift, 0, 0);
                _character.SetGarmentSeat(seated);
            }
        }

        private void ApplySkeletonPose()
        {
            if (_hairTailL != null)
            {
                float sway = Mathf.Sin(_phase * Mathf.PI * 2) * _blend * 5f;
                float idle = Mathf.Sin(_idleTime * 1.7f) * .8f * (1 - _blend);
                _hairTailL.localRotation = Quaternion.Euler(-2f * _blend, 0, sway + idle);
                _hairTailR.localRotation = Quaternion.Euler(-2f * _blend, 0, sway - idle);
            }
            bool working = !_bedWakePose.HasValue && _scooter == null && !_seated && _furnitureSeatBlend <= 0 && _reachWeight <= 0 && _blend < .1f;
            const float hipHeight = PlayerCharacterModel.HipHeight;
            const float segment = PlayerCharacterModel.LegSegment;
            const float ankle = PlayerCharacterModel.AnkleHeight;
            if (_spatula != null) _spatula.SetActive(working && _workAction == FigureWorkAction.Cook);
            if (_screwdriver != null) _screwdriver.SetActive(working && _workAction == FigureWorkAction.Assemble);
            if (_scooter != null)
            {
                ApplyScooterPose();
                return;
            }
            if (_bedWakePose.HasValue)
            {
                var (progress, surface, braceHand, standing, braceArm) = _bedWakePose.Value;
                ApplyBedWakePose(progress, surface, braceHand, standing, braceArm);
                return;
            }
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

        private void ApplyScooterPose()
        {
            Vector3 pelvis = Vector3.up * PlayerCharacterModel.HipHeight;
            _visual.localPosition = transform.InverseTransformPoint(_scooter.TransformPoint(MotorVisual.ScooterPelvis)) - pelvis * _scale;
            Torso.localRotation = Quaternion.Euler(15, 0, 0);
            Torso.localPosition = pelvis - Torso.localRotation * pelvis;

            Vector3 deck = new Vector3(0, MotorVisual.ScooterDeckHeight, MotorVisual.ScooterAnkleDepth);
            Vector3 ankle = _visual.InverseTransformPoint(_scooter.TransformPoint(deck));
            const float shoeSole = .014f;
            ankle.y += PlayerCharacterModel.AnkleHeight - shoeSole;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                ankle.x = _hips[i].localPosition.x * 1.25f;
                AimLimb(_hips[i], _knees[i], _feet[i], ankle, Vector3.forward);
                _feet[i].localRotation = Quaternion.Inverse(_hips[i].localRotation * _knees[i].localRotation);

                Vector3 grip = Torso.InverseTransformPoint(_scooter.TransformPoint(MotorVisual.ScooterGrip(side)));
                Quaternion handRotation = Quaternion.Euler(-25, 0, 0);
                Vector3 palmOffset = new Vector3(side * .006f, -.035f, .003f);
                AimLimb(_arms[i], _elbows[i], _hands[i], grip - handRotation * palmOffset, new Vector3(side * .4f, -1, 0));
                _hands[i].localRotation = Quaternion.Inverse(_arms[i].localRotation * _elbows[i].localRotation) * handRotation;
            }
        }

        // Solve the two segments in the upper joint's parent space so scaled figures reach the same contact points.
        private static void AimLimb(Transform upper, Transform joint, Transform end, Vector3 target, Vector3 bendDirection)
        {
            Vector3 reach = target - upper.localPosition;
            Vector3 direction = reach.normalized;
            float upperLength = joint.localPosition.magnitude;
            float lowerLength = end.localPosition.magnitude;
            float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(upperLength - lowerLength) + .001f, upperLength + lowerLength - .001f);
            float projected = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float offset = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - projected * projected));
            Vector3 bend = Vector3.ProjectOnPlane(bendDirection, direction).normalized;
            Vector3 jointOffset = direction * projected + bend * offset;
            upper.localRotation = Quaternion.FromToRotation(joint.localPosition, jointOffset);
            joint.localRotation = Quaternion.FromToRotation(end.localPosition,
                Quaternion.Inverse(upper.localRotation) * (direction * distance - jointOffset));
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
            if (_diningFoodPosition is Vector3 food) ApplyDiningMealPose(food);
        }

        private void ApplyBedWakePose(float progress, float surface, Vector3 braceHand, Vector3 standing, int braceArm)
        {
            const float hipHeight = PlayerCharacterModel.HipHeight;
            const float ankleHeight = PlayerCharacterModel.AnkleHeight;
            float sitUp = WakeEase(progress, .08f, .52f);
            float tuckKnees = WakeEase(progress, .05f, .31f) * (1 - WakeEase(progress, .40f, .60f));
            float support = WakeEase(progress, .04f, .20f) * (1 - WakeEase(progress, .41f, .59f));
            float leanForward = WakeEase(progress, .57f, .78f) * (1 - WakeEase(progress, .80f, .98f));
            float rise = WakeEase(progress, .75f, 1f);
            float settle = WakeEase(progress, .96f, 1f);
            float seatedHeight = Mathf.Max(.15f * _scale, surface + .025f * _scale);
            float heightOnBed = Mathf.Lerp(surface + .10f * _scale, seatedHeight, sitUp) - .026f * _scale * leanForward;
            float pelvisHeight = Mathf.Lerp(heightOnBed, hipHeight * _scale, rise);
            _visual.localPosition = Vector3.up * (pelvisHeight - hipHeight * _scale);

            float braceSide = braceArm == 0 ? -1f : 1f;
            Vector3 pelvis = Vector3.up * hipHeight;
            Torso.localRotation = Quaternion.Euler(Mathf.Lerp(-86f, 4f, sitUp) * (1 - rise) + 29f * leanForward,
                -7f * braceSide * support, -10f * braceSide * support);
            Torso.localPosition = pelvis - Torso.localRotation * pelvis;
            _head.localRotation = Quaternion.Euler(19f * Mathf.Sin(sitUp * Mathf.PI) + 11f * leanForward,
                -7f * braceSide * Mathf.Sin(sitUp * Mathf.PI), 0);

            // Keep the floor contact in world space while the pelvis comes forward over the feet.
            Vector3 planted = _visual.InverseTransformPoint(standing) + Vector3.up * ankleHeight;
            for (int i = 0; i < 2; i++)
            {
                bool bracing = i == braceArm;
                float swing = bracing ? WakeEase(progress, .46f, .73f) : WakeEase(progress, .37f, .66f);
                float tuck = tuckKnees * (bracing ? .72f : 1f);
                float thigh = -90f - 37f * tuck;
                float shin = thigh + 6f + 70f * tuck;
                Vector3 onBed = _hips[i].localPosition + Quaternion.Euler(thigh, 0, 0) * _knees[i].localPosition
                    + Quaternion.Euler(shin, 0, 0) * _feet[i].localPosition;
                Vector3 onFloor = planted + Vector3.right * _hips[i].localPosition.x;
                Vector3 ankle = Vector3.Lerp(onBed, onFloor, swing) + Vector3.up * (.13f * Mathf.Sin(swing * Mathf.PI));
                AimLimb(_hips[i], _knees[i], _feet[i], ankle, Vector3.forward + Vector3.up * (1 - swing));
                _hips[i].localRotation = Quaternion.Slerp(_hips[i].localRotation, Quaternion.identity, settle);
                _knees[i].localRotation = Quaternion.Slerp(_knees[i].localRotation, Quaternion.identity, settle);
                _feet[i].localRotation = Quaternion.Inverse(_hips[i].localRotation * _knees[i].localRotation)
                    * Quaternion.Euler(Mathf.Lerp(-69f, 0, swing), 0, 0);

                float side = i == 0 ? -1f : 1f;
                _arms[i].localRotation = Quaternion.Euler((-6f - (bracing ? 6f : 24f) * sitUp) * (1 - rise),
                    0, side * Mathf.Lerp(10f, 1.5f, rise));
                SetElbow(i, Mathf.Lerp(12f + 15f * sitUp, 6f, rise));
                _hands[i].localRotation = Quaternion.identity;
                if (bracing && support > 0)
                {
                    Quaternion shoulder = _arms[i].localRotation;
                    Quaternion elbow = _elbows[i].localRotation;
                    AimLimb(_arms[i], _elbows[i], _hands[i], Torso.InverseTransformPoint(braceHand), new Vector3(side, 0, -.45f));
                    _arms[i].localRotation = Quaternion.Slerp(shoulder, _arms[i].localRotation, support);
                    _elbows[i].localRotation = Quaternion.Slerp(elbow, _elbows[i].localRotation, support);
                    Quaternion palm = Quaternion.Inverse(Torso.localRotation * _arms[i].localRotation * _elbows[i].localRotation)
                        * Quaternion.Euler(84, side * 16, 0);
                    _hands[i].localRotation = Quaternion.Slerp(Quaternion.identity, palm, support);
                }
            }
        }

        private void ApplyDiningMealPose(Vector3 foodPosition)
        {
            float cycle = Mathf.Repeat(_idleTime / 2.25f + _diningPhase, 1f);
            float raise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.16f, .47f, cycle));
            float lower = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.61f, .97f, cycle));
            float bite = raise - lower;
            Vector3 food = Torso.InverseTransformPoint(transform.TransformPoint(foodPosition)) + Vector3.up * .025f;
            Vector3 mouth = new Vector3(.02f, 1.37f, .145f);
            Vector3 tips = Vector3.Lerp(food, mouth, bite);
            Vector3 direction = Vector3.Slerp(new Vector3(-.26f, -.7f, .68f), new Vector3(-.52f, .48f, -.7f), bite).normalized;
            Quaternion grip = Quaternion.LookRotation(direction);
            AimLimb(_arms[1], _elbows[1], _hands[1], tips - grip * ChopstickGrip - direction * ChopstickReach,
                new Vector3(.65f, -1f, 0));
            _hands[1].localRotation = Quaternion.Inverse(_arms[1].localRotation * _elbows[1].localRotation) * grip;
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
                    _arms[0].localRotation = Quaternion.Euler(_paintedSample ? -55 : -91, -7, 12);
                    _arms[1].localRotation = Quaternion.Euler((_paintedSample ? -58 : -100) + Mathf.Cos(stir) * 14, -21 + Mathf.Sin(stir) * 22, -13);
                    break;
                case FigureWorkAction.Assemble:
                    float turn = _idleTime * 7f;
                    float press = Mathf.Pow((1 + Mathf.Sin(turn * .5f)) * .5f, 2);
                    Torso.localRotation = Quaternion.Euler((_paintedSample ? 8 + 4 * press : 13 + 7 * press), -4, -3);
                    _arms[0].localRotation = Quaternion.Euler((_paintedSample ? -56 : -99) + 4 * press, -8, 13);
                    _arms[1].localRotation = Quaternion.Euler((_paintedSample ? -63 : -112) + 11 * press, -18 + Mathf.Sin(turn) * 5, -12);
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
