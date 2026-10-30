using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public bool HasMcn {get;private set;}
        public string RomanceId {get;private set;}
        public int BusinessStanding => Math.Min(100,(int)(PlayerShop.Reputation*.6 + Math.Log10(Math.Max(1,NetWorth().Yuan/1000.0))*10) + (_owned.Contains(VehicleKind.Car) ? 5 : 0));
        public long McnDailyWages => HasMcn ? 60+30*_creators.Count(x=>x.McnUntilDay>Clock.Today.TotalDays) : 0;
        public bool FoundMcn()
        {
            if(IsGameOver || HasMcn)return false;
            if(!HasShop || PlayerShop.Reputation<25)return Fail("需要主店和知名度 25。");
            if(!Ledger.TryDebit(Clock.Today,"MCN投资",Money.FromYuan(50000),"成立创作工作室"))return Fail("成立工作室需要 ¥50,000。");
            HasMcn=true;Say("创作工作室成立。固定日薪 ¥60，每位签约创作者再加 ¥30。创作者广告收入按 20% 分成，单次合约 90 天。");return true;
        }
        public string McnContractBlocker(CreatorState c)
        {
            if(!HasMcn)return "先成立创作工作室";
            if(c.McnUntilDay>Clock.Today.TotalDays)return "合约尚未到期";
            if(c.Relationship<30)return "商务交情达到 30";
            if(c.Resources<40)return "创作资源达到 40";
            if(BusinessStanding<c.Ambition*.5)return "你的经营地位还不足以匹配对方的抱负";
            if(Ledger.Balance<CreatorFee(c)*5)return "签约费不足（当期推广费的五倍）";
            return null;
        }
        public bool SignMcnCreator(string id)
        {
            var c=Creator(id);if(c==null || IsGameOver)return false;
            string why=McnContractBlocker(c);if(why!=null)return Fail(why);
            Ledger.ForceDebit(Clock.Today,"MCN签约",CreatorFee(c)*5,Creators.Get(id).Name+"90 日合约");
            c.McnUntilDay=Clock.Today.TotalDays+90;c.McnIncomeCents=0;return true;
        }
        public bool EndMcnContract(string id)
        {
            var c=Creator(id);if(c==null || c.McnUntilDay<=Clock.Today.TotalDays || IsGameOver)return false;
            if(!Ledger.TryDebit(Clock.Today,"MCN解约",CreatorFee(c),Creators.Get(id).Name))return Fail("提前解约需补偿一次当期推广费。");
            c.McnUntilDay=Clock.Today.TotalDays;c.Relationship=Math.Max(0,c.Relationship-5);return true;
        }
        public bool CloseMcn()
        {
            if(!HasMcn || IsGameOver)return false;
            if(_creators.Any(x=>x.McnUntilDay>Clock.Today.TotalDays))return Fail("先结束所有创作者合约。");
            HasMcn=false;Say("工作室已关闭，不再支付日常人员费用。设立费用不退。");return true;
        }
        public string CreatorOutingBlocker(CreatorState c)
        {
            if(!HasSmartphone)return "需要智能手机联系";
            if(c.Relationship<20)return "商务交情达到 20 后才愿意私下见面";
            if(c.LastOutingDay==Clock.Today.TotalDays)return "今天已经见过面";
            if(Trip!=null || PendingEvent!=null)return "先结束出行或当前事件";
            if(Clock.MinuteOfDay<9*60 || Clock.MinuteOfDay>=20*60)return "见面时间 9:00–20:00，需留出两小时";
            if(Ledger.Balance<Money.FromYuan(180))return "请客需要 ¥180";
            return null;
        }
        public bool InviteCreatorPrivately(string id)
        {
            var c=Creator(id);if(c==null || IsGameOver)return false;
            string why=CreatorOutingBlocker(c);if(why!=null)return Fail(why);
            Ledger.ForceDebit(Clock.Today,"生活",Money.FromYuan(180),"与"+Creators.Get(id).Name+"见面");
            c.LastOutingDay=Clock.Today.TotalDays;c.Affection=Math.Min(100,c.Affection+8+Math.Min(4,EffectiveCharm/10));
            c.Relationship=Math.Min(100,c.Relationship+2);
            AdvanceClock(120);
            Say(Creators.Get(id).Name+"与你吃饭聊了会儿工作以外的事。私人好感增长；商务关系与私人关系分别记录。");return true;
        }
        public string CreatorStory(CreatorState c)
        {
            if(c.StoryStage>=3 || c.Affection<20+c.StoryStage*25)return null;
            if(c.Id=="momo")
            {
                if(c.StoryStage==0)return "莫莫想把旧电脑改造成给小学生用的学习机。有人建议把孩子们收礼物的反应剪成爆款，但她担心镜头会让一件好事变味。她问你，有没有不靠煽情也能被看见的办法。";
                if(c.StoryStage==1)return "一家公司愿意出钱买断莫莫的教程，条件是以后只推荐他们的配件。她已经算过，这笔钱够换设备，也够给家里寄一笔。可她不想把每一句建议都变成广告。";
                return "莫莫把拍摄日历推给你看，周日第一次是空白的。她想去河边走走，不拍片，也不聊下一次合作。她问，你能不能把那个下午留出来。";
            }
            if(c.Id=="ake")
            {
                if(c.StoryStage==0)return "阿可给你看旧战队的合照。手腕受伤后，他开始做直播，观众总问他什么时候回赛场。新赛事方愿意给曝光，但要求连续直播通宵。他说自己有点想答应。";
                if(c.StoryStage==1)return "阿可收到去外地做电竞内容的邀请，收入不稳定，却能试试一直想做的访谈。他担心离开熟悉的人以后，自己又得从头开始，也担心因为舍不得而错过。";
                return "阿可关掉了直播间的小灯。他说自己在镜头前永远得接得住梗，有时候其实只想安静吃顿饭。你们开始谈，不需要表演的时候该怎样相处。";
            }
            if(c.StoryStage==0)return "乔乔的评测样机出了问题，品牌希望她把负面结果删掉，承诺以后给更多新品。她一向把测试记录整理得很仔细，却第一次不知道该不该按发布键。";
            if(c.StoryStage==1)return "新的团队给乔乔开了更好的条件，但会接管账号、选题和时间。她很想往前走，也不想把刚找回的生活全部交出去。她问你，你希望她怎么选。";
            return "乔乔准备把工作和私人账号分开。她说，喜欢一个人不等于愿意把每次见面都当作内容。你们认真讨论，哪些事可以一起做，哪些边界应该由彼此保留。";
        }
        public bool ResolveCreatorStory(string id,bool respectChoice)
        {
            var c=Creator(id);if(c==null || IsGameOver || CreatorStory(c)==null)return false;
            string memory="creator-story:"+id+":"+c.StoryStage+":"+(respectChoice ? "respect" : "growth");
            if(!Flags.Add(memory))return false;
            c.StoryStage++;
            if(respectChoice) {c.Affection=Math.Min(100,c.Affection+8);c.Relationship=Math.Min(100,c.Relationship+4);}
            else {c.Followers=Math.Min(2000000,(int)(c.Followers*1.05));c.Affection=Math.Max(0,c.Affection-4);c.Resources=Math.Max(0,c.Resources-10);}
            Say(respectChoice ? "你支持对方自己做选择，这次谈话让关系更近了。" : "你把增长机会放在前面。账号有了更多曝光，但对方觉得自己的顾虑没被理解。");return true;
        }
        public string RomanceBlocker(CreatorState c) => RomanceId!=null ? "已有伴侣，先认真处理现有关系" : c.Affection<60 ? "私人好感达到 60" : c.StoryStage<2 ? "先完成两段私人交谈" : BusinessStanding<c.Ambition*.5 ? "你们对未来生活的预期还不一致" : null;
        public bool StartRomance(string id)
        {
            var c=Creator(id);if(c==null || IsGameOver)return false;
            string why=RomanceBlocker(c);if(why!=null)return Fail(why);
            RomanceId=id;Say("你与"+Creators.Get(id).Name+"确认了恋爱关系。保持联系与相处，商业合约仍按原条款独立执行。");return true;
        }
        public bool EndRomance()
        {
            if(RomanceId==null || IsGameOver)return false;
            var c=Creator(RomanceId);c.Affection=Math.Max(0,c.Affection-20);RomanceId=null;
            Say("你们结束了私人关系，已经签署的商务合同继续按约执行。");return true;
        }
        private void SettleMcnAndRelationships(GameDate previous,GameDate today)
        {
            if(HasMcn)
            {
                int active=_creators.Count(x=>x.McnUntilDay>previous.TotalDays);
                Ledger.ForceDebit(today,"MCN人员",Money.FromYuan(60+30*active),"工作室日常人员费用");
                foreach(var c in _creators.Where(x=>x.McnUntilDay>previous.TotalDays))
                {
                    long gross=(long)(c.Followers*4*(.5+c.Resources/100.0)*(.8+c.Relationship/100.0));
                    Money share=Money.FromCents(gross).Scale(.2);
                    if(share.IsPositive) {Ledger.Credit(today,"MCN分成",share,Creators.Get(c.Id).Name+"广告合作分成");c.McnIncomeCents+=share.Cents;}
                    c.Resources=Math.Max(0,c.Resources-1);
                    if(c.McnUntilDay==today.TotalDays)Say(Creators.Get(c.Id).Name+"的 MCN 合约到期，可商谈续约。");
                }
            }
            foreach(var c in _creators)
            {
                if(today.TotalDays-c.LastOutingDay>10)c.Affection=Math.Max(0,c.Affection-1);
                if(RomanceId==c.Id && c.Affection<20)
                {RomanceId=null;Say(Creators.Get(c.Id).Name+"觉得你们很久没有真正相处，提出结束私人关系。商务合约继续履行。");}
            }
        }
    }
}
