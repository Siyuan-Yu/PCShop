using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    internal enum FigureAppearance { Npc, Player, PhoneClerk, Classmate, Florist, Coach }

    /// <summary>Stable combinations of shared meshes, matched to the named NPC portraits.</summary>
    internal readonly struct NpcLook
    {
        private static readonly Dictionary<string, string> PortraitHeads = BuildPortraitHeads();
        private static readonly string[] CrowdHeads =
            { "crop", "swept", "tousled", "spectacles", "bob", "fringe", "waves", "tied", "senior" };
        private static readonly string[] AdultHeads = { "crop", "swept", "tousled" };
        private static readonly string[] HairColors = { "3D302D", "54382F", "2E272D", "603F36", "47323A", "322F35" };
        private static readonly string[] SkinColors = { "EBAD85", "EBA77E", "D79673", "F0BA97", "CD8D6A" };

        internal readonly string Head;
        internal readonly Color Skin, Hair;
        internal readonly float Height, Width;
        internal readonly bool PrefersSkirt;

        private NpcLook(string head, Color skin, Color hair, float height, float width, bool prefersSkirt)
        {
            Head = head;
            Skin = skin;
            Hair = hair;
            Height = height;
            Width = width;
            PrefersSkirt = prefersSkirt;
        }

        internal static FigureAppearance ForPerson(string id) => id switch
        {
            "keeper_phone" => FigureAppearance.PhoneClerk,
            "res_coder" => FigureAppearance.Classmate,
            "res_flower" => FigureAppearance.Florist,
            "life_coach" => FigureAppearance.Coach,
            _ => FigureAppearance.Npc
        };

        internal static NpcLook Select(string id, string slot, Color baseSkin, Color clothing)
        {
            uint hash = StableHash(id ?? slot, clothing, id == null);
            string head;
            if (id != null && PortraitHeads.TryGetValue(id, out string portraitHead))
                head = portraitHead;
            else
            {
                var heads = id == null ? CrowdHeads : AdultHeads;
                head = heads[hash % (uint)heads.Length];
            }
            bool slim = head is "bob" or "fringe" or "waves" or "tied";
            bool young = head == "young";
            float height = young ? .79f : slim ? .92f : .98f;
            height += ((hash >> 12) % 7) * .01f;
            float width = slim ? .91f : head is "crop" or "senior" ? 1.05f : .99f;
            if (id == "landlady") width = 1.06f;

            string hair = head switch
            {
                "senior" => "8F8580",
                "officer" => "E3E7E6",
                "worker" => "D5AB3E",
                "young" => "343033",
                _ => HairColors[(hash >> 4) % (uint)HairColors.Length]
            };
            Color skin = ArtDirection.Hex(SkinColors[(hash >> 8) % (uint)SkinColors.Length]);
            if (id == null) skin = Color.Lerp(baseSkin, skin, .78f);
            bool skirt = slim && (id is "life_ning" or "life_singer" or "life_teacher" or "resident_yujia" or "resident_zengyu"
                || id == null && (hash >> 16) % 3 == 0);
            return new NpcLook(head, skin, ArtDirection.Hex(hair), height, width, skirt);
        }

        private static uint StableHash(string key, Color clothing, bool includeColor)
        {
            uint hash = 2166136261;
            unchecked
            {
                foreach (char c in key ?? "") hash = (hash ^ c) * 16777619;
                if (includeColor)
                {
                    Color32 rgb = clothing;
                    hash = (hash ^ rgb.r) * 16777619;
                    hash = (hash ^ rgb.g) * 16777619;
                    hash = (hash ^ rgb.b) * 16777619;
                }
            }
            return hash;
        }

        private static Dictionary<string, string> BuildPortraitHeads()
        {
            var heads = new Dictionary<string, string>();
            void Add(string head, params string[] ids)
            {
                foreach (string id in ids) heads.Add(id, head);
            }

            Add("crop", "keeper_mall", "keeper_phone2", "life_bartender", "life_chef", "life_gardener", "life_maker",
                "life_model_supplier", "life_qiao", "res_baker", "res_cook", "res_wang", "resident_dongyu",
                "resident_gaobo", "resident_hanzheng", "resident_jiyun", "resident_peiran", "resident_qiufeng",
                "resident_quyuan", "resident_shaoan", "resident_songan", "resident_wenbo", "resident_yucheng",
                "staff_gpu", "staff_gpu2", "staff_psu", "sun", "zhou");
            Add("swept", "keeper_dealer", "life_enterprise", "life_golfer", "res_pharm", "resident_baiming",
                "resident_guwen", "resident_jiangning", "resident_leiming", "resident_sujun");
            Add("tousled", "ake", "jie", "keeper_tobacco", "life_clothsupplier", "life_ming", "lin", "racer_2",
                "res_rider", "resident_fuzhi", "resident_lishan", "resident_luchen", "resident_luhai",
                "resident_maoning", "resident_pancheng", "resident_shiyang", "resident_xuweilin", "resident_zhaoyu",
                "resident_zhengke", "resident_zhouran", "staff_mall2", "wang");
            Add("spectacles", "keeper_agent", "li", "life_curator", "res_student", "story_liangjing");
            Add("senior", "res_grandpa", "resident_guanghe", "resident_qiaosong", "zhang");
            Add("young", "res_pupil", "story_shenxiao", "CommunityLostChild");
            Add("bob", "life_librarian", "life_tenant", "res_auntie", "res_mom", "resident_lanxin", "resident_linhe",
                "resident_luqiao", "resident_mengqiu", "resident_ningxi", "resident_siyu", "resident_wuyou",
                "resident_xiaqing", "resident_yujia", "staff_cpu", "staff_mem");
            Add("fringe", "life_clothier", "life_ning", "resident_caihong", "resident_fanglu", "resident_luwei",
                "resident_wanqing", "resident_yinhe", "resident_yuanan");
            Add("waves", "keeper_farm", "landlady", "life_creator", "life_designer", "life_singer", "life_teacher",
                "qiao", "racer_0", "racer_1", "racer_3", "res_fang", "resident_luoya", "resident_penglan",
                "resident_taoran", "resident_xuwei", "resident_yeli", "resident_zengyu");
            Add("tied", "momo", "qian", "res_tea", "resident_chenxia", "resident_dengyue", "resident_duma",
                "resident_hexin", "resident_moxin", "resident_renxin", "resident_rongke", "staff_mall", "story_tangwei");
            Add("officer", "service_officer");
            Add("worker", "service_repair", "CommunityRoofWorker");
            return heads;
        }
    }
}
