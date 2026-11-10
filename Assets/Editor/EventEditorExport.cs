using System.IO;
using System.Linq;
using System.Text;
using PCShopSell.Core.Session;
using UnityEditor;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    /// <summary>把游戏里所有真实人物的 id / 名字 / 身份导出给外部事件编辑器（ExternalTools/EventEditor）做下拉用。</summary>
    public static class EventEditorExport
    {
        public const string Target = "ExternalTools/EventEditor/npcs.json";

        [MenuItem("PCShop/事件/导出人物名单给事件编辑器")]
        public static void Export()
        {
            var sb = new StringBuilder("[\n");
            var people = CityLifeCatalog.People.ToList();
            for (int i = 0; i < people.Count; i++)
            {
                var p = people[i];
                sb.Append("  {\"id\": ").Append(EventScriptWriter.Str(p.Id)).Append(", \"name\": ").Append(EventScriptWriter.Str(p.Name ?? p.Id))
                  .Append(", \"role\": ").Append(EventScriptWriter.Str(p.Role ?? "")).Append('}').Append(i < people.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("]\n");
            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            File.WriteAllText(Target, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("已导出 " + people.Count + " 个人物 → " + Target);
        }

        public static void ExportFromBatch() => Export();
    }
}