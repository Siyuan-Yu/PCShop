using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using UnityEditor;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    /// <summary>随机事件工具：看清单、校验 events.json、运行时一键触发某个事件。</summary>
    public sealed class EventToolsWindow : EditorWindow
    {
        private const string JsonPath = "Assets/Resources/Data/events.json";
        private Vector2 _scroll;
        private string _filter = "";
        private string _lastResult = "";

        [MenuItem("PCShop/事件/事件清单与触发")]
        public static void Open() => GetWindow<EventToolsWindow>("事件清单").Show();

        [MenuItem("PCShop/事件/校验 events.json")]
        public static void ValidateMenu()
        {
            EventCatalog.Reload();
            var all = EventCatalog.All;
            var errors = EventCatalog.LoadErrors;
            if (errors.Count == 0) EditorUtility.DisplayDialog("events.json", $"没有问题，共 {all.Count} 个事件。", "好");
            else EditorUtility.DisplayDialog("events.json 有 " + errors.Count + " 个问题", string.Join("\n", errors.Take(25)), "好");
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开 events.json", GUILayout.Width(130)))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath));
                if (GUILayout.Button("重新读取并校验", GUILayout.Width(130))) { EventCatalog.Reload(); _lastResult = Summary(); }
                GUILayout.Label("搜索", GUILayout.Width(32));
                _filter = EditorGUILayout.TextField(_filter);
            }
            if (!string.IsNullOrEmpty(_lastResult)) EditorGUILayout.HelpBox(_lastResult, EventCatalog.LoadErrors.Count == 0 ? MessageType.Info : MessageType.Error);

            var session = Application.isPlaying ? FindObjectOfType<GameController>()?.Session : null;
            EditorGUILayout.LabelField("每天 11/15/20 点各抽一次，概率（前 14 天减半）", GameSession.EventSlotChance.ToString("0.##"));
            if (!Application.isPlaying) EditorGUILayout.HelpBox("进入 Play 后才能点“触发”。", MessageType.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var def in EventCatalog.All)
            {
                if (!string.IsNullOrEmpty(_filter) && !(def.Id + def.Title).Contains(_filter)) continue;
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label($"{def.Title}  [{def.Id}]  第{def.MinDay}天起 · 冷却{def.CooldownDays}天 · 权重{def.Weight}"
                        + (string.IsNullOrEmpty(def.Group) ? "" : " · " + def.Group) + (def.When != null ? " · 有条件" : "") + $" · {def.Choices.Count}个选项");
                    GUI.enabled = session != null && session.PendingEvent == null;
                    if (GUILayout.Button("触发", GUILayout.Width(50))) session.RaiseEvent(def.Id);
                    GUI.enabled = true;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static string Summary() => EventCatalog.LoadErrors.Count == 0
            ? $"没有问题，共 {EventCatalog.All.Count} 个事件。"
            : string.Join("\n", EventCatalog.LoadErrors.Take(20));
    }
}