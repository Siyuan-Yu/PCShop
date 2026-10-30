using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>进货渠道上的人：档口老板、约出去玩、行情小道消息。</summary>
    public sealed partial class GameUi
    {
        private string TrustText(NpcDef n)
        {
            int right = S.TipsRight(n.Id), wrong = S.TipsWrong(n.Id);
            return right + wrong == 0 ? "消息：还没验证过" : $"消息：{right + wrong} 条里应验 {right} 条";
        }

        public void OpenInvitePanel(string npcId) => OpenInviteWindow(Npcs.Get(npcId), null);

        /// <summary>约某人晚上或周末出去玩。对方答不答应看交情和魅力。</summary>
        private void OpenInviteWindow(NpcDef n, Window parent)
        {
            OpenWindow("约" + n.Name, 620, 520, w =>
            {
                int rel = S.Relation(n.Id);
                Line(w.Content, $"{n.Name}　{Npcs.LevelName(rel)} {rel}　愿意赴约的把握约 {S.InviteAcceptChance(n.Id) * 100:0}%", 18);
                Line(w.Content, "出去玩要花钱、花几个小时（店里没人看着），但能涨交情。交情 ≥ " + GameSession.MinInviteRelation + " 才约得动，他爱的活动涨得更多。", 15, Ui.Dim);

                Appointment existing = S.Appointments.FirstOrDefault(a => a.NpcId == n.Id);
                if (existing != null)
                {
                    Line(w.Content, $"已约好：{S.DescribeDay(existing.Day)} {GameSession.SlotName(existing.Slot)}。到点会弹出选择去哪里玩。", 18, Ui.Good);
                    Ui.Size(Ui.Button(w.Content, "推掉（交情 -3）", () =>
                    {
                        S.CancelAppointment(n.Id);
                        w.Rebuild();
                    }, 18, Ui.Warn), -1, 46);
                    return;
                }

                foreach (OutingSlot slot in new[] { OutingSlot.Evening, OutingSlot.Weekend })
                {
                    OutingSlot sl = slot;
                    string blocker = S.InviteBlocker(n.Id, sl);
                    string activities = string.Join("、", Activities.All.Where(a => a.Slot == sl).Select(a => a.Name));
                    Line(w.Content, $"{GameSession.SlotName(sl)}（{S.DescribeDay(S.NextSlotDate(sl).TotalDays)}）：{activities}", 16, Ui.Dim);
                    Ui.Size(Ui.Button(w.Content, blocker == null ? "约 " + GameSession.SlotName(sl) : blocker, () =>
                    {
                        S.Invite(n.Id, sl);
                        w.Rebuild();
                        parent?.Rebuild();
                    }, 18, Ui.ButtonHot, blocker == null), -1, 46);
                }
                if (n.Likes != null) Line(w.Content, "听说他最爱：" + Activities.Get(n.Likes).Name, 16, Ui.Good);
            });
        }

        /// <summary>行情面板里的小道消息：听来的话，几天后能看到真假。</summary>
        private void BuildTipsSection(Window w)
        {
            Line(w.Content, "小道消息（档口老板们的话，有真有假，几天后验证）：", 18);
            RectTransform list = Ui.Scroll(w.Content, 130);
            foreach (MarketTip t in S.Tips.Reverse().Take(12))
            {
                NpcDef who = Npcs.Get(t.NpcId);
                string verdict = t.Outcome == TipOutcome.Pending ? "<color=#ABB5A8>等着看</color>" :
                    t.Outcome == TipOutcome.Right ? "<color=#A9C9A9>应验</color>" : "<color=#D79780>没应验</color>";
                Text line = Ui.Label(list, $"第 {t.IssuedDay + 1} 天　{who.Name}：{PartCatalog.CategoryName(t.Category)}要{(t.Direction > 0 ? "涨" : "跌")}　{verdict}", 16);
                Ui.Size(line, -1, 26);
            }
            if (S.Tips.Count == 0)
                Ui.Size(Ui.Label(list, "还没听到过。认识档口老板后，递根烟或一起出去玩，可能会听到点什么。", 16, TextAnchor.MiddleLeft, Ui.Dim), -1, 26);
        }
    }
}