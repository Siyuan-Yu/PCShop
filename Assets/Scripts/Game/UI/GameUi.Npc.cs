using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>人物立绘、店里的人（店员 / 老板）和对话框：点人物弹出对话框，说话在上面，选项在下面，随交情解锁更多。</summary>
    public sealed partial class GameUi
    {
        private static readonly Dictionary<string, Texture2D> PortraitCache = new Dictionary<string, Texture2D>();

        /// <summary>按稳定人物ID加载动漫头像（Resources/PortraitsAnime）；动态人物使用动漫备用图。旧复古头像已移出 Resources，不进游戏包。</summary>
        public static Texture2D PortraitOf(string npcId)
        {
            npcId = string.IsNullOrWhiteSpace(npcId) ? "default" : npcId;
            if (!PortraitCache.TryGetValue(npcId, out Texture2D tex) || tex == null)
            {
                tex = Resources.Load<Texture2D>("PortraitsAnime/" + npcId)
                    ?? Resources.Load<Texture2D>("PortraitsAnime/default");
                PortraitCache[npcId] = tex;
            }
            return tex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void InvalidatePortraitCache()
        {
            PortraitCache.Clear();
            ScenePortraits.Clear();
        }

        private static RawImage FitPortrait(RectTransform frame, string portraitId, float padding = 0)
        {
            var holder = Ui.Rect(frame, "PortraitViewport").Fill(padding);
            var raw = Ui.Rect(holder, "Image").Fill().gameObject.AddComponent<RawImage>();
            raw.texture = PortraitOf(portraitId);
            FitImageToParent(raw);
            return raw;
        }

        private static void FitImageToParent(RawImage image)
        {
            if (image.texture == null) return;
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)image.texture.width / image.texture.height;
        }

        private static void CropPortraitToSquare(RawImage image)
        {
            if (image.texture == null) return;
            var crop = new Rect(.12f, .48f, .76f, .5f);
            float aspect = (float)image.texture.width / image.texture.height;
            float width = Mathf.Min(crop.width, crop.height / aspect);
            float height = width * aspect;
            image.uvRect = new Rect(crop.center.x - width * .5f, crop.center.y - height * .5f, width, height);
        }

        /// <summary>立绘卡片（3:4）。onClick 不为空时可点击，右上角有“点击交谈”小标。unknown = 还不认识，只显示暗色剪影。</summary>
        private RectTransform PortraitCard(Transform parent, NpcDef n, float width, System.Action onClick, bool unknown = false) =>
            PortraitCard(parent, n.Id, n.Name, n.Role, width, onClick, unknown);

        private RectTransform PortraitCard(Transform parent, string portraitId, string title, string subtitle, float width, System.Action onClick, bool unknown = false, string tagText = "交谈")
        {
            float height = width * 4f / 3f;
            Image frame = Ui.Panel(parent, "Portrait", Ui.PanelBg2);
            Ui.Size(frame, width, height);
            frame.GetComponent<LayoutElement>().minWidth = width;

            var raw = FitPortrait(frame.rectTransform, portraitId, 3);
            var imgRt = raw.rectTransform;
            raw.color = unknown ? new Color(0.12f, 0.12f, 0.14f) : Color.white;
            if (raw.texture == null)
            {
                raw.color=new Color(.20f,.24f,.26f);
                var head=Ui.Panel(imgRt,"PortraitPlaceholderHead",new Color(.56f,.60f,.58f));
                head.raycastTarget=false;head.rectTransform.anchorMin=new Vector2(.36f,.58f);head.rectTransform.anchorMax=new Vector2(.64f,.82f);head.rectTransform.offsetMin=head.rectTransform.offsetMax=Vector2.zero;
                var shoulders=Ui.Panel(imgRt,"PortraitPlaceholderBody",new Color(.40f,.49f,.47f));
                shoulders.raycastTarget=false;shoulders.rectTransform.anchorMin=new Vector2(.19f,.17f);shoulders.rectTransform.anchorMax=new Vector2(.81f,.56f);shoulders.rectTransform.offsetMin=shoulders.rectTransform.offsetMax=Vector2.zero;
            }

            if (onClick != null && !unknown)
            {
                var b = imgRt.gameObject.AddComponent<Button>();
                b.targetGraphic = raw;
                Ui.SteadyButtonFeedback(b);
                b.onClick.AddListener(() => onClick());
            }

            Image bar = Ui.Panel(frame.transform, "Caption", new Color(0, 0, 0, 0.62f));
            bar.raycastTarget = false;
            bar.rectTransform.Anchor(0, 0, 1, 0, 3, 3, 3, -34);
            Text cap = Ui.Label(bar.transform, unknown ? "？？？" : $"<b>{title}</b>　<size=14>{subtitle}</size>", 18, TextAnchor.MiddleCenter);
            cap.raycastTarget = false;
            cap.rectTransform.Fill(2);

            if (onClick != null && !unknown && !string.IsNullOrEmpty(tagText))
            {
                Image tag = Ui.Panel(frame.transform, "Tag", Ui.ButtonHot);
                tag.raycastTarget = false;
                tag.rectTransform.Anchor(1, 1, 1, 1, -96, -30, 6, 6);
                Text t = Ui.Label(tag.transform, tagText, 14, TextAnchor.MiddleCenter, Ui.TextColor);
                t.raycastTarget = false;
                t.rectTransform.Fill(1);
            }
            return frame.rectTransform;
        }

        private static string PersonNameWithKolBadge(string personId, string name) =>
            KolMarket.Get(personId) == null ? name
                : $"{name}  <size=14><color=#{ColorUtility.ToHtmlStringRGB(Ui.Accent)}>KOL</color></size>";

        private RectTransform PersonSummary(Transform parent, string personId, string name, string detail, int height = 54,
            System.Action onClick = null, Color? ink = null, Color? muted = null)
        {
            var row = Ui.Row(parent, height, 9);
            PortraitThumb(row, personId, Mathf.Min(48, height - 4), onClick);
            string secondary = ColorUtility.ToHtmlStringRGB(muted ?? Ui.Dim);
            var label = Ui.Label(row, $"<b>{name}</b>\n<size=14><color=#{secondary}>{detail}</color></size>", 17, color: ink);
            Ui.Size(label, -1, -1, 1);
            return row;
        }

        /// <summary>小头像（截取立绘上半部分的脸）。</summary>
        private void PortraitThumb(Transform parent, NpcDef n, float size, System.Action onClick = null)
            => PortraitThumb(parent, n.Id, size, onClick);

        private void PortraitThumb(Transform parent, string portraitId, float size, System.Action onClick, bool dimmed = false)
        {
            Image frame = Ui.Panel(parent, "Thumb", Ui.PanelBg2);
            Ui.Size(frame, size, size);
            frame.GetComponent<LayoutElement>().minWidth = size;
            RectTransform imgRt = Ui.Rect(frame.transform, "Image");
            imgRt.anchorMin = imgRt.anchorMax = Vector2.one * .5f;
            imgRt.sizeDelta = Vector2.one * (size - 4f);
            var raw = imgRt.gameObject.AddComponent<RawImage>();
            raw.texture = PortraitOf(portraitId);
            CropPortraitToSquare(raw);
            if (dimmed) raw.color = new Color(.52f, .52f, .52f);
            if (raw.texture == null)
            {
                raw.color=new Color(.20f,.24f,.26f);
                var head=Ui.Panel(imgRt,"PortraitPlaceholderHead",new Color(.56f,.60f,.58f));
                head.raycastTarget=false;head.rectTransform.anchorMin=new Vector2(.36f,.58f);head.rectTransform.anchorMax=new Vector2(.64f,.82f);head.rectTransform.offsetMin=head.rectTransform.offsetMax=Vector2.zero;
                var shoulders=Ui.Panel(imgRt,"PortraitPlaceholderBody",new Color(.40f,.49f,.47f));
                shoulders.raycastTarget=false;shoulders.rectTransform.anchorMin=new Vector2(.19f,.17f);shoulders.rectTransform.anchorMax=new Vector2(.81f,.56f);shoulders.rectTransform.offsetMin=shoulders.rectTransform.offsetMax=Vector2.zero;
            }
            if (onClick != null)
            {
                var b = imgRt.gameObject.AddComponent<Button>();
                b.targetGraphic = raw;
                b.onClick.AddListener(() => onClick());
            }
        }

        // ---------- 店里的人 ----------

        /// <summary>
        /// 左栏显示实际到场的人，值班人员和本次同行者排在前面。返回右侧商品或服务容器。
        /// </summary>
        private RectTransform BuildRosterFrame(Window w, Place p, float bodyHeight, bool scrollGoods = false, float rosterWidth = 210)
        {
            var locations = p.Kind == PlaceKind.Wholesaler ? S.WholesaleStallsAt(p) : new[] { p };
            var present = locations.SelectMany(place => S.PeopleInPlace(place.Id))
                .Where(person => S.CityPerson(person.PersonId) != null).ToArray();
            var activity = S.RunningCityActivity is CityActivity running && running.VenueId == p.Id
                ? running : S.PendingVenueActivity(p);

            var body=Ui.Row(w.Content,bodyHeight,12);
            var people = Ui.FlatPanel(body, "People", Ui.PanelBg2).rectTransform;
            Ui.VList(people, 8, 5);
            // Override the vertical group's flexible width: the roster must not share
            // spare horizontal space with the goods column, especially with a ScrollRect.
            var peopleSize=Ui.Size(people,rosterWidth,-1,0).GetComponent<LayoutElement>();
            peopleSize.minWidth=rosterWidth;
            bool fishing = p.Id == GameSession.FishingVenueId;
            bool hospital = p.Business == GameSession.HospitalBusiness;
            bool sport = p.Business is "venue_court" or "venue_golf";
            Ui.Size(Ui.Label(people, fishing ? "钓场在场人物" : sport ? "球场在场人物" : hospital ? "医院在场人物" : "店内在场人物", 17), -1, 30);
            float portraitArea = Mathf.Max(140, bodyHeight - 40);
            float portraitWidth = rosterWidth >= 190 ? 100 : 82;
            float portraitRowHeight = portraitWidth * 4f / 3f + 64;
            bool compact = present.Length > 1 && present.Length * (portraitRowHeight + 4) + 8 > portraitArea;
            var portraits = Ui.Scroll(people, portraitArea);
            if (S.NpcVenueStaffingBlocker(p) is string staffing)
                Para(portraits, staffing, 16, Ui.Warn);
            else if (present.Length == 0) Para(portraits, "此刻没有其他人在场。", 16, Ui.Dim);
            bool OnDuty(CityPersonState person) => S.IsOnDutyVenueNpc(S.City.GetPlace(person.LocationPlaceId), person);
            bool Companion(CityPersonState person) => activity?.Participants.Any(participant =>
                participant.PersonId == person.PersonId && participant.Phase != CityParticipantPhase.Left) == true;
            bool sponsorGuide = p.Id == GameSession.VenueSponsorGuidePlaceId;
            bool GuideContact(CityPersonState person) => sponsorGuide && person.PersonId == GameSession.VenueSponsorGuidePersonId;
            bool guideTalk = sponsorGuide && S.VenueSponsorGuideStep == SponsorGuideStep.Talk;
            foreach (var state in present.OrderByDescending(person => guideTalk && GuideContact(person))
                         .ThenByDescending(OnDuty).ThenByDescending(Companion))
            {
                string id=state.PersonId;var def=S.CityPerson(id);
                bool onDuty = OnDuty(state);
                bool companion = Companion(state);
                string role = onDuty ? S.CityPersonRole(id) + " · 值班" : companion ? "同行" : S.CityPersonRole(id);
                string tooltip = CanTalkToScenePerson(id) ? "当面交谈" : "查看人物资料";
                if (compact)
                {
                    var row = PersonSummary(portraits, id, def.Name, role, onClick: () => OpenCityPerson(id),
                        muted: onDuty ? Ui.Accent : companion ? Ui.Good : Ui.Dim);
                    var button = row.GetComponentInChildren<Button>();
                    if (button != null)
                    {
                        button.Tip(tooltip);
                        if (GuideContact(state)) button.name = SponsorGuidePortraitControl;
                    }
                }
                else
                    OrgNode(portraits, id, def.Name, role, onDuty, false,
                        () => OpenCityPerson(id), portraitWidth, rosterWidth - 26, tooltip,
                        GuideContact(state) ? SponsorGuidePortraitControl : null);
            }
            var center=Ui.Rect(body,"Goods");Ui.VList(center,6,0);
            var goodsSize=Ui.Size(center,0,-1,1).GetComponent<LayoutElement>();
            goodsSize.minWidth=0;
            if (!scrollGoods) return center;
            // The roster row has a fixed height. Its product column needs its own viewport;
            // overflow inside a plain layout group is invisible to the window's outer scroll.
            var goods=Ui.Scroll(center);
            var area=(RectTransform)goods.parent.parent;
            area.name="PlaceGoodsScroll";
            var size=area.gameObject.AddComponent<LayoutElement>();
            size.minHeight=0;size.preferredHeight=0;size.flexibleHeight=1;
            return goods;
        }

        private void OrgNode(Transform parent, string portraitId, string name, string title, bool top, bool locked,
            System.Action onClick, float portraitWidth = 44, float columnWidth = 72, string tooltip = null, string controlId = null)
        {
            float portraitHeight = portraitWidth * 4f / 3f;
            RectTransform row = Ui.Row(parent, portraitHeight + 64, 0);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            RectTransform col = Ui.Rect(row, "Node");
            Ui.VList(col, 1, 0);
            Ui.Size(col, columnWidth, -1, 0);
            col.GetComponent<LayoutElement>().minWidth = columnWidth;

            RectTransform holder = Ui.Rect(col, "Holder");
            var holderRow = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
            holderRow.childAlignment = TextAnchor.MiddleCenter;
            holderRow.childControlWidth = true;
            holderRow.childControlHeight = true;
            holderRow.childForceExpandWidth = false;
            holderRow.childForceExpandHeight = false;
            Ui.Size(holder, -1, portraitHeight + 2);

            Color frameColor = locked ? new Color(0.3f, 0.32f, 0.36f) : top ? new Color(0.88f, 0.70f, 0.28f) : new Color(0.42f, 0.5f, 0.6f);
            Image frame = Ui.Panel(holder, "Frame", frameColor);
            Ui.Size(frame, portraitWidth, portraitHeight);
            frame.GetComponent<LayoutElement>().minWidth = portraitWidth;
            var raw = FitPortrait(frame.rectTransform, portraitId, 2);
            var imgRt = raw.rectTransform;
            raw.color = locked ? new Color(0.1f, 0.1f, 0.12f) : Color.white;
            if (raw.texture == null)
            {
                raw.color=new Color(.20f,.24f,.26f);
                var head=Ui.Panel(imgRt,"PortraitPlaceholderHead",new Color(.56f,.60f,.58f));
                head.raycastTarget=false;head.rectTransform.anchorMin=new Vector2(.36f,.58f);head.rectTransform.anchorMax=new Vector2(.64f,.82f);head.rectTransform.offsetMin=head.rectTransform.offsetMax=Vector2.zero;
                var shoulders=Ui.Panel(imgRt,"PortraitPlaceholderBody",new Color(.40f,.49f,.47f));
                shoulders.raycastTarget=false;shoulders.rectTransform.anchorMin=new Vector2(.19f,.17f);shoulders.rectTransform.anchorMax=new Vector2(.81f,.56f);shoulders.rectTransform.offsetMin=shoulders.rectTransform.offsetMax=Vector2.zero;
            }
            if (onClick != null && !locked)
            {
                var b = imgRt.gameObject.AddComponent<Button>();
                b.targetGraphic = raw;
                Ui.SteadyButtonFeedback(b);
                b.onClick.AddListener(() => onClick());
                if (tooltip != null) b.Tip(tooltip);
                if (controlId != null) b.name = controlId;
            }

            Ui.Size(Ui.Label(col, $"<b>{name}</b>", 14, TextAnchor.MiddleCenter), -1, 20);
            Ui.Size(Ui.Label(col, title, 13, TextAnchor.UpperCenter, top ? Ui.Accent : Ui.Dim), -1, 32);
        }

        /// <summary>上下级之间的连线。</summary>
        private void OrgLink(Transform parent)
        {
            RectTransform row = Ui.Row(parent, 12, 0);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            Image line = Ui.Panel(row, "Link", Ui.Edge);
            Ui.Size(line, 2, 12);
        }

        // ---------- 对话框 ----------

        private sealed class TalkOpt
        {
            public string Label, Blocker, Id;
            public bool Hot, SeparateBlocker, Invitation, Mainline;
            public System.Action Act;
        }

        private sealed class TalkCtx
        {
            private string _reply;
            public Window W;
            public ScrollRect ReplyScroll;
            public string SpeakerId;
            public TalkOpt[] Replies;
            public bool AwaitingDecision;
            public string ExitLabel { get; private set; }
            public string Reply
            {
                get => _reply;
                set
                {
                    _reply = value;
                    if (ReplyScroll != null) ReplyScroll.content.anchoredPosition = Vector2.zero;
                }
            }

            public void Show(string text, params TalkOpt[] replies) =>
                SetPage(text, replies.Length > 0 ? replies : null, null);

            public void Finish(string text, string exitLabel) => SetPage(text, null, exitLabel);

            private void SetPage(string text, TalkOpt[] replies, string exitLabel)
            {
                Reply = text;
                Replies = replies;
                ExitLabel = exitLabel;
                if (W == null) return;
                W.ResetScrollOnRebuild = true;
                W.Rebuild();
            }

            public void Continue(string text, System.Action next) => Show(text,
                new TalkOpt { Id = "DialogueContinue", Label = "继续", Hot = true, Act = next });
        }

        private Window OpenDialogue(string portraitId, string name, string role, string greeting,
            System.Func<TalkCtx, List<TalkOpt>> makeOptions, string firstReply = null, System.Action onClosed = null,
            bool remote = false, System.Action<TalkCtx> start = null)
        {
            var ctx = new TalkCtx { Reply = firstReply };
            start?.Invoke(ctx);
            var ink = Ui.TextColor;
            return OpenWindow(name + "　" + role, 930, 520, w =>
            {
                ctx.W = w;
                if (makeOptions != null)
                {
                    w.LiveConversation = true;
                    w.LiveKey = () => ConversationViewKey(portraitId);
                    w.LiveStamp = w.LiveKey();
                }
                string speakerId = ctx.SpeakerId ?? portraitId;
                string speakerName = speakerId == portraitId ? name : S.CityPerson(speakerId)?.Name ?? speakerId;
                string speakerRole = speakerId == portraitId ? role : S.CityPersonRole(speakerId);

                var frame = Ui.Panel(w.Content, "DialogueReadingPanel", Ui.FlatWellEdge);
                frame.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
                var reading = Ui.Row(frame.transform, -1, 1).Fill(1);
                var identity = Ui.Panel(reading, "DialogueIdentity", Ui.PanelBg2);
                Ui.Size(identity, 184, -1, 0);
                var identityLayout = Ui.VList(identity.rectTransform, 5, 12);
                identityLayout.childAlignment = TextAnchor.MiddleCenter;
                var portrait = PortraitCard(identity.transform, speakerId, speakerName, "", 148,
                    () => OpenCityPersonProfile(speakerId, fromConversation: true), tagText: null);
                portrait.Find("Caption").gameObject.SetActive(false);
                portrait.GetComponentInChildren<Button>().Tip("查看人物资料");
                Ui.Label(identity.transform, PersonNameWithKolBadge(speakerId, speakerName), 22, TextAnchor.MiddleCenter, Ui.Accent).fontStyle = FontStyle.Bold;
                Ui.Label(identity.transform, speakerRole, 15, TextAnchor.MiddleCenter, Ui.Dim);
                Ui.Label(identity.transform, "熟悉度 " + S.Relation(speakerId), 16, TextAnchor.MiddleCenter, Ui.Good);
                if (S.IsPlayerPartner(speakerId)) Ui.Label(identity.transform, "你的伴侣", 16, TextAnchor.MiddleCenter, Ui.Accent);

                var speech = Ui.Panel(reading, "DialogueSpeech", Ui.FlatWell);
                Ui.Size(speech, -1, -1, 1);
                ctx.AwaitingDecision = false;
                var options = ctx.Replies == null && ctx.ExitLabel == null ? makeOptions?.Invoke(ctx) : null;
                var mainline = options?.Where(opt => opt.Mainline).ToArray();
                int mainlineHeight = mainline == null || mainline.Length == 0 ? 0 : mainline.Length <= 2 ? 48 : 102;
                var replyBody = Ui.Scroll(speech.transform);
                ctx.ReplyScroll = replyBody.GetComponentInParent<ScrollRect>();
                ctx.ReplyScroll.name = "DialogueTextScroll";
                ((RectTransform)ctx.ReplyScroll.transform).Anchor(0, 0, 1, 1, 18, 18 + mainlineHeight, 18, 18);
                ctx.ReplyScroll.GetComponent<Image>().color = Color.clear;
                bool nearby = remote || S.CityPersonState(portraitId) == null || S.CitySamePlace(portraitId);
                string district = options != null && ctx.Reply == null ? DistrictDialogueContext(portraitId, remote) : null;
                string currentGreeting = S.IsPlayerPartner(portraitId) ? "你来了，正好想跟你说会儿话。" : greeting;
                string text = ctx.Reply ?? (nearby ? district ?? currentGreeting : "对方已经离开了，等下次见面再聊吧。");
                var line = Para(replyBody, Ui.EmphasizeReading(text), 22, ink);
                line.name = "DialogueReplyText";
                line.lineSpacing = 1.2f;
                if (mainlineHeight > 0) BuildDialogueMainline(speech.transform, mainline, mainlineHeight);
                var actions = options == null ? ctx.Replies : options.Where(opt => !opt.Mainline).ToArray();
                string exitLabel = ctx.Replies != null || ctx.AwaitingDecision ? null : ctx.ExitLabel ?? "结束对话";
                BuildDialogueFooter(w, actions, exitLabel);
            }, true, true, onClosed, scrollContent: false, showTitleBar: false);
        }

        private static void BuildDialogueMainline(Transform speech, TalkOpt[] options, int height)
        {
            var strip = Ui.Rect(speech, "DialogueMainline").Anchor(0, 0, 1, 0, 18, 14, 18, -14 - height);
            var layout = Ui.Row(strip, -1, 10).Fill();
            Ui.Size(Ui.Label(layout, "主线", 16, color: Ui.Accent), 44);
            RectTransform actions;
            if (options.Length > 4)
            {
                actions = Ui.Scroll(layout);
                Ui.Size(actions.GetComponentInParent<ScrollRect>().GetComponent<Image>(), 0, -1, 1);
            }
            else
            {
                actions = Ui.Size(Ui.Rect(layout, "MainlineActions"), 0, -1, 1);
                Ui.VList(actions, 6);
            }
            var list = actions.GetComponent<VerticalLayoutGroup>();
            list.padding = new RectOffset();
            list.spacing = 6;
            for (int i = 0; i < options.Length; i += 2)
            {
                var row = Ui.Row(actions, 48);
                BuildTalkOption(row, options[i], 280, 48);
                if (i + 1 < options.Length) BuildTalkOption(row, options[i + 1], 280, 48);
            }
        }

        private static void BuildDialogueFooter(Window w, IReadOnlyList<TalkOpt> options, string exitLabel)
        {
            const int columns = 5;
            const int optionWidth = 140;
            const int rowHeight = 40;
            const int gap = 6;
            const int endWidth = 146;
            const int visibleRows = 2;
            const int footerHeight = visibleRows * rowHeight + (visibleRows - 1) * gap;
            int count = options?.Count ?? 0;
            int rows = (count + columns - 1) / columns;
            var footer = Ui.Size(Ui.Rect(w.Content, "DialogueFooter"), -1, footerHeight);
            if (exitLabel != null)
            {
                var end = Ui.Button(footer, exitLabel, w.Close, 16, exitLabel == "结束对话" ? Ui.Warn : Ui.SelectedButton(true));
                end.name = "DialogueEnd";
                ((RectTransform)end.transform).Anchor(1, 0, 1, 0, -endWidth, 0, 0, -rowHeight);
            }
            if (count == 0) return;

            RectTransform actions;
            RectTransform optionsArea;
            if (rows > visibleRows)
            {
                actions = Ui.Scroll(footer);
                optionsArea = (RectTransform)actions.GetComponentInParent<ScrollRect>().transform;
            }
            else
            {
                optionsArea = actions = Ui.Rect(footer, "DialogueOptions");
                Ui.VList(actions);
            }
            optionsArea.name = "DialogueOptions";
            optionsArea.Anchor(0, 0, 1, 1, 0, 0, exitLabel != null ? endWidth + gap : 0, rows == 1 ? rowHeight + gap : 0);
            var list = actions.GetComponent<VerticalLayoutGroup>();
            list.padding = new RectOffset();
            list.spacing = gap;

            for (int i = 0; i < count; i += columns)
            {
                var row = Ui.Row(actions, rowHeight, gap);
                row.name = "DialogueOptionRow";
                for (int j = i; j < Mathf.Min(i + columns, count); j++)
                    BuildTalkOption(row, options[j], optionWidth, rowHeight);
            }
        }

        private static void BuildTalkOption(Transform row, TalkOpt opt, int width, int height)
        {
            string label = opt.Invitation ? "＋ " + opt.Label : opt.Label;
            if (opt.Blocker != null && !opt.SeparateBlocker && label.Length * 16 <= width - 8 && opt.Blocker.Length * 13 <= width - 8)
                label += "\n<size=13>" + opt.Blocker + "</size>";
            Color fill = opt.Invitation && !opt.Mainline
                ? new Color32(184, 224, 216, 255)
                : Ui.SelectedButton(opt.Mainline || opt.Hot);
            var button = Ui.Size(Ui.Button(row, label, opt.Act, 16, fill, opt.Blocker == null), width, height, 0);
            var caption = button.GetComponentInChildren<Text>();
            caption.rectTransform.Fill(3);
            caption.lineSpacing = .92f;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            if (opt.Invitation) caption.fontStyle = FontStyle.Bold;
            if (opt.Id != null) button.name = opt.Id;
            if (opt.Blocker != null) button.Tip(opt.Label + "\n" + opt.Blocker);
            else if (label.Length * 16 > width - 8) button.Tip(opt.Label);
        }

        public void OpenPersonDialogue(string personId,Place place=null,Window parent=null,string firstReply=null,StaffDef staff=null,PlaceRoster roster=null,System.Action onClosed=null,int initialPage=0,bool remote=false)
        {
            var definition=S.CityPerson(personId);var npc=Npcs.All.FirstOrDefault(n=>n.Id==personId);
            staff=staff??Rosters.Get(personId)??Residents.Get(personId)?.Person;
            if(staff==null&&Npcs.Get(personId)==null&&definition!=null)staff=definition.Staff;
            if(definition==null&&npc==null&&staff==null){Toast("暂时找不到这个人物。");return;}
            if (!remote && _ctrl.InVisit && _ctrl.VisitRoom?.CanReachResident(personId) != true)
            {
                Toast("请走到住户身边再交谈。");
                return;
            }
            if(remote){string why=S.RemoteConversationBlocker(personId);if(why!=null){Toast(why);return;}}
            else if(definition!=null&&!S.CitySamePlace(personId)){OpenCityPersonProfile(personId,onClosed);return;}
            string name=definition?.Name??npc?.Name??staff.Name;
            string role=definition!=null?S.CityPersonRole(personId):npc?.Role??staff.Role;
            string greeting = S.HasGreeted(personId)
                ? npc?.Intro ?? staff?.LineFor(S.Clock.Today.TotalDays) ?? "你好，今天过得怎么样？"
                : name + "看向你，等你开口。";
            bool creatorChoice = initialPage == 3;
            bool discussingPeople = false;
            bool confirmingBreakup = false;
            var dialogue = OpenDialogue(personId, name, role + (remote ? " · 电话" : ""), greeting, ctx =>
            {
                var options = new List<TalkOpt>();
                bool near = !remote && (definition == null || S.CitySamePlace(personId));
                bool canTalk = remote || near;
                void Reply(string text) { ctx.Show(text); parent?.RequestRebuild(); }
                if (confirmingBreakup)
                {
                    ctx.AwaitingDecision = true;
                    options.Add(new TalkOpt { Label = "继续相处", Act = () =>
                    {
                        confirmingBreakup = false;
                        Reply(S.PartnerConversation(personId));
                    } });
                    options.Add(new TalkOpt { Label = "确认分手", Blocker = near ? null : "需要当面交谈", Act = () =>
                    {
                        confirmingBreakup = false;
                        if (S.IsPlayerPartner(personId) && S.EndRomance())
                            Reply("我明白了。以后见面，还是可以像朋友一样聊聊。");
                        else ctx.W.Rebuild();
                    } });
                    return options;
                }
                if (!creatorChoice && !discussingPeople)
                {
                    if (S.KolAmbassadorAccessBlocker == null && KolMarket.Get(personId) != null && S.IsMet(personId))
                        options.Add(new TalkOpt { Label = "谈品牌代言与直播", Blocker = canTalk ? null : "对方已离开",
                            Act = () => OpenKolNegotiation(personId, ctx.W, remote) });
                    if (Creators.Get(personId) != null && S.IsMet(personId) && S.HasMcn)
                        options.Add(new TalkOpt { Label = "谈MCN合作", Blocker = canTalk ? null : "对方已离开",
                            Act = () => OpenCreatorRelations(personId, ctx.W) });
                }
                AddDistrictDialogue(options,personId,remote,near,ctx,Reply);
                if(personId=="life_enterprise"||personId=="life_tenant")
                    foreach(var tender in S.BrandBusiness.Tenders.Where(t=>t.Stage<2))
                    {
                        int clue=personId=="life_enterprise"?1:2;string id=tender.Id;
                        if((tender.Intel&clue)!=0)continue;
                        options.Add(new TalkOpt{Label=(clue==1?"核实企业采购要求":"调查现有供应商")+" · ¥200",Blocker=near?null:"请找到本人，当面核实",Act=()=>Reply(S.ResearchBrandTender(id,clue)?(clue==1?"沈远：按时交付是底线，最低价不一定赢。拿试单证明你能供货。":"袁岚：原供应商一定会还价。我们可以联合履约，你也可以先把自己的供货做稳。"):"这次未能完成核实，请查看预算和截止时间。")});
                    }
                if(S.CommunitySearchActive)
                    for(int i=0;i<3;i++)
                        if(GameSession.CommunityLeadPerson(i)==personId&&!S.CommunityHasClue(i))
                        {
                            int lead=i;
                            if (S.CommunityLeadAppointment(lead) is CityActivity scheduled)
                                options.Add(new TalkOpt { Label = "查看已约活动", Mainline = true, Hot = true,
                                    Act = () => OpenCityActivityDetail(scheduled.Id) });
                            else if (!S.CommunityHasOuting(lead))
                            {
                                string activityId = GameSession.CommunityLeadActivity(lead);
                                options.Add(new TalkOpt { Label = "邀约 · " + CityLifeCatalog.Activity(activityId).Name,
                                    Mainline = true, Invitation = true, Blocker = canTalk ? S.CityActivityContactBlocker(activityId, personId) : "对方已离开",
                                    Act = () => OpenCityActivityOffer(activityId, personId) });
                            }
                            else options.Add(new TalkOpt {Label="打听旧街图页",Mainline=true,Hot=true,Blocker=canTalk?null:"对方已离开",
                                Act=()=>Reply(S.AskCommunityLead(lead,remote))});
                        }
                if (creatorChoice)
                {
                    ctx.AwaitingDecision = true;
                    foreach (bool support in new[] { true, false })
                    {
                        bool choice = support;
                        options.Add(new TalkOpt {
                            Label = choice ? "先听清顾虑，支持对方自己决定" : "优先抓住增长机会，其他问题以后再说",
                            Blocker = near ? null : "需要当面交谈",
                            Act = () => { if (S.ResolveCreatorStory(personId, choice)) { creatorChoice = false; Reply(choice ? "谢谢你愿意先听我说。" : "我会记住你的想法，也想保留自己的选择。"); } }
                        });
                    }
                    options.Add(new TalkOpt { Label = "我再想想", Act = () => { creatorChoice = false; Reply("好，等你想好了，我们再聊这件事。"); } });
                    return options;
                }
                if (discussingPeople)
                {
                    foreach (var person in S.PersonCircle(personId))
                    {
                        string targetId = person.PersonId;
                        options.Add(new TalkOpt {
                            Label = "谈谈" + person.Name,
                            Blocker = S.AskAboutOthersBlocker(personId, targetId, remote),
                            Act = () => Reply(S.AskAboutPerson(personId, targetId, remote))
                        });
                    }
                    options.Add(new TalkOpt { Label = "返回聊天", Act = () => { discussingPeople = false; ctx.W.ResetScrollOnRebuild = true; ctx.W.Rebuild(); } });
                    return options;
                }
                if (near && _ctrl.InPachinkoRoom && S.IsLeisureVenueWorker(S.CityPersonState(personId)))
                    options.Add(new TalkOpt { Label = "买卖弹珠", Blocker = S.PachinkoTradeBlocker, Act = () => OpenPachinkoExchange() });
                options.Add(new TalkOpt { Label = "聊一聊", Hot = true, Blocker = canTalk ? null : "对方已离开",
                    Id = personId == GameSession.VenueSponsorGuidePersonId ? SponsorGuideChatControl : null,
                    Act = () => StartCasualDialogue(ctx, personId, remote, Reply) });
                AddRelationshipStoryOption(options, ctx, personId, remote, Reply);
                options.Add(new TalkOpt { Label = "交流物价", Blocker = canTalk ? null : "对方已离开",
                    Act = () => Reply(S.ExchangeMarketTalk(personId, remote, place, text => Ui.Emphasize(text))) });
                options.Add(new TalkOpt { Label = "谈论别人", Blocker = S.AskAboutOthersBlocker(personId,remote:remote), Act = () =>
                {
                    discussingPeople = true;
                    ctx.W.ResetScrollOnRebuild = true;
                    ctx.W.Rebuild();
                } });
                options.Add(new TalkOpt { Label = "送礼", Blocker = !near ? "需要当面送礼" : S.PersonGiftBlocker(personId), Act = () => OpenPersonGiftGrid(personId, name, ctx) });
                if (definition != null)
                    options.Add(new TalkOpt { Label = "邀请活动", Invitation = true, Blocker = canTalk ? S.CityInviteBlocker(personId) : "对方已离开", Act = () => OpenCityActivityChooser(personId) });
                if (S.IsPlayerPartner(personId))
                {
                    options.Add(new TalkOpt { Label = "聊聊心事", Blocker = canTalk ? null : "对方已离开", Act = () => Reply(S.PartnerConversation(personId)) });
                    options.Add(new TalkOpt { Label = "谈谈分手", Blocker = near ? null : "需要当面交谈", Act = () =>
                    {
                        confirmingBreakup = true;
                        ctx.W.ResetScrollOnRebuild = true;
                        Reply("我们要分开吗？");
                    } });
                }
                else if (GameSession.IsRomanceCandidate(personId) && !GameSession.HasRelationshipStory(personId)
                    && S.Relation(personId) >= GameSession.ConfessionMinFamiliarity)
                    options.Add(new TalkOpt { Label = "表白", Hot = true,
                        Blocker = remote ? "需要当面表白" : S.ConfessionBlocker(personId),
                        Act = () => Reply(S.ConfessToPerson(personId)) });
                if (personId == "life_creator" && near && !S.HasStartedRelationshipStory(personId)
                    && S.CityStory("romance") is CityStoryProgress story && story.Stage == 2)
                {
                    var outing = S.CityActivities.FirstOrDefault(activity => activity.Id == story.ActivityId && activity.Active);
                    options.Add(new TalkOpt { Label = outing == null ? "约去河岸钓鱼" : "查看河岸邀约", Act = () =>
                    {
                        if (outing != null) { OpenCityActivityDetail(outing.Id); return; }
                        var scheduled = S.ScheduleCityStoryActivity("romance");
                        Reply(scheduled == null ? "这次没约上，我们再找个合适的时间吧。"
                            : "好，我们约在 " + CityTime(scheduled.ScheduledStart) + " 到河岸见。");
                    } });
                }
                if (npc != null && place != null)
                    options.AddRange(NpcDialogueActions(npc, place, parent, ctx).Where(o => o.Label.StartsWith("砍价") || o.Label.StartsWith("聊聊行情") || o.Label.StartsWith("交心打听")));
                var creator = S.Creator(personId);
                if (creator != null && S.CreatorStory(creator) != null)
                    options.Add(new TalkOpt { Label = "聊聊最近的心事", Blocker = remote ? "需要当面交谈" : near ? null : "对方已离开", Act = () =>
                    {
                        creatorChoice = true;
                        ctx.W.ResetScrollOnRebuild = true;
                        Reply(S.CreatorStory(creator));
                    } });
                return options;
            }, firstReply, () => { parent?.RequestRebuild(); onClosed?.Invoke(); },remote:remote);
            if (!remote && personId == GameSession.VenueSponsorGuidePersonId && S.VenueSponsorGuidePaused)
                _venueSponsorGuideDialogue = dialogue;
        }

        private string ConversationViewKey(string id) => S.Clock.Today.TotalDays + "|" + S.CitySamePlace(id) + "|" + S.Relation(id)
            + "|" + S.HasGreeted(id) + "|" + S.IsMet(id) + "|" + S.RomanceId + "|" + S.CarriedKg.ToString("R")
            + "|" + S.DistrictAccessTier;

        private void StartCasualDialogue(TalkCtx ctx, string personId, bool remote, System.Action<string> reply)
        {
            var session = S;
            bool acquainted = session.HasGreeted(personId);
            string greeting = session.CasualConversation(personId, remote, out var followUp);
            const string photoShopStory = "conversation:landlady-photo-shop";
            if (personId == "landlady" && acquainted && !session.Flags.Contains(photoShopStory))
            {
                void Finish(string line)
                {
                    session.Flags.Add(photoShopStory);
                    reply(line);
                }
                ctx.Show("今天路过旧居街，才发现那家老照相馆换招牌了。以前的老板还在门口认出我了。",
                    new TalkOpt { Label = "你以前常去吗？", Act = () =>
                        ctx.Continue("你搬来之前，我每年都在那里拍全家福。老板知道我爸坐不久，总把椅子先留给他。", () =>
                            Finish("后来人凑不齐，照片也就没再拍了。今天看到他拿着老相机，倒想把家里那几张翻出来看看。")) },
                    new TalkOpt { Label = "现在开了家什么店？", Act = () =>
                        Finish("还是照相馆。现在是他女儿看店，顺便帮人把老照片扫进手机里。我的那几张，改日也拿过去试试。") });
                return;
            }
            if (followUp != null)
                ctx.Continue(greeting, () => reply(followUp() ?? "这事等下次有空再聊。"));
            else
                reply(greeting);
        }

        private void OpenPersonGiftGrid(string personId, string name, TalkCtx ctx)
        {
            string selected = null;
            OpenWindow("送礼给" + name, 760, 650, w =>
            {
                w.LiveConversation = true;
                w.LiveKey = () => ConversationViewKey(personId); w.LiveStamp = w.LiveKey();
                Para(w.Content, "从背包点选一件礼物，再确认赠送。每次送出一件；大米每次送出 0.5 千克。", 17, Ui.Dim);
                var stacks = CollectBag();
                foreach (var stack in stacks)
                    if (S.PersonGiftItemBlocker(personId, stack.Key) != null) stack.Color = new Color(.23f, .25f, .26f);
                BagGrid(w.Content, stacks, Mathf.CeilToInt((float)S.CurrentVehicle.CargoCapacity), selected, key => { selected = key; w.Rebuild(); }, 360);
                var item = stacks.FirstOrDefault(stack => stack.Key == selected);
                string why = S.PersonGiftItemBlocker(personId, selected);
                Para(w.Content, item == null ? "点选背包中的一格。礼品、鲜花和未过期的食物可以赠送。" : item.Name + " ×" + item.Count + (why == null ? "\n送出一件" : "\n" + why), 18, why == null ? Ui.Good : Ui.Dim);
                Ui.Size(Ui.Button(w.Content, item == null ? "先选一件礼物" : "送出「" + item.Name + "」", () =>
                {
                    if (!S.GivePersonGift(personId, selected)) { w.Rebuild(); return; }
                    ctx.Reply = name + "收下了「" + item.Name + "」：“谢谢你想着我。”";
                    w.Close(); ctx.W.RequestRebuild();
                }, 19, Ui.ButtonHot, why == null), -1, 44);
            }, false);
        }

        /// <summary>打开这家店接待的人的对话框（没有人则什么也不做）。</summary>
        public void OpenGreeterDialogue(Place p, Window parent = null)
        {
            PlaceRoster r = Rosters.For(p);
            if (r != null) OpenStaffDialogue(r.Greeter, r, p, parent);
        }

        /// <summary>和店员（或直接接待的小店老板）说话。</summary>
        private void OpenStaffDialogue(StaffDef staff, PlaceRoster roster, Place place, Window parent)
        {
            if(staff!=null)OpenPersonDialogue(staff.Id,place,parent,null,staff,roster);
        }
        private List<TalkOpt> StaffDialogueActions(StaffDef staff,PlaceRoster roster,Place place,Window parent,TalkCtx ctx)
        {
            bool asksBoss=roster!=null&&roster.BossNpcId!=null&&staff==roster.Greeter;
                var list = new List<TalkOpt>();
                list.Add(new TalkOpt
                {
                    Label = "随便聊聊",
                    Blocker = S.StaffChatBlocker(staff.Id),
                    Act = () =>
                    {
                        string r = S.StaffChat(staff);
                        if (r != null) ctx.Reply = r;
                        ctx.W.Rebuild();
                    },
                });
                list.Add(new TalkOpt
                {
                    Label = "送他点东西（从背包里挑）",
                    Blocker = S.StaffGiftBlocker(staff.Id),
                    Hot = true,
                    Act = () => OpenStaffGiftPicker(staff, ctx),
                });
                list.Add(new TalkOpt
                {
                    Label = "听他讲讲自己的事",
                    Blocker = S.StaffStoryBlocker(staff.Id),
                    Act = () =>
                    {
                        string r = S.StaffStory(staff);
                        if (r != null) ctx.Reply = r;
                        ctx.W.Rebuild();
                    },
                });
                if (asksBoss)
                {
                    list.Add(new TalkOpt
                    {
                        Label = "打听老板的事",
                        Blocker = S.StaffBossBlocker(roster),
                        Act = () =>
                        {
                            string r = S.AskStaffAboutBoss(roster);
                            if (r != null) ctx.Reply = r;
                            ctx.W.Rebuild();
                            parent?.Rebuild();
                        },
                    });
                }
                list.Add(new TalkOpt { Label = place?.Kind == PlaceKind.Agency ? "继续看房" : "看看货", Hot = true, Act = () => ctx.W.Close() });
                return list;
        }

        /// <summary>从背包里挑一份礼品送给店员（一瓶饮料就够意思，贵的东西涨得多）。</summary>
        private void OpenStaffGiftPicker(StaffDef staff, TalkCtx ctx) => OpenPersonGiftGrid(staff.Id, staff.Name, ctx);

        /// <summary>和某个有交情的人交谈（档口老板、唐俊杰、熟人）。stall 不为空表示正在这个档口里，可以砍价；parent 是背后的窗口，操作后要刷新。</summary>
        public void OpenNpcWindow(NpcDef n, Place stall = null, Window parent = null, string firstReply = null)
        {
            if(n!=null)OpenPersonDialogue(n.Id,stall,parent,firstReply);
        }
        private List<TalkOpt> NpcDialogueActions(NpcDef n,Place stall,Window parent,TalkCtx ctx)
        {
                var o = new List<TalkOpt>();
                o.Add(new TalkOpt
                {
                    Label = "随便闲聊",
                    Blocker = S.SmallTalkBlocker(n.Id),
                    Act = () =>
                    {
                        string r = S.SmallTalk(n.Id);
                        if (r != null) ctx.Reply = r;
                        ctx.W.Rebuild();
                        parent?.Rebuild();
                    },
                });
                o.Add(new TalkOpt
                {
                    Label = "送礼",
                    Blocker = S.PersonGiftBlocker(n.Id),
                    Hot = true,
                    Act = () =>
                    {
                        OpenPersonGiftGrid(n.Id, n.Name, ctx);
                    },
                });
                if (n.IsTipster)
                {
                    o.Add(new TalkOpt
                    {
                        Label = "聊聊行情（递一包烟）",
                        Blocker = S.TipBlocker(n.Id),
                        Act = () =>
                        {
                            string reply=S.AskForTipReply(n.Id);
                            if(reply!=null)ctx.Reply=reply;
                            ctx.W.Rebuild();
                        },
                    });
                }
                o.Add(new TalkOpt
                {
                    Label = "聊聊人生（听他讲讲过去）",
                    Blocker = S.LifeBlocker(n.Id),
                    Act = () =>
                    {
                        string r = S.LifeTalk(n.Id);
                        if (r != null) ctx.Reply = r;
                        ctx.W.Rebuild();
                    },
                });
                if (n.IsTipster)
                {
                    o.Add(new TalkOpt
                    {
                        Label = "交心打听内幕（一定是真的）",
                        Blocker = S.InsiderBlocker(n.Id),
                        Hot = true,
                        Act = () =>
                        {
                            string r = S.AskInsider(n.Id);
                            if (r != null) ctx.Reply = r;
                            ctx.W.Rebuild();
                        },
                    });
                }
                o.Add(new TalkOpt { Label = "邀请活动", Invitation = true, Blocker = S.CityInviteBlocker(n.Id), Act = () => OpenInvitePanel(n.Id) });
                if (stall != null)
                {
                    bool open = S.IsWholesalerOpen;
                    bool tried = S.HasBargainedToday(stall);
                    o.Add(new TalkOpt
                    {
                        Label = $"砍价（看魅力 {S.Player.Charm}）",
                        Blocker = !open ? "没开门" : tried ? "今天砍过价了" : null,
                        Act = () =>
                        {
                            BargainResult r = S.Bargain(stall);
                            if (r == BargainResult.Success) ctx.Reply = n.Name + "：“行行行，今天给你便宜几个点。”";
                            else if (r == BargainResult.Failed) ctx.Reply = n.Name + "：“爱买不买。”（今天还涨了 1%）";
                            ctx.W.Rebuild();
                            parent?.Rebuild();
                        },
                    });
                }
                return o;
        }
    }
}
