using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PCShopSell.Core.Session;

namespace PCShopSell.EventEditor
{
    public sealed class Npc { public string Id, Name, Role; public override string ToString() => Id + "  " + Name + (string.IsNullOrEmpty(Role) ? "" : "（" + Role + "）"); }

    /// <summary>人物名单：npcs.json 由 Unity 菜单“PCShop/事件/导出人物名单给事件编辑器”生成。</summary>
    public static class NpcList
    {
        public static List<Npc> All = new List<Npc>();
        public static string Source = "";

        public static string Locate()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                foreach (string rel in new[] { "npcs.json", Path.Combine("ExternalTools", "EventEditor", "npcs.json") })
                {
                    string p = Path.Combine(dir.FullName, rel);
                    if (File.Exists(p)) return p;
                }
            }
            return null;
        }

        public static void Load()
        {
            All = new List<Npc>(); Source = "";
            string path = Locate(); if (path == null) return;
            try
            {
                if (MiniJson.Parse(File.ReadAllText(path)) is List<object> list)
                    foreach (var o in list.OfType<Dictionary<string, object>>())
                        All.Add(new Npc { Id = o.TryGetValue("id", out var i) ? i as string : null, Name = o.TryGetValue("name", out var n) ? n as string : "", Role = o.TryGetValue("role", out var r) ? r as string : "" });
                Source = path;
            }
            catch { All = new List<Npc>(); }
        }

        public static string Name(string id) => All.FirstOrDefault(n => n.Id == id)?.Name ?? id;
    }
}