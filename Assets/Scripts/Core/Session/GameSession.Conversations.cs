using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    public sealed class PersonCircleLink
    {
        public string PersonId, Name, Relationship;
    }

    public sealed class ConversationTrait
    {
        public string Name;
        public int Unlock;
        public bool Known;
    }

    public sealed partial class GameSession
    {
        public const int AskAboutOthersMinRelation = 30;
        public IEnumerable<PersonCircleLink> PersonCircle(string speakerId)
        {
            var links = CityLifeCatalog.Relations.Where(r => r.From == speakerId)
                .Select(r => new PersonCircleLink { PersonId = r.To, Relationship = r.Kind }).ToList();
            foreach (var activity in _cityActivities.Where(a => a.State == CityActivityPhase.Completed
                && a.Participants.Any(p => p.PersonId == speakerId)))
                foreach (var participant in activity.Participants.Where(p => p.PersonId != speakerId))
                    links.Add(new PersonCircleLink { PersonId = participant.PersonId, Relationship = "一起参加过" + (CityActivityService(activity)?.Name ?? "活动") });
            var worker = Employees.FirstOrDefault(e => e.Person.PersonId == speakerId);
            if (worker != null)
                foreach (var colleague in Employees.Where(e => e.Shop == worker.Shop && e.Person.PersonId != speakerId))
                    links.Add(new PersonCircleLink { PersonId = colleague.Person.PersonId, Relationship = "同一家店工作的同事" });
            return links.Where(link => !string.IsNullOrEmpty(link.PersonId) && link.PersonId != speakerId && CityPerson(link.PersonId) != null
                && (link.PersonId!=CityLifeCatalog.InvitationTestPersonId || DebugInvitationPersonEnabled))
                .GroupBy(link => link.PersonId).Select(group => new PersonCircleLink {
                    PersonId = group.Key, Name = CityPerson(group.Key).Name,
                    Relationship = string.Join("；", group.Select(link => link.Relationship).Distinct())
                }).OrderBy(link => link.Name).ToArray();
        }
        public string RemoteConversationBlocker(string id, bool phoneCall = false)
        {
            if(IsGameOver)return "现在不能交谈";
            if(!phoneCall && !HasChatApp)return "需要安装聊天软件";
            if(!IsMet(id))return "尚未交换联系方式";
            if(CityPerson(id)==null&&Npcs.Get(id)==null&&Rosters.Get(id)==null&&Residents.Get(id)==null)return "找不到这个联系人";
            return null;
        }
        public string AskAboutOthersBlocker(string speakerId, string targetId = null, bool remote = false)
        {
            if (IsGameOver) return "现在不能交谈";
            if(remote){var why=RemoteConversationBlocker(speakerId);if(why!=null)return why;}
            else if (!CitySamePlace(speakerId)) return "需要当面聊天";
            if (Relation(speakerId) < AskAboutOthersMinRelation) return "熟悉度 " + AskAboutOthersMinRelation + " 解锁";
            var circle = PersonCircle(speakerId).ToArray();
            if (targetId == null) return circle.Length == 0 ? "目前没有可以打听的熟人" : null;
            return circle.Any(link => link.PersonId == targetId) ? null : "对方不认识这个人，不能随便打听";
        }
        public string AskAboutPerson(string speakerId, string targetId, bool remote = false)
        {
            string why = AskAboutOthersBlocker(speakerId, targetId, remote);
            if (why != null) return why;
            var link = PersonCircle(speakerId).First(person => person.PersonId == targetId);
            var speaker = CityPersonState(speakerId); var target = CityPersonState(targetId);
            string name = CityPerson(speakerId)?.Name ?? "对方";
            string text = link.Name + "啊，我认识。我们是" + link.Relationship + "。\n";
            string work = target?.WorkPlaceId;
            text += work != null && City.HasPlace(work) ? "对方在「" + City.GetPlace(work).Name + "」工作，做的是" + CityPersonRole(targetId) + "。" : "对方平时是" + CityPersonRole(targetId) + "。";
            if (speaker != null) speaker.KnownRelations = (speaker.KnownRelations ?? Array.Empty<string>()).Union(new[] { targetId }).ToArray();
            Flags.Add("heard:" + targetId);
            if (target != null) target.KnownTopics = (target.KnownTopics ?? Array.Empty<string>()).Union(new[] { "work" }).ToArray();
            if (Relation(speakerId) >= 50)
            {
                text += "\n对方平时喜欢" + ConversationTraits(targetId)[0].Name + "，有机会你们可以自己聊聊。";
                if (target != null) target.KnownTopics = target.KnownTopics.Union(new[] { "trait:0" }).ToArray();
            }
            string key = speakerId + ":about:" + targetId;
            bool fresh = !_cityTopicDay.TryGetValue(key, out int day) || day != Clock.Today.TotalDays;
            if (fresh)
            {
                _cityTopicDay[key] = Clock.Today.TotalDays;
                RememberSocial(speakerId, "向" + name + "打听了" + link.Name + "。\n" + text);
            }
            OnCityStoryTalk(speakerId, "friends");
            return text;
        }

        public IEnumerable<ContactInfo> SavedConversationContacts() => Contacts().Concat(_cityPeople.Values
            .Select(p => CityPerson(p.PersonId)).Where(p => p != null)
            .Select(p => new ContactInfo { Id = p.Id, Name = p.Name, Role = CityPersonRole(p.Id), Level = Relation(p.Id) }))
            .Where(p => IsMet(p.Id)).GroupBy(p => p.Id).Select(p => p.First());

        // Reading a profile never unlocks chapters or advances a relationship.
        public string[] LearnedPersonStories(string id)
        {
            var stories = new List<string>();
            stories.AddRange(_brandStories.Where(s=>s.Stage==3 && s.PersonId==id && !string.IsNullOrWhiteSpace(s.Conclusion)).Select(s=>s.Conclusion));
            int heard = CityPersonState(id)?.StoryChapter ?? 0;
            var definition = CityPerson(id);
            if (definition?.Stories != null) stories.AddRange(definition.Stories.Take(Math.Max(0, heard)));
            var staffStories = Rosters.StoriesOf(id);
            if (staffStories != null) stories.AddRange(staffStories.Take(StaffStoriesHeard(id)));
            if (Npcs.Get(id) != null)
                for (int i = 0; i < Math.Min(NpcTalk.LifeChapterTotal, LifeChaptersHeard(id)); i++)
                    stories.Add(NpcTalk.LifeStory(id, i));
            return stories.Where(story => !string.IsNullOrWhiteSpace(story)).Distinct().ToArray();
        }

        public bool HasGreeted(string id) => IsMet(id) || Flags.Contains("greeted:" + id);
        public bool GreetPerson(string id, bool remote = false)
        {
            if (CityPerson(id) == null && Npcs.Get(id) == null && Rosters.Get(id) == null && Residents.Get(id) == null) return false;
            if(remote){if(RemoteConversationBlocker(id)!=null)return false;}
            else if (CityPersonState(id) != null && !CitySamePlace(id)) return false;
            if (!HasGreeted(id))
            {
                Flags.Add("greeted:" + id);
                if (Relation(id) < 3) AddRelation(id, 3 - Relation(id));
                RememberSocial(id, "第一次打招呼，互相认识了；尚未交换联系方式。");
            }
            return true;
        }

        private static int ConversationSeed(string id)
        {
            int hash = 0;
            foreach (char c in id ?? "") hash = (hash * 31 + c) & 0x7fffffff;
            return hash;
        }
        public ConversationTrait[] ConversationTraits(string id)
        {
            var def = CityPerson(id);
            string interest = def?.Interest ?? Npcs.Get(id)?.Likes ?? "";
            switch (interest)
            {
                case "dinner": interest = "美食"; break;
                case "court": interest = "篮球"; break;
                case "ktv": interest = "唱歌"; break;
                case "fishing": interest = "钓鱼"; break;
                case "market": interest = "逛市场"; break;
                case "hike": interest = "徒步"; break;
            }
            string[] names = id == "res_cook" ? new[] { "抽烟", "骑车" }
                : new[] { string.IsNullOrEmpty(interest) ? "日常爱好" : interest, "喜欢的小礼物" };
            int first = id == "res_cook" || ConversationSeed(id) % 2 == 0 ? 0 : 15;
            var state = CityPersonState(id);
            return names.Select((name, i) => new ConversationTrait {
                Name = name, Unlock = i == 0 ? first : 30,
                Known = Relation(id) >= (i == 0 ? first : 30) || (state?.KnownTopics ?? Array.Empty<string>()).Contains("trait:" + i)
            }).ToArray();
        }
        public string ConversationTraitText(string id, int index)
        {
            var traits = ConversationTraits(id);
            if (index < 0 || index >= traits.Length) return "";
            if (!traits[index].Known) return "还不知道这一点。熟悉度达到 " + traits[index].Unlock + " 后逐渐了解。";
            if (id == "res_cook") return index == 0 ? "罗志勇闲下来时会抽烟。" : "罗志勇喜欢骑车，闲聊时常会提到路上的见闻。";
            if (index == 0) return (CityPerson(id)?.Name ?? Npcs.Get(id)?.Name ?? "对方") + "平时喜欢" + traits[index].Name + "。";
            return Npcs.Get(id) is NpcDef npc ? "喜欢收到：" + npc.GiftName + "。也可以从背包挑其他小礼物。" : "饮料、水果、点心、鲜花或一份做好的饭，都可以从背包里挑选。";
        }
        public string CasualConversation(string id, bool remote = false)
        {
            if(remote){var why=RemoteConversationBlocker(id);if(why!=null)return why;}
            else if (CityPersonState(id) != null && !CitySamePlace(id)) return "对方已经离开了，等下次见面再聊吧。";
            bool firstConversation = !HasGreeted(id);
            if (!GreetPerson(id,remote)) return "现在无法和对方交谈。";
            var def = CityPerson(id);
            if (!remote && id == LuZhi) BeginLuPersonalStory();
            if (firstConversation)
            {
                var npc = Npcs.All.FirstOrDefault(person => person.Id == id);
                var staff = Rosters.Get(id) ?? Residents.Get(id)?.Person;
                string name = def?.Name ?? npc?.Name ?? staff?.Name;
                return npc?.FirstMeet ?? $"你好，我是{name}。以后见面打声招呼。";
            }
            if (def == null)
            {
                var staff = Rosters.Get(id) ?? Residents.Get(id)?.Person;
                return staff != null ? StaffChat(staff) : SmallTalk(id);
            }
            int pick = (ConversationSeed(id) % 4 + Clock.Today.TotalDays % 4) % 4;
            string topic = new[] { "work", "interest", "life", "friends" }[pick];
            if (topic == "interest" && !ConversationTraits(id)[0].Known || topic == "friends" && Relation(id) < AskAboutOthersMinRelation) topic = "work";
            string text = CityTalk(id, topic, remote);
            if (Relation(id) >= 15 && Clock.Today.TotalDays % 5 == ConversationSeed(id) % 5)
            {
                if (Npcs.All.Any(n=>n.Id==id))
                {
                    if (LifeBlocker(id) == null) text += "\n" + LifeTalk(id);
                }
                else if ((Rosters.Get(id) ?? Residents.Get(id)?.Person) is StaffDef staff)
                {
                    if (StaffStoryBlocker(id) == null) text += "\n" + StaffStory(staff);
                }
                else
                {
                    var state = CityPersonState(id); int previous = state.StoryChapter;
                    string story = CityHearStory(id, remote);
                    if (state.StoryChapter > previous) text += "\n" + story;
                }
            }
            var person = CityPersonState(id);
            if (person != null)
                person.KnownTopics = person.KnownTopics.Union(ConversationTraits(id).Select((t, i) => t.Known ? "trait:" + i : null).Where(t => t != null)).ToArray();
            return text;
        }

        private IEnumerable<FoodBatch> GiftableFoodBatches(bool meal) =>
            (meal ? Backpack.Food.Packed : Backpack.Food.Ingredient)
                .Where(b => b.Count > 0 && (!meal || b.ExpireDay >= Clock.Today.TotalDays));

        public string PersonGiftBlocker(string id)
        {
            if (IsGameOver) return "当前不能送礼";
            if (CityPersonState(id) != null && !CitySamePlace(id)) return "需要当面送礼";
            if (!HasGreeted(id)) return "先聊聊认识一下";
            var days = Npcs.Get(id) != null ? _giftDays : _staffGiftDay;
            if (days.TryGetValue(id, out int day) && Clock.Today.TotalDays - day < 3) return (3 - Clock.Today.TotalDays + day) + " 天后才能再送";
            bool food = GiftableFoodBatches(true).Any() || GiftableFoodBatches(false).Any();
            if (!Backpack.GiftLines.Any() && !food && Backpack.Food.RiceKg < .5) return "背包里没有可以送的东西";
            return null;
        }
        public string PersonGiftItemBlocker(string id, string key)
        {
            string why = PersonGiftBlocker(id);
            if (why != null) return why;
            if (string.IsNullOrEmpty(key)) return "请先点选背包中的礼物";
            if (key.StartsWith("gift:")) return Gifts.Get(key.Substring(5)) != null && Backpack.GiftCount(key.Substring(5)) > 0 ? null : "这个物品已不在背包里";
            if (key == "rice") return Backpack.Food.RiceKg >= .5 ? null : "至少需要 0.5 千克大米";
            if (key.StartsWith("lunch:") || key.StartsWith("ing:"))
            {
                bool meal = key.StartsWith("lunch:");
                string item = key.Substring(key.IndexOf(':') + 1);
                return GiftableFoodBatches(meal).Any(b => b.Id == item) ? null : meal ? "食物已用完或过期" : "食材已用完";
            }
            return "这类物品不能作为小礼物";
        }
        public bool GivePersonGift(string id, string key)
        {
            string why = PersonGiftItemBlocker(id, key);
            if (why != null) return Fail(why);
            string name; double value;
            var npc = Npcs.Get(id);
            bool preferred = false;
            if (key.StartsWith("gift:"))
            {
                var gift = Gifts.Get(key.Substring(5));
                if (!Backpack.TakeGift(gift.Id)) return Fail("礼物已经不在背包里。");
                name = gift.Name; value = gift.Price.Yuan; preferred = npc?.GiftItemId == gift.Id || id == "res_cook" && (gift.Id == Gifts.Cig || gift.Id == Gifts.CigHi);
            }
            else if (key == "rice") { Backpack.Food.RiceKg -= .5; name = "0.5 千克大米"; value = 3; }
            else
            {
                bool meal = key.StartsWith("lunch:");
                var list = meal ? Backpack.Food.Packed : Backpack.Food.Ingredient;
                string item = key.Substring(key.IndexOf(':') + 1);
                var batch = GiftableFoodBatches(meal).Where(b => b.Id == item).OrderBy(b => b.ExpireDay).First();
                name = batch.Name;
                value = meal ? FastFoodCatalog.Packed(item)?.Price.Yuan ?? 12 : Ingredients.Get(item)?.Price.Yuan ?? 3;
                if (--batch.Count == 0) list.Remove(batch);
            }
            (npc != null ? _giftDays : _staffGiftDay)[id] = Clock.Today.TotalDays;
            double gain = Math.Min(10, Math.Max(1, value / 15) * (preferred ? 1.5 : 1));
            AddRelation(id, gain);
            RememberSocial(id, "当面送给对方「" + name + "」。");
            ApplyCargoLimit();
            Say((CityPerson(id)?.Name ?? npc?.Name ?? "对方") + "收下了「" + name + "」。");
            return true;
        }
    }
}
