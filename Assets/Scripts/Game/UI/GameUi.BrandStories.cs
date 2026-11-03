using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildBrandStories(Transform body, Window w, BrandRecord brand)
        {
            Para(body, "在真实合作中认识彼此。故事可选，准备样品会扣库存；交付与合同仍按原规则结算。", 16, Ui.Dim);
            var stories = S.BrandStories.Concat(S.BrandStoryOffers()).Where(s => s.BrandId == brand.Id).ToArray();
            if (stories.Length == 0) Para(body, "与莫文宁或乔雨桐签署推广后可聊合作；报名展会后可找任帆，模型与服装品牌还可找陆栀或温岚。", 18);
            foreach (var story in stories)
            {
                var s = story; var card = BusinessCard(body, GameSession.BrandStoryTitle(s.Kind));
                Line(card, (S.CityPerson(s.PersonId)?.Name ?? s.PersonId) + " · " + (s.Stage == 3 ? "已收束" : S.BrandStoryObjective(s)), 17);
                if (s.Stage == 3)
                {
                    Ui.Size(Ui.Button(card, "回看这次合作", () => OpenWindow(GameSession.BrandStoryTitle(s.Kind), 780, 500, detail => Para(detail.Content, s.Conclusion, 19)), 17), -1, 36);
                    continue;
                }
                string person = S.BrandStorySpeaker(s);
                Ui.Size(Ui.Button(card, "联系" + S.CityPerson(person)?.Name, () => OpenBrandContact(person), 17), -1, 38);
                if (person != s.PersonId)
                    Ui.Size(Ui.Button(card, "联系任帆 · 了解准备情况", () => OpenBrandContact(s.PersonId), 16), -1, 34);
                if (s.Stage == 1 && s.Kind != "curator")
                {
                    string shop, product;
                    if (s.Kind == "design")
                    {
                        shop = s.ShopId; product = s.ProductId;
                        Line(card, "报名展品：" + S.BrandProductName(product), 17);
                    }
                    else
                    {
                        BuildBrandStockChoice(card, w, brand);
                        shop = _brandShop; product = _brandProduct;
                    }
                    if (shop != null && S.City.HasPlace(shop))
                        Ui.Size(Ui.Button(card, "前往样品门店 · " + S.City.GetPlace(shop).Name, () => { CloseAllWindows(); S.BeginCityTravel(shop); }, 16), -1, 36);
                    string[] choices = s.Kind == "promotion" ? new[] { "value", "service" } : s.Kind == "review" ? new[] { "disclose", "retest" } : new[] { "design", "buyers" };
                    foreach (string choice in choices)
                    {
                        string action = choice, sourceShop = shop, sourceProduct = product;
                        string why = S.BrandStoryPreparationBlocker(s.Id, shop, product, choice);
                        var button = Ui.Button(card, GameSession.BrandStoryChoiceLabel(choice), () =>
                        {
                            string cost = s.Kind == "design" ? "确认本届展示方向，不重复扣除报名样品。" : "从这家店扣除 " + (action == "retest" ? 2 : 1) + " 件现货作为样品，不计为销售。";
                            ConfirmBusiness("确认样品与合作方案", S.City.GetPlace(sourceShop).Name + "\n" + S.BrandProductName(sourceProduct) + "\n" + cost,
                                () => { S.PrepareBrandStory(s.Id, sourceShop, sourceProduct, action); w.Rebuild(); });
                        }, 17, Ui.ButtonBg, why == null);
                        Ui.Size(button, -1, 38); if (why != null) button.Tip(why);
                    }
                    var reason = S.BrandStoryPreparationBlocker(s.Id, shop, product, choices[0]);
                    if (reason != null) Para(card, reason, 15, Ui.Dim);
                }
            }
        }

        private void AddBrandStoryDialogue(System.Collections.Generic.List<TalkOpt> options, string personId, bool remote, System.Action<string> reply)
        {
            var topics = S.BrandStoryTopics(personId).ToArray();
            if (topics.Length > 0)
                options.Add(new TalkOpt { Label = "查看品牌合作故事与样品准备", Act = () => {
                    CloseAllWindows(); _brandTab = 5; _brandId = topics[0].BrandId; OpenBrandBusiness();
                } });
            foreach (var story in topics)
            {
                string id = story.Id;
                string brandName = S.BrandBusiness.Brands.FirstOrDefault(b => b.Id == story.BrandId)?.Name ?? "品牌";
                if (story.Stage == 0)
                    options.Add(new TalkOpt { Label = brandName + " · " + GameSession.BrandStoryTitle(story.Kind), Hot = true, Act = () => reply(S.BeginBrandStory(id, remote)) });
                else if (story.Kind == "curator" && story.Stage == 1 && personId == "resident_baiming")
                {
                    foreach (string choice in new[] { "share", "brief" })
                    {
                        string c = choice;
                        options.Add(new TalkOpt { Label = GameSession.BrandStoryChoiceLabel(c), Blocker = S.ShareBrandExpoBlocker(id, remote), Act = () => reply(S.ShareBrandExpoStory(id, c, remote)) });
                    }
                }
                else
                {
                    var copy = story;
                    options.Add(new TalkOpt { Label = "合作进度 · " + GameSession.BrandStoryTitle(story.Kind), Act = () => reply(S.BrandStoryText(copy) + "\n\n下一步：" + S.BrandStoryObjective(copy)) });
                    options.Add(new TalkOpt { Label = "复盘 · " + brandName, Blocker = S.BrandStoryFinishBlocker(id), Act = () => reply(S.FinishBrandStory(id, remote)) });
                }
            }
        }
    }
}
