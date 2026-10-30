using NUnit.Framework;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class FigureMotionTests
    {
        private GameObject _figure;
        private FigureMotion _motion;
        private const float Dt = 1f / 60f;

        [SetUp]
        public void SetUp()
        {
            _figure = Prims.Figure(null, "MotionTest", Color.white, Color.gray, 1.1f);
            _motion = _figure.GetComponent<FigureMotion>();
            _motion.Advance(Dt);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_figure);

        private void Walk(int frames = 30)
        {
            for (int i = 0; i < frames; i++)
            {
                _figure.transform.position += _figure.transform.forward * (1.8f * Dt);
                _motion.Advance(Dt);
            }
        }

        [Test]
        public void Walking_AlternatesFeetAndArms_AndKeepsSupportFootOnFloor()
        {
            Transform left = _figure.transform.Find("Visual/HipL/KneeL/FootL");
            Transform right = _figure.transform.Find("Visual/HipR/KneeR/FootR");
            Transform hand = _motion.Torso.Find("ShoulderL/Hand");
            bool leftForward = false, rightForward = false, lifted = false, oppositeArm = false;
            for (int i = 0; i < 120; i++)
            {
                Walk(1);
                float l = left.TransformPoint(0, -.08f, 0).y;
                float r = right.TransformPoint(0, -.08f, 0).y;
                Assert.That(l, Is.GreaterThanOrEqualTo(-.002f), "left shoe must not penetrate the floor");
                Assert.That(r, Is.GreaterThanOrEqualTo(-.002f), "right shoe must not penetrate the floor");
                Assert.That(Mathf.Min(l, r), Is.LessThan(.004f), "one foot must support the figure");
                leftForward |= left.position.z - right.position.z > .2f;
                rightForward |= right.position.z - left.position.z > .2f;
                lifted |= Mathf.Max(l, r) > .08f;
                oppositeArm |= left.position.z - _figure.transform.position.z > .1f && hand.position.z - _figure.transform.position.z < -.03f;
            }
            Assert.IsTrue(leftForward && rightForward && lifted && oppositeArm);
            Assert.That(_figure.transform.position.y, Is.Zero, "animation must never lift the gameplay root");
        }

        [Test]
        public void StoppingOrBeingBlocked_ReturnsToRest_WithoutMovingTheRoot()
        {
            Walk();
            Vector3 stopped = _figure.transform.position;
            for (int i = 0; i < 30; i++) _motion.Advance(Dt);
            Assert.That(_motion.WalkBlend, Is.Zero);
            Assert.AreEqual(stopped, _figure.transform.position);
            Assert.That(_figure.transform.Find("Visual/HipL/KneeL/FootL").position.z, Is.EqualTo(stopped.z).Within(.001f));
        }

        [Test]
        public void PausingFreezesPose_AndResumingContinuesMovement()
        {
            Walk();
            Transform knee = _figure.transform.Find("Visual/HipL/KneeL");
            Quaternion pose = knee.localRotation;
            for (int i = 0; i < 30; i++) _motion.Advance(Dt, false);
            Assert.AreEqual(pose, knee.localRotation);
            Walk(7);
            Assert.That(Quaternion.Angle(pose, knee.localRotation), Is.GreaterThan(2f));
        }

        [Test]
        public void TeleportAndReactivation_DoNotBecomeSteps()
        {
            Walk();
            _figure.transform.position += Vector3.right * 500f;
            _motion.Advance(Dt);
            Assert.That(_motion.WalkBlend, Is.Zero);
            _figure.SetActive(false);
            _figure.transform.position += Vector3.forward * .5f;
            FigureMotion.AdvanceAll(Dt, true);
            _figure.SetActive(true);
            FigureMotion.AdvanceAll(Dt, true);
            Assert.That(_motion.WalkBlend, Is.Zero);
        }

        [Test]
        public void VehiclesAndTurningInPlace_DoNotPlayWalking()
        {
            for (int i = 0; i < 20; i++)
            {
                _figure.transform.Rotate(0, 9, 0);
                _motion.Advance(Dt);
            }
            Assert.That(_motion.WalkBlend, Is.Zero);
            _motion.LocomotionEnabled = false;
            Walk();
            Assert.That(_motion.WalkBlend, Is.Zero);
            _motion.LocomotionEnabled = true;
            Walk();
            Assert.That(_motion.WalkBlend, Is.GreaterThan(.9f));
        }

        [Test]
        public void ClothesFollowTorso_AndRecolorBothLegSegments()
        {
            Prims.Dress(_figure, "suit");
            Walk();
            Assert.NotNull(_motion.Torso.Find("Outfit_Tie"));
            foreach (string side in new[] { "L", "R" })
            {
                var upper = _figure.transform.Find("Visual/Hip" + side + "/Leg" + side).GetComponent<Renderer>();
                var lower = _figure.transform.Find("Visual/Hip" + side + "/Knee" + side + "/Shin" + side).GetComponent<Renderer>();
                Assert.AreSame(upper.sharedMaterial, lower.sharedMaterial);
            }
            Prims.Dress(_figure, "jacket");
            Assert.IsNull(_motion.Torso.Find("Outfit_Tie"));
            Assert.NotNull(_motion.Torso.Find("Outfit_Zip"));
        }

        [Test]
        public void ZeroTimeRendering_DoesNotAlterTheWalkingPose()
        {
            Walk();
            var knee = _figure.transform.Find("Visual/HipL/KneeL");
            Quaternion pose = knee.localRotation;
            _motion.Advance(0);
            Assert.AreEqual(pose, knee.localRotation);
        }
    }
}
