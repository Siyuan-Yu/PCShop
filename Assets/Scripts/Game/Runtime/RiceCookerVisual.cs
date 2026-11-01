using PCShopSell.Core.Home;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>Visual-only cooking/keep-warm states, driven by the home's existing pantry.</summary>
    public sealed class RiceCookerVisual : MonoBehaviour
    {
        public Renderer Indicator;
        public Transform Lid;
        public Renderer[] Steam;
        private MaterialPropertyBlock _properties;
        private float _phase;
        private int _state = -1;
        private static readonly Vector3 LidRest = new Vector3(-.35f, 1.045f, 0);

        public void Tick(Pantry pantry, float dt)
        {
            if (pantry == null) return;
            // Unity-native objects are created on the update thread, including after script reloads.
            if (_properties == null) _properties = new MaterialPropertyBlock();
            int state = pantry.Cooking != null ? 1 : pantry.CookedBowls > 0 ? 2 : 0;
            if (state != _state) { _state = state; _phase = 0; }
            _phase += Mathf.Max(0, dt);
            bool cooking = state == 1, warm = state == 2;
            Color light = cooking ? new Color(1f, .42f, .08f) : warm ? new Color(.18f, .95f, .35f) : new Color(.18f, .2f, .17f);
            float pulse = cooking ? .7f + .3f * Mathf.Sin(_phase * 3f) : warm ? .85f : 0;
            _properties.Clear();
            _properties.SetColor("_BaseColor", light);
            _properties.SetColor("_Color", light);
            _properties.SetColor("_EmissionColor", light * (2.5f * pulse));
            if (Indicator != null) Indicator.SetPropertyBlock(_properties);
            if (Lid != null)
            {
                Lid.localPosition = LidRest + Vector3.up * (cooking ? .002f * Mathf.Sin(_phase * 13f) : 0);
                Lid.localRotation = cooking ? Quaternion.Euler(.45f * Mathf.Sin(_phase * 11f), 0, .3f * Mathf.Sin(_phase * 9f)) : Quaternion.identity;
            }
            if (Steam == null) return;
            for (int i = 0; i < Steam.Length; i++)
            {
                var puff = Steam[i]; if (puff == null) continue;
                bool visible = cooking || warm && i < 2;
                puff.gameObject.SetActive(visible);
                if (!visible) continue;
                float t = Mathf.Repeat(_phase * (cooking ? .58f : .25f) + i / (float)Steam.Length, 1);
                puff.transform.localPosition = new Vector3(-.24f + .025f * Mathf.Sin(_phase * 1.8f + i), 1.075f + t * (cooking ? .38f : .2f), .025f + .018f * Mathf.Cos(_phase + i));
                float size = Mathf.Lerp(.025f, cooking ? .085f : .05f, t);
                puff.transform.localScale = new Vector3(size, size * 1.15f, size);
                var color = new Color(.94f, .97f, 1f, Mathf.Sin(t * Mathf.PI) * (cooking ? .42f : .2f));
                _properties.Clear(); _properties.SetColor("_BaseColor", color); _properties.SetColor("_Color", color);
                puff.SetPropertyBlock(_properties);
            }
        }
    }
}
