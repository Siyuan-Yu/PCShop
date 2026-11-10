using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    /// <summary>读 Resources/Data/events.json（文本由 Game 层通过 EventCatalog.JsonSource 提供），和 C# 里的事件合并成最终目录。</summary>
    public static class EventLoader
    {
        public const string ResourcePath = "Data/events";

        public static EventScriptFile ReadFile()
        {
            string text = EventCatalog.JsonSource?.Invoke();
            return text == null ? null : EventScriptReader.Read(text);
        }

        public static IReadOnlyList<EventDef> Load()
        {
            var code = EventCatalog.CodeEvents.ToDictionary(e => e.Id);
            var file = ReadFile();
            if (file == null)
                throw new InvalidOperationException("读不到 Resources/" + ResourcePath + ".json（EventCatalog.JsonSource 没设置，或文件不存在）。");
            var errors = EventScript.Validate(file, code.Keys);
            EventCatalog.LoadErrors = errors;

            var all = new List<EventDef>();
            var used = new HashSet<string>();
            foreach (var entry in file.events)
            {
                if (string.IsNullOrEmpty(entry.id) || !used.Add(entry.id)) continue;
                if (entry.code)
                {
                    if (!code.TryGetValue(entry.id, out var def)) continue;
                    if (entry.minDay >= 0) def.MinDay = entry.minDay;
                    if (entry.maxDay >= 0) def.MaxDay = entry.maxDay;
                    if (entry.triggerChance >= 0) def.TriggerChance = entry.triggerChance;
                    if (entry.cooldownDays >= 0) def.CooldownDays = entry.cooldownDays;
                    if (entry.weight >= 0) def.Weight = entry.weight;
                    all.Add(def);
                }
                else all.Add(EventScript.Compile(entry));
            }
            // json 没列到的 C# 事件也要保留，免得漏。
            foreach (var def in code.Values) if (used.Add(def.Id)) all.Add(def);
            return all;
        }
    }
}