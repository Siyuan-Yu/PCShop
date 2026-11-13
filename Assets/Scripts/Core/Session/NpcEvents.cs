using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 围绕固定人物的随机事件。大部分在 Resources/Data/events.json（group = "npc"）；
    /// 这里只留 json 表达不了的（要检查货架空间、直接往库存里放货）。
    /// </summary>
    public static class NpcEvents
    {
        /// <summary>所有人物事件（来自 events.json 里 group = "npc" 的条目，加上 C# 里的周维成）。</summary>
        public static IReadOnlyList<EventDef> All => EventCatalog.All.Where(e => e.Group == "npc").ToList();

        public static readonly EventDef ZhouDeal = new EventDef
        {
            Id = "zhou_deal", Group = "npc", Title = "周维成出清库存", MinDay = 30, CooldownDays = 30, Weight = 8,
            When = s => s.IsMet(Npcs.Zhou) && s.Relation(Npcs.Zhou) >= 25
                && s.MyShops.Any(shop => shop.RetailKind == "computer"),
        };
    }
}
