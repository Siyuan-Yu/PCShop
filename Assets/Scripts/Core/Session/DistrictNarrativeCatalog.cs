using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    // Authored content shared by previews and the district runner. Opening this catalogue
    // never grants completion evidence; the runner records actual meetings and actions.
    public enum NarrativeFact { OpenDistrictShops, Cash, NetAssets, OwnBrandFormed, ClosedSectorMonthlyRevenue }
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
        public const string ContentVersion="2026-11-07.2";
        public const string IntegrationStatus="正式主线只保留：三页寻图、品牌展会与订单交付、企业竞标与三期履约。旧活动、模型委托、门店试点及上市股价门槛已删除。";
        public const string RetryRule="可稍后再谈或重新约时间；保留已完成事实、实际费用和关系变化，不因错过一天清空整条线。";
        private static NarrativeGate Gate(NarrativeFact fact,decimal value,string scope,string text)=>new NarrativeGate{Fact=fact,Minimum=value,Scope=scope,Description=text};

        // Fresh objects on every call keep previews independent.
        public static DistrictNarrativeChapter[] CreatePlan() => new[] {
            new DistrictNarrativeChapter{Id="district.community",Title="城郊 → 社区",Reward="交付三页商铺图，开放社区。",Modules=new[]{"community_entry"},Gates=new[]{Gate(NarrativeFact.OpenDistrictShops,5,"suburb","城郊5家开业门店"),Gate(NarrativeFact.Cash,1000000,"player","现金100万元")}},
            new DistrictNarrativeChapter{Id="district.park",Title="社区 → 公园",Reward="同一品牌完成展会采购订单、知名度100，实际会面后开放公园。",Modules=new[]{"brand_exhibition"},Gates=new[]{Gate(NarrativeFact.OpenDistrictShops,6,"community","社区6家开业门店"),Gate(NarrativeFact.NetAssets,30000000,"player","净资产3000万元"),Gate(NarrativeFact.OwnBrandFormed,1,"player","已创建行业品牌")}},
            new DistrictNarrativeChapter{Id="district.garden",Title="公园 → 花园",Reward="赢得企业供应商席位，同合同连续三期交付后实际会面，开放花园。",Modules=new[]{"enterprise_tender"},Gates=new[]{Gate(NarrativeFact.ClosedSectorMonthlyRevenue,100000000,"one_owned_sector","品牌所属行业上个完整月营业额1亿元")}}
        };

        private static DistrictNarrativeNode Node(string id,string module,string title,string place,string[] people,string[] before,string entry,string objective,string dialogue,string evidence,string result,params NarrativeChoice[] choices)=>new DistrictNarrativeNode
        {Id=id,Module=module,Title=title,PlaceId=place,People=people,Prerequisites=before,Entry=entry,Objective=objective,Dialogue=dialogue,Evidence=evidence,Result=result,Retry=RetryRule,Choices=choices};
        private static NarrativeChoice Choice(string id,string label,string effect,string evidence)=>new NarrativeChoice{Id=id,Label=label,Consequence=effect,Evidence=evidence};
        public static IReadOnlyList<DistrictNarrativeNode> Nodes {get;}=new[]{
            Node("expo.invitation","brand_exhibition","品牌第一次正式亮相","life_expo",new[]{"life_curator",CityLifeCatalog.TangWei},Array.Empty<string>(),"品牌及社区经营达到引荐条件。","接受任帆的季度展会邀请。","任帆：店开得好，还得让同行看到。云帆每季度办展，你选好代表商品和接待方向，亲自来见买家。唐蔚：展后答应的订单也要交出来，才算站稳。以前参加的展会和完成的交付也算；已经达标就带记录来确认，不用重新参加。","expo.invited","获得正式参展邀请，开始准备展位。"),
            Node("expo.prepare","brand_exhibition","把门店带到展台","life_expo",new[]{"life_curator",CityLifeCatalog.TangWei},new[]{"expo.invitation"},"已接受展会引荐。","为选定品牌报名季度展会。","任帆：选小、中、大摊位，每种展品准备100件，邀请本品牌签约KOL。中大型还需提前委托海报。展会连续4天，现场得由你亲自主持。","expo.prepared","展位与样品已确认，下一步亲自接待。"),
            Node("expo.reception","brand_exhibition","让买家记住你","life_expo",new[]{"life_curator"},new[]{"expo.prepare"},"已报名。","连续4天处理现场事件与舞台互动。","任帆：每天都有人提问，也会发生排队、采访和演示意外。签约KOL必须真实到场；判断与展示决定能否获得采购线索。错过这届，下季度还能再来。","expo.received","完成正式亮相，获得采购线索。"),
            Node("expo.delivery","brand_exhibition","展会散场以后","life_expo",new[]{"life_enterprise","life_curator"},new[]{"expo.reception"},"已接待买家。","交付展会订单，品牌知名度达到100。","沈远：台上说得再好，也要看货能不能准时到。先把约定数量的货送到客户手里，我们再谈公园商圈的引荐。发货单不等于收货单。","expo.delivered","知名度和真实交付达标，可以约见推荐人。"),
            Node("expo.review","brand_exhibition","展会之后的交付","life_expo",new[]{"life_curator","life_enterprise"},new[]{"expo.invitation"},"参展并完成后续订单。","同一品牌知名度至少100，并真实交付展会线索订单。","沈远：我们看展会，也看展会之后。请带上已经履约的记录，任帆和我会共同引荐你进入公园商圈。","expo.fulfilled","完成正式展会和交付，开放公园道路。"),
            Node("tender.invitation","enterprise_tender","席位只有一个","agency_s_sports",new[]{"life_enterprise","life_tenant"},Array.Empty<string>(),"单一品牌行业月营收达到门槛后进入次月。","了解企业采购的年度供应商席位。","沈远：原供应商还在，你没有空位可坐。先了解采购要求，再交试单、报方案。袁岚：拿下只是开始，连续履约才有人替你担保。","tender.invited","开放竞标主线，去品牌页准备企业采购方案。"),
            Node("tender.prepare","enterprise_tender","摸清采购方与对手","agency_s_sports",new[]{"life_enterprise","life_tenant"},new[]{"tender.invitation"},"已接受席位挑战。","报名年度竞标并准备需求调查。","沈远：先报名，再来找我核实交货要求。袁岚：原供应商不会自己让位，我能帮你了解他打算怎么应对。最后报什么方案，由你决定。","tender.prepared","进入正式竞标，准备试单。"),
            Node("tender.trial","enterprise_tender","让试单替你说话","life_company",new[]{"life_enterprise","life_tenant"},new[]{"tender.prepare"},"已报名。","提交方案并实际交付10件试单。","沈远：价格、服务、联合保障都能谈，但先做一笔真实的试单。袁岚：对手看到你的报价会回应，别把自己的周转和供货能力全部押上去。","tender.tried","试单到货，可以评审席位。"),
            Node("tender.decision","enterprise_tender","挤进供应商名单","agency_s_sports",new[]{"life_enterprise","life_tenant"},new[]{"tender.trial"},"试单已到货。","评审超过原供应商，取得年度席位。","沈远：现在对照需求、试单和履约保障评审。袁岚：失利也不是断路，保住生意，下一年度还能重新投标。","tender.won","取得席位，正式考验开始。"),
            Node("tender.fulfilment","enterprise_tender","把席位坐稳","life_company",new[]{"life_enterprise","life_tenant"},new[]{"tender.decision"},"已经中标。","连续完成同一合约的三期交付。","沈远：中标只是开始，接下来三期每期30件，都要真实交货。袁岚：货源、库存和现金要一起安排，不能靠一张漂亮报价一直坐在名单上。","tender.delivered","三期履约完成，可以申请花园引荐。"),
            Node("tender.review","enterprise_tender","用交付赢得席位","agency_s_sports",new[]{"life_enterprise","life_tenant"},new[]{"tender.invitation"},"取得席位并履约三个周期。","完成一份企业合约的连续三期交付。","沈远：你已经证明不只是报价好看。这份履约记录可以带进花园。袁岚：进去以后再看看，谁在制作内容、谁掌握渠道，事情不只在台前。","tender.fulfilled","获得花园引荐，解锁机构与渠道合作。"),
            Node("community.intro","community_entry","隔壁街的机会","agency",new[]{"landlady","keeper_agent"},Array.Empty<string>(),"城郊门店及现金门槛达成后，李秀兰在见面或电话中提起。","通过陈启明约见唐蔚；确认时间和地点。","李秀兰：你不是只开了一家试试了。陈启明认识隔壁街做商户事务的人，我问问她愿不愿意见你。","appointment.tang.accepted","建立真实邀约，不瞬移、不立即授予资格。",Choice("accept","请陈启明帮忙约时间","进入会面预约。","appointment.tang.accepted"),Choice("later","最近忙，改天再说","保留机会。","")),
            Node("community.meeting","community_entry","搬家时散落的三页图","agency",new[]{CityLifeCatalog.TangWei},new[]{"community.intro"},"先找陈启明约唐蔚，再赴约了解唐蔚遇到的困难。","接受找回旧街手绘商铺图的委托。","唐蔚：我正在准备街区介绍会，想把旧街还在经营的小店也放进去。可那张图原本是三页拼起来的，搬家时拆开夹进不同的资料箱，分别托了三个人保管。李秀兰知道是谁，你能帮我把三页找齐吗？\n那张图上有些小店还没进新名册，别因为资料丢了就把他们漏掉。","community.map.accepted","开始寻物委托，先向李秀兰询问；本次会面不开放道路。"),
            Node("community.clue","community_entry","问问那天帮忙的人","life_home_s_oldhomes",new[]{"landlady"},new[]{"community.meeting"},"接受委托后约见李秀兰。","打听搬家箱子的去向及地图的包装。","李秀兰：三页没丢，我、林远航和周维成各保管了一页。我爱去城郊公园散步，林远航喜欢在街坊餐厅和老朋友吃饭，周维成爱到电子批发市场找李承业看旧收音机、聊评书。你分别去找我们聊聊吧，熟悉以后一起走走，活动结束时我们就告诉你各自的资料箱放在哪里。","community.map.clue","记录三位保管人与各自喜好的活动；此时不生成箱子。"),
            Node("community.search","community_entry","散落在三条街的图页","shop12",new[]{"landlady","lin","zhou"},new[]{"community.clue"},"已从李秀兰那里取得有效线索。","分别与三位保管人交往、完成喜欢的活动并打听，找到三只箱子中的图页，再当面交给唐蔚。","陈启明：三页分别画着旧居街、商业街和批发市场，沿着街道接缝就能拼起来。每个人的箱子要等他整理好、告诉你藏点后才会出现，不用现在满地图盲找。","community.map.found","三处现场分别取物；三页齐全后当面交付，完成前不开放道路。"),
            Node("community.trial","community_entry","把旧街带进新名单","agency",new[]{CityLifeCatalog.TangWei},new[]{"community.search"},"已经实际找到手绘商铺图。","回城郊中介，当面交还地图。","唐蔚：三页都找回来了，旧水塔和旁边的备注都还在。我能把遗漏的小店补上了。\n谢谢你把这件事跑清楚。接下来由我介绍社区的经营联系人，具体租约我们按正常手续谈。","community.map.delivered","交付任务物品，开放社区道路与经营接洽。"),
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
            Arc("side.tenant","下一次续租","life_tenant","袁岚准备续租现有小店。","新商圈机会与熟客、资金、家庭节奏发生冲突。","留在原店或搬迁都可成立；记录本人意愿。",CityLifeCatalog.TangWei,CityLifeCatalog.LiangJing),
            Arc("side.curator","名单上还有谁","life_curator","任帆准备下一次展会推荐名单。","熟悉的合作方安全，新人却一直没有履历。","有限试点并公开标准，推荐基于实际合作证据。",CityLifeCatalog.TangWei,CityLifeCatalog.LiangJing),
            Arc("side.client","孩子惦记的不是价格","life_enterprise","沈远从工作谈到孩子的旧展会记忆。","越贵的新款并不等于孩子想要的那件。","听清本人需求，再讨论纪念品与回忆。",CityLifeCatalog.ShenXiao,"life_curator"),
            Arc("side.agent","签约之前","keeper_agent","陈启明发现新商户只盯着月租。","押金、用途和退出条款决定了真实风险。","给出明确条款，允许暂缓签约；不能用对话强租商铺。","landlady",CityLifeCatalog.TangWei),
            Arc("side.li","账期与交情","li","李承业愿意谈进货账期的老经验。","熟人赊货逾期，收款会影响关系。","明示可承受的额度和期限，既能催款也能停止赊销。","zhou","wang"),
            Arc("side.tang","谁来通知小店","story_tangwei","唐蔚筹备商业街变更通知。","消息总先到大商户，晚知道的人承担更多调整成本。","逐一确认受影响商户收到通知，记录异议再实施。","life_tenant","resident_zhouran"),
            Arc("side.liang","一直如此","story_liangjing","梁敬回忆自己得到第一次试租机会。","今天他制定的统一门槛也挡住了后来者。","通过花园合作谈具体风险和退出办法，不靠一句劝说改变全部制度。","life_curator",CityLifeCatalog.TangWei)
        };
    }
}
