using System.Collections.Generic;
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
            Image frame = Ui.Panel(parent, "Portrait", new Color(0.05f, 0.06f, 0.08f, 1f));
            Ui.Size(frame, width, height);
            frame.GetComponent<LayoutElement>().minWidth = width;

            RectTransform imgRt = Ui.Rect(frame.transform, "Image").Fill(3);
            var raw = imgRt.gameObject.AddComponent<RawImage>();
            raw.texture = PortraitOf(portraitId);
            raw.color = unknown ? new Color(0.12f, 0.12f, 0.14f) : Color.white;
            if (raw.texture == null) raw.color = new Color(0.25f, 0.28f, 0.33f);

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

            if (onClick != null && !unknown)
            {
                Image tag = Ui.Panel(frame.transform, "Tag", new Color(0.2f, 0.55f, 0.52f, 0.92f));
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
            Image frame = Ui.Panel(parent, "Thumb", new Color(0.05f, 0.06f, 0.08f, 1f));
            Ui.Size(frame, size, size);
            frame.GetComponent<LayoutElement>().minWidth = size;
            RectTransform imgRt = Ui.Rect(frame.transform, "Image").Fill(2);
            var raw = imgRt.gameObject.AddComponent<RawImage>();
            raw.texture = PortraitOf(n.Id);
            raw.uvRect = new Rect(0.12f, 0.48f, 0.76f, 0.5f);
            if (raw.texture == null) raw.color = new Color(0.25f, 0.28f, 0.33f);
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
            PlaceRoster r = Rosters.For(p);
            if (r == null) return null;
            RectTransform body = Ui.Row(w.Content, bodyHeight, 12);
            RectTransform left = Ui.Rect(body, "People");
            Ui.VList(left, 8, 0);
            Ui.Size(left, 210, -1, 0);
            left.GetComponent<LayoutElement>().minWidth = 210;

            Image chart = Ui.Panel(left, "OrgChart", new Color(0.09f, 0.11f, 0.15f, 1f));
            Ui.VList(chart.rectTransform, 0, 8);
            Ui.Size(Ui.Label(chart.transform, "店内人员", 14, TextAnchor.MiddleCenter, Ui.Dim), -1, 20);

            StaffDef greeter = r.Greeter;
            NpcDef bossNpc = r.Boss;
            bool stall = p.Kind == PlaceKind.Wholesaler;
            var notes = new List<string>();

            // 第一级：老板（交情老板 / 普通上司 / 直接接待的小店老板）
            bool greeterIsTop = bossNpc == null && r.PlainBoss == null;
            if (bossNpc != null)
            {
                if (S.BossReachable(r))
                {
                    int rel = S.Relation(bossNpc.Id);
                    OrgNode(chart.transform, bossNpc.Id, bossNpc.Name, "老板", true, false, () => OpenNpcWindow(bossNpc, stall ? p : null, w));
                    notes.Add($"<color=#6FD0A8>{bossNpc.Name}：{Npcs.LevelName(rel)} {rel}</color>");
                    if (stall) notes.Add($"进价 -{NpcPerks.OwnerDiscount(rel) * 100:0.#}%　每样每天最多 {NpcPerks.StallQuota(rel)} 个");
                }
                else
                {
                    OrgNode(chart.transform, bossNpc.Id, "？？？", "老板", true, true, null);
                    notes.Add("<color=#E0806A>" + (!S.IsMet(bossNpc.Id) ? "老板：还没认识" : $"老板：交情 {r.BossGate} 才肯见你（现在 {S.Relation(bossNpc.Id)}）") + "</color>");
                    notes.Add("找店员聊聊老板的事，替你说几句好话，交情够了他自然肯出来。");
                }
            }
            else if (r.PlainBoss != null)
            {
                StaffDef top = r.PlainBoss;
                OrgNode(chart.transform, top.Id, top.Name, "店长", true, false, () => OpenStaffDialogue(top, r, p, w));
            }
            else
            {
                OrgNode(chart.transform, greeter.Id, greeter.Name, "老板", true, false, () => OpenStaffDialogue(greeter, r, p, w));
            }

            // 第二级：接待的店员
            if (!greeterIsTop)
            {
                OrgLink(chart.transform);
                OrgNode(chart.transform, greeter.Id, greeter.Name, greeter.Role.EndsWith("导购") ? "导购" : "店员", false, false, () => OpenStaffDialogue(greeter, r, p, w));
            }
            notes.Add("<color=#9AA3AD>点头像和他说话</color>");

            foreach (string n in notes) Ui.Size(Ui.Label(left, "<size=13>" + n + "</size>", 13, TextAnchor.UpperLeft, Ui.Dim), -1, n.Length > 22 ? 44 : 22);

            RectTransform center = Ui.Rect(body, "Goods");
            Ui.VList(center, 6, 0);
            Ui.Size(center, -1, -1, 1);
            return center;
        }

        private const float OrgCardWidth = 44;

        /// <summary>人员图里的一个人：小头像卡（老板金框、店员灰框），下面是名字和职务；locked 时是暗色剪影。</summary>
        private void OrgNode(Transform parent, string portraitId, string name, string title, bool top, bool locked, System.Action onClick)
        {
            RectTransform row = Ui.Row(parent, 104, 0);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
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
            if (raw.texture == null) raw.color = new Color(0.25f, 0.28f, 0.33f);
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
            Ui.Size(Ui.Label(col, $"<size=11><color={(top ? "#E3B347" : "#9AA3AD")}>{title}</color></size>", 11, TextAnchor.MiddleCenter), -1, 14);
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
            System.Func<TalkCtx, List<TalkOpt>> makeOptions, string firstReply = null)
        {
            var ctx = new TalkCtx { Reply = firstReply };
            float height = 440 + 46 * optionCount + (npc != null ? 66 : 0);
            OpenWindow(name + "　" + role, 700, height, w =>
            {
                ctx.W = w;

                RectTransform top = Ui.Row(w.Content, 200, 14);
                PortraitCard(top, portraitId, name, "", 150, null);
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
                Image bubble = Ui.Panel(say, "Bubble", new Color(0.16f, 0.18f, 0.22f, 1f));
                Ui.Size(bubble, -1, bubbleHeight);
                string text = ctx.Reply ?? "“" + greeting.Trim('"') + "”";
                Text sayText = Ui.Label(bubble.transform, text, 19, TextAnchor.UpperLeft, Ui.TextColor);
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

                if (npc != null)
                {
                    string perk = "<color=#9AA3AD>交情好处：" + NpcPerks.Describe(npc.Id) + (npc.IsTipster ? "\n" + TrustText(npc) : "") + "</color>";
                    Ui.Size(Ui.Label(w.Content, "<size=14>" + perk + "</size>", 14, TextAnchor.UpperLeft), -1, 58);
                }

                Ui.Size(Ui.Button(w.Content, "结束对话", w.Close, 20), -1, 46);
            }, true, true, null);
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
            int chats = 0;
            string greeting = staff.LineFor(S.Clock.Today.TotalDays);
            bool asksBoss = roster.BossNpcId != null && staff == roster.Greeter;
            int count = 2 + (asksBoss ? 1 : 0);
            OpenDialogue(staff.Id, staff.Name, staff.Role, null, greeting, count, ctx =>
            {
                var list = new List<TalkOpt>();
                list.Add(new TalkOpt
                {
                    Label = "随便聊聊",
                    Act = () =>
                    {
                        chats++;
                        ctx.Reply = staff.Name + "：“" + staff.LineFor(S.Clock.Today.TotalDays + chats) + "”";
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
                list.Add(new TalkOpt { Label = "看看货", Hot = true, Act = () => ctx.W.Close() });
                return list;
            });
        }

        /// <summary>和某个有交情的人交谈（档口老板、阿杰、熟人）。stall 不为空表示正在这个档口里，可以砍价；parent 是背后的窗口，操作后要刷新。</summary>
        public void OpenNpcWindow(NpcDef n, Place stall = null, Window parent = null, string firstReply = null)
        {
            int rel0 = S.Relation(n.Id);
            string greeting = rel0 < 20 && !string.IsNullOrEmpty(n.FirstMeet) ? n.FirstMeet : n.Intro;
            int count = 4 + (n.IsTipster ? 2 : 0) + (stall != null ? 1 : 0);
            OpenDialogue(n.Id, n.Name, n.Role, n, greeting, count, ctx =>
            {
                var o = new List<TalkOpt>();
                o.Add(new TalkOpt
                {
                    Label = "随便闲聊（10 分钟）",
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
                    Label = GiftLabel(n),
                    Blocker = S.GiftBlocker(n.Id),
                    Hot = true,
                    Act = () =>
                    {
                        if (S.GiveGift(n.Id)) ctx.Reply = n.GiftReply;
                        ctx.W.Rebuild();
                        parent?.Rebuild();
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
                            S.AskForTip(n.Id);
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
            }, firstReply);
        }
    }
}
