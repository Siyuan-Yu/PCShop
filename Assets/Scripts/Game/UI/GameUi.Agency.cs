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
        private bool _agencyResidential;
        private string _agencyMapDistrictId;
        private AgencyStreetMap.ViewState _agencyViewState = new AgencyStreetMap.ViewState();
        private static readonly Color AgencyPaper = new Color32(230, 224, 207, 255);
        private static readonly Color AgencyInk = new Color32(46, 66, 61, 255);
        private static readonly Color AgencyMuted = new Color32(106, 115, 100, 255);
        private static readonly Color AgencyFuture = new Color32(157, 113, 66, 255);
        private string AgencyDiscountPercent => $"{S.AgentDiscount * 100:0.#}%";
        private static string NewShopHours => $"{Ui.Hm(ShopSimulation.DefaultOpenMinute)}–{Ui.Hm(ShopSimulation.DefaultCloseMinute)}";

        // Selection is local UI state. Looking at a property never moves the player or signs a lease.
        private void BuildAgency(Window w, Place agency)
        {
            if(_agencyMapDistrictId!=agency.Id){_agencyMapDistrictId=agency.Id;_agencyViewState=new AgencyStreetMap.ViewState();}
            var versions = Ui.Row(w.Content, 42, 8);
            Ui.Size(Ui.Button(versions,"商铺",()=>{_agencyResidential=false;w.Rebuild();},21,!_agencyResidential?Ui.ButtonHot:Ui.ButtonBg),-1,42,1).name="AgencyCommercialTab";
            Ui.Size(Ui.Button(versions,"住宅",()=>{_agencyResidential=true;w.Rebuild();},21,_agencyResidential?Ui.ButtonHot:Ui.ButtonBg),-1,42,1).name="AgencyResidentialTab";
            BuildAgencyHeader(w, agency);
            if(_agencyResidential){BuildResidentialAgency(w,agency);return;}
            var units = AgencyStreetMap.DistrictPlaces(S,agency);
            if (!units.Any(p => p.Id == _agencySelectedId))
                _agencySelectedId = S.Listings.Where(l => S.AgencyServes(agency, S.ListingPlace(l)))
                    .OrderBy(l => S.LookingForFirstShop && l.PlaceId==S.City.PlayerShop().Id ? 0 : 1)
                    .ThenBy(l => S.LeaseBlocker(l) == null ? 0 : 1)
                    .ThenBy(l => l.OpenDay).ThenBy(l => l.Rent.Cents).FirstOrDefault()?.PlaceId
                    ?? agency.Id;

            var (map, detail) = AgencyPropertyColumns(w.Content, "AgencyMap", "AgencyDetail");
            BuildAgencyMap(map, units, w, agency);
            Place selected = units.FirstOrDefault(p => p.Id == _agencySelectedId);
            if (selected != null) BuildAgencyDetail(detail, selected, System.Array.IndexOf(units, selected) + 1, w);
        }

        private void BuildAgencyHeader(Window w, Place agency)
        {
            var header = Ui.Row(w.Content, 78, 12);
            if (agency.Id == "agency")
            {
                var portrait = Ui.Rect(header, "AgentPortrait");
                Ui.Size(portrait, 40, 54);
                var image = FitPortrait(portrait, Rosters.AgentChen.Id);
                var talk = image.gameObject.AddComponent<Button>();
                talk.targetGraphic = image;
                talk.onClick.AddListener(() => OpenGreeterDialogue(agency, w));
                talk.Tip("和陈启明聊聊");
            }
            var district=S.City.AgencyDistrictFor(agency.StreetId);
            string coverage=district==null?S.City.StreetName(agency.StreetId):string.Join("、",district.streets.Select(S.City.StreetName));
            string detail = _agencyResidential ? "当前住处：" + S.HomeTitle
                : $"与{Rosters.AgentChen.Name}的熟悉度 {S.AgentRapport} · 中介费减免 {AgencyDiscountPercent}";
            Ui.Size(Ui.Label(header, $"<b>{S.City.AgencyCoverageName(agency)} · 本片区房源</b>　现金 {S.Ledger.Balance}\n<size=16>负责：{coverage}\n<color=#ABB5A8>{detail}</color></size>", 20), -1, 78, 1);
            if(BusinessUnlocked && !_agencyResidential)
            {
                Ui.Size(Ui.Button(header,"委托店长开店",()=>OpenBusinessSection(3),16),155);
                Ui.Size(Ui.Button(header,S.HasPark?"园区管理":"园区筹建",()=>OpenBusinessSection(6),16),135);
            }
        }

        private static (RectTransform map, RectTransform detail) AgencyPropertyColumns(Transform parent, string mapName, string detailName)
        {
            var body = Ui.Row(parent, 740, 14);
            var map = Ui.Panel(body, mapName, Ui.PanelBg2).rectTransform;
            Ui.Size(map, 1120, 740);
            map.GetComponent<LayoutElement>().minWidth = 1120;
            var detailPanel = Ui.Panel(body, detailName, Ui.PanelBg2).rectTransform;
            Ui.Size(detailPanel, -1, 740, 1);
            Ui.VList(detailPanel, 4, 10);
            return (map, Ui.Scroll(detailPanel, 720));
        }

        private static void AgencyPropertyHeading(Transform parent, string title, string status)
        {
            Ui.Size(Ui.Label(parent, $"<b>{title}</b>", 23), -1, 30);
            Ui.Size(Ui.Label(parent, status, 17, color: Ui.Accent), -1, 22);
        }

        private static void AgencyPropertyMetrics(Transform parent, string rentLabel, string rent, string price,
            string areaLabel, string area, string capacity = null)
        {
            var row = Ui.Row(parent, 60, 12);
            Metric(rentLabel, rent);
            Metric("全款售价", price);
            Metric(areaLabel, area, capacity);

            void Metric(string caption, string value, string note = null)
            {
                string text = $"<size=14>{caption}</size>\n<b>{value}</b>";
                if (note != null) text += $"\n<size=13>{note}</size>";
                var label = Ui.Size(Ui.Label(row, text, 23), 0, 60, 1);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = 23;
            }
        }

        private static void AgencyPropertyAction(Transform parent, string label, System.Action action, string name = null,
            bool primary = true, string blocker = null)
        {
            var button = Ui.Size(Ui.Button(parent, label, action, 18, primary ? Ui.ButtonHot : Ui.ButtonBg,
                blocker == null), -1, 44);
            if (name != null) button.name = name;
            if (blocker != null) Para(parent, blocker, 15, Ui.Dim);
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
            AgencyPropertyHeading(parent, $"{number:00}　{(mine ? S.PlaceTitle(p) : l != null ? UnitName(p) : p.Name)}", AgencyStatus(p, l));
            if (p.Kind == PlaceKind.Agency)
            {
                Para(parent, "你正在本片区中介办公室里看房。地图只包含本片区沿街建筑；绿光表示现在可租，橙光表示之后可租；其余建筑保持原样。点建筑后在右侧查看报价与户型。", 18, Ui.Dim);
                return;
            }
            if (p.Kind != PlaceKind.Shop)
            {
                Para(parent, "这是本片区地点，不属于商铺租赁房源。请选择发出绿光或橙光的铺面。", 18, Ui.Dim);
                return;
            }
            bool pending = S.IsPendingShop(p);
            OwnedShop own = S.ShopAt(p);
            var property = S.PropertyAt(p.Id);
            var purchase = property == null ? S.PreviewCityPropertyPurchase(p.Id, false) : null;
            bool hasShop = S.HasShop;
            string leaseBlocker = l != null ? S.LeaseBlocker(l, hasShop) : null;
            bool advance = l != null && S.IsAdvance(l);
            Money? rent = l != null ? l.Rent : mine ? (Money?)S.RentOf(own) : pending ? (Money?)S.PendingLease.Rent : null;
            bool dedicatedVenue = S.IsDedicatedVenue(p);
            string capacity = dedicatedVenue ? null : $"最多{GameSession.ShelfLimit(p.Size)}个货架 · {GameSession.StaffLimit(p.Size)}名员工";
            AgencyPropertyMetrics(parent, "每周租金", rent?.ToString() ?? "未挂牌", purchase?.Price.ToString() ?? "—",
                $"室内面积 · {GameSession.SizeName(p.Size)}", RealEstate.SizeArea(p.Size), capacity);
            if (l != null)
                AgencyPropertyAction(parent, advance ? "提前租赁" : "租赁", () => ConfirmAgencyLease(w, p.Id, hasShop),
                    "AgencyReviewLease", blocker: leaseBlocker);
            if (purchase != null)
                AgencyPropertyAction(parent, mine ? "全款买下当前租用铺面" : "全款购买", () => CityPropertyConfirm(p.Id, false, w),
                    "AgencyReviewPurchase", primary: l == null);
            BuildAgencyFloorPlan(parent, RoomSpec.ForShop(p.Size));
            var flow = S.FootfallOf(p);
            string previewKind = CompetitionPreviewKind(p);
            Ui.Size(Ui.Label(parent, $"预计每日入店约 {S.ExpectedDailyShopEntrants(p,previewKind):N0} 人", 22, TextAnchor.MiddleLeft, Ui.Good), -1, 32).name="AgencyDailyEntrants";
            string station=double.IsPositiveInfinity(flow.StationMeters)?"无可达地铁站":$"距最近地铁 {flow.StationMeters:0} 米";
            string housing=double.IsPositiveInfinity(flow.HousingMeters)?"附近无可达住宅":$"距最近住宅 {flow.HousingMeters:0} 米";
            Ui.Size(Ui.Label(parent,station+" · "+housing,15),-1,24);
            var information = Ui.Row(parent, 32, 6);
            var rentHelp = HelpLink(information,"租赁与客流说明",
                $"新店按所选店型、{NewShopHours}正常营业估算。知名度、品牌口碑、街道人流、周末和同类分流会影响客流，实际人数也受开门和值守时长影响。餐馆还受座位周转影响。\n新租报价随地段和面积带来的基础客流及周行情变化。签约付首周租金、一周押金和半周中介费，之后每七天交一次租。\n"
                + $"与{Rosters.AgentChen.Name}的熟悉度每增加 1 点，中介费减免 {RealEstate.AgentDiscountPerRapport * 100:0.#} 个百分点，最多减免 {RealEstate.MaxAgentDiscount * 100:0.#}%。\n"
                + "已签租金保留签约价。提前签约到交房日才能使用。退租前请清理库存，主动退租不退押金。");
            Ui.Size(rentHelp, 0, 32, 1);
            if (!dedicatedVenue)
                Ui.Size(Ui.Button(information, "商圈详情与试算", () => OpenShopCompetition(p), 15), 0, 32, 1);
            if(mine||pending)Line(parent,"已签租约保留原价。",14,Ui.Dim);
            if(dedicatedVenue)Para(parent,"专用场地 · "+S.VenueBusinessName(p)+"\n只能延续原有服务，不能改作零售店。接手不包含原教练劳动；需自己值守或另行招聘。每日维护 ¥"+S.VenueMaintenanceYuan(p)+"，租金及工资另计。",17,Ui.Accent);

            if (l == null)
            {
                if (pending) AgencyEntryWarning(parent, S.PendingLease.StartDay);
                string explanation = mine
                        ? own.OwnsProperty?"这是你的自有店铺，可以结束自营并保留产权。":$"这是你的{S.ShopTitle(own)}，已交押金 {own.Lease.Deposit}。可在这里办理退租，押金不退；请先搬走货物。"
                    : pending ? $"你已经提前签下这间，押金 {S.PendingLease.Deposit}。现租户第 {S.PendingLease.StartDay + 1} 天搬走，那天自动交房；在那之前房租照交。"
                    : S.VentureAt(p.Id) != null || property != null ? "这间铺面已纳入你的资产。经营收支可在资产页查看。"
                    : "这间铺面目前没有出租报价。";
                Para(parent, explanation, 18, Ui.Dim);
                if(mine)
                {
                    var close=Ui.Size(Ui.Button(parent,own.OwnsProperty?"结束自营":"退租店铺",()=>ConfirmCloseShop(own),18,Ui.Warn),-1,44);
                    close.name="AgencyCloseShop_"+p.Id;
                    string why=S.CloseShopBlocker(own);
                    if(why!=null)Line(parent,"办理前需处理："+why,16,Ui.Warn);
                }
                if (property != null) BuildCommercialPropertyActions(parent,p,property,w);
                return;
            }

            AgencyQuote(parent, l, hasShop);
            Money cost = S.LeaseCost(l, hasShop);
            Ui.Size(Ui.Label(parent, $"<b>{AgencySettlement(cost)}</b>", 23, TextAnchor.MiddleLeft, Ui.Accent), -1, 32);
            Money remaining = S.Ledger.Balance - cost;
            Ui.Size(Ui.Label(parent, remaining.IsNegative ? $"现金还差 {remaining.Abs()}" : $"付清后剩余现金 {remaining}", 17,
                TextAnchor.MiddleLeft, remaining.IsNegative ? new Color32(231, 160, 135, 255) : Ui.Good), -1, 24);
            if (advance) AgencyEntryWarning(parent, l.OpenDay);
            if (leaseBlocker == null)
                Line(parent, $"挂牌还剩 {l.CloseDay - S.Clock.Today.TotalDays + 1} 天 · 可以签约", 16, Ui.Good);
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
            AgencyFeeLine(parent, "首周租金", l.Rent.ToString());
            AgencyFeeLine(parent, "押金（主动退租不退）", RealEstate.Deposit(l.Rent).ToString());
            AgencyFeeLine(parent, $"中介费（熟悉度 {S.AgentRapport} · 减免 {AgencyDiscountPercent}）", S.AgentFeeFor(l.Rent).ToString());
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
            OpenWindow("确认租赁 · " + UnitName(p), 660, S.IsAdvance(listing) ? 620 : 520, confirm =>
            {
                Line(confirm.Content, $"{GameSession.SizeName(p.Size)} · {RealEstate.SizeArea(p.Size)} · 周租 {listing.Rent}", 21, Ui.Accent);
                string previewKind = CompetitionPreviewKind(p);
                Line(confirm.Content,$"{RetailShopTypes.Name(previewKind)} · 预计每日入店约 {S.ExpectedDailyShopEntrants(p,previewKind):N0} 人",19,Ui.Good);
                if (S.IsAdvance(listing)) AgencyEntryWarning(confirm.Content, listing.OpenDay);
                if(S.IsDedicatedVenue(p))
                    Para(confirm.Content,"专用场地："+S.VenueBusinessName(p)+"，只能延续原用途，不可改作零售店。\n保留原设施，另行安排值守。原教练不自动受雇；维护费 ¥"+S.VenueMaintenanceYuan(p)+"/天，员工按实际排班另付工资。老板自用免费，不产生营业收入。",18,Ui.Accent);
                else if (newShop || !S.HasShop)
                {
                    Para(confirm.Content,"租下后，还会单独确认“准备开什么店”。选择经营类型后，再发放对应的开店任务礼包。",17,Ui.Accent);
                }
                AgencyQuote(confirm.Content, listing, newShop);
                Line(confirm.Content, $"{AgencySettlement(quotedCost)}　剩余 {S.Ledger.Balance - quotedCost}", 23, Ui.Good);
                Para(confirm.Content, S.IsDedicatedVenue(p)?"接手后进入场馆管理，设置营业时间、价格并开启接客。顾客按场地容量付费使用；不提供普通店铺柜台或货架经营。":newShop
                    ? "铺子是毛坯的，原来的店照常营业。货可以打电话叫货车从别的店或家里搬过来；人不在店里时要雇人看店才开门。"
                    : "铺子是毛坯的，签好后自己到店里，按所选经营类型摆好设施再开业。", 17);
                string fine = "签约后每七天交一次周租。主动退租，已交押金不退。";
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
                    if (S.SignLeaseThenChooseKind(placeId, newShop))
                    {
                        if (S.PendingLease != null) Toast($"已提前签约，还需等待 {S.PendingLease.StartDay - S.Clock.Today.TotalDays} 天才能进入，期间房租照常计算。");
                        else Toast(S.IsDedicatedVenue(p)?"接手完成，请到场馆设置营业与值守。":"签约完成，可以自行到新店装修了。");
                        if(S.PendingShopKindPlaceId!=null){parent.Close();return;}
                    }
                    parent.Rebuild();
                }, 18, Ui.ButtonHot);
                pay.name = "AgencyConfirmLease";
                Ui.Size(pay, -1, 46, 1);
            });
        }

        private static string UnitName(Place p) => p.Business == RealEstate.VacantBusiness ? p.Name : p.Name + "原址";

        private void RefreshOpeningKindChoice()
        {
            string placeId=S.PendingShopKindPlaceId;
            if(placeId==null||AnyModalOpen||PopupOpen||S.IsGameOver)return;
            string selected=RetailShopTypes.Default;
            OpenWindow("准备开什么店？",720,640,w=>
            {
                Line(w.Content,S.City.StreetName(S.City.GetPlace(placeId).StreetId)+" · 已签约",21,Ui.Accent);
                Para(w.Content,"确认店型后即可准备开业。租金已经支付。",17);
                foreach(string kind in RetailShopTypes.All)
                {
                    var button=Ui.Size(Ui.Button(w.Content,(selected==kind?"✓ ":"")+RetailShopTypes.Name(kind),
                        ()=>{selected=kind;w.Rebuild();},18,selected==kind?Ui.ButtonHot:Ui.ButtonBg),-1,42);
                    button.name="OpeningKind_"+kind;
                }
                var place = S.City.GetPlace(placeId);
                Line(w.Content,$"新店 {NewShopHours} · 预计每日入店约 {S.ExpectedDailyShopEntrants(place,selected,asNewShop:true):N0} 人",17,Ui.Good);
                Para(w.Content,RetailShopTypes.Description(selected),16,Ui.Dim);
                Para(w.Content,"家具城开店清单："+string.Join("、",GameSession.OpeningFurniture(selected).Select(id=>FurnitureCatalog.Get(id).Name)),16,Ui.Good);
                Ui.Size(Ui.Button(w.Content,"确认开一家"+RetailShopTypes.Name(selected),()=>
                {
                    if(S.ConfirmOpeningKind(placeId,selected)){w.Close();RebuildDock();}
                },19,Ui.ButtonHot),-1,46).name="ConfirmOpeningKind";
            },closable:false);
        }
    }
}
