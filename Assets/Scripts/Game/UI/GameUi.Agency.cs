using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _agencySelectedId;
        private AgencyStreetMap.ViewState _agencyViewState = new AgencyStreetMap.ViewState();
        private static readonly Color AgencyPaper = new Color32(230, 224, 207, 255);
        private static readonly Color AgencyInk = new Color32(46, 66, 61, 255);
        private static readonly Color AgencyMuted = new Color32(106, 115, 100, 255);
        private static readonly Color AgencyFuture = new Color32(157, 113, 66, 255);

        // Selection is local UI state. Looking at a property never moves the player or signs a lease.
        private void BuildAgency(Window w, Place agency)
        {
            var units = S.City.Places.Where(p => S.AgencyServes(agency, p))
                .OrderBy(p => S.City.ViewPosition(p.NodeId).X).ToArray();
            if (!units.Any(p => p.Id == _agencySelectedId))
                _agencySelectedId = S.Listings.Where(l => S.AgencyServes(agency, S.ListingPlace(l))).OrderBy(l => S.LeaseBlocker(l) == null ? 0 : 1)
                    .ThenBy(l => l.OpenDay).ThenBy(l => l.Rent.Cents).FirstOrDefault()?.PlaceId
                    ?? agency.Id;

            var header = Ui.Row(w.Content, 54, 12);
            if (agency.Id == "agency")
            {
                var portrait = Ui.Rect(header, "AgentPortrait");
                Ui.Size(portrait, 40, 54);
                var image = portrait.gameObject.AddComponent<RawImage>();
                image.texture = PortraitOf(Rosters.AgentChen.Id);
                var talk = portrait.gameObject.AddComponent<Button>();
                talk.targetGraphic = image;
                talk.onClick.AddListener(() => OpenGreeterDialogue(agency, w));
                talk.Tip("和小陈聊聊");
            }
            string discount = S.AgentDiscount > 0 ? $"中介费减免 {S.AgentDiscount * 100:0.#}%" : "中介费暂无折扣";
            Ui.Size(Ui.Label(header, $"<b>{S.City.StreetName(S.AgencyStreetOf(agency))} · 本街房源</b>　现金 {S.Ledger.Balance}\n<size=16><color=#ABB5A8>仅办理本街房源。点建筑查看位置、客流和户型，选好了再签约。{discount}</color></size>", 20), -1, 54, 1);
            if (BusinessUnlocked) Ui.Size(Ui.Button(header, "分店 / 物业 / 住处", OpenAgencyBusiness, 17), 210);

            var body = Ui.Row(w.Content, 740, 14);
            var map = Ui.Panel(body, "AgencyMap", Ui.PanelBg2).rectTransform;
            Ui.Size(map, 1120, 740);
            map.GetComponent<LayoutElement>().minWidth = 1120;
            BuildAgencyMap(map, units, w);
            var detail = Ui.Panel(body, "AgencyDetail", Ui.PanelBg2).rectTransform;
            Ui.Size(detail, -1, 740, 1);
            Ui.VList(detail, 4, 12);
            Place selected = units.FirstOrDefault(p => p.Id == _agencySelectedId);
            if (selected != null) BuildAgencyDetail(detail, selected, System.Array.IndexOf(units, selected) + 1, w);
        }

        private ShopListing AgencyListing(Place p) => S.Listings.FirstOrDefault(l => l.PlaceId == p.Id);

        private string AgencyStatus(Place p, ShopListing listing)
        {
            if (p.Kind == PlaceKind.Agency) return "房产中介 · 你在这里";
            if (S.IsMyShop(p)) return S.ShopAt(p) == S.MainShop ? "我的主店" : S.PlaceTitle(p);
            if (S.IsPendingShop(p)) return $"已提前签下 · {S.PendingLease.StartDay - S.Clock.Today.TotalDays} 天后交房";
            if (S.VentureAt(p.Id) != null || S.PropertyAt(p.Id) != null) return "我的经营资产";
            if (listing == null) return S.IsVacant(p) ? "空置 · 暂未挂牌" : "营业中 · 暂未挂牌";
            int wait = listing.OpenDay - S.Clock.Today.TotalDays;
            return wait > 0 ? $"{wait} 天后可租" : "正在出租";
        }

        private Color AgencyColor(Place p, ShopListing listing)
        {
            if (S.IsMyShop(p) || S.VentureAt(p.Id) != null || S.PropertyAt(p.Id) != null || p.Kind == PlaceKind.Agency)
                return new Color32(81, 111, 126, 255);
            if (listing == null) return new Color32(190, 193, 177, 255);
            return listing.OpenDay > S.Clock.Today.TotalDays ? AgencyFuture : Ui.ButtonHot;
        }

        private void BuildAgencyDetail(RectTransform parent, Place p, int number, Window w)
        {
            ShopListing l = AgencyListing(p);
            bool mine = S.IsMyShop(p);
            Ui.Size(Ui.Label(parent, $"<b>{number:00}　{(mine ? S.PlaceTitle(p) : l != null ? UnitName(p) : p.Name)}</b>", 23), -1, 30);
            Ui.Size(Ui.Label(parent, AgencyStatus(p, l), 17, TextAnchor.MiddleLeft, Ui.Accent), -1, 22);
            if (p.Kind == PlaceKind.Agency)
            {
                Para(parent, "你正在本街中介办公室里看房。地图只包含本街沿街建筑；绿光表示现在可租，橙光表示之后可租；其余建筑保持原样。点建筑后在右侧查看报价与户型。", 18, Ui.Dim);
                return;
            }
            if (p.Kind != PlaceKind.Shop)
            {
                Para(parent, "这是本街地点，不属于商铺租赁房源。请选择发出绿光或橙光的铺面。", 18, Ui.Dim);
                return;
            }
            bool pending = S.IsPendingShop(p);
            OwnedShop own = S.ShopAt(p);
            Money? rent = l != null ? l.Rent : mine ? (Money?)S.RentOf(own) : pending ? (Money?)S.PendingLease.Rent : null;
            var metrics = Ui.Row(parent, 60, 12);
            Ui.Size(Ui.Label(metrics, $"<size=14>每月租金</size>\n<b>{(rent.HasValue ? rent.Value.ToString() : "未挂牌")}</b>", 27), -1, 60, 1);
            Ui.Size(Ui.Label(metrics, $"<size=14>室内面积 · {GameSession.SizeName(p.Size)}</size>\n<b>{RealEstate.SizeArea(p.Size)}</b>", 27), -1, 60, 1);
            double flow = S.LocationFactorOf(p);
            Ui.Size(Ui.Label(parent, $"距地铁 {S.StationDistance(p):0} 米　　地段客流 ×{flow:0.00}", 17), -1, 24);
            Ui.Size(Ui.Bar(parent, (float)flow, Ui.Good), -1, 6);
            Ui.Size(Ui.Label(parent, "条越长，地段客流越好；实际进店还受经营影响。", 14, TextAnchor.MiddleLeft, Ui.Dim), -1, 22);
            BuildAgencyFloorPlan(parent, p);

            if (l == null)
            {
                if (pending) AgencyEntryWarning(parent, S.PendingLease.StartDay);
                string explanation = mine
                        ? $"这是你的{S.ShopTitle(own)}，已交押金 {own.Lease.Deposit}。不想要了就到店里办退租（先叫货车把货搬走）。"
                    : pending ? $"你已经提前签下这间，押金 {S.PendingLease.Deposit}。现租户第 {S.PendingLease.StartDay + 1} 天搬走，那天自动交房；在那之前房租照交。"
                    : S.VentureAt(p.Id) != null || S.PropertyAt(p.Id) != null ? "这间铺面已经纳入你的经营资产，可在“分店 / 物业 / 住处”里管理。"
                    : "这间铺面目前没有出租报价。你可以先了解位置和户型，等它挂牌后再来询价。";
                Para(parent, explanation, 18, Ui.Dim);
                return;
            }

            bool hasShop = S.HasShop;
            AgencyQuote(parent, l, hasShop);
            Money cost = S.LeaseCost(l, hasShop);
            Ui.Size(Ui.Label(parent, $"<b>{AgencySettlement(cost)}</b>", 23, TextAnchor.MiddleLeft, Ui.Accent), -1, 32);
            Money remaining = S.Ledger.Balance - cost;
            Ui.Size(Ui.Label(parent, remaining.IsNegative ? $"现金还差 {remaining.Abs()}" : $"付清后剩余现金 {remaining}", 17,
                TextAnchor.MiddleLeft, remaining.IsNegative ? new Color32(231, 160, 135, 255) : Ui.Good), -1, 24);
            string blocker = S.LeaseBlocker(l, hasShop);
            bool advance = S.IsAdvance(l);
            if (advance) AgencyEntryWarning(parent, l.OpenDay);
            else Ui.Size(Ui.Label(parent, blocker ?? $"挂牌还剩 {l.CloseDay - S.Clock.Today.TotalDays + 1} 天 · 可以签约", 16,
                TextAnchor.MiddleLeft, blocker == null ? Ui.Good : Ui.Accent), -1, 38);
            var sign = Ui.Button(parent, advance ? $"提前签约（还需等 {l.OpenDay - S.Clock.Today.TotalDays} 天）" : "核对费用，租下这间", () => ConfirmAgencyLease(w, p.Id, hasShop), 19,
                Ui.ButtonHot, blocker == null);
            sign.name = "AgencyReviewLease";
            Ui.Size(sign, -1, 44);
        }

        private void AgencyEntryWarning(Transform parent, int openDay)
        {
            int days = System.Math.Max(0, openDay - S.Clock.Today.TotalDays);
            var warning = Ui.Panel(parent, "AgencyEntryWarning", new Color32(79, 43, 27, 255));
            Ui.Size(warning, -1, 82);
            var accent = new Color32(255, 184, 104, 255);
            Ui.Panel(warning.transform, "WarningStripe", accent).rectTransform.Anchor(0, 0, 0, 1, 0, 0, -5, 0);
            var heading = Ui.Label(warning.transform, $"<b>还需等待 {days} 天才能进入</b>", 25, TextAnchor.MiddleLeft, accent);
            heading.name = "AgencyEntryCountdown";
            heading.rectTransform.Anchor(0, .48f, 1, 1, 14, 0, 8, 2);
            var detail = Ui.Label(warning.transform, $"<b>不能立即入驻 / 营业</b> · 第 {openDay + 1} 天交房\n提前签约，房租从签约当天开始计算", 14, TextAnchor.UpperLeft, new Color32(255, 222, 181, 255));
            detail.rectTransform.Anchor(0, 0, 1, .48f, 14, 5, 8, 0);
        }

        private void AgencyQuote(Transform parent, ShopListing l, bool newShop)
        {
            AgencyFeeLine(parent, "首月租金", l.Rent.ToString());
            AgencyFeeLine(parent, "押金（退租时退还）", RealEstate.Deposit(l.Rent).ToString());
            AgencyFeeLine(parent, S.AgentDiscount > 0 ? $"中介费（已减 {S.AgentDiscount * 100:0.#}%）" : "中介费", S.AgentFeeFor(l.Rent).ToString());
            if (S.HasShop && !newShop && !S.IsAdvance(l))
            {
                AgencyFeeLine(parent, "搬家费", RealEstate.MoveFee.ToString());
                AgencyFeeLine(parent, "退回旧铺押金", "− " + S.Lease.Deposit);
            }
        }

        private static void AgencyFeeLine(Transform parent, string label, string value)
        {
            var row = Ui.Row(parent, 22);
            Ui.Size(Ui.Label(row, label, 16, TextAnchor.MiddleLeft, Ui.Dim), -1, 22, 1);
            Ui.Size(Ui.Label(row, value, 17, TextAnchor.MiddleRight), 130, 22);
        }

        private static string AgencySettlement(Money cost) => cost.IsNegative ? $"本次净退回　{cost.Abs()}" : $"本次实付　{cost}";

        private void ConfirmAgencyLease(Window parent, string placeId, bool newShop)
        {
            ShopListing listing = S.Listings.FirstOrDefault(l => l.PlaceId == placeId);
            if (listing == null || S.LeaseBlocker(listing, newShop) != null) { parent.Rebuild(); return; }
            Place p = S.ListingPlace(listing);
            Money quotedCost = S.LeaseCost(listing, newShop);
            string retailKind = newShop ? "computer" : S.RetailKind;
            OpenWindow("确认租赁 · " + UnitName(p), 660, S.IsAdvance(listing) ? 620 : 520, confirm =>
            {
                Line(confirm.Content, $"{GameSession.SizeName(p.Size)} · {RealEstate.SizeArea(p.Size)} · 月租 {listing.Rent}", 21, Ui.Accent);
                if (S.IsAdvance(listing)) AgencyEntryWarning(confirm.Content, listing.OpenDay);
                if (newShop)
                {
                    Line(confirm.Content, "选择经营类型", 19, Ui.Accent);
                    var kinds = Ui.Row(confirm.Content, 42);
                    foreach (var value in PCShopSell.Core.Home.RetailShopTypes.All)
                    {
                        string kind = value;
                        Ui.Size(Ui.Button(kinds, PCShopSell.Core.Home.RetailShopTypes.Name(kind), () => { retailKind = kind; confirm.Rebuild(); }, 17,
                            retailKind == kind ? Ui.ButtonHot : Ui.ButtonBg, S.RetailTypeBlocker(kind) == null), -1, -1, 1);
                    }
                    Para(confirm.Content, PCShopSell.Core.Home.RetailShopTypes.Description(retailKind), 16, Ui.Dim);
                    if (!S.OtherRetailShopsUnlocked) Para(confirm.Content, "第 15 天起可选择其他经营类型。", 15, Ui.Dim);
                }
                AgencyQuote(confirm.Content, listing, newShop);
                Line(confirm.Content, $"{AgencySettlement(quotedCost)}　剩余 {S.Ledger.Balance - quotedCost}", 23, Ui.Good);
                Para(confirm.Content, newShop
                    ? "铺子是毛坯的，原来的店照常营业。货可以打电话叫货车从别的店或家里搬过来；人不在店里时要雇人看店才开门。"
                    : "铺子是毛坯的，签好后自己到店里，摆好收银台和货架再开业。", 17);
                string fine = "以后每月 1 日交租，押金退租时退还。";
                if (S.IsAdvance(listing)) fine += $"现租户第 {listing.OpenDay + 1} 天才搬走，到那天才能搬进去，房租从签约当天算起。";
                Para(confirm.Content, fine, 14, Ui.Dim);
                var row = Ui.Row(confirm.Content, 46, 10);
                Ui.Size(Ui.Button(row, "再看看", confirm.Close, 18), -1, 46, 1);
                var pay = Ui.Button(row, S.IsAdvance(listing) ? "确认提前签约" : quotedCost.IsNegative ? "确认签约并结算" : "确认签约并付款", () =>
                {
                    ShopListing live = S.Listings.FirstOrDefault(l => l.PlaceId == placeId);
                    confirm.Close();
                    if (!S.InAgency || live == null || S.LeaseBlocker(live, newShop) != null || S.LeaseCost(live, newShop) != quotedCost)
                    {
                        Toast("房源或费用有变化，请重新核对。");
                        parent.Rebuild();
                        return;
                    }
                    if (S.SignRetailLease(placeId, newShop, retailKind))
                    {
                        if (S.PendingLease != null) Toast($"已提前签约，还需等待 {S.PendingLease.StartDay - S.Clock.Today.TotalDays} 天才能进入，期间房租照常计算。");
                        else Toast("签约完成，可以自行到新店装修了。");
                    }
                    parent.Rebuild();
                }, 18, Ui.ButtonHot);
                pay.name = "AgencyConfirmLease";
                Ui.Size(pay, -1, 46, 1);
            });
        }

        private static string UnitName(Place p) => p.Business == RealEstate.VacantBusiness ? p.Name : p.Name + "原址";
    }
}
