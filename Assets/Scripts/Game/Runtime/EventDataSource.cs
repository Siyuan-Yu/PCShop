using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>把 Resources/Data/events.json 接给 Core 的事件目录（Core 不能引用 Unity）。</summary>
    public static class EventDataSource
    {
        public static string Read() => Resources.Load<TextAsset>("Data/events")?.text;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InstallAtRuntime() => EventCatalog.JsonSource = Read;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallInEditor() => EventCatalog.JsonSource = Read;
#endif
    }
}