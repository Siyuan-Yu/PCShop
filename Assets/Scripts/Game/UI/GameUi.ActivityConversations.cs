using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static (string Line, string Reply, string Answer, string Topic) ActivityTopic(string label) => label switch
        {
            "聊近况" or "聊今天" or "聊日常" or "聊生活" or "聊最近的生活" =>
                (null, "最近有哪件事让你开心？", "能像现在这样有空坐一会儿，我就挺开心的。平时总想着还有事没做完。", "life"),
            "聊彼此的工作" => ("你店里最近忙吗？说来我也有阵子没跟你聊工作了。", "你忙起来怎么安排？",
                "我先记下今天非做完不可的几件事。剩下的留到明天，省得饭吃到一半还惦记。", "work"),
            "聊共同朋友" => ("说到朋友，我倒想起一个人，好久没找对方坐坐了。", null, null, "friends"),
            "认识同桌" => ("你好，刚才光顾着看菜单了。今天能坐一桌，很高兴认识你。", "你经常来这儿吗？",
                "偶尔来。忙完了想喝点东西，我就挑个能听清别人说话的位置坐。", "greet"),
            "聊最近去过的地方" => ("前几天我绕到湖对岸走了一圈。以前总是路过，没想过那边有条贴水的小路。",
                "哪天我们也走一趟？", "挑个天还亮的时候吧。走到桥那边还能看见对岸店里的灯一盏盏亮起来。", null),
            "分享经历" or "聊创作经历" =>
                (null, "后来呢？", "过了这么久，记得最清楚的反而是当时身边的人。自己做过的那些事，好些都得别人提醒才想起来。", "story"),
            "听旧设备故事" => ("以前邻居家有台旧收音机，找准一个台得把旋钮拨得很慢。好几家人都在院里听，谁先听见声音，谁就招呼大家过来。",
                "现在还能收到吗？", "得看机子有没有修过，也得在窗边慢慢找。有时先听见沙沙声，一转过去，歌就出来了。", null),
            "听家常往事" => ("以前家里有人加班，我们就把汤温在锅里。回来的时间不一样，谁进门都能吃到一口热的。",
                "后来也一直这样吗？", "后来各自都有了家，可谁要回来吃饭，还是会先打个电话，我就多煮一碗。", null),
            "聊喜欢的声音" => ("我喜欢能听出换气的歌。知道那个人是怎么唱到下一句的，就觉得离他近一点。",
                "刚才那首呢？", "副歌前面那口气我听见了。下一遍我们也试试在那儿停一下。", null),
            "反馈伴奏效果" => ("我刚才听着，鼓进来的时候盖住了你第一句。你唱的时候也这么觉得吗？",
                "把那一段再听一遍？", "好，我记一下时间。从进副歌前两拍听，比较容易听出差别。", null),
            "比较人声" or "比较低频" or "检查杂音" => ("你指出的地方我记下了。试听的时候换一小段同样的音源，前后听起来更容易比较。", null, null, null),
            "休息交流" => ("刚才一直顾着看拍子，我到第二段才听清旁边的声部，合起来比一个人练舒服多了。",
                "下一遍还在这儿接？", "行。我先吸口气，你听我进来再跟，就不容易抢拍了。", null),
            "交流体验" => ("听别人讲的时候，我只记住了几个细节。今天自己来了一趟，才知道哪些地方值得慢慢看。",
                "下次还来吗？", "来啊。下回别赶时间，喜欢的地方我们多留一会儿。", null),
            "聊生意" or "聊经营" => ("进货和周转我会分开记。知道一批货多久能卖掉，比光看进价有用。", "你会怎么试新品？",
                "先带一小批，看客人什么时候会主动问。卖得动再补，货架摆满也要占着钱。", null),
            "聊行业消息" => ("最近有几家都在调供货量。碰到报价低的，我先问交期和这批能拿多少。", null, null, null),
            "聊配件沿革" => ("旧机子里有些接口，现在看着陌生，当年大家装机时天天碰。遇到还带原线的，我会多看一眼。", null, null, null),
            "聊老物件" => ("你看这道磨痕，位置正好是手最常碰的地方。这种痕迹我反而愿意留下来。", "那怎么判断值不值得买？",
                "看要拿回去做什么。真要天天用，我会先试功能，能不能正常用比成色要紧。", null),
            "聊比赛亮点" => ("今天最后那一段我最紧张。前面追了那么久，过了线才敢松口气。", "你觉得哪一圈最关键？",
                "倒数第二圈。那圈没抢进弯的位置，出了弯才有空间把速度带起来。", null),
            "约下次练习" => ("下次有空可以先练一个弯。多跑几次，把刹车点记住了再换下一段。", null, null, null),
            "讲解赛道" => ("先看出弯。要是出弯的时候还在修方向，直道上就已经慢半拍了。", null, null, null),
            "聊夜校生活" => ("夜校散得晚，我出来的时候街边都静了。那会儿走一段再回家，白天的事也就放下了。", null, null, "work"),
            "询问炖菜的故事" => ("这锅原先是做给后厨收工吃的。后来有个熟客闻见香味，坐着不肯走，我就给他盛了一碗。",
                "你后来改过做法吗？", "改了火候。以前赶着吃，现在就多炖一会儿，等肉和配菜的味道都进汤里。", null),
            "聊熟客偏好" => ("有几位下班晚的熟客爱坐靠门那桌。他们一来我就知道，饭要热，菜别太咸。", null, null, null),
            "聊值班经历" => ("有次快交班了，临时来了好几桌。我刚脱下围裙，又系回去帮他们把菜上齐。", null, null, "work"),
            "聊餐厅经营" => ("要让我选，我会先把常点的几道做稳。客人吃过一次，知道下次来还能吃到，才会再带人过来。", null, null, null),
            "聊农贸市场" => ("想挑新鲜菜，早点到比较好。我通常先逛一圈再拿东西，免得买了重的还得拎着走。", null, null, null),
            "请教选饵" => ("浅滩先试面饵，芦苇边用蚯蚓。想往深水边找鲈鱼，就换小鱼饵，把竿甩远些。", null, null, null),
            "交流提竿技巧" => ("漂刚动时先别急。等它压下去再提，鱼往外冲的时候先放一点线，等它缓下来再收。", null, null, null),
            "请教园艺" => ("浇水前先摸摸土。表面看起来干，下面有时还是湿的，我会等一等再浇。", null, null, null),
            "听往事" => ("刚退休那阵我每天还是起得很早。后来才习惯不赶着出门，在窗台上摆了几盆花，一盆一盆地养。", null, null, null),
            "听河边往事" => ("小时候河边没有现在这些栏杆。我们蹲在岸边看漂，太阳一落，家里人就隔着堤喊吃饭。", null, null, null),
            "了解供货条件" => ("单价问清了，再问起订量、每天能拿多少和什么时候能到。写在一起，你回店再算也看得懂。", null, null, null),
            "尝试谈价" => ("咱们按同一款、同样的数量来谈。你拿到新报价以后，记得把运费一起算进这笔账。", null, null, null),
            "谈账期" => ("先把首款、尾款日期和交货时间说清楚。应得上你的回款再签，省得到时候两头赶。", null, null, null),
            "询问流转记录" => ("先问上一个人用了多久、哪儿修过。有旧票就对一对时间，记不清的地方先留着，别替卖家猜。", null, null, null),
            "观察机芯" => ("先看齿轮边缘有没有磨损。转起来再听听，有一处不顺，声音常常会先变。", null, null, null),
            "核对走时" => ("把这只钟和准点的钟对齐，隔一阵再看差了多少。只看指针在动，还看不出走得准不准。", null, null, null),
            "学习电路检测" => ("先断电，再看板上有没有发黑的地方。量之前先确认你两根表笔搭在哪儿。", null, null, null),
            "学习线材检测" => ("先沿着外皮看有没有折伤，再量两头。弯一弯线的时候数值变了，就值得再查一次。", null, null, null),
            "学习接头检测" => ("看针脚有没有歪，再插进去轻轻晃一下。松动的地方有时一动就会断讯号。", null, null, null),
            "交流供货条件" or "交流试单条件" or "商谈采购与企业需求" =>
                ("首批先说清规格、数量和到货时间。之后真要下单，留一份双方都确认过的清单。", null, null, null),
            "行业联系人" or "兴趣社交" => ("有想细问的展品就趁现在问作者或供货人。想以后接着聊，我可以帮你们介绍。", null, null, null),
            "讨论推广合作" => ("先让我看看你希望观众看到什么。样品、展示方式和上线时间说好了，后面沟通就省力。", null, null, null),
            "听布展安排" => ("先把通道让出来，再排供电和走线。展台通电试过，最后才摆样品，返工的时候就不用整桌搬。", null, null, null),
            "反馈设计细节" => ("你说的接缝我也想再看看。换个角度装一次，就能知道是卡扣太紧，还是手指没地方用力。", null, null, null),
            "听他讲小凳子的细节" => ("我记得那张小凳子总在窗边。一坐下就能看见外面的路，所以我也把模型里的凳子摆在那里。", null, null, null),
            _ => default
        };

        private static bool IsActivityTopic(CityActivityDefinition definition, int choice)
        {
            if (definition?.Choices == null || choice < definition.ServiceCount || choice >= definition.Choices.Length) return false;
            var topic = ActivityTopic(definition.Choices[choice]);
            return topic.Line != null || topic.Topic != null;
        }

        private int[] ActivityTopicIndices(CityActivity activity, CityActivityDefinition definition) => definition?.Choices == null
            ? Array.Empty<int>() : Enumerable.Range(definition.ServiceCount, definition.Choices.Length - definition.ServiceCount)
                .Where(choice => IsActivityTopic(definition, choice) && ActivitySpeakers(activity, definition.Choices[choice]).Length > 0).ToArray();

        private CityPersonDefinition[] ActivitySpeakers(CityActivity activity, string label)
        {
            var speakers = activity.Participants.Where(participant => participant.Phase == CityParticipantPhase.Participating
                    && S.CitySamePlace(participant.PersonId))
                .Select(participant => S.CityPerson(participant.PersonId)).Where(person => person != null)
                .OrderByDescending(person => person.Id == activity.HostId).ToArray();
            if (label == "听他讲小凳子的细节")
                return speakers.Where(person => person.Id == "story_shenxiao").ToArray();
            if (speakers.Length == 0 && (activity.DefinitionId is "expo_electronics" or "expo_comic"))
            {
                string contact = activity.DefinitionId == "expo_comic" ? "life_creator" : "life_enterprise";
                string speaker = new[] { "life_curator", contact }.FirstOrDefault(S.CitySamePlace);
                if (speaker != null && S.CityPerson(speaker) is CityPersonDefinition exhibitor) return new[] { exhibitor };
            }
            return speakers;
        }

        private bool SelectCityActivityOption(CityActivity activity, CityActivityDefinition definition, int choice,
            string returnLabel = "继续活动", Action onDialogueClosed = null, Action beforeOtherChoice = null)
        {
            var session = S;
            if (!IsActivityTopic(definition, choice))
            {
                beforeOtherChoice?.Invoke();
                return session.ChooseCityActivity(activity.Id, choice);
            }

            string label = definition.Choices[choice];
            var speakers = ActivitySpeakers(activity, label);
            if (speakers.Length == 0) { Toast("等同行者到场后再聊。"); return false; }
            if (activity.Choices.Contains(choice.ToString()) && !session.OutdoorRepeatChoice(activity, choice)
                || !session.ChooseCityActivity(activity.Id, choice, out string detail)) return false;

            var topic = ActivityTopic(label);
            var first = speakers[0];
            OpenDialogue(first.Id, first.Name, session.CityPersonRole(first.Id), "", null, onClosed: onDialogueClosed,
                start: ctx => ShowTopic(ctx, 0));
            return true;

            void ShowTopic(TalkCtx ctx, int index)
            {
                var person = speakers[index];
                ctx.SpeakerId = person.Id;
                var replies = new List<TalkOpt>();
                var nextSpeaker = index + 1 < speakers.Length && topic.Topic != null
                    ? new TalkOpt { Label = $"听{speakers[index + 1].Name}说说", Act = () => ShowTopic(ctx, index + 1) }
                    : null;
                if (topic.Reply != null)
                    replies.Add(new TalkOpt { Label = topic.Reply, Act = () =>
                    {
                        if (nextSpeaker != null) ctx.Show(topic.Answer, nextSpeaker);
                        else ctx.Finish(topic.Answer, returnLabel);
                    } });
                if (nextSpeaker != null) replies.Add(nextSpeaker);

                if (session.RelationshipStoryOutingDialogue(activity, person.Id) is string authored)
                {
                    ShowRelationshipOutingDialogue(ctx, activity, person.Id, authored, returnLabel);
                    return;
                }

                string personal = topic.Topic switch
                {
                    null => null,
                    "greet" => Greet(),
                    "story" => session.SharePersonalExperience(person.Id),
                    _ => session.CityTalk(person.Id, topic.Topic)
                };
                string line = string.Join("\n\n", new[] { topic.Line, personal }.Where(part => !string.IsNullOrWhiteSpace(part)));
                if (line.Length == 0) line = "难得有空在一起待会儿。你最近还好吗？";
                if (index == 0 && !string.IsNullOrWhiteSpace(detail))
                    line += "\n\n<size=16><color=#655E52>" + detail + "</color></size>";
                if (replies.Count > 0) ctx.Show(line, replies.ToArray());
                else ctx.Finish(line, returnLabel);

                string Greet() { session.GreetPerson(person.Id); return null; }
            }
        }

        private void OpenActivityReply(string personId, string line, string returnLabel = "继续活动", Action onClosed = null)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            var person = S.CityPerson(personId);
            OpenDialogue(personId ?? "", person?.Name ?? "同行者", person == null ? "" : S.CityPersonRole(person.Id), "", null,
                onClosed: onClosed, start: ctx => ctx.Finish(line, returnLabel));
        }
    }
}
