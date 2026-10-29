using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    /// <summary>智能手机上能装的 app。刚买的手机只带应用商店和通讯录，其余去应用商店下载。</summary>
    public sealed class PhoneAppDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        public PhoneAppDef(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    public static class PhoneApps
    {
        public const string Store = "store";
        public const string Contacts = "contacts";
        public const string Schedule = "schedule";
        public const string Shop = "shop";
        public const string Chat = "chat";
        public const string Wholesale = "w1788";

        public static readonly IReadOnlyList<PhoneAppDef> All = new[]
        {
            new PhoneAppDef(Store, "应用商店", "下载各种 app。"),
            new PhoneAppDef(Contacts, "通讯录", "认识的人，打电话约出去。"),
            new PhoneAppDef(Schedule, "日历", "系统自带。约好的朋友、师傅上门、快递、焖饭一目了然，约会和师傅上门前 30 分钟会提醒。"),
            new PhoneAppDef(Shop, "网购", "买家具、衣服、礼品、大米，2 天后送到家。"),
            new PhoneAppDef(Chat, "聊天", "和认识的人聊两句，一定有回，涨一点交情（到 40 为止）。不装的话只能发短信，常常没人回。"),
            new PhoneAppDef(Wholesale, "1788 进货", "网上批发进货，比档口便宜，次日到店（要在自己的店里下单）。"),
        };

        /// <summary>刚买手机时自带的。</summary>
        public static readonly IReadOnlyList<string> Preinstalled = new[] { Store, Contacts, Schedule };

        public static PhoneAppDef Get(string id) => All.FirstOrDefault(a => a.Id == id);
    }
}