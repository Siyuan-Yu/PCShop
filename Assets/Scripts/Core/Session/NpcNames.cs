using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    // Stable IDs are save identities; familiar names are descriptive aliases only.
    public static class NpcNames
    {
        public static readonly IReadOnlyDictionary<string, string> Familiar = new Dictionary<string, string>
        {
            {"zhang", "老张"},
            {"landlady", "李姐"},
            {"zhou", "老周"},
            {"jie", "阿杰"},
            {"li", "老李"},
            {"wang", "阿王"},
            {"qian", "钱姐"},
            {"sun", "老孙"},
            {"lin", "小林"},
            {"staff_cpu", "小慧"},
            {"staff_gpu", "阿强"},
            {"staff_mem", "小静"},
            {"staff_psu", "小峰"},
            {"keeper_phone", "小唐"},
            {"keeper_mall", "小赵"},
            {"keeper_tobacco", "老胡"},
            {"keeper_farm", "王婶"},
            {"keeper_dealer", "老马"},
            {"staff_gpu2", "小凯"},
            {"keeper_phone2", "小乐"},
            {"staff_mall2", "阿旺"},
            {"keeper_agent", "小陈"},
            {"res_wang", "老王"},
            {"res_fang", "阿芳"},
            {"res_tea", "小雨"},
            {"res_baker", "刘师傅"},
            {"res_flower", "晓晴"},
            {"res_pharm", "何药师"},
            {"res_cook", "胖哥"},
            {"res_rider", "小吴"},
            {"res_grandpa", "周大爷"},
            {"res_auntie", "张阿姨"},
            {"res_student", "小宇"},
            {"res_coder", "陈工"},
            {"res_mom", "吴姐"},
            {"res_pupil", "乐乐"},
            {"momo", "莫莫"},
            {"ake", "阿可"},
            {"qiao", "乔乔"},
            {"life_chef", "杜师傅"},
        };
        public static string Alias(string id) => id != null && Familiar.TryGetValue(id, out var name) ? name : null;
    }
}
