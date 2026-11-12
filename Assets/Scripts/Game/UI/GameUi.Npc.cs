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

        /// <summary>按稳定人物ID加载复古头像；未知动态人物使用同画风备用图。</summary>
        public static Texture2D PortraitOf(string npcId)
        {
            npcId = string.IsNullOrWhiteSpace(npcId) ? "default" : npcId;
            if (!PortraitCache.TryGetValue(npcId, out Texture2D tex) || tex == null)
            {
                tex = Resources.Load<Texture2D>("PortraitsRetro/" + npcId)
                    ?? Resources.Load<Texture2D>("Portraits/" + npcId)
                    ?? Resources.Load<Texture2D>("PortraitsRetro/default");
                PortraitCache[npcId] = tex;
            }
            return tex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void InvalidatePortraitCache() => PortraitCache.Clear();

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
                ColorBlock cb = b.colors;
                cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                b.colors = cb;
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
        /// 有人接待的店（档口、手机店、家具城、烟酒茶行、农贸市场、车行）：左边是“店内人员图”，
        /// 像宗门图一样自上而下——老板在最上面（金框），下面一级是店员（灰框），上下之间有连线；头像很小，人多了也排得下。
        /// 老板交情够了才见得到，没到时是灰色“？？？”，原因写在图下面。右边是原来的商品内容。
        /// 返回放商品内容的容器；没有人返回 null。
        /// </summary>
        private RectTransform BuildRosterFrame(Window w, Place p, float bodyHeight, bool scrollGoods = false, float rosterWidth = 210)
        {
            var locations = p.Kind == PlaceKind.Wholesaler ? S.WholesaleStallsAt(p) : new[] { p };
            var present = locations.SelectMany(place => S.PeopleInPlace(place.Id))
                .Where(person => S.CityPerson(person.PersonId) != null).ToArray();

            var body=Ui.Row(w.Content,bodyHeight,12);
            var people=Ui.Rect(body,"People");Ui.VList(people,8,0);
            // Override the vertical group's flexible width: the roster must not share
            // spare horizontal space with the goods column, especially with a ScrollRect.
            var peopleSize=Ui.Size(people,rosterWidth,-1,0).GetComponent<LayoutElement>();
            peopleSize.minWidth=rosterWidth;
            Ui.Size(Ui.Label(people,"店内在场人物",17),-1,30);
            var portraits = Ui.Scroll(people, Mathf.Max(140, bodyHeight - 40));
            if(present.Length==0)Para(portraits,"此刻没有可交谈的店员在场。",16,Ui.Dim);
            foreach(var state in present)
            {
                string id=state.PersonId;var def=S.CityPerson(id);
                OrgNode(portraits, id, def.Name, S.CityPersonRole(id), state.WorkPlaceId == state.LocationPlaceId, false, () => OpenCityPerson(id));
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

        private const float OrgCardWidth = 44;

        /// <summary>人员图里的一个人：小头像卡（老板金框、店员灰框），下面是名字和职务；locked 时是暗色剪影。</summary>
        private void OrgNode(Transform parent, string portraitId, string name, string title, bool top, bool locked, System.Action onClick)
        {
            RectTransform row = Ui.Row(parent, 104, 0);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            OrgNodeIn(row, portraitId, name, title, top, locked, onClick);
        }

        private void OrgNodeIn(Transform row, string portraitId, string name, string title, bool top, bool locked, System.Action onClick)
        {
            RectTransform col = Ui.Rect(row, "Node");
            Ui.VList(col, 1, 0);
            Ui.Size(col, 72, -1, 0);
            col.GetComponent<LayoutElement>().minWidth = 72;

            RectTransform holder = Ui.Rect(col, "Holder");
            var holderRow = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
            holderRow.childAlignment = TextAnchor.MiddleCenter;
            holderRow.childControlWidth = true;
            holderRow.childControlHeight = true;
            holderRow.childForceExpandWidth = false;
            holderRow.childForceExpandHeight = false;
            Ui.Size(holder, -1, OrgCardWidth * 4f / 3f + 2);

            Color frameColor = locked ? new Color(0.3f, 0.32f, 0.36f) : top ? new Color(0.88f, 0.70f, 0.28f) : new Color(0.42f, 0.5f, 0.6f);
            Image frame = Ui.Panel(holder, "Frame", frameColor);
            Ui.Size(frame, OrgCardWidth, OrgCardWidth * 4f / 3f);
            frame.GetComponent<LayoutElement>().minWidth = OrgCardWidth;
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
                ColorBlock cb = b.colors;
                cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                b.colors = cb;
                b.onClick.AddListener(() => onClick());
            }

            Ui.Size(Ui.Label(col, $"<b>{name}</b>", 14, TextAnchor.MiddleCenter), -1, 20);
            Ui.Size(Ui.Label(col, title, 13, TextAnchor.MiddleCenter, top ? Ui.Accent : Ui.Dim), -1, 17);
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
            public string Label, Blocker;
            public bool Hot, SeparateBlocker;
            public System.Action Act;
        }

        private sealed class TalkCtx
        {
            private string _reply;
            public Window W;
            public ScrollRect ReplyScroll;
            public string Reply
            {
                get => _reply;
                set
                {
                    _reply = value;
                    if (ReplyScroll != null) ReplyScroll.content.anchoredPosition = Vector2.zero;
                }
            }
        }

        private void OpenDialogue(string portraitId, string name, string role, string greeting,
            System.Func<TalkCtx, List<TalkOpt>> makeOptions, string firstReply = null, System.Action onClosed = null,
            bool remote = false, bool replyOnly = false, string replyConfirm = "知道了")
        {
            var ctx = new TalkCtx { Reply = firstReply };
            var paper = new Color32(242, 230, 205, 255);
            OpenWindow(name + "　" + role, replyOnly ? 700 : 930, replyOnly ? 660 : 520, w =>
            {
                ctx.W = w;
                w.LiveConversation = true;
                w.LiveKey = () => ConversationViewKey(portraitId);
                w.LiveStamp = w.LiveKey();

                var frame = Ui.Panel(w.Content, "DialogueReadingPanel", new Color32(166, 134, 80, 255));
                frame.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
                var reading = Ui.Row(frame.transform, -1, 1).Fill(1);
                var identity = Ui.Panel(reading, "DialogueIdentity", Ui.PanelBg2);
                Ui.Size(identity, replyOnly ? 158 : 184, -1, 0);
                var identityLayout = Ui.VList(identity.rectTransform, 5, 12);
                identityLayout.childAlignment = TextAnchor.MiddleCenter;
                var portrait = PortraitCard(identity.transform, portraitId, name, "", replyOnly ? 120 : 148,
                    () => OpenCityPersonProfile(portraitId, fromConversation: true), tagText: null);
                portrait.Find("Caption").gameObject.SetActive(false);
                portrait.GetComponentInChildren<Button>().Tip("查看人物资料");
                Ui.Label(identity.transform, name, 22, TextAnchor.MiddleCenter, Ui.Accent).fontStyle = FontStyle.Bold;
                Ui.Label(identity.transform, role, 15, TextAnchor.MiddleCenter, Ui.Dim);
                if (!replyOnly)
                {
                    Ui.Label(identity.transform, "熟悉度 " + S.Relation(portraitId), 16, TextAnchor.MiddleCenter, Ui.Good);
                    if (S.IsPlayerPartner(portraitId)) Ui.Label(identity.transform, "你的伴侣", 16, TextAnchor.MiddleCenter, Ui.Accent);
                }

                var speech = Ui.Panel(reading, "DialogueSpeech", paper);
                Ui.Size(speech, -1, -1, 1);
                var replyBody = Ui.Scroll(speech.transform);
                ctx.ReplyScroll = replyBody.GetComponentInParent<ScrollRect>();
                ctx.ReplyScroll.name = "DialogueTextScroll";
                ((RectTransform)ctx.ReplyScroll.transform).Fill(18);
                ctx.ReplyScroll.GetComponent<Image>().color = Color.clear;
                bool nearby = remote || S.CityPersonState(portraitId) == null || S.CitySamePlace(portraitId);
                string district = replyOnly ? null : DistrictDialogueContext(portraitId);
                string currentGreeting = S.IsPlayerPartner(portraitId) ? "你来了，正好想跟你说会儿话。" : greeting;
                string text = ctx.Reply ?? (nearby ? district ?? currentGreeting : "对方已经离开了，等下次见面再聊吧。");
                var line = Para(replyBody, text, 22, Ui.PanelBg);
                line.name = "DialogueReplyText";
                line.lineSpacing = 1.2f;
                if (ctx.Reply == null && nearby && district != null &&
                    S.DistrictStoryState.MeetingNodeId == S.CurrentDistrictNode?.Id)
                    Para(replyBody, S.DistrictMeetingDescription, 17, Ui.PanelBg);

                if (replyOnly)
                    Ui.Size(Ui.Button(w.Content, replyConfirm, w.Close, 18, Ui.ButtonHot), -1, 42);
                else
                    BuildDialogueOptions(w, makeOptions(ctx));
            }, true, true, onClosed, scrollContent: false, showTitleBar: replyOnly);
        }

        private static void BuildDialogueOptions(Window w, List<TalkOpt> options)
        {
            const int columns = 6;
            const int rowHeight = 40;
            const int gap = 6;
            while (options.Count < columns - 1) options.Add(null);
            options.Insert(columns - 1, new TalkOpt { Label = "结束对话", Act = w.Close });
            int rows = (options.Count + columns - 1) / columns;
            int visibleRows = Mathf.Min(rows, 2);
            int height = visibleRows * rowHeight + (visibleRows - 1) * gap;
            RectTransform actions;
            if (rows > visibleRows)
            {
                actions = Ui.Scroll(w.Content, height);
                var actionsScroll = actions.GetComponentInParent<ScrollRect>();
                actionsScroll.name = "DialogueOptions";
                actionsScroll.GetComponent<Image>().color = Color.clear;
            }
            else
            {
                actions = Ui.Size(Ui.Rect(w.Content, "DialogueOptions"), -1, height);
                Ui.VList(actions);
            }
            var list = actions.GetComponent<VerticalLayoutGroup>();
            list.padding = new RectOffset();
            list.spacing = gap;

            for (int i = 0; i < options.Count; i += columns)
            {
                var row = Ui.Row(actions, rowHeight, gap);
                row.name = "DialogueOptionRow";
                for (int j = 0; j < columns; j++)
                {
                    var opt = i + j < options.Count ? options[i + j] : null;
                    if (opt == null)
                    {
                        Ui.Size(Ui.Rect(row, "Empty"), 0, -1, 1);
                        continue;
                    }
                    string label = opt.Blocker == null || opt.SeparateBlocker || opt.Blocker.Length > 20
                        ? opt.Label : opt.Label.Split('（')[0] + "\n<size=13>" + opt.Blocker + "</size>";
                    bool close = opt.Label == "结束对话";
                    var button = Ui.Size(Ui.Button(row, label, opt.Act, 16,
                        close ? new Color32(73, 49, 51, 255) : opt.Hot ? Ui.ButtonHot : Ui.ButtonBg, opt.Blocker == null), 0, -1, 1);
                    var caption = button.GetComponentInChildren<Text>();
                    caption.rectTransform.Fill(4);
                    caption.resizeTextForBestFit = true;
                    caption.resizeTextMinSize = 11;
                    caption.resizeTextMaxSize = 16;
                    if (close) button.name = "DialogueEnd";
                    if (opt.Blocker != null) button.Tip(opt.Blocker);
                }
            }
        }

        public void OpenPersonDialogue(string personId,Place place=null,Window parent=null,string firstReply=null,StaffDef staff=null,PlaceRoster roster=null,System.Action onClosed=null,int initialPage=0,bool remote=false,bool phoneCall=false)
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
            if(remote){string why=S.RemoteConversationBlocker(personId,phoneCall);if(why!=null){Toast(why);return;}}
            else if(definition!=null&&!S.CitySamePlace(personId)){OpenCityPersonProfile(personId,onClosed);return;}
            string name=definition?.Name??npc?.Name??staff.Name;
            string role=definition!=null?S.CityPersonRole(personId):npc?.Role??staff.Role;
            string greeting = S.HasGreeted(personId)
                ? npc?.Intro ?? staff?.LineFor(S.Clock.Today.TotalDays) ?? "你好，今天过得怎么样？"
                : name + "看向你，等你开口。";
            if(!remote&&S.AtCurrentDistrictMeeting&&S.DistrictMeetingBlocker==null
                &&S.DistrictStoryState.MeetingPeople.Contains(personId)
                &&(S.DistrictMeetingContact==null||personId==S.DistrictMeetingContact))greeting=S.CurrentDistrictNode.Dialogue;
            bool creatorChoice = initialPage == 3;
            bool discussingPeople = false;
            bool confirmingBreakup = false;
            OpenDialogue(personId, name, role+(phoneCall?" · 电话":remote?" · 手机聊天":""), greeting, ctx =>
            {
                var options = new List<TalkOpt>();
                bool near = !remote && (definition == null || S.CitySamePlace(personId));
                bool canTalk = remote || near;
                void Reply(string text) { ctx.Reply = text; ctx.W.Rebuild(); parent?.RequestRebuild(); }
                if (confirmingBreakup)
                {
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
                    if (KolMarket.Get(personId) != null && S.IsMet(personId))
                        options.Add(new TalkOpt { Label = "谈品牌代言与直播", Blocker = canTalk ? null : "对方已离开",
                            Act = () => OpenKolNegotiation(personId, ctx.W, remote, phoneCall) });
                    if (Creators.Get(personId) != null && S.IsMet(personId) && S.HasMcn)
                        options.Add(new TalkOpt { Label = "谈MCN合作", Blocker = canTalk ? null : "对方已离开",
                            Act = () => OpenCreatorRelations(personId, ctx.W) });
                }
                if (phoneCall && !S.HasChatApp)
                {
                    options.Add(new TalkOpt { Label = "电话邀约", Act = () => OpenCityActivityChooser(personId) });
                    return options;
                }
                AddDistrictDialogue(options,personId,remote,near,Reply);
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
                            options.Add(new TalkOpt {Label="打听你保管的旧街图页",Hot=true,Blocker=near?null:"需要走到本人身边，当面打听",
                                Act=()=>Reply(S.AskCommunityLead(lead,remote))});
                        }
                if (creatorChoice)
                {
                    foreach (bool support in new[] { true, false })
                    {
                        bool choice = support;
                        options.Add(new TalkOpt {
                            Label = choice ? "先听清顾虑，支持对方自己决定" : "优先抓住增长机会，其他问题以后再说",
                            Blocker = near ? null : "需要当面交谈",
                            Act = () => { if (S.ResolveCreatorStory(personId, choice)) { creatorChoice = false; Reply(choice ? "谢谢你愿意先听我说。" : "我会记住你的想法，也想保留自己的选择。"); } }
                        });
                    }
                    options.Add(new TalkOpt { Label = "返回聊天", Act = () => { creatorChoice = false; ctx.W.ResetScrollOnRebuild = true; ctx.W.Rebuild(); } });
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
                options.Add(new TalkOpt { Label = "聊聊", Hot = true, Blocker = canTalk ? null : "对方已离开", Act = () => Reply(S.CasualConversation(personId,remote)) });
                if (definition?.Stories != null && definition.Stories.Length > 0)
                    options.Add(new TalkOpt { Label = "听听你的经历", Blocker = canTalk ? null : "对方已离开",
                        Act = () => Reply(remote ? S.CityHearStory(personId, true) : S.HearCityPersonalStory(personId)) });
                options.Add(new TalkOpt { Label = "谈论别人", Blocker = S.AskAboutOthersBlocker(personId,remote:remote), Act = () =>
                {
                    discussingPeople = true;
                    ctx.W.ResetScrollOnRebuild = true;
                    ctx.W.Rebuild();
                } });
                if (!S.IsMet(personId)) options.Add(new TalkOpt {
                    Label = "要个联系方式", Blocker = !near ? "需要当面交换" : S.ContactBlocker(personId),
                    Act = () => Reply(S.AskContact(personId) ? name + "：“好，咱们留个联系方式，以后可以联系。”\n已存入通讯录。" : S.ContactBlocker(personId) ?? "暂时没有交换成功。")
                });
                options.Add(new TalkOpt { Label = "送礼", Blocker = !near ? "需要当面送礼" : S.PersonGiftBlocker(personId), Act = () => OpenPersonGiftGrid(personId, name, ctx) });
                if (S.IsMet(personId) && definition != null)
                    options.Add(new TalkOpt { Label = "邀请活动", Blocker = canTalk ? null : "对方已离开", Act = () => OpenCityActivityChooser(personId) });
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
                else if (GameSession.IsRomanceCandidate(personId) && S.Relation(personId) >= GameSession.ConfessionMinFamiliarity)
                    options.Add(new TalkOpt { Label = "表白", Hot = true,
                        Blocker = remote ? "需要当面表白" : S.ConfessionBlocker(personId),
                        Act = () => Reply(S.ConfessToPerson(personId)) });
                if (personId == "life_creator" && near && S.CityStory("romance") is CityStoryProgress story && story.Stage == 2)
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
        }

        private string ConversationViewKey(string id) => S.Clock.Today.TotalDays + "|" + S.CitySamePlace(id) + "|" + S.Relation(id)
            + "|" + S.HasGreeted(id) + "|" + S.IsMet(id) + "|" + S.RomanceId + "|" + S.CarriedKg.ToString("R");

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
                if (!S.IsMet(n.Id))
                {
                    // 联系方式得当面要，不是进店买了东西就自动有
                    o.Add(new TalkOpt
                    {
                        Label = "要个联系方式（存进通讯录）",
                        Blocker = S.ContactBlocker(n.Id),
                        Hot = true,
                        Act = () =>
                        {
                            if (S.AskContact(n.Id)) ctx.Reply = n.Name + "：“行，存一下，以后有事打电话。”\n" + (n.FirstMeet ?? n.Intro);
                            ctx.W.Rebuild();
                            parent?.Rebuild();
                        },
                    });
                }
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
                o.Add(new TalkOpt { Label = "发出邀约（约出去玩）", Act = () => OpenInviteWindow(n, ctx.W) });
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
