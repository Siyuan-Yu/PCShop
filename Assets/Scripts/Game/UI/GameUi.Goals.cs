using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        /// <summary>目标与人脉：三年路线、各检查点进度、结局、认识的人和送礼。</summary>
        public void OpenGoalsPanel()
        {
            OpenWindow("目标与人脉", 940, 800, w =>
            {
                RectTransform list = Ui.Scroll(w.Content, 700);


                Ui.Size(Ui.Label(list, "三年之路", 22), -1, 34);
                Ui.Size(Ui.Label(list, $"现在是第 {S.Clock.Today.TotalDays + 1} 天。身家 {S.NetWorth()}，" + (S.HasShop ? $"店面 {GameSession.SizeName(S.PlayerShop.Size)}，店租 {S.CurrentShopRent}/月" : "还没有店铺（去房产中介租）") +
                                       (S.RentHikes > 0 ? $"（已涨租 {S.RentHikes} 次）" : ""), 16, TextAnchor.MiddleLeft, Ui.Dim), -1, 28);
                foreach (GoalLine g in S.Goals())
                {
                    string mark = g.State == GoalState.Passed ? "<color=#A9C9A9>✔ 达成</color>" :
                        g.State == GoalState.Failed ? "<color=#D79780>✘ 未达成</color>" : "<color=#ABB5A8>进行中</color>";
                    Text t = Ui.Label(list, $"<b>{g.Title}</b>  {mark}\n<size=15>{g.Goal}</size>\n<size=15><color=#ABB5A8>{g.Progress}</color></size>", 18);
                    Ui.Size(t, -1, 82);
                }
                if (S.EndingId != null)
                {
                    EndingDef e = new[] { Campaign.Scrape, Campaign.Steady, Campaign.Star, Campaign.Boss }.First(x => x.Id == S.EndingId);
                    Ui.Size(Ui.Button(list, $"回看三年结局：{e.Title}", () => Popup("三年之后：" + e.Title, S.ComposeEpilogue(e)), 18, Ui.ButtonHot), -1, 44);
                }

                if (S.Appointments.Count > 0)
                {
                    Ui.Size(Ui.Label(list, "约好的行程", 22), -1, 40);
                    foreach (Appointment a in S.Appointments)
                        Ui.Size(Ui.Label(list, $"{S.DescribeDay(a.Day)} {GameSession.SlotName(a.Slot)}　和{Npcs.Get(a.NpcId).Name}", 17, TextAnchor.MiddleLeft, Ui.Good), -1, 30);
                }

                Ui.Size(Ui.Label(list, "认识的人", 22), -1, 40);
                foreach (NpcDef def in Npcs.All)
                {
                    NpcDef n = def;
                    if (!S.IsMet(n.Id))
                    {
                        Ui.Size(Ui.Label(list, $"<color=#ABB5A8>？？？　（还没遇到。留心店里的随机事件）</color>", 17), -1, 34);
                        continue;
                    }
                    int rel = S.Relation(n.Id);
                    RectTransform row = Ui.Row(list, 56);
                    PortraitThumb(row, n, 52, () => OpenNpcWindow(n, null, w));
                    Ui.Size(Ui.Label(row, $"<b>{n.Name}</b>  {n.Role}", 17), 270);
                    Ui.Size(Ui.Label(row, $"{Npcs.LevelName(rel)} {rel}", 16, TextAnchor.MiddleLeft, Ui.Good), 100);
                    Ui.Bar(row, rel / 100f, Ui.Accent, 120, 14);
                    Ui.Size(Ui.Button(row, "交谈", () => OpenNpcWindow(n, null, w), 16, Ui.ButtonHot), 90);
                    string perk = NpcPerks.Describe(n.Id);
                    if (n.IsTipster) perk += "　" + TrustText(n);
                    Ui.Size(Ui.Label(list, "<size=14><color=#ABB5A8>" + perk + "</color></size>", 14), -1, 24);
                }
            });
        }
    }
}
