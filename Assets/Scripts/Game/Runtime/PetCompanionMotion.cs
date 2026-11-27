using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // 事件猫狗复用寄养模型的六组关节；移动产生步态，停下后轻点头、摆尾。
    public sealed class PetCompanionMotion : MonoBehaviour
    {
        private Transform _head, _tail;
        private Transform[] _legs;
        private bool _dog, _hasPrevious;
        private Vector3 _previous;
        private float _phase, _walk, _clock;
        internal bool Playing = true;

        internal void Bind(bool dog, Transform head, Transform tail, Transform[] legs)
        { _dog = dog; _head = head; _tail = tail; _legs = legs; }

        internal void ResetTravel() { _hasPrevious = false; _walk = 0; }

        internal void Advance(float dt, bool active)
        {
            if (_head == null || _legs == null) return;
            Vector3 position = transform.position;
            Vector3 movement = _hasPrevious ? position - _previous : Vector3.zero;
            movement.y = 0;
            _previous = position; _hasPrevious = true;
            // 切换室内外产生的跳跃不算走路。
            float distance = movement.magnitude;
            if (distance > 1) { distance = 0; movement = Vector3.zero; }
            if (active)
            {
                _clock += dt;
                _phase = Mathf.Repeat(_phase + distance * (_dog ? 13 : 17), Mathf.PI * 2);
                _walk = Mathf.MoveTowards(_walk, Mathf.Clamp01(distance / Mathf.Max(.001f, dt * .45f)), dt * 9);
                if (movement.sqrMagnitude > .00001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(movement), dt * 350);
            }
            _head.localRotation = Quaternion.Euler(Mathf.Sin(_clock * 2.1f) * 2, 0, 0);
            _tail.localRotation = Quaternion.Euler(Mathf.Sin(_clock * 2) * 5, Mathf.Sin(_clock * (_dog ? 7 : 3)) * (_dog ? 17 : 12), 0);
            for (int i = 0; i < _legs.Length; i++)
                _legs[i].localRotation = Quaternion.Euler(Mathf.Sin(_phase + (i == 0 || i == 3 ? 0 : Mathf.PI)) * 23 * _walk, 0, 0);
        }

        private void LateUpdate() => Advance(Time.deltaTime, Playing);
    }
}
