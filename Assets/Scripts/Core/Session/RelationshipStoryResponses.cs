using System;
using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    internal readonly struct RelationshipStoryResponse
    {
        internal readonly string Label, Reply, Recall;

        internal RelationshipStoryResponse(string label, string reply, string recall)
        {
            Label = label;
            Reply = reply;
            Recall = recall;
        }
    }

    internal static partial class RelationshipStoryCatalog
    {
        private static readonly Dictionary<string, RelationshipStoryResponse[]> OutingResponses = new(StringComparer.Ordinal)
        {
            ["life_ming"] = new RelationshipStoryResponse[]
            {
                new("我想多听一会儿", "行。手先别碰旋钮，等这首放完。", "你说再听一会儿，我后来也没舍得关。"),
                new("收完工具一起吃饭", "那你等我把螺丝放回盒里。这次我先去占座。", "上回你肯等我收完工具，我记得。"),
            },
            ["lin"] = new RelationshipStoryResponse[]
            {
                new("你现在还想说什么？", "想说我最近也挺想你的。说完了，轮到你夹菜。", "那次你问我还有什么话想说，我回去想了好久。"),
                new("要不要再添一碗饭？", "行，手机还没翻过来呢。我俩都不用罚。", "上次第二碗饭吃得特别慢。好久没那么慢过了。"),
            },
            ["life_singer"] = new RelationshipStoryResponse[]
            {
                new("结尾留一拍空白吧", "听上去像给别人留一句。回头我在谱上标一下。", "结尾给你留的那一拍，我没有补进去。"),
                new("下一遍我跟你接唱", "行，你听我吸气。接歪了也别停。", "我记得你接进来之前吸了好大一口气。"),
            },
            ["life_ning"] = new RelationshipStoryResponse[]
            {
                new("下回提前留个晚上", "你说一天。我现在就把那天的补班划掉。", "你上回说提前留一晚，我真的去看了下周的课表。"),
                new("先把今晚过完", "好。那就慢一点，我把练习册收起来。", "上次你叫我先把那晚过完，我回去才想起来看表。"),
            },
            ["momo"] = new RelationshipStoryResponse[]
            {
                new("我想看看它吃完没有", "蹲低点，别让它看见我们。等等，另一只也过来了。", "那两只偷饼干的鸟，是我那周最好笑的事。"),
                new("再走一段？", "走吧。下一段也把手机揣兜里。", "你提议再走一段的时候，我刚好也不想回去。"),
            },
            ["life_curator"] = new RelationshipStoryResponse[]
            {
                new("就在这儿等街口亮灯", "好。今天可以等，不用催谁收场。", "那天第一盏灯亮起来，你比我先看见。"),
                new("去看看街口那家店", "走，等走近再看招牌写了什么。", "上次那家店的招牌，我路过还会往那儿看一眼。"),
            },
            ["life_creator"] = new RelationshipStoryResponse[]
            {
                new("浮漂没动，先坐着吧", "好，我刚才差点又想站起来找事做。", "你说先坐着吧，我才发现我真想再坐会儿。"),
                new("起风了，把盒盖扣上", "我来。下次记得带件挡风的外套，我也是。", "我回去把挡风的外套找出来了，下次我带着。"),
            },
            ["life_designer"] = new RelationshipStoryResponse[]
            {
                new("这一针可以一直留着", "我也这么想。回去给下一版也缝一针。", "你摸里的那一针，我在新样衣里也留着。"),
                new("我想听听你准备怎么谈", "想先问清他们怕什么。然后告诉他们，我的名字还是要留下。", "后来我照饭桌上理过的顺序，和他们谈了一遍。"),
            },
            ["resident_zhaoyu"] = new RelationshipStoryResponse[]
            {
                new("再放最后那几秒", "好。把椅子往这边挪一点，后面那个脚步就听清了。", "你让重放的那几秒，我完整留在小样里了。"),
                new("下次一起去买面包", "行。到时候早点去，省得耳机里那个人白替我们着急。", "我记着呢，下次要一起去那家买面包。"),
            },
            ["resident_dengyue"] = new RelationshipStoryResponse[]
            {
                new("留住店里那盏灯", "好。我会把灯落在桌边的那一小块也画出来。", "你说留住店里的灯。昨天我从街对面看过去，那盏灯还开着。"),
                new("画我们这桌的倒影", "好啊。我本来以为你没发现玻璃里的那两个人。", "我把我们这桌画进倒影了。要站近点才能看见。"),
            },
            ["resident_zengyu"] = new RelationshipStoryResponse[]
            {
                new("我也试试你那杯", "给你。先抿一点，我私下做的这杯真的挺酸。", "你那天第一口就皱了眉。我以为你会把杯子递回来，结果你又喝了一口。"),
                new("我想兑淡一点", "水在我这儿。你自己来，兑成你愿意喝完的一杯。", "我记得你兑得淡一点。后来我也那么兑了一杯，走久了喝正好。"),
            },
            ["resident_jiyun"] = new RelationshipStoryResponse[]
            {
                new("把这张糊的也留着", "我以为你会让我重拍。这张还真是我最高兴时拍的。", "那张糊照片还在相册第一格，我没换。"),
                new("下次能看看开花的样子吗？", "等它再开一朵，我叫你。花期该由它说了算。", "我每天看一眼新花苞，等开了就告诉你。"),
            },
        };

        internal static RelationshipStoryResponse[] ResponsesFor(string personId) =>
            personId != null && OutingResponses.TryGetValue(personId, out var responses)
                ? responses : Array.Empty<RelationshipStoryResponse>();
    }
}
