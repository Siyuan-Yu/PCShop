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
        private string _agencyQuotePlaceId;
        private bool _agencyPurchaseSelected;
        private AgencyStreetMap.ViewState _agencyViewState = new AgencyStreetMap.ViewState();
        private static readonly Color AgencyPaper = Ui.PanelBg2;
        private static readonly Color AgencyInk = Ui.TextColor;
        private static readonly Color AgencyMuted = Ui.Dim;
        private static readonly Color AgencyFuture = Ui.Accent;
        private static string AgencyDiscountPercent(int rapport) => $"{RealEstate.AgentDiscount(rapport) * 100:0.#}%";
        private static string NewShopHours => $"{Ui.Hm(ShopSimulation.DefaultOpenMinute)}–{Ui.Hm(ShopSimulation.DefaultCloseMinute)}";

        // Selection is local UI state. Looking at a property never moves the player or signs a lease.
        private void BuildAgency(Window w, Place agency)
        {
            if(_agencyMapDistrictId!=agency.Id){_agencyMapDistrictId=agency.Id;_agencyViewState=new AgencyStreetMap.ViewState();_agencyMyProperties=false;}
            var versions = Ui.Row(w.Content, 42, 8);
            Ui.Size(Ui.Button(versions,"商铺",()=>{_agencyMyProperties=false;_agencyResidential=false;w.Rebuild();},21,Ui.SelectedButton(!_agencyMyProperties&&!_agencyResidential)),-1,42,1).name="AgencyCommercialTab";
            Ui.Size(Ui.Button(versions,"住宅",()=>{_agencyMyProperties=false;_agencyResidential=true;w.Rebuild();},21,Ui.SelectedButton(!_agencyMyProperties&&_agencyResidential)),-1,42,1).name="AgencyResidentialTab";
            Ui.Size(Ui.Button(versions,"我的房产",()=>{_agencyMyProperties=true;_agencyMyResidences=_agencyResidential;w.Rebuild();},21,
                Ui.SelectedButton(_agencyMyProperties)),-1,42,1).name="AgencyOwnedTab";
            System.Action refreshProperty = null;
            BuildAgencyHeader(w, agency, () => refreshProperty?.Invoke());
            if (_agencyMyProperties)
            {
                BuildAgencyOwnedProperties(w, agency);
                return;
            }
            if (_agencyResidential)
            {
                refreshProperty = BuildResidentialAgency(w, agency);
                return;
            }
            var units = AgencyStreetMap.DistrictPlaces(S,agency);
            if (!units.Any(p => p.Id == _agencySelectedId))
                _agencySelectedId = S.Listings.Where(l => S.AgencyServes(agency, S.ListingPlace(l)))
                    .OrderBy(l => S.LookingForFirstShop && l.PlaceId==S.City.PlayerShop().Id ? 0 : 1)
                    .ThenBy(l => S.LeaseBlocker(l) == null ? 0 : 1)
                    .ThenBy(l => l.OpenDay).ThenBy(l => l.Rent.Cents).FirstOrDefault()?.PlaceId
                    ?? agency.Id;

            var (map, detail) = AgencyPropertyColumns(w.Content, "AgencyMap", "AgencyDetail");
            void RefreshProperty()
            {
                Ui.Clear(detail);
                Place selected = units.FirstOrDefault(p => p.Id == _agencySelectedId);
                if (selected != null) BuildAgencyDetail(detail, selected, System.Array.IndexOf(units, selected) + 1, w);
            }
            BuildAgencyMap(map, units, w, agency, RefreshProperty);
            RefreshProperty();
            refreshProperty = RefreshProperty;
        }

        private void BuildAgencyHeader(Window w, Place agency, System.Action refreshProperty)
        {
            var header = Ui.Row(w.Content, 62, 12);
            var agent = S.AgencyAgent(agency);
            Button talk = null;
            if (agent != null)
            {
                var portrait = Ui.Rect(header, "AgentPortrait");
                Ui.Size(portrait, 40, 54);
                var image = FitPortrait(portrait, agent.Id);
                talk = image.gameObject.AddComponent<Button>();
                talk.targetGraphic = image;
                talk.Tip(CanTalkToScenePerson(agent.Id) ? $"和{agent.Name}交谈" : $"查看{agent.Name}的资料");
            }
            var district=S.City.AgencyDistrictFor(agency.StreetId);
            string coverage=district==null?S.City.StreetName(agency.StreetId):string.Join("、",district.streets.Select(S.City.StreetName));
            var summary = Ui.Size(Ui.Label(header, "", 20), -1, 62, 1);
            RefreshSummary();
            if (talk != null)
                talk.onClick.AddListener(() => OpenPersonDialogue(agent.Id, agency, onClosed: () =>
                {
                    if (w.Closed || summary == null) return;
                    RefreshSummary();
                    refreshProperty();
                }));

            void RefreshSummary()
            {
                int rapport = S.AgencyRapport(agency);
                string detail = _agencyResidential ? "当前住处：" + S.HomeTitle
                    : agent == null ? "按标准中介费办理" : $"熟悉度 {rapport} · 中介费减免 {AgencyDiscountPercent(rapport)}";
                string coverageLine = agent == null ? $"负责：{coverage}" : $"负责人 {agent.Name} · 负责：{coverage}";
                summary.text = $"<b>{S.City.AgencyCoverageName(agency)} · 本片区房源</b>　现金 {S.Ledger.Balance}\n<size=16>{coverageLine}\n<color=#655E52>{detail}</color></size>";
            }

            if (_agencyMyProperties || _agencyResidential || S.DistrictAccessBlocker(agency) != null) return;
            if (agency.Id == GameSession.PachinkoTransferAgencyId && S.PachinkoTransferUnlocked)
            {
                var place = S.City.GetPlace(CityLifeCatalog.PachinkoVenue);
                if (!S.IsOwnedVenue(place))
                    Ui.Size(Ui.Button(header, "购买" + place.Name, () => OpenPachinkoTransfer(w), 17, Ui.ButtonHot), 150)
                        .name = "AgencyBuyPachinko";
            }
            if (!BusinessUnlocked) return;
            void OpenSection(int section)
            {
                var business = OpenBusinessSection(section);
                business.OnClosed += w.RequestRebuild;
            }
            var localListings = S.Listings.Where(l => S.City.HasPlace(l.PlaceId) && S.AgencyServes(agency, S.ListingPlace(l))).ToArray();
            bool CanOpen(VentureDef kind) => localListings.Any(l => S.VentureBlocker(l.PlaceId, kind.Id) == null);
            int openableKind = CanOpen(Ventures.All[_ventureKind]) ? _ventureKind : System.Array.FindIndex(Ventures.All, CanOpen);
            if (openableKind >= 0)
            {
                Ui.Size(Ui.Button(header, "委托店长开店", () => { _ventureKind = openableKind; OpenSection(3); }, 16), 155)
                    .name = "AgencyOpenVenture";
            }
            bool canUsePark = S.HasPark ? S.AgencyServes(agency, S.City.GetPlace(S.ParkPlaceId))
                : localListings.Any(l => S.ParkPurchaseBlocker(l) == null);
            if (canUsePark)
                Ui.Size(Ui.Button(header, S.HasPark ? "园区管理" : "园区筹建", () => OpenSection(6), 16), 135)
                    .name = "AgencyOpenPark";
        }

        private void OpenPachinkoTransfer(Window agencyWindow)
        {
            var place = S.City.GetPlace(CityLifeCatalog.PachinkoVenue);
            bool alreadyOwnsProperty = S.PropertyAt(place.Id) != null;
            string blocker = S.PachinkoPurchaseBlocker;
            OpenWindow("购买" + place.Name, 580, 300, w =>
            {
                Para(w.Content, $"{place.Name} · {S.City.StreetName(place.StreetId)}", 20);
                Para(w.Content, "随店交付三台机台。接手后可在店内设置机台返奖和营业时间。", 17);
                if (blocker != null) Para(w.Content, blocker, 17, Ui.Accent);
                var row = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(row, "返回", w.Close, 17), -1, 44, 1);
                string action = alreadyOwnsProperty ? "确认自营" : $"确认购买 · {GameSession.PachinkoTransferPrice}";
                Ui.Size(Ui.Button(row, action, () =>
                {
                    if (!S.PurchasePachinko()) return;
                    w.Close();
                    agencyWindow.Rebuild();
                }, 17, Ui.ButtonHot, blocker == null), -1, 44, 2);
            }, fitContent: true);
        }

        private static (RectTransform map, RectTransform detail) AgencyPropertyColumns(Transform parent, string mapName, string detailName)
        {
            var body = Ui.Row(parent, 740, 14);
            body.GetComponent<LayoutElement>().preferredHeight = -1;
            var layout = body.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            var map = Ui.Panel(body, mapName, Ui.PanelBg2).rectTransform;
            Ui.Size(map, 1120, 740);
            map.GetComponent<LayoutElement>().minWidth = 1120;
            var detailPanel = Ui.Panel(body, detailName, Ui.PanelBg2).rectTransform;
            Ui.Size(detailPanel, -1, -1, 1).GetComponent<LayoutElement>().minHeight = 740;
            Ui.VList(detailPanel, 8, 10);
            return (map, detailPanel);
        }

        private static void AgencyPropertyHeading(Transform parent, string title, string status)
        {
            var heading = Ui.Size(Ui.Label(parent, $"<b>{title}</b>", 23), -1, 32);
            heading.resizeTextForBestFit = true;
            heading.resizeTextMinSize = 17;
            heading.resizeTextMaxSize = 23;
            Ui.Size(Ui.Label(parent, status, 17, color: Ui.Accent), -1, 22);
        }

        private static RectTransform AgencyPropertyCard(Transform parent, string name)
        {
            var card = Ui.FlatPanel(parent, name, Ui.PanelBg).rectTransform;
            Ui.VList(card, 6, 12);
            return card;
        }

        private static RectTransform AgencyPropertySummary(Transform parent, RoomSpec room)
        {
            var row = Ui.Row(parent, 110, 12);
            BuildAgencyFloorPlan(row, room);
            var info = Ui.Size(Ui.Rect(row, "AgencyPropertyFacts"), 0, 110, 1);
            Ui.VList(info, 3, 0).childAlignment = TextAnchor.MiddleLeft;
            return info;
        }

        private bool AgencyPurchaseMode(Transform card, Place place, System.Action rebuild, bool hasRental, bool hasPurchase)
        {
            if (_agencyQuotePlaceId != place.Id)
            {
                _agencyQuotePlaceId = place.Id;
                _agencyPurchaseSelected = !hasRental;
            }
            if (!hasRental || !hasPurchase) return hasPurchase;

            var modes = Ui.Row(card, 40, 6);
            Ui.Size(Ui.Button(modes, "租赁", () => { _agencyPurchaseSelected = false; rebuild(); },
                17, Ui.SelectedButton(!(_agencyPurchaseSelected))), 0, 40, 1).name = "AgencyRentMode";
            Ui.Size(Ui.Button(modes, "全款购买", () => { _agencyPurchaseSelected = true; rebuild(); },
                17, Ui.SelectedButton(_agencyPurchaseSelected)), 0, 40, 1).name = "AgencyPurchaseMode";
            return _agencyPurchaseSelected;
        }

        private void AgencyPaymentSummary(Transform card, Money cost, string actionLabel, System.Action action,
            string name = null, string blocker = null)
        {
            Ui.Size(Ui.Panel(card, "AgencyPaymentDivider", Ui.Edge), -1, 1);
            var total = Ui.Row(card, 34);
            Ui.Size(Ui.Label(total, cost.IsNegative ? "<b>本次净收</b>" : "<b>本次实付</b>", 22, color: Ui.Accent), 0, 34, 1);
            Ui.Size(Ui.Label(total, $"<b>{cost.Abs()}</b>", 24, TextAnchor.MiddleRight, Ui.Accent), 210, 34);
            Money remaining = S.Ledger.Balance - cost;
            bool cashShort = cost.IsPositive && remaining.IsNegative;
            Line(card, cashShort ? $"现金还差 {remaining.Abs()}" : $"办理后余额 {remaining}",
                17, remaining.IsNegative ? Ui.Warn : Ui.Good);
            if (blocker != null && (!cashShort || !blocker.StartsWith("现金", System.StringComparison.Ordinal)))
                Para(card, blocker, 16, Ui.Warn);
            var button = Ui.Size(Ui.Button(card, actionLabel, action, 18, Ui.ButtonHot, blocker == null), -1, 46);
            if (name != null) button.name = name;
        }

        private ShopListing AgencyListing(Place p) => S.Listings.FirstOrDefault(l => l.PlaceId == p.Id);

        private string AgencyStatus(Place p, ShopListing listing)
        {
            if (p.Kind == PlaceKind.Agency) return p == S.CurrentPlace ? "房产中介 · 你在这里" : "房产中介";
            if (OwnsPropertyAt(p)) return "已购 · " + OwnedPropertyStatus(p);
            if (S.IsPendingShop(p)) return $"已租 · {S.ShopHandoverText(S.PendingLease.StartDay)}";
            if (LeasesPropertyAt(p)) return "已租 · 经营";
            if (!GameSession.IsCommercialPremises(p)) return "不开放租购";
            if (listing == null) return "未挂牌出租 · 暂不可租";
            return S.IsAdvance(listing) ? S.ShopHandoverText(listing.OpenDay) : "正在出租";
        }

        private Color? AgencyPropertyMapColor(Place p)
        {
            if (OwnsPropertyAt(p)) return PropertyOwnedColor;
            if (LeasesPropertyAt(p)) return PropertyRentedColor;
            var listing = AgencyListing(p);
            if (listing == null || S.IsMyShop(p) || S.IsPendingShop(p) || S.VentureAt(p.Id) != null) return null;
            return S.IsAdvance(listing) ? AgencyStreetMap.RentalUpcoming : AgencyStreetMap.RentalAvailable;
        }

        private void BuildAgencyDetail(RectTransform parent, Place p, int number, Window w)
        {
            ShopListing l = AgencyListing(p);
            bool mine = S.IsMyShop(p);
            var overview = AgencyPropertyCard(parent, "AgencyPropertyOverview");
            AgencyPropertyHeading(overview, $"{number:00}　{(mine ? S.PlaceTitle(p) : l != null ? UnitName(p) : p.Name)}", AgencyStatus(p, l));
            if (p.Kind == PlaceKind.Agency)
            {
                Para(overview, "中介办事处不开放租购。", 18, Ui.Dim);
                return;
            }
            string blocked = GameSession.CommercialPremisesBlocker(p);
            if (blocked != null && !OwnsPropertyAt(p) && !LeasesPropertyAt(p))
            {
                Para(overview, blocked + "。", 18, Ui.Dim);
                return;
            }
            bool dedicatedVenue = S.IsDedicatedVenue(p);
            var facts = AgencyPropertySummary(overview, RoomSpec.ForShop(p.Size));
            string sizeLabel = p.Size == ShopSize.Large ? "大型店铺" : p.Size == ShopSize.Medium ? "中型店铺" : "小型店铺";
            Line(facts, sizeLabel, 27, Ui.Accent).name = "AgencyShopSize";
            Line(facts, $"面积 {RealEstate.SizeArea(p.Size)}", 17, Ui.Dim);
            if (!dedicatedVenue)
                Para(facts, $"{GameSession.ShelfLimit(p.Size)} 个货架 · {GameSession.StaffLimit(p.Size)} 名员工", 16, Ui.Dim);
            else
                Para(facts, "专用场地 · " + S.VenueBusinessName(p), 16, Ui.Dim);
            var overviewActions = Ui.Row(overview, 38, 8);
            var entrants = S.ExpectedDailyShopEntrants(p, CompetitionPreviewKind(p));
            var dailyEntrants = Ui.Size(Ui.Label(overviewActions, $"预计每日入店 <b><color=#386047>{entrants:N0}</color></b> 人", 17), 0, 38, 1);
            dailyEntrants.name = "AgencyDailyEntrants";
            Ui.Size(Ui.Button(overviewActions, dedicatedVenue ? "场地详情 ›" : "商圈详情与试算 ›", () =>
            {
                if (dedicatedVenue)
                    OpenWindow("场地详情 · " + p.Name, 660, 470, details =>
                    {
                        BuildAgencyShopNotes(details.Content, p);
                        Para(details.Content, $"只能延续原有服务。接手后需自己值守或另行招聘。每日维护 ¥{S.VenueMaintenanceYuan(p)}，租金及工资另计。", 17);
                    });
                else OpenShopCompetition(p, agencyDetails: true);
            }, 15), 146, 38);
            BuildCommercialSettlement(AgencyPropertyCard(parent, "AgencySettlement"), p, w);
        }

        private void BuildCommercialSettlement(Transform settlement, Place p, Window w)
        {
            if (S.PropertyAt(p.Id) is OwnedProperty property)
            {
                BuildCommercialPropertyActions(settlement, p, property, w);
                return;
            }
            ShopListing l = AgencyListing(p);
            var own = S.ShopAt(p);
            bool pending = S.IsPendingShop(p);
            var purchase = S.PreviewCityPropertyPurchase(p.Id, false);
            bool managesLease = own != null && !own.OwnsProperty;
            bool hasRental = l != null || managesLease || pending;
            Line(settlement, purchase != null ? (hasRental ? "办理结算" : "全款购买结算") :
                l != null ? "租赁结算" : "房源管理", 21);
            if (AgencyPurchaseMode(settlement, p, () =>
            {
                Ui.Clear(settlement);
                BuildCommercialSettlement(settlement, p, w);
            }, hasRental, purchase != null))
            {
                if (purchase.HasTenant)
                    Para(settlement, "购买的是产权，原商户继续经营。", 16, Ui.Dim);
                AgencyFeeLine(settlement, "全款售价", purchase.Price.ToString());
                AgencyFeeLine(settlement, "交易费", purchase.Fee.ToString());
                if (purchase.DepositCredit.IsPositive) AgencyFeeLine(settlement, "抵扣押金", "− " + purchase.DepositCredit);
                if (purchase.PrepaidCredit.IsPositive) AgencyFeeLine(settlement, "抵扣本周未使用租金", "− " + purchase.PrepaidCredit);
                AgencyPaymentSummary(settlement, purchase.TotalDue, own != null ? "全款买下铺面" : "确认全款购买",
                    () => CityPropertyConfirm(p.Id, false, w), "AgencyReviewPurchase", S.CityPropertyPurchaseBlocker(p.Id, false));
                return;
            }
            if (l != null)
            {
                bool newShop = S.HasShop;
                bool advance = S.IsAdvance(l);
                AgencyQuote(settlement, l, newShop);
                if (advance)
                    Line(settlement, $"{S.ShopHandoverText(l.OpenDay)} · 签约起计租", 16, Ui.Warn).name = "AgencyEntryCountdown";
                AgencyPaymentSummary(settlement, S.LeaseCost(l, newShop), advance ? "确认提前租赁" : "确认租赁",
                    () => ConfirmAgencyLease(w, p.Id, newShop), "AgencyReviewLease", S.LeaseBlocker(l, newShop));
                return;
            }

            if (own != null)
            {
                if (managesLease)
                {
                    AgencyFeeLine(settlement, "签约周租", S.RentOf(own).ToString());
                    AgencyFeeLine(settlement, "已交押金（主动退租不退）", own.Lease.Deposit.ToString());
                }
                string why = S.CloseShopBlocker(own);
                if (why != null) Para(settlement, "办理前需处理：" + why, 16, Ui.Warn);
                Ui.Size(Ui.Button(settlement, own.OwnsProperty ? "结束自营" : "退租店铺",
                    () => ConfirmCloseShop(own), 18, Ui.ButtonDanger), -1, 44).name = "AgencyCloseShop_" + p.Id;
            }
            else if (pending)
            {
                AgencyFeeLine(settlement, "签约周租", S.PendingLease.Rent.ToString());
                AgencyFeeLine(settlement, "已交押金", S.PendingLease.Deposit.ToString());
                Line(settlement, $"{S.ShopHandoverText(S.PendingLease.StartDay)} · 等待期间照常计租", 16, Ui.Warn).name = "AgencyEntryCountdown";
            }
            else
                Para(settlement, S.VentureAt(p.Id) != null ? "这间铺面已纳入你的资产。" : "这间铺面目前没有租购报价。", 17, Ui.Dim);
        }

        private void BuildAgencyShopNotes(Transform parent, Place place)
        {
            var flow = S.FootfallOf(place);
            string station = double.IsPositiveInfinity(flow.StationMeters) ? "无可达地铁站" : $"距最近地铁 {flow.StationMeters:0} 米";
            string housing = double.IsPositiveInfinity(flow.HousingMeters) ? "附近无可达住宅" : $"距最近住宅 {flow.HousingMeters:0} 米";
            Line(parent, station + " · " + housing, 16, Ui.Dim);
            var listing = AgencyListing(place);
            if (listing != null)
                Line(parent, $"挂牌还剩 {listing.CloseDay - S.Clock.Today.TotalDays + 1} 天", 16, Ui.Dim);
            var agent = S.AgencyAgent(S.AgencyFor(place));
            string discount = agent == null ? ""
                : $"与{agent.Name}的熟悉度每增加 1 点，中介费减免 {RealEstate.AgentDiscountPerRapport * 100:0.#} 个百分点，最多减免 {RealEstate.MaxAgentDiscount * 100:0.#}%。\n";
            HelpLink(parent, "租赁与客流说明",
                $"新店按所选店型、{NewShopHours}正常营业估算。知名度、品牌口碑、街道人流、周末和同类分流会影响客流，实际人数也受开门和值守时长影响。餐馆还受座位周转影响。\n"
                + "新租报价随地段、面积和周行情变化。签约付首周租金、一周押金和半周中介费，之后周日扣租。\n"
                + discount
                + "已签租金保留签约价。提前签约到交房日才能使用。退租前请清理库存，主动退租不退押金。");
        }

        private void AgencyEntryWarning(Transform parent, int openDay)
        {
            var warning = Ui.Panel(parent, "AgencyEntryWarning", Ui.WarningSurface);
            Ui.Size(warning, -1, 82);
            var accent = Ui.Warn;
            Ui.Panel(warning.transform, "WarningStripe", accent).rectTransform.Anchor(0, 0, 0, 1, 0, 0, -5, 0);
            var heading = Ui.Label(warning.transform, $"<b>{S.ShopHandoverText(openDay)}</b>", 25, TextAnchor.MiddleLeft, accent);
            heading.name = "AgencyEntryCountdown";
            heading.rectTransform.Anchor(0, .48f, 1, 1, 14, 0, 8, 2);
            var detail = Ui.Label(warning.transform, "交房后可入驻，房租从签约当天开始计算。", 14, TextAnchor.UpperLeft, Ui.TextColor);
            detail.rectTransform.Anchor(0, 0, 1, .48f, 14, 5, 8, 0);
        }

        private void AgencyQuote(Transform parent, ShopListing l, bool newShop)
        {
            var place = S.ListingPlace(l);
            var agency = S.AgencyFor(place);
            int rapport = S.AgencyRapport(agency);
            string feeLabel = S.AgencyAgent(agency) == null ? "中介费" : $"中介费（熟悉度 {rapport} · 减免 {AgencyDiscountPercent(rapport)}）";
            AgencyFeeLine(parent, "首周租金", l.Rent.ToString());
            AgencyFeeLine(parent, "押金（主动退租不退）", RealEstate.Deposit(l.Rent).ToString());
            AgencyFeeLine(parent, feeLabel, S.AgentFeeFor(l.Rent, place).ToString());
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
            bool advance = S.IsAdvance(listing);
            bool dedicatedVenue = S.IsDedicatedVenue(p);
            string previewKind = CompetitionPreviewKind(p);
            string details = $"{GameSession.SizeName(p.Size)} · {RealEstate.SizeArea(p.Size)}\n"
                + $"{RetailShopTypes.Name(previewKind)} · 预计每日入店约 {S.ExpectedDailyShopEntrants(p, previewKind):N0} 人";
            details += advance ? "\n" + S.ShopHandoverText(listing.OpenDay) : "\n签约后交付";
            string terms = $"首周租金签约时支付，之后<b>周日扣租 {listing.Rent}</b>。主动退租，<b><color=#{ColorUtility.ToHtmlStringRGB(Ui.Warn)}>{RealEstate.Deposit(listing.Rent)} 押金不退</color></b>。";
            if (advance) terms += "\n交房后可入驻，房租从签约日开始计算。";
            if (dedicatedVenue)
                terms += $"\n专用场地「{S.VenueBusinessName(p)}」须保留原用途与设施，另行安排值守，原教练不自动受雇。"
                    + $"\n维护费 ¥{S.VenueMaintenanceYuan(p)}/天，工资按排班另计。老板自用免费且无营业收入。"
                    + "\n接手后在场馆管理设置价格、营业时间并开启接客。";
            bool signed = false;
            OpenPropertyContract(advance ? "商铺提前租赁合同" : "商铺租赁合同", UnitName(p), details,
                quotedCost, fees => AgencyQuote(fees, listing, newShop), terms, false, () =>
                {
                    ShopListing live = S.Listings.FirstOrDefault(l => l.PlaceId == placeId);
                    if (!S.InAgency || live == null || S.LeaseBlocker(live, newShop) != null || S.LeaseCost(live, newShop) != quotedCost)
                    {
                        Toast("房源或费用有变化，请重新核对。");
                        return false;
                    }
                    signed = S.SignLeaseThenChooseKind(placeId, newShop);
                    if (!signed) return false;
                    if (S.PendingLease != null) Toast($"已提前签约，{S.ShopHandoverText(S.PendingLease.StartDay)}，期间房租照常计算。");
                    else if (dedicatedVenue) Toast("接手完成，请到场馆设置营业与值守。");
                    else if (newShop) Toast("签约完成，可以自行到新店装修了。");
                    return true;
                }, () =>
                {
                    if (signed && S.PendingShopKindPlaceId != null) parent.Close();
                    else parent.Rebuild();
                }, "AgencyConfirmLease", height: dedicatedVenue ? 710 : 610);
        }

        private static string UnitName(Place p) => p.Business == RealEstate.VacantBusiness ? p.Name : p.Name + "原址";

        private void RefreshOpeningKindChoice()
        {
            string placeId=S.PendingShopKindPlaceId;
            if(placeId==null||AnyModalOpen||PopupOpen||S.IsGameOver)return;
            string selected=RetailShopTypes.Default;
            bool animateSelection = false;
            OpenWindow("准备开什么店？",720,720,w=>
            {
                Line(w.Content,S.City.StreetName(S.City.GetPlace(placeId).StreetId)+" · 已签约",21,Ui.Accent);
                Para(w.Content,"确认店型后即可准备开业。租金已经支付。",17);
                const int columns = 3;
                RectTransform row = null;
                for (int i = 0; i < RetailShopTypes.All.Length; i++)
                {
                    if (i % columns == 0) row = Ui.Row(w.Content, 104, 8);
                    string kind = RetailShopTypes.All[i];
                    string blocker = S.RetailTypeBlocker(kind);
                    bool enabled = blocker == null;
                    bool active = enabled && selected == kind;
                    string label = (active ? "✓ " : "") + RetailShopTypes.Name(kind);
                    if (!enabled) label += " · " + blocker;
                    var button = Ui.Size(Ui.Button(row, label, () =>
                    {
                        if (selected == kind) return;
                        selected = kind;
                        animateSelection = true;
                        w.Rebuild();
                    }, 18, Ui.SelectedButton(active), enabled), 0, 104, 1);
                    button.name = "OpeningKind_" + kind;
                    button.GetComponentInChildren<Text>().rectTransform.Anchor(0, 0, 1, .36f, 6, 5, 6);

                    var portrait = Ui.Rect(button.transform, "OpeningKindPortrait");
                    portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, 1);
                    portrait.sizeDelta = new Vector2(55, 55);
                    portrait.anchoredPosition = new Vector2(0, -34);
                    FillItemPortrait(portrait, OpeningKindPortrait(kind)).Fill();
                    if (active && animateSelection)
                    {
                        var reveal = NewUiReveal();
                        reveal?.Emphasize(portrait, 0);
                        PlayUiReveal(reveal);
                    }
                }
                animateSelection = false;
                string carShopHint = S.DistrictAccessTier < 1 ? "解锁社区后可到中介签分店合同" : "请在中介的「委托店长开店」办理";
                var carShop = Ui.Size(Ui.Button(w.Content, "二手车行 · " + carShopHint, null, 17, interactable: false), -1, 42);
                carShop.name = "OpeningKind_cars";
                ItemButtonPortrait(carShop, "vehicle:c_city", 34);
                var place = S.City.GetPlace(placeId);
                Line(w.Content,$"新店 {NewShopHours} · 预计每日入店约 {S.ExpectedDailyShopEntrants(place,selected,asNewShop:true):N0} 人",17,Ui.Good);
                Para(w.Content,RetailShopTypes.Description(selected),16,Ui.Dim);
                Para(w.Content,"家具城开店清单："+string.Join("、",GameSession.OpeningFurniture(selected).Select(id=>FurnitureCatalog.Get(id).Name)),16,Ui.Good);
                Ui.Size(Ui.Button(w.Content,"确认开一家"+RetailShopTypes.Name(selected),()=>
                {
                    if(S.ConfirmOpeningKind(placeId,selected)){w.Close();RebuildDock();UpdateQuestTracker();}
                },19,Ui.ButtonHot,S.RetailTypeBlocker(selected) == null),-1,46).name="ConfirmOpeningKind";
            },closable:false);
        }

        private static string OpeningKindPortrait(string kind) => kind switch
        {
            "computer" => "computer",
            "models" => "model:rainbot:courier",
            "clothing" => "jacket",
            "secondhand" => "oldmarket:watch",
            "restaurant" => "kitchen:meal",
            _ => "pet:mid:bowl"
        };

    }
}
