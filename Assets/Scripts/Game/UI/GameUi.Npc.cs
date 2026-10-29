using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>人物立绘与“和某人交谈”窗口：送礼、打听行情、约出去玩、砍价都从点人物进入。</summary>
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

        private RectTransform PortraitCard(Transform parent, string portraitId, string title, string subtitle, float width, System.Action onClick, bool unknown = false, string tagText = "点我交谈")
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

        // ---------- 店主（档口以外的店：烟酒茶行、手机店、家具城、农贸市场、车行）----------

        private sealed class Keeper
        {
            public string Id, Name, Role;
            public string[] Lines;
        }

        private static readonly Keeper TobaccoKeeper = new Keeper
        {
            Id = "keeper_tobacco", Name = "老胡", Role = "烟酒茶行老板",
            Lines = new[] { "来来来，坐，喝口茶再挑。", "送人的话，还是好烟好酒体面。", "烟酒不愁卖，就是搬着沉，你背包扛得住不？", "老主顾我给你留着好货。" },
        };

        private static readonly Keeper PhoneKeeper = new Keeper
        {
            Id = "keeper_phone", Name = "小唐", Role = "手机店店员",
            Lines = new[] { "智能手机了解一下？能装好多 app！", "按键机也不错，就是只能打电话、发短信。", "买了手机去应用商店下载，别忘了哦。" },
        };

        private static readonly Keeper MallKeeper = new Keeper
        {
            Id = "keeper_mall", Name = "小赵", Role = "家具城导购",
            Lines = new[] { "轻的您自己拎，太大件我们叫货车送。", "线上同款便宜点，不过要等两天。", "房子收拾收拾，人心情都不一样。", "店里摆设得歇业装修一天才能摆。" },
        };

        private static readonly Keeper FarmKeeper = new Keeper
        {
            Id = "keeper_farm", Name = "王婶", Role = "农贸市场摊主",
            Lines = new[] { "今天的菜刚摘的，新鲜！", "米要几斤？称给你。", "家里没冰箱别买多了，会放坏的。" },
        };

        private static readonly Keeper DealerKeeper = new Keeper
        {
            Id = "keeper_dealer", Name = "老马", Role = "车行老板",
            Lines = new[] { "电驴好骑，三轮拉货，就是别让交警逮着。", "天天走路多累，来看看车。", "有了车，去市场进货能省一半时间。" },
        };

        private readonly Dictionary<string, int> _keeperTalk = new Dictionary<string, int>();

        private static Keeper KeeperFor(Place p)
        {
            if (p.Kind == PlaceKind.Dealer) return DealerKeeper;
            if (p.Kind != PlaceKind.Shop || p.IsPlayerShop) return null;
            if (p.Business == Gifts.TobaccoBusiness) return TobaccoKeeper;
            if (p.Business == GameSession.PhoneBusiness) return PhoneKeeper;
            if (p.Business == GameSession.MallBusiness) return MallKeeper;
            if (p.Business == GameSession.FarmBusiness) return FarmKeeper;
            return null;
        }

        /// <summary>
        /// 有店主的店：左边店主立绘 + 一句话（点头像换一句），右边是原来的商品内容。
        /// 返回放商品内容的容器；没有店主返回 null。
        /// </summary>
        private RectTransform BuildKeeperFrame(Window w, Place p, float bodyHeight)
        {
            Keeper k = KeeperFor(p);
            if (k == null) return null;
            RectTransform body = Ui.Row(w.Content, bodyHeight, 12);
            RectTransform left = Ui.Rect(body, "Keeper");
            Ui.VList(left, 8, 0);
            Ui.Size(left, 230, -1, 0);
            left.GetComponent<LayoutElement>().minWidth = 230;
            _keeperTalk.TryGetValue(k.Id, out int extra);
            int idx = (S.Clock.Today.TotalDays + extra) % k.Lines.Length;
            PortraitCard(left, k.Id, k.Name, k.Role, 230, () =>
            {
                _keeperTalk[k.Id] = extra + 1;
                w.Rebuild();
            }, false, "点我聊聊");
            Image bubble = Ui.Panel(left, "Bubble", new Color(0.16f, 0.18f, 0.22f, 1f));
            Ui.Size(bubble, -1, 96);
            Text say = Ui.Label(bubble.transform, "“" + k.Lines[idx] + "”", 16, TextAnchor.MiddleLeft);
            say.rectTransform.Fill(10);

            RectTransform center = Ui.Rect(body, "Goods");
            Ui.VList(center, 6, 0);
            Ui.Size(center, -1, -1, 1);
            return center;
        }

        /// <summary>和某人交谈。stall 不为空表示正在这个档口里，可以砍价；parent 是背后的档口窗口，操作后要刷新。</summary>
        public void OpenNpcWindow(NpcDef n, Place stall = null, Window parent = null)
        {
            OpenWindow("和" + n.Name + "聊聊", 920, 620, w =>
            {
                RectTransform body = Ui.Row(w.Content, 470, 14);
                PortraitCard(body, n, 340, null);

                RectTransform right = Ui.Rect(body, "Right");
                Ui.VList(right, 8, 4);
                Ui.Size(right, -1, -1, 1);

                int rel = S.Relation(n.Id);
                Text head = Ui.Label(right, $"<size=26><b>{n.Name}</b></size>　{n.Role}", 20);
                Ui.Size(head, -1, 40);
                RectTransform relRow = Ui.Row(right, 26);
                Ui.Size(Ui.Label(relRow, $"{Npcs.LevelName(rel)} {rel}", 17, TextAnchor.MiddleLeft, Ui.Good), 110);
                Ui.Bar(relRow, rel / 100f, Ui.Accent, 200, 14);

                string say = rel < 20 && !string.IsNullOrEmpty(n.FirstMeet) ? n.FirstMeet : n.Intro;
                Image bubble = Ui.Panel(right, "Bubble", new Color(0.16f, 0.18f, 0.22f, 1f));
                Ui.Size(bubble, -1, 84);
                Text sayText = Ui.Label(bubble.transform, "“" + say.Trim('"') + "”", 17, TextAnchor.MiddleLeft, Ui.TextColor);
                sayText.rectTransform.Fill(10);

                Text perk = Ui.Label(right, "<size=14><color=#9AA3AD>交情好处：" + NpcPerks.Describe(n.Id) + "</color></size>", 14, TextAnchor.UpperLeft);
                Ui.Size(perk, -1, 44);

                string gift = S.GiftBlocker(n.Id);
                Ui.Size(Ui.Button(right, gift == null ? GiftLabel(n) : gift, () =>
                {
                    S.GiveGift(n.Id);
                    w.Rebuild();
                    parent?.Rebuild();
                }, 17, Ui.ButtonHot, gift == null), -1, 44);

                if (n.IsTipster)
                {
                    string tip = S.TipBlocker(n.Id);
                    Ui.Size(Ui.Button(right, tip == null ? "递烟打听行情（用掉一包烟）" : tip, () =>
                    {
                        S.AskForTip(n.Id);
                        w.Rebuild();
                    }, 17, Ui.ButtonBg, tip == null), -1, 44);
                }

                Ui.Size(Ui.Button(right, "约出去玩", () => OpenInviteWindow(n, w), 17, Ui.ButtonBg), -1, 44);

                if (stall != null)
                {
                    bool open = S.IsWholesalerOpen;
                    bool tried = S.HasBargainedToday(stall);
                    Ui.Size(Ui.Button(right, tried ? "今天砍过价了" : $"砍价（成功率看魅力 {S.Player.Charm}，太累会降低）", () =>
                    {
                        BargainResult r = S.Bargain(stall);
                        if (r == BargainResult.Success) Toast("砍价成功！今天这家便宜了几个点。");
                        else if (r == BargainResult.Failed) Toast("老板不乐意：\"爱买不买。\" 今天还涨了 1%。");
                        w.Rebuild();
                        parent?.Rebuild();
                    }, 17, Ui.ButtonBg, open && !tried), -1, 44);
                }

                if (n.IsTipster) Line(right, TrustText(n), 14, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 20), -1, 46);
            });
        }
    }
}
