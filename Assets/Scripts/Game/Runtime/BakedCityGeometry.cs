using UnityEngine;
namespace PCShopSell.Game.Runtime
{
    // Binary subassets avoid bloated YAML for mesh buffers. Shared geometry is stored once.
    [PreferBinarySerialization]
    public sealed class BakedCityGeometry : ScriptableObject
    {
        public int MeshCount,MaterialCount,TextureCount,ForestPlacements;
        public long VertexCount;
    }
}
