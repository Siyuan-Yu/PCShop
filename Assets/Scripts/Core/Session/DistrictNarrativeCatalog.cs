using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    // Authored content shared by previews and the district runner. Opening this catalogue
    // never grants completion evidence; the runner records actual meetings and actions.
    public enum NarrativeFact { OpenDistrictShops, Cash, NetAssets, OwnBrandFormed, OwnBrandListed, ClosedSectorMonthlyRevenue, OwnSharePrice, FollowingMonth }
    [Serializable] public sealed class NarrativeGate
    {
        public NarrativeFact Fact;
        public string Scope;
        public decimal Minimum;
        public string Description;
    }
    [Serializable] public sealed class NarrativeChoice
    {
        public string Id, Label, Consequence, Evidence;
    }
    [Serializable] public sealed class DistrictNarrativeNode
    {
        public string Id, Module, Title, PlaceId, Entry, Objective, Dialogue, Evidence, Result, Retry;
        public string[] People, Prerequisites;
        public NarrativeChoice[] Choices;
    }
    [Serializable] public sealed class DistrictNarrativeChapter
    {
        public string Id, Title, Reward;
        public string[] Modules;
        public NarrativeGate[] Gates;
    }
    [Serializable] public sealed class PersonalNarrativeArc
    {
        public string Id, Title, PersonId, Premise, Conflict, Resolution;
        public string[] RelatedPeople;
    }

    public static class DistrictNarrativeCatalog
    {
        public const string ContentVersion="2026-11-03.1";
        public const string IntegrationStatus="三段街区引荐已接入聊天消息、实际会面与履约、存档及道路解锁。新品牌路线接入季度展会、真实行业月账和企业竞标履约；旧主线验收保留。IPO与发行人股价尚未实现；20条人物支线仍为内容配置。执行时晋升前会面安排在已开放区域。";
        public const string RetryRule="可稍后再谈或重新约时间；保留已完成事实、实际费用和关系变化，不因错过一天清空整条线。";
        private static NarrativeGate Gate(NarrativeFact fact,decimal value,string scope,string text)=>new NarrativeGate{Fact=fact,Minimum=value,Scope=scope,Description=text};

        // Fresh objects on every call: moving a module cannot mutate another save/preview's plan.
        public static DistrictNarrativeChapter[] CreatePlan(bool listingAtGarden=false,bool dominanceAfterGarden=false)
        {
            var suburb=new DistrictNarrativeChapter{Id="district.community",Title="城郊 → 社区",Reward="开放社区道路及商铺洽谈资格；租金、押金照常支付。",Modules=new[]{"community_entry"},Gates=new[]{
                Gate(NarrativeFact.OpenDistrictShops,5,"suburb","城郊已有5家实际开业门店"),Gate(NarrativeFact.Cash,1000000,"player","手上现金至少100万元")}};
            var park=new DistrictNarrativeChapter{Id="district.park",Title="社区 → 公园",Reward="任帆与沈远共同引荐，开放公园道路与商铺机会。",Modules=new[]{"community_cooperation","rare_model"},Gates=new[]{
                Gate(NarrativeFact.OpenDistrictShops,6,"community","社区至少6家实际开业门店（可调为7家）"),Gate(NarrativeFact.NetAssets,30000000,"player","资产门槛暂定3000万元；建议净资产口径，待平衡确认")}};
            var garden=new DistrictNarrativeChapter{Id="district.garden",Title="公园 → 花园",Reward="完成合作后开放花园道路与经营资格；不赠送房产或保证收益。",Modules=new[]{"garden_project"},Gates=Array.Empty<NarrativeGate>()};
            var ending=new DistrictNarrativeChapter{Id="ending.industry",Title="行业终章（预留）",Reward="行业终章叙事；不重复解锁已有街区。",Modules=Array.Empty<string>(),Gates=Array.Empty<NarrativeGate>()};
            var listing=listingAtGarden?garden:park;
            listing.Modules=listing.Modules.Concat(new[]{"brand_listing"}).ToArray();
            listing.Gates=listing.Gates.Concat(new[]{Gate(NarrativeFact.OwnBrandFormed,1,"player","已成立自己的品牌/公司"),Gate(NarrativeFact.OwnBrandListed,1,"player","自己的品牌/公司已上市；购买别人的股票不算")}).ToArray();
            var dominance=dominanceAfterGarden?ending:garden;
            dominance.Modules=dominance.Modules.Concat(new[]{"industry_dominance"}).ToArray();
            dominance.Gates=dominance.Gates.Concat(new[]{
                Gate(NarrativeFact.ClosedSectorMonthlyRevenue,100000000,"one_owned_sector","一个完整已结算月，同一行业的全部自营门店营业收入达到1亿元；不同业态不可合并"),
                Gate(NarrativeFact.OwnSharePrice,100,"same_issuer","对应自有上市品牌股价达到100元（暂定值，需结合发行股数平衡）"),
                Gate(NarrativeFact.FollowingMonth,1,"qualified_month","次月才发起邀约；以结算月标记去重，不在月中预支触发")}).ToArray();
            return dominanceAfterGarden?new[]{suburb,park,garden,ending}:new[]{suburb,park,garden};
        }

        private static DistrictNarrativeNode Node(string id,string module,string title,string place,string[] people,string[] before,string entry,string objective,string dialogue,string evidence,string result,params NarrativeChoice[] choices)=>new DistrictNarrativeNode
        {Id=id,Module=module,Title=title,PlaceId=place,People=people,Prerequisites=before,Entry=entry,Objective=objective,Dialogue=dialogue,Evidence=evidence,Result=result,Retry=RetryRule,Choices=choices};
        private static NarrativeChoice Choice(string id,string label,string effect,string evidence)=>new NarrativeChoice{Id=id,Label=label,Consequence=effect,Evidence=evidence};
        public static IReadOnlyList<DistrictNarrativeNode> Nodes {get;}=new[]{
            Node("expo.invitation","community_cooperation","品牌第一次正式亮相","life_expo",new[]{"life_curator",CityLifeCatalog.TangWei},Array.Empty<string>(),"品牌及社区经营达到引荐条件。","接受任帆的季度展会邀请。","任帆：店开得好，还得让同行看到。云帆每季度办展，你选好代表商品和接待方向，亲自来见买家。唐蔚：展后答应的订单也要交出来，才算站稳。","expo.invited","获得正式参展邀请，开始准备展位。"),
            Node("expo.prepare","community_cooperation","把门店带到展台","life_expo",new[]{"life_curator",CityLifeCatalog.TangWei},new[]{"expo.invitation"},"已接受展会引荐。","为选定品牌报名季度展会。","任帆：先选一件你能稳定供应的商品，交三件样品、确认展位和接待方向。宣传可以请人帮忙，但真正答应买家的事要由你的店完成。","expo.prepared","展位与样品已确认，下一步亲自接待。"),
            Node("expo.reception","community_cooperation","让买家记住你","life_expo",new[]{"life_curator"},new[]{"expo.prepare"},"已报名。","展会当天完成三轮现场接待。","任帆：买家问供货，媒体问亮点，设计师问细节。按你的品牌定位接待，带来的员工和嘉宾也必须真实到场。错过这届，下季度再来，机会还在。","expo.received","完成正式亮相，获得采购线索。"),
            Node("expo.delivery","community_cooperation","展会散场以后","life_expo",new[]{"life_enterprise","life_curator"},new[]{"expo.reception"},"已接待买家。","交付展会订单，品牌影响力达到100。","沈远：台上说得再好，也要看货能不能准时到。先把这20件送到客户手里，我们再谈公园商圈的引荐。发货单不等于收货单。","expo.delivered","影响力和真实交付达标，可以约见推荐人。"),
            Node("expo.review","community_cooperation","展会之后的交付","life_expo",new[]{"life_curator","life_enterprise"},new[]{"expo.invitation"},"参展并完成后续订单。","同一品牌影响力至少100，并真实交付展会线索订单。","沈远：我们看展会，也看展会之后。请带上已经履约的记录，任帆和我会共同引荐你进入公园商圈。","expo.fulfilled","完成正式展会和交付，开放公园道路。"),
            Node("tender.invitation","garden_project","席位只有一个","agency_s_sports",new[]{"life_enterprise","life_tenant"},Array.Empty<string>(),"单一品牌行业月营收达到门槛后进入次月。","了解企业采购的年度供应商席位。","沈远：原供应商还在，你没有空位可坐。先了解采购要求，再交试单、报方案。袁岚：拿下只是开始，连续履约才有人替你担保。","tender.invited","开放竞标主线，去品牌页准备企业采购方案。"),
            Node("tender.prepare","garden_project","摸清采购方与对手","agency_s_sports",new[]{"life_enterprise","life_tenant"},new[]{"tender.invitation"},"已接受席位挑战。","报名年度竞标并准备需求调查。","沈远：先报名，再来找我核实交货要求。袁岚：原供应商不会自己让位，我能帮你了解他打算怎么应对。最后报什么方案，由你决定。","tender.prepared","进入正式竞标，准备试单。"),
            Node("tender.trial","garden_project","让试单替你说话","life_company",new[]{"life_enterprise","life_tenant"},new[]{"tender.prepare"},"已报名。","提交方案并实际交付10件试单。","沈远：价格、服务、联合保障都能谈，但先做一笔真实的试单。袁岚：对手看到你的报价会回应，别把自己的周转和供货能力全部押上去。","tender.tried","试单到货，可以评审席位。"),
            Node("tender.decision","garden_project","挤进供应商名单","agency_s_sports",new[]{"life_enterprise","life_tenant"},new[]{"tender.trial"},"试单已到货。","评审超过原供应商，取得年度席位。","沈远：现在对照需求、试单和履约保障评审。袁岚：失利也不是断路，保住生意，下一年度还能重新投标。","tender.won","取得席位，正式考验开始。"),
            Node("tender.fulfilment","garden_project","把席位坐稳","life_company",new[]{"life_enterprise","life_tenant"},new[]{"tender.decision"},"已经中标。","连续完成同一合约的三期交付。","沈远：中标只是开始，接下来三期每期30件，都要真实交货。袁岚：货源、库存和现金要一起安排，不能靠一张漂亮报价一直坐在名单上。","tender.delivered","三期履约完成，可以申请花园引荐。"),
            Node("tender.review","garden_project","用交付赢得席位","agency_s_sports",new[]{"life_enterprise","life_tenant"},new[]{"tender.invitation"},"取得席位并履约三个周期。","完成一份企业合约的连续三期交付。","沈远：你已经证明不只是报价好看。这份履约记录可以带进花园。袁岚：进去以后再看看，谁在制作内容、谁掌握渠道，事情不只在台前。","tender.fulfilled","获得花园引荐，解锁机构与渠道合作。"),
            Node("community.intro","community_entry","隔壁街的机会","agency",new[]{"landlady","keeper_agent"},Array.Empty<string>(),"城郊门店及现金门槛达成后，李秀兰在见面或电话中提起。","通过陈启明约见唐蔚；确认时间和地点。","李秀兰：你不是只开了一家试试了。陈启明认识隔壁街做商户事务的人，我问问她愿不愿意见你。","appointment.tang.accepted","建立真实邀约，不瞬移、不立即授予资格。",Choice("accept","请陈启明帮忙约时间","进入会面预约。","appointment.tang.accepted"),Choice("later","最近忙，改天再说","保留机会。","")),
            Node("community.meeting","community_entry","搬家时散落的三页图","agency",new[]{CityLifeCatalog.TangWei},new[]{"community.intro"},"先找陈启明约唐蔚，再赴约了解唐蔚遇到的困难。","接受找回旧街手绘商铺图的委托。","唐蔚：我正在准备街区介绍会，想把旧街还在经营的小店也放进去。可那张图原本是三页拼起来的，搬家时拆开夹进不同的资料箱，分别托了三个人保管。李秀兰知道是谁，你能帮我找齐、重新拼起来吗？\n那张图上有些小店还没进新名册，别因为资料丢了就把他们漏掉。","community.map.accepted","开始寻物委托，先向李秀兰询问；本次会面不开放道路。"),
            Node("community.clue","community_entry","问问那天帮忙的人","life_home_s_oldhomes",new[]{"landlady"},new[]{"community.meeting"},"接受委托后约见李秀兰。","打听搬家箱子的去向及地图的包装。","李秀兰：三页没丢，我、林远航和周维成各保管了一页。我爱去城郊公园散步，林远航喜欢在街坊餐厅和老朋友吃饭，周维成爱到李承业CPU档口看旧收音机、聊评书。你分别去找我们聊聊吧，熟悉以后一起走走，活动结束时我们就告诉你各自的资料箱放在哪里。","community.map.clue","记录三位保管人与各自喜好的活动；此时不生成箱子。"),
            Node("community.search","community_entry","散落在三条街的图页","shop12",new[]{"landlady","lin","zhou"},new[]{"community.clue"},"已从李秀兰那里取得有效线索。","分别与三位保管人交往、完成喜欢的活动并打听，找到三只箱子中的图页，再拼成完整地图。","陈启明：三页分别画着旧居街、商业街和批发市场，沿着街道接缝就能拼起来。每个人的箱子要等他整理好、告诉你藏点后才会出现，不用现在满地图盲找。","community.map.found","三处现场分别取物；三页齐全后手动拼图，完成前不开放道路。"),
            Node("community.trial","community_entry","把旧街带进新名单","agency",new[]{CityLifeCatalog.TangWei},new[]{"community.search"},"已经实际找到手绘商铺图。","回城郊中介，当面交还地图。","唐蔚：三页都接上了，旧水塔和旁边的备注都还在。我能把遗漏的小店补上了。\n谢谢你把这件事跑清楚。接下来由我介绍社区的经营联系人，具体租约我们按正常手续谈。","community.map.delivered","交付任务物品，开放社区道路与经营接洽。"),
            Node("park.proposal","community_cooperation","一起把活动办完","agency_s_services",new[]{CityLifeCatalog.TangWei,"life_tenant","life_curator"},new[]{"community.trial"},"社区阶段数值及当前配置的上市条件达成。","确认跨店活动预算、参与店铺、人员与自己到场时间。","袁岚：我出小店这一份，不代表我能承担整条街的费用。咱们先把各自负责什么说清楚。","cooperation.plan.accepted","创建有预算、有日期、有责任人的合作记录；接受不等于完成。",Choice("shared","共同分摊并写清各自负责的事","按实际约定扣费与安排。","cooperation.plan.shared"),Choice("scaled","缩小活动规模，先做得过来","预算与规模降低，仍可得到认可。","cooperation.plan.scaled")),
            Node("park.change","community_cooperation","临时换了位置","life_service",new[]{"life_tenant","life_curator",CityLifeCatalog.TangWei},new[]{"park.proposal"},"准备期发生位置调整。","先勘察入口和卸货通道，分别听袁岚、任帆的意见，再让袁岚确认具体布置。","任帆：大展位想换到门口，但原先答应袁岚的位置不能当作没说过。你愿意一起把这事谈清吗？","cooperation.change.resolved","两种协商均可推进；不强制牺牲自己的门店或袁岚。",Choice("honor","保留已答应的位置，调整其他动线","需要重新协调布置时间。","cooperation.change.honor"),Choice("consent","征得同意后换位，并补足展示条件","必须获得受影响商户同意、兑现补充安排。","cooperation.change.consent")),
            Node("park.delivery","community_cooperation","被记住的是交代","life_service",new[]{"life_tenant","life_curator",CityLifeCatalog.TangWei},new[]{"park.change"},"活动当天按计划准备并实际到场。","现场核对导览和展架，向任帆完成角色交接，再参与30分钟；不要求销售额夺冠。","任帆：这次出了变动，你没有躲起来，也没有替别人一口答应。下次有合作，我知道该找谁。","cooperation.event.fulfilled","获得商业伙伴任帆的具体认可；引荐沈远。"),
            Node("model.dinner","rare_model","不是一张大订单","life_restaurant_tier2",new[]{"life_curator","life_enterprise"},new[]{"park.delivery"},"接受任帆安排的饭局并实际参加。","与沈远交谈，听到孩子惦记旧展会模型。","沈远：孩子喜欢的不是新款，是初版那张窗边的小凳子。我问了几家都没货。找不到也没关系，别为这件事答应不该答应的事。","rare_model.request.heard","开启初版城市模型线索；沈远承担确认过的购买、修复费用。"),
            Node("model.clues","rare_model","旧展册上的一页","model_market",new[]{"life_tenant","life_creator","life_model_supplier","life_curator"},new[]{"model.dinner"},"已听到需求；询问认识的相关人物。","任选一条顺序调查链：袁岚介绍→陆栀对图→市场查展册，或周岑查批次→市场查展册→任帆核对去向；以C-01编号找到程弈。","普通目录找不到孩子记得的版本。你可以请袁岚介绍原作者，也可以从周岑的批次记录查起。先把窗凳、站牌和旧展册对上，再追查实物去向。","rare_model.maker.identified","任一链获得明确线索即可；另一链保留为补充，不强制重复跑。",Choice("creator","请袁岚介绍陆栀，查原作记录","通过作者找到程弈。","rare_model.clue.creator"),Choice("supplier","向周岑问旧批次，再找任帆核对展册","通过制作及展览记录找到程弈。","rare_model.clue.archive")),
            Node("model.repair","rare_model","缺了一角的展样","life_market",new[]{"life_maker","life_creator","life_enterprise"},new[]{"model.clues"},"经介绍约见程弈。","亲自核对编号和裂纹，听程弈解释修复取舍，再向沈远确认费用。付费等工期，现场验收修复单后领取。","程弈：底座裂了一角，修好也该告诉孩子它修过。我愿意转让，条件是保留作者署名，不把它当成全新未拆的货。","rare_model.repaired.and.acquired","获得有来源、有修复说明的实物；不靠好感直接生成物品。",Choice("restore","按约修复并保留修复记录","等待真实工期和费用结算。","rare_model.restore"),Choice("disclose","先带照片说明原状，再确认是否继续","买方不接受可暂缓，已发生费用不回滚。","rare_model.disclosure")),
            Node("model.delivery","rare_model","那张小凳子还在","life_service",new[]{"life_enterprise",CityLifeCatalog.ShenXiao,"life_maker"},new[]{"model.repair"},"实物已取得，约定交付时间。","当面交付、说明来源和修复、完成报销。","沈晓：就是这张小凳子。那道裂纹也留下吧，我想记得它以前去过展览。\n沈远：你问清楚才答应，过程中也一直有交代。这比说什么都能办重要。","rare_model.delivered.and.settled","获得重要客户沈远的认可；孩子是独立NPC，不是好感奖励道具。"),
            Node("park.recommendation","rare_model","两个人愿意介绍你","agency_s_services",new[]{"life_curator","life_enterprise"},new[]{"model.delivery"},"任帆的合作履约认可与沈远的交付认可都成立。","参加公园街区介绍会，确认后续租赁手续。","任帆：我推荐他，是因为我们一起办过事。\n沈远：我补充的是，他也能把没有现成答案的小事交代到底。","district.park.introduced","开放公园经营资格；不强迫选择某个行业或恋爱路线。"),
            Node("brand.listing","brand_listing","从店名到品牌","life_company",new[]{CityLifeCatalog.TangWei,"life_enterprise"},Array.Empty<string>(),"在所属阶段的前置条件检查中使用；不可依赖尚未解锁的花园设施。","成立自有品牌/公司，并通过正式上市流程。","沈远：上市不是把别人的股票买一手。你得清楚这家公司持有哪些经营资产，对外承诺的又是什么。","own_issuer.listed","条件模块；没有发行机制时保持未达成，禁止对话按钮伪造上市。"),
            Node("industry.settlement","industry_dominance","一个行业的完整月份","life_company",new[]{"life_curator"},Array.Empty<string>(),"结算月收入及对应发行人股价达到门槛。","锁定行业、所属门店、结算月和发行人；在下一月发出邀约。","任帆：这次不是把所有生意凑在一起。有人注意到，你在这个行业已经持续做出了一定规模。","industry.month.qualified","保存结算事实和邀约月；同月同一事实不重复发邀请。"),
            Node("garden.invitation","garden_project","名单上的新名字","life_restaurant_tier3",new[]{"life_curator",CityLifeCatalog.LiangJing},new[]{"park.recommendation"},"默认：行业合格月的次月；若将行业模块移到终章，则采用当前阶段配置条件。","回应邀请并真实赴约，与梁敬交换联系方式。","梁敬：任帆说你做过不同规模的合作。我们正找能长期经营的人，今晚先聊，不急着签。","garden.meeting.completed","认识花园项目负责人，不直接授予完整区域资格。"),
            Node("garden.proposal","garden_project","空出来的一间铺子","agency_s_sports",new[]{CityLifeCatalog.LiangJing,"life_tenant"},new[]{"garden.invitation"},"会面后获得项目资料。","分别听袁岚的周转压力和梁敬的履约要求，选定并实地查看公园试点店。","袁岚：地方很好，但这份保证金和租期，会把我原来的店也拖进去。我想试，不等于我付得起任何代价。","garden.project.reviewed","先在已开放的公园门店完成合作试点；履约完成后才开放花园道路。"),
            Node("garden.terms","garden_project","一直如此，也可以再谈","agency_s_sports",new[]{CityLifeCatalog.LiangJing,"life_tenant",CityLifeCatalog.TangWei},new[]{"garden.proposal"},"清楚长租与保证金的实际现金流压力。","分别取得所选合作方的明确同意，与梁敬写清费用、退出办法和责任上限；尊重退出分支需宋棉自愿参与。","梁敬：这套条件原先是为了减少撤店，但也确实挡住了一些人。改可以，风险和退出办法要写清楚。","garden.terms.signed","三条路线同等通向资格；不以强行救人或背叛伙伴作为唯一正确答案。",Choice("trial","谈分阶段试经营，按阶段决定是否续约","签订阶段条款，真实计费。","garden.terms.trial"),Choice("limited","在承受范围内提供有限支持","资金实际划出，写明上限；不允许无限担保。","garden.terms.support"),Choice("respect","尊重袁岚退出，另寻自愿合作者","保留关系，重新确认合作者意愿。","garden.terms.alternative")),
            Node("garden.fulfilment","garden_project","签字之后","agency_s_sports",new[]{CityLifeCatalog.LiangJing,"life_tenant"},new[]{"garden.terms"},"所选方案真实签署且费用、责任已落实。","处理合作方反映的售后交接问题，回试点店核查真实成交，向本人完成交接复盘；签约满一天后再验收。","梁敬：我们先看这次约定有没有兑现，再谈更长的合作。你没有替别人决定他必须成功，这一点我记住了。","garden.project.fulfilled","获得花园经营资格；项目亏损也按约承担，不凭收入预设成功。"),
            Node("garden.echo","garden_project","这次由你介绍","life_restaurant_tier4",new[]{CityLifeCatalog.LiangJing,"life_tenant",CityLifeCatalog.TangWei},new[]{"garden.fulfilment"},"获得资格后，在后续商户聚会遇到请求介绍的新经营者。","选择是否介绍、介绍哪些具体经历；不替对方担保未知事项。","唐蔚：当年有人介绍你，现在也有人想请你介绍。你可以拒绝，但不必用一句一直如此把话说完。","garden.echo.responded","主线收束于参与制定条件；介绍或婉拒均保留已获资格。",Choice("introduce","介绍自己确实了解的经历","只传递已获本人同意的信息。","garden.echo.introduce"),Choice("decline","说明自己了解不足，暂不推荐","提供可公开联系的途径，不惩罚已完成主线。","garden.echo.decline"))
        };

        private static PersonalNarrativeArc Arc(string id,string title,string person,string premise,string conflict,string resolution,params string[] others)=>new PersonalNarrativeArc{Id=id,Title=title,PersonId=person,Premise=premise,Conflict=conflict,Resolution=resolution,RelatedPeople=others};
        public static IReadOnlyList<PersonalNarrativeArc> SideStories {get;}=new[]{
            Arc("side.zhou","担保的名字","zhou","熟悉周维成后，得知他曾为同行作担保。","朋友再求他签字，但口头义气与家中积蓄发生冲突。","可帮助核对条款或支持拒绝；不让玩家用付钱买结局。","li","lin"),
            Arc("side.jie","守住这一间","jie","唐俊杰愿意分享手机店的日常成本。","扩店邀请看起来体面，却会让原店失去熟练的人手。","一起算清账再选择，留下经营也算完整结局。","keeper_phone","keeper_phone2"),
            Arc("side.landlady","下一次续约","landlady","李秀兰谈起旧街住户与租约。","改善房屋的成本与老租户可承受租金矛盾。","分期维护、透明协商；不要求一方无条件承担全部。","res_wang","keeper_agent"),
            Arc("side.ming","第二块招牌","life_ming","许明在工作与交谈中谈起独立经营的愿望。","玩家需要熟手，员工却希望有自己的成长。","可协商交接、合作或体面离职；须尊重实际雇佣状态。","life_ning","life_qiao"),
            Arc("side.momo","镜头之外","momo","莫文宁得到一份商业推广邀请。","合作方希望隐藏不利体验，影响观众信任。","披露合作、重谈脚本或拒绝；后续反映真实选择。","ake","qiao"),
            Arc("side.lin","还是同一个人吗","lin","在玩家经营规模变化后，林远航谈起最近难约见。","旧朋友感受到生活节奏与话题的距离。","通过真实见面或诚实约定维持关系，不用财富强制断交。","res_coder"),
            Arc("side.creator","我已经不喜欢那个角色了","life_creator","陆栀讲起早期作品和后来删改的细节。","商业机会要求永远重复最成功的旧作。","可支持新作、保留旧版署名或坦诚终止合作；不强制恋爱。","life_maker","life_model_supplier"),
            Arc("side.clothing","合适的衣服","life_clothier","宋棉遇到需要正式着装但预算有限的熟客。","体面并不等于最高价，卖货目标与实际需要不同。","提供适合预算的搭配；不得用羞辱顾客触发剧情。","life_designer","life_clothsupplier"),
            Arc("side.designer","名字写在哪里","life_designer","温岚的版型被合作店采用。","追加订单希望抹掉原设计署名。","谈清使用范围与报酬，也可拒绝继续供稿。","life_clothier","life_creator"),
            Arc("side.supplier","先说清楚的一批货","life_clothsupplier","许纬接到缺货季的补单。","加急供货与质量稳定不能同时保证。","告知风险、分批交付或取消；实际订单结果留存。","life_clothier","resident_sujun"),
            Arc("side.models","初版与再版","life_model_supplier","周岑面对收藏热潮中的同款询价。","稀缺炒价与真实版本信息容易混淆。","公开批次来源，不把再版冒充初版；可影响供货信任。","life_maker","life_creator"),
            Arc("side.chef","一张留下来的饭桌","life_chef","杜建川发现张绍文越来越少来吃饭。","便宜套餐解决不了独居时不愿出门的原因。","先问本人意愿，约一次普通饭局；不擅自公开私人困难。","zhang","resident_linhe"),
            Arc("side.maker","旧物的来路","life_maker","程弈收到一件待修旧模型。","委托方只想换新，原主人却珍惜使用痕迹。","核对归属和意愿后修复或保留原状，记录真实出处。","resident_guwen","life_creator"),
            Arc("side.tenant","下一次续租","life_tenant","袁岚准备续租现有小店。","新商圈机会与熟客、资金、家庭节奏发生冲突。","留在原店或参与试点都可成立；与花园主线共享已确认事实。",CityLifeCatalog.TangWei,CityLifeCatalog.LiangJing),
            Arc("side.curator","名单上还有谁","life_curator","任帆准备下一次展会推荐名单。","熟悉的合作方安全，新人却一直没有履历。","有限试点并公开标准，推荐基于实际合作证据。",CityLifeCatalog.TangWei,CityLifeCatalog.LiangJing),
            Arc("side.client","孩子惦记的不是价格","life_enterprise","沈远从工作谈到孩子的旧展会记忆。","越贵的新款并不等于孩子想要的那件。","按稀有模型主线听清需求并如实交付，不重复生成奖励。",CityLifeCatalog.ShenXiao,"life_curator"),
            Arc("side.agent","签约之前","keeper_agent","陈启明发现新商户只盯着月租。","押金、用途和退出条款决定了真实风险。","给出明确条款，允许暂缓签约；不能用对话强租商铺。","landlady",CityLifeCatalog.TangWei),
            Arc("side.li","账期与交情","li","李承业愿意谈进货账期的老经验。","熟人赊货逾期，收款会影响关系。","明示可承受的额度和期限，既能催款也能停止赊销。","zhou","wang"),
            Arc("side.tang","谁来通知小店","story_tangwei","唐蔚筹备商业街变更通知。","消息总先到大商户，晚知道的人承担更多调整成本。","逐一确认受影响商户收到通知，记录异议再实施。","life_tenant","resident_zhouran"),
            Arc("side.liang","一直如此","story_liangjing","梁敬回忆自己得到第一次试租机会。","今天他制定的统一门槛也挡住了后来者。","通过花园合作谈具体风险和退出办法，不靠一句劝说改变全部制度。","life_curator",CityLifeCatalog.TangWei)
        };
    }
}
