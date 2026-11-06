using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _kolLobbyFilter;
        private static readonly string[] KolPhases = { "开场", "讲解", "演示", "答疑" };
        private static readonly string[] KolChatNames = { "北窗", "旧机箱", "纸箱猫", "小比例", "窗台手作", "慢一点", "刚来看", "周末走街" };

        private sealed class KolWatch
        {
            public string Question;
            public long AskedAt;
            public string LastSession;
            public long ExpectedEnd;
            public KolLiveSession LastResult;
            public bool Finished;
        }

        private static string KolTimestamp(long at)
        {
            var date = new GameDate((int)(at / 1440));
            return date + " · " + Ui.WeekdayName(date.Weekday) + " " + KolTime((int)(at % 1440));
        }

        private static void TrackKolWindow(Window w, Func<string> refresh)
        {
            w.LiveConversation = true;
            w.LiveKey = refresh;
            w.LiveStamp = refresh();
        }

        private const int KolLobbyCardHeight = 278;
        private static string KolRoomKey(KolRoomStatus room) => room.SessionId + ":" + room.Blocker;
        private static string KolLobbyRoomKey(KolRoomStatus room) =>
            $"{KolRoomKey(room)}:{room.EndAt}:{room.NextStart}:{room.NextBrandId}:{room.NextSponsor}:{room.UpcomingBrandStart}:{room.UpcomingBrandName}";

        public void OpenKolLobby()
        {
            string blocker = S.AppBlocker(PhoneApps.Live);
            if (blocker != null) { Toast(blocker); return; }
            OpenWindow("手机 · 街坊直播", 1200, 840, BuildKolLobby, holds: false);
        }

        private void BuildKolLobby(Window w)
        {
            var bar = Ui.Row(w.Content, 42, 10);
            Ui.Size(Ui.Button(bar, "‹ 返回手机", () => { w.Close(); OpenPhone(); }, 17), 145, 40);
            Ui.Size(Ui.Label(bar, "直播大厅", 23, color: Ui.Accent), -1, 40, 1);
            var rooms = KolMarket.All.Select(t => S.GetKolRoom(t.Id)).ToArray();
            int live = rooms.Count(r => r.IsLive);
            var liveCount = Ui.Size(Ui.Label(bar, "", 17, TextAnchor.MiddleRight), 290, 40);
            void UpdateLiveCount(int count)
            {
                liveCount.text = count == 0 ? "当前没有主播开播" : $"{count} 位主播正在直播";
                liveCount.color = count == 0 ? Ui.Dim : Ui.Good;
            }
            UpdateLiveCount(live);

            var tabs = Ui.Row(w.Content, 40, 8);
            var labels = new[] { "全部主播", "我的品牌", "已关注" };
            for (int i = 0; i < labels.Length; i++)
            {
                int filter = i;
                Ui.Size(Ui.Button(tabs, labels[i], () => { _kolLobbyFilter = filter; w.Rebuild(); }, 18,
                    _kolLobbyFilter == i ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);
            }
            if (_kolLobbyFilter == 1)
            {
                var signed = S.KolStreaming.Contracts.Where(c => !c.Ended && c.EndAt > S.Clock.Now.TotalMinutes)
                    .Select(c => c.PersonId).ToHashSet();
                rooms = rooms.Where(r => signed.Contains(r.PersonId)).ToArray();
            }
            else if (_kolLobbyFilter == 2) rooms = rooms.Where(r => S.IsFollowingKol(r.PersonId)).ToArray();
            if (rooms.Length == 0)
            {
                Para(w.Content, _kolLobbyFilter == 1 ? "签约主播会显示在这里。" : "关注主播后，就可以在这里找到他们。", 18, Ui.Dim);
            }
            var ordered = rooms.OrderByDescending(r => r.IsLive).ToArray();
            var audience = new Dictionary<string, Text>();
            for (int i = 0; i < ordered.Length; i += 3)
            {
                var row = Ui.Row(w.Content, KolLobbyCardHeight, 8);
                foreach (var room in ordered.Skip(i).Take(3)) audience.Add(room.PersonId, BuildKolLobbyCard(row, w, room));
                for (int blank = 3 - Math.Min(3, ordered.Length - i); blank > 0; blank--)
                    Ui.Size(Ui.Rect(row, "EmptyRoomCell"), 0, KolLobbyCardHeight, 1);
            }
            TrackKolWindow(w, () =>
            {
                var current = KolMarket.All.Select(t => S.GetKolRoom(t.Id, audience.ContainsKey(t.Id))).ToArray();
                foreach (var room in current)
                    if (room.IsLive && audience.TryGetValue(room.PersonId, out var label))
                        label.text = $"{room.Viewers:N0} 人在线";
                UpdateLiveCount(current.Count(r => r.IsLive));
                return string.Join("|", current.Where(r => audience.ContainsKey(r.PersonId)).Select(KolLobbyRoomKey));
            });
        }

        private Text BuildKolLobbyCard(Transform parent, Window w, KolRoomStatus room)
        {
            var card = BrandCard(parent, "");
            Ui.Size(card, 0, KolLobbyCardHeight, 1).GetComponent<LayoutElement>().minWidth = 0;
            var info = Ui.Row(card, 94, 8);
            KolAvatar(info, room.PersonId, 65, 86, !room.IsLive);
            var labels = Ui.Rect(info, "KolRoomLabels"); Ui.Size(labels, 0, 94, 1); Ui.VList(labels, 4, 0);
            Line(labels, S.CityPerson(room.PersonId).Name + "的直播间", 18, room.IsLive ? Ui.TextColor : Ui.Dim);
            Line(labels, room.IsLive ? "● 直播中 · " + KolRoomKind(room) : room.EndAt > S.Clock.Now.TotalMinutes ? room.Blocker : "未在线",
                16, room.IsLive ? Ui.Good : Ui.Dim);
            Line(labels, KolMarket.Get(room.PersonId).Topic, 15, Ui.Dim);
            var audience = Line(card, room.IsLive ? $"{room.Viewers:N0} 人在线" : room.EndAt > S.Clock.Now.TotalMinutes
                ? "本场预计结束 " + KolTime((int)(room.EndAt % 1440)) : "", 15, Ui.Dim);
            BuildKolLobbyNext(card, room);
            var actions = Ui.Row(card, 36, 6);
            Ui.Size(Ui.Button(actions, room.IsLive ? "进入直播间" : "等待开播", () =>
            {
                w.Close();
                OpenKolViewer(room.PersonId, OpenKolLobby);
            }, 17, Ui.ButtonHot, room.IsLive), -1, 36, 1).name = "KolRoom_" + room.PersonId;
            Ui.Size(Ui.Button(actions, S.IsFollowingKol(room.PersonId) ? "已关注" : "关注", () =>
            {
                S.ToggleKolFollow(room.PersonId);
                w.Rebuild();
            }, 15), 80, 36);
            return audience;
        }

        private void BuildKolLobbyNext(Transform card, KolRoomStatus room)
        {
            var next = Ui.Panel(card, "KolLobbyNext_" + room.PersonId, Ui.PanelBg).rectTransform;
            Ui.Size(next, -1, 80);
            Ui.VList(next, 0, 4);
            if (room.NextStart < 0)
            {
                Ui.Size(Ui.Label(next, "下一场 · 时间待定", 16, color: Ui.Dim), -1, 24);
                return;
            }
            string kind = room.NextBrandId != null ? "品牌直播 · " + room.NextSponsor
                : room.NextSponsor != null ? "合作直播 · " + room.NextSponsor : "个人直播";
            Ui.Size(Ui.Label(next, "下一场 · " + kind, 16, color: room.NextSponsor != null ? Ui.Accent : Ui.TextColor), -1, 24);
            Ui.Size(Ui.Label(next, KolTimestamp(room.NextStart), 15, color: Ui.Dim), -1, 23);
            if (room.NextBrandId != null)
                Ui.Size(Ui.Label(next, "将播你的品牌内容", 14, color: Ui.Good), -1, 22);
            else if (room.UpcomingBrandStart > room.NextStart && room.UpcomingBrandName != null)
                Ui.Size(Ui.Label(next, $"下次播 {room.UpcomingBrandName} · {KolTimestamp(room.UpcomingBrandStart)}",
                    14, color: Ui.Dim), -1, 22);
        }

        private static string KolRoomKind(KolRoomStatus room) => room.BrandId != null
            ? room.Sponsor + "品牌专场" : room.Sponsor == null ? "个人直播" : room.Sponsor + "合作专场";

        public void OpenKolLiveRoom(string contractId)
        {
            var contract = S.KolStreaming.Contracts.FirstOrDefault(c => c.Id == contractId);
            if (contract == null) { Toast("没有这份直播合约。"); return; }
            OpenKolViewer(contract.PersonId, () =>
            {
                _brandTab = BrandKolTab;
                _brandId = contract.BrandId;
                _kolPage = "我的代言人";
                OpenBrandBusiness();
            });
        }

        private void OpenKolViewer(string person, Action back)
        {
            var watch = new KolWatch();
            OpenWindow("街坊直播 · 直播间", 1200, 775, w => BuildKolViewer(w, person, watch, back), holds: false);
        }

        private void BuildKolViewer(Window w, string person, KolWatch watch, Action back)
        {
            var room = S.GetKolRoom(person);
            var talent = KolMarket.Get(person);
            long now = S.Clock.Now.TotalMinutes;
            if (!watch.Finished && watch.LastSession != null && (!room.IsLive || watch.LastSession != room.SessionId))
            {
                var result = S.KolSessionResult(watch.LastSession);
                if (result?.Settled == true || watch.ExpectedEnd <= now)
                {
                    watch.Finished = true;
                    watch.LastResult = result ?? watch.LastResult;
                }
            }
            if (!watch.Finished && room.IsLive)
            {
                watch.LastSession = room.SessionId;
                watch.ExpectedEnd = room.EndAt;
                watch.LastResult = room.Session;
            }

            if (watch.Finished) w.LiveConversation = false;

            var bar = Ui.Row(w.Content, 60, 10);
            Ui.Size(Ui.Button(bar, "‹ 返回", () => { w.Close(); back(); }, 17), 104, 40);
            KolAvatar(bar, person, 44, 58);
            var name = Ui.Rect(bar, "KolViewerHost"); Ui.Size(name, -1, 60, 1); Ui.VList(name, 2, 0);
            Line(name, S.CityPerson(person).Name + " · " + (watch.Finished ? "本场已结束" : KolRoomKind(room)), 21, Ui.Accent);
            var hostStats = Line(name, $"粉丝 {S.KolFollowers(person):N0} · 人气 {S.KolPopularity(person)} · {S.KolPublicReputation(person)}", 16, Ui.Dim);
            Ui.Size(Ui.Button(bar, S.IsFollowingKol(person) ? "已关注" : "＋ 关注", () =>
            {
                S.ToggleKolFollow(person); w.Rebuild();
            }, 17), 125, 40);

            if (!room.IsLive || watch.Finished)
            {
                if (!watch.Finished) TrackKolWindow(w, () => KolRoomKey(S.GetKolRoom(person, false)));
                Line(w.Content, watch.Finished ? "本场直播已结束" : room.Blocker, 23, Ui.Accent);
                var result = watch.LastResult ?? room.Session;
                if (result != null) Para(w.Content, $"直播 {result.LiveMinutes} 分钟 · 峰值在线 {result.PeakViewers:N0} 人 · 本场送礼 {Money.FromCents(result.TipsCents)}", 18);
                if (watch.Finished && result?.Settled == true)
                    BuildKolViewerSettlement(w, result);
                if (room.NextStart >= 0) Line(w.Content, "下场预计 " + KolTimestamp(room.NextStart), 17, Ui.Dim);
                return;
            }

            int phase = KolPhase(room, now);
            var row = Ui.Row(w.Content, 418, 12);
            var stage = Ui.Rect(row, "KolLiveStage"); Ui.Size(stage, 740, 418);
            BuildKolStage(stage, talent, room, phase);
            var badge = Ui.Panel(stage, "LiveBadge", Ui.Warn).rectTransform.Anchor(0, 1, 0, 1, 12, -41, -130, 12);
            Ui.Label(badge, "● 直播中", 16, TextAnchor.MiddleCenter).rectTransform.Fill();
            var audience = Ui.Panel(stage, "LiveAudience", Ui.PanelBg).rectTransform.Anchor(1, 1, 1, 1, -166, -41, 12, 12);
            var viewerCount = Ui.Label(audience, "", 16, TextAnchor.MiddleCenter);
            viewerCount.rectTransform.Fill();
            var caption = Ui.Panel(stage, "LiveSubtitle", Ui.PanelBg).rectTransform.Anchor(0, 0, 1, 0, 13, 13, 13, -65);
            var subtitle = Ui.Label(caption, "", 17);
            subtitle.rectTransform.Fill(12);

            var chat = BrandCard(row, "直播弹幕 · " + KolPhases[phase]); Ui.Size(chat, -1, 418, 1);
            var messages = Ui.Scroll(chat, 264);
            var chatLines = Enumerable.Range(0, 5)
                .Select(_ => Ui.Size(Ui.Label(messages, "", 17, TextAnchor.MiddleLeft), -1, 42)).ToArray();
            var notice = Line(chat, "", 16, Ui.Accent);
            var questions = Ui.Row(chat, 34, 6);
            foreach (string question in KolQuestions(talent.Kind))
                Ui.Size(Ui.Button(questions, question, () =>
                {
                    watch.Question = question;
                    watch.AskedAt = S.Clock.Now.TotalMinutes;
                    w.LiveKey?.Invoke();
                }, 15), -1, 34, 1);

            var actions = Ui.Row(w.Content, 48, 10);
            Ui.Size(Ui.Label(actions, talent.Topic + " · " + KolPhases[phase], 22, color: Ui.Accent), -1, 46, 1);
            Ui.Size(Ui.Button(actions, S.Time.IsUserPaused ? "继续时间" : "暂停时间",
                () => { S.Time.TogglePause(); w.Rebuild(); }, 16), 125, 40);
            var gift = Ui.Size(Ui.Button(actions, "", () =>
            {
                var current = S.GetKolRoom(person, false);
                if (current.IsLive && current.Session != null && current.GiftWait == 0)
                    OpenKolGift(w, person, current.Session.Id);
            }, 18, Ui.ButtonHot), 214, 43);
            gift.name = "KolOpenGifts";
            var giftText = gift.GetComponentInChildren<Text>();

            TrackKolWindow(w, () =>
            {
                var current = S.GetKolRoom(person, false);
                long time = S.Clock.Now.TotalMinutes;
                int currentPhase = KolPhase(current, time);
                if (!current.IsLive || current.SessionId != room.SessionId || currentPhase != phase)
                    return KolRoomKey(current) + ":" + currentPhase;

                watch.LastResult = current.Session;
                hostStats.text = $"粉丝 {S.KolFollowers(person):N0} · 人气 {S.KolPopularity(person)} · {S.KolPublicReputation(person)}";
                viewerCount.text = $"{current.Viewers:N0} 人在线";
                subtitle.text = S.CityPerson(person).Name + "：" + KolSubtitle(talent, current, phase, watch, time);
                var lines = KolChat(talent, current, phase, time).ToArray();
                for (int i = 0; i < chatLines.Length; i++) chatLines[i].text = i < lines.Length ? lines[i] : "";
                notice.text = current.Session?.LastGiftYuan > 0 && time - current.Session.LastTipAt < 15
                    ? "你送出了「" + KolGiftName(current.Session.LastGiftYuan) + "」×1"
                    : watch.Question != null && time - watch.AskedAt <= 8 ? "你：" + watch.Question : "";
                notice.gameObject.SetActive(notice.text.Length > 0);
                giftText.text = current.GiftWait > 0 ? $"送礼冷却 · {current.GiftWait} 分钟" : "送礼物";
                gift.interactable = current.Session != null && current.GiftWait == 0;
                return KolRoomKey(current) + ":" + currentPhase;
            });
            var topics = BrandCard(w.Content, "本场话题");
            Line(topics, $"{KolTopics(talent.Kind)}     ·     预计下播 {KolTimestamp(room.EndAt)}", 17, Ui.Dim);
        }

        private void BuildKolViewerSettlement(Window w, KolLiveSession result)
        {
            var brand = result.BrandId == null ? null : S.BrandBusiness.Brands.FirstOrDefault(b => b.Id == result.BrandId);
            string sponsor = result.SponsorName ?? (result.RivalKind == null ? brand?.Name : S.BrandRivalName(result.RivalKind));
            if (sponsor == null)
            {
                Line(w.Content, "本场是个人直播，没有品牌合作。", 20, Ui.Accent);
                Para(w.Content, "本场累计主播人气和粉丝，品牌客流、知名度和口碑没有变化。", 17, Ui.Dim);
                return;
            }

            string kind = result.ShowcaseKind ?? brand?.Kind ?? result.RivalKind;
            string category = kind == "clothing" ? "服装" : kind == "models" ? "模型手办" : "电脑与配件";
            Line(w.Content, "本场品牌结算 · " + sponsor, 22, Ui.Accent);
            Line(w.Content, "本场展示 · " + sponsor + "的" + category, 18);
            BuildKolBrandResult(w.Content, result, result.RivalKind != null);
            if (brand == null) return;
            Ui.Size(Ui.Button(w.Content, "查看品牌专场记录", () =>
            {
                w.Close();
                _brandTab = BrandKolTab;
                _brandId = brand.Id;
                _kolPage = "专场记录";
                OpenBrandBusiness();
            }, 17), 235, 40);
        }

        private void BuildKolStage(RectTransform stage, KolTalentDef talent, KolRoomStatus room, int phase)
        {
            var image = Ui.Rect(stage, "KolStudioArt").Fill().gameObject.AddComponent<RawImage>();
            image.texture = BrandShowcaseArt.Studio(S.KolShowcaseKind(room));
            image.color = image.texture == null ? Ui.PanelBg2 : Color.white;
            image.raycastTarget = false;
            var hostArea = Ui.Rect(stage, "KolPresenterArea").Anchor(.025f, .13f, .53f, .97f);
            var host = Ui.Rect(hostArea, "KolPresenter_" + talent.Id).Fill().gameObject.AddComponent<RawImage>();
            BrandShowcaseArt.ApplyHost(host, talent.Id);
            string product = S.KolShowcaseProduct(room);
            var productArea = Ui.Rect(stage, "KolProductArea").Anchor(phase == 2 ? .48f : .56f, .23f, .97f, phase == 2 ? .88f : .79f);
            var demo = Ui.Rect(productArea, "KolProduct_" + (product ?? "none")).Fill().gameObject.AddComponent<RawImage>();
            BrandShowcaseArt.ApplyProduct(demo, product);
            var label = Ui.Panel(stage, "KolProductCaption", Ui.PanelBg).rectTransform.Anchor(.53f, .17f, .98f, .25f);
            Ui.Label(label, product == null ? "待准备品牌货物" :
                (room.BrandId == null ? "讲解样品 · " : "本场货物 · ") + S.BrandProductName(product),
                15, TextAnchor.MiddleCenter).rectTransform.Fill(4);
        }

        private static int KolPhase(KolRoomStatus room, long now)
        {
            if (room.Session == null) return 0;
            long span = Math.Max(1, room.EndAt - room.Session.StartedAt);
            return Math.Min(3, Math.Max(0, (int)((now - room.Session.StartedAt) * 4 / span)));
        }

        private static string KolTopics(string kind) => kind == "clothing" ? "版型 · 材料 · 日常搭配"
            : kind == "models" ? "比例 · 上色 · 陈列" : "用途与预算 · 配件兼容 · 机箱通风";

        private static string[] KolQuestions(string kind) => kind == "clothing" ? new[] { "问版型", "问搭配" }
            : kind == "models" ? new[] { "问材料", "问怎么补救" } : new[] { "问预算", "问装机" };

        private static string KolSubtitle(KolTalentDef talent, KolRoomStatus room, int phase, KolWatch watch, long now)
        {
            if (room.Session?.LastGiftYuan > 0 && now - room.Session.LastTipAt <= 2)
                return "谢谢你的" + KolGiftName(room.Session.LastGiftYuan) + "！我们接着看。";
            if (watch.Question != null && now >= watch.AskedAt + 1 && now <= watch.AskedAt + 8)
            {
                switch (watch.Question)
                {
                    case "问版型": return "先看肩线，再量胸围和活动空间，最后看衣长。";
                    case "问搭配": return "先选用途，再用一件基础色的衣服把颜色连起来。";
                    case "问材料": return "先在边角试涂，等干透后再决定要不要加第二层。";
                    case "问怎么补救": return "让它先干，再从边角轻轻补薄，别一次涂太厚。";
                    case "问预算": return "先定用途，再给最重要的部分留预算。";
                    default: return "先确认尺寸和接口，装之前也给走线留点空间。";
                }
            }
            if (phase == 0) return (room.Sponsor == null ? "大家晚上好。" : "欢迎来到" + room.Sponsor + "的分享专场。") + "今天聊聊" + talent.Topic + "。";
            if (phase == 3) return "现在来答几个问题，大家可以说说自己的情况。";
            if (talent.Kind == "clothing")
                return phase == 1 ? "先看肩线和活动空间，再决定要不要叠穿。" : "在灯下看一下面料，再比较它垂下来时的形状。";
            if (talent.Kind == "models")
                return phase == 1 ? "先定比例和摆放位置，再考虑做旧的程度。" : "我把底座转一下，从侧面看看接缝和轮廓。";
            return phase == 1 ? "装机前先想清楚用途，再把预算留给最需要的地方。" : "先看看空间和通风，最后再确认供电和走线。";
        }

        private IEnumerable<string> KolChat(KolTalentDef talent, KolRoomStatus room, int phase, long now)
        {
            var pool = new List<string>(talent.Kind == "clothing"
                ? new[] { "叠穿的话要多留多少空间？", "怎么判断版型适不适合自己？", "想看自然光下的颜色。", "日常怎么搭配比较方便？" }
                : talent.Kind == "models"
                    ? new[] { "做旧需要用什么材料？", "能转过来看一下侧面吗？", "放在小柜子上适合吗？", "刷重了还能怎么补救？" }
                    : new[] { "第一次装机最容易漏哪一步？", "办公和打游戏预算怎么分？", "小机箱要留意哪些尺寸？", "旧配件能继续用吗？" });
            pool.Add(phase == 0 ? "刚进来，今天讲什么？" : phase == 3 ? "下一场大概什么时候播？" : "能再慢一点讲这一步吗？");
            pool.Add(phase == 0 ? "晚上好，来看会儿直播。" : "想知道这个办法适合什么情况。");
            if (S.KolPublicReputation(talent.Id) != "风评稳定")
                pool.AddRange(new[] { "能先说明这场有没有商业合作吗？", "想看从头到尾的演示过程。", "可以也说说它的局限吗？" });
            if (room.SponsorInfluence >= 50 || room.Viewers >= 350)
                pool.AddRange(new[] { "看到有人提起这场，来听听。", "跟上次直播讲的有什么区别？" });
            if (room.SponsorReputation > 60) pool.Add("之前就听说过这个牌子，想多了解一些。");
            else if (room.SponsorReputation < 40) pool.Add("可以说一下哪些情况不适合吗？");

            // A stable presentation seed never advances the simulation or consumes game randomness.
            int seed = unchecked((int)(now / (room.Viewers >= 350 ? 1 : 2)) + talent.Audience * 17 + phase * 97);
            var random = new System.Random(seed);
            int count = room.Viewers >= 350 ? 5 : 4;
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int pick = random.Next(pool.Count);
                yield return KolChatNames[(i + (seed & 7)) % KolChatNames.Length] + "：" + pool[pick];
                pool.RemoveAt(pick);
            }
        }

        private static string KolGiftName(int yuan) => yuan == 10 ? "加油牌" : yuan == 100 ? "应援灯" : "星光礼盒";

        private void OpenKolGift(Window viewer, string person, string sessionId)
        {
            int selection = 100;
            OpenWindow("送礼物 · " + S.CityPerson(person).Name, 780, 365, w =>
            {
                Line(w.Content, "游戏现金 " + S.Ledger.Balance, 19, Ui.Good);
                var options = Ui.Row(w.Content, 80, 10);
                foreach (int yuan in new[] { 10, 100, 1000 })
                    Ui.Size(Ui.Button(options, KolGiftName(yuan) + "\n¥" + yuan.ToString("N0"), () =>
                    {
                        selection = yuan;
                        w.Rebuild();
                    }, 18, selection == yuan ? Ui.ButtonHot : Ui.ButtonBg), -1, 78, 1).name = "KolGift_" + yuan;
                int heat = selection == 10 ? 1 : selection == 100 ? 3 : 8;
                int followers = selection == 10 ? 5 : selection == 100 ? 20 : 80;
                int audience = selection == 10 ? 25 : selection == 100 ? 60 : 120;
                Line(w.Content, $"预计效果 · 人气 +{heat} · 粉丝 +{followers} · 临时在线加成 +{audience}", 19, Ui.Accent);
                Para(w.Content, "在线加成随时间衰减，每个直播间每15游戏分钟可送一次，效果受上限限制。", 16, Ui.Dim);
                var actions = Ui.Row(w.Content, 45, 8);
                Ui.Size(Ui.Button(actions, "取消", w.Close, 17), 180, 44);
                Ui.Size(Ui.Button(actions, $"确认送出 · 扣除 ¥{selection:N0}", () =>
                {
                    if (!S.TipKolRoom(person, selection, sessionId)) return;
                    w.Close(); viewer.Rebuild();
                }, 18, Ui.ButtonHot, S.Ledger.Balance >= Money.FromYuan(selection)), -1, 44, 1).name = "KolConfirmGift";
            });
        }
    }
}
