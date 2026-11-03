using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public static partial class CityLifeCatalog
    {
        public const string TangWei="story_tangwei", LiangJing="story_liangjing", ShenXiao="story_shenxiao";
        public const int NarrativePopulation=120;
        private static readonly string[] KeyPeople={"landlady","keeper_agent","lin","zhou","jie","li","life_curator","life_tenant","life_enterprise","life_creator","life_maker","life_clothier","life_designer","life_clothsupplier","life_model_supplier","life_chef","life_ming","momo",TangWei,LiangJing};

        // Each row is a stable authored identity, not a name regenerated on scene changes.
        // New resident cohorts retain their own three-part histories and small social circles.
        private static readonly string[][] ResidentRows={
            new[]{"yuanan","袁安","洗衣店收衣员","29","old","shop18","修补旧衣","存钱买一台缝纫机","第一份工作负责认领衣物，我把认不出的衣标画进小本子。","有位顾客少了一粒旧扣子，我找了一下午，后来才知道那件衣服是她母亲留下的。","现在收衣先问哪些地方不能改。我想做修补，不只把旧东西换新。","res_fang","同一条街互相介绍顾客"},
            new[]{"hexin","何欣","快递站分拣员","25","old","shop18","羽毛球","考取货运驾驶资格","刚上夜班时，我总把同音的街名分错袋。","小吴带我实际走过一次巷子，纸面上的地址才有了样子。","我把错分过的路口标在地图上，准备考证后负责一条固定路线。","res_rider","核对送件路线的朋友"},
            new[]{"gaobo","高博","家电维修学徒","22","old","shop13","拆解旧收音机","独立修好一台老收音机","师父第一次让我拆机器，我把螺丝按位置摆满一桌。","许明教我先量供电，别看到坏件就急着换。","爷爷那台收音机还在等零件。我答应修好以后陪他听一场球。","life_ming","一起学维修的朋友"},
            new[]{"penglan","彭兰","社区保洁员","48","old","life_service","种薄荷","给楼道留一个整洁的休息角","我清早上班，最先认识的不是店主，而是每家店开门的声音。","周大爷会把长椅挪到树荫里，我们商量给推车留出路。","最近在门口养了两盆薄荷。谁想摘都行，记得留一点让它再长。","res_grandpa","一起照看楼下长椅"},
            new[]{"sujun","苏骏","夜班仓管","34","old","clothing_wholesale","做面食","把轮班后的睡眠安排稳定","以前交班只数箱子，漏掉一箱受潮的布料，被下一班发现了。","乔禾没有替我遮掩，我们一起补上温湿度和异常记录。","现在交班快了，我也能准时回家发面，晚上带给一起值班的人。","life_qiao","合作过的仓储同事"},
            new[]{"dengyue","邓悦","夜校插画学生","21","old","shop01","画店铺招牌","画完一条街的早餐店","白天做店员，晚上赶去学画，我最初只敢照着照片描。","顾宁帮我把排班和课程写在一张表上，我终于留出两晚固定上课。","现在会征求店主同意再画招牌。画完一条街，准备办个很小的展示。","life_ning","夜校认识的同学"},
            new[]{"qiufeng","邱峰","公交调度助理","41","old","metro_a","象棋","整理一份夜班换乘提示","以前赶末班车错过一站，只能走回去，那晚记住了几个没有灯的路口。","张阿姨说她看不懂缩写站名，我把提示改成了路边能认出的建筑。","我想把这份换乘纸贴在店里，下晚班的人至少知道下一班还来不来。","res_auntie","帮忙核对夜班站点"},
            new[]{"luwei","陆薇","社区烘焙店员","31","community","shop15","手冲咖啡","试做一款低糖早餐面包","刚跟刘师傅学和面时，我总想多加糖让客人说好吃。","一位熟客说太甜，我才开始记录不同人的口味。","最近试做低糖面包，先做六个听意见，不急着印一整套包装。","res_baker","跟着学习烘焙的同事"},
            new[]{"chenxia","陈霞","社区会计","43","community","life_service","散步和旧账本","让小商户看得懂自己的账","小时候帮家里记卖菜钱，我以为余额就是赚到的钱。","后来给一家小店补账，才发现进货和借款混在一起会让人判断错。","顾宁来做练习时，我让她先问老板的钱从哪里来，再谈表格怎么画。","life_ning","夜校实务课的指导人"},
            new[]{"yeli","叶莉","社区护士","35","community","shop10","养多肉","固定每周一次河边散步","刚工作时我习惯把所有班都接下来，回家连饭都懒得热。","何药师提醒我先把自己的作息排好，才能长久照顾别人。","现在周末和朋友走一段河岸，不聊值班，也不比谁更忙。","res_pharm","经常沟通用药提醒的同行"},
            new[]{"xuwei","徐薇","亲子手工老师","33","community","life_service","纸艺","让孩子自己完成一件不完美的作品","第一堂课，我替孩子剪好了所有材料，看上去很整齐。","宋青问我孩子们究竟做了哪一步，那句话让我重新设计课程。","现在剪歪的屋顶也留着。孩子说那是风吹斜的，我觉得很有意思。","life_teacher","共同组织社区课堂"},
            new[]{"zhouran","周然","物业值班员","28","community","life_home_s_services","跑步","把报修事项交代清楚再下班","刚入职时我怕惹麻烦，报修只写一句已经联系。","一次漏水换了三班还没解决，我才知道下一班根本不知道联系了谁。","现在记录具体到谁会来、何时再问。唐蔚办活动也会提前和我核对出入口。",TangWei,"协调场地出入的工作伙伴"},
            new[]{"guwen","顾文","钟表修理师","56","community","shop13","老式机械表","把修表笔记教给愿意学的人","我学徒时先练清洗，三个月才碰到一只真正的机芯。","程弈拿来一个模型的小齿轮，我们一起试了几种垫片。","有些东西修好也不值多少钱，但还能走，就值得把过程记下来。","life_maker","交流机械结构的朋友"},
            new[]{"linhe","林荷","社区餐厅领班","38","community","life_restaurant_tier2","听评书","让新人学会安排而不是硬扛","第一次带班我把好说话的人排满了最忙的时段。","杜师傅提醒我，没抱怨不等于不累，我重新问了一遍大家的安排。","现在交班前留出十分钟，把没做完的事说清，不让新人猜。","life_chef","请教过带班经验"},
            new[]{"hanzheng","韩征","展会电工","39","park","life_expo","修手电筒","给小展位也留足用电准备","我第一次布展只按报表装插座，现场却多出几盏大灯。","任帆陪我逐个问清设备，才避免开场后再拉满地临时线。","如今我提前给新人发一张简短清单，写清需要带什么，不靠熟练程度猜。","life_curator","多次一起搭建展会"},
            new[]{"baiming","白明","展陈设计助理","26","park","life_expo","拍街角窗户","独立完成一个能让人停留的小展台","实习时我把展台做得很亮，观众却看不清说明。","陆栀让我站到小孩的高度再看一次，发现入口挡住了作品。","现在先画人的行走路线，再决定装饰。我想做一场真正有人看完的小展。","life_creator","合作过模型展示"},
            new[]{"zhaoyu","赵予","配乐录音师","30","park","life_ktv_tier3","采集雨声","做完一张城市声音小样","第一段录音里全是空调声，我觉得器材一定不够贵。","温音让我先换位置，才发现最重要的是愿不愿意认真听。","最近录了早市收摊和夜班车门，想把城市普通的一天连起来。","life_singer","互相试听原创旋律"},
            new[]{"taoran","陶然","跑团领队","36","park","life_fitness","城市慢跑","开一条新人能跟上的路线","以前带跑只想着配速，回头才发现最后的人掉队了。","郑禾建议我设一个不用比赛的组，愿意慢跑的人反而多起来。","现在出发前先问大家今天想跑多远，提前回家也不必解释。","life_coach","一起组织大众运动"},
            new[]{"mengqiu","孟秋","图书编辑","42","park","life_service","旧报纸","整理一册本地店主口述","我做第一本口述稿时，把犹豫和停顿都删掉了。","叶书说那些停顿也有原因，我回去重新听完了录音。","现在每篇都请讲述者确认，卖得慢一点，也不替别人决定他们的人生。","life_librarian","共同整理地方口述"},
            new[]{"shiyang","石扬","绿化工程员","45","park","life_park","认树","保住河岸一段树荫","刚做绿化时觉得树越整齐越好，修剪后才发现长椅失去了阴凉。","方林带我看老人什么时候来坐，我们把养护时间错开了。","这一季的新苗长得慢，我准备多记两年的变化，再决定要不要补种。","life_gardener","一起维护河岸树木"},
            new[]{"wuyou","吴悠","艺术品装裱员","32","park","life_studio","纸张修复","开一次免费的保管小讲座","我曾用漂亮的新纸替换一张旧衬纸，客户却很难过。","袁岚告诉我，旧东西不只需要看上去新，也要问主人想留下什么。","现在动手前先讲清能修到哪里，想把这些保管经验教给街坊。","life_tenant","经常委托旧招牌装裱"},
            new[]{"jiyun","季云","花园会客厅管家","47","garden","life_restaurant_tier4","兰花","让服务有明确边界","刚入行时我总说什么都可以，结果把同事的时间也答应出去了。","梁敬有次问我到底能安排几个人，我才学会先核实再承诺。","现在会把能做和不能做都讲清楚，客人反而比较放心。",LiangJing,"长期接待合作方"},
            new[]{"rongke","容可","室内陈设顾问","34","garden","shop12","旧木器","设计一间普通预算也能住的房间","第一份方案堆满了漂亮家具，却没有给主人留放杂物的位置。","周敏带我看客户真正的生活习惯，我才开始先问东西放在哪。","最近想做一套小房间方案，预算少也能把每一天用顺手。","staff_mall","一起核对家具用途"},
            new[]{"xiaqing","夏清","宴会统筹","40","garden","life_restaurant_tier4","拼图","让开场前最后半小时安静一点","以前总在最后一刻换座位，服务员背好的位置全作废。","林荷提醒我，改动不只是一张纸，所有走动的人都要重新适应。","如今临时要求会先确认代价，尽量让每个人知道自己下一步做什么。","resident_linhe","交流宴会排班经验"},
            new[]{"wenbo","温博","商务翻译","37","garden","life_company","做家常菜","保留不接工作的晚饭时间","有一年我把每次聚餐都当成新的工作机会，后来谁都不敢随便约我。","小林笑我吃饭像在开会，我才意识到自己很久没问朋友过得怎样。","现在每周留一晚不接临时翻译，做两道普通菜，聊点没有结论的事。","lin","偶然结识的饭友"},
            new[]{"siyu","司雨","财务分析助理","27","garden","life_company","看鸟","完成一份诚实的经营分析","第一份报告我选了最好看的月份，主管问剩下的月份去哪了。","沈远让我去看一次真实交付，我才明白数字背后是谁在等。","现在会把估计和已经发生的分开写。周末去河岸看鸟时，倒不必预测。","life_enterprise","核对采购记录的同事"},
            new[]{"jiangning","江宁","精品酒店礼宾","29","garden","life_bar_tier4","画步行地图","整理一份不只推荐贵店的城市地图","刚做礼宾时，我只记住了客单价最高的几家店。","有位客人只想吃碗热面，我跟着杜师傅的指路才发现附近的小馆。","现在会先问客人想过怎样的一天，再给路线。贵不贵不是唯一问题。","life_chef","向他请教街坊餐馆"},
            new[]{"luoya","罗雅","社区合唱团成员","58","garden","life_service","合唱","办一场不用穿礼服的邻里演出","我第一次上台紧张得忘词，旁边的人替我接了下一句。","温音后来教我们听彼此的呼吸，声音小的人也能找到位置。","下一次演出想请楼下值班员来看，谁都不用先学会端着坐。","life_singer","合唱排练的朋友"},
            new[]{"caihong","蔡虹","批发市场打包员","44","wholesale","model_market","编绳","把打包损耗降下来","最初只想着包得紧，第一箱模型到货时却压弯了小零件。","周岑让我跟着看一次拆箱，才发现收货人最担心的地方。","如今不同盒子有不同垫法，省材料也不能省掉关键的一层。","life_model_supplier","模型批发行同事"},
            new[]{"luchen","陆晨","面料验货员","28","wholesale","clothing_wholesale","骑行","整理一套容易看懂的面料样卡","我第一次验货只对颜色，忽略了同批布料手感不同。","许纬把退回的样布留给我，让我洗过晒过再比较。","现在样卡上会写缩水和用途，客户摸得到，也看得懂。","life_clothsupplier","一起核对服装到货"},
            new[]{"songan","宋安","货运司机","46","wholesale","dealer","钓鱼","留出一段不赶路的休息时间","年轻时觉得多跑一趟就是本事，后来困得在停车场睡过了交货时间。","老马劝我把等待装卸也算进路线，不能只看车开了多久。","现在宁可说明明早到，也不随口承诺今晚一定到。","keeper_dealer","长期维护车辆的熟人"},
            new[]{"xuweilin","许蔚林","电子仓库盘点员","33","wholesale","stall_mem","数独","把借调件也记进盘点","一次盘点少了三条内存，所有人都以为是我数错。","钱姐陪我查了两天，原来借去测试的记录没归档。","现在谁借走、什么时候还都留条目，账清楚以后反而少了互相猜疑。","qian","一起核对存储库存"},
            new[]{"yucheng","俞程","售后检测员","24","wholesale","stall_gpu","拼装模型","学会把故障解释给外行听","刚上岗我把检测术语念给客户听，对方一句也没明白。","阿强让我拿实物指出哪里异常，比连说十个缩写有效。","现在检测单会多写一句对使用有什么影响，不想让人只听见坏了两个字。","staff_gpu","带过自己的售后同事"},
            new[]{"duma","杜玛","运输调度员","35","wholesale","model_market","看火车","让拼车也能说清交期","第一次安排拼车，我只按重量凑满，忘了有人急着用。","乔禾告诉我先问截止时间，再看货能不能一起压在车底。","现在清单会分怕压和急用，少打一通催货电话就是好事。","life_qiao","一起安排运输路线"},
            new[]{"leiming","雷鸣","市场电器维修员","52","wholesale","stall_psu","口琴","教会学徒先断电再检查","我刚入行时急着表现，总想一步找出故障。","老孙让我先把安全步骤做完整，说慢几秒总比出事强。","现在教新人也这样，练熟顺序以后，速度自然会有。","sun","多年的市场维修伙伴"},
            new[]{"fanglu","方露","书店活动助理","23","culture","life_service","读诗","做一次只有十个人也值得的读书会","第一次活动报名很多，真正来的人只有四个，我觉得失败了。","老张坐下来读了两页，四个人后来聊了整整一个下午。","现在先准备好几本真想讨论的书，人数少也照常摆椅子。","zhang","一起办过读书会"},
            new[]{"ningxi","宁汐","模型摄影师","26","culture","model_workshop","微距摄影","拍出模型被实际把玩的样子","我最初把样件拍得毫无瑕疵，买家却说看不出大小。","陆栀让我加一张手拿着的照片，也留下正常拼缝。","现在一组照片里会有真正能说明用途的角度，漂亮和清楚都重要。","life_creator","原创模型拍摄伙伴"},
            new[]{"pancheng","潘澄","道具制作员","31","culture","life_studio","纸板建筑","把旧材料做成耐用展具","第一次做展具只试了空载，放上作品才开始摇。","程弈陪我重做了底座，没有替我把歪架子藏起来。","现在先测试再上漆，剩下的纸板留给社区孩子做小房子。","life_maker","共同制作展具"},
            new[]{"zhengke","郑可","服装版师助理","25","culture","clothing_boutique","逛旧货摊","做出一件能长期穿的外套","我曾照着图把衣服做得很窄，试穿的人连抬手都费劲。","温岚让我自己穿着搬一箱布料，才懂活动量不是纸上的数字。","现在打样会留一轮实际试穿，我想让外套穿旧以后还舒服。","life_designer","原创服饰工作同事"},
            new[]{"lanxin","蓝心","陶艺工作者","36","culture","life_studio","做茶杯","开一次新手不怕失败的体验课","第一次烧窑裂了一排杯子，我一度只敢让学生看成品。","袁岚说她也想知道失败的杯子长什么样，我们把碎片摆了出来。","现在体验课先做一个小碟子，歪一点也能端回家用。","life_tenant","一起办过街区展示"},
            new[]{"guanghe","关禾","地方志志愿者","61","culture","life_service","旧照片","找回一张合照里的人名","退休后整理照片，发现自己记得街名，却忘了旁边人的名字。","叶书帮我做了登记卡，我们挨家问的时候也会有人说记不清了。","想不起来的先留空，下一次有人认出来，还能接着补。","life_librarian","一起收集街区口述"},
            new[]{"yujia","余佳","原创玩具顾客","28","culture","life_company","收藏城市模型","给每件收藏留一张购买记录","第一件模型是旅行时买的，我忘了作者名字，只记得店门口下雨。","周岑帮我查到系列，原来作者后来就在这座城。","现在会记下在哪里买、为什么喜欢；价格涨不涨不是我留着它的理由。","life_model_supplier","常来问系列出处的熟客"},
            new[]{"wanqing","万晴","早餐摊帮工","30","food","shop15","研究汤底","独立做好一锅稳定的早餐汤","我以前靠感觉加盐，人一多就容易手重。","胖哥教我先记清水量，忙的时候也能照着基础做。","这阵子每天留下半碗比较，想把普通的一锅汤做稳定。","res_cook","请教过后厨准备"},
            new[]{"luhai","卢海","餐厅洗碗员","49","food","life_restaurant","听广播","把换班安排得不耽误接孩子","我原来答应所有临时加班，接孩子总要拜托邻居。","杜师傅问清我的时间，才知道我不是不愿意说，是怕说了丢工作。","现在提前写好能换的班，厨房也更容易安排。","life_chef","同一厨房的同事"},
            new[]{"renxin","任昕","冷链收货员","32","food","life_market","散步","学会判断食材到货状态","第一回收货我只看数量，没留意箱角已经湿了。","王婶让我打开看一眼，说账上的一箱和真正能用的一箱不同。","现在交接会记温度和异常，不拿签过字代替看过货。","keeper_farm","一起核对食材收货"},
            new[]{"zengyu","曾玉","饮品研发助理","24","food","life_bar_tier2","调无酒精饮料","做出一款不用很甜也好喝的饮品","我第一次调饮靠糖浆遮掉所有酸味，喝完只记得甜。","夏澄让我一次只改一种配料，慢慢才喝得出变化。","新配方还没定名，先请下班的朋友尝，再决定要不要上菜单。","life_bartender","一起试过饮品配方"},
            new[]{"lishan","李善","社区食堂采购员","54","food","life_market","养葱","保持菜单价格说得清楚","以前原料涨了我就临时少买一点，后厨总到开饭才发现不够。","王婶把每天报价写给我，我们才开始提前商量替换。","现在更换食材会写在菜单上，吃饭的人知道今天为什么不同。","keeper_farm","长期食材采购往来"},
            new[]{"moxin","莫欣","夜班便利店员","27","food","shop18","写短日记","保留每天十分钟写字","夜班没客人时，我会记今天见到的人，但只敢写一句。","温音来买热饮，说一句也能记住一个晚上。","最近终于写满一本，没有大故事，大多是谁又忘带伞。","life_singer","下夜班常遇见的朋友"},
            new[]{"quyuan","曲原","餐具配送员","40","food","life_restaurant_tier2","骑自行车","改掉抢时间时乱堆货的习惯","刚送餐具时为了快，把回收筐和干净筐挤在一起。","林荷当面退回那一车，我们重新定了摆放顺序。","现在每趟慢一点装车，路上少摔碎几只盘子，月底反而省了钱。","resident_linhe","固定餐厅配送伙伴"},
            new[]{"maoning","茅宁","夜班保安","46","leisure","life_ktv_tier2","晨练","换班后还能睡个完整觉","夜里值班白天帮家里跑事，我总觉得睡觉可以往后拖。","陶然带我做了很短的晨练，之后反而劝我早点回去休息。","现在下班只走一小圈，别人的长跑成绩不用拿来和自己比。","resident_taoran","晨练时认识的朋友"},
            new[]{"peiran","裴然","自由剪辑师","29","leisure","life_studio","骑车拍风景","把每周休息日真的留出来","第一份合作我承诺随时修改，手机响起来就不敢停。","莫莫教我把交稿和修改次数写清，合作才没有一直拖着。","现在周末骑车不拍工作素材，也允许一段风景只留在记忆里。","momo","一起剪过教程视频"},
            new[]{"dongyu","董宇","球场器材管理员","33","leisure","life_court_mid","修篮球网","办一次不用抢场地的友谊赛","刚管器材时我只记得常客，新来的人不知道找谁借球。","郑禾让我把说明贴出来，也给新手留一个时间段。","现在认识的人更多了，我希望大家不用先跟我熟才敢问。","life_coach","一起维护社区球场"},
            new[]{"qiaosong","乔松","退休园艺爱好者","64","leisure","life_park","嫁接植物","养活一株从旧院子带来的树苗","搬家时我只带走一盆树苗，别人都说重新买一盆更方便。","方林帮我看了根，没有保证一定能活，只说先别急着换盆。","今年发了两片新叶，我每天看看，不再老想着它以前多大。","life_gardener","照看树苗时认识的朋友"},
            new[]{"fuzhi","傅知","练歌房音响助理","22","leisure","life_ktv","原创伴奏","把一首自己的曲子完整录下来","第一次调音把所有旋钮都拧大，以为响就是好听。","温音让我先听人声，才知道有些时候要学会留空。","我的曲子只写了一分钟，但这次想先完整录完，不急着发出去。","life_singer","同班工作的音响伙伴"},
            new[]{"shaoan","邵安","周末钓友","57","leisure","shop17","河岸钓鱼","学会放下已经过时的比赛心态","刚退休时每次钓鱼都比谁钓得多，空手回家就不高兴。","老李陪我坐过一个没口的下午，聊完觉得这趟也没白来。","如今带个保温杯，能坐一会就行，鱼不是每次都得证明什么。","li","河岸一起钓鱼的朋友"},
            new[]{"luqiao","卢桥","户外活动领队","38","leisure","life_hike","认路和野餐","整理一条适合慢走的线路","以前我按自己脚程排活动，队尾的人一路只顾追赶。","陶川提醒我把停下来的时间也算进去，走路才不只是赶路。","现在有一条很短的路线，带孩子或想聊天的人也能从容走完。","life_golfer","交流户外活动安排"},
            new[]{"yinhe","尹禾","社区维修接线员","35","community","life_service","拼图和听故事","把每一次报修的后续问清楚","刚接电话时，我总急着把工单转出去，觉得这样就算处理完了。","周然回访时发现有户人家一直没等到维修，我们一起补问了原因。","现在每天留十分钟看没结清的单，有些问题只是差一次有人再问。","resident_zhouran","共同跟进社区报修"}
        };

        private static string CohortHome(string cohort)=>cohort=="old"||cohort=="wholesale"?"life_home_s_oldhomes":cohort=="community"||cohort=="food"?"life_home_s_services":cohort=="garden"?"life_home_s_newtown":"life_home_s_sports";
        private static string[] CohortLeisure(string cohort)=>cohort=="garden"?new[]{"life_bar_tier4","life_golf","life_restaurant_tier4"}:cohort=="park"||cohort=="culture"||cohort=="leisure"?new[]{"life_park","life_fitness","life_bar_tier3"}:cohort=="wholesale"?new[]{"metro_b","model_market","clothing_wholesale"}:new[]{"life_court","life_market","life_restaurant_tier2"};
        private static string CircleName(string cohort)=>cohort=="old"?"旧街邻里":cohort=="community"?"社区生活":cohort=="park"?"公园工作圈":cohort=="garden"?"花园生活":cohort=="wholesale"?"批发物流":cohort=="culture"?"创作与收藏":cohort=="food"?"餐饮与夜班":"运动与闲暇";

        private static void ConfigureNarrativePeople(List<CityPersonDefinition> people)
        {
            void Add(string id,string name,string role,int age,string home,string work,string interest,string goal,string a,string b,string c)=>people.Add(new CityPersonDefinition{Id=id,Name=name,Role=role,Age=age,HomeId=home,WorkId=work,WorkStart=540,WorkEnd=1080,Interest=interest,Goal=goal,Experience=a,Stories=new[]{a,b,c},Topics=new[]{a,"我喜欢"+interest+"。",goal,c}});
            Add(TangWei,"唐蔚","社区商业街运营负责人",38,"life_home_s_services","agency_s_services","走访街边小店","把共同事务安排清楚，让新商户也知道如何参与。","我从商户登记做起，最初只会照着表格问问题。后来才知道，同一笔押金对不同店主意味着不同的退路。","一次活动临时改位置，最早报名的小店反而没被看见。我挨家解释后，把变更确认加进了流程。","现在有人推荐新人，我会先问具体做过什么。老商户的经验有用，新人也该有一次把事情做出来的机会。");
            Add(LiangJing,"梁敬","花园商业项目运营合伙人",46,"life_home_s_newtown","agency_s_night","木工和小型商户聚会","找到能够承担长期经营责任的合作伙伴。","我早年也被要求先拿履历。第一位肯给试租机会的人没有替我免租，只是允许分阶段证明自己。","后来我参与制定统一保证金，确实减少了撤店，却也把一些认真经营的小店挡在外面。这个结果我不能说没看见。","有伙伴提出试经营，我会谈清退出和损失由谁承担。既然条件可以谈，就不该只拿一直如此来结束谈话。");
            Add(ShenXiao,"沈晓","喜欢城市模型的小学生",11,"life_home_s_services","life_service","城市模型和画公交站","把旧展会上看过的城市模型和自己画的街景摆在一起。","爸爸带我看过一次展览。我喜欢那座小楼，因为窗边的凳子很像以前住的地方。","后来商店里卖的新款更大，但没有那张小凳子。我照着记忆画过好几次，不一定完全一样。","要是找不到旧款，也可以先留着画。我还想问作者，当时为什么把那张凳子放在那里。");
            foreach(var r in ResidentRows)
            {
                Add("resident_"+r[0],r[1],r[2],int.Parse(r[3]),CohortHome(r[4]),r[5],r[6],r[7],r[8],r[9],r[10]);
                var p=people.Last();p.Circle=CircleName(r[4]);p.LeisurePlaces=CohortLeisure(r[4]);
                p.WorkStart=r[0]=="moxin"||r[0]=="maoning"||r[0]=="sujun"?1200:480+(people.Count%4)*30;
                p.WorkEnd=p.WorkStart==1200?360:p.WorkStart+480;
                p.StoryTitle=r[7];p.FirstEncounter="在工作地点初次见面；下班后也可能在附近的公共场所遇见。";
            }
            foreach(var p in people.Where(p=>p.Id!=InvitationTestPersonId))
            {
                p.KeyCharacter=KeyPeople.Contains(p.Id);
                if(string.IsNullOrEmpty(p.Circle))p.Circle=p.WorkId=="life_expo"||p.WorkId=="life_studio"||p.WorkId=="model_workshop"?"创作与合作":"商户与街坊";
                if(string.IsNullOrEmpty(p.StoryTitle))p.StoryTitle=p.Name+"的日常";
                if(string.IsNullOrEmpty(p.FirstEncounter))p.FirstEncounter=p.WorkId==null?"在公共场所相识，或通过已有朋友介绍。":"在工作地点相识，再通过交谈了解彼此。";
                if(p.LeisurePlaces==null)p.LeisurePlaces=CohortLeisure(p.KeyCharacter?"community":"old");
            }
            ConfigureExistingNarrativePeople(people);
            if(people.Count(p=>p.Id!=InvitationTestPersonId)!=NarrativePopulation)throw new InvalidOperationException("Narrative population must contain 120 distinct production identities.");
        }

        private static IEnumerable<CityRelationEdge> Pair(string a,string b,string ab,string ba=null)
        {
            yield return new CityRelationEdge{From=a,To=b,Kind=ab,RevealTopic="friends"};
            yield return new CityRelationEdge{From=b,To=a,Kind=ba??ab,RevealTopic="friends"};
        }
        private static IEnumerable<CityRelationEdge> NarrativeRelations()
        {
            foreach(var r in ResidentRows)foreach(var e in Pair("resident_"+r[0],r[11],r[12]))yield return e;
            // Small neighbourhood clusters. No accidental family or romance links generated from list order.
            foreach(var group in ResidentRows.GroupBy(r=>r[4]))
            {
                var rows=group.ToArray();
                for(int i=0;i<rows.Length;i++)foreach(var e in Pair("resident_"+rows[i][0],"resident_"+rows[(i+1)%rows.Length][0],"在附近休息时认识的街坊"))yield return e;
            }
            foreach(var tie in new[]{
                new[]{"landlady",TangWei,"介绍过社区商户的熟人"},new[]{"keeper_agent",TangWei,"租铺接洽的工作伙伴"},
                new[]{TangWei,"life_tenant","商户事务中认识的伙伴"},new[]{TangWei,"life_curator","共同筹备商户活动"},
                new[]{"life_curator","life_enterprise","商户活动中认识的联系人"},new[]{"life_curator",LiangJing,"曾合作过商业活动"},
                new[]{LiangJing,TangWei,"交流过商业街运营经验"},new[]{"life_model_supplier","life_creator","原创模型供货往来"},
                new[]{"life_model_supplier","life_maker","核对过初版模型制作记录"},new[]{"life_creator","life_maker","共同制作过早期样件"},
                new[]{"life_curator","life_maker","旧展会筹备伙伴"},new[]{"life_tenant","life_maker","委托过旧招牌模型"},
                new[]{"lin","res_coder","通过玩家认识的老朋友"},new[]{"landlady","res_wang","多年街坊"},
                new[]{"keeper_mall","staff_mall","家具城同事"},new[]{"keeper_phone","keeper_phone2","手机店同事"},
                new[]{"staff_cpu","staff_mem","午饭常遇见的市场同事"},new[]{"staff_gpu2","staff_psu","一起帮忙搬过货"},
                new[]{"res_student","res_tea","夜校活动认识的朋友"},new[]{"res_mom","res_auntie","同住一片街区"},
                new[]{"res_pupil","zhang","在校师生"},new[]{"ake","momo","交流过内容制作"},new[]{"qiao","life_enterprise","评测和采购交流"}})
                foreach(var e in Pair(tie[0],tie[1],tie[2]))yield return e;
            foreach(var e in Pair("life_enterprise",ShenXiao,"自己的孩子","父亲"))yield return e;
            foreach(var e in Pair("life_teacher",ShenXiao,"社区手工课的学生","社区手工课老师"))yield return e;
            foreach(var e in Pair("sun","keeper_phone2","认识的维修学徒","市场里认识的长辈"))yield return e;
        }
    }
}
