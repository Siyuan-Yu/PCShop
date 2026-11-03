using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class BrandStoryRecord
    {
        public string Id, BrandId, Kind, PersonId, SourceId, ShopId, ProductId, Choice, Conclusion;
        public int Stage, StartedDay, PreparedDay = -1;
        public bool Fulfilled;
        public BrandStoryRecord Copy() => (BrandStoryRecord)MemberwiseClone();
    }
    public sealed partial class SaveData { public BrandStoryRecord[] brandStories; }

    public sealed partial class GameSession
    {
        private readonly List<BrandStoryRecord> _brandStories = new List<BrandStoryRecord>();
        public IReadOnlyList<BrandStoryRecord> BrandStories => _brandStories.Select(s => s.Copy()).ToArray();
        public static string BrandStoryTitle(string kind) => kind == "promotion" ? "第一次替你推荐"
            : kind == "review" ? "把测试条件说清楚" : kind == "design" ? "这次带一点新东西" : "名单上多一个名字";
        public string BrandStorySpeaker(BrandStoryRecord s) => s.Kind == "curator" && s.Stage == 1 ? "resident_baiming" : s.PersonId;
        private string StoryName(string id) => CityPerson(id)?.Name ?? id;
        private string StoryContactBlocker(string id, bool remote)
        {
            if (IsGameOver || _sleeping || IsWorking || CityActivityExclusive) return "请先结束当前工作、休息或活动。";
            return remote ? RemoteConversationBlocker(id) : CitySamePlace(id) ? null : "请当面交谈，或使用已交换的联系方式。";
        }
        // Offers are projections. Reading a tab or a profile must not accept a story.
        public IEnumerable<BrandStoryRecord> BrandStoryOffers()
        {
            foreach (var b in _brands.Brands)
            {
                foreach (var person in new[] { "momo", "qiao" })
                {
                    string kind = person == "momo" ? "promotion" : "review", id = b.Id + "|" + kind;
                    if (_brandStories.Any(s => s.Id == id)) continue;
                    var deal = _brands.Deals.LastOrDefault(d => d.BrandId == b.Id && d.PersonId == person && !d.Ended && d.End > BrandDay);
                    if (deal != null) yield return new BrandStoryRecord { Id = id, BrandId = b.Id, Kind = kind, PersonId = person, SourceId = deal.Id };
                }
                var exhibit = _brands.Exhibits.LastOrDefault(e => e.BrandId == b.Id && !e.Finished && !e.Missed && e.Day >= BrandDay);
                if (exhibit == null) continue;
                foreach (string kind in new[] { "design", "curator" })
                {
                    if (kind == "design" && b.Kind != "models" && b.Kind != "clothing") continue;
                    string id = b.Id + "|" + kind;
                    if (_brandStories.Any(s => s.Id == id)) continue;
                    yield return new BrandStoryRecord { Id = id, BrandId = b.Id, Kind = kind,
                        PersonId = kind == "curator" ? "life_curator" : b.Kind == "models" ? "life_creator" : "life_designer",
                        SourceId = exhibit.Id, ShopId = exhibit.ShopId, ProductId = exhibit.ProductId };
                }
            }
        }
        public IEnumerable<BrandStoryRecord> BrandStoryTopics(string person) => BrandStories.Concat(BrandStoryOffers())
            .Where(s => s.Stage < 3 && (BrandStorySpeaker(s) == person || s.PersonId == person));
        public string BeginBrandStory(string id, bool remote = false)
        {
            var offer = BrandStoryOffers().FirstOrDefault(s => s.Id == id);
            if (offer == null) return "这次合作已记录，或当前没有对应的合作邀请。";
            string why = StoryContactBlocker(offer.PersonId, remote); if (why != null) return why;
            if (!GreetPerson(offer.PersonId, remote)) return "请先与本人交谈。";
            offer.Stage = 1; offer.StartedDay = BrandDay; _brandStories.Add(offer);
            string text = BrandStoryText(offer);
            RememberSocial(offer.PersonId, text);
            return text;
        }
        public string BrandStoryText(BrandStoryRecord s)
        {
            if (s.Stage == 3) return s.Conclusion;
            string who = StoryName(s.PersonId);
            if (s.Kind == "promotion") return who + "：这是第一次替你的品牌做合作。我想拍一段真能照着买的介绍。先选一家店、一件有现货的商品，留一件作拍摄样品。你想重点介绍价格用途，还是使用和售后？三天后我们看实际账单，也看看货架还能不能接住来的人。没有成交也照样聊。";
            if (s.Kind == "review") return who + "：一次样品表现不能代表所有场景。我还缺一份清楚的测试说明，先别写‘什么都能用’。可以用一件样品，明确介绍适用范围；也可以留两件做补测，至少等一天。两种方案都保留测试条件，不要求我把缺点删掉。";
            if (s.Kind == "design") return who + (s.PersonId == "life_creator"
                ? "：以前大家只问最熟悉的那套作品。这次我想换个讲法，让人看到一件东西怎么被设计出来。你已报名的商品不变，我们可以在展会上讲设计，也可以先把用途和供货讲清楚。换展示方向不是凭空生产一个新款。"
                : "：上次有人只问这件衣服能卖多少钱。我想让人试着理解剪裁和日常穿着。这次用你已报名的展品，选设计展示或采购洽谈都可以；我希望宣传里的名字和承诺讲清楚。")
                + "展后等这届订单真正交付或逾期，我们再聊结果。";
            return "任帆：第一次办展，我连入口指路都贴错过。白明现在也想独立做一次展示。你先把这届接待和后续订单完成，再找他分享准备过程；可以带着记录细讲，也可以先交换一份简短清单。最后回来告诉我聊得怎样，我会把这次介绍记下来。";
        }
        public string BrandStoryObjective(BrandStoryRecord s)
        {
            if (s.Stage == 3) return s.Fulfilled ? "已完成，可在人物经历中回看" : "已收束，未完成的业务按原规则处理";
            if (s.Stage == 0) return "与" + StoryName(s.PersonId) + "聊聊这次合作";
            if (s.Stage == 1) return s.Kind == "curator" ? "完成本届接待及展后交付，再与白明分享准备记录"
                : s.Kind == "design" ? "到展品来源店确认展示方向，需在第一轮接待前完成"
                : "到品牌所属门店，选择真实商品并交出拍摄／测试样品";
            return BrandStoryFinishBlocker(s.Id) ?? "与" + StoryName(s.PersonId) + "复盘本次合作";
        }
        private bool StorySourceEnded(BrandStoryRecord s)
        {
            if (s.Kind == "promotion" || s.Kind == "review")
            { var d = _brands.Deals.FirstOrDefault(x => x.Id == s.SourceId); return d == null || d.Ended || d.End <= BrandDay; }
            var e = _brands.Exhibits.FirstOrDefault(x => x.Id == s.SourceId);
            return e == null || e.Missed || e.Day < BrandDay && !e.Finished;
        }
        private BrandOrder StoryExpoOrder(BrandStoryRecord s) => _brands.Orders.FirstOrDefault(o => o.Id == s.SourceId + ":lead" && o.BrandId == s.BrandId);
        public string BrandStoryPreparationBlocker(string id, string shopId, string productId, string choice)
        {
            var s = _brandStories.FirstOrDefault(x => x.Id == id); if (s == null || s.Stage != 1 || s.Kind == "curator") return "当前没有这项样品准备。";
            if (IsGameOver || _sleeping || IsWorking || CityActivityExclusive) return "请先结束当前工作或活动。";
            if (StorySourceEnded(s)) return "这次合作已经结束，可联系本人收束故事。";
            var b = Brand(s.BrandId); var shop = ShopAt(shopId);
            if (b == null || shop == null || !BrandShops(b).Any(x => x.Place.Id == shopId)) return "请选择这个品牌所属的门店。";
            if (CurrentPlace?.Id != shopId) return "请到所选门店现场准备。";
            if (s.Kind == "design")
            {
                var e = _brands.Exhibits.FirstOrDefault(x => x.Id == s.SourceId);
                if (e == null || e.ShopId != shopId || e.ProductId != productId) return "请使用报名时的门店和展品。";
                if (e.Stage > 0 || e.Finished) return "现场接待已开始，不再更换准备方案；可在展后复盘。";
                return choice == "design" || choice == "buyers" ? null : "请选择设计展示或采购洽谈。";
            }
            if (s.Kind == "promotion" && choice != "value" && choice != "service"
                || s.Kind == "review" && choice != "disclose" && choice != "retest") return "请选择有效的准备方案。";
            int count = choice == "retest" ? 2 : 1;
            return !BrandProducts(shopId).Contains(productId) || BrandProductCount(shopId, productId) < count ? "需要所选商品现货 " + count + " 件。" : null;
        }
        public bool PrepareBrandStory(string id, string shopId, string productId, string choice)
        {
            string why = BrandStoryPreparationBlocker(id, shopId, productId, choice); if (why != null) return Fail(why);
            var s = _brandStories.First(x => x.Id == id);
            if (s.Kind == "design") _brands.Exhibits.First(e => e.Id == s.SourceId).Focus = choice;
            else if (!BrandTake(shopId, productId, choice == "retest" ? 2 : 1)) return false;
            s.ShopId = shopId; s.ProductId = productId; s.Choice = choice; s.PreparedDay = BrandDay; s.Stage = 2;
            RememberSocial(s.PersonId, "为《" + BrandStoryTitle(s.Kind) + "》准备了" + BrandProductName(productId) + "，选择" + BrandStoryChoiceLabel(choice) + "。");
            Say(s.Kind == "design" ? "本届展示方向已确认，报名样品不重复扣除。" : "样品已从所选门店现货扣除，用于这次拍摄或测试。请正常经营，之后联系合作方复盘。");
            return true;
        }
        public static string BrandStoryChoiceLabel(string choice) => choice == "value" ? "介绍价格与适用用途（1件样品）"
            : choice == "service" ? "演示使用与售后（1件样品）" : choice == "disclose" ? "写清适用范围（1件样品）"
            : choice == "retest" ? "补充对照测试（2件样品，至少1天）" : choice == "design" ? "讲解设计与署名"
            : choice == "buyers" ? "先讲用途与供货" : choice == "share" ? "带记录讲一遍准备过程" : "交换一份简短清单";
        public string ShareBrandExpoBlocker(string id, bool remote = false)
        {
            var s = _brandStories.FirstOrDefault(x => x.Id == id);
            if (s == null || s.Kind != "curator" || s.Stage != 1) return "当前没有这次交流。";
            string why = StoryContactBlocker("resident_baiming", remote); if (why != null) return why;
            return StoryExpoOrder(s)?.Done == true ? null : "先完成本届接待，并实际送达本届展会订单。";
        }
        public string ShareBrandExpoStory(string id, string choice, bool remote = false)
        {
            if (choice != "share" && choice != "brief") return "请选择交流方式。";
            string why = ShareBrandExpoBlocker(id, remote); if (why != null) return why;
            if (!GreetPerson("resident_baiming", remote)) return "请先与白明交谈。";
            var s = _brandStories.First(x => x.Id == id); s.Choice = choice; s.Stage = 2;
            string text = choice == "share" ? "白明：你把样品、接待和发货分开记，我才看清一场展览不是闭馆就结束。我会先画一个做得完的小展台。等有正式方案，再请任帆看。"
                : "白明：这几项正是我容易漏的。我先照清单做一份布置草图，不急着申请大展位。以后碰到具体问题，再来问你。";
            RememberSocial("resident_baiming", text); return text;
        }
        public string BrandStoryFinishBlocker(string id)
        {
            var s = _brandStories.FirstOrDefault(x => x.Id == id); if (s == null || s.Stage >= 3) return "这段故事已收束或尚未开始。";
            if (StorySourceEnded(s)) return null;
            if (s.Kind == "design" || s.Kind == "curator")
            {
                var o = StoryExpoOrder(s);
                if (o?.Failed == true) return null;
                if (s.Kind == "design" && o?.Done == true) return null;
                if (s.Kind == "curator" && s.Stage == 2) return null;
                return s.Kind == "curator" && o?.Done == true ? "先与白明交流这次准备记录。" : "等待本届接待和展后订单的实际结果。";
            }
            if (s.Stage < 2) return "先到品牌门店准备约好的样品。";
            int wait = s.Kind == "promotion" ? 3 : s.Choice == "retest" ? 1 : 0;
            return BrandDay < s.PreparedDay + wait ? "样品交出后至少等待 " + wait + " 天，再联系复盘。" : null;
        }
        public string FinishBrandStory(string id, bool remote = false)
        {
            var s = _brandStories.FirstOrDefault(x => x.Id == id); if (s == null) return "尚未开始这段故事。";
            string why = StoryContactBlocker(s.PersonId, remote) ?? BrandStoryFinishBlocker(id); if (why != null) return why;
            bool prepared = s.Stage >= 2;
            int wait = s.Kind == "promotion" ? 3 : s.Kind == "review" && s.Choice == "retest" ? 1 : 0;
            s.Fulfilled = prepared && BrandDay >= s.PreparedDay + wait && (s.Kind == "promotion" || s.Kind == "review" || StoryExpoOrder(s)?.Done == true);
            string text;
            if (!s.Fulfilled) text = StoryName(s.PersonId) + "：这次没有完成原先约好的整套准备或交付。我们把做到哪里说清楚，已有费用和订单照原约处理。以后有合适的机会，再按实际能力安排。";
            else if (s.Kind == "promotion")
            {
                long revenue = BrandRevenue(MyShops.Where(x => x.Place.Id == s.ShopId), s.PreparedDay, BrandDay + 1);
                text = StoryName(s.PersonId) + "：这次选了" + BrandStoryChoiceLabel(s.Choice) + "。样品准备后，这家店记录的营业收入是" + Money.FromCents(revenue)
                    + "，不能都算成视频带来的。" + (revenue > 0 ? "有人买了，也要继续把交付做好。" : "暂时没有成交，我们先检查商品和来店的人合不合适，不把没卖出说成成功。")
                    + "现在这件商品还有 " + BrandProductCount(s.ShopId, s.ProductId) + " 件现货。下次合作，我会记得我们这次是怎样把话说清楚的。";
            }
            else if (s.Kind == "review") text = StoryName(s.PersonId) + (s.Choice == "retest"
                ? "：两件样品的补测整理完了。我会把测试条件和适用范围一起写进去，不能验证的部分不下结论。你愿意留出测试时间，下次我也能把准备安排得更清楚。"
                : "：我们按约保留了适用范围。少一句包办所有用途的话，观众反而知道该怎么选。它不是满分保证，这份说明会留在我们的合作记录里。");
            else if (s.Kind == "design") text = StoryName(s.PersonId) + (s.Choice == "design" ? "：这次讲设计，也有人愿意认真听过程。" : "：这次从用途和供货谈起，让买家先知道自己买的是什么。")
                + "这届展后的订单已经实际送达。你没有把现场的热闹当成全部成果，我也不用为了合作把自己的名字藏起来。下一次还可以尝试不同的展示方式。";
            else text = "任帆：白明跟我提起了你们的交流。" + (s.Choice == "share" ? "他带来了按实际准备过程整理的草图。" : "他先把清单里容易遗漏的地方补进了方案。")
                + "我会和他继续确认展台能不能做成。这次介绍源于你真的参过展、交过货，不代表他从此不用自己准备，也不替他保证订单。";
            s.Stage = 3; s.Conclusion = text;
            if (s.Fulfilled)
            {
                AddRelation(s.PersonId, 2); var creator = Creator(s.PersonId); if (creator != null) creator.Relationship = Math.Min(100, creator.Relationship + 2);
                if (s.Kind == "curator") AddRelation("resident_baiming", 2);
            }
            RememberSocial(s.PersonId, text); return text;
        }
        // Routine career updates are not recurring accusations. A work update does not invent misconduct.
        private static string BrandCareerNews(string person, int phase)
        {
            string[] news = person == "momo" ? new[] { "正在整理新手常见问题", "新教程补充了使用条件", "实用教程受到更多关注", "完成合作拍摄，整理观众反馈", "本周留出休息和备稿时间" }
                : person == "qiao" ? new[] { "整理样品测试记录", "新评测区分了不同使用场景", "详细评测受到更多关注", "正在回复读者的测试问题", "本期没有新的商务安排" }
                : person == "ake" ? new[] { "调整直播时长，准备新选题", "邀请老队友聊设备使用", "新一期访谈受到更多关注", "直播结束后整理设备清单", "安排休息，暂不增加夜场" }
                : person == "life_creator" ? new[] { "为城市系列画新草图", "整理旧作品的制作记录", "新展示受到更多关注", "检查展样与运输包装", "去旧街采风，放慢更新" }
                : new[] { "调整新一季的剪裁样稿", "整理试穿与面料反馈", "原创搭配受到更多关注", "核对合作作品署名", "工作室安排样衣整理" };
            return news[phase % news.Length];
        }
    }
}
