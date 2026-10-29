using PCShopSell.Core.Home;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>店里的一个没有交情系统的人（店员，或小店老板）。立绘 id 即 Resources/Portraits/{Id}。</summary>
    public sealed class StaffDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Role { get; }
        public string[] Lines { get; }

        /// <summary>true = 他就是老板（小店只有一两个人，老板直接接待）。</summary>
        public bool IsBoss { get; }

        public StaffDef(string id, string name, string role, string[] lines, bool isBoss = false)
        {
            Id = id;
            Name = name;
            Role = role;
            Lines = lines;
            IsBoss = isBoss;
        }

        public string LineFor(int key) => Lines == null || Lines.Length == 0 ? "……" : Lines[((key % Lines.Length) + Lines.Length) % Lines.Length];
    }

    /// <summary>
    /// 一家店里能找到的人：一般是店员来接待；老板要交情够了才见得到。
    /// 小店（老板直接站柜台的）没有店员，老板直接接待。
    /// </summary>
    public sealed class PlaceRoster
    {
        /// <summary>接待的人（店员 / 直接接待的小店老板）；一定有。</summary>
        public StaffDef Greeter { get; }

        /// <summary>有交情系统的老板（档口老板、阿杰）。没有则 null。</summary>
        public string BossNpcId { get; }

        /// <summary>老板肯见你需要的交情。</summary>
        public int BossGate { get; }

        public PlaceRoster(StaffDef greeter, string bossNpcId = null, int bossGate = 0, StaffDef plainBoss = null, StaffDef[] others = null)
        {
            Greeter = greeter;
            Others = others ?? new StaffDef[0];
            PlainBoss = plainBoss;
            BossNpcId = bossNpcId;
            BossGate = bossGate;
        }

        /// <summary>和接待的人同一级的其他店员（组织架构图上同一排）。</summary>
        public StaffDef[] Others { get; }

        /// <summary>没有交情系统、但比接待的人高一级的上司（比如家具城店长）。没有则 null。</summary>
        public StaffDef PlainBoss { get; }

        public NpcDef Boss => BossNpcId == null ? null : Npcs.Get(BossNpcId);
    }

    public static class Rosters
    {
        /// <summary>档口老板藏在里间，交情到这个数才肯出来见你。</summary>
        public const int StallBossGate = 15;

        /// <summary>阿杰是隔壁手机店老板，平时是小唐在柜台。</summary>
        public const int PhoneBossGate = 20;

        public static readonly StaffDef CpuClerk = new StaffDef("staff_cpu", "小慧", "CPU 档口店员", new[]
        {
            "要什么 CPU？这边都是盒装正品。",
            "老板在里间对账，熟了才好找他。",
            "这两天出货挺快的，要的话趁早。",
        });

        public static readonly StaffDef GpuClerk = new StaffDef("staff_gpu", "阿强", "显卡王档口店员", new[]
        {
            "显卡随便看，别用手摸风扇！",
            "我们老板嗓门大，在里面打电话呢，熟人才能见。",
            "矿潮那会儿，一天能走几十块。",
        });

        public static readonly StaffDef MemClerk = new StaffDef("staff_mem", "小静", "存储专营店员", new[]
        {
            "内存和硬盘都在这边，要多少说一声。",
            "钱姐在后面算账，没熟之前不接待新客人。",
            "这批货刚到，价格我帮你看着。",
        });

        public static readonly StaffDef PsuClerk = new StaffDef("staff_psu", "小峰", "机箱电源城店员", new[]
        {
            "机箱电源都是实价，不用问能不能便宜。",
            "孙老板在仓库点货，熟人他才出来。",
            "电源别选太小的，装上显卡带不动。",
        });

        public static readonly StaffDef PhoneClerk = new StaffDef("keeper_phone", "小唐", "手机店店员", new[]
        {
            "智能手机了解一下？能装好多 app！",
            "按键机也不错，就是只能打电话、发短信。",
            "买了手机去应用商店下载，别忘了哦。",
        });

        public static readonly StaffDef MallGuide = new StaffDef("keeper_mall", "小赵", "家具城导购", new[]
        {
            "轻的您自己拎，太大件我们叫货车送。",
            "线上同款便宜点，不过要等两天。",
            "房子收拾收拾，人心情都不一样。",
            "店里摆设得歇业装修一天才能摆。",
        });

        public static readonly StaffDef MallManager = new StaffDef("staff_mall", "周敏", "家具城店长", new[]
        {
            "欢迎光临。有什么想要的，让小赵带你看。",
            "家具城的规矩：大件先量好尺寸再下单。",
            "房子是自己的脸面，收拾好了，生意也顺。",
        }, true);

        public static readonly StaffDef TobaccoBoss = new StaffDef("keeper_tobacco", "老胡", "烟酒茶行老板", new[]
        {
            "来来来，坐，喝口茶再挑。",
            "送人的话，还是好烟好酒体面。",
            "烟酒不愁卖，就是搬着沉，你背包扛得住不？",
            "老主顾我给你留着好货。",
        }, true);

        public static readonly StaffDef FarmBoss = new StaffDef("keeper_farm", "王婶", "农贸市场摊主", new[]
        {
            "今天的菜刚摘的，新鲜！",
            "米要几斤？称给你。",
            "家里没冰箱别买多了，会放坏的。",
        }, true);

        public static readonly StaffDef DealerBoss = new StaffDef("keeper_dealer", "老马", "车行老板", new[]
        {
            "电驴好骑，三轮拉货，就是别让交警逮着。",
            "天天走路多累，来看看车。",
            "有了车，去市场进货能省一半时间。",
        }, true);

        public static readonly StaffDef GpuAssistant = new StaffDef("staff_gpu2", "小凯", "显卡档口打包员", new[]
        {
            "显卡我都亲手擦过、封好了，放心拿。",
            "强哥脾气急，其实人不坏。",
            "新卡到货那天最累，要拆一整天箱。",
        });

        public static readonly StaffDef PhoneApprentice = new StaffDef("keeper_phone2", "小乐", "手机店学徒", new[]
        {
            "我还在学，贴膜是我的拿手活！",
            "杰哥说我再练练就能自己接待了。",
            "要不要看看新到的手机壳？",
        });

        public static readonly StaffDef MallPorter = new StaffDef("staff_mall2", "阿旺", "家具城理货员", new[]
        {
            "大件我来搬，您只管指方向。",
            "仓库里的货我都认得，哪件放哪排我闭着眼都能找。",
            "货车到了我喊您。",
        });

        /// <summary>全部店员，按 id 查。</summary>
        public static readonly StaffDef[] All =
        {
            CpuClerk, GpuClerk, MemClerk, PsuClerk, PhoneClerk, MallGuide, MallManager,
            TobaccoBoss, FarmBoss, DealerBoss, GpuAssistant, PhoneApprentice, MallPorter,
        };

        public static StaffDef Get(string id)
        {
            foreach (StaffDef s in All) if (s.Id == id) return s;
            return null;
        }

        /// <summary>他愿意讲的三段自己的事（熟络度 15 / 35 / 60 解锁）。</summary>
        public static string[] StoriesOf(string id)
        {
            switch (id)
            {
                case "staff_cpu": return new[] { "小慧说她刚来的时候连 CPU 和显卡都分不清，老李每天考她十个型号，答错了就得多擦一排柜台。", "“其实我是为了攒钱给弟弟交学费才来的。”她笑笑，“现在弟弟考上大学了，我反而舍不得走。”", "她偷偷告诉你：老李嘴硬心软，每次发工资都会多塞一个红包，还叮嘱别说出去。" };
                case "staff_gpu": return new[] { "阿强以前在工地搬砖，是矿潮那阵子被老板挖来搬显卡的：“一箱能顶我一个月工资，手都不敢抖。”", "他说矿潮最疯的时候，凌晨三点都有人排队，老板让他睡在仓库里守货。", "“后来矿潮一退，货砸手里，老板愣是没裁一个人。”阿强低声说，“所以他嗓门再大，我也认他。”" };
                case "staff_mem": return new[] { "小静以前学会计的，钱姐看她算账快才收了她：“她说我手比计算器还稳。”", "她喜欢在价签旁边画小笑脸，说这样客人看到价格不那么紧张。", "她攒了三年钱想开一间自己的小店：“不卖多贵的，就卖内存和硬盘，像钱姐这样靠谱就行。”" };
                case "staff_psu": return new[] { "小峰高中毕业就在电源城搬货，现在已经能一眼看出电源功率虚不虚标。", "他讲过一次：有个客人用杂牌电源带显卡，半夜烧了主板，哭着来退货。“从那以后我只卖实标的。”", "他梦想考个电工证，将来做上门装机和布线：“你开店要是缺人，记得叫我。”" };
                case "keeper_phone": return new[] { "小唐是阿杰的表弟，初中没读完就来帮忙，现在背得出市面上所有手机型号。", "她说阿杰其实不太会和客人打交道，所以前台一直让她站：“我替他笑，他在后面修手机。”", "她偷偷攒了一台旧电脑，想学做视频：“以后给店做宣传，让更多人知道我们。”" };
                case "keeper_mall": return new[] { "小赵做导购前是装修队的，看见沙发就能估出多少钱的料。", "他说最怕客人买了大件才发现门进不去，所以每次都让客人先量尺寸。", "“周敏姐教我的：卖家具是在卖一个家的样子。”他说着，目光忽然柔和了。" };
                case "staff_mall": return new[] { "周敏说她最早是在家具城门口发传单的，店长看她站了一整个夏天才收她进店。", "她曾经因为一批货发错，自己赔了三个月工资，从此每张单子都要核三遍。", "“房子是自己的脸面”那句话，是她第一个客人说的：那是个刚搬进新家的小夫妻。" };
                case "keeper_tobacco": return new[] { "老胡年轻时在码头扛包，后来盘下这间小铺子：“一壶茶泡了三十年，人情都在茶里。”", "他有个本子，记着每个老主顾爱抽什么、爱喝什么，从不会记错。", "“生意好做，人难做。”老胡说，“你记住：别欠人情，还得起才叫朋友。”" };
                case "keeper_farm": return new[] { "王婶天不亮就去菜地摘菜，说隔夜的菜她自己都不吃。", "她儿子在外地读书，每次寄来的信她都念给隔壁摊主听。", "她说其实最想开一间小饭馆：“把我的菜亲手做给人吃，比称斤卖有意思。”" };
                case "keeper_dealer": return new[] { "老马年轻时是长途货车司机，跑过十几个省，后来腰坏了才开车行。", "他最爱的是一辆旧三轮，说那是他第一笔生意买的，死活不卖。", "“有了车，人才有路。”老马拍了拍车座，“你要是想干大的，我给你留一辆好的。”" };
                case "staff_gpu2": return new[] { "小凯是强哥徒弟，刚来时拆箱撕掉了包装上的防伪标，被骂了三天。", "他把每块卡的序列号都记在手机里：“万一出问题，我一眼就能查。”", "他想学维修，强哥答应以后教他：“强哥嘴上骂，其实都在偷偷带我。”" };
                case "keeper_phone2": return new[] { "小乐是附近技校的学生，周末来手机店实习，一边学一边攒生活费。", "他第一次独自贴膜的时候紧张得手抖，贴了三次才成功，小唐在旁边偷偷给他鼓劲。", "他说毕业后想去大厂做售后：“可这里才是我学会笑着跟人说话的地方。”" };
                case "staff_mall2": return new[] { "阿旺有一身蛮力，一个人能扛起一台冰箱，老板娘说他是活货梯。", "他有个习惯：搬完货会把地扫干净，说客人踩脏的地方也是门面。", "他攒钱想给家里的老房子换个新沙发：“让我妈坐着看电视。”" };
                default: return null;
            }
        }
        /// <summary>这个地点能找到的人；没有（街边、家里、自己的店）返回 null。</summary>
        public static PlaceRoster For(Place p)
        {
            if (p == null) return null;
            if (p.Kind == PlaceKind.Dealer) return new PlaceRoster(DealerBoss);
            if (p.Kind == PlaceKind.Wholesaler)
            {
                NpcDef owner = Npcs.OwnerOf(p.Id);
                if (owner == null) return null;
                StaffDef clerk = null;
                switch (p.Id)
                {
                    case Stalls.Cpu: clerk = CpuClerk; break;
                    case Stalls.Gpu: clerk = GpuClerk; break;
                    case Stalls.Memory: clerk = MemClerk; break;
                    case Stalls.PowerCase: clerk = PsuClerk; break;
                }
                return clerk == null ? null : new PlaceRoster(clerk, owner.Id, StallBossGate, null, p.Id == Stalls.Gpu ? new[] { GpuAssistant } : null);
            }
            if (p.Kind != PlaceKind.Shop || p.IsPlayerShop) return null;
            if (p.Business == Gifts.TobaccoBusiness) return new PlaceRoster(TobaccoBoss);
            if (p.Business == GameSession.PhoneBusiness) return new PlaceRoster(PhoneClerk, Npcs.Jie, PhoneBossGate, null, new[] { PhoneApprentice });
            if (p.Business == GameSession.MallBusiness) return new PlaceRoster(MallGuide, null, 0, MallManager, new[] { MallPorter });
            if (p.Business == GameSession.FarmBusiness) return new PlaceRoster(FarmBoss);
            return null;
        }
    }
}
