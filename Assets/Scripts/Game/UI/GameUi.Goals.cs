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
            OpenWindow("三年目标", 940, 800, w =>
            {
                RectTransform list = Ui.Scroll(w.Content, 700);


                Ui.Size(Ui.Label(list, "三年之路", 22), -1, 34);
                Ui.Size(Ui.Label(list, $"现在是第 {S.Clock.Today.TotalDays + 1} 天。身家 {S.NetWorth()}，" + (S.MyShopCount > 1 ? $"自营 {S.MyShopCount} 家店，店租合计 {S.TotalShopRent}/周" : S.HasShop ? $"店面 {GameSession.SizeName(S.PlayerShop.Size)}，店租 {S.CurrentShopRent}/周" : "还没有店铺（去房产中介租）") +
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

                Para(list, "人物关系和邀约在顶栏“人脉”中查看；已约行程在“日程”中查看。", 16, Ui.Dim);
            });
        }
    }
}
