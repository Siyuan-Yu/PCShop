using System.Linq;
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
            if (ticket != null && !ticket.Paid)
            {
                var row = Ui.Row(w.Content, 38, 8); Ui.Size(Ui.Label(row, "刮刮乐 · 尚未兑付", 18), -1, 38, 1);
                Ui.Size(Ui.Button(row, "刮开", OpenEventScratch, 17), 90, 38);
            }
            Line(w.Content, "住院保障 · " + (S.HasHospitalCoverage ? "有效至 " + new GameTimestamp(S.HospitalPolicyUntil) + " · 报销80%，不封顶" : "未生效或已到期"), 17);
            if (S.HospitalPolicyUntil > 0)
            {
                var row = Ui.Row(w.Content, 38, 8); Ui.Size(Ui.Label(row, "年度保费¥3,000 · 自行续费", 17), -1, 38, 1);
                Ui.Size(Ui.Button(row, "续保一年", () => { if (!S.BuyHospitalPolicy()) Toast("现金不足，未续保。"); w.Rebuild(); }, 17), 120, 38);
            }
            foreach (var i in S.PrivateInvestments)
                Line(w.Content, "私人投资¥10,000 · " + (i.Settled ? i.Success ? "已回款¥20,000" : "本金已损失" : new GameTimestamp(i.At) + "到期 · 成功率" + i.Chance.ToString("P0")), 17);
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
        public void OpenEventScratch()
        {
            OpenWindow("刮刮乐", 760, 400, w =>
            {
                var ticket = S.ScratchTicket;
                if (ticket == null) { Line(w.Content, "没有待刮的彩票。", 18); return; }
                Line(w.Content, "按住鼠标左键，在银色区域来回刮。", 19);
                var area = Ui.Rect(w.Content, "ScratchTicketSurface"); Ui.Size(area, -1, 180);
                var prize = Ui.Label(area, ticket.PrizeYuan > 0 ? "奖金 ¥" + ticket.PrizeYuan : "谢谢参与", 40, TextAnchor.MiddleCenter, Ui.Accent);
                prize.raycastTarget = false; var pr = prize.rectTransform; pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
                var cover = Ui.Rect(area, "ScratchCover"); cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one; cover.offsetMin = cover.offsetMax = Vector2.zero;
                var graphic = cover.gameObject.AddComponent<ScratchTicketGraphic>(); graphic.Session = S;
                var row = Ui.Row(w.Content, 40, 8); var status = Ui.Size(Ui.Label(row, "", 18), -1, 40, 1);
                var claim = Ui.Button(row, "兑奖", () => { if (S.ClaimEventScratch()) w.Rebuild(); }, 18); Ui.Size(claim, 100, 40);
                void Refresh() { var t = S.ScratchTicket; status.text = t.Paid ? (t.PrizeYuan > 0 ? "奖金已到账" : "本张未中奖") : "已刮开 " + (t.Cleared.Count(c => c) * 100 / 144) + "%"; claim.interactable = S.CanClaimScratch; }
                graphic.Changed = Refresh; Refresh(); graphic.SetVerticesDirty();
            });
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
