using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    // ============================================================================================
    // 事件脚本的数据结构、字段说明（schema）和格式校验。不依赖游戏运行时，外部事件编辑器直接引用这个文件。
    // Resources/Data/events.json 是 { "events": [ ... ] }，字段说明见 docs/events-json.md。
    // 读取由 EventScriptReader 负责，写出由 EventScriptWriter 负责，执行在 EventScript.cs。
    // 需要“没写就不覆盖”的字段默认是 -1 / 空。
    // ============================================================================================

    public sealed class EventScriptFile
    {
        public List<EventEntry> events = new List<EventEntry>();
        /// <summary>读文件时发现的问题（未知字段、类型不对等）。</summary>
        public List<string> readErrors = new List<string>();
    }

    public sealed class EventEntry
    {
        public string id, title, body, group;
        /// <summary>true = 这个事件的内容在 C# 里（要绑定具体店铺等），json 里只负责排序和覆盖 minDay / maxDay / cooldownDays / weight / triggerChance。</summary>
        public bool code;
        /// <summary>最早 / 最晚触发的游戏天数（开局那天是第 0 天）；maxDay = -1 表示没有上限。</summary>
        public int minDay = -1, maxDay = -1, cooldownDays = -1, weight = -1;
        /// <summary>每次轮到它时先掷一次，掷中才有资格进入抽取（0–1）。-1 = 没写 = 必定有资格。</summary>
        public double triggerChance = -1;
        /// <summary>全部满足才会被抽到。</summary>
        public List<EventCond> when = new List<EventCond>();
        /// <summary>非空时，其中至少一条满足。</summary>
        public List<EventCond> whenAny = new List<EventCond>();
        public List<EventChoiceEntry> choices = new List<EventChoiceEntry>();
    }

    public sealed class EventChoiceEntry
    {
        public string text, disabledReason;
        /// <summary>全部满足这个选项才能点；留空 = 总能点。</summary>
        public List<EventCond> requires = new List<EventCond>();
        public List<EventOp> effects = new List<EventOp>();
        /// <summary>掷骰：-1 = 不掷。所有参与判定的数值须达到门槛，超过门槛的部分按每点系数加入 chance。</summary>
        public double chance = -1, chanceCharm, chanceIntel, chanceRelation;
        public string chanceNpc;
        public List<EventChanceBonus> chanceBonus = new List<EventChanceBonus>();
        public List<EventOp> onHit = new List<EventOp>(), onMiss = new List<EventOp>();
    }

    public sealed class EventChanceBonus { public List<EventCond> when = new List<EventCond>(); public double add, perPoint; }

    /// <summary>条件。type 见 <see cref="EventScriptSchema.Conds"/>。not=true 取反。</summary>
    public sealed class EventCond { public string type, key; public double value; public bool not; }

    /// <summary>效果。type 见 <see cref="EventScriptSchema.Ops"/>。文字里可以用 {item}（loseItem 丢掉的货名）。</summary>
    public sealed class EventOp
    {
        public string type, text, alt, key, npc, kind, memo;
        public double value; public long yuan; public int days, count;
        public bool flag, stopIfNone;
    }

    /// <summary>一种条件 / 效果：type、中文名、会用到哪些字段（"字段名:显示名"）。编辑器按它显示表单，校验也按它来。</summary>
    public sealed class TypeSpec
    {
        public readonly string Type, Name;
        public readonly string[] Fields;
        public TypeSpec(string type, string name, params string[] fields) { Type = type; Name = name; Fields = fields; }
        public bool Uses(string field) => Fields.Any(f => f.StartsWith(field + ":", StringComparison.Ordinal));
        public string Label(string field) { var f = Fields.FirstOrDefault(x => x.StartsWith(field + ":", StringComparison.Ordinal)); return f == null ? field : f.Substring(field.Length + 1); }
    }

    public static class EventScriptSchema
    {
        public static readonly TypeSpec[] Conds =
        {
            new TypeSpec("cash", "现金不少于", "value:金额（元）"),
            new TypeSpec("cashBelow", "现金低于", "value:金额（元）；填 0 = 账户为负"),
            new TypeSpec("negativeCashDays", "账户已连续为负", "value:天数（不少于）"),
            new TypeSpec("flag", "有某个标记", "key:标记名"),
            new TypeSpec("upgrade", "已买店铺升级", "key:升级 id（如 cctv）"),
            new TypeSpec("stock", "店里有货"),
            new TypeSpec("samePlace", "人物此刻和玩家在同一地点", "key:人物"),
            new TypeSpec("met", "已经认识", "key:人物"),
            new TypeSpec("relationAtLeast", "交情不低于", "key:人物", "value:交情"),
            new TypeSpec("relationBelow", "交情低于", "key:人物", "value:交情"),
            new TypeSpec("rentHikesBelow", "房东涨租次数低于", "value:次数"),
            new TypeSpec("energyBelow", "精力低于", "value:精力"),
            new TypeSpec("sleepDebtAbove", "睡眠债高于", "value:小时"),
            new TypeSpec("healthBelow", "健康低于", "value:健康"),
            new TypeSpec("hasPhone", "有智能手机"),
            new TypeSpec("bankDeposit", "银行存款不少于", "value:金额（元）"),
            new TypeSpec("hasLoan", "有未还贷款"),
            new TypeSpec("reputationAtLeast", "名望不低于", "value:名望"),
            new TypeSpec("racingUnlocked", "已有能比赛的车（可以参加赛车）"),
            new TypeSpec("racingWins", "赛车累计胜场不少于", "value:胜场"),
            new TypeSpec("netWorthAtLeast", "身家不少于", "value:金额（元）"),
            new TypeSpec("charmAtLeast", "魅力不低于", "value:魅力"),
            new TypeSpec("intelAtLeast", "智力不低于", "value:智力"),
        };

        public static readonly TypeSpec[] Ops =
        {
            new TypeSpec("text", "文字结果", "text:结果文字"),
            new TypeSpec("pay", "付钱", "yuan:金额（元）", "memo:账目备注"),
            new TypeSpec("gain", "收钱", "yuan:金额（元）", "memo:账目备注"),
            new TypeSpec("shopRep", "本店口碑变化", "value:变化量（可负）"),
            new TypeSpec("rep", "名望变化", "value:变化量（可负）"),
            new TypeSpec("charm", "魅力变化", "value:变化量（可负）"),
            new TypeSpec("health", "健康变化", "value:变化量（可负）"),
            new TypeSpec("energy", "消耗精力", "value:精力点数"),
            new TypeSpec("rest", "休息（时间流逝）", "value:分钟"),
            new TypeSpec("flag", "设置标记", "key:标记名"),
            new TypeSpec("loseItem", "随机丢一件货", "text:丢货时的文字（可用 {item}）", "alt:没货时的文字", "stopIfNone:没货就中止后面的效果"),
            new TypeSpec("relation", "交情变化", "npc:人物", "value:变化量（可负）"),
            new TypeSpec("meet", "认识人物", "npc:人物", "value:初始交情"),
            new TypeSpec("markup", "加价倍率变化", "value:变化量（可负）"),
            new TypeSpec("lend", "借钱给别人", "yuan:金额（元）", "memo:借款人"),
            new TypeSpec("repay", "别人 N 天后还钱", "days:几天后", "yuan:金额（元）", "memo:账目备注"),
            new TypeSpec("meeting", "约一次真实饭局/活动", "npc:人物", "kind:活动类型（dinner/ktv/bar/court/golf/fishing/hike/snack/market/old_market/expo_electronics/expo_comic）", "key:来源前缀（需唯一）", "flag:由对方做东"),
            new TypeSpec("referral", "介绍客户订单", "key:订单来源 id", "memo:客户名", "count:台数", "flag:优惠价"),
            new TypeSpec("rentHike", "店租上涨一次"),
            new TypeSpec("intel", "智力变化", "value:变化量（可负）"),
            new TypeSpec("raceInvite", "约对方参加下一场正式赛车", "npc:人物（要是赛车爱好者）", "key:赛场 id（留空 = race_0）"),
            new TypeSpec("interestFreeLoan", "对方借钱给玩家（无息）", "npc:出借人", "yuan:金额（元）", "count:期限（月，1 月 = 30 天）"),
        };

        public static TypeSpec Cond(string type) => Conds.FirstOrDefault(c => c.Type == type);
        public static TypeSpec Op(string type) => Ops.FirstOrDefault(c => c.Type == type);
    }

    public static partial class EventScript
    {
        public static readonly string[] CondTypes = EventScriptSchema.Conds.Select(c => c.Type).ToArray();
        public static readonly string[] OpTypes = EventScriptSchema.Ops.Select(c => c.Type).ToArray();

        /// <summary>检查一份事件文件的格式问题（给单元测试和编辑器用）。没问题返回空列表。</summary>
        public static List<string> Validate(EventScriptFile file, ICollection<string> codeEventIds)
        {
            var errors = new List<string>(file.readErrors);
            var seen = new HashSet<string>();
            foreach (var e in file.events)
            {
                string where = "事件 " + (e.id ?? "<无 id>");
                if (string.IsNullOrEmpty(e.id)) { errors.Add(where + "：缺 id"); continue; }
                if (!seen.Add(e.id)) errors.Add(where + "：id 重复");
                if (e.triggerChance > 1) errors.Add(where + "：triggerChance 要在 0–1 之间");
                if (e.maxDay >= 0 && e.minDay > e.maxDay) errors.Add(where + "：minDay 不能大于 maxDay");
                if (e.code)
                {
                    if (!codeEventIds.Contains(e.id)) errors.Add(where + "：标了 code 但 C# 里没有这个事件");
                    continue;
                }
                if (string.IsNullOrEmpty(e.title)) errors.Add(where + "：缺 title");
                if (string.IsNullOrEmpty(e.body)) errors.Add(where + "：缺 body");
                if (e.choices.Count < 2) errors.Add(where + "：至少 2 个选项");
                if (e.choices.Count > 0 && e.choices.All(c => c.requires.Count > 0)) errors.Add(where + "：至少要有一个总能点的选项");
                foreach (var c in e.when.Concat(e.whenAny)) CheckCond(errors, where + " 条件", c);
                for (int i = 0; i < e.choices.Count; i++)
                {
                    var c = e.choices[i]; string cw = where + " 选项" + (i + 1);
                    if (string.IsNullOrEmpty(c.text)) errors.Add(cw + "：缺 text");
                    if (c.requires.Count > 0 && string.IsNullOrEmpty(c.disabledReason)) errors.Add(cw + "：有 requires 但没写 disabledReason");
                    foreach (var r in c.requires) CheckCond(errors, cw + " requires", r);
                    foreach (var b in c.chanceBonus)
                    {
                        foreach (var r in b.when) CheckCond(errors, cw + " chanceBonus", r);
                        if (b.perPoint < 0 || (b.perPoint > 0 && (b.when.Count != 1 || b.when[0].not
                            || (b.when[0].type != "racingWins" && b.when[0].type != "relationAtLeast"))))
                            errors.Add(cw + "：perPoint 须为正数，并且只设一个未取反的胜场或交情门槛");
                    }
                    foreach (var o in c.effects.Concat(c.onHit).Concat(c.onMiss)) CheckOp(errors, cw, o);
                    if (c.chance < 0 && (c.onHit.Count > 0 || c.onMiss.Count > 0)) errors.Add(cw + "：写了 onHit/onMiss 但没设 chance");
                    if (c.chance > 1) errors.Add(cw + "：chance 要在 0–1 之间");
                    if (c.effects.Count + c.onHit.Count + c.onMiss.Count == 0) errors.Add(cw + "：没有任何效果");
                }
            }
            return errors;
        }

        private static void CheckCond(List<string> errors, string where, EventCond c)
        {
            var spec = EventScriptSchema.Cond(c.type);
            if (spec == null) { errors.Add(where + "：未知条件 " + c.type); return; }
            if (spec.Uses("key") && string.IsNullOrEmpty(c.key)) errors.Add(where + "：" + c.type + " 缺 key（" + spec.Label("key") + "）");
        }

        private static void CheckOp(List<string> errors, string where, EventOp o)
        {
            var spec = EventScriptSchema.Op(o.type);
            if (spec == null) { errors.Add(where + "：未知效果 " + o.type); return; }
            if (spec.Uses("npc") && string.IsNullOrEmpty(o.npc)) errors.Add(where + "：" + o.type + " 缺 npc");
            if (spec.Uses("key") && o.type != "raceInvite" && string.IsNullOrEmpty(o.key)) errors.Add(where + "：" + o.type + " 缺 key");
            if (spec.Uses("yuan") && o.yuan <= 0) errors.Add(where + "：" + o.type + " 的 yuan 要大于 0");
            if (o.type == "text" && string.IsNullOrEmpty(o.text)) errors.Add(where + "：text 效果缺 text");
            if (o.type == "interestFreeLoan" && o.count < 1) errors.Add(where + "：interestFreeLoan 的期限（count）至少 1 个月");
            if (o.type == "repay" && o.days < 1) errors.Add(where + "：repay 的 days 至少 1 天");
        }
    }
}
