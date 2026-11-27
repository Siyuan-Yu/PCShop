using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // 一处登记所有出场入口的外形与职业服装；换场景、天气变化不会随机丢掉人物身份。
    internal static class NpcArtCatalog
    {
        [Serializable] internal sealed class Profile
        {
            public string id, head, cut, bottom, top, layer, accent, trousers, skin, hair;
            public float height=1, width=1;
            internal void Dress(PlayerCharacterModel model)=>model.DressNpc(cut,ArtDirection.Hex(top),ArtDirection.Hex(trousers),ArtDirection.Hex(layer),ArtDirection.Hex(accent),bottom);
        }
        [Serializable] private sealed class Data { public Profile[] profiles; }
        private static Dictionary<string,Profile> _profiles;
        // 首次出现前在加载界面准备共享网格、坐姿形变和材质。临时人物逐个销毁，
        // 不把整座城市的人物实例常驻到游戏里，也不改变实际出场数量和服装。
        internal static IEnumerator Prewarm()
        {
            Find("landlady");
            var root = new GameObject("人物资源预备");
            root.SetActive(false);
            try
            {
                foreach (var profile in _profiles.Values)
                {
                    var figure = Prims.FigureForPerson(root.transform, profile.id,
                        ArtDirection.Hex(profile.top), ArtDirection.Hex(profile.skin), 1, profile.id);
                    profile.Dress(figure.GetComponent<PlayerCharacterModel>());
                    UnityEngine.Object.Destroy(figure);
                    yield return null;
                }
            }
            finally { UnityEngine.Object.Destroy(root); }
        }

        internal static Profile Find(string id)
        {
            if(string.IsNullOrEmpty(id))return null;
            if(_profiles==null)
            {
                _profiles=new Dictionary<string,Profile>(StringComparer.Ordinal);
                var asset=Resources.Load<TextAsset>("Characters/NpcArtCatalog");
                if(asset!=null)foreach(var row in JsonUtility.FromJson<Data>(asset.text).profiles)_profiles.Add(row.id,row);
            }
            if(_profiles.TryGetValue(id,out var profile))return profile;
            // 现场临时裁缝沿用服装店身份；随机顾客/员工由 NpcLook 的稳定种子分配。
            return id.StartsWith("tailor_garden_",StringComparison.Ordinal)?_profiles["tailor_garden_1"]:null;
        }
    }
}
