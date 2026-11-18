using UnityEngine;
namespace PCShopSell.Game.Runtime
{
    // Shared immutable placement records are not deep-copied when the map prefab is instantiated.
    [PreferBinarySerialization]
    public sealed class BakedForestLayout : ScriptableObject
    {
        public BakedCityMap.ForestChunk[] Chunks;
    }
}
