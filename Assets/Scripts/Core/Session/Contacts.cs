using System.Collections.Generic;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>人脉图上的分组（图上一个扇区）。</summary>
    public enum ContactGroup
    {
        Friends,
        Market,
        Street,
        Neighbors,
    }

    /// <summary>人脉图上的一个人：有交情的人（NpcDef）或店员 / 街坊（StaffDef，按熟络度）。</summary>
    public sealed class ContactInfo
    {
        public string Id;
        public string Name;
        public string Role;

        /// <summary>true = 有交情系统的人（交情值），false = 店员 / 街坊（熟络度）。</summary>
        public bool IsNpc;

        /// <summary>交情或熟络度，0 ~ 100。</summary>
        public int Level;
        public string LevelName;
        public ContactGroup Group;

        /// <summary>在哪能找到他。</summary>
        public string Where;
    }

    /// <summary>人和人之间本来就有的关系（老板和店员、老姐妹……），两个人你都认识时图上连一条线。</summary>
    public static class SocialTies
    {
        public static readonly string[][] All =
        {
            new[] { Npcs.Li, "staff_cpu", "老板和店员" },
            new[] { Npcs.Wang, "staff_gpu", "老板和店员" },
            new[] { Npcs.Wang, "staff_gpu2", "老板和打包员" },
            new[] { "staff_gpu", "staff_gpu2", "师徒" },
            new[] { Npcs.Qian, "staff_mem", "老板和店员" },
            new[] { Npcs.Sun, "staff_psu", "老板和店员" },
            new[] { Npcs.Li, Npcs.Wang, "市场里的老邻居" },
            new[] { Npcs.Zhou, Npcs.Li, "做了二十年生意的老相识" },
            new[] { Npcs.Jie, "keeper_phone", "表亲，一起开店" },
            new[] { Npcs.Jie, "keeper_phone2", "老板和学徒" },
            new[] { "keeper_phone", "keeper_phone2", "同事" },
            new[] { "staff_mall", "keeper_mall", "店长和导购" },
            new[] { "staff_mall", "staff_mall2", "店长和理货员" },
            new[] { "keeper_mall", "staff_mall2", "同事" },
            new[] { Npcs.Landlady, "keeper_agent", "房东和中介" },
            new[] { "keeper_agent", "res_mom", "帮她家找过房子" },
            new[] { "keeper_farm", "res_auntie", "二十年的老姐妹" },
            new[] { "keeper_tobacco", "res_grandpa", "天天来喝茶的老主顾" },
            new[] { "res_grandpa", "res_wang", "几十年的老街坊" },
            new[] { "res_rider", "res_cook", "天天来取餐" },
            new[] { "res_fang", "res_tea", "下班一起逛街的姐妹" },
            new[] { "res_student", "res_coder", "学长和学弟" },
            new[] { "res_pupil", "res_grandpa", "爷爷的老棋友家的孙子" },
            new[] { "res_baker", "res_auntie", "每天早上来买面包" },
            new[] { "res_pharm", "res_grandpa", "按时来拿降压药" },
            new[] { "res_flower", "res_fang", "隔壁铺子" },
            new[] { "keeper_dealer", "res_rider", "电动车在他那修" },
            new[] { Npcs.Zhang, "res_pupil", "班主任和学生" },
            new[] { Npcs.Lin, Npcs.Jie, "一起喝过酒" },
        };

        /// <summary>和这个人有关系的人：(对方 id, 关系)。</summary>
        public static IEnumerable<KeyValuePair<string, string>> Of(string id)
        {
            foreach (string[] t in All)
            {
                if (t[0] == id) yield return new KeyValuePair<string, string>(t[1], t[2]);
                else if (t[1] == id) yield return new KeyValuePair<string, string>(t[0], t[2]);
            }
        }
    }

    public sealed partial class GameSession
    {
        /// <summary>认识的人：有交情的人见过面或有交情；店员和街坊聊过（熟络度大于 0）。</summary>
        public List<ContactInfo> Contacts()
        {
            var list = new List<ContactInfo>();
            var seen = new HashSet<string>();
            foreach (NpcDef n in Npcs.All)
            {
                int rel = Relation(n.Id);
                if (!IsMet(n.Id) && rel <= 0) continue;
                seen.Add(n.Id);
                list.Add(new ContactInfo
                {
                    Id = n.Id,
                    Name = n.Name,
                    Role = n.Role,
                    IsNpc = true,
                    Level = rel,
                    LevelName = Npcs.LevelName(rel) + (IsMet(n.Id) ? "" : "（没留联系方式）"),
                    Group = n.StallId != null || n.Id == Npcs.Zhou ? ContactGroup.Market : n.Id == Npcs.Jie ? ContactGroup.Street : ContactGroup.Friends,
                    Where = NpcWhere(n),
                });
            }
            foreach (StaffDef s in Rosters.All) AddStaffContact(list, seen, s);
            foreach (ResidentDef r in Residents.All) AddStaffContact(list, seen, r.Person);
            return list;
        }

        private void AddStaffContact(List<ContactInfo> list, HashSet<string> seen, StaffDef s)
        {
            int rap = StaffRapport(s.Id);
            if (rap <= 0 || !seen.Add(s.Id)) return;
            ResidentDef r = Residents.Get(s.Id);
            Place work = StaffWorkplace(s.Id);
            bool market = work != null && work.Kind == PlaceKind.Wholesaler;
            list.Add(new ContactInfo
            {
                Id = s.Id,
                Name = s.Name,
                Role = s.Role,
                IsNpc = false,
                Level = rap,
                LevelName = StaffTalk.LevelName(rap),
                Group = market ? ContactGroup.Market : work != null ? ContactGroup.Street : ContactGroup.Neighbors,
                Where = work != null ? $"在「{work.Name}」上班" + (r != null ? "，上下班路过商业街" : "")
                    : "常在商业街上走动",
            });
        }

        /// <summary>这个店员 / 街坊在哪家店上班；没有返回 null。</summary>
        public Place StaffWorkplace(string staffId)
        {
            ResidentDef r = Residents.Get(staffId);
            if (r?.WorkPlaceId != null) return City.HasPlace(r.WorkPlaceId) ? City.GetPlace(r.WorkPlaceId) : null;
            foreach (Place p in City.Places)
            {
                PlaceRoster roster = Rosters.For(p);
                if (roster == null) continue;
                if (roster.Greeter.Id == staffId || roster.PlainBoss?.Id == staffId) return p;
                foreach (StaffDef o in roster.Others) if (o.Id == staffId) return p;
            }
            return null;
        }

        private string NpcWhere(NpcDef n)
        {
            if (n.StallId != null && City.HasPlace(n.StallId)) return $"在电子市场「{City.GetPlace(n.StallId).Name}」";
            switch (n.Id)
            {
                case Npcs.Jie: return "隔壁手机店，平时在里间修手机";
                case Npcs.Zhang: return "常来你店里";
                case Npcs.Landlady: return "房东，住楼上";
                case Npcs.Zhou: return "在 1788 上开店";
                default: return "打电话、发消息联系";
            }
        }
    }
}
