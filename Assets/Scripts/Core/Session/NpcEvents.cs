using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 围绕固定人物的随机事件。大部分在 Resources/Data/events.json（group = "npc"）；
    /// 这里只留 json 表达不了的（要检查货架空间、直接往库存里放货）。
    /// </summary>
    public static class NpcEvents
    {
        private static Money Y(long yuan) => Money.FromYuan(yuan);

        private static bool CanPay(GameSession s, long yuan) => s.Ledger.Balance >= Y(yuan);

        /// <summary>所有人物事件（来自 events.json 里 group = "npc" 的条目，加上 C# 里的周维成）。</summary>
        public static IReadOnlyList<EventDef> All => EventCatalog.All.Where(e => e.Group == "npc").ToList();

        public static readonly EventDef ZhouDeal = new EventDef
        {
            Id = "zhou_deal", Group = "npc", Title = "周维成出清库存", MinDay = 30, CooldownDays = 30, Weight = 8,
            When = s => s.IsMet(Npcs.Zhou) && s.Relation(Npcs.Zhou) >= 25,
            Body = "周维成发来消息：\"手上压了一批货急着出，一条星耀 RX 660 加两条 16G 内存，打包 ¥1,600，只给你留一天。\"",
            Choices =
            {
                new EventChoice
                {
                    Text = "全收了（¥1,600）",
                    Enabled = s => CanPay(s, 1600) && s.PlayerShop.Stock.FreeSpace >= DealSpace(),
                    DisabledReason = "钱不够，或者店里放不下",
                    Apply = s =>
                    {
                        s.EventPay(Y(1600), "周维成的货");
                        s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("gpu1"), true, 1);
                        s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("ram1"), true, 2);
                        s.AddRelation(Npcs.Zhou, 4);
                        return "货第二天就到了，比市价便宜一大截。交情 +4。";
                    },
                },
                new EventChoice
                {
                    Text = "这次算了",
                    Apply = s =>
                    {
                        s.AddRelation(Npcs.Zhou, -1);
                        return "周维成回了个“好”。";
                    },
                },
            },
        };

        private static double DealSpace() =>
            PCShopSell.Core.Inventory.Stock.SpaceOf(PartCatalog.Get("gpu1"), true, 1)
            + PCShopSell.Core.Inventory.Stock.SpaceOf(PartCatalog.Get("ram1"), true, 2);
    }
}