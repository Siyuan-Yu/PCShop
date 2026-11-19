using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenModelCommissions()
        {
            var shop = S.CurrentShop;
            if (!S.InPlayerShop || shop.RetailKind != "models") return;
            bool history = false;
            OpenWindow("模型委托", 880, 660, w =>
            {
                var tabs = Ui.Row(w.Content, 36, 8);
                Ui.Size(Ui.Button(tabs, "在店订单", () => { history = false; w.Rebuild(); },
                    16, history ? Ui.ButtonBg : Ui.ButtonHot), 128);
                Ui.Size(Ui.Button(tabs, "交付记录", () => { history = true; w.Rebuild(); },
                    16, history ? Ui.ButtonHot : Ui.ButtonBg), 128);

                var orders = S.ModelCommissions(shop).Where(order => history ==
                    (order.Stage == ModelCommissionStage.Delivered || order.Stage == ModelCommissionStage.Returned)).ToArray();
                if (orders.Length == 0)
                {
                    string message = history ? "暂无交付或归还记录。"
                        : shop.Layout.Any(p => p.Id == FurnitureCatalog.ModelPaintBench)
                            ? "暂无委托。营业时偶尔有顾客带自己的模型到店。"
                            : "先从家具城买一张喷漆台并摆进模型店，营业后即可收到委托。";
                    Para(w.Content, message, 17, Ui.Dim);
                    return;
                }

                var sorted = orders.OrderByDescending(o => o.Id).ToArray();
                for (int i = 0; i < sorted.Length; i += 2)
                {
                    var row = Ui.Row(w.Content, history ? 114 : 168, 8);
                    BuildModelCommissionCard(w, row, shop, sorted[i]);
                    if (i + 1 < sorted.Length) BuildModelCommissionCard(w, row, shop, sorted[i + 1]);
                    else Ui.Size(Ui.Rect(row, "EmptyOrderSlot"), 0, -1, 1);
                }
            });
        }

        private void BuildModelCommissionCard(Window w, Transform parent, OwnedShop shop, ModelCommission order)
        {
            var card = Ui.Size(Ui.FlatPanel(parent, "ModelCommission_" + order.Id), 0, -1, 1);
            var body = Ui.Rect(card.transform, "OrderDetails").Fill(8);
            Ui.VList(body, 3, 0);
            Line(body, $"{order.ModelName} · {order.Service}", 18);
            Line(body, $"{order.Customer} · 指定配色 {order.Theme}", 15, Ui.Dim);
            string status = order.Stage switch
            {
                ModelCommissionStage.Offered => $"等待接单 · {new GameTimestamp(order.OfferUntil)} 前有效",
                ModelCommissionStage.Waiting => $"已接单 · 开工耗材 ¥{order.Materials} · 基础工费 ¥{order.BaseFee}",
                ModelCommissionStage.EmployeeWork => $"员工加工 {order.Work / order.WorkMinutes:0%} · 在岗且有空台时推进",
                ModelCommissionStage.ManualWork => $"手工加工 {order.Passes}/3 · 下一步 {order.PassName}",
                ModelCommissionStage.Ready => $"待交付 · 品质 {order.Quality:0.#}/10 · 工费 ¥{order.Payout}",
                ModelCommissionStage.Delivered => $"已交付 · 品质 {order.Quality:0.#}/10 · 已收 ¥{order.Payout}",
                _ => "已归还 · 未收工费"
            };
            Line(body, status, 15, Ui.Accent);
            bool workChoice = order.Stage == ModelCommissionStage.Waiting || order.Stage == ModelCommissionStage.ManualWork;
            if (!workChoice && (order.Stage == ModelCommissionStage.Delivered || order.Stage == ModelCommissionStage.Returned)) return;

            var actions = Ui.Row(body, 34, 8);
            void Action(string label, System.Func<bool> run, string blocker = null)
            {
                var button = Ui.Size(Ui.Button(actions, label, () => { if (run()) w.Rebuild(); },
                    15, Ui.ButtonBg, blocker == null), -1, 34, 1);
                if (blocker != null) button.Tip(blocker);
            }

            if (order.Stage == ModelCommissionStage.Offered)
            {
                Action("接下委托", () => S.AcceptModelCommission(shop, order.Id));
                return;
            }
            if (workChoice)
            {
                string manualBlocker = S.ModelCommissionWorkBlocker(shop, order, true);
                if (manualBlocker == null && !_ctrl.Interior.NearModelPaintBench)
                    manualBlocker = "靠近喷漆台按 F，可手工加工。";
                Action(order.Stage == ModelCommissionStage.Waiting ? "亲自加工" : "继续手工加工", () =>
                {
                    if (!S.StartModelCommission(shop, order.Id, true)) return false;
                    OpenModelPaintWork(w, shop, order);
                    return true;
                }, manualBlocker);
                if (manualBlocker != null) Line(body, manualBlocker, 14, Ui.Dim);
                if (order.Stage == ModelCommissionStage.Waiting)
                    Action("交给喷漆师", () => S.StartModelCommission(shop, order.Id, false),
                        S.ModelCommissionWorkBlocker(shop, order, false));
            }
            if (order.Stage == ModelCommissionStage.Ready)
                Action($"交付并收款  ¥{order.Payout}", () => S.DeliverModelCommission(shop, order.Id));
            Action("归还模型", () =>
            {
                OpenWindow("归还模型", 480, 240, confirm =>
                {
                    Para(confirm.Content, "归还后结束委托。已使用的耗材不会退回，也不会收取工费。", 16);
                    Ui.Size(Ui.Button(confirm.Content, "确认归还", () =>
                    {
                        if (!S.ReturnModelCommission(shop, order.Id)) return;
                        confirm.Close();
                        w.Rebuild();
                    }, 16, Ui.ButtonHot), -1, 36);
                });
                return false;
            });
        }

        private void OpenModelPaintWork(Window desk, OwnedShop shop, ModelCommission order)
        {
            OpenWindow("喷漆台 · " + order.ModelName, 720, 410, w =>
            {
                if (order.Stage == ModelCommissionStage.Ready)
                {
                    Line(w.Content, $"加工完成 · 品质 {order.Quality:0.#}/10", 22, Ui.Accent);
                    Para(w.Content, $"成品已准备交付。交付后可收取 ¥{order.Payout}。", 17);
                    Ui.Size(Ui.Button(w.Content, "返回委托", w.Close, 16, Ui.ButtonHot), -1, 42);
                    return;
                }
                int step = order.Passes;
                Line(w.Content, $"{step + 1}/3 · {order.PassName} · {order.Theme}", 20, Ui.Accent);
                Para(w.Content, "等游标进入绿色区域再确认。越接近目标中心，这道工序的得分越高。", 17);

                float[] centers = { .32f, .68f, .48f };
                float[] widths = { .29f, .23f, .19f };
                float center = centers[(step + order.Id % 3) % centers.Length];
                float halfWidth = widths[step] / 2;
                var track = Ui.Size(Ui.Panel(w.Content, "PaintTrack", Ui.PanelBg2), -1, 52);
                var target = Ui.Panel(track.transform, "PaintTarget", Ui.Good).rectTransform;
                target.anchorMin = new Vector2(center - halfWidth, .16f);
                target.anchorMax = new Vector2(center + halfWidth, .84f);
                target.offsetMin = target.offsetMax = Vector2.zero;
                var marker = Ui.Panel(track.transform, "PaintCursor", Color.white).rectTransform;
                marker.offsetMin = new Vector2(-3, 3);
                marker.offsetMax = new Vector2(3, -3);

                float started = Time.unscaledTime;
                float Position() => Mathf.PingPong((Time.unscaledTime - started) * (.52f + step * .17f), 1);
                void RefreshMarker()
                {
                    float position = Position();
                    marker.anchorMin = new Vector2(position, 0);
                    marker.anchorMax = new Vector2(position, 1);
                }

                RefreshMarker();
                w.RefreshContent = RefreshMarker;
                Ui.Size(Ui.Button(w.Content, "确认" + order.PassName, () =>
                {
                    float distance = Mathf.Abs(Position() - center);
                    double accuracy = distance <= halfWidth
                        ? Mathf.Lerp(1, .65f, distance / halfWidth)
                        : .65f * Mathf.Clamp01(1 - (distance - halfWidth) / .3f);
                    if (!S.CompleteModelCommissionPass(shop, order.Id, accuracy)) return;
                    desk.Rebuild();
                    w.Rebuild();
                }, 18, Ui.ButtonHot), -1, 44);
            });
        }
    }
}
