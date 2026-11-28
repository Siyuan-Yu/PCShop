using System;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Image _questPanel;
        private Text _questHeading, _questTitle, _questProgress, _questText, _questActionText;
        private RectTransform _questProgressBar, _questProgressFill, _questDetails;
        private Button _questActionButton;
        private Action _questAction;
        private Place _trackedQuestTravelDestination;
        private ShortcutMenu _trackedQuestShortcut;
        private string _questSeen;
        private bool _questInitialized;
        private Window _questWindow;
        private Window _questPopup;
        private bool _questHidden;
        private QuestDef _pendingNext;
        private const string OpeningMallTravelLabel = "前往家具城";

        /// <summary>任务完成 / 换任务时强制弹窗（冒烟测试脚本会关掉，免得挡住脚本流程）。</summary>
        public bool QuestPopups = true;

        public void ResetGuideState()
        {
            _purchaseDrafts.Clear();
            _openingCounterPendingAction = null;
            _openingCounterRejectedAction = null;
            _scheduledPropertyPopups.Clear();
            _questSeen = null;
            _questInitialized = false;
            _questWindow = null;
            _questPopup = null;
            _questHidden = false;
            _pendingNext = null;
            _businessTab = 0;
            _competitionPreviewKind = RetailShopTypes.Default;
        }

        private void BuildQuestTracker(Transform root)
        {
            _questPanel = Ui.PaperPanel(root, "QuestTracker", Ui.NoteStock, lifted: true);
            _questPanel.rectTransform.Anchor(0, 1, 0, 1, 16, -332, -420, 82);
            Ui.Trim(_questPanel.rectTransform, "TaskPaperEdge", Ui.FlatWellEdge, 1);
            var rail = Ui.Panel(_questPanel.transform, "TaskRail", Ui.FlatHotEdge);
            rail.rectTransform.Anchor(0, 0, 0, 1, 1, 8, -4, 8);
            rail.raycastTarget = false;

            _questHeading = Ui.Label(_questPanel.transform, "", 17, color: Ui.Accent);
            _questHeading.fontStyle = FontStyle.Bold;
            _questHeading.rectTransform.Anchor(0, 1, 1, 1, 16, -37, 46, 10);
            _questTitle = Ui.Label(_questPanel.transform, "", 22);
            _questTitle.fontStyle = FontStyle.Bold;
            _questTitle.resizeTextForBestFit = true;
            _questTitle.resizeTextMinSize = 16;
            _questTitle.resizeTextMaxSize = 22;
            _questTitle.rectTransform.Anchor(0, 1, 1, 1, 16, -70, 16, 37);
            _questProgress = Ui.Label(_questPanel.transform, "", 16, color: Ui.Good);
            _questProgress.rectTransform.Anchor(0, 1, 1, 1, 16, -96, 16, 72);
            _questProgressBar = Ui.Panel(_questPanel.transform, "QuestProgressTrack", Ui.PanelBg2).rectTransform;
            _questProgressBar.Anchor(0, 1, 1, 1, 16, -109, 16, 102);
            _questProgressFill = Ui.Panel(_questProgressBar, "QuestProgressFill", Ui.Good).rectTransform;
            _questText = Ui.Label(_questPanel.transform, "", 17, TextAnchor.UpperLeft);
            _questText.rectTransform.Anchor(0, 0, 1, 1, 16, 58, 16, 76);
            _questText.verticalOverflow = VerticalWrapMode.Truncate;

            _questActionButton = Ui.Button(_questPanel.transform, "", ActivateQuestAction, 18, Ui.ButtonHot,
                exclusiveEmphasis: false);
            _questActionButton.name = "QuestPrimaryAction";
            _questActionText = _questActionButton.GetComponentInChildren<Text>();
            ((RectTransform)_questActionButton.transform).Anchor(0, 0, .74f, 0, 14, 12, 4, -49);
            var help = Ui.Button(_questPanel.transform, "详情", OpenTrackedQuestDetails, 16);
            _questDetails = ((RectTransform)help.transform).Anchor(.74f, 0, 1, 0, 4, 12, 14, -49);
            var close = Ui.Button(_questPanel.transform, "×", () =>
            {
                _questHidden = true;
                UpdateQuestTracker();
            }, 20);
            ((RectTransform)close.transform).Anchor(1, 1, 1, 1, -38, -36, 6, 6);
            close.Tip("收起任务条（顶栏“任务”按钮可重新打开）");
            _questPanel.gameObject.SetActive(false);
        }

        private void ActivateQuestAction()
        {
            if (S.OpeningAgencyGuidePending)
            {
                if (!AnyModalOpen && S.StartOpeningAgencyTravel()) UpdateFirstUseGuide();
                return;
            }
            _questAction?.Invoke();
        }

        /// <summary>顶栏“任务”按钮：打开当前任务说明，并重新显示左上角的任务条。</summary>
        public void OpenQuests()
        {
            bool sponsorGuide = S.VenueSponsorGuideQuest() != null;
            if (!sponsorGuide && S.FirstChapterTrialCompleted)
            {
                _questHidden = false;
                UpdateQuestTracker();
                OpenDistrictStory();
                return;
            }
            if (!sponsorGuide && S.IsOwnedVenue(S.CurrentPlace)) { OpenVenueManagement(S.CurrentPlace); return; }
            _questHidden = false;
            UpdateQuestTracker();
            if (_questWindow != null && !_questWindow.Closed) { _questWindow.Rebuild(); return; }
            _questWindow = OpenWindow("当前任务", 960, 620, BuildGuideWindow, onClosed: () => _questWindow = null, fitContent: true);
        }

        /// <summary>冒烟截图用：弹出“完成了租铺任务、接下来去家具城”的任务完成窗口。</summary>
        public void DebugQuestPopup() => OpenQuestPopup(Quests.Get("rent"), Quests.Get("mall"));

        public string QuestTrackerText => _questPanel != null && _questPanel.gameObject.activeSelf
            ? string.Join("\n", _questHeading.text, _questTitle.text, _questProgress.text, _questText.text) : null;
        public void DebugRefreshQuests() => UpdateQuestTracker();

        private bool QuestAnnouncementOpen => _questPopup?.Closed == false;
        private bool QuestAnnouncementBlocksGuide => QuestAnnouncementOpen || (QuestPopups && S.PendingQuestCompletion() != null);

        private void UpdateQuestTracker()
        {
            if (_questPanel == null) return;
            _trackedQuestTravelDestination = null;
            _trackedQuestShortcut = null;
            if (OpeningMealPreludePending) { _questPanel.gameObject.SetActive(false); return; }
            QuestDef q = S.CurrentQuest();
            QuestDef done = S.PendingQuestCompletion();
            if (!QuestPopups)
            {
                while (done != null)
                {
                    S.ConfirmQuestCompletion(done.Id);
                    done = S.PendingQuestCompletion();
                }
            }
            if (_questInitialized && _questSeen != q?.Id && !S.IsGameOver)
            {
                _pendingNext = null;
                if (done == null && q != null)
                {
                    if (QuestPopups && !q.QuietTransition) _pendingNext = q;
                    else Toast("新任务：" + q.Title);
                }
            }
            _questInitialized = true;
            _questSeen = q?.Id;

            bool canAnnounce = !MainMenuOpen && !OpeningMealRestricted && !S.IsGameOver && !S.IsWorking
                && !S.Racing && !S.CityActivityExclusive && !FishingInputActive && !OnsiteInputActive
                && !_ctrl.Placing && !_ctrl.PetFocusActive && !PopupOpen && !QuestAnnouncementOpen
                && !PropertyContractOpen && _propertyContractPopups.Count == 0
                && S.PendingShopKindPlaceId == null && S.PendingEvent == null;
            if (done != null)
            {
                _pendingNext = null;
                _questPanel.gameObject.SetActive(false);
                if (canAnnounce)
                {
                    SoundFx.Play(Sfx.Success);
                    OpenQuestPopup(Quests.ForShop(done, S), null, () => S.ConfirmQuestCompletion(done.Id));
                }
                return;
            }
            if (_pendingNext != null && canAnnounce && !AnyModalOpen && !S.RequiredTutorialPaused && !S.OpeningAgencyGuidePending)
            {
                OpenQuestPopup(null, _pendingNext);
                _pendingNext = null;
            }

            if (S.VenueSponsorGuideQuest() is QuestDef sponsorGuide)
            {
                bool show = !_questHidden && !S.IsGameOver && !_ctrl.Placing;
                _questPanel.gameObject.SetActive(show);
                if (show)
                {
                    var (label, action, travel) = QuestAction(sponsorGuide.Target, sponsorGuide.GuideDestination);
                    SetQuestCard("经营引导", sponsorGuide.Title, sponsorGuide.Short,
                        GuideLocation(sponsorGuide.Target, sponsorGuide.GuideDestination), ProgressOf(sponsorGuide), label, action,
                        travelDestination: travel);
                }
                return;
            }

            bool showMallTravel = q?.Id == "mall" && S.OpeningMallTravelPending;
            bool showRenovationTravel = S.OpeningRenovationStep == OpeningRenovationLesson.Travel;
            if (!showMallTravel && !showRenovationTravel && !S.FirstChapterTrialCompleted && S.IsOwnedVenue(S.CurrentPlace))
            {
                _questPanel.gameObject.SetActive(!_questHidden && !S.IsGameOver && !_ctrl.Placing);
                SetQuestCard("场馆经营", S.VenueBusinessName(S.CurrentPlace), "设置价格和营业时间，安排值守后开启接客。",
                    "顾客到场后付费，老板自用免费。", null, "场馆管理", () => OpenVenueManagement(S.CurrentPlace));
                return;
            }
            var district = S.CurrentDistrictNode;
            string invitation = S.DistrictInvitationStatus;
            bool visible = (q != null || district != null || invitation != null || S.FirstChapterTrialCompleted)
                && (!_questHidden || showMallTravel || showRenovationTravel) && !S.IsGameOver && !_ctrl.Placing;
            _questPanel.gameObject.SetActive(visible);
            if (!visible) return;
            if (S.FirstChapterTrialCompleted)
                SetQuestCard("试玩完成", "城郊经营目标已达成", "可以继续在城郊经营。", progress: "5 家店 · 100 万元自有现金 · 无贷款");
            else if (showMallTravel)
                SetQuestCard("当前任务", q.Title, "去宜居家具城买齐开店设施。", GuideLocation(q.Target, q.GuideDestination),
                    actionLabel: OpeningMallTravelLabel, action: S.StartOpeningMallTravel, travelDestination: S.OpeningFurnitureMall);
            else if (showRenovationTravel)
                SetQuestCard("当前任务", q.Title, "家具已买齐，先前往自己的店铺。", "到店后打开「装修」。",
                    actionLabel: "前往店铺", action: S.StartOpeningRenovationTravel, travelDestination: S.OpeningRenovationTravelDestination);
            else if (district != null) UpdateDistrictQuestCard(district);
            else if (invitation != null) SetQuestCard("主线 · 等待联系", "等待街区引荐", invitation,
                "引荐条件已达成");
            else
            {
                var (label, action, travel) = QuestAction(q.Target, q.GuideDestination);
                string heading = q.Target == QuestTarget.CommunityGoal ? "主线目标" : "当前任务";
                string next = q.Target == QuestTarget.RacingEnrollment ? S.RacingQualificationBlocker ?? q.Short : q.Short;
                if (q.Target == QuestTarget.BankSecurities && S.BankTutorialPaused)
                    next = S.BankBorrowTutorialPending ? "在柜台了解借款" : "在柜台打开「证券交易」";
                SetQuestCard(heading, q.Title, next, GuideLocation(q.Target, q.GuideDestination), ProgressOf(q)?.Split('\n')[0], label, action,
                    travelDestination: travel);
            }
        }

        private void SetQuestCard(string heading, string title, string next, string status = null, string progress = null,
            string actionLabel = null, Action action = null, float? completion = null, Place travelDestination = null)
        {
            _trackedQuestTravelDestination = travelDestination;
            _trackedQuestShortcut = FindQuestShortcut(travelDestination);
            if (_trackedQuestShortcut != null || QuestUsesOpeningTravelShortcut(travelDestination)) action = null;
            bool showProgress = !string.IsNullOrEmpty(progress);
            _questHeading.text = heading;
            _questTitle.text = title;
            _questProgress.text = progress ?? "";
            _questProgressBar.gameObject.SetActive(completion.HasValue);
            if (completion.HasValue) _questProgressFill.Anchor(0, 0, Mathf.Clamp01(completion.Value), 1);
            float textTop = completion.HasValue ? 116 : showProgress ? 99 : 77;
            _questText.rectTransform.offsetMax = new Vector2(-16, -textTop);
            _questText.text = QuestPaperInk(next)
                + (string.IsNullOrEmpty(status) ? "" : $"\n<size=15><color=#{ColorUtility.ToHtmlStringRGB(Ui.Dim)}>{status}</color></size>");
            _questPanel.rectTransform.offsetMin = new Vector2(16, -(82 + HudFunctionOffset + textTop + 118));
            _questAction = action;
            _questActionText.text = actionLabel ?? "";
            bool hasAction = action != null;
            if (_questActionButton.gameObject.activeSelf != hasAction)
            {
                _questActionButton.gameObject.SetActive(hasAction);
                _questDetails.Anchor(hasAction ? .74f : 0, 0, 1, 0, hasAction ? 4 : 14, 12, 14, -49);
            }
            RefreshToastPlacement();
        }

        private bool QuestUsesOpeningTravelShortcut(Place destination) => destination != null
            && OpeningTravelGuide().destination == destination && FindQuestShortcut(destination, allowTemporaryDestination: true) != null;

        private bool QuestUsesShortcut(Place destination) => FindQuestShortcut(destination) != null || QuestUsesOpeningTravelShortcut(destination);

        private static string QuestPaperInk(string text) => Ui.EmphasizeReading(text?.Replace("<color=#FFC966>", "<color=#80551F>"));

        private void OpenTrackedQuestDetails()
        {
            if (S.FirstChapterTrialCompleted || !S.IsOwnedVenue(S.CurrentPlace) && (S.CurrentDistrictNode != null || S.DistrictInvitationStatus != null))
                OpenDistrictStory();
            else OpenQuestWindow();
        }

        private void UpdateDistrictQuestCard(DistrictNarrativeNode node)
        {
            var state = S.DistrictStoryState;
            string heading = "主线 · " + GameSession.DistrictName(state.Chapter) + "引荐";
            if (node.Id == "community.intro")
            {
                SetQuestCard(heading, node.Title, "可在手机通讯录回复李秀兰，或当面和她聊聊。",
                    "李秀兰的来电等待回复", actionLabel: "查看来电", action: OpenDistrictStory);
                return;
            }
            if (S.CommunitySearchActive)
            {
                var leads = Enumerable.Range(0, 3).Where(i => !S.CommunityHasPiece(i)).ToArray();
                int found = 3 - leads.Length;
                if (leads.Length == 0)
                {
                    SetQuestCard(heading, node.Title, "去城郊中介旁等唐蔚来收图页。", progress: "已找到图页 3/3",
                        actionLabel: "前往城郊中介", action: () => NavigateToDistrictMeeting("agency"), completion: 1);
                    return;
                }
                int lead = leads.FirstOrDefault(S.CommunityBoxAvailable);
                string progress = "已找到图页 " + found + "/3";
                if (S.CommunityBoxAvailable(lead))
                {
                    SetQuestCard(heading, node.Title, "去藏箱位置调查" + GameSession.CommunityPieceName(lead) + "。",
                        progress: progress, actionLabel: "前往藏箱地点", action: () => S.GoToCommunityBox(lead), completion: found / 3f);
                }
                else
                {
                    lead = leads[0];
                    string person = GameSession.CommunityLeadPerson(lead);
                    if (S.CommunityLeadAppointment(lead) is CityActivity outing)
                        SetQuestCard(heading, node.Title, S.CommunityLeadStatus(lead),
                            CityTime(outing.ScheduledStart), progress,
                            S.CurrentPlace?.Id == outing.VenueId ? "打开活动现场" : "前往赴约",
                            () => GoToScheduledCityActivity(outing.Id), found / 3f);
                    else
                        SetQuestPersonHint(heading, node.Title, person, S.CommunityLeadStatus(lead), progress, found / 3f,
                            allowPhone: true);
                }
                return;
            }
            if (S.DistrictCommerceGameplay)
            {
                SetQuestCard(heading, node.Title, S.DistrictCommerceBlocker ?? node.Objective,
                    progress: S.DistrictCommerceStatus.Split('\n')[0],
                    actionLabel: state.Chapter == 2 ? "打开品牌展会" : "打开企业竞标", action: OpenDistrictBrandGameplay);
                return;
            }
            string contact = S.DistrictMeetingContact ?? DistrictNavigationContact(node.People);
            int foundPages = node.Id == "community.trial" ? Enumerable.Range(0, 3).Count(S.CommunityHasPiece) : -1;
            string mapProgress = foundPages >= 0 ? "已找到图页 " + foundPages + "/3" : null;
            float? mapCompletion = foundPages >= 0 ? (float?)(foundPages / 3f) : null;
            if (state.MeetingNodeId == node.Id && S.City.HasPlace(state.MeetingPlaceId))
            {
                string place = state.MeetingPlaceId;
                string where = S.City.GetPlace(place).Name + (S.DistrictMeetingOutdoors ? "旁的街边会面点" : "门前");
                string time = where + " · " + S.DistrictMeetingTime;
                if (!S.AtCurrentDistrictMeeting)
                    SetQuestCard(heading, node.Title, "按约定去会面点。", time, mapProgress, "前往会面点",
                        () => NavigateToDistrictMeeting(place), mapCompletion);
                else if (S.Clock.Now.TotalMinutes < state.MeetingAt)
                    SetQuestCard(heading, node.Title, "已到达，等到约定时间再交谈。", time, mapProgress, completion: mapCompletion);
                else if (S.DistrictMeetingBlocker is string blocker)
                {
                    string next = state.MeetingPeople.Any(id => S.CityPersonState(id)?.Route != null) ? "会面人员还在路上，请稍候。" : blocker;
                    SetQuestCard(heading, node.Title, next, time, mapProgress, completion: mapCompletion);
                }
                else
                    SetQuestPersonHint(heading, node.Title, contact,
                        mapProgress != null ? "把图页交给唐蔚。" : "与联系人交谈，确认这一步。", mapProgress, mapCompletion, where + " · 已到达");
                return;
            }
            if (node.Id == "community.trial" && S.City.HasPlace(node.PlaceId))
            {
                SetQuestCard(heading, node.Title, "去城郊中介旁等唐蔚来收图页。", progress: mapProgress,
                    actionLabel: "前往城郊中介", action: () => NavigateToDistrictMeeting(node.PlaceId), completion: mapCompletion);
                return;
            }
            SetQuestPersonHint(heading, node.Title, contact, node.Objective, mapProgress, mapCompletion, allowPhone: true);
        }

        private void SetQuestPersonHint(string heading, string title, string id, string next, string progress,
            float? completion, string status = null, bool allowPhone = false)
        {
            var person = S.CityPerson(id);
            if (person == null)
            {
                SetQuestCard(heading, title, next, status, progress, completion: completion);
                return;
            }
            bool nearby = S.CitySamePlace(id);
            var state = S.CityPersonState(id);
            var place = S.DistrictPersonCurrentPlace(state);
            bool locked = S.DistrictAccessBlocker(place) != null;
            if (status == null && place != null)
                status = place.Name + (nearby ? " · 已到达" : locked ? " · " + GameSession.DistrictName(S.DistrictTierOf(place)) + "片区尚未开放" : "");
            else if (status == null && state?.Route != null)
                status = person.Name + "正在途中";
            next += "\n" + (nearby ? $"走到{person.Name}身边按 F 交谈。"
                : allowPhone ? DistrictContactHint(id, person.Name) : $"请当面与{person.Name}交谈。");
            bool showNavigation = !nearby && state != null && !locked;
            SetQuestCard(heading, title, next, status, progress, showNavigation ? "寻找" + person.Name : null,
                showNavigation ? () => NavigateToDistrictPerson(id) : null, completion);
        }

        private Place ScooterDealer => S.City.Places.FirstOrDefault(p => MotorCatalog.SoldBy(p.Id, VehicleKind.EBike));

        private (string label, Action action, Place travel) QuestAction(QuestTarget target, Place destination = null)
        {
            switch (target)
            {
                case QuestTarget.Cooker:
                    if (_ctrl.InHome) return ("打开电饭锅", OpenCookerWindow, null);
                    return QuestPlaceAction("回家做饭", S.Home);
                case QuestTarget.Bed:
                    var home = S.CanSleepAt(S.CurrentPlace) ? S.CurrentPlace : SelectedShortcut(SleepShortcut);
                    if (home == null || S.CityIntent?.TargetPlaceId == home.Id) return default;
                    return home == S.CurrentPlace ? ("走到床边", () => _ctrl.GoHomeToSleep(home), null)
                        : ("回家睡觉", () => _ctrl.GoHomeToSleep(home), home);
                case QuestTarget.Agency:
                case QuestTarget.Ventures: return QuestPlaceAction("前往中介", destination ?? (S.City.HasPlace("agency") ? S.City.GetPlace("agency") : null));
                case QuestTarget.Mall: return QuestPlaceAction(OpeningMallTravelLabel, S.OpeningFurnitureMall);
                case QuestTarget.PhoneShop: return QuestPlaceAction("前往手机店", S.City.Places.FirstOrDefault(p => p.Business == GameSession.PhoneBusiness));
                case QuestTarget.BankSecurities:
                    if (S.AtBank) return ("打开银行柜台", () => OpenBank(S.CurrentPlace), null);
                    if (S.BankPlace == null || S.CityIntent?.TargetPlaceId == S.BankPlace.Id) return default;
                    return QuestPlaceAction("前往银行", S.BankPlace);
                case QuestTarget.VenueSponsorship:
                    if (destination == null || S.CityIntent?.TargetPlaceId == destination.Id) return default;
                    return QuestPlaceAction("前往" + destination.Name, destination);
                case QuestTarget.ScooterDealer: return QuestPlaceAction("前往顺丰车行", ScooterDealer);
                case QuestTarget.RacingEnrollment:
                    if (S.RacingUnlocked || S.RacingQualificationBlocker != null) return default;
                    if (S.CurrentPlace?.Id == GameSession.StarterRaceGarageId)
                        return ("找负责人办理登记", OpenMotorGarage, null);
                    return ("前往城郊赛场车库", () => S.BeginCityTravel(GameSession.StarterRaceGarageId, enter: false), null);
                case QuestTarget.Market:
                    if (destination != null) return QuestPlaceAction("前往进货", destination);
                    if (S.ShopSupplyMarketId(S.RetailKind) is string marketId)
                        return QuestPlaceAction("前往批发市场", S.PurchaseDestination(S.City.GetPlace(marketId)));
                    return S.RetailKind == "secondhand" ? QuestShopAction(ShopTab.Counter, "打开柜台")
                        : ("打开采购", OpenCityProcurement, null);
                case QuestTarget.Renovate: return QuestShopAction(ShopTab.Renovate, "打开装修", destination);
                case QuestTarget.Stock: return QuestShopAction(ShopTab.Stock, "打开库存", destination);
                case QuestTarget.Counter: return QuestShopAction(ShopTab.Counter, "打开柜台", destination);
                case QuestTarget.Staff: return QuestShopAction(ShopTab.Staff, "打开员工", destination);
                case QuestTarget.Pricing: return QuestShopAction(ShopTab.Pricing, "打开定价");
                case QuestTarget.Orders:
                case QuestTarget.Enterprise: return QuestShopAction(ShopTab.Orders, "打开订单");
                case QuestTarget.Finance: return ("查看经营收支", OpenCityFinanceOverview, null);
                case QuestTarget.Property: return ("查看商铺和住宅", OpenCityProperties, null);
                case QuestTarget.Restock: return ("打开网购", OpenOnlinePanel, null);
                case QuestTarget.Direct: return ("打开采购", OpenCityProcurement, null);
                case QuestTarget.Creators: return ("打开品牌", OpenBrandBusiness, null);
                default: return default;
            }
        }

        private (string label, Action action, Place travel) QuestPlaceAction(string label, Place place)
        {
            if (place == null || S.CityIntent?.TargetPlaceId == place.Id) return default;
            return S.CurrentPlace == place ? ("打开" + place.Name, () => OnEnteredPlace(place), null)
                : (label, () => S.BeginCityTravel(place.Id), place);
        }

        private (string label, Action action, Place travel) QuestShopAction(ShopTab tab, string label, Place destination = null)
        {
            if (destination != null && S.CurrentPlace != destination)
                return QuestPlaceAction(tab == ShopTab.Staff ? "前往要招人的店" : tab == ShopTab.Counter ? "前往店铺" : "前往新店", destination);
            if (!S.HasShop) return default;
            if (S.InPlayerShop) return (label, () => { OpenDock(); SelectTab(tab); }, null);
            return QuestPlaceAction("前往自己的店", S.PlayerShopPlace);
        }

        private string ProgressOf(QuestDef q)
        {
            string text = q.Progress?.Invoke(S);
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private void ProgressLine(Transform parent, QuestDef q, int size)
        {
            string text = ProgressOf(q);
            if (text != null) Para(parent, text, size, Ui.Dim);
        }

        public void OpenQuestWindow()
        {
            if(S.IsOwnedVenue(S.CurrentPlace)){OpenVenueManagement(S.CurrentPlace);return;}
            UpdateQuestTracker();
            if (S.CurrentQuest() == null && S.CurrentDistrictNode == null && S.DistrictInvitationStatus == null && !S.FirstChapterTrialCompleted) return;
            if (_questWindow != null && !_questWindow.Closed) { _questWindow.Rebuild(); return; }
            _questWindow = OpenWindow("当前任务", 960, 620, BuildGuideWindow, onClosed: () => _questWindow = null, fitContent: true);
        }

        /// <summary>展示任务完成或新任务。确认完成后再刷新任务卡和教学。</summary>
        private void OpenQuestPopup(QuestDef done, QuestDef next, Action onContinue = null)
        {
            if (_questPopup != null && !_questPopup.Closed) _questPopup.Close();
            bool animateOnOpen = true;
            _questPopup = OpenWindow(done != null ? "任务完成" : "新任务", 620, 420, w =>
            {
                var reveal = animateOnOpen ? NewUiReveal() : null;
                animateOnOpen = false;
                if (done != null)
                {
                    var title = Line(w.Content, "✓ " + done.Title, 23, Ui.Good);
                    reveal?.Fade(title.transform, .03f);
                    reveal?.Emphasize(title.transform, .03f);
                    string rewardText = done.RewardText(S);
                    if (!string.IsNullOrEmpty(rewardText))
                    {
                        var reward = Para(w.Content, "获得：" + rewardText, 18, Ui.Accent);
                        reveal?.Fade(reward.transform, .09f);
                    }
                }
                if (next != null)
                {
                    Line(w.Content, "接下来：" + next.Title, 21, Ui.Accent);
                    Para(w.Content, GuideLocation(next.Target, next.GuideDestination), 16, Ui.Dim);
                    Para(w.Content, QuestPaperInk(next.Short), 18);
                    ProgressLine(w.Content, next, 16);
                }
                Ui.Size(Ui.Button(w.Content, "继续游戏", () =>
                {
                    onContinue?.Invoke();
                    w.Close();
                    if (onContinue != null) UpdateQuestTracker();
                }, 18, onContinue != null ? Ui.ButtonHot : Ui.ButtonBg), -1, 42);
                PlayUiReveal(reveal);
            }, closable: onContinue == null, onClosed: () => _questPopup = null, fitContent: true);
        }

        private void BuildGuideWindow(Window w)
        {
            BuildDistrictMessage(w.Content);
            var q = S.VenueSponsorGuideQuest() ?? S.CurrentQuest();
            if (q == null)
            {
                int n = Quests.All.Count(x => S.QuestCompleted(x.Id));
                if (!S.FirstChapterTrialCompleted)
                    Para(w.Content, $"暂时没有新任务（已完成 {n} 个）。继续经营，达到下一阶段的条件时会弹出新任务。", 20);
                Ui.Size(Ui.Button(w.Content, "继续", w.Close, 18, Ui.ButtonHot), -1, 42);
                return;
            }
            var heading = Ui.Row(w.Content, 46, 14);
            Ui.Size(Line(heading, q.Title, 27, Ui.Accent), 0, -1, 1);
            Ui.Size(Ui.Label(heading, "进行中", 16, TextAnchor.MiddleCenter, Ui.Accent), 80);

            var columns = Ui.Row(w.Content, -1, 16);
            var objective = QuestDetailCard(columns, "QuestObjective", 1.35f);
            SectionBand(objective, "任务目标");
            Para(objective, QuestPaperInk(q.Hint), 18);
            Ui.Size(Ui.Panel(objective, "QuestObjectiveRule", Ui.Edge), -1, 1);
            Line(objective, "下一步", 20, Ui.Accent);
            string nextStep = q.Target == QuestTarget.RacingEnrollment
                ? S.RacingQualificationBlocker ?? q.Short : q.Short;
            Para(objective, QuestPaperInk(nextStep), 18,
                q.Target == QuestTarget.RacingEnrollment && S.RacingQualificationBlocker != null ? Ui.Warn : Ui.TextColor);

            var status = QuestDetailCard(columns, "QuestStatus", 1);
            SectionBand(status, "达成进度");
            if (q.Target == QuestTarget.RacingEnrollment)
            {
                Para(status, (S.HasOutstandingLoans ? "○ 待结清贷款" : "✓ 贷款已结清"), 18,
                    S.HasOutstandingLoans ? Ui.Warn : Ui.Good);
                Para(status, "自有现金\n" + S.CommunityOwnCash + " / "
                    + PCShopSell.Core.Common.Money.FromYuan(GameSession.RacingCashMinimumYuan), 19);
            }
            else
            {
                string progress = ProgressOf(q);
                Para(status, progress == null ? "完成左侧目标后自动更新。" : QuestPaperInk(progress.Replace(" · ", "\n")), 18,
                    progress == null ? Ui.Dim : Ui.TextColor);
            }
            string location = GuideLocation(q.Target, q.GuideDestination);
            if (!string.IsNullOrEmpty(location))
            {
                Ui.Size(Ui.Panel(status, "QuestLocationRule", Ui.Edge), -1, 1);
                Line(status, "办理地点 / 入口", 20, Ui.Accent);
                Para(status, QuestPaperInk(location), 18);
            }
            if (q.Reward != null)
                Para(w.Content, "完成奖励　" + QuestPaperInk(q.RewardText(S)), 18, Ui.Good);
            if (q.Target == QuestTarget.RacingEnrollment || q.Target == QuestTarget.BankSecurities)
            {
                var (label, action, travel) = QuestAction(q.Target, q.GuideDestination);
                if (action != null && !QuestUsesShortcut(travel))
                    Ui.Size(Ui.Button(w.Content, label, () => { w.Close(); action(); }, 18, Ui.ButtonHot), -1, 44);
                else
                    Ui.Size(Ui.Button(w.Content, "继续经营", w.Close, 18, Ui.ButtonHot), -1, 44);
                return;
            }
            if (q.Lesson)
            {
                bool canComplete = S.CanCompleteQuestLesson(q.Id);
                if (!canComplete) Para(w.Content, "按上面的指引操作后，回来确认完成。", 16, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "已了解，完成这一步", () =>
                {
                    if (!S.CompleteQuestLesson(q.Id)) return;
                    w.Close();
                    UpdateQuestTracker();
                }, 18, Ui.ButtonHot, canComplete), -1, 42);
            }
            else
            {
                var (label, action, travel) = QuestAction(q.Target, q.GuideDestination);
                var actions = Ui.Row(w.Content, 52, 12);
                if (action != null && !QuestUsesShortcut(travel))
                    Ui.Size(Ui.Button(actions, label, () => { w.Close(); action(); }, 18, Ui.ButtonHot), 0, 52, 1);
                Ui.Size(Ui.Button(actions, "继续游戏", w.Close, 18), 0, 52, 1);
            }
        }

        private static RectTransform QuestDetailCard(Transform parent, string name, float widthWeight)
        {
            var card = Ui.FlatPanel(parent, name, Ui.FlatBand);
            card.raycastTarget = false;
            Ui.Size(card, 0, -1, widthWeight);
            var rect = card.rectTransform;
            Ui.VList(rect, 14, 18);
            return rect;
        }

        private static void ResetGuideScroll(Window w)
        {
            Canvas.ForceUpdateCanvases();
            w.Content.parent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
        }

        private static QuestTarget TargetFor(ShopTab tab)
        {
            switch (tab)
            {
                case ShopTab.Stock: return QuestTarget.Stock;
                case ShopTab.Pricing: return QuestTarget.Pricing;
                case ShopTab.Orders: return QuestTarget.Orders;
                case ShopTab.Renovate: return QuestTarget.Renovate;
                case ShopTab.Staff: return QuestTarget.Staff;
                default: return QuestTarget.Counter;
            }
        }
        private static readonly QuestTarget[] BusinessTargets = { QuestTarget.Finance, QuestTarget.Direct, QuestTarget.Enterprise, QuestTarget.Ventures, QuestTarget.Creators, QuestTarget.Property, QuestTarget.Park };
        private string GuideLocation(QuestTarget target, Place destination = null)
        {
            if (destination != null)
            {
                string suffix = target == QuestTarget.Renovate ? " → 装修"
                    : target == QuestTarget.Stock ? " → 库存" : target == QuestTarget.Counter ? " → 柜台" : "";
                string name = S.ShopAt(destination) is OwnedShop shop ? S.ShopTitle(shop) : destination.Name;
                return $"地点：{name}{suffix}";
            }
            if (target == QuestTarget.Market && S.ShopSupplyMarketId(S.RetailKind) is string marketId)
            {
                var market = S.PurchaseDestination(S.City.GetPlace(marketId));
                return $"地点：{S.City.StreetName(market.StreetId)} · <color=#80551F>{market.Name}</color>";
            }
            if(S.RetailKind!="computer")
            {
                if(target==QuestTarget.Market)return S.RetailKind=="restaurant"?"地点：农贸市场 · 采购大米与配菜":S.RetailKind=="secondhand"?"位置：自己的店 → 柜台 · 收购旧物":S.RetailKind==PetShopCatalog.ShopKind?"地点：城外批发街 · 暖爪宠物用品批发市场":"地点：对应品类的批发店 · 或手机网购";
                if(target==QuestTarget.Stock)return "位置：自己的店 → 右侧“库存”页";
                if(target==QuestTarget.Counter)return "位置：自己的店 → 右侧“柜台”页";
            }
            switch (target)
            {
                case QuestTarget.Cooker: return "位置：家里 · 电饭锅";
                case QuestTarget.Bed: return "位置：家里 · 床边";
                case QuestTarget.Agency:
                    return S.City.HasPlace("agency") && S.City.GetPlace("agency") is Place agency
                        ? $"地点：{S.City.StreetName(agency.StreetId)} · <color=#80551F>{agency.Name}</color>"
                        : "";
                case QuestTarget.Renovate: return "位置：自己的店 → 右侧“装修”页";
                case QuestTarget.Stock: return "位置：自己的店 · 货架";
                case QuestTarget.Market: return "地点：<color=#80551F>电子市场</color> · 配件档口";
                case QuestTarget.Counter: return "位置：自己的店 · 收银台";
                case QuestTarget.Staff: return "位置：自己的店 → 右侧“员工”页";
                case QuestTarget.PhoneShop: return "地点：商业街 · <color=#80551F>手机店</color>";
                case QuestTarget.BankSecurities: return "地点：城郊银行柜台";
                case QuestTarget.CommunityGoal: return "片区：城郊";
                case QuestTarget.RacingEnrollment: return "地点：城郊赛场车库";
                case QuestTarget.ScooterDealer:
                    return ScooterDealer is Place dealer
                        ? $"地点：{S.City.StreetName(dealer.StreetId)} · <color=#80551F>{dealer.Name}</color>"
                        : "";
                case QuestTarget.Mall: return "地点：商业街 · <color=#80551F>宜居家具城</color>";
                case QuestTarget.Upgrades: return "设备在街上的对应商店购买，家具在店铺“装修”页摆放。";
                case QuestTarget.Pricing: return "位置：自己的店 → 右侧“定价”页";
                case QuestTarget.Orders: return "位置：自己的店 → 右侧“订单”页";
                case QuestTarget.Restock: return "入口：手机 → 网购";
                case QuestTarget.Finance: return "入口：资产 → 经营中的店铺。";
                case QuestTarget.Enterprise: return "入口：电脑店 → 订单 → 企业采购订单。";
                case QuestTarget.Direct: return "入口：经营 → 店 → 直供协议与账款。";
                case QuestTarget.Ventures: return "地点：安居房产中介";
                case QuestTarget.Park: return "入口：前往园区 → 园区管理";
                case QuestTarget.Creators: return "入口：品牌 → “代言人（KOL）”。";
                case QuestTarget.Property: return "入口：资产 → 我的物业；购买前往所属中介。";
                default: return "";
            }
        }
    }
}
