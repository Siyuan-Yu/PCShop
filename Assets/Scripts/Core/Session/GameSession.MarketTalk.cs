using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private sealed class MarketLead
        {
            public readonly string Industry, Subject, Source, Horizon;
            public readonly double Pressure;
            public readonly int Priority;
            public readonly bool Stock;
            public readonly PartCategory? Category;

            public MarketLead(string industry, string subject, string source, double pressure, int priority = 1,
                string horizon = "接下来几天", bool stock = false, PartCategory? category = null)
            {
                Industry = industry;
                Subject = subject;
                Source = source;
                Pressure = pressure;
                Priority = priority;
                Horizon = horizon;
                Stock = stock;
                Category = category;
            }
        }

        public string ExchangeMarketTalk(string id, bool remote = false, Place workplaceHint = null,
            Func<string, string> emphasize = null)
        {
            if (IsGameOver) return "现在不能交谈。";
            if (remote && RemoteConversationBlocker(id) is string blocker) return blocker;
            var state = CityPersonState(id);
            if (!remote && state != null && !CitySamePlace(id)) return "对方已经离开了，见面再聊吧。";
            var person = CityPerson(id);
            var npc = Npcs.Get(id);
            var staff = Rosters.Get(id) ?? Residents.Get(id)?.Person;
            if (person == null && npc == null && staff == null) return "现在找不到这个人。";
            if (!GreetPerson(id, remote)) return "现在无法和对方交谈。";

            var job = CityEmploymentOf(id);
            string workId = state?.WorkPlaceId ?? person?.WorkId ?? npc?.StallId;
            var workplace = job?.Shop?.Place ?? (!string.IsNullOrEmpty(workId) && City.HasPlace(workId) ? City.GetPlace(workId) : workplaceHint);
            string industry = ConversationMarketIndustry(workplace, job?.Shop?.RetailKind, person?.Role ?? npc?.Role ?? staff?.Role, npc?.IsTipster == true);
            int day = Clock.Today.TotalDays;
            // Repeating the question does not roll fresh information or advance the simulation.
            var talkRandom = new GameRandom(unchecked((ulong)ConversationSeed(id) * 0x9E3779B97F4A7C15UL
                ^ (ulong)day * 0xBF58476D1CE4E5B9UL));
            int Affinity(MarketLead lead)
            {
                if (lead.Industry == industry)
                    return lead.Category is PartCategory category && workplace?.Kind == PlaceKind.Wholesaler
                        && StallCarries(workplace, category) ? 3 : 2;
                if ((industry == "金融" && lead.Stock) || (industry == "物流" && !lead.Stock)) return 1;
                return 0;
            }

            var leads = MarketConversationLeads(day).Concat(StockConversationLeads(day)).ToArray();
            var familiar = leads.Where(lead => Affinity(lead) > 0).ToArray();
            var pool = leads;
            if (familiar.Length > 0)
                pool = familiar.Length == leads.Length || talkRandom.Chance(.88)
                    ? familiar : leads.Where(lead => Affinity(lead) == 0).ToArray();
            var ranked = pool.OrderByDescending(lead => Affinity(lead)).ThenByDescending(lead => lead.Priority)
                .ThenByDescending(lead => Math.Abs(lead.Pressure)).Take(3).ToArray();
            if (ranked.Length == 0)
                return "这两天没听到谁准备改报价，也没摸清后面会怎么走。";

            var signal = talkRandom.Pick(ranked);
            int connection = Affinity(signal);
            string subject = emphasize?.Invoke(signal.Subject) ?? signal.Subject;
            if (talkRandom.Chance(connection > 0 ? .05 : .19))
                return $"我听说{subject}接下来可能有变动，有人看涨，也有人说还要往下走。我还没弄清哪个版本靠得住。";

            double reliability = connection switch { 3 => .91, 2 => .84, 1 => .67, _ => .52 };
            bool accurate = talkRandom.Chance(reliability);
            bool up = (signal.Pressure > 0) == accurate;
            string source = accurate ? signal.Source : AlternateMarketLead(signal, up);
            string introduction = connection >= 2 ? "刚跟同行对过口风，" : connection == 1 ? "跟我们有来往的人提到，" : "听朋友转述，";
            string direction = up ? "往上走一些" : "往下走一些";
            string horizon = emphasize?.Invoke(signal.Horizon) ?? signal.Horizon;
            return $"{introduction}{source}。照这个说法，{horizon}{subject}可能会{direction}。";
        }

        private static string AlternateMarketLead(MarketLead signal, bool up)
        {
            if (signal.Stock)
            {
                if (signal.Industry == CityInvestments.SoftwareServicesIndustry)
                    return up ? "有客户在追加软件许可，已经问到部署档期了"
                        : "有客户把系统验收往后推了，据说这季能确认的收入要重算";
                return up ? "有人说这家公司在谈一笔额外的渠道单，交货时间都在问了"
                    : "有人说几个大客户把订单往后推，公司正在重算这季的收入";
            }
            if (signal.Industry == "地产")
                return up ? "有几户房东在试探着提挂牌价，等下一轮一起改"
                    : "挂了很久的几套终于肯松口，好像打算一起把挂牌价放低";
            return up ? "有几个档口反映下一批分到的货比预想少，正在重排供货"
                : "有几个档口提到一批余货准备放出来，正问下游能接多少";
        }

        private IEnumerable<MarketLead> MarketConversationLeads(int today)
        {
            var news = IndustryCycles.CommodityEventOn(today + 4, Market.CommoditySeed);
            bool nearMajorMove = news.IsVolatile(Math.Max(today + 1, news.StartDay));
            if (nearMajorMove && Enum.TryParse<PartCategory>(news.Key, out var affectedPart))
            {
                int nextMoveDay = Math.Max(today + 1, news.StartDay);
                double pressure = IndustryCycles.CommodityEventFactor(news.Key, nextMoveDay, Market.CommoditySeed)
                    / IndustryCycles.CommodityEventFactor(news.Key, nextMoveDay - 1, Market.CommoditySeed) - 1;
                yield return new MarketLead("硬件", news.Name + "批发价", news.Headline, pressure, 5, category: affectedPart);
            }

            foreach (var category in Market.PendingShocks.Where(shock => shock.day > today && shock.day <= today + 4)
                .GroupBy(shock => shock.category))
            {
                int firstDay = category.Min(shock => shock.day);
                double pressure = category.Where(shock => shock.day == firstDay).Sum(shock => shock.delta);
                if (Math.Abs(pressure) < .005) continue;
                string item = PartCatalog.CategoryName(category.Key);
                string source = pressure > 0 ? $"上游开始把{item}的下一批配额往回收，几个档口还没拿到足量的确认单"
                    : $"仓库在给一批积存的{item}排出库，已经在问档口愿不愿意接";
                yield return new MarketLead("硬件", item + "批发价", source, pressure, 3, category: category.Key);
            }

            foreach (string series in ModelProducts.SeriesIds)
            {
                double current = IndustryCycles.ModelFactor(series, today, Market.CommoditySeed);
                double pressure = IndustryCycles.ModelFactor(series, today + 4, Market.CommoditySeed) / current - 1;
                if (Math.Abs(pressure) < .005) continue;
                string name = ModelProducts.SeriesName(series);
                bool affected = nearMajorMove && news.Key == series;
                string source = affected ? news.Headline : pressure > 0
                    ? $"有店家在加订{name}的模型，工坊开始排补单"
                    : $"有店家准备给{name}的现货做折扣，正在问同行的报价";
                yield return new MarketLead("文创", name + "的模型行情", source, pressure, affected ? 5 : 2);
            }

            foreach (var (industry, business, subject, goods) in new[]
            {
                ("餐饮", FarmBusiness, "食材批发价", "农贸的食材"),
                ("餐饮", FastFoodBusiness, "外带食品的进货价", "外带食品"),
                ("零售", Gifts.TobaccoBusiness, "烟酒饮料的批发价", "烟酒饮料"),
                ("零售", "花店", "鲜花批发价", "花卉"),
                ("服装", ClothingProducts.Business, "服装批发价", "服装"),
                ("宠物", PetShopCatalog.SupplierBusiness, "宠物用品的进货价", "宠物用品")
            })
            {
                double Factor(int day) => IndustryCycles.WholesaleMarketFactor(business, day, Market.CommoditySeed);
                double pressure = Factor(today + 4) / Factor(today) - 1;
                if (Math.Abs(pressure) < .006) continue;
                bool affected = nearMajorMove && news.Key == business;
                string source = affected ? news.Headline : pressure > 0 ? $"做{goods}的供货商在收紧新一批的折扣，有些单子要重报"
                    : $"做{goods}的批发商正在问谁能接下批货，愿意给接单的店多让一点";
                yield return new MarketLead(industry, subject, source, pressure, affected ? 5 : 2);
            }

            foreach (var competition in ActiveCityCompetition("terms").Where(e => e.Response == "small" && e.EndDay <= today + 4))
            {
                string industry = ConversationMarketIndustry(null, competition.RetailKind, null, false);
                string supplier = City.HasPlace(competition.SupplierId) ? City.GetPlace(competition.SupplierId).Name : "那家供货商";
                yield return new MarketLead(industry, supplier + "的小单批发价",
                    $"{supplier}原先的小单加价快到期，有人问下批货时已经在谈撤掉这笔加价", 1 / 1.08 - 1, 3);
            }

            int propertyDay = (_propertyPriceWeek + 1) * GameDate.DaysPerWeek;
            if (propertyDay > today && propertyDay <= today + GameDate.DaysPerWeek)
            {
                var preview = new GameRandom(_propertyPriceRandom.State);
                for (int i = 0; i < _propertyPriceFactors.Length; i++)
                {
                    double current = _propertyPriceFactors[i];
                    double pressure = NextPropertyPriceFactor(current, i, preview) / current - 1;
                    if (Math.Abs(pressure) < .006) continue;
                    string subject = i switch
                    {
                        0 => "住宅挂牌价", 1 => "住宅挂牌周租", 2 => "商铺挂牌价", _ => "商铺挂牌周租"
                    };
                    string source = pressure > 0 ? "有房东向中介摸下一轮的底价，几套挂出去试水的都把要价抬了些"
                        : "有几套房源谈了几轮没签，房东已经向中介打听降一点能不能更快出手";
                    yield return new MarketLead("地产", subject, source, pressure, 2, "下轮改挂牌时");
                }
            }
        }

        private IEnumerable<MarketLead> StockConversationLeads(int today)
        {
            long now = Clock.Now.TotalMinutes;
            var nextQuotes = new List<long>();
            int tradingDays = 0;
            for (int day = today; tradingDays < 3; day++)
            {
                if (new GameDate(day).IsWeekend || day == today && Clock.MinuteOfDay >= StockSlots[StockSlots.Length - 1]) continue;
                tradingDays++;
                foreach (int slot in StockSlots)
                    if (slot >= 600 && day * 1440L + slot > now) nextQuotes.Add(day * 1440L + slot);
            }
            foreach (var company in CityInvestments.All)
            {
                var equity = StockState(company.Id);
                if (equity == null || equity.PriceCents <= 0) continue;
                string subject = company.Name + "的股价";
                StockCompanyChange? change = null;
                foreach (long quote in nextQuotes)
                {
                    change = ScheduledStockCompanyChange(company.Id, quote);
                    if (change.HasValue) break;
                }
                if (change is StockCompanyChange scheduled)
                    yield return new MarketLead(company.Industry, subject, StockRumorSource(company, scheduled),
                        StockAnnouncementShock(scheduled), 4, "接下来几个交易日", stock: true);
                else
                {
                    double pressure = StockFundamentalDrift(equity, company, (int)(nextQuotes[0] / 1440));
                    if (Math.Abs(pressure) < .003) continue;
                    string source = pressure > 0 ? $"有人按{company.Name}的经营情况重算了一遍，觉得后面能赚的钱可能比现在的股价反映的多"
                        : $"有人在重新算{company.Name}的经营账，说现在的股价对后续利润估得有点乐观";
                    yield return new MarketLead(company.Industry, subject, source, pressure, 1, "接下来几个交易日", stock: true);
                }
            }
        }

        private static string StockRumorSource(CityEquityDefinition company, StockCompanyChange change)
        {
            string name = company.Name;
            if (company.Industry == CityInvestments.SoftwareServicesIndustry)
                return name + SoftwareServicesAnnouncement(change).Rumor;
            return change switch
            {
                StockCompanyChange.NewLocations => $"有人接到{name}的新门店物料单，连招人和开张的时间都在对",
                StockCompanyChange.HigherCosts => $"给{name}供货的人说，新一轮采购报价已经报上去了，人工预算也在往上调",
                StockCompanyChange.ChannelOrders => $"跟{name}合作的渠道商正在腾仓位，据说有一批新订单等着确认",
                StockCompanyChange.Returns => $"{name}的售后点临时加派了人手，经销商说恐怕有一批货要退回来处理",
                StockCompanyChange.Efficiency => $"给{name}供货的人说，新排班和到货流程已经试过一轮，积压和损耗少了些",
                _ => $"有人收到{name}缩减几个业务点的通知，原先计划的后续订单也先撤了"
            };
        }

        private static string ConversationMarketIndustry(Place workplace, string shopKind, string role, bool tipster)
        {
            string shopIndustry = shopKind switch
            {
                "computer" => "硬件", "clothing" => "服装", "models" or "model" => "文创",
                "pet" => "宠物", "restaurant" or "food" or "produce" => "餐饮", "flowers" or "secondhand" => "零售", _ => null
            };
            if (shopIndustry != null) return shopIndustry;
            if (CityInvestments.Get(workplace?.Id) is CityEquityDefinition listedCompany) return listedCompany.Industry;
            if (tipster || workplace?.Kind == PlaceKind.Wholesaler) return "硬件";
            if (workplace?.Kind == PlaceKind.Agency) return "地产";
            if (workplace?.Kind is PlaceKind.Dealer or PlaceKind.Garage) return "汽车";
            string job = (workplace?.Business ?? "") + " " + (workplace?.Name ?? "") + " " + (role ?? "");
            if (job.Contains("银行") || job.Contains("证券") || job.Contains("理财")) return "金融";
            if (job.Contains("硬件") || job.Contains("电脑") || job.Contains("电子") || job.Contains("手机") || job.Contains("显卡")) return "硬件";
            if (job.Contains("服装") || job.Contains("面料")) return "服装";
            if (job.Contains("潮玩") || job.Contains("模型") || job.Contains("文创") || job.Contains("展会") || job.Contains("会展")) return "文创";
            if (job.Contains("宠物") || job.Contains("饲养")) return "宠物";
            if (job.Contains("家具")) return "家具";
            if (job.Contains("餐") || job.Contains("厨") || job.Contains("酒吧") || job.Contains("农贸") || job.Contains("食材")) return "餐饮";
            if (job.Contains("车行") || job.Contains("赛车") || job.Contains("汽车") || job.Contains("改车")) return "汽车";
            if (job.Contains("物流") || job.Contains("货车") || job.Contains("配送") || job.Contains("骑手")) return "物流";
            if (job.Contains("房东") || job.Contains("中介") || job.Contains("地产")) return "地产";
            return job.Contains("零售") || job.Contains("便利") || job.Contains("杂货") || job.Contains("超市") ? "零售" : null;
        }
    }
}
