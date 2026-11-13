using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 把事件文件写回 json，格式固定：事件、选项各占几行，每个条件/效果占一行，只写不是默认值的字段。
    /// 游戏里的编辑器和外部事件编辑器都用它，所以 git diff 只会出现真正改过的地方。
    /// </summary>
    public static class EventScriptWriter
    {
        public static string Write(EventScriptFile file)
        {
            var lines = new List<string> { "{", "  \"events\": [" };
            for (int i = 0; i < file.events.Count; i++)
            {
                var block = Event(file.events[i]);
                if (i < file.events.Count - 1) block[block.Count - 1] += ",";
                lines.AddRange(block);
            }
            lines.Add("  ]"); lines.Add("}");
            return string.Join("\n", lines) + "\n";
        }

        // ---------- 值 ----------
        public static string Str(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default: if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static string Num(double d) => d == Math.Floor(d) && Math.Abs(d) < 1e15 ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString("R", CultureInfo.InvariantCulture);

        private static void Add(List<string> parts, string key, string value) => parts.Add("\"" + key + "\": " + value);
        private static void AddStr(List<string> parts, string key, string value) { if (!string.IsNullOrEmpty(value)) Add(parts, key, Str(value)); }
        private static void AddNum(List<string> parts, string key, double value) { if (value != 0) Add(parts, key, Num(value)); }
        private static void AddFlag(List<string> parts, string key, bool value) { if (value) Add(parts, key, "true"); }
        private static string Obj(List<string> parts) => "{" + string.Join(", ", parts) + "}";

        // ---------- 条件 / 效果（各占一行）----------
        private static string Cond(EventCond c)
        {
            var p = new List<string>(); Add(p, "type", Str(c.type ?? ""));
            AddStr(p, "key", c.key); AddNum(p, "value", c.value); AddFlag(p, "not", c.not);
            return Obj(p);
        }

        private static string Op(EventOp o)
        {
            var p = new List<string>(); Add(p, "type", Str(o.type ?? ""));
            AddStr(p, "text", o.text); AddStr(p, "alt", o.alt); AddStr(p, "key", o.key); AddStr(p, "npc", o.npc); AddStr(p, "kind", o.kind); AddStr(p, "memo", o.memo);
            AddNum(p, "value", o.value); AddNum(p, "yuan", o.yuan); AddNum(p, "days", o.days); AddNum(p, "count", o.count);
            AddFlag(p, "flag", o.flag); AddFlag(p, "stopIfNone", o.stopIfNone);
            return Obj(p);
        }

        private static void Section(List<string> lines, string indent, string key, IEnumerable<string> items, bool last)
        {
            var list = items.ToList();
            lines.Add(indent + "\"" + key + "\": [");
            for (int i = 0; i < list.Count; i++) lines.Add(indent + "  " + list[i] + (i < list.Count - 1 ? "," : ""));
            lines.Add(indent + "]" + (last ? "" : ","));
        }

        // ---------- 事件 ----------
        private static void EventScalars(List<string> p, EventEntry e)
        {
            Add(p, "id", Str(e.id ?? ""));
            if (e.code) Add(p, "code", "true");
            else { Add(p, "title", Str(e.title ?? "")); Add(p, "body", Str(e.body ?? "")); AddStr(p, "group", e.group); }
            if (e.minDay >= 0) Add(p, "minDay", Num(e.minDay));
            if (e.maxDay >= 0) Add(p, "maxDay", Num(e.maxDay));
            if (e.cooldownDays >= 0) Add(p, "cooldownDays", Num(e.cooldownDays));
            if (e.weight >= 0) Add(p, "weight", Num(e.weight));
            if (e.triggerChance >= 0) Add(p, "triggerChance", Num(e.triggerChance));
        }

        private static List<string> Event(EventEntry e)
        {
            var p = new List<string>(); EventScalars(p, e);
            if (e.code) return new List<string> { "    " + Obj(p) };
            var lines = new List<string> { "    {", "      " + string.Join(", ", p) + "," };
            if (e.when.Count > 0) Section(lines, "      ", "when", e.when.Select(Cond), false);
            if (e.whenAny.Count > 0) Section(lines, "      ", "whenAny", e.whenAny.Select(Cond), false);
            lines.Add("      \"choices\": [");
            for (int i = 0; i < e.choices.Count; i++)
            {
                var block = Choice(e.choices[i]);
                if (i < e.choices.Count - 1) block[block.Count - 1] += ",";
                lines.AddRange(block);
            }
            lines.Add("      ]"); lines.Add("    }");
            return lines;
        }

        private static List<string> Choice(EventChoiceEntry c)
        {
            var p = new List<string>(); Add(p, "text", Str(c.text ?? "")); AddStr(p, "disabledReason", c.disabledReason);
            if (c.chance >= 0) Add(p, "chance", Num(c.chance));
            AddNum(p, "chanceCharm", c.chanceCharm); AddNum(p, "chanceIntel", c.chanceIntel); AddStr(p, "chanceNpc", c.chanceNpc); AddNum(p, "chanceRelation", c.chanceRelation);
            var lines = new List<string> { "        {" };
            var sections = new List<(string key, List<string> items)>();
            if (c.requires.Count > 0) sections.Add(("requires", c.requires.Select(Cond).ToList()));
            if (c.effects.Count > 0) sections.Add(("effects", c.effects.Select(Op).ToList()));
            if (c.onHit.Count > 0) sections.Add(("onHit", c.onHit.Select(Op).ToList()));
            if (c.onMiss.Count > 0) sections.Add(("onMiss", c.onMiss.Select(Op).ToList()));
            if (c.chanceBonus.Count > 0) sections.Add(("chanceBonus", c.chanceBonus.Select(Bonus).ToList()));
            lines.Add("          " + string.Join(", ", p) + (sections.Count > 0 ? "," : ""));
            for (int i = 0; i < sections.Count; i++) Section(lines, "          ", sections[i].key, sections[i].items, i == sections.Count - 1);
            lines.Add("        }");
            return lines;
        }

        private static string Bonus(EventChanceBonus b)
        {
            var p = new List<string>();
            Add(p, "when", "[" + string.Join(", ", b.when.Select(Cond)) + "]");
            Add(p, "add", Num(b.add));
            AddNum(p, "perPoint", b.perPoint);
            return Obj(p);
        }
    }
}
