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

        public PlaceRoster(StaffDef greeter, string bossNpcId = null, int bossGate = 0, StaffDef plainBoss = null)
        {
            Greeter = greeter;
            PlainBoss = plainBoss;
            BossNpcId = bossNpcId;
            BossGate = bossGate;
        }

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
                return clerk == null ? null : new PlaceRoster(clerk, owner.Id, StallBossGate);
            }
            if (p.Kind != PlaceKind.Shop || p.IsPlayerShop) return null;
            if (p.Business == Gifts.TobaccoBusiness) return new PlaceRoster(TobaccoBoss);
            if (p.Business == GameSession.PhoneBusiness) return new PlaceRoster(PhoneClerk, Npcs.Jie, PhoneBossGate);
            if (p.Business == GameSession.MallBusiness) return new PlaceRoster(MallGuide, null, 0, MallManager);
            if (p.Business == GameSession.FarmBusiness) return new PlaceRoster(FarmBoss);
            return null;
        }
    }
}
