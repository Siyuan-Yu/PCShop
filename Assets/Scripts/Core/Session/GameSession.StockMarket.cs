using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public readonly struct StockBatchQuote
    {
        public readonly Money Net, Fees, CashAfter, Reserve;
        public readonly string Blocker;
        public readonly bool UsesReserve;

        public StockBatchQuote(Money net, Money fees, Money cashAfter, Money reserve, string blocker, bool usesReserve)
        {
            Net = net;
            Fees = fees;
            CashAfter = cashAfter;
            Reserve = reserve;
            Blocker = blocker;
            UsesReserve = usesReserve;
        }
    }

    public sealed partial class GameSession
    {
        private StockMarketState _stockMarket = new StockMarketState();
        private static readonly int[] StockSlots = {0,540,600,780,960};
        private enum StockCompanyChange { NewLocations, HigherCosts, ChannelOrders, Returns, Efficiency, Cuts }
        public const int StockEarlyMarketDays = 30;
        public const int StockDailyLimitPercent = 20;
        private const double EarlyStockSentimentMultiplier = 2.4;
        private const double LaterStockSentimentMultiplier = 4;
        private const double EarlyStockAnnouncementMultiplier = 1.5;
        private const double LaterStockAnnouncementMultiplier = 2.25;
        public const int StockLotSize = 1;
        public const int StockOrderLimit = 1_000_000;
        public const double StockBorrowDailyRate = .0003;
        public const double StockMaintenanceRatio = 1.3;
        public bool StockMarketOpen => !Clock.Today.IsWeekend;
        public IReadOnlyList<StockNews> StockNewsItems => _stockMarket.News;
        public Money ShortPositionEquity => Money.FromCents(_cityEquities.Sum(e=>e.ShortCollateralCents-e.PriceCents*e.ShortUnits));
        public Money StockFrozenMargin => Money.FromCents(_cityEquities.Sum(e=>e.ShortMarginCents));
        public Money StockShortUnrealized => Money.FromCents(_cityEquities.Sum(e=>e.ShortBasisCents-e.PriceCents*e.ShortUnits));
        public Money StockShortRealized => Money.FromCents(_cityEquities.Sum(e=>e.ShortRealizedCents));
        public Money StockTotalRealized => CityEquityRealized+Money.FromCents(CityFundRealizedCents)+StockShortRealized;
        public CityEquityState StockState(string id) => _cityEquities.Find(e=>e.Id==id);
        public StockQuote[] StockQuotes(string id) => id=="hardware"?_stockMarket.IndexQuotes:StockState(id)?.Quotes??Array.Empty<StockQuote>();
        public StockExecution[] StockExecutions(string id, long from, bool shortMode) =>
            _stockMarket.Executions.Where(e => e.StockId == id && e.Minute >= from && e.Minute <= Clock.Now.TotalMinutes
                && shortMode == (e.Kind is StockExecutionKind.Short or StockExecutionKind.Cover or StockExecutionKind.ForcedCover)).ToArray();

        private void RecordStockExecution(string id, StockExecutionKind kind, long minute, long priceCents, long units) =>
            _stockMarket.Executions.Add(new StockExecution { StockId = id, Kind = kind, Minute = minute, PriceCents = priceCents, Units = units });

        private void RestoreLegacyStockExecutions()
        {
            foreach (var entry in Ledger.Entries)
            {
                bool buy = entry.Category == "证券投资", sell = entry.Category == "证券赎回";
                if ((!buy && !sell) || entry.SourceEventId == null) continue;
                var source = entry.SourceEventId.Split(':');
                if (source.Length != 4 || source[0] != "investment" || source[1] != entry.Attribution.PropertyId
                    || !long.TryParse(source[2], out long minute) || minute < 0 || minute / 1440 != entry.Date.TotalDays
                    || minute > Clock.Now.TotalMinutes || (buy ? !entry.Amount.IsNegative : !entry.Amount.IsPositive)) continue;

                string id = source[1];
                var quote = StockQuotes(id).LastOrDefault(q => q.Minute <= minute);
                if (quote == null || quote.PriceCents <= 0) continue;
                bool hardware = id == "hardware";
                long cents = entry.Amount.Abs().Cents;
                int feePerThousand = hardware ? 5 : 2;
                decimal estimatedUnits = Math.Round(cents * 1000m / ((decimal)quote.PriceCents * (1000 + (buy ? feePerThousand : -feePerThousand))));
                if (estimatedUnits < 1 || estimatedUnits > StockOrderLimit) continue;
                long units = (long)estimatedUnits;
                var gross = Money.FromCents(quote.PriceCents) * units;
                var fee = CityInvestments.Fee(gross, hardware);
                if ((buy ? gross + fee : gross - fee).Cents != cents) continue;
                RecordStockExecution(id, buy ? StockExecutionKind.Buy : StockExecutionKind.Sell, minute, quote.PriceCents, units);
            }
            _stockMarket.Version = StockMarketState.ExecutionHistoryVersion;
        }
        public bool StockWatched(string id) => id=="hardware"?_stockMarket.IndexWatched:StockState(id)?.Watched==true;
        public void ToggleStockWatch(string id)
        {if(id=="hardware")_stockMarket.IndexWatched=!_stockMarket.IndexWatched;else if(StockState(id)!=null)StockState(id).Watched=!StockState(id).Watched;}
        public long StockAvailable(string id) => id=="hardware"?_stockMarket.IndexAvailableUnits:StockState(id)?.AvailableUnits??0;
        public long StockBorrowAvailable(string id) => ListedCompanyOf(id) != null ? 0 : Math.Max(0,(StockDefinition(id)?.Liquidity??0)/2-(StockState(id)?.ShortUnits??0));
        public long StockPreviousClose(string id)
        {
            // 展示基准跟随最新报价所属的交易日，不能因开市重置限价基准而提前归零。
            // 同时从历史报价恢复，已有存档在首笔报价前或休市时也能正确显示。
            var quotes = StockQuotes(id);
            var latest = quotes.LastOrDefault(quote => quote.PriceCents > 0 && quote.Minute <= Clock.Now.TotalMinutes);
            if (latest != null)
            {
                long dayStart = latest.Minute / GameTimestamp.MinutesPerDay * GameTimestamp.MinutesPerDay;
                var previous = quotes.LastOrDefault(quote => quote.PriceCents > 0 && quote.Minute < dayStart);
                if (previous != null) return previous.PriceCents;
                // 游戏刚开始还没有上一交易日，以初始挂牌价为基准。
                var initial = quotes.FirstOrDefault(quote => quote.PriceCents > 0);
                if (initial != null) return initial.PriceCents;
            }
            return id == "hardware" ? _stockMarket.IndexPreviousCloseCents : StockState(id)?.PreviousCloseCents ?? 0;
        }
        public double StockDayChange(string id) => StockPreviousClose(id)>0?CityInvestmentPrice(id).Cents/(double)StockPreviousClose(id)-1:0;
        public double StockMarginRatio(string id)
        {var e=StockState(id);return e==null||e.ShortUnits<=0?0:e.ShortCollateralCents/(double)(e.PriceCents*e.ShortUnits);}
        public string StockTradingStatus => StockMarketOpen ? "工作日全天可交易" : "周末休市";
        public string StockNextQuote
        {
            get
            {
                for(int day=Clock.Today.TotalDays;day<=Clock.Today.TotalDays+7;day++)
                    if(!new GameDate(day).IsWeekend)
                        foreach(int slot in new[]{600,780,960})
                            if(day*1440L+slot>Clock.Now.TotalMinutes)return new GameDate(day)+" "+ClockText(slot);
                return "下一交易日";
            }
        }
        private string StockName(string id) => id=="hardware"?"硬件景气指数":StockDefinition(id)?.Name??id;
        private string StockEffect(string id,string action,long minute) => "stock:"+id+":"+action+":"+minute+":"+_cityAssetEffects.Count;
        private static long StockPortion(long total,long quantity,long held) => quantity==held?total:(long)((decimal)total*quantity/held);
        private void InitializeStockMarket(ulong seed)
        {
            _cityEquityRandom.State=new GameRandom(seed^0x75BA927CD315UL).State;
            _stockMarket.CompanyEventSeed = StockEventSeed(seed);
            _stockMarket.LastMinute=Clock.Now.TotalMinutes;
            _stockMarket.IndexPriceCents=HardwareMarketIndex.Cents;
            _stockMarket.IndexPreviousCloseCents=_stockMarket.IndexPriceCents;
            _stockMarket.IndexQuotes=new[]{new StockQuote{Minute=Clock.Now.TotalMinutes,PriceCents=_stockMarket.IndexPriceCents}};
            foreach(var e in _cityEquities)InitializeStockCompany(e);
        }
        private void InitializeStockCompany(CityEquityState e)
        {
            var d=StockDefinition(e.Id);
            e.PreviousCloseCents=e.PreviousCloseCents>0?e.PreviousCloseCents:e.PriceCents;
            if(e.BusinessScale<=0)e.BusinessScale=1;
            if(e.Quotes==null||e.Quotes.Length==0)
            {
                if(e.OperatingMargin==0)e.OperatingMargin=d.Margin;
                e.AvailableUnits=d.Liquidity;
                e.Quotes=new[]{new StockQuote{Minute=Clock.Now.TotalMinutes,PriceCents=e.PriceCents}};
            }
            e.LastReason=e.LastReason??"等待下一次企业经营披露";
        }
        private void AddStockNews(string id,long minute,string headline,string detail)
        {
            _stockMarket.News=_stockMarket.News.Concat(new[]{new StockNews{StockId=id,Minute=minute,Headline=headline,Detail=detail}}).Reverse().Take(100).Reverse().ToArray();
        }
        private static StockQuote[] AppendStockQuote(StockQuote[] quotes,long minute,long cents) =>
            (quotes??Array.Empty<StockQuote>()).Concat(new[]{new StockQuote{Minute=minute,PriceCents=cents}}).Reverse().Take(270).Reverse().ToArray();
        // Quotes and settlements are driven only by world time. Reading a page never consumes randomness.
        public void TickCityAssets()
        {
            long now=Clock.Now.TotalMinutes;
            if(_stockMarket.LastMinute<0){_stockMarket.LastMinute=now;return;}
            if(now<=_stockMarket.LastMinute)return;
            int first=(int)(_stockMarket.LastMinute/1440),last=(int)(now/1440);
            for(int day=first;day<=last;day++)
                foreach(int slot in StockSlots)
                {
                    long minute=day*1440L+slot;
                    if(minute<=_stockMarket.LastMinute||minute>now)continue;
                    if(slot==0)SettleStockDay(day,minute);
                    else if(!new GameDate(day).IsWeekend)
                    {
                        if(slot==540)OpenStockDay(day,minute);
                        else RepriceStocks(day,minute);
                    }
                }
            _stockMarket.LastMinute=now;
        }
        private double StockIndustryFactor(CityEquityDefinition d,int day)
        {
            if(d.Industry=="硬件")return Math.Max(.6,Math.Min(1.5,HardwareMarketIndex.Cents/10000d));
            double seasonal=Math.Sin((day%360)/360d*Math.PI*2+(d.Industry=="服装"?1:0))*.08;
            double expo=d.Industry=="文创"&&day%30>=15&&day%30<=23?.08:0;
            return 1+seasonal+expo;
        }
        private void OpenStockDay(int day,long minute)
        {
            _stockMarket.IndexPreviousCloseCents=_stockMarket.IndexPriceCents;
            _stockMarket.IndexAvailableUnits=40000;
            foreach(var e in _cityEquities)
            {
                var d=StockDefinition(e.Id);e.PreviousCloseCents=e.PriceCents;e.AvailableUnits=d.Liquidity;
                if (ListedCompanyOf(e.Id) is CompanyRecord company)
                {
                    var accounts = CompanyAccounts(company, day - 1, day);
                    e.YesterdayRevenueCents = accounts.Revenue.Cents;
                    e.YesterdayProfitCents = accounts.Revenue.Cents - accounts.Costs.Cents;
                    e.QuarterProfitCents += e.YesterdayProfitCents;
                    long turnover = Math.Max(accounts.Revenue.Cents, accounts.Costs.Cents);
                    e.OperatingMargin = turnover > 0 ? e.YesterdayProfitCents / (double)turnover : 0;
                    e.LastReason = e.YesterdayProfitCents >= 0 ? "所属门店经营净额改善报价" : "所属门店经营成本压低报价";
                    if (day % 7 == 0) AddStockNews(e.Id, minute, "经营周报：" + d.Name,
                        $"实际门店营收 {accounts.Revenue}，经营净额 {accounts.Revenue - accounts.Costs}。");
                    continue;
                }
                double baseline=d.RivalKind==null?d.DailyRevenueYuan:BrandRivalMonthlySales(d.RivalKind,day)/30d;
                double audience=d.RivalKind==null?1:1+Math.Min(.15,(_kol.Rivals.FirstOrDefault(r=>r.Kind==d.RivalKind)?.Influence??0)*.0005);
                e.YesterdayRevenueCents=(long)Math.Round(baseline*100*e.BusinessScale*StockIndustryFactor(d,day)*audience*_cityEquityRandom.Range(.94,1.06));
                e.YesterdayProfitCents=(long)Math.Round(e.YesterdayRevenueCents*e.OperatingMargin);
                e.QuarterProfitCents+=e.YesterdayProfitCents;
                double growth=e.YesterdayRevenueCents/(baseline*100)-1;
                e.LastReason=(growth>.04?"销售需求较旺":growth<-.04?"销售需求偏弱":"销售需求平稳")
                    +"；"+(e.OperatingMargin<0?"运营成本高于营收":e.OperatingMargin<d.Margin*.7?"成本压力增加":"保持经营盈利");
                if(day%7==0)AddStockNews(e.Id,minute,"经营周报："+d.Name,e.LastReason+"。最近披露日营收 "+Money.FromCents(e.YesterdayRevenueCents)+"，利润 "+Money.FromCents(e.YesterdayProfitCents)+"。");
            }
        }
        private static int StockEventSeed(ulong seed) => unchecked((int)(seed ^ (seed >> 32))) | 1;

        // The rumor and the future quote consult the same announcement schedule.
        private StockCompanyChange? ScheduledStockCompanyChange(string id, long minute)
        {
            ulong key = unchecked((ulong)(uint)_stockMarket.CompanyEventSeed * 0x9E3779B97F4A7C15UL
                ^ (ulong)minute * 0xBF58476D1CE4E5B9UL ^ (ulong)(uint)ConversationSeed(id) * 0x94D049BB133111EBUL);
            var eventRandom = new GameRandom(key);
            if (!eventRandom.Chance(.035)) return null;
            return (StockCompanyChange)eventRandom.Next(0, 6);
        }

        private static double StockAnnouncementShock(StockCompanyChange change) => change switch
        {
            StockCompanyChange.NewLocations => .018,
            StockCompanyChange.HigherCosts => -.035,
            StockCompanyChange.ChannelOrders => .028,
            StockCompanyChange.Returns => -.04,
            StockCompanyChange.Efficiency => .02,
            _ => -.012
        };

        private static (string Headline, string Detail, string Rumor) SoftwareServicesAnnouncement(StockCompanyChange change) => change switch
        {
            StockCompanyChange.NewLocations => ("增设实施交付中心",
                "新中心将承接更多企业软件上线项目，前期招聘和场地投入压低近期利润率。",
                "在筹建新的实施交付中心，已经开始招聘实施工程师"),
            StockCompanyChange.HigherCosts => ("云资源和运维成本上涨",
                "云资源合同续约价格上调，工程师值班支出也有所增加，近期利润率承压。",
                "的新一轮云资源报价变高了，运维团队也在重算值班预算"),
            StockCompanyChange.ChannelOrders => ("签下企业软件合同",
                "公司签下企业管理系统的部署和运维合同，预计增加项目收入，款项将按里程碑验收确认。",
                "和几家企业谈到部署日期了，据说软件和运维合同很快能签下来"),
            StockCompanyChange.Returns => ("核心项目验收延期",
                "一批新系统未能按期通过客户验收，收入确认推迟，返工增加了交付成本。",
                "有几个系统在客户验收时被退回整改，项目组正重排上线时间"),
            StockCompanyChange.Efficiency => ("交付流程标准化",
                "通用部署和测试工具已在项目中使用，重复实施工时减少，近期利润率改善。",
                "已在几个项目试用自动部署工具，原先要重复做的上线工作少了许多"),
            _ => ("裁撤低毛利定制项目",
                "公司停止承接部分长期亏损的定制项目并缩减相关岗位，短期营收规模收缩，经常性支出减少。",
                "正在缩减定制业务，有项目组接到调整人员安排的通知")
        };

        private void PublishStockCompanyChange(CityEquityState e, CityEquityDefinition d, long minute, StockCompanyChange change)
        {
            string headline,detail;
            switch(change)
            {
                case StockCompanyChange.NewLocations:e.BusinessScale=Math.Min(2.5,e.BusinessScale*1.05);e.OperatingMargin=Math.Max(-.08,e.OperatingMargin-.012);
                    headline="新增营业网点";detail="公司投入新网点，销售规模扩大，新增租金与人工也压低了近期利润率。";break;
                case StockCompanyChange.HigherCosts:e.OperatingMargin=Math.Max(-.08,e.OperatingMargin-.025);
                    headline="成本上涨";detail="采购与人工支出增加，公司近期利润率下调。";break;
                case StockCompanyChange.ChannelOrders:e.BusinessScale=Math.Min(2.5,e.BusinessScale*1.04);
                    headline="新增渠道订单";detail="公司披露一批新增渠道订单，需求改善，最终收益仍取决于履约与成本。";break;
                case StockCompanyChange.Returns:e.BusinessScale=Math.Max(.4,e.BusinessScale*.96);e.OperatingMargin=Math.Max(-.08,e.OperatingMargin-.008);
                    headline="产品售后增加";detail="近期售后问题增加，订单和利润率承压，公司开始整改。";break;
                case StockCompanyChange.Efficiency:e.OperatingMargin=Math.Min(.2,e.OperatingMargin+.015);
                    headline="运营效率改善";detail="公司调整供应和人员安排，近期利润率有所改善。";break;
                default:e.BusinessScale=Math.Max(.4,e.BusinessScale*.97);e.OperatingMargin=Math.Min(.2,e.OperatingMargin+.008);
                    headline="收缩低效业务";detail="公司缩减部分低效业务，营收规模减少，同时降低了运营负担。";break;
            }
            if (d.Industry == CityInvestments.SoftwareServicesIndustry)
                (headline, detail, _) = SoftwareServicesAnnouncement(change);
            e.LastReason=headline+"；"+detail;
            AddStockNews(e.Id,minute,d.Name+"："+headline,detail+" 市场预期和短期交易情绪也会影响价格。");
        }

        private double StockFundamentalDrift(CityEquityState equity, CityEquityDefinition company, int day)
        {
            double profitability = Math.Max(.35, Math.Min(1.6, .6 + equity.OperatingMargin / company.Margin * .4));
            double fair = company.BaseCents * equity.BusinessScale * StockIndustryFactor(company, day) * profitability;
            return Math.Max(-.025, Math.Min(.025, (fair / equity.PriceCents - 1) * .035));
        }

        private void RepriceStocks(int day,long minute)
        {
            bool earlyMarket = day < StockEarlyMarketDays;
            double sentimentMultiplier = earlyMarket ? EarlyStockSentimentMultiplier : LaterStockSentimentMultiplier;
            double announcementMultiplier = earlyMarket ? EarlyStockAnnouncementMultiplier : LaterStockAnnouncementMultiplier;
            foreach(var e in _cityEquities)
            {
                var d=StockDefinition(e.Id);
                bool own = ListedCompanyOf(e.Id) != null;
                // Gradual mean reversion plus public fundamentals and unpredictable market sentiment.
                double shock = own ? Math.Max(-.03, Math.Min(.03, e.OperatingMargin * .04)) : StockFundamentalDrift(e, d, day);
                if (!own && ScheduledStockCompanyChange(e.Id, minute) is StockCompanyChange change)
                {
                    PublishStockCompanyChange(e, d, minute, change);
                    shock += StockAnnouncementShock(change) * announcementMultiplier;
                }
                if (!own) e.OperatingMargin+=(d.Margin-e.OperatingMargin)*.005;
                double volatility = d.Volatility * sentimentMultiplier;
                shock+=_cityEquityRandom.Range(-volatility,volatility);
                long next = (long)Math.Round(e.PriceCents * (1 + shock));
                long maxMove = e.PreviousCloseCents * StockDailyLimitPercent / 100;
                long dailyLimited = Math.Max(e.PreviousCloseCents - maxMove, Math.Min(e.PreviousCloseCents + maxMove, next));
                e.PriceCents = Math.Max(100, Math.Min(1_000_000, dailyLimited));
                e.AvailableUnits=d.Liquidity;
                e.Quotes=AppendStockQuote(e.Quotes,minute,e.PriceCents);
                e.History=e.Quotes.Select(q=>q.PriceCents).ToArray();
                CheckStockMargin(e,minute);
            }
            double indexDrift=Math.Max(-.025,Math.Min(.025,(HardwareMarketIndex.Cents/(double)_stockMarket.IndexPriceCents-1)*.15));
            long index=(long)Math.Round(_stockMarket.IndexPriceCents*(1+indexDrift)+_cityEquityRandom.Range(-30,30));
            _stockMarket.IndexPriceCents=Math.Max(100,Math.Max((long)Math.Ceiling(_stockMarket.IndexPreviousCloseCents*.9),Math.Min((long)Math.Floor(_stockMarket.IndexPreviousCloseCents*1.1),index)));
            _stockMarket.IndexAvailableUnits=40000;
            _stockMarket.IndexQuotes=AppendStockQuote(_stockMarket.IndexQuotes,minute,_stockMarket.IndexPriceCents);
            _cityFundHistory=_stockMarket.IndexQuotes.Select(q=>q.PriceCents).ToArray();
        }
        private void SettleStockDay(int day,long minute)
        {
            foreach(var e in _cityEquities)
            {
                if(e.ShortUnits>0)
                {
                    long fee=Math.Max(1,(long)Math.Ceiling(e.PriceCents*e.ShortUnits*StockBorrowDailyRate));
                    ChargeStockShortCost(e,fee,"借股费",minute);
                }
                if(day>0&&day%90==0)
                {
                    if (ListedCompanyOf(e.Id) != null) { e.QuarterProfitCents = 0; continue; }
                    // One million issued shares, distributing 25% of accumulated operating profit.
                    long dividend=Math.Max(0,Math.Min(e.PriceCents-100,(long)Math.Floor(e.QuarterProfitCents*.25/1000000)));
                    e.LastDividendCents=dividend;e.QuarterProfitCents=0;
                    if(dividend>0)
                    {
                        // Adjust the quote before any cash-shortfall liquidation, so a short
                        // closed to pay compensation still receives the ex-dividend price change.
                        e.PriceCents=Math.Max(100,e.PriceCents-dividend);
                        e.PreviousCloseCents=e.PriceCents;
                        string key="stock:dividend:"+e.Id+":"+day;
                        if(e.Units>0){Ledger.Credit(new GameDate(day),"股票分红",Money.FromCents(dividend*e.Units),StockName(e.Id)+"季度分红",LedgerAttribution.ForAsset(e.Id,key));e.RealizedCents+=dividend*e.Units;}
                        if(e.ShortUnits>0)ChargeStockShortCost(e,dividend*e.ShortUnits,"分红补偿",minute);
                        // Ex-dividend adjustment prevents buying immediately before payout from creating free money.
                        e.Quotes=AppendStockQuote(e.Quotes,minute,e.PriceCents);e.History=e.Quotes.Select(q=>q.PriceCents).ToArray();
                    }
                    AddStockNews(e.Id,minute,StockName(e.Id)+"季度分红",dividend>0?"每股分红 "+Money.FromCents(dividend)+"，按分红时实际持股支付并同步除息；做空持仓需补偿出借人的分红。":"本季盈利不足以支付每股至少1分的分红，本次不派息。");
                }
            }
        }
        private void ChargeStockShortCost(CityEquityState e,long cents,string label,long minute)
        {
            if(cents<=0)return;
            if(Ledger.Balance.Cents<cents)CloseStockShort(e,e.ShortUnits,true,minute);
            string key=StockEffect(e.Id,label,minute);
            Ledger.ForceDebit(new GameDate((int)(minute/1440)),label,Money.FromCents(cents),StockName(e.Id),LedgerAttribution.ForAsset(e.Id,key));
            _cityAssetEffects.Add(key);e.ShortRealizedCents-=cents;
        }
        private void CheckStockMargin(CityEquityState e,long minute)
        {
            if(e.ShortUnits<=0)return;
            if(StockMarginRatio(e.Id)<=StockMaintenanceRatio)CloseStockShort(e,e.ShortUnits,true,minute);
        }
        public string SecuritiesAccessBlocker => AtBank ? BankBlocker() : AppBlocker(PhoneApps.Securities);

        public string StockTradeBlocker(string id,long units,string action) =>
            StockTradeBlocker(id,units,action,checkCash:true);

        private string StockTradeBlocker(string id,long units,string action,bool checkCash)
        {
            string app=SecuritiesAccessBlocker;if(app!=null)return app;
            var e=StockState(id);
            if(id!="hardware"&&e==null)return "没有这只股票";
            if(IsGameOver)return "当前不能交易";
            if(!StockMarketOpen)return "周末休市，周一至周五全天可交易";
            if(units<=0||units>StockOrderLimit)return "数量须为1—1,000,000的整数";
            if(action!="buy"&&action!="sell"&&action!="short"&&action!="cover")return "未知交易类型";
            if (ListedCompanyOf(id) != null)
            {
                if (action == "short" || action == "cover") return "自有公司股票暂不提供借股";
                if (action == "buy" && units > CompanyPublicShares - e.Units) return "公众剩余股份不足";
            }
            if(id=="hardware"&&action=="buy")return "硬件景气指数仅供查看，已有持仓可以卖出";
            if(units>StockAvailable(id))return "本次报价还可成交"+StockAvailable(id)+"股，请减少数量或等待下一次报价";
            if(action=="short"||action=="cover")
            {
                if(e==null)return "指数不提供借股做空";
                if(action=="short"&&e.Units>0)return "请先卖出这只股票的普通持仓";
                if(action=="short"&&units>StockBorrowAvailable(id))return "可借股票不足，当前可借"+StockBorrowAvailable(id)+"股";
                if(action=="cover"&&units>e.ShortUnits)return "平仓数量超过实际做空股数";
                if(checkCash&&action=="short"&&Ledger.Balance<StockShortOpeningCost(id,units))return "现金不足以支付100%保证金及手续费";
            }
            if(action=="buy"&&e?.ShortUnits>0)return "请先买回平掉这只股票的做空持仓";
            if(checkCash&&action=="buy"&&Ledger.Balance<CityInvestmentTradeTotal(id,units,true))return "买入现金不足";
            if(action=="sell"&&units>CityInvestmentUnits(id))return "卖出数量超过实际持股";
            return null;
        }

        public long StockMaximumBuyQuantity(string id) => StockMaximumOpeningQuantity(id,false,Ledger.Balance);

        public long StockMaximumOpeningQuantity(string id,bool shortMode,Money cash)
        {
            string action = shortMode ? "short" : "buy";
            if (StockTradeBlocker(id, StockLotSize, action, checkCash:false) != null || !CityInvestmentPrice(id).IsPositive) return 0;
            long limit = Math.Min(StockAvailable(id), StockOrderLimit);
            if (shortMode) limit = Math.Min(limit, StockBorrowAvailable(id));
            else if (ListedCompanyOf(id) != null) limit = Math.Min(limit, CompanyPublicShares - CityInvestmentUnits(id));
            long upper = limit / StockLotSize;
            long affordable = 0;
            while (affordable < upper)
            {
                long lots = affordable + (upper - affordable + 1) / 2;
                long units = lots * StockLotSize;
                var cost = shortMode ? StockShortOpeningCost(id,units) : CityInvestmentTradeTotal(id,units,buy:true);
                if (cost <= cash) affordable = lots;
                else upper = lots - 1;
            }
            return affordable * StockLotSize;
        }

        public Money StockOrderCashChange(string id,long signedUnits,bool shortMode)
        {
            if (signedUnits > 0)
                return -(shortMode ? StockShortOpeningCost(id,signedUnits) : CityInvestmentTradeTotal(id,signedUnits,buy:true));
            if (signedUnits == 0 || signedUnits < -StockOrderLimit) return Money.Zero;
            return shortMode ? StockCoverReturn(id,-signedUnits) : CityInvestmentTradeTotal(id,-signedUnits,buy:false);
        }

        public StockBatchQuote PreviewStockBatch(IReadOnlyDictionary<string,long> orders,bool shortMode,bool calculateReserve=true)
        {
            var net = Money.Zero;
            var fees = Money.Zero;
            string blocker = orders.Count == 0 ? "暂无待成交" : null;
            bool opensPosition = false;
            foreach (var order in orders)
            {
                string id = order.Key;
                long signedUnits = order.Value;
                bool opening = signedUnits > 0;
                opensPosition |= opening;
                string action = shortMode ? (opening ? "short" : "cover") : (opening ? "buy" : "sell");
                long units = signedUnits < -StockOrderLimit ? StockOrderLimit + 1L : Math.Abs(signedUnits);
                string issue = StockTradeBlocker(id,units,action,checkCash:false);
                if (issue != null && blocker == null) blocker = StockName(id) + " · " + issue;
                if (units <= 0 || units > StockOrderLimit) continue;
                var gross = CityInvestmentPrice(id) * units;
                var change = StockOrderCashChange(id,signedUnits,shortMode);
                if (blocker == null && (!gross.IsPositive || (!shortMode && !opening && !change.IsPositive)))
                    blocker = StockName(id) + " · 交易金额不足以支付手续费";
                fees += CityInvestments.Fee(gross,id=="hardware");
                net += change;
            }
            var remaining = Ledger.Balance + net;
            if (blocker == null && opensPosition && remaining.IsNegative)
                blocker = $"可用现金还差 {-remaining}";
            bool needsReserve = calculateReserve && blocker == null && opensPosition;
            var reserve = needsReserve ? GetCityCashReserve().Total : Money.Zero;
            return new StockBatchQuote(net,fees,remaining,reserve,blocker,needsReserve && remaining < reserve);
        }

        public bool TradeStockBatch(IReadOnlyDictionary<string,long> orders,bool shortMode,bool acknowledgeReserve=false)
        {
            var quote = PreviewStockBatch(orders,shortMode);
            if (quote.Blocker != null) return Fail(quote.Blocker + "。");
            if (quote.UsesReserve && !acknowledgeReserve) return Fail("本次交易将动用预留资金，请在确认页核对后继续。");

            // 每个标的只出现一次。先平仓再开仓，卖出所得可用于本次买入。
            // 屏蔽逐单通知，避免在批次结算中间调用外部监听器。
            _quiet++;
            try
            {
                foreach (var order in orders.OrderBy(line => line.Value > 0).ToArray())
                {
                    bool opening = order.Value > 0;
                    long units = Math.Abs(order.Value);
                    bool traded = !shortMode ? TradeCityInvestment(order.Key,units,opening,acknowledgeReserve:true)
                        : opening ? OpenStockShort(order.Key,units,acknowledgeReserve:true) : CoverStockShort(order.Key,units);
                    if (!traded) return false;
                }
            }
            finally { _quiet--; }
            Say($"证券成交 {orders.Count} 笔 · 现金变动 {quote.Net} · 手续费 {quote.Fees}。");
            return true;
        }

        private void ConsumeStockLiquidity(string id,long units)
        {if(id=="hardware")_stockMarket.IndexAvailableUnits-=units;else StockState(id).AvailableUnits-=units;}
        public Money StockShortOpeningCost(string id,long units)
        {if(units<=0||units>StockOrderLimit)return Money.Zero;var gross=CityInvestmentPrice(id)*units;return gross+CityInvestments.Fee(gross);}
        public Money StockCoverReturn(string id,long units)
        {
            var e=StockState(id);if(e==null||units<=0||units>e.ShortUnits)return Money.Zero;
            var gross=CityInvestmentPrice(id)*units;
            return Money.FromCents(StockPortion(e.ShortBasisCents,units,e.ShortUnits)+StockPortion(e.ShortMarginCents,units,e.ShortUnits))-gross-CityInvestments.Fee(gross);
        }
        public bool OpenStockShort(string id,long units,bool acknowledgeReserve=false)
        {
            string why=StockTradeBlocker(id,units,"short");if(why!=null)return Fail(why+"。");
            var cost=StockShortOpeningCost(id,units);
            if(!acknowledgeReserve&&Ledger.Balance-cost<GetCityCashReserve().Total)return Fail("本次保证金将动用账单预留资金，请在确认页核对后继续。");
            var e=StockState(id);var gross=CityInvestmentPrice(id)*units;var fee=CityInvestments.Fee(gross);
            string key=StockEffect(id,"short",Clock.Now.TotalMinutes);
            // Own collateral is an internal transfer, not operating expenditure or investment loss.
            Ledger.TryDebit(Clock.Today,"冻结做空保证金",gross,null,new LedgerAttribution(LedgerBusinessKind.InternalTransfer,propertyId:id,sourceEventId:key+":margin"));
            Ledger.TryDebit(Clock.Today,"证券手续费",fee,StockName(id),LedgerAttribution.ForAsset(id,key+":fee"));
            e.ShortUnits+=units;e.ShortBasisCents+=gross.Cents;e.ShortMarginCents+=gross.Cents;e.ShortCollateralCents+=gross.Cents*2;e.ShortRealizedCents-=fee.Cents;
            ConsumeStockLiquidity(id,units);_cityAssetEffects.Add(key);
            RecordStockExecution(id, StockExecutionKind.Short, Clock.Now.TotalMinutes, e.PriceCents, units);
            Say("已借股卖出：100%自有保证金与卖出所得均冻结，买回归还后释放；每天计借股费。");return true;
        }
        public bool CoverStockShort(string id,long units)
        {
            string why=StockTradeBlocker(id,units,"cover");if(why!=null)return Fail(why+"。");
            CloseStockShort(StockState(id),units,false,Clock.Now.TotalMinutes);ConsumeStockLiquidity(id,units);return true;
        }
        private void CloseStockShort(CityEquityState e,long units,bool forced,long minute)
        {
            if(e==null||units<=0||units>e.ShortUnits)return;
            long basis=StockPortion(e.ShortBasisCents,units,e.ShortUnits),margin=StockPortion(e.ShortMarginCents,units,e.ShortUnits);
            long collateral=basis+margin;
            var gross=Money.FromCents(e.PriceCents*units);var fee=CityInvestments.Fee(gross);
            long net=collateral-gross.Cents-fee.Cents,profit=net-margin;
            string key=StockEffect(e.Id,forced?"forced-cover":"cover",minute);var date=new GameDate((int)(minute/1440));
            if(margin>0)Ledger.Credit(date,"释放做空保证金",Money.FromCents(margin),StockName(e.Id),new LedgerAttribution(LedgerBusinessKind.InternalTransfer,propertyId:e.Id,sourceEventId:key+":margin"));
            if(profit>0)Ledger.Credit(date,"做空平仓收益",Money.FromCents(profit),StockName(e.Id),LedgerAttribution.ForAsset(e.Id,key+":pnl"));
            else if(profit<0)Ledger.ForceDebit(date,"做空平仓损失",Money.FromCents(-profit),StockName(e.Id)+"（含平仓手续费）",LedgerAttribution.ForAsset(e.Id,key+":pnl"));
            e.ShortUnits-=units;e.ShortBasisCents-=basis;e.ShortMarginCents-=margin;e.ShortCollateralCents-=collateral;e.ShortRealizedCents+=basis-gross.Cents-fee.Cents;
            _cityAssetEffects.Add(key);
            RecordStockExecution(e.Id, forced ? StockExecutionKind.ForcedCover : StockExecutionKind.Cover, minute, e.PriceCents, units);
            string message=StockName(e.Id)+(forced?"已自动买回平仓":"已买回归还")+units+"股，释放到账 "+Money.FromCents(net)+"。";
            if(forced)AddStockNews(e.Id,minute,"账户自动平仓",message+" 原因：担保比例达到130%警戒线或可用现金不足以支付持仓费用。");
            Say(message);
        }
        public bool AddStockShortMargin(string id,Money amount)
        {
            var e=StockState(id);if(e==null||e.ShortUnits<=0||!amount.IsPositive||IsGameOver)return false;
            string blocker=SecuritiesAccessBlocker;if(blocker!=null)return Fail(blocker);
            string key=StockEffect(id,"add-margin",Clock.Now.TotalMinutes);
            if(!Ledger.TryDebit(Clock.Today,"补充做空保证金",amount,StockName(id),new LedgerAttribution(LedgerBusinessKind.InternalTransfer,propertyId:id,sourceEventId:key)))return Fail("可用现金不足。");
            e.ShortMarginCents+=amount.Cents;e.ShortCollateralCents+=amount.Cents;_cityAssetEffects.Add(key);return true;
        }
    }
}
