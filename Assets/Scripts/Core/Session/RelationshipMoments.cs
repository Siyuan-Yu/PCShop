using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    internal sealed class RelationshipMoment
    {
        internal readonly string Title, Activity, Interest, InterestLine, Scene, DateLine, HandReply;
        internal readonly RelationshipStoryResponse First, Second, DateFirst, DateSecond;

        internal RelationshipMoment(string title, string activity, string interest, string interestLine,
            string scene, string dateLine, string handReply, RelationshipStoryResponse first, RelationshipStoryResponse second,
            RelationshipStoryResponse dateFirst = default, RelationshipStoryResponse dateSecond = default)
        {
            Title = title;
            Activity = activity;
            Interest = interest;
            InterestLine = interestLine;
            Scene = scene;
            DateLine = dateLine;
            HandReply = handReply;
            First = first;
            Second = second;
            DateFirst = dateFirst;
            DateSecond = dateSecond;
        }

        internal RelationshipStoryResponse Response(bool date, int choice) =>
            date ? (choice == 0 ? DateFirst : DateSecond) : (choice == 0 ? First : Second);
    }

    public sealed partial class GameSession
    {
        public const int PersonalMomentMinRelation = 25;
        private const string PersonalMomentPrefix = "personal-moment:";
        private const string IllustratedMomentPrefix = "relationship-cg:";
        private const string PersonalMomentChoice = "personal-moment";

        public static string PersonalInterest(string id) => RelationshipMoments.For(id)?.Interest;
        private static string PersonalInterestLine(string id) => RelationshipMoments.For(id)?.InterestLine;

        private static bool MatchesPersonalActivity(RelationshipMoment moment, string activityId) =>
            activityId != null && (activityId == moment.Activity || activityId.StartsWith(moment.Activity + "_tier", StringComparison.Ordinal));

        public static bool LikesPersonalActivity(string id, string activityId) =>
            RelationshipMoments.For(id) is RelationshipMoment moment && MatchesPersonalActivity(moment, activityId);

        private string MomentPerson(CityActivity activity)
        {
            if (IsGameOver || activity == null || activity != RunningCityActivity || CurrentPlace?.Id != activity.VenueId
                || activity.Participants.Length != 1) return null;
            var participant = activity.Participants[0];
            return participant.Phase == CityParticipantPhase.Participating && CitySamePlace(participant.PersonId)
                ? participant.PersonId : null;
        }

        private static string MomentKey(string id, bool together) => PersonalMomentPrefix + id + (together ? ":date" : ":friend");
        private static string DateReplyKey(string id) => PersonalMomentPrefix + id + ":follow-up";

        private int ReadMomentChoice(string id, bool together)
        {
            string key = MomentKey(id, together);
            if (Flags.Contains(key + ":0")) return 0;
            return Flags.Contains(key + ":1") ? 1 : -1;
        }

        public (string PersonId, string Title, string Line, bool Illustrated) PersonalMoment(CityActivity activity)
        {
            string id = MomentPerson(activity);
            var moment = RelationshipMoments.For(id);
            if (moment == null || !MatchesPersonalActivity(moment, activity.DefinitionId)
                || activity.Choices.Contains(PersonalMomentChoice)) return default;
            bool together = IsPlayerPartner(id);
            if (HasRelationshipStory(id) || (!together && Relation(id) < PersonalMomentMinRelation)
                || ReadMomentChoice(id, together) >= 0) return default;
            int previous = together ? ReadMomentChoice(id, false) : -1;
            string recall = previous == 0 ? moment.First.Recall : previous == 1 ? moment.Second.Recall : null;
            string line = !together ? moment.Scene : (recall ?? moment.Scene) + "\n\n" + moment.DateLine;
            return (id, moment.Title, line, together);
        }

        public string[] PersonalMomentOptions(string id, bool together = false) => RelationshipMoments.For(id) is RelationshipMoment moment
            ? new[] { moment.Response(together, 0).Label, moment.Response(together, 1).Label } : Array.Empty<string>();

        public string ReplyInPersonalMoment(CityActivity activity, string id, int choice)
        {
            if (choice is < 0 or > 1 || PersonalMoment(activity).PersonId != id || id == null) return null;
            var moment = RelationshipMoments.For(id);
            bool together = IsPlayerPartner(id);
            if (!Flags.Add(MomentKey(id, together) + ":" + choice)) return null;
            activity.Choices = activity.Choices.Append(PersonalMomentChoice).ToArray();
            var response = moment.Response(together, choice);
            if (together)
            {
                Flags.Add(DateReplyKey(id));
                RememberRelationshipIllustration(id);
            }
            RememberSocial(id, response.Recall);
            return response.Reply;
        }

        private void RememberRelationshipIllustration(string id) => Flags.Add(IllustratedMomentPrefix + id);

        public string RelationshipIllustrationTitle(string id)
        {
            if (id == null) return null;
            if (Flags.Contains(IllustratedMomentPrefix + id)
                || _relationshipStories.TryGetValue(id, out var story) && (story.OutingAnswer > 0 || story.OutingCompleted))
                return RelationshipStoryTitle(id) ?? RelationshipMoments.For(id)?.Title;
            return null;
        }

        private string PartnerMomentLine(string id, string activityId)
        {
            var moment = RelationshipMoments.For(id);
            return moment != null && MatchesPersonalActivity(moment, activityId) ? moment.DateLine : null;
        }

        private string PartnerMomentReply(string id, bool holdHands)
        {
            var moment = RelationshipMoments.For(id);
            if (moment == null) return null;
            if (holdHands) return moment.HandReply;
            if (HasRelationshipStory(id) && _relationshipStories.TryGetValue(id, out var progress) && progress.OutingAnswer > 0)
            {
                var responses = RelationshipStoryCatalog.ResponsesFor(id);
                if (progress.OutingAnswer <= responses.Length && Flags.Add("relationship-recalled:" + id))
                    return responses[progress.OutingAnswer - 1].Recall + "\n\n" + PartnerConversation(id);
            }
            int choice = ReadMomentChoice(id, true);
            if (choice >= 0 && Flags.Add("relationship-recalled:" + id))
                return moment.Response(Flags.Contains(DateReplyKey(id)), choice).Recall + "\n\n" + PartnerConversation(id);
            return PartnerConversation(id);
        }

    }

    internal static class RelationshipMoments
    {
        internal static RelationshipMoment For(string id) => id != null && Moments.TryGetValue(id, out var moment) ? moment : null;

        private static readonly Dictionary<string, RelationshipMoment> Moments = new(StringComparer.Ordinal)
        {
            ["life_ming"] = new("夜间电台", "oldmarket_visit", "修旧收音机、听夜间电台",
                "夜里调台，常常先听见一阵沙沙声。好歌都是在我准备关机的时候进来的。",
                null, "这家摊主今天换了一个台。等听出来是什么歌，我们再走。", "好。收音机开着吧，旋钮先别碰。",
                default, default),
            ["lin"] = new("桌上两碗饭", "dinner", "打游戏、和发小吃饭、重拍旧合照",
                "小时候那张合照，我今天才发现你踩了我一只鞋。哪天同一个地方再拍一张，这回我先站稳。",
                null, "我把手机反扣了。今天不用猜我想说哪一句。我想你了。", "行，这回不和你比谁先松手。",
                default, default),
            ["life_singer"] = new("先听完再笑", "ktv", "写原创旋律、收集自己没唱好的录音",
                "我连唱劈的一小段都留着。有时候听回去，会发现错的地方比写好的还好听。",
                null, "给你听个还没填词的开头。我先哼一遍，你别急着猜下一句。", "来。麦克风先放一边，这两拍不用数。",
                default, default),
            ["life_ning"] = new("下课后的那一页", "dinner", "搜老恐怖片、夜校记账",
                "看老恐怖片的时候，我会记怪物第几分钟出现。上次猜得太准，结果被卖爆米花的开门声吓到了。",
                null, "明晚我课表上空了一格。我想跟你看部老片，猜错怪物的人负责剥橘子。", "好。我把练习册收起来，今晚这段时间已经留好了。",
                default, default),
            ["momo"] = new("镜头关掉的周日", "park_walk", "羽毛球、观鸟、给旧电脑做教程",
                "我去打球通常不拍。上回打丢一个羽毛球，倒在树根底下看见两只鸟抢东西，蹲到搭档来找我。",
                null, "我放包里了，今天就算特别好笑，也只给我们两个记得。", "可以。另一只手拿饼干，别又被鸟抢走。",
                default, default),
            ["life_curator"] = new("走一条没有图的路", "park_walk", "收旧明信片、散步找照片里的街角",
                "这张明信片上那盏灯还在，下面的理发店倒换了三家。要不要下回走过去认一认？",
                null, "我带了张旧明信片。到那个街口以后，你来决定往哪边走。", "走吧。碰到岔路也不用松手，我今天没画路线。",
                default, default),
            ["life_creator"] = new("本子留在工作台", "fishing", "做原创模型、改旧蕾丝、收发条玩具",
                "我改发带剩的蕾丝刚好够给一只发条兔围脖子。它走两步就偏，还不许我把发条拧紧。",
                null, "我带了两只发条动物。等浮漂安静下来，就在盒盖上比谁走得直。", "手借我一下。发条可以等，今天的风倒很舒服。",
                default, default),
            ["life_designer"] = new("袖口内侧的名字", "dinner", "原创服装、研究旧衣里藏着的针脚",
                "有些旧衣看着很普通，翻到里面，针脚却齐得叫人舍不得拆。上次我看了半天，店家以为我在找破洞。",
                null, "你抬一下手。我想看你平常怎么卷袖口，下回给你多留一点活动的地方。", "好。牵一会儿，再让我看这个袖口够不够松。",
                default, default),
            ["resident_zhaoyu"] = new("雨停后的几秒", "ktv", "录磁带、采集雨声、听地下电子乐",
                "我有一盒雨声，最想留的是中间那一下没关严的车门。每次听到那里，我都想再等几秒。",
                null, "把耳机分你一边。我今天没剪开头，从第一声脚步一起听。", "嗯。别说话，听完这几秒也不用松开。",
                default, default),
            ["resident_dengyue"] = new("留一点没上色的地方", "dinner", "画老店招、刻橡皮章",
                "我会在小票背面试章。你看这块空白，我擦了三次才发现，留着比填满好看。",
                null, "我给你刻了枚小窗户。等吃完，可以盖在你那张小票背面。", "好。等印泥干了我再翻页，现在坐一会儿。",
                default, default),
            ["resident_zengyu"] = new("最后一杯不算试喝", "park_walk", "做无酒精饮品、盲听老唱片",
                "老唱片我挑封面最看不懂的。买回去放第一首，同时调一杯看着也猜不出口味的。",
                null, "这次我出一杯怪饮，你出一首怪歌。先说好，谁皱眉谁先选第二首。", "行，我把杯子换一只手。你要是突然停下听歌，就拉我一下。",
                default, default),
            ["resident_jiyun"] = new("兰花开得慢", "dinner", "养兰花和苔藓、给新花苞拍照",
                "花苞刚冒那天我拍糊了，可那张反而舍不得删。那天我下班进门，站着看了很久。",
                null, "今晚上菜的人早安排好了。我只给那盆兰浇了水，然后就来见你。", "好。我今晚给自己留足了时间，慢慢来。",
                default, default),
            ["keeper_agent"] = new("地图上少了一条路", "park_walk", "收藏旧地图、散步拍门头",
                "我收旧地图，常顺着图找那些已经改名的巷子。最喜欢找到有人还在用的侧门。",
                "我找着了图上的侧门。图把它画成死路，门底下却每天有人扫过。想跟我走近一点看看吗？",
                "我把这张路线留给我们俩。今天没人等着签合同，我们可以走回头路。",
                "好。走到那扇门再告诉你，我第一次找它走错了几回。",
                new("走过去敲敲门", "先看看门上有没有住户的牌，我不想打扰人。", "你在门边等我看清那块老牌子，我记得。"),
                new("把这一段空着", "也好。下回入口从这儿找，咱们不用知道它通到哪。", "地图上那段空着的路，我还是没补线。"),
                new("在图上记下今天", "给我笔。日期我写，记号你画。", "地图上多了你画的那个记号，我一直没擦。"),
                new("以后你带我走", "好。记不住也打给我，我去接你。", "那条小路现在我走熟了，等你来找我。")),
            ["keeper_phone"] = new("旧兔子的补丁", "oldmarket_visit", "收集玩偶、给旧玩偶缝补丁",
                "你看我包上的兔子，耳朵补过两次。上次有小孩说它旧了，我说那是它今天穿得比较随便。",
                "旧兔子又露棉花了。摊上只剩两块刚好够大的布。你觉得给它补个太阳，还是补一小片夜色？",
                "你看它耳朵背面，我把收针藏进旧布里了。下回我想留一点布，咱们做个能挂在一起的小东西。",
                "好啊。先让我把兔子的耳朵塞进包里，省得它又卡在摊架上。",
                new("挑暖黄布", "就补太阳。以后它挂在包外，阴天也能晒着。", "兔耳朵那块暖黄布，是你帮我挑的。"),
                new("挑深蓝布", "那我再缝三颗歪星星。你到时候帮我数，别让我多扎一针。", "你选的蓝布上，我缝了三颗歪星星。"),
                new("教我缝最后一针", "好啊。慢一点，你缝歪了，我也留着。", "兔耳朵背面那一针是你缝的，我没舍得拆。"),
                new("给它做个小伙伴", "好，我找一块软的给它。这样它在包上也有伴了。", "我给兔子的新伙伴留了一小块同色布。")),
            ["life_coach"] = new("一点烤焦的边", "court", "收旧号码布、凌晨烤面包",
                "早上出门前烤了一炉，自己先掰了块焦边。训练服口袋里，还有一张以前跑比赛的号码布。",
                "歇会儿。纸袋里有我早上烤的吐司，软的在中间，脆的在边上。你先挑。",
                "今天不记得分，只记得早上出门前给你留了两种边。你想先吃哪块？",
                "来，先把球放下。我们谁也不赶下一组。",
                new("我吃软的", "我给你撕下来，剩下这块薄边正合我口味。", "我后来换了面粉，里面比上次还软。"),
                new("我吃最脆的", "行。响一点也没关系，场上刚好在拍球。", "我今天烤边的时候，多给你留了半分钟。"),
                new("分一块吧", "行。我从中间掰，这样两边都有焦边。", "我们分着吃那块，我后来又试了好几次才烤成那样。"),
                new("下回换我带早餐", "好。软的脆的都行，记得自己也吃点。", "下回球场换你带早餐，我带保温壶。")),
            ["racer_0"] = new("最慢的一圈", "park_walk", "遥控改装车、翻老赛车游戏",
                "赛车游戏我会把第一关来回开。真下了场，我反倒爱摆瓶盖，比谁能让遥控车最慢绕完。",
                "这两个瓶盖是弯心。敢不敢比一圈最慢的？碰了盖子算输。先拿手柄的人先定路线。",
                "今天我想在你旁边玩。谁先撞了瓶盖，就负责找个地方坐下来，输赢先欠着。",
                "好。手柄先放下，今天允许并排慢一点。",
                new("绕一条大弯", "有点意思，留出空间就不用一慌推满杆。我跟着你的走法试试。", "我后来练了你划的那条大弯，还没碰过瓶盖。"),
                new("在最窄的地方掉头", "你真敢选。那我提前说，别看我动作，自己听轮子蹭地的声音。", "我给你选的窄弯多留了一厘米，回来还你一局。"),
                new("一人开一圈接力", "行。交手柄的时候别急，我等你松手再接。", "我练会了不抢你交过来的手柄。"),
                new("挪出一个并排终点", "好。这样两辆都能停在那儿，谁也不用让道。", "那两个当终点的瓶盖，我放在同一个小袋里了。")),
            ["ake"] = new("二号手柄", "leisure_claw", "下播打双人游戏、攒旧手柄",
                "关播以后我还会打两把。和熟人玩，我连刚才手滑都不用替自己找台阶。",
                "二号手柄我修好了。以前一进双人关我总抢前面，这回你领，我跟着。",
                "今天我把推送都关了。开不开一局你说了算，我想陪的是你。",
                "来。等松手了再按开始，反正没人催我开播。",
                new("你打头，我补位", "也行，有人补位我就敢试一次不熟的走法。", "我没把那回存档删掉，留着下回接着补位。"),
                new("我们从头慢慢打", "好。卡关就停下来吃点东西，存档留着明天。",
                    "那天停在第二关，我自己开过机，没往后打。"),
                new("一起给存档起名", "用个普通的名字吧，只有我俩知道为什么这么叫。", "那格存档的名字，开机看到我还会笑。"),
                new("今天先不开机器", "好。我想听你讲讲上一回过关的时候到底在笑什么。", "那天连电源都没开，可我回去以后还觉得玩得挺开心。")),
            ["qiao"] = new("只走一次的捷径", "dinner", "做数码评测、解只有一条捷径的推理题",
                "我最近卡在一张纸迷宫上。每次我很肯定地划一条线，三步以后就又得擦掉。",
                "借你铅笔。这里我填了三版，都差一个转弯。你想先看我擦掉的，还是自己画一条？",
                "今天我不急着找答案。我想看看你选路的时候，第一眼会看哪儿。",
                "好。铅笔放桌上吧，这一小会儿谁都不用证明什么。",
                new("看看擦掉的那一条", "你看到我漏算的那个门了？真好，下次我也想听你说怎么算的。", "那道擦掉的线我又描了一遍，留着提醒自己听你把话说完。"),
                new("画一条谁都没走过的", "借你橡皮。画错也留一小截，我想知道你刚开始怎么猜。", "你拿铅笔犹豫的那一下，我记得比答案还清楚。"),
                new("把两条路都画上", "好，留下两条。我喜欢这张纸上有你的笔迹。", "那张纸上两条路都还在，我没有挑哪条更好。"),
                new("下次我出题", "行，答不出来我可不许你提前告诉我。", "我还记得你说下次轮到你出题。")),
            ["resident_baiming"] = new("纸楼倒下那一秒", "expo_comic", "拍特摄短片、用废包装搭纸街景",
                "台上展架我量得可准了。回家拍纸楼倒塌，倒故意装歪一块，省得怪兽撞半天撞不下来。",
                "这回纸楼只够倒一次。我想拍它倒下去那秒。你来拍，还是你来喊开拍？",
                "我给模型店招换了个名字，镜头里只有暂停才看得到。等咱们单独看成片，我指给你。",
                "好。刚才倒的那栋是假的，这只手是真的。",
                new("我来拍", "我按住街口那一块。你别跟着楼往下倒，收住镜头就行。", "你把纸楼倒下那一秒拍得比我想的还稳。"),
                new("我来喊开拍", "行，喊得严肃点。我拿废纸盒在后面扮怪兽。", "你上回喊开拍太认真，我在镜头后憋笑。"),
                new("把你的笑也剪进去", "行，片尾放那段。反正幕后也得有人笑场。", "片尾那声笑我留了，下回跟你一起听。"),
                new("把我们录成最后一幕", "可以，不过这次纸楼先别倒，我想留着这个街口。", "最后一幕的街口，我留在桌上没拆。")),
            ["resident_ningxi"] = new("照片留在相机里", "oldmarket_visit", "用相机拍旧物和用旧后的痕迹",
                "我喜欢别人拍商品图会擦掉的东西。柜台边那道划痕，看得出以前有人老把杯子摆在那儿。",
                "这面窗刚好照出你停下来的样子。能拍一张吗？这张只留在相机里，你想删也可以。",
                "今天相机里还有很多空位。你愿意的话，我想留一张你最放松的样子。你说了算。",
                "等一下，我把相机挂好。好了，这段路不用拍。",
                new("可以，留下吧", "那就拍刚才那样。看窗里就好，不用回头找我。", "那张你答应留下的照片，我给你存了原片。"),
                new("今天只看看窗", "行，我把镜头盖扣上。我们一起看，回头还能记得。", "你说只看看窗那晚，我没照片，也记得灯是什么颜色。"),
                new("留一张你的视角", "好。拍哪一刻我来挑，留不留我们一起定。", "我留了那张你最放松的照片，只发给你。"),
                new("今天一起看倒影", "好。我不拿相机，你往这边站一点，有两个人了。", "后来路过那扇窗，我还是会停下来下意识往旁边看。")),
            ["res_coder"] = new("一号位给你", "oldmarket_visit", "修退役掌机、跟朋友同屏联机",
                "旧掌机最让我舍不得的是按键磨掉的那一圈。我的那台一号键已经磨亮了，还能联机。",
                "我把小时候那台掌机的触点擦好了。一号位给你。我看你试按两下，马上就知道手柄还有没有毛病。",
                "机子里还有个空存档。你愿意的话，我想把咱们俩的那一格一直留着。",
                "嗯。游戏先暂停，我想认真待在这儿一会儿。",
                new("试试我们没过的那关", "我给你画个小图，不报答案。要试几回都行。", "卡着的那关，我把图留在机套里了。"),
                new("换到二号位并排打", "行。其实我更会配合，刚才只是在你面前逞强。", "我把二号位手柄也擦干净了，等你来插线。"),
                new("留给我们从头打", "行。我想从最简单那关开始，这次哪一关都不跳。", "我给我们留了一个双人存档。"),
                new("把旧进度接着打", "好。那次没过的地方，我特意没自己练过去。", "那个旧进度现在还是停在我们上次分开的地方。")),
            ["life_golfer"] = new("先听那一声", "park_walk", "高尔夫、散步辨鸟鸣",
                "那边从芦苇里出来的声音我跟了好几天。你别急着走，等第二声一般就能听清方向。",
                "先停一会儿。刚才一短一长，就在芦苇上头。想听它再叫一次，还是绕到能看见的位置？",
                "我其实认错过这种鸟。今天我想告诉你我猜的，然后陪你在那儿一起猜错。",
                "好啊。咱们轻轻走，别吵到它。",
                new("站这儿等第二声", "行。上一次我等太急，反倒没听见第二声结尾。", "你陪我等到的那一声，我也记在小本上了。"),
                new("走到能看见的地方", "往这边，咱俩走慢点。别踩那丛干芦苇。", "我又走过那丛芦苇。特地踩你上次选的那块石头。"),
                new("把猜的写在同一页", "好。猜对了也别改，让我看看我们一开始各觉得是什么。", "记鸟的那页，我给你的猜法留了一行。"),
                new("先不猜了，再听会儿", "好。反正我来这儿最想做的事已经做到了。", "我们最后没猜那只鸟。我下回还是想和你去听。")),
            ["racer_3"] = new("给旧车留个名字", "oldmarket_visit", "收藏老车刊、给车起外号",
                "车况得写清楚，外号倒可以随便起。那台老车钥匙丢过三次，我就管它叫回头客。",
                "刊里这辆车，我比照片里的车主还熟。有人出价买，维修单都看完了，我突然想知道，你舍不舍得卖？",
                "我把原登记名字留着，在钥匙旁边留了一格。你想给我们的那段路起个外号吗？",
                "好。今天只带钥匙，估价单留在柜里了。",
                new("再留它开一阵", "我也舍不得现在卖。以后打算出手，也该先认真跑完想跑的路。", "我后来用它跑了一趟，一路没想报价。"),
                new("让下个人接着开", "那就把维修册交给真正愿意开的人。我留一张旧照片就够了。", "我把照片留下了，钥匙交得很干脆。"),
                new("叫它慢一点", "好。以后看见这个名字，我会记得也可以慢慢走。", "钥匙旁边那张纸，我写的是慢一点。"),
                new("等下次开完再取名", "听你的。我最近想去一趟河边，就挑个你也有空的傍晚。", "我把那条河边的路留着，跑完一起给它起名。")),
            ["resident_luoya"] = new("这一句换你接", "ktv", "唱合唱、织花、收集各地方言绕口令",
                "我边织边背方言歌，背错一句就多织一针。肩上这朵看起来花瓣多，其实是我唱错好几回。",
                "这句是我老家小孩边走边唱的。唱慢了反而容易绕进去。你想先听我唱，还是下一句接在我的后面？",
                "这首我在合唱团一直没唱过。今天我想唱给你听，要是唱错了，你可以笑，就别急着提醒我。",
                "好。我把话筒放回去。最后这句哼给你一个人听。",
                new("你唱，我仔细听", "好，那我不会的地方就混个短音。回家问清了，再给你补上。", "我问到了最后那句。还是得照旧家的念法唱。"),
                new("下一句我来接", "听我换气再进来。接歪就一起接歪，我的小花花瓣还能再添两片。", "上次你接上来的时候，我已经准备笑了，差点忘了唱。"),
                new("留个尾音给我", "好。我唱得慢一点，你听见我换气再进来。", "那首歌最后留给你的尾音，我一个人唱的时候也空着。"),
                new("我想听你唱完", "那你靠近一点。这句不用唱到话筒里。", "我后来一唱到那句，就想起你坐得很近。")),
        };
    }
}
