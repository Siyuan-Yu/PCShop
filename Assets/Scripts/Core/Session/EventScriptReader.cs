using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PCShopSell.Core.Session
{
    /// <summary>够用的 JSON 解析（Core 不能引用 Unity 的 JsonUtility）。对象 = Dictionary，数组 = List，数字 = double。</summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0; var v = Value(text, ref i); Skip(text, ref i);
            if (i != text.Length) throw new FormatException("JSON 末尾有多余内容，位置 " + i);
            return v;
        }

        private static void Skip(string s, ref int i) { while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '\uFEFF')) i++; }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON 提前结束");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++; Skip(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Skip(s, ref i); string key = Str(s, ref i); Skip(s, ref i);
                    if (s[i++] != ':') throw new FormatException("缺少 ':'，位置 " + i);
                    d[key] = Value(s, ref i); Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("对象里应是 ',' 或 '}'，位置 " + i);
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++; Skip(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i)); Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("数组里应是 ',' 或 ']'，位置 " + i);
                }
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i || !double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
                throw new FormatException("看不懂的内容，位置 " + start);
            return n;
        }

        private static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("这里应该是引号，位置 " + i);
            i++; var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("字符串没有结束");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
        }
    }

    /// <summary>把 events.json 读成 <see cref="EventScriptFile"/>。写错的字段名、类型会记进 readErrors，由 Validate 一起报出来。</summary>
    public static class EventScriptReader
    {
        private static readonly string[] EventKeys = { "id", "title", "body", "group", "code", "minDay", "maxDay", "cooldownDays", "weight", "triggerChance", "when", "whenAny", "choices" };
        private static readonly string[] ChoiceKeys = { "text", "disabledReason", "requires", "effects", "chance", "chanceCharm", "chanceIntel", "chanceRelation", "chanceNpc", "chanceBonus", "onHit", "onMiss" };
        private static readonly string[] CondKeys = { "type", "key", "value", "not" };
        private static readonly string[] OpKeys = { "type", "text", "alt", "key", "npc", "kind", "memo", "value", "yuan", "days", "count", "flag", "stopIfNone" };
        private static readonly string[] BonusKeys = { "when", "add", "perPoint" };

        public static EventScriptFile Read(string json)
        {
            var file = new EventScriptFile();
            try
            {
                var root = MiniJson.Parse(json) as Dictionary<string, object>;
                if (root == null || !root.TryGetValue("events", out var list) || !(list is List<object> events))
                { file.readErrors.Add("文件应该是 { \"events\": [ ... ] }"); return file; }
                foreach (var item in events)
                {
                    if (item is Dictionary<string, object> d) file.events.Add(ReadEvent(d, file.readErrors));
                    else file.readErrors.Add("events 里有不是对象的内容");
                }
            }
            catch (Exception e) { file.readErrors.Add("JSON 格式错误：" + e.Message); }
            return file;
        }

        private static string S(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) ? v as string : null;
        private static double D(Dictionary<string, object> d, string key, double def) => d.TryGetValue(key, out var v) && v is double n ? n : def;
        private static bool B(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) && v is bool b && b;

        private static void CheckKeys(Dictionary<string, object> d, string[] allowed, string where, List<string> errors)
        {
            foreach (var key in d.Keys) if (Array.IndexOf(allowed, key) < 0) errors.Add(where + "：不认识的字段 \"" + key + "\"（拼错了？）");
        }

        private static IEnumerable<Dictionary<string, object>> Items(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v is List<object> list)
                foreach (var o in list) if (o is Dictionary<string, object> x) yield return x;
        }

        private static EventEntry ReadEvent(Dictionary<string, object> d, List<string> errors)
        {
            string where = "事件 " + (S(d, "id") ?? "<无 id>");
            CheckKeys(d, EventKeys, where, errors);
            var e = new EventEntry { id = S(d, "id"), title = S(d, "title"), body = S(d, "body"), group = S(d, "group"), code = B(d, "code"),
                minDay = (int)D(d, "minDay", -1), maxDay = (int)D(d, "maxDay", -1), cooldownDays = (int)D(d, "cooldownDays", -1), weight = (int)D(d, "weight", -1),
                triggerChance = D(d, "triggerChance", -1) };
            foreach (var c in Items(d, "when")) e.when.Add(ReadCond(c, where, errors));
            foreach (var c in Items(d, "whenAny")) e.whenAny.Add(ReadCond(c, where, errors));
            int n = 0;
            foreach (var c in Items(d, "choices")) e.choices.Add(ReadChoice(c, where + " 选项" + (++n), errors));
            return e;
        }

        private static EventChoiceEntry ReadChoice(Dictionary<string, object> d, string where, List<string> errors)
        {
            CheckKeys(d, ChoiceKeys, where, errors);
            var c = new EventChoiceEntry { text = S(d, "text"), disabledReason = S(d, "disabledReason"), chance = D(d, "chance", -1),
                chanceCharm = D(d, "chanceCharm", 0), chanceIntel = D(d, "chanceIntel", 0), chanceRelation = D(d, "chanceRelation", 0), chanceNpc = S(d, "chanceNpc") };
            foreach (var x in Items(d, "requires")) c.requires.Add(ReadCond(x, where, errors));
            foreach (var x in Items(d, "effects")) c.effects.Add(ReadOp(x, where, errors));
            foreach (var x in Items(d, "onHit")) c.onHit.Add(ReadOp(x, where, errors));
            foreach (var x in Items(d, "onMiss")) c.onMiss.Add(ReadOp(x, where, errors));
            foreach (var x in Items(d, "chanceBonus"))
            {
                CheckKeys(x, BonusKeys, where + " chanceBonus", errors);
                var b = new EventChanceBonus { add = D(x, "add", 0), perPoint = D(x, "perPoint", 0) };
                foreach (var w in Items(x, "when")) b.when.Add(ReadCond(w, where, errors));
                c.chanceBonus.Add(b);
            }
            return c;
        }

        private static EventCond ReadCond(Dictionary<string, object> d, string where, List<string> errors)
        {
            CheckKeys(d, CondKeys, where + " 条件", errors);
            return new EventCond { type = S(d, "type"), key = S(d, "key"), value = D(d, "value", 0), not = B(d, "not") };
        }

        private static EventOp ReadOp(Dictionary<string, object> d, string where, List<string> errors)
        {
            CheckKeys(d, OpKeys, where + " 效果", errors);
            return new EventOp { type = S(d, "type"), text = S(d, "text"), alt = S(d, "alt"), key = S(d, "key"), npc = S(d, "npc"), kind = S(d, "kind"), memo = S(d, "memo"),
                value = D(d, "value", 0), yuan = (long)D(d, "yuan", 0), days = (int)D(d, "days", 0), count = (int)D(d, "count", 0), flag = B(d, "flag"), stopIfNone = B(d, "stopIfNone") };
        }
    }
}
