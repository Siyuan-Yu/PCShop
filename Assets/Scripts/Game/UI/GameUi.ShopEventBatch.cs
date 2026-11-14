using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenShopEventTasks()
        {
            OpenWindow("经营待办", 850, 700, w =>
            {
                BuildMoneyEventTasks(w);
                if (S.MyShops.Any(s => S.HasEventMembership(s)) || S.EventCoupon != null || S.EventFlashSales.Length > 0 || S.AtBank)
                    Line(w.Content, "会员与采购优惠", 20, Ui.Accent);
                foreach (var shop in S.MyShops.Where(s => S.HasEventMembership(s)))
                {
                    Line(w.Content, S.ShopTitle(shop) + " · 满¥100积1分，50分抵下次¥100", 17);
                    foreach (var m in S.EventMembers.Where(m => m.ShopId == shop.Place.Id && m.SignedDay == (shop.Lease?.SignedDay ?? -1)))
                        Line(w.Content, (S.CityPerson(m.PersonId)?.Name ?? m.PersonId) + " · " + m.Points + "分", 17);
                }
                var coupon = S.EventCoupon;
                if (coupon != null) Line(w.Content, "采购券：满¥5,000减¥500 · " + (coupon.Used ? "已使用" : S.Clock.Now.TotalMinutes >= coupon.Until ? "已过期" : "有效至 " + new GameTimestamp(coupon.Until)), 17);
                if (S.AtBank)
                {
                    var row = Ui.Row(w.Content, 38, 8);
                    Ui.Size(Ui.Label(row, "存款" + S.BankDeposit + " · 待还借款" + S.AppLoanDebt, 17), -1, 38, 1);
                    Ui.Size(Ui.Button(row, "柜台业务", () => OpenBank(S.CurrentPlace), 17), 110, 38);
                }
                foreach (var f in S.EventFlashSales)
                    Line(w.Content, (S.ShopAt(f.ShopId) is OwnedShop flashShop ? S.ShopTitle(flashShop) : f.ShopId) + " · " + (PartCatalog.Exists(f.ProductId) ? PartCatalog.Get(f.ProductId).Name : PCShopSell.Core.Home.Gifts.Get(f.ProductId)?.Name) + " 九折 · 余" + f.Remaining + "笔 · " + new GameTimestamp(f.Until) + "结束", 17);
                foreach (var entry in S.ShopEventTasks)
                {
                    var t = entry; var place = S.City.HasPlace(t.ShopId) ? S.City.GetPlace(t.ShopId) : null;
                    string title = t.Kind switch { "sample" => "试用样品", "claim" => "物流赔付", "design" => "设计师布置", "self-display" => "自己调整陈列", "audit" => "库存盘点", "evening" => "晚间宣传", "bank" => "银行柜台", "subsidy" => "经营补贴", "subsidy-donation" => "自愿捐赠", "return-customer" => "老顾客到访", _ => "顾客反馈" };
                    var targetShop = S.ShopAt(t.ShopId);
                    Line(w.Content, title + " · " + (targetShop != null ? S.ShopTitle(targetShop) : place?.Name ?? "原门店"), 20, Ui.Accent);
                    if (t.Kind == "sample") Line(w.Content, PartCatalog.Get(t.PartId).Name + " · 借用中", 18);
                    string status = t.Status;
                    if (t.Started && t.Remaining > 0) status = "进行中 · 还需" + t.Remaining + "分钟";
                    if (t.At > S.Clock.Now.TotalMinutes) status += " · " + new GameTimestamp(t.At);
                    Line(w.Content, status, 17);
                    if (t.Kind == "bank" && !t.Started)
                    {
                        var row = Ui.Row(w.Content, 38, 8);
                        Ui.Size(Ui.Label(row, "营业时到柜台开始 · " + t.Remaining + "分钟", 17), -1, 38, 1);
                        Ui.Size(Ui.Button(row, "开始", () => { if (S.StartBankEventTask(t.Id)) { CloseWindowSilently(w); S.Time.SetPaused(false); } }, 17, Ui.ButtonBg, S.BankBlocker() == null), 90, 38);
                    }
                    if (t.Kind == "subsidy-donation")
                    {
                        var row = Ui.Row(w.Content, 38, 8);
                        Ui.Size(Ui.Label(row, "补贴已到账，自愿捐出一半", 17), -1, 38, 1);
                        Ui.Size(Ui.Button(row, "捐¥1,500", () => { S.DonateEventSubsidy(t.Id); w.Rebuild(); }, 17), 125, 38);
                    }
                    if (place != null && S.CurrentPlace != place && t.Kind is not ("claim" or "complaint" or "evening"))
                        Ui.Size(Ui.Button(w.Content, t.Kind == "bank" ? "前往银行" : "前往门店", () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 18), -1, 42);
                    if (!t.Started && t.Kind is "design" or "self-display" or "audit")
                        Ui.Size(Ui.Button(w.Content, t.Kind == "design" ? "开始布置 · ¥1,500 · 今日歇业" : t.Kind == "self-display" ? "打开装修 · 今日歇业" : t.Kind == "sample" ? "试用 · 10分钟" : "暂停接待，开始盘点", () =>
                        {
                            if (!S.StartShopEventTask(t.Id)) return;
                            CloseWindowSilently(w);
                            if (t.Kind == "self-display") StartPlacement(true); else S.Time.SetPaused(false);
                        }, 18, Ui.ButtonHot, S.CurrentPlace?.Id == t.ShopId && S.Clock.Now.TotalMinutes >= t.At), -1, 44);
                    if (t.Kind == "sample" && t.Status is "已到店" or "已试用")
                    {
                        var row = Ui.Row(w.Content, 38, 8);
                        Ui.Size(Ui.Label(row, "买断价 " + Money.FromCents(t.Paid), 17), -1, 38, 1);
                        if (t.Status == "已到店")
                            Ui.Size(Ui.Button(row, "试用10分钟", () => { if (S.StartShopEventTask(t.Id)) { CloseWindowSilently(w); S.Time.SetPaused(false); } }, 17, Ui.ButtonBg, S.CurrentPlace?.Id == t.ShopId), 130, 38);
                        Ui.Size(Ui.Button(row, "归还", () => { S.FinishSupplierSample(t.Id, false); w.Rebuild(); }, 17, Ui.ButtonBg, S.CurrentPlace?.Id == t.ShopId), 90, 38);
                        Ui.Size(Ui.Button(row, "买下", () => { S.FinishSupplierSample(t.Id, true); w.Rebuild(); }, 17, Ui.ButtonBg, S.CurrentPlace?.Id == t.ShopId), 90, 38);
                    }
                    if (t.Started && t.Remaining > 0)
                        Ui.Size(Ui.Button(w.Content, "收起，继续", () => { CloseWindowSilently(w); S.Time.SetPaused(false); }, 18, Ui.ButtonHot), -1, 42);
                }
                foreach (var shop in S.MyShops.Where(shop => S.Flags.Contains("shop-event:aircon:" + shop.Place.Id)))
                {
                    var target = shop;
                    Line(w.Content, "为" + S.ShopTitle(shop) + "选购空调", 20, Ui.Accent);
                    Ui.Size(Ui.Button(w.Content, "家具采购 · 冷暖空调¥1,200 / 大店空调¥3,600", () => { _onlineTab = 1; OpenOnlinePanel(target); }, 18), -1, 44);
                }
                foreach (var shop in S.MyShops.Where(shop => S.Flags.Contains("shop-event:procure:" + shop.Place.Id)))
                {
                    var target = shop;
                    var row = Ui.Row(w.Content, 38, 8);
                    Ui.Size(Ui.Label(row, S.ShopTitle(shop) + " · 自选配件采购", 17), -1, 38, 1);
                    Ui.Size(Ui.Button(row, "选购", () => { _onlineTab = 5; OpenOnlinePanel(target); }, 17), 90, 38);
                }
                if (S.ShopEventTasks.Length == 0 && !S.PrivateInvestments.Any(i => !i.Settled)
                    && !(S.ScratchTicket is EventScratchTicket ticket && !ticket.Paid)
                    && !S.EventBills.Any(b => b.CreditCents > 0 || S.EventBillRefundDue(b.Id) > 0)
                    && !S.Flags.Contains("event-idle-sale")) Line(w.Content, "暂时没有待处理事件。", 18, Ui.Dim);
            });
        }
    }
}
