using System;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private void RecordActivityFarewells(CityActivity activity, CityActivityPhase phase)
        {
            if (IsGameOver || !activity.EntryPaid || CurrentPlace?.Id != activity.VenueId
                || phase is not (CityActivityPhase.Completed or CityActivityPhase.Interrupted))
                return;

            var service = CityActivityService(activity);
            foreach (var participant in activity.Participants)
            {
                if (participant.Phase != CityParticipantPhase.Participating || !CitySamePlace(participant.PersonId)) continue;
                participant.FarewellLine = ActivityFarewell(activity, service, participant.PersonId, phase);
            }
        }

        private string ActivityFarewell(CityActivity activity, CityActivityDefinition service, string person, CityActivityPhase phase)
        {
            bool partner = IsPlayerPartner(person);
            int familiarity = Relation(person);
            bool family = person is "res_mom" or "res_grandpa" || person == CityLifeCatalog.CousinId;
            if (phase == CityActivityPhase.Interrupted)
            {
                if (partner) return "我们先回去吧。今天能见到你就很好了，剩下的下次再来。";
                return family || familiarity >= 60
                    ? "那今天先到这儿。回去路上慢点，咱们改天再来。"
                    : "那今天先到这儿吧。之后有空我们再约。";
            }

            if (family)
                return person == "res_mom" ? "跟你这么走一趟我挺高兴的。回去记得吃饭，别又忙忘了。"
                    : "今天玩得挺好。什么时候得空，咱们再出来待一会儿。";

            int tone = partner ? 3 : familiarity >= 65 || person == Npcs.Lin ? 2 : familiarity >= 35 ? 1 : 0;
            if (CityLifeCatalog.LeisureService(activity) == LeisureGame.Cinema)
            {
                string genre = VenueServiceName(service, 0);
                return genre switch
                {
                    "喜剧" => tone == 3 ? "你刚才一笑，我也跟着笑了。下次还坐你旁边。"
                        : "最后那段我没忍住。你是不是也笑了？",
                    "爱情片" => tone == 3 ? "散场了。我们牵手走回去吧。"
                        : "结尾那个电话，我一路都在想他会不会打。",
                    "动作片" => tone >= 2 ? "大楼上那段追车太离谱了，不过我看得挺过瘾。"
                        : "有一段追车我差点忘了吃手里的爆米花。",
                    _ => tone == 3 ? "我刚才差点抓你的手。反正散场了，现在可以抓一下吧？"
                        : "我猜到柜子有问题，居然完全猜错了里面的东西。"
                };
            }
            string activityKind = FarewellActivityKind(activity, service, CurrentPlace);
            bool differentDay = (activity.ActualEnd / 1440 + activity.Seat) % 2 == 1;
            if (activityKind == "boat" && differentDay)
            {
                return tone switch
                {
                    0 => "在水上坐了一会儿，感觉人都轻松些了。谢谢你。",
                    1 => "刚才停桨那会儿真安静。下次挑个傍晚再来？",
                    2 => "这地方挺好。下回谁要是心烦，咱们就来这儿坐一船。",
                    _ => "一会儿从码头慢慢走回去吧。我今天不太想那么快跟你分开。"
                };
            }
            string[] lines = activityKind switch
            {
                "boat" => new[]
                {
                    "船靠稳了。谢谢你，今天划得很舒服。",
                    "刚才离岸那下挺顺的。下次我也想试试掌舵。",
                    "船还上了？走，沿湖再绕一小段，刚才压根没顾上看岸边。",
                    "先别急着下船。我想再跟你坐一小会儿，看看水慢慢静下来。"
                },
                "walk" => new[]
                {
                    "这条路走着挺舒服。谢谢你陪我走这段。",
                    "走这一圈人轻松点了。下回还可以走湖边那条。",
                    "本来想着出来走两步，没想到和你说了一路。下次还来。",
                    "下回还走这条路吧。刚才走到一半我就想牵你的手，还好我们走得够慢。"
                },
                "food" => new[]
                {
                    "今天这顿吃得很舒服。谢谢你一起出来。",
                    "下回还来这儿吧。我刚才看隔壁桌有道菜挺香的。",
                    "吃饱了！下次换我挑地方，给你找一家我舍不得告诉别人的。",
                    "吃完也不用急着走吧？我想和你再坐一会儿，走的时候一起出去。"
                },
                "listening" => new[]
                {
                    "这次听得挺清楚的。谢谢你来陪我试音。",
                    "刚才那段伴奏回去我还想再听一遍。下次一起挑首歌吧。",
                    "下次咱们别光听，挑两首自己喜欢的慢慢唱。",
                    "刚才坐在你旁边听歌还挺舒服的。下次也留一首给我们。"
                },
                "ktv" => new[]
                {
                    "今天包间里的时间过得挺快。谢谢你约我来。",
                    "下次把今天没唱到的那首留给我，我回去先练练。",
                    "刚才谁先唱跑调来着？算了，下一回我们还是一起唱。",
                    "刚才你接上那一句的时候，我差点忘了自己该唱什么。下次还跟你一组。"
                },
                "bar" => new[]
                {
                    "今晚坐得很舒服。谢谢你叫我出来喝一杯。",
                    "下次也坐这边吧。这里说话不用抬着嗓子。",
                    "聊着聊着杯子都空了。下回还是老位置，我先来占座。",
                    "等下陪我走一段吧。我还挺想接着听你刚才说的那件事。"
                },
                "fishing" => new[]
                {
                    "这会儿坐在水边挺舒服的。谢谢你约我来。",
                    "下次换边上那座钓台试试？我刚才一直在看那片水纹。",
                    "收竿吧，再坐下去天都要晚了。下次给我留你旁边的位置。",
                    "今天最喜欢的是等浮漂那一会儿。你坐在边上，我们不用一直找话说。"
                },
                "sport" => new[]
                {
                    "今天练得挺开心的。回去记得歇一歇。",
                    "刚才最后几下手感才回来。改天我们再来一场？",
                    "下次见面再接着练。今天先去找点喝的，我渴了。",
                    "这会儿累了吧？水给你，歇一歇我们再一起走。"
                },
                "expo" => new[]
                {
                    "我看到几个挺有意思的作品。谢谢你和我一起逛。",
                    "刚才路过的那排展品我还挺喜欢的。下回进去再仔细看看。",
                    "出去以后给我看看你今天记下的东西？我想知道你最喜欢哪个。",
                    "逛的时候我一直在想，把今天喜欢的那件作品拍下来，就能一起记得了。"
                },
                "market" => new[]
                {
                    "陪我逛了这么久，辛苦你了。改天见。",
                    "下次还有新到的东西吧？咱们早点来，慢慢翻。",
                    "刚才那家摊我记住了。下次你路过看到新货，给我发条消息。",
                    "下回别急着逛完。我想跟你挑个小东西，带回去就能想起今天。"
                },
                _ => new[]
                {
                    "谢谢你今天抽空一起过来。改天见。",
                    "今天这一趟挺高兴的。下回有空再见。",
                    "跟你出来一趟总觉得时间过得快。下次我约你。",
                    "今天跟你在一起很开心。等下先别分开，陪我再走一会儿吧。"
                }
            };

            // Keep repeated outings from always landing on the same generic goodbye.
            if (activityKind == "other" && !partner && familiarity >= 35)
            {
                if (person == "life_ning") return "下次见面我想听听你后来怎么想的。今天有几句还没聊完。";
                if (person == "life_singer") return "路上慢点。下回有空，我放一段刚写的旋律给你听。";
                if (person == "resident_ningxi") return "今天有些画面挺好看的，回去我会想一想。下次再跟你说。";
            }
            return lines[tone];
        }

        private static string FarewellActivityKind(CityActivity activity, CityActivityDefinition service, Place place)
        {
            string kind = service?.LogicalId ?? service?.Id ?? activity.DefinitionId ?? "";
            if (IsParkRowing(activity)) return "boat";
            if (IsInteractiveFishingActivity(activity)) return "fishing";
            if (kind.StartsWith("hike", StringComparison.Ordinal) || kind == "park_walk") return "walk";
            if (kind == "social_49") return "listening";
            if (IsOnsiteSport(activity)) return "sport";
            return place?.Business switch
            {
                "venue_hike" or "venue_park" => "walk",
                "venue_restaurant" => "food",
                "venue_ktv" => "ktv",
                "venue_bar" => "bar",
                "venue_court" or "venue_golf" or "venue_billiards" => "sport",
                "venue_expo" => "expo",
                "venue_oldmarket" or "农贸" or "电子批发" => "market",
                _ => "other"
            };
        }
    }
}
