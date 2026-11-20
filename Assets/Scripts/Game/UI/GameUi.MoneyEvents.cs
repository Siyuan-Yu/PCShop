using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildMoneyEventTasks(Window w)
        {
            var ticket = S.ScratchTicket;
            if (ticket != null)
            {
                var row = Ui.Row(w.Content, 38, 8);
                Ui.Size(Ui.Label(row, ticket.Paid ? "刮刮乐 · 已开奖" : "刮刮乐 · 待刮", 18), -1, 38, 1);
                if (ticket.Paid)
                    Ui.Size(Ui.Button(row, "查看结果", () => Popup("刮刮乐结果", ticket.ResultText), 17), 108, 38);
                else
                    Ui.Size(Ui.Button(row, ticket.ProgressPercent > 0 ? "继续刮" : "刮开", () => OpenEventScratch(w), 17), 108, 38);
            }
            Line(w.Content, "住院保障 · " + (S.HasHospitalCoverage ? "有效至 " + new GameTimestamp(S.HospitalPolicyUntil) + " · 报销80%，不封顶" : "未生效或已到期"), 17);
            if (S.HospitalPolicyUntil > 0)
            {
                var row = Ui.Row(w.Content, 38, 8); Ui.Size(Ui.Label(row, "年度保费¥3,000 · 自行续费", 17), -1, 38, 1);
                Ui.Size(Ui.Button(row, "续保一年", () => { if (!S.BuyHospitalPolicy()) Toast("现金不足，未续保。"); w.Rebuild(); }, 17), 120, 38);
            }
            foreach (var i in S.PrivateInvestments)
                Line(w.Content, "私人投资¥10,000 · " + (i.Settled ? i.Success ? "已回款¥20,000" : "本金已损失" : new GameTimestamp(i.At) + "到期"), 17);
            foreach (var bill in S.EventBills)
            {
                var b = bill;
                if (b.CreditCents > 0) Line(w.Content, "账单抵扣余额 · " + (S.City.HasPlace(b.PropertyId) ? S.City.GetPlace(b.PropertyId).Name : "原租约") + " · " + Money.FromCents(b.CreditCents), 17);
                if (b.Settled) continue;
                // Only show proven differences; regular correctly charged invoices stay out of the task list.
                long extra = S.EventBillRefundDue(b.Id); if (extra <= 0) continue;
                var row = Ui.Row(w.Content, 38, 8); Ui.Size(Ui.Label(row, "账单待退 " + Money.FromCents(extra) + " · " + new GameDate(b.Day), 17), -1, 38, 1);
                Ui.Size(Ui.Button(row, "退款", () => { S.SettleEventBill(b.Id, false); w.Rebuild(); }, 17), 85, 38);
                Ui.Size(Ui.Button(row, "抵下期", () => { S.SettleEventBill(b.Id, true); w.Rebuild(); }, 17), 95, 38);
            }
            if (S.Flags.Contains("event-idle-sale"))
            {
                var row = Ui.Row(w.Content, 38, 8); Ui.Size(Ui.Label(row, "随身闲置 · 到旧物市场交货", 17), -1, 38, 1);
                Ui.Size(Ui.Button(row, "查看", OpenEventIdleSale, 17), 90, 38);
            }
        }
        public void OpenEventScratch(Window owner = null)
        {
            OpenWindow("刮刮乐", 760, 400, w =>
            {
                var ticket = S.ScratchTicket;
                if (ticket == null) { Line(w.Content, "没有待刮的彩票。", 18); return; }
                var instruction = Line(w.Content, "", 19);
                var area = Ui.Size(Ui.Panel(w.Content, "ScratchTicketSurface", Ui.PanelBg2), -1, 180).rectTransform;
                string prizeText = ticket.PrizeYuan > 0 ? $"奖金 {Money.FromYuan(ticket.PrizeYuan)}" : "谢谢参与";
                Ui.Label(area, prizeText, 40, TextAnchor.MiddleCenter, Ui.Accent).rectTransform.Fill();
                var cover = Ui.Rect(area, "ScratchCover").Fill();
                var graphic = cover.gameObject.AddComponent<ScratchTicketGraphic>();
                graphic.Session = S;

                var row = Ui.Row(w.Content, 44, 8);
                var status = Ui.Size(Ui.Label(row, "", 18), 0, 44, 1);
                var finish = Ui.Size(Ui.Button(row, "完成", w.Close, 18, Ui.ButtonHot), 110, 42);
                void Refresh()
                {
                    S.ClaimEventScratch(showPopup: false);
                    var current = S.ScratchTicket;
                    instruction.text = current.Paid ? "本张已开奖" : "按住鼠标左键，在银色涂层上来回刮。";
                    status.text = current.Paid ? current.ResultText : $"已刮开 {current.ProgressPercent}%";
                    status.color = current.Paid && current.PrizeYuan > 0 ? Ui.Good : Ui.TextColor;
                    finish.gameObject.SetActive(current.Paid);
                    graphic.raycastTarget = !current.Paid;
                }
                graphic.Changed = Refresh;
                Refresh();
                graphic.SetVerticesDirty();
            }, onClosed: () => owner?.RequestRebuild());
        }
        public void OpenEventIdleSale()
        {
            OpenWindow("随身闲置", 850, 600, w =>
            {
                Line(w.Content, "只出售带在身上的闲置家具和市场旧物。", 18);
                if (!S.AtOldMarket)
                {
                    Ui.Size(Ui.Button(w.Content, "前往青湾旧物市场", () => { CloseAllWindows(); _ctrl.GoToPlace(S.City.GetPlace(GameSession.OldMarketVenueId)); }, 18), -1, 40);
                }
                foreach (var item in S.EventIdleItems)
                {
                    var i = item; var row = Ui.Row(w.Content, 66, 8);
                    Ui.Size(Ui.Label(row, i.Name + " ×" + i.Count + "\n回收单价 " + i.Price, 18), -1, 64, 1);
                    Ui.Size(Ui.Button(row, "卖1件", () => { S.SellEventIdle(i.Id, i.Furniture, 1); w.Rebuild(); }, 17, Ui.ButtonBg, S.AtOldMarket), 90, 38);
                    if (i.Count > 1) Ui.Size(Ui.Button(row, "全卖", () => { S.SellEventIdle(i.Id, i.Furniture, i.Count); w.Rebuild(); }, 17, Ui.ButtonBg, S.AtOldMarket), 90, 38);
                }
                if (S.EventIdleItems.Length == 0) Line(w.Content, "没有可卖的随身闲置。", 18, Ui.Dim);
            });
        }
    }
}
