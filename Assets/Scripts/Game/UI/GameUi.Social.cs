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

        public void OpenInvitePanel(string npcId) => OpenCityActivityChooser(npcId);

        /// <summary>行情面板里的小道消息：听来的话，几天后能看到真假。</summary>
        private void BuildTipsSection(Window w)
        {
            Line(w.Content, "小道消息（档口老板们的话，有真有假，几天后验证）：", 18);
            RectTransform list = Ui.Scroll(w.Content, 130);
            foreach (MarketTip t in S.Tips.Reverse().Take(12))
            {
                NpcDef who = Npcs.Get(t.NpcId);
                string verdict = t.Outcome == TipOutcome.Pending ? "<color=#655E52>等着看</color>" :
                    t.Outcome == TipOutcome.Right ? "<color=#386047>应验</color>" : "<color=#9C4634>没应验</color>";
                Text line = Ui.Label(list, $"第 {t.IssuedDay + 1} 天　{who.Name}：{PartCatalog.CategoryName(t.Category)}要{(t.Direction > 0 ? "涨" : "跌")}　{verdict}", 16);
                Ui.Size(line, -1, 26);
            }
            if (S.Tips.Count == 0)
                Ui.Size(Ui.Label(list, "还没听到过。认识档口老板后，递根烟或一起出去玩，可能会听到点什么。", 16, TextAnchor.MiddleLeft, Ui.Dim), -1, 26);
        }
    }
}
