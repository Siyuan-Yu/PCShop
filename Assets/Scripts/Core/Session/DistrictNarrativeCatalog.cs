using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    // Authored content, not a second quest simulator. Evidence IDs are integration contracts:
    // a future runner must obtain them from completed game actions, never from opening this catalogue.
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
        public const string IntegrationStatus="主线为节点配置；尚未接入完整任务执行器。公司上市、发行人股价和项目履约凭据仍需对应机制。";
        public const string RetryRule="可稍后再谈或重新约时间；保留已完成事实、实际费用和关系变化，不因错过一天清空整条线。";
        private static NarrativeGate Gate(NarrativeFact fact,decimal value,string scope,string text)=>new NarrativeGate{Fact=fact,Minimum=value,Scope=scope,Description=text};

        // Fresh objects on every call: moving a module cannot mutate another save/preview's plan.
        public static DistrictNarrativeChapter[] CreatePlan(bool listingAtGarden=false,bool dominanceAfterGarden=false)
        {
            var suburb=new DistrictNarrativeChapter{Id="district.community",Title="城郊 → 社区",Reward="获得社区商铺洽谈资格；租金、押金照常支付。",Modules=new[]{"community_entry"},Gates=new[]{
                Gate(NarrativeFact.OpenDistrictShops,5,"suburb","城郊已有5家实际开业门店"),Gate(NarrativeFact.Cash,1000000,"player","手上现金至少100万元")}};
            var park=new DistrictNarrativeChapter{Id="district.park",Title="社区 → 公园",Reward="任帆与沈远共同引荐，开放公园商铺机会。",Modules=new[]{"community_cooperation","rare_model"},Gates=new[]{
                Gate(NarrativeFact.OpenDistrictShops,6,"community","社区至少6家实际开业门店（可调为7家）"),Gate(NarrativeFact.NetAssets,30000000,"player","资产门槛暂定3000万元；建议净资产口径，待平衡确认")}};
            var garden=new DistrictNarrativeChapter{Id="district.garden",Title="公园 → 花园",Reward="完成合作后开放花园经营资格；不赠送房产或保证收益。",Modules=new[]{"garden_project"},Gates=Array.Empty<NarrativeGate>()};
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
            Node("community.intro","community_entry","隔壁街的机会","agency",new[]{"landlady","keeper_agent"},Array.Empty<string>(),"城郊门店及现金门槛达成后，李姐在见面或电话中提起。","通过小陈约见唐蔚；确认时间和地点。","李姐：你不是只开了一家试试了。小陈认识隔壁街做商户事务的人，我问问她愿不愿意见你。","appointment.tang.accepted","建立真实邀约，不瞬移、不立即授予资格。",Choice("accept","请小陈帮忙约时间","进入会面预约。","appointment.tang.accepted"),Choice("later","最近忙，改天再说","保留机会。","")),
            Node("community.meeting","community_entry","带着做过的事来","agency_s_services",new[]{CityLifeCatalog.TangWei,"keeper_agent"},new[]{"community.intro"},"赴约并与唐蔚当面交谈。","介绍现有门店及经营情况，听清新街区的租赁条件。","唐蔚：不用保证每家店都成功。说清现在谁负责、钱从哪来，遇到问题能找到谁就行。","meeting.tang.completed","开放社区洽谈资格，记录唐蔚联系方式。"),
            Node("park.proposal","community_cooperation","一起把活动办完","agency_s_services",new[]{CityLifeCatalog.TangWei,"life_tenant","life_curator"},new[]{"community.meeting"},"社区阶段数值及当前配置的上市条件达成。","确认跨店活动预算、参与店铺、人员与自己到场时间。","袁岚：我出小店这一份，不代表我能承担整条街的费用。咱们先把各自负责什么说清楚。","cooperation.plan.accepted","创建有预算、有日期、有责任人的合作记录；接受不等于完成。",Choice("shared","共同分摊并写清各自负责的事","按实际约定扣费与安排。","cooperation.plan.shared"),Choice("scaled","缩小活动规模，先做得过来","预算与规模降低，仍可得到认可。","cooperation.plan.scaled")),
            Node("park.change","community_cooperation","临时换了位置","life_expo",new[]{"life_tenant","life_curator",CityLifeCatalog.TangWei},new[]{"park.proposal"},"准备期发生位置调整。","当面协调位置、动线与费用，落实一种可承担的安排。","任帆：大展位想换到门口，但原先答应袁岚的位置不能当作没说过。你愿意一起把这事谈清吗？","cooperation.change.resolved","两种协商均可推进；不强制牺牲自己的门店或袁岚。",Choice("honor","保留已答应的位置，调整其他动线","需要重新协调布置时间。","cooperation.change.honor"),Choice("consent","征得同意后换位，并补足展示条件","必须获得受影响商户同意、兑现补充安排。","cooperation.change.consent")),
            Node("park.delivery","community_cooperation","被记住的是交代","life_expo",new[]{"life_tenant","life_curator",CityLifeCatalog.TangWei},new[]{"park.change"},"活动当天按计划准备并实际到场。","完成布置、参与与结算；不要求销售额夺冠。","任帆：这次出了变动，你没有躲起来，也没有替别人一口答应。下次有合作，我知道该找谁。","cooperation.event.fulfilled","获得商业伙伴任帆的具体认可；引荐沈远。"),
            Node("model.dinner","rare_model","不是一张大订单","life_restaurant_tier2",new[]{"life_curator","life_enterprise"},new[]{"park.delivery"},"接受任帆安排的饭局并实际参加。","与沈远交谈，听到孩子惦记旧展会模型。","沈远：孩子喜欢的不是新款，是初版那张窗边的小凳子。我问了几家都没货。找不到也没关系，别为这件事答应不该答应的事。","rare_model.request.heard","开启初版城市模型线索；沈远承担确认过的购买、修复费用。"),
            Node("model.clues","rare_model","旧展册上的一页","model_market",new[]{"life_tenant","life_creator","life_model_supplier","life_curator"},new[]{"model.dinner"},"已听到需求；询问认识的相关人物。","任选线索链：袁岚→陆栀，或周岑→任帆；查明程弈参与初版制作。","陆栀：后来量产版把凳子删了。程弈留过一件展样，不过先问他愿不愿意让出来，别直接给人许诺。","rare_model.maker.identified","任一链获得明确线索即可；另一链保留为补充，不强制重复跑。",Choice("creator","请袁岚介绍陆栀，查原作记录","通过作者找到程弈。","rare_model.clue.creator"),Choice("supplier","向周岑问旧批次，再找任帆核对展册","通过制作及展览记录找到程弈。","rare_model.clue.archive")),
            Node("model.repair","rare_model","缺了一角的展样","model_workshop",new[]{"life_maker","life_creator","life_enterprise"},new[]{"model.clues"},"经介绍约见程弈。","确认真伪、转让意愿、修复报价与工期；得到沈远同意后实际完成交易和修复。","程弈：底座裂了一角，修好也该告诉孩子它修过。我愿意转让，条件是保留作者署名，不把它当成全新未拆的货。","rare_model.repaired.and.acquired","获得有来源、有修复说明的实物；不靠好感直接生成物品。",Choice("restore","按约修复并保留修复记录","等待真实工期和费用结算。","rare_model.restore"),Choice("disclose","先带照片说明原状，再确认是否继续","买方不接受可暂缓，已发生费用不回滚。","rare_model.disclosure")),
            Node("model.delivery","rare_model","那张小凳子还在","life_service",new[]{"life_enterprise",CityLifeCatalog.ShenXiao,"life_maker"},new[]{"model.repair"},"实物已取得，约定交付时间。","当面交付、说明来源和修复、完成报销。","沈晓：就是这张小凳子。那道裂纹也留下吧，我想记得它以前去过展览。\n沈远：你问清楚才答应，过程中也一直有交代。这比说什么都能办重要。","rare_model.delivered.and.settled","获得重要客户沈远的认可；孩子是独立NPC，不是好感奖励道具。"),
            Node("park.recommendation","rare_model","两个人愿意介绍你","agency_s_sports",new[]{"life_curator","life_enterprise"},new[]{"model.delivery"},"任帆的合作履约认可与沈远的交付认可都成立。","参加公园街区介绍会，确认后续租赁手续。","任帆：我推荐他，是因为我们一起办过事。\n沈远：我补充的是，他也能把没有现成答案的小事交代到底。","district.park.introduced","开放公园经营资格；不强迫选择某个行业或恋爱路线。"),
            Node("brand.listing","brand_listing","从店名到品牌","life_company",new[]{CityLifeCatalog.TangWei,"life_enterprise"},Array.Empty<string>(),"在所属阶段的前置条件检查中使用；不可依赖尚未解锁的花园设施。","成立自有品牌/公司，并通过正式上市流程。","沈远：上市不是把别人的股票买一手。你得清楚这家公司持有哪些经营资产，对外承诺的又是什么。","own_issuer.listed","条件模块；没有发行机制时保持未达成，禁止对话按钮伪造上市。"),
            Node("industry.settlement","industry_dominance","一个行业的完整月份","life_company",new[]{"life_curator"},Array.Empty<string>(),"结算月收入及对应发行人股价达到门槛。","锁定行业、所属门店、结算月和发行人；在下一月发出邀约。","任帆：这次不是把所有生意凑在一起。有人注意到，你在这个行业已经持续做出了一定规模。","industry.month.qualified","保存结算事实和邀约月；同月同一事实不重复发邀请。"),
            Node("garden.invitation","garden_project","名单上的新名字","life_restaurant_tier3",new[]{"life_curator",CityLifeCatalog.LiangJing},new[]{"park.recommendation"},"默认：行业合格月的次月；若将行业模块移到终章，则采用当前阶段配置条件。","回应邀请并真实赴约，与梁敬交换联系方式。","梁敬：任帆说你做过不同规模的合作。我们正找能长期经营的人，今晚先聊，不急着签。","garden.meeting.completed","认识花园项目负责人，不直接授予完整区域资格。"),
            Node("garden.proposal","garden_project","空出来的一间铺子","agency_s_night",new[]{CityLifeCatalog.LiangJing,"life_tenant"},new[]{"garden.invitation"},"会面后获得项目资料。","了解限定试点的租约和责任，推荐袁岚或自愿参与的合适伙伴。","袁岚：地方很好，但这份保证金和租期，会把我原来的店也拖进去。我想试，不等于我付得起任何代价。","garden.project.reviewed","只开放该项目的洽谈/试点通行，防止必须先解锁花园才能完成解锁任务。"),
            Node("garden.terms","garden_project","一直如此，也可以再谈","agency_s_night",new[]{CityLifeCatalog.LiangJing,"life_tenant",CityLifeCatalog.TangWei},new[]{"garden.proposal"},"清楚长租与保证金的实际现金流压力。","谈出双方同意、自己能履行的方案。","梁敬：这套条件原先是为了减少撤店，但也确实挡住了一些人。改可以，风险和退出办法要写清楚。","garden.terms.signed","三条路线同等通向资格；不以强行救人或背叛伙伴作为唯一正确答案。",Choice("trial","谈分阶段试经营，按阶段决定是否续约","签订阶段条款，真实计费。","garden.terms.trial"),Choice("limited","在承受范围内提供有限支持","资金实际划出，写明上限；不允许无限担保。","garden.terms.support"),Choice("respect","尊重袁岚退出，另寻自愿合作者","保留关系，重新确认合作者意愿。","garden.terms.alternative")),
            Node("garden.fulfilment","garden_project","签字之后","agency_s_night",new[]{CityLifeCatalog.LiangJing,"life_tenant"},new[]{"garden.terms"},"所选方案真实签署且费用、责任已落实。","完成约定开业与首个履约检查；伙伴退出路线按实际新合作者结算。","梁敬：我们先看这次约定有没有兑现，再谈更长的合作。你没有替别人决定他必须成功，这一点我记住了。","garden.project.fulfilled","获得花园经营资格；项目亏损也按约承担，不凭收入预设成功。"),
            Node("garden.echo","garden_project","这次由你介绍","life_restaurant_tier4",new[]{CityLifeCatalog.LiangJing,"life_tenant",CityLifeCatalog.TangWei},new[]{"garden.fulfilment"},"获得资格后，在后续商户聚会遇到请求介绍的新经营者。","选择是否介绍、介绍哪些具体经历；不替对方担保未知事项。","唐蔚：当年有人介绍你，现在也有人想请你介绍。你可以拒绝，但不必用一句一直如此把话说完。","garden.echo.responded","主线收束于参与制定条件；介绍或婉拒均保留已获资格。",Choice("introduce","介绍自己确实了解的经历","只传递已获本人同意的信息。","garden.echo.introduce"),Choice("decline","说明自己了解不足，暂不推荐","提供可公开联系的途径，不惩罚已完成主线。","garden.echo.decline"))
        };

        private static PersonalNarrativeArc Arc(string id,string title,string person,string premise,string conflict,string resolution,params string[] others)=>new PersonalNarrativeArc{Id=id,Title=title,PersonId=person,Premise=premise,Conflict=conflict,Resolution=resolution,RelatedPeople=others};
        public static IReadOnlyList<PersonalNarrativeArc> SideStories {get;}=new[]{
            Arc("side.zhou","担保的名字","zhou","熟悉老周后，得知他曾为同行作担保。","朋友再求他签字，但口头义气与家中积蓄发生冲突。","可帮助核对条款或支持拒绝；不让玩家用付钱买结局。","li","lin"),
            Arc("side.jie","守住这一间","jie","阿杰愿意分享手机店的日常成本。","扩店邀请看起来体面，却会让原店失去熟练的人手。","一起算清账再选择，留下经营也算完整结局。","keeper_phone","keeper_phone2"),
            Arc("side.landlady","下一次续约","landlady","李姐谈起旧街住户与租约。","改善房屋的成本与老租户可承受租金矛盾。","分期维护、透明协商；不要求一方无条件承担全部。","res_wang","keeper_agent"),
            Arc("side.ming","第二块招牌","life_ming","许明在工作与交谈中谈起独立经营的愿望。","玩家需要熟手，员工却希望有自己的成长。","可协商交接、合作或体面离职；须尊重实际雇佣状态。","life_ning","life_qiao"),
            Arc("side.momo","镜头之外","momo","莫莫得到一份商业推广邀请。","合作方希望隐藏不利体验，影响观众信任。","披露合作、重谈脚本或拒绝；后续反映真实选择。","ake","qiao"),
            Arc("side.lin","还是同一个人吗","lin","在玩家经营规模变化后，小林谈起最近难约见。","旧朋友感受到生活节奏与话题的距离。","通过真实见面或诚实约定维持关系，不用财富强制断交。","res_coder"),
            Arc("side.creator","我已经不喜欢那个角色了","life_creator","陆栀讲起早期作品和后来删改的细节。","商业机会要求永远重复最成功的旧作。","可支持新作、保留旧版署名或坦诚终止合作；不强制恋爱。","life_maker","life_model_supplier"),
            Arc("side.clothing","合适的衣服","life_clothier","宋棉遇到需要正式着装但预算有限的熟客。","体面并不等于最高价，卖货目标与实际需要不同。","提供适合预算的搭配；不得用羞辱顾客触发剧情。","life_designer","life_clothsupplier"),
            Arc("side.designer","名字写在哪里","life_designer","温岚的版型被合作店采用。","追加订单希望抹掉原设计署名。","谈清使用范围与报酬，也可拒绝继续供稿。","life_clothier","life_creator"),
            Arc("side.supplier","先说清楚的一批货","life_clothsupplier","许纬接到缺货季的补单。","加急供货与质量稳定不能同时保证。","告知风险、分批交付或取消；实际订单结果留存。","life_clothier","resident_sujun"),
            Arc("side.models","初版与再版","life_model_supplier","周岑面对收藏热潮中的同款询价。","稀缺炒价与真实版本信息容易混淆。","公开批次来源，不把再版冒充初版；可影响供货信任。","life_maker","life_creator"),
            Arc("side.chef","一张留下来的饭桌","life_chef","杜师傅发现老张越来越少来吃饭。","便宜套餐解决不了独居时不愿出门的原因。","先问本人意愿，约一次普通饭局；不擅自公开私人困难。","zhang","resident_linhe"),
            Arc("side.maker","旧物的来路","life_maker","程弈收到一件待修旧模型。","委托方只想换新，原主人却珍惜使用痕迹。","核对归属和意愿后修复或保留原状，记录真实出处。","resident_guwen","life_creator"),
            Arc("side.tenant","下一次续租","life_tenant","袁岚准备续租现有小店。","新商圈机会与熟客、资金、家庭节奏发生冲突。","留在原店或参与试点都可成立；与花园主线共享已确认事实。",CityLifeCatalog.TangWei,CityLifeCatalog.LiangJing),
            Arc("side.curator","名单上还有谁","life_curator","任帆准备下一次展会推荐名单。","熟悉的合作方安全，新人却一直没有履历。","有限试点并公开标准，推荐基于实际合作证据。",CityLifeCatalog.TangWei,CityLifeCatalog.LiangJing),
            Arc("side.client","孩子惦记的不是价格","life_enterprise","沈远从工作谈到孩子的旧展会记忆。","越贵的新款并不等于孩子想要的那件。","按稀有模型主线听清需求并如实交付，不重复生成奖励。",CityLifeCatalog.ShenXiao,"life_curator"),
            Arc("side.agent","签约之前","keeper_agent","小陈发现新商户只盯着月租。","押金、用途和退出条款决定了真实风险。","给出明确条款，允许暂缓签约；不能用对话强租商铺。","landlady",CityLifeCatalog.TangWei),
            Arc("side.li","账期与交情","li","老李愿意谈进货账期的老经验。","熟人赊货逾期，收款会影响关系。","明示可承受的额度和期限，既能催款也能停止赊销。","zhou","wang"),
            Arc("side.tang","谁来通知小店","story_tangwei","唐蔚筹备商业街变更通知。","消息总先到大商户，晚知道的人承担更多调整成本。","逐一确认受影响商户收到通知，记录异议再实施。","life_tenant","resident_zhouran"),
            Arc("side.liang","一直如此","story_liangjing","梁敬回忆自己得到第一次试租机会。","今天他制定的统一门槛也挡住了后来者。","通过花园合作谈具体风险和退出办法，不靠一句劝说改变全部制度。","life_curator",CityLifeCatalog.TangWei)
        };
    }
}
