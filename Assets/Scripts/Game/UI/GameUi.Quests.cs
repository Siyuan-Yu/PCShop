using System;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Image _questPanel;
        private Text _questHeading, _questTitle, _questProgress, _questText, _questActionText;
        private RectTransform _questProgressBar, _questProgressFill;
        private Button _questActionButton;
        private Action _questAction;
        private string _questSeen;
        private bool _questInitialized;
        private Window _questWindow;
        private Window _questPopup;
        private bool _questHidden;
        private bool _popupPending;
        private QuestDef _pendingDone, _pendingNext;

        /// <summary>任务完成 / 换任务时强制弹窗（冒烟测试脚本会关掉，免得挡住脚本流程）。</summary>
        public bool QuestPopups = true;

        public void ResetGuideState()
        {
            _purchaseDrafts.Clear();
            _questSeen = null;
            _questInitialized = false;
            _questWindow = null;
            _questPopup = null;
            _questHidden = false;
            _popupPending = false;
            _pendingDone = _pendingNext = null;
            _businessTab = 0;
        }

        private void BuildQuestTracker(Transform root)
        {
            _questPanel = Ui.Panel(root, "QuestTracker", Ui.PanelBg);
            _questPanel.rectTransform.Anchor(0, 1, 0, 1, 16, -332, -456, 82);

            _questHeading = Ui.Label(_questPanel.transform, "", 17, color: Ui.Accent);
            _questHeading.rectTransform.Anchor(0, 1, 1, 1, 16, -37, 46, 10);
            _questTitle = Ui.Label(_questPanel.transform, "", 22);
            _questTitle.fontStyle = FontStyle.Bold;
            _questTitle.rectTransform.Anchor(0, 1, 1, 1, 16, -70, 16, 37);
            _questProgress = Ui.Label(_questPanel.transform, "", 16, color: Ui.Good);
            _questProgress.rectTransform.Anchor(0, 1, 1, 1, 16, -96, 16, 72);
            _questProgressBar = Ui.Panel(_questPanel.transform, "QuestProgressTrack", Ui.PanelBg2).rectTransform;
            _questProgressBar.Anchor(0, 1, 1, 1, 16, -109, 16, 102);
            _questProgressFill = Ui.Panel(_questProgressBar, "QuestProgressFill", Ui.Good).rectTransform;
            _questText = Ui.Label(_questPanel.transform, "", 17, TextAnchor.UpperLeft);
            _questText.rectTransform.Anchor(0, 0, 1, 1, 16, 58, 16, 76);
            _questText.verticalOverflow = VerticalWrapMode.Truncate;

            _questActionButton = Ui.Button(_questPanel.transform, "", () => _questAction?.Invoke(), 18, Ui.ButtonHot);
            _questActionButton.name = "QuestPrimaryAction";
            _questActionText = _questActionButton.GetComponentInChildren<Text>();
            ((RectTransform)_questActionButton.transform).Anchor(0, 0, .74f, 0, 14, 12, 4, -49);
            var help = Ui.Button(_questPanel.transform, "详情", OpenTrackedQuestDetails, 16);
            ((RectTransform)help.transform).Anchor(.74f, 0, 1, 0, 4, 12, 14, -49);
            var close = Ui.Button(_questPanel.transform, "×", () =>
            {
                _questHidden = true;
                UpdateQuestTracker();
                Toast("任务条收起来了，点顶栏“任务”（或按 T）随时再打开。");
            }, 20);
            ((RectTransform)close.transform).Anchor(1, 1, 1, 1, -38, -36, 6, 6);
            close.Tip("收起任务条（顶栏“任务”按钮可重新打开）");
            _questPanel.gameObject.SetActive(false);
        }

        /// <summary>顶栏“任务”按钮：打开当前任务说明，并重新显示左上角的任务条。</summary>
        public void OpenQuests()
        {
            if(S.IsOwnedVenue(S.CurrentPlace)){OpenVenueManagement(S.CurrentPlace);return;}
            _questHidden = false;
            UpdateQuestTracker();
            if (_questWindow != null && !_questWindow.Closed) { _questWindow.Rebuild(); return; }
            _questWindow = OpenWindow("当前任务", 700, 520, BuildGuideWindow, onClosed: () => _questWindow = null);
        }

        /// <summary>冒烟截图用：弹出“完成了租铺任务、接下来去家具城”的任务完成窗口。</summary>
        public void DebugQuestPopup() => OpenQuestPopup(Quests.Get("rent"), Quests.Get("mall"));

        public string QuestTrackerText => _questPanel != null && _questPanel.gameObject.activeSelf
            ? string.Join("\n", _questHeading.text, _questTitle.text, _questProgress.text, _questText.text) : null;
        public void DebugRefreshQuests() => UpdateQuestTracker();

        private void UpdateQuestTracker()
        {
            if (_questPanel == null) return;
            if(S.IsOwnedVenue(S.CurrentPlace))
            {
                _questPanel.gameObject.SetActive(!_questHidden&&!S.IsGameOver&&!_ctrl.Placing);
                SetQuestCard("场馆经营", S.VenueBusinessName(S.CurrentPlace), "设置价格和营业时间，安排值守后开启接客。",
                    "顾客到场后付费，老板自用免费。", null, "场馆管理", () => OpenVenueManagement(S.CurrentPlace));
                return;
            }
            QuestDef q = S.CurrentQuest();
            if (_questInitialized && _questSeen != q?.Id && !S.IsGameOver)
            {
                QuestDef done = _questSeen != null && S.QuestCompleted(_questSeen) ? Quests.Get(_questSeen) : null;
                if (QuestPopups && (done != null || q != null))
                {
                    _pendingDone = _pendingDone ?? done;
                    _pendingNext = q;
                    _popupPending = true;
                }
                else if (done != null) Toast($"<color=#A9C9A9>完成：{done.Title}</color>" + (q != null ? $"\n接下来：{q.Title}" : "\n继续经营小店吧。"));
                else if (q != null) Toast("新任务：" + q.Title);
            }
            // Wait until result popups (e.g. "签约成功") are dismissed, so they read first.
            if (_popupPending && !PopupOpen && (_questPopup == null || _questPopup.Closed) && !S.IsGameOver)
            {
                OpenQuestPopup(_pendingDone, _pendingNext);
                _popupPending = false;
                _pendingDone = _pendingNext = null;
            }
            _questInitialized = true;
            _questSeen = q?.Id;
            var district = S.CurrentDistrictNode;
            string invitation = S.DistrictInvitationStatus;
            bool visible = (q != null || district != null || invitation != null) && !_questHidden && !S.IsGameOver && !_ctrl.Placing;
            _questPanel.gameObject.SetActive(visible);
            if (!visible) return;
            if (district != null) UpdateDistrictQuestCard(district);
            else if (invitation != null) SetQuestCard("主线 · 等待联系", "等待街区引荐", invitation);
            else
            {
                var (label, action) = QuestAction(q.Target);
                SetQuestCard("当前任务", q.Title, q.Short, GuideLocation(q.Target), ProgressOf(q)?.Split('\n')[0], label, action);
            }
        }

        private void SetQuestCard(string heading, string title, string next, string status = null, string progress = null,
            string actionLabel = null, Action action = null, float? completion = null)
        {
            bool showProgress = !string.IsNullOrEmpty(progress);
            _questHeading.text = heading;
            _questTitle.text = title;
            _questProgress.text = progress ?? "";
            _questProgressBar.gameObject.SetActive(completion.HasValue);
            if (completion.HasValue) _questProgressFill.Anchor(0, 0, Mathf.Clamp01(completion.Value), 1);
            float textTop = completion.HasValue ? 116 : showProgress ? 99 : 77;
            _questText.rectTransform.offsetMax = new Vector2(-16, -textTop);
            _questText.text = "<color=#E8BE62>下一步</color>　" + next
                + (string.IsNullOrEmpty(status) ? "" : "\n<size=15><color=#AAB4BB>" + status + "</color></size>");
            _questPanel.rectTransform.offsetMin = new Vector2(16, -(82 + textTop + 130));
            _questAction = action;
            _questActionText.text = actionLabel ?? "";
            _questActionButton.gameObject.SetActive(action != null);
        }

        private void OpenTrackedQuestDetails()
        {
            if (!S.IsOwnedVenue(S.CurrentPlace) && (S.CurrentDistrictNode != null || S.DistrictInvitationStatus != null))
                OpenDistrictStory();
            else OpenQuestWindow();
        }

        private void UpdateDistrictQuestCard(DistrictNarrativeNode node)
        {
            var state = S.DistrictStoryState;
            string heading = "主线 · " + GameSession.DistrictName(state.Chapter) + "引荐";
            if (node.Id == "community.intro")
            {
                SetQuestCard(heading, node.Title, "回复李秀兰，接下寻找旧街商铺图的委托。",
                    "李秀兰的消息等待回复", actionLabel: "查看并回复", action: OpenDistrictStory);
                return;
            }
            if (S.CommunitySearchActive)
            {
                var leads = Enumerable.Range(0, 3).Where(i => !S.CommunityHasPiece(i)).ToArray();
                int found = 3 - leads.Length;
                if (leads.Length == 0)
                {
                    SetQuestCard(heading, node.Title, "准备把图页交给唐蔚。", progress: "已找到图页 3/3", completion: 1);
                    return;
                }
                int lead = leads.FirstOrDefault(S.CommunityBoxAvailable);
                string progress = "已找到图页 " + found + "/3";
                if (S.CommunityBoxAvailable(lead))
                {
                    SetQuestCard(heading, node.Title, "去藏箱位置调查" + GameSession.CommunityPieceName(lead) + "。",
                        progress: progress, actionLabel: "前往藏箱地点", action: () => S.GoTo(S.CommunityBoxNode(lead)), completion: found / 3f);
                }
                else
                {
                    lead = leads[0];
                    string person = GameSession.CommunityLeadPerson(lead);
                    SetQuestPersonAction(heading, node.Title, person, S.CommunityLeadStatus(lead), progress, found / 3f);
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
                    SetQuestCard(heading, node.Title, "按约定去会面点。", time, mapProgress, "前往会面点", () =>
                    {
                        if (S.DistrictMeetingStreetNode is string streetNode) S.GoTo(streetNode);
                        else S.BeginCityTravel(place, enter: false);
                    }, mapCompletion);
                else if (S.Clock.Now.TotalMinutes < state.MeetingAt)
                    SetQuestCard(heading, node.Title, "已到达，等到约定时间再交谈。", time, mapProgress, completion: mapCompletion);
                else if (S.DistrictMeetingBlocker is string blocker)
                {
                    string next = state.MeetingPeople.Any(id => S.CityPersonState(id)?.Route != null) ? "会面人员还在路上，请稍候。" : blocker;
                    SetQuestCard(heading, node.Title, next, time, mapProgress, completion: mapCompletion);
                }
                else
                    SetQuestPersonAction(heading, node.Title, contact,
                        mapProgress != null ? "把图页交给唐蔚。" : "与联系人交谈，确认这一步。", mapProgress, mapCompletion, where + " · 已到达");
                return;
            }
            SetQuestPersonAction(heading, node.Title, contact, node.Objective, mapProgress, mapCompletion);
        }

        private void SetQuestPersonAction(string heading, string title, string id, string next, string progress,
            float? completion, string status = null)
        {
            var person = S.CityPerson(id);
            if (person == null)
            {
                SetQuestCard(heading, title, next, status, progress, completion: completion);
                return;
            }
            bool nearby = S.CitySamePlace(id);
            var waiting = S.DistrictPersonWaitingPlace(id);
            string place = S.CityPersonState(id)?.Route?.TargetPlaceId ?? S.CityPersonState(id)?.LocationPlaceId;
            if (status == null && (waiting != null || S.City.HasPlace(place)))
                status = (waiting ?? S.City.GetPlace(place)).Name + (nearby ? " · 已到达" : " · " + person.Name);
            SetQuestCard(heading, title, next, status, progress, (nearby ? "与" : "寻找") + person.Name + (nearby ? "交谈" : ""),
                () => GoToTrackedPerson(id), completion);
        }

        private void GoToTrackedPerson(string id)
        {
            var person = S.CityPersonState(id);
            if (person == null) return;
            if (S.CitySamePlace(id)) { OpenPersonDialogue(id); return; }
            var waiting = S.DistrictPersonWaitingPlace(id);
            if (waiting != null) { S.BeginCityTravel(waiting.Id, enter: false); return; }
            if (S.CityPersonInside(person) && S.City.HasPlace(person.LocationPlaceId))
            { S.BeginCityTravel(person.LocationPlaceId, enter: false); return; }
            var position = person.Route?.Position(S.Clock.Now.TotalMinutes + S.Time.FractionalMinute) ?? new MapPoint(person.X, person.Y);
            S.GoTo(S.City.NearestNode(position.X, position.Y));
        }

        private (string label, Action action) QuestAction(QuestTarget target)
        {
            switch (target)
            {
                case QuestTarget.Agency:
                case QuestTarget.Ventures: return QuestPlaceAction("前往中介", S.City.HasPlace("agency") ? S.City.GetPlace("agency") : null);
                case QuestTarget.Mall: return QuestPlaceAction("前往家具城", S.City.Places.FirstOrDefault(p => p.Business == GameSession.MallBusiness));
                case QuestTarget.PhoneShop: return QuestPlaceAction("前往手机店", S.City.Places.FirstOrDefault(p => p.Business == GameSession.PhoneBusiness));
                case QuestTarget.Market:
                    if (S.RetailKind == "restaurant") return QuestPlaceAction("前往农贸市场", S.City.Places.FirstOrDefault(p => p.Business == GameSession.FarmBusiness));
                    if (S.RetailKind == PetShopCatalog.ShopKind && S.City.HasPlace(PetShopCatalog.SupplierId)) return QuestPlaceAction("前往宠物批发市场", S.City.GetPlace(PetShopCatalog.SupplierId));
                    if (S.RetailKind == "computer") return QuestPlaceAction("前往电子市场", S.City.HasPlace("stall_cpu") ? S.City.GetPlace("stall_cpu") : null);
                    if (S.RetailKind != "secondhand") return ("打开采购", OpenCityProcurement);
                    return QuestShopAction(ShopTab.Counter, "打开柜台");
                case QuestTarget.Renovate: return QuestShopAction(ShopTab.Renovate, "打开装修");
                case QuestTarget.Stock: return QuestShopAction(ShopTab.Stock, "打开库存");
                case QuestTarget.Counter: return QuestShopAction(ShopTab.Counter, "打开柜台");
                case QuestTarget.Staff: return QuestShopAction(ShopTab.Staff, "打开员工");
                case QuestTarget.Pricing: return QuestShopAction(ShopTab.Pricing, "打开定价");
                case QuestTarget.Orders:
                case QuestTarget.Enterprise: return QuestShopAction(ShopTab.Orders, "打开订单");
                case QuestTarget.Finance: return ("打开资产", () => OpenAssetWorkspace(0));
                case QuestTarget.Property: return ("查看物业", () => OpenAssetWorkspace(1));
                case QuestTarget.Restock: return ("打开网购", OpenOnlinePanel);
                case QuestTarget.Direct: return ("打开采购", OpenCityProcurement);
                case QuestTarget.Creators: return ("打开品牌", OpenBrandBusiness);
                default: return (null, null);
            }
        }

        private (string label, Action action) QuestPlaceAction(string label, Place place)
        {
            if (place == null) return (null, null);
            return S.CurrentPlace == place ? ("打开" + place.Name, () => OnEnteredPlace(place))
                : (label, () => S.BeginCityTravel(place.Id));
        }

        private (string label, Action action) QuestShopAction(ShopTab tab, string label)
        {
            if (!S.HasShop) return (null, null);
            if (S.InPlayerShop) return (label, () => { OpenDock(); SelectTab(tab); });
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
            if (S.CurrentQuest() == null && S.CurrentDistrictNode == null && S.DistrictInvitationStatus == null) return;
            if (_questWindow != null && !_questWindow.Closed) { _questWindow.Rebuild(); return; }
            _questWindow = OpenWindow("当前任务", 700, 520, BuildGuideWindow, onClosed: () => _questWindow = null);
        }

        /// <summary>任务变化时强制弹出：先单独弹“任务完成”（完成了什么、拿到什么），关掉后再弹“新任务”。</summary>
        private void OpenQuestPopup(QuestDef done, QuestDef next)
        {
            if (_questPopup != null && !_questPopup.Closed) _questPopup.Close();
            _questPopup = OpenWindow(done != null ? "任务完成" : "新任务", 620, 420, w =>
            {
                if (done != null)
                {
                    Line(w.Content, "✓ " + done.Title, 23, Ui.Good);
                    if (done.Reward != null) Para(w.Content, "获得：" + done.RewardText(S), 18, Ui.Accent);
                }
                if (next != null)
                {
                    Line(w.Content, "接下来：" + next.Title, 21, Ui.Accent);
                    Para(w.Content, GuideLocation(next.Target), 16, Ui.Dim);
                    Para(w.Content, next.Short, 18);
                    ProgressLine(w.Content, next, 16);
                }
                Ui.Size(Ui.Button(w.Content, "继续游戏", w.Close, 18, Ui.ButtonHot), -1, 42);
            }, onClosed: () => _questPopup = null);
        }

        private void BuildGuideWindow(Window w)
        {
            BuildDistrictMessage(w.Content);
            var q = S.CurrentQuest();
            if (q == null)
            {
                int n = Quests.All.Count(x => S.QuestCompleted(x.Id));
                Para(w.Content, $"暂时没有新任务（已完成 {n} 个）。继续经营，达到下一阶段的条件时会弹出新任务。", 20);
                Ui.Size(Ui.Button(w.Content, "继续", w.Close, 18, Ui.ButtonHot), -1, 42);
                return;
            }
            Line(w.Content, q.Title, 23, Ui.Accent);
            // Guidance explains where/how; it must never move the player or open a destination page.
            Para(w.Content, GuideLocation(q.Target), 17, Ui.Accent);
            Para(w.Content, q.Hint, 18);
            if (q.Reward != null) Para(w.Content, "完成奖励：" + q.RewardText(S), 16, Ui.Good);
            ProgressLine(w.Content, q, 16);
            Ui.Size(Ui.Button(w.Content, "知道了", w.Close, 18, Ui.ButtonHot), -1, 42);
        }

        private static void ResetGuideScroll(Window w)
        {
            Canvas.ForceUpdateCanvases();
            w.Content.parent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
        }

        private void GuideHint(Transform parent, QuestTarget target, Action refresh)
        {
            var q = S.CurrentQuest();
            if (q == null || q.Target != target) return;
            S.VisitQuestTarget(target);
            var card = Ui.Panel(parent, "GuideHint", Ui.PanelBg2).rectTransform;
            Ui.VList(card, 6, 12);
            Para(card, "<color=#DDC18B><b>当前任务 · " + q.Title + "</b></color>\n" + (target==QuestTarget.Staff&&q.Lesson?q.Hint:q.Short), 16);
            ProgressLine(card, q, 14);
            var row = Ui.Row(card, 34, 6);
            Ui.Size(Ui.Button(row, "查看步骤", OpenQuestWindow, 15), -1, -1, 1);
            if (q.Lesson)
                Ui.Size(Ui.Button(row, "已了解，完成这一步", () => { S.CompleteQuestLesson(q.Id); UpdateQuestTracker(); refresh(); }, 15, Ui.ButtonHot, q.LessonReady == null || q.LessonReady(S)), -1, -1, 1);
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
        private string GuideLocation(QuestTarget target)
        {
            if(S.RetailKind!="computer")
            {
                if(target==QuestTarget.Market)return S.RetailKind=="restaurant"?"地点：农贸市场 · 采购大米与配菜":S.RetailKind=="secondhand"?"位置：自己的店 → 柜台 · 收购旧物":S.RetailKind==PetShopCatalog.ShopKind?"地点：城外批发街 · 暖爪宠物用品批发市场":"地点：对应品类的批发店 · 或手机网购";
                if(target==QuestTarget.Stock)return "位置：自己的店 → 右侧“库存”页";
                if(target==QuestTarget.Counter)return "位置：自己的店 → 右侧“柜台”页";
            }
            switch (target)
            {
                case QuestTarget.Agency: return "地点：商业街 · <color=#FFC966>安居房产中介</color>";
                case QuestTarget.Renovate: return "位置：自己的店 → 右侧“装修”页";
                case QuestTarget.Stock: return "位置：自己的店 · 货架";
                case QuestTarget.Market: return "地点：<color=#FFC966>电子市场</color> · 配件档口";
                case QuestTarget.Counter: return "位置：自己的店 · 收银台";
                case QuestTarget.Staff: return "位置：自己的店 → 右侧“员工”页";
                case QuestTarget.PhoneShop: return "地点：商业街 · <color=#FFC966>手机店</color>";
                case QuestTarget.Mall: return "地点：商业街 · <color=#FFC966>宜居家具城</color>";
                case QuestTarget.Upgrades: return "设备在街上的对应商店购买，家具在店铺“装修”页摆放。";
                case QuestTarget.Pricing: return "位置：自己的店 → 右侧“定价”页";
                case QuestTarget.Orders: return "位置：自己的店 → 右侧“订单”页";
                case QuestTarget.Restock: return "入口：手机 → 网购";
                case QuestTarget.Finance: return "入口：资产 → 经营中的店铺。";
                case QuestTarget.Enterprise: return "入口：电脑店 → 订单 → 企业采购订单。";
                case QuestTarget.Direct: return "入口：采购 → 直供协议与账款。";
                case QuestTarget.Ventures: return "地点：安居房产中介";
                case QuestTarget.Park: return "入口：前往园区 → 园区管理";
                case QuestTarget.Creators: return "入口：品牌 → “代言人（KOL）”。";
                case QuestTarget.Property: return "入口：资产 → 我的物业；购买前往所属中介。";
                default: return "";
            }
        }
    }
}
