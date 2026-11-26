using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Baked city interiors and temporary room views both receive the active campaign's sky.
    public sealed class InteriorWindowLight : MonoBehaviour
    {
        [SerializeField] private Light source;
        public void Initialize(Light lamp) => source=lamp;
        private void LateUpdate()
        {
            if(!source) return;
            float night=Shader.GetGlobalFloat(Night);
            float intensity=Mathf.Lerp(.78f,.07f,night);
            if(Mathf.Abs(source.intensity-intensity)>.002f) source.intensity=intensity;
        }
        private static readonly int Night=Shader.PropertyToID("_OldTownNightFactor");
    }
}
