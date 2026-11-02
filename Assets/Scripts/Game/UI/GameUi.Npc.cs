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

        /// <summary>人物立绘（Resources/Portraits/{id}）；没有则返回 null。</summary>
        public static Texture2D PortraitOf(string npcId)
        {
            if (!PortraitCache.TryGetValue(npcId, out Texture2D tex) || tex == null)
            {
                tex = Resources.Load<Texture2D>("Portraits/" + npcId);
                PortraitCache[npcId] = tex;
            }
            return tex;
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

            RectTransform imgRt = Ui.Rect(frame.transform, "Image").Fill(3);
            var raw = imgRt.gameObject.AddComponent<RawImage>();
            raw.texture = PortraitOf(portraitId);
            if(raw.texture!=null)
            {
                var aspect=imgRt.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio=(float)raw.texture.width/raw.texture.height;
            }
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
                Text t = Ui.Label(tag.transform, tagText, 14, TextAnchor.MiddleCenter);
                t.raycastTarget = false;
                t.rectTransform.Fill(1);
            }
            return frame.rectTransform;
        }

        /// <summary>小头像（截取立绘上半部分的脸）。</summary>
        private void PortraitThumb(Transform parent, NpcDef n, float size, System.Action onClick = null)
        {
            Image frame = Ui.Panel(parent, "Thumb", Ui.PanelBg2);
            Ui.Size(frame, size, size);
            frame.GetComponent<LayoutElement>().minWidth = size;
            RectTransform imgRt = Ui.Rect(frame.transform, "Image").Fill(2);
            var raw = imgRt.gameObject.AddComponent<RawImage>();
            raw.texture = PortraitOf(n.Id);
            raw.uvRect = new Rect(0.12f, 0.48f, 0.76f, 0.5f);
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
        private RectTransform BuildRosterFrame(Window w, Place p, float bodyHeight)
        {
            var present=S.PeopleInPlace(p.Id).Where(person=>S.CityPerson(person.PersonId)!=null).ToArray();

            var body=Ui.Row(w.Content,bodyHeight,12);
            var people=Ui.Rect(body,"People");Ui.VList(people,8,0);Ui.Size(people,210,-1);
            Ui.Size(Ui.Label(people,"店内在场人物",17),-1,30);
            var portraits = Ui.Scroll(people, Mathf.Max(140, bodyHeight - 40));
            if(present.Length==0)Para(portraits,"此刻没有可交谈的店员在场。",16,Ui.Dim);
            foreach(var state in present)
            {
                string id=state.PersonId;var def=S.CityPerson(id);
                OrgNode(portraits, id, def.Name, S.CityPersonRole(id), state.WorkPlaceId == p.Id, false, () => OpenCityPerson(id));
            }
            var center=Ui.Rect(body,"Goods");Ui.VList(center,6,0);Ui.Size(center,-1,-1,1);return center;
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
            RectTransform imgRt = Ui.Rect(frame.transform, "Image").Fill(2);
            var raw = imgRt.gameObject.AddComponent<RawImage>();
            raw.texture = PortraitOf(portraitId);
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
            Ui.Size(Ui.Label(col, $"<size=11><color={(top ? "#E3B347" : "#ABB5A8")}>{title}</color></size>", 11, TextAnchor.MiddleCenter), -1, 14);
        }

        /// <summary>上下级之间的连线。</summary>
        private void OrgLink(Transform parent)
        {
            RectTransform row = Ui.Row(parent, 12, 0);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            Image line = Ui.Panel(row, "Link", new Color(0.55f, 0.62f, 0.72f));
            Ui.Size(line, 2, 12);
        }

        // ---------- 对话框 ----------

        private sealed class TalkOpt
        {
            public string Label, Blocker;
            public bool Hot;
            public System.Action Act;
        }

        private sealed class TalkCtx
        {
            public string Reply;
            public Window W;
        }

        /// <summary>
        /// 对话框：上面左边是人物头像、右边是他说的话；下面一列居中的选项，点一个选项他就回一句；不够条件的选项灰着写明原因。
        /// </summary>
        private void OpenDialogue(string portraitId, string name, string role, NpcDef npc, string greeting, int optionCount,
            System.Func<TalkCtx, List<TalkOpt>> makeOptions, string firstReply = null, string rapportId = null, System.Action onClosed = null, bool remote = false)
        {
            var ctx = new TalkCtx { Reply = firstReply };
            float height = 485 + 46 * optionCount + (npc != null ? 66 : rapportId != null ? 36 : 0);
            // Conversation owns a normal modal hold, including while its profile or gift UI is open.
            // Keep existing panel holds intact so closing chat restores the previous time state.
            OpenWindow(name + "　" + role, 700, height, w =>
            {
                ctx.W = w;
                w.LiveConversation = true;
                w.LiveKey = () => ConversationViewKey(portraitId); w.LiveStamp = w.LiveKey();
                RectTransform top = Ui.Row(w.Content, 245, 14);
                var identity = Ui.Rect(top, "PersonIdentity");
                Ui.VList(identity, 4, 0); Ui.Size(identity, 170, -1);
                PortraitCard(identity, portraitId, name, "", 150, () => OpenCityPersonProfile(portraitId, fromConversation: true), tagText: "资料");
                var traits = Ui.Row(identity, 30, 3);
                var knownTraits = S.ConversationTraits(portraitId);
                for (int i = 0; i < knownTraits.Length; i++)
                {
                    int index = i; var trait = knownTraits[i];
                    Ui.Size(Ui.Button(traits, trait.Known ? trait.Name : "???", () =>
                        OpenWindow(trait.Known ? trait.Name : "尚未了解", 440, 230, detail =>
                            Para(detail.Content, S.ConversationTraitText(portraitId, index), 18), false), 13,
                        trait.Known ? Ui.ButtonBg : new Color(.18f, .21f, .23f)), -1, 30, 1);
                }
                RectTransform say = Ui.Rect(top, "Say");
                Ui.VList(say, 6, 0);
                Ui.Size(say, -1, -1, 1);
                float bubbleHeight = 200;
                if (npc != null)
                {
                    int rel = S.Relation(npc.Id);
                    RectTransform relRow = Ui.Row(say, 26);
                    Ui.Size(Ui.Label(relRow, $"{Npcs.LevelName(rel)} {rel}", 17, TextAnchor.MiddleLeft, Ui.Good), 110);
                    Ui.Bar(relRow, rel / 100f, Ui.Accent, 200, 14);
                    bubbleHeight -= 32;
                }
                if (npc == null && rapportId != null)
                {
                    int rap = S.Relation(rapportId);
                    RectTransform relRow = Ui.Row(say, 26);
                    Ui.Size(Ui.Label(relRow, $"{StaffTalk.LevelName(rap)} {rap}", 17, TextAnchor.MiddleLeft, Ui.Good), 110);
                    Ui.Bar(relRow, rap / 100f, Ui.Accent, 200, 14);
                    bubbleHeight -= 32;
                }
                Image bubble = Ui.Panel(say, "Bubble", new Color(.89f,.87f,.82f,1f));
                Ui.Size(bubble, -1, bubbleHeight);
                bool nearby = remote || S.CityPersonState(portraitId) == null || S.CitySamePlace(portraitId);
                string text = nearby ? ctx.Reply ?? "“" + greeting.Trim('"') + "”" : "对方已经离开了，等下次见面再聊吧。";
                Text sayText = Ui.Label(bubble.transform, text, 19, TextAnchor.UpperLeft, Ui.PanelBg);
                sayText.rectTransform.Fill(12);

                Ui.Size(Ui.Label(w.Content, "你想……", 15, TextAnchor.MiddleCenter, Ui.Dim), -1, 26);

                foreach (TalkOpt o in makeOptions(ctx))
                {
                    TalkOpt opt = o;
                    string label = opt.Blocker == null ? opt.Label : opt.Label.Split('（')[0] + "（" + opt.Blocker + "）";
                    RectTransform row = Ui.Row(w.Content, 42, 0);
                    Ui.Size(Ui.Rect(row, "L"), -1, -1, 1);
                    Ui.Size(Ui.Button(row, label, opt.Act, 18, opt.Hot ? Ui.ButtonHot : Ui.ButtonBg, opt.Blocker == null), 460);
                    Ui.Size(Ui.Rect(row, "R"), -1, -1, 1);
                }

                if (npc != null && !string.IsNullOrEmpty(NpcPerks.Describe(npc.Id)))
                {
                    string perk = "<color=#ABB5A8>交情好处：" + NpcPerks.Describe(npc.Id) + (npc.IsTipster ? "\n" + TrustText(npc) : "") + "</color>";
                    Ui.Size(Ui.Label(w.Content, "<size=14>" + perk + "</size>", 14, TextAnchor.UpperLeft), -1, 58);
                }

                Ui.Size(Ui.Button(w.Content, "结束对话", w.Close, 20), -1, 46);
            }, true, true, onClosed);
        }

        public void OpenPersonDialogue(string personId,Place place=null,Window parent=null,string firstReply=null,StaffDef staff=null,PlaceRoster roster=null,System.Action onClosed=null,int initialPage=0,bool remote=false)
        {
            var definition=S.CityPerson(personId);var npc=Npcs.All.FirstOrDefault(n=>n.Id==personId);
            staff=staff??Rosters.Get(personId)??Residents.Get(personId)?.Person;
            if(staff==null&&Npcs.Get(personId)==null&&definition!=null)staff=definition.Staff;
            if(definition==null&&npc==null&&staff==null){Toast("暂时找不到这个人物。");return;}
            if(remote){string why=S.RemoteConversationBlocker(personId);if(why!=null){Toast(why);return;}}
            else if(definition!=null&&!S.CitySamePlace(personId)){OpenCityPersonProfile(personId,onClosed);return;}
            string name=definition?.Name??npc?.Name??staff.Name;
            string role=definition!=null?S.CityPersonRole(personId):npc?.Role??staff.Role;
            string greeting=npc!=null?(S.IsMet(personId)?npc.Intro:npc.FirstMeet??npc.Intro):staff?.LineFor(S.Clock.Today.TotalDays)??"你好，今天过得怎么样？";
            bool creatorChoice = initialPage == 3;
            bool discussingPeople = false;
            OpenDialogue(personId, name, role+(remote?" · 手机聊天":""), npc, greeting, 6, ctx =>
            {
                var options = new List<TalkOpt>();
                bool near = !remote && (definition == null || S.CitySamePlace(personId));
                bool canTalk = remote || near;
                void Reply(string text) { ctx.Reply = text; ctx.W.Rebuild(); parent?.RequestRebuild(); }
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
                    options.Add(new TalkOpt { Label = "返回", Act = () => { creatorChoice = false; ctx.W.Rebuild(); } });
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
                    options.Add(new TalkOpt { Label = "返回聊天", Act = () => { discussingPeople = false; ctx.W.Rebuild(); } });
                    return options;
                }
                if (!S.HasGreeted(personId)) options.Add(new TalkOpt {
                    Label = "打个招呼", Hot = true, Blocker = near ? null : "对方已离开",
                    Act = () => Reply(S.GreetPerson(personId) ? "你好，我是" + name + "。以后见面打声招呼。" : "对方现在不在这里。")
                });
                options.Add(new TalkOpt { Label = "随便聊聊", Blocker = canTalk ? null : "对方已离开", Act = () => Reply(S.CasualConversation(personId,remote)) });
                if (definition?.Stories != null && definition.Stories.Length > 0)
                    options.Add(new TalkOpt { Label = "听听你的经历", Blocker = canTalk ? null : "对方已离开", Act = () => Reply(S.CityHearStory(personId,remote)) });
                options.Add(new TalkOpt { Label = "谈论别人", Blocker = S.AskAboutOthersBlocker(personId,remote:remote), Act = () =>
                {
                    discussingPeople = true;
                    ctx.Reply = "你想打听谁？聊聊我认识的人吧。";
                    ctx.W.Rebuild();
                } });
                if (!S.IsMet(personId)) options.Add(new TalkOpt {
                    Label = "要个联系方式", Blocker = !near ? "需要当面交换" : S.ContactBlocker(personId),
                    Act = () => Reply(S.AskContact(personId) ? name + "：“好，咱们留个联系方式，以后可以联系。”\n已存入通讯录。" : S.ContactBlocker(personId) ?? "暂时没有交换成功。")
                });
                else options.Add(new TalkOpt { Label = "已交换联系方式", Blocker = "已存入通讯录" });
                options.Add(new TalkOpt { Label = "送礼", Blocker = !near ? "需要当面送礼" : S.PersonGiftBlocker(personId), Act = () => OpenPersonGiftGrid(personId, name, ctx) });
                if (S.IsMet(personId) && definition != null)
                    options.Add(new TalkOpt { Label = "邀请活动", Blocker = canTalk ? null : "对方已离开", Act = () => OpenCityActivityChooser(personId) });
                if (npc != null && place != null)
                    options.AddRange(NpcDialogueActions(npc, place, parent, ctx).Where(o => o.Label.StartsWith("砍价") || o.Label.StartsWith("聊聊行情") || o.Label.StartsWith("交心打听")));
                var creator = S.Creator(personId);
                if (creator != null && S.CreatorStory(creator) != null)
                    options.Add(new TalkOpt { Label = "聊聊最近的心事", Blocker = remote ? "需要当面交谈" : near ? null : "对方已离开", Act = () => { creatorChoice = true; Reply(S.CreatorStory(creator)); } });
                return options;
            }, firstReply, staff?.Id ?? (definition != null ? personId : null), () => { parent?.RequestRebuild(); onClosed?.Invoke(); },remote:remote);
        }

        private string ConversationViewKey(string id) => S.Clock.Today.TotalDays + "|" + S.CitySamePlace(id) + "|" + S.Relation(id)
            + "|" + S.HasGreeted(id) + "|" + S.IsMet(id) + "|" + S.CarriedKg.ToString("R");

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

        /// <summary>和某个有交情的人交谈（档口老板、阿杰、熟人）。stall 不为空表示正在这个档口里，可以砍价；parent 是背后的窗口，操作后要刷新。</summary>
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
