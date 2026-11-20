using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static PCShopSell.Core.Session.OnsitePlayState;

namespace PCShopSell.Game.Runtime
{
    internal sealed class FootballPenaltyView
    {
        private const float SurfaceY = .14f;
        private const float NetDepth = .85f;
        private readonly Transform _court, _stage, _ball, _player, _keeper, _net;
        private readonly Transform _guide, _reticle;
        private readonly Transform[] _dashes = new Transform[14];
        private readonly List<GameObject> _basketballProps = new List<GameObject>();
        private readonly Camera _camera;
        private readonly FigureMotion _playerMotion, _keeperMotion;
        private readonly float _goalZ, _spotZ;

        internal FootballPenaltyView(PlaceVisual venue, GameSession session, Camera worldCamera, RenderTexture texture)
        {
            _court = (venue.VenueInterior ?? venue.Root).transform;
            _goalZ = (float)venue.Place.Depth - 2.1f;
            _spotZ = _goalZ - FootballGoalDistance;
            foreach (Transform child in _court)
            {
                if (!child.gameObject.activeSelf || !BasketballProp(child.name)) continue;
                _basketballProps.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }

            _stage = Child(_court, "FootballPenaltyPractice");
            _net = Child(_stage, "GoalNet");
            _net.localPosition = FieldPoint(0, 0, _goalZ);
            BuildGoal();
            BuildMarkings();
            _ball = BuildBall();

            var shooter = Prims.Figure(_stage, "PenaltyPlayer", ArtDirection.Clay, ArtDirection.Cream, 1, FigureAppearance.Player);
            Prims.Dress(shooter, session.EquippedOutfit);
            _player = shooter.transform;
            _playerMotion = shooter.GetComponent<FigureMotion>();
            _playerMotion.LocomotionEnabled = false;

            var personId = session.RunningCityActivity.Participants.FirstOrDefault()?.PersonId;
            var keeper = personId == null
                ? Prims.Figure(_stage, "PracticeKeeper", ArtDirection.Sage, ArtDirection.Cream, 1, FigureAppearance.Coach)
                : Prims.FigureForPerson(_stage, session.CityPerson(personId)?.Name, CrowdView.PersonClothing(personId),
                    ArtDirection.Cream, 1, personId);
            if (personId != null) CityNpcClothing.Apply(keeper, session, personId);
            _keeper = keeper.transform;
            _keeper.localRotation = Quaternion.Euler(0, 175f, 0);
            _keeperMotion = keeper.GetComponent<FigureMotion>();
            _keeperMotion.LocomotionEnabled = false;

            _camera = Child(_stage, "PenaltyCamera").gameObject.AddComponent<Camera>();
            _camera.CopyFrom(worldCamera);
            _camera.enabled = false;
            _camera.orthographic = false;
            _camera.fieldOfView = 44;
            _camera.nearClipPlane = .15f;
            _camera.farClipPlane = 200f;
            _camera.targetTexture = texture;
            _camera.aspect = (float)texture.width / texture.height;
            _camera.transform.position = _court.TransformPoint(new Vector3(-6.8f, 8, -.7f));
            _camera.transform.LookAt(_court.TransformPoint(new Vector3(.1f, 1.1f, 8.45f)));
            var pipeline = _camera.GetUniversalAdditionalCameraData();
            var worldPipeline = worldCamera.GetUniversalAdditionalCameraData();
            pipeline.renderPostProcessing = worldPipeline.renderPostProcessing;
            pipeline.antialiasing = worldPipeline.antialiasing;

            _guide = Child(_stage, "AimGuide");
            var gold = Mats.Unlit(ArtDirection.Hex("E5C47F"));
            for (int i = 0; i < _dashes.Length; i++)
                _dashes[i] = Prims.Box(_guide, "Dash", Vector3.zero, new Vector3(.024f, .024f, .25f), gold).transform;
            _reticle = Child(_guide, "GoalReticle");
            var ring = _reticle.gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.material = Mats.Unlit(ArtDirection.Hex("FFF4DE"));
            ring.widthMultiplier = .021f;
            ring.positionCount = 32;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * 2 * Mathf.PI / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .18f);
            }
        }

        internal void Render(OnsitePlayState shot, bool shotsLeft, Color background)
        {
            _camera.backgroundColor = background;
            var start = FieldPoint(0, FootballBallRadius, _spotZ);
            _ball.localPosition = FieldPoint((float)shot.BallX, (float)shot.BallY, _spotZ + (float)shot.BallZ);
            _ball.localRotation = Quaternion.Euler((float)shot.BallZ * 108, 0, (float)shot.BallX * -108);

            var target = FieldPoint((float)shot.FootballAimX, FootballAimHeight, _goalZ);
            float followThrough = shot.Phase == "flight" ? Mathf.Clamp01(1 - (float)shot.Flight / .29f) : 0;
            _player.localPosition = FieldPoint(.57f - .22f * followThrough, 0, _spotZ - .65f + .29f * followThrough);
            Vector3 direction = target - _player.localPosition;
            direction.y = 0;
            _player.localRotation = Quaternion.LookRotation(direction);
            _playerMotion.SetKickPose(followThrough);

            _keeper.localPosition = FieldPoint((float)shot.DefenderX, 0, _goalZ - .2f);
            float reach = shot.Phase == "flight" ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((float)(shot.Flight / FootballShotSeconds))) : 0;
            var savePoint = FieldPoint((float)shot.LandingX, (float)shot.Distance, _goalZ);
            _keeperMotion.SetReachPose(_stage.TransformPoint(savePoint), reach, .22f, shot.LandingX > shot.ShotOriginX);

            float netHit = shot.Phase == "flight" && shot.LastSportResult == OnsiteSportResult.Success
                ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((float)((shot.Flight - FootballGoalLineTime) / (FootballShotSeconds - FootballGoalLineTime)))) : 0;
            _net.localScale = new Vector3(1, 1, 1 + netHit * .22f);

            bool showGuide = shotsLeft && shot.FootballPreparing;
            _guide.gameObject.SetActive(showGuide);
            if (showGuide)
            {
                Vector3 aim = target - start;
                _guide.localPosition = start;
                _guide.localRotation = Quaternion.LookRotation(aim);
                for (int i = 0; i < _dashes.Length; i++)
                    _dashes[i].localPosition = Vector3.forward * (aim.magnitude * (i + 1) / (_dashes.Length + 1));
                _reticle.position = _stage.TransformPoint(target);
                _reticle.rotation = _camera.transform.rotation;
            }
            _camera.Render();
        }

        internal bool TryAim(Vector2 viewport, out float x)
        {
            x = 0;
            if (viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
            var ray = _camera.ViewportPointToRay(viewport);
            var plane = new Plane(_court.forward, _court.TransformPoint(new Vector3(0, 0, _goalZ)));
            if (!plane.Raycast(ray, out float distance)) return false;
            var point = _court.InverseTransformPoint(ray.GetPoint(distance));
            x = point.x;
            return Mathf.Abs(x) <= FootballGoalHalfWidth && point.y >= SurfaceY && point.y <= SurfaceY + FootballGoalHeight;
        }

        internal void Close()
        {
            if (_stage != null)
            {
                _stage.gameObject.SetActive(false);
                Kill.It(_stage.gameObject);
            }
            foreach (var prop in _basketballProps)
                if (prop != null) prop.SetActive(true);
        }

        private void BuildGoal()
        {
            var frame = Mats.Lit(ArtDirection.Cream);
            var cord = Mats.Lit(ArtDirection.Hex("AEBBB2"));
            Vector3 Front(float x, float y, float depth = 0) => FieldPoint(x, y, _goalZ + depth);

            foreach (float side in new[] { -FootballGoalHalfWidth, FootballGoalHalfWidth })
            {
                Rod(_stage, Front(side, 0), Front(side, FootballGoalHeight), .11f, frame);
                Rod(_stage, Front(side, .03f), Front(side, .03f, NetDepth), .08f, frame);
                Rod(_stage, Front(side, FootballGoalHeight), Front(side, .05f, NetDepth), .07f, frame);
                for (int i = 1; i < 4; i++)
                {
                    float t = i / 4f;
                    Rod(_net, new Vector3(side, FootballGoalHeight * t, 0),
                        new Vector3(side, FootballGoalHeight * t, NetDepth * (1 - t)), .018f, cord);
                }
            }
            Rod(_stage, Front(-FootballGoalHalfWidth, FootballGoalHeight),
                Front(FootballGoalHalfWidth, FootballGoalHeight), .11f, frame);
            for (int i = 1; i < 17; i++)
            {
                float x = Mathf.Lerp(-FootballGoalHalfWidth, FootballGoalHalfWidth, i / 17f);
                Rod(_net, new Vector3(x, FootballGoalHeight, 0), new Vector3(x, 0, NetDepth), .021f, cord);
            }
            for (int i = 0; i <= 7; i++)
            {
                float t = i / 7f;
                float y = FootballGoalHeight * t, depth = NetDepth * (1 - t);
                Rod(_net, new Vector3(-FootballGoalHalfWidth, y, depth),
                    new Vector3(FootballGoalHalfWidth, y, depth), .021f, cord);
            }
        }

        private void BuildMarkings()
        {
            const float halfWidth = 4.2f;
            const float depth = 3.9f;
            var paint = Mats.Unlit(ArtDirection.Cream);
            foreach (float x in new[] { -halfWidth, halfWidth })
                Prims.Box(_stage, "PenaltyAreaLine", FieldPoint(x, 0, _goalZ - depth / 2), new Vector3(.075f, .017f, depth), paint);
            foreach (float z in new[] { _goalZ, _goalZ - depth })
                Prims.Box(_stage, "PenaltyAreaLine", FieldPoint(0, 0, z), new Vector3(halfWidth * 2, .017f, .075f), paint);
            Prims.Shape(PrimitiveType.Cylinder, _stage, "PenaltySpot", FieldPoint(0, 0, _spotZ), new Vector3(.24f, .012f, .24f), paint);
        }

        private Transform BuildBall()
        {
            var assembly = Child(_stage, "Football");
            Prims.Shape(PrimitiveType.Sphere, assembly, "BallShell", Vector3.zero,
                Vector3.one * (2 * FootballBallRadius), Mats.Lit(ArtDirection.Cream));
            var dark = Mats.Lit(ArtDirection.Ink);
            for (int i = 0; i < 10; i++)
            {
                float y = i / 9f * 2 - 1, angle = i * 2.4f;
                float radial = Mathf.Sqrt(1 - y * y);
                var normal = new Vector3(Mathf.Sin(angle) * radial, y, Mathf.Cos(angle) * radial);
                var panel = Prims.Shape(PrimitiveType.Sphere, assembly, "BallPanel", normal * (FootballBallRadius - .005f),
                    new Vector3(.10f, .025f, .10f), dark);
                panel.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
            }
            return assembly;
        }

        private static Vector3 FieldPoint(float x, float y, float z) => new Vector3(x, SurfaceY + y, z);

        private static bool BasketballProp(string name) => name is "Basketball" or "HoopPost" or "Backboard" or "CenterLine"
            or "EntranceFascia" or "SignSupportLeft" or "SignSupportRight" or "Painted lettering"
            || name.StartsWith("Court markings and open hoop net", System.StringComparison.Ordinal);

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void Rod(Transform parent, Vector3 start, Vector3 end, float width, Material material)
        {
            var delta = end - start;
            var rod = Prims.Box(parent, "GoalRod", (start + end) / 2, new Vector3(width, width, delta.magnitude), material);
            rod.transform.localRotation = Quaternion.LookRotation(delta);
        }
    }
}
