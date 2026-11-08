using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>Load each vehicle thumbnail once; file names follow MotorCatalog model IDs.</summary>
    public static class MotorPortraitArt
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        public static Texture2D Get(string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return null;
            if (!Cache.TryGetValue(modelId, out var texture))
                Cache[modelId] = texture = Resources.Load<Texture2D>("VehiclePortraits/" + modelId);
            return texture;
        }
    }
}
