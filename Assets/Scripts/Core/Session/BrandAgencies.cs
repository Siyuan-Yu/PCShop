using System;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public sealed class BrandAgency
    {
        public string Id, Name, Specialty, Description;
        public string[] People;
        public static readonly BrandAgency[] All =
        {
            new BrandAgency { Id="chengguang", Name="澄光传媒", Specialty="数码评测与装机内容",
                Description="专业测评与参数讲解，主要面向电脑消费者。", People=new[]{"momo","ake"} },
            new BrandAgency { Id="yijian", Name="衣见文化", Specialty="生活方式与服装设计",
                Description="生活展示与设计内容，比较人选的行业适配后选择合作。", People=new[]{"qiao","life_designer"} },
            new BrandAgency { Id="wanjie", Name="玩界工作室", Specialty="模型手办与收藏",
                Description="模型制作与收藏交流，主要面向模型手办消费者。", People=new[]{"life_creator"} }
        };
        public static BrandAgency ForPerson(string person) => All.FirstOrDefault(a => a.People.Contains(person)) ?? All[0];
        public static BrandAgency Get(string id) => All.FirstOrDefault(a => a.Id == id);
    }
}
