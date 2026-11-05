namespace PCShopSell.Core.Session
{
    public sealed class ExpoMoment
    {
        public readonly string Title,Prompt;
        public readonly string[] Options,Feedback;
        public readonly int[] Scores,Heat;
        public ExpoMoment(string title,string prompt,string[] options,int[] scores,int[] heat,string[] feedback)
        {Title=title;Prompt=prompt;Options=options;Scores=scores;Heat=heat;Feedback=feedback;}
    }
    public static class ExpoMoments
    {
        private static readonly string[] Zones={"aisle","booth","reception","booth","booth","reception","stage","aisle","booth","booth","booth","reception"};
        private static readonly string[] Effects={"queue","question","interview","rival","fault","buyer","question","empty","camera","question","stock","buyer"};
        public static string ZoneAt(int day,int decision)=>decision%2==1?"stage":Zones[(day/90*3+decision/6*3+decision%6/2)%12];
        public static string EffectAt(int day,int decision)=>decision%2==1?"performance":Effects[(day/90*3+decision/6*3+decision%6/2)%12];
        private static ExpoMoment M(string title,string prompt,string a,string b,string c,int best,int loud)
        {
            var score=new[]{3,3,3};score[best]=18;score[loud]=-8;
            var heat=new[]{2,2,2};heat[best]=8;heat[loud]=12;
            var feedback=new[]{"场面平稳，但没有充分回应现场需求。","场面平稳，但没有充分回应现场需求。","场面平稳，但没有充分回应现场需求。"};
            feedback[best]="回应了现场需求，观众更愿意停留与购买。";feedback[loud]="短暂吸引围观，但承诺或节奏失当，信任下降。";
            return new ExpoMoment(title,prompt,new[]{a,b,c},score,heat,feedback);
        }
        private static readonly ExpoMoment[] Events={
            M("排队堵住通道","第一波观众涌来，体验区排长队，场馆提醒不能占用公共通道。","让KOL先高声拉人，继续排队","改为分批体验，KOL在队外讲解","停止所有演示，只发传单",1,0),
            M("顾客质疑宣传","顾客指出海报上的一句描述容易被理解为所有场景都适用。","当场说明限制，补充适用场景","坚持宣传完全没问题，压过质疑","先请顾客去别处再谈",0,1),
            M("记者临时采访","记者只给一分钟，希望听到一项可以验证的产品亮点。","把品牌未来目标当作现有能力","报出所有技术术语","演示一项亮点，同时讲清测试条件",2,0),
            M("同行在隔壁降价","观众拿着隔壁的低价单，追问你的价格差别。","比较真实配置和服务，允许按需选择","承诺无条件最低价","暂停介绍，等隔壁活动结束",0,1),
            M("样品突然失灵","舞台旁的体验样品不能正常启动，排队观众已经注意到。","让KOL继续夸效果，当作没发生","切换备用展示并说明故障待检查","直接结束全部体验",1,0),
            M("大客户问供货","采购人员想大批量订货，但问到了门店目前没有把握的交期。","先接下全部数量，交期以后再说","只介绍外观，避开交期","核对现货与产能，明确可交付范围",2,0),
            M("主播口误","KOL把一项规格说错了，弹幕已经有人指出。","请主播及时纠正并做实物核对","让其他KOL刷屏盖掉质疑","暂停互动，不回应",0,1),
            M("人流突然减少","邻近舞台开始表演，你的摊位短时间冷清。","制造没有准备好的抽奖承诺","安排短演示，集中讲一个实际用途","全员等候自然回流",1,0),
            M("拍摄与体验冲突","几位创作者想拍素材，占住了原本留给顾客的体验台。","谁流量大就一直给谁拍","拒绝所有拍摄","划分拍摄时段，保留顾客体验位",2,0),
            M("观众追问溢价","观众认可设计，但反复追问为什么比普通款贵。","拆开说明材料、设计和服务成本","说价格越高就是越好","只回答这是品牌定价",0,1),
            M("库存快卖完","一款展品很受欢迎，现场余量已不足，后面的顾客还在等。","让KOL继续承诺现场都有货","公布剩余量，区分现货与后续订单","把所有商品临时撤下",1,0),
            M("闭展前的选择","闭展前还有最后一批观众，希望尽快决定是否购买。","临时喊出无法兑现的终身免费服务","只留二维码，不再答疑","集中答疑，明确价格与售后边界",2,0)
        };
        private static readonly ExpoMoment[] Stage={
            M("舞台开场 · 找到话题","首日围观者多数刚接触品牌，主持人请你用一句话说明今天能看到什么。","从一个日常问题切入，告诉大家接下来现场验证","一口气背完全部规格","声称所有同类产品都不值得买",0,2),
            M("舞台演示 · 抓住节奏","观众已围过来，但还没看懂关键步骤。下一段只有短短一次演示机会。","加快速度跳过操作步骤","先交代观察点，再完整做一次对比","先宣布效果绝对无敌",1,2),
            M("舞台收尾 · 接住提问","有人开始提问，前排准备购买，后排还在观望。","带头起哄，催所有人立刻下单","不再答疑，只感谢大家","由KOL分工答疑，指向真实体验和购买位置",2,0),
            M("舞台开场 · 老观众回来","第二天有不少人已看过首日介绍，想听到新内容。","重复完整品牌口号","回应昨天最多的问题，再做新演示","直接承诺今天一律半价",1,2),
            M("舞台演示 · 观众来试","主持人提议请观众上手，但体验设备需要先说明操作方法。","先讲安全步骤，再邀请观众试用","直接让观众随便操作制造节目效果","取消体验，继续念参数",0,1),
            M("舞台收尾 · 热度转化","现场反响不错，但工作人员提醒购买区开始排队。","继续堆人，要求所有KOL同时喊单","提前收工不再沟通","缩短收尾，由KOL引导分流与咨询",2,0),
            M("舞台开场 · 专业质疑","第三天来的采购和媒体更多，有人要求实际证据。","声称测试数据是行业秘密","展示可复核的例子，说明测试范围","只讲创立品牌时的心情",1,0),
            M("舞台演示 · 操作失误","你在演示中点错了一步，观众已经发现，主持人正在等你接话。","承认操作失误，重做关键步骤","说这是特意设计的隐藏功能","跳过演示只讲外观",0,1),
            M("舞台收尾 · 合作邀约","一位采购代表希望你当场报出超出现有能力的保证。","为了热度直接保证无限供货","拖到下次再说，不留联系方式","留下明确的跟进范围，不作超能力承诺",2,0),
            M("舞台开场 · 最后一天","第四天既有第一次来的观众，也有回来作决定的顾客。","先讲最值得验证的一点，再留出答疑","只喊最后机会制造恐慌","重复四天的全部内容",0,1),
            M("舞台演示 · 适可而止","观众已经理解亮点，主持人提醒再拖下去会挤占最后的体验时间。","临时加一个没彩排的夸张挑战","按计划结束演示，转入真实体验","停止与观众沟通",1,0),
            M("舞台收尾 · 留下信任","闭展前的最后一分钟，顾客询问展后还能在哪里找到品牌。","保证任何问题都无条件赔十倍","只宣布展会成功，不留信息","说明真实门店、联系渠道和售后范围",2,0)
        };
        public static ExpoMoment At(int day,int decision)
        {
            int daily=decision/6,step=decision%6;
            return step%2==1?Stage[daily*3+step/2]:Events[(day/90*3+daily*3+step/2)%Events.Length];
        }

        public static ExpoMoment BrandIntroduction(string brand, string[] presenters)
        {
            string lead = string.Join("、", presenters);
            return new ExpoMoment("开幕日 · 品牌舞台介绍", lead + "已经在舞台等你。观众聚在" + brand + "的展品旁，接下来怎么开场？",
                new[] { "请代言人介绍品牌", "回答现场观众的问题", "和代言人一起演示展品" },
                new[] { 8, 16, 12 }, new[] { 11, 5, 8 },
                new[] { lead + "从品牌故事讲起，吸引了更多观众。", lead + "逐一回应实际问题，观众更信任品牌。",
                    lead + "用展品展示了一个具体用途，观众留下来继续体验。" });
        }
    }
}
