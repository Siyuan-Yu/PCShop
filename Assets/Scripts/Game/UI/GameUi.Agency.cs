using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        /// <summary>房产中介：在租的铺面一间间列出来（大小、位置、月租、什么时候能签），点签约就租下 / 搬过去。</summary>
        private void BuildAgency(Window w, Place p)
        {
            int today = S.Clock.Today.TotalDays;
            Para(w.Content, "中介小陈：“商业街上的铺子都在我这儿挂着。离地铁越近人越多、租金越贵；大铺子房东要看你的名气。" +
                            "签约一次付清：首月租金 + 押金（一个月）+ 中介费（标价一个月房租，跟我混熟了能打折），押金退租换铺时退给你。租下来是毛坯，货架收银台得自己去家具城置办。" +
                            "有的铺子老租户还没到期，先给你挂着，到期就能签；挂牌到期没人签，就被别人租走了。”", 16, Ui.Dim);
            int rapport = S.AgentRapport;
            double off = S.AgentDiscount;
            Line(w.Content, $"和小陈：{StaffTalk.LevelName(rapport)}（熟络 {rapport}）　中介费折扣 {(off > 0 ? $"<color=#7CE38B>{off * 100:0.#}% off</color>" : "暂无")}　<size=14><color=#9AA3AD>每点熟络省 {RealEstate.AgentDiscountPerRapport * 100:0.#}%，最多 {RealEstate.MaxAgentDiscount * 100:0}%；点左边头像和他聊聊、送点小礼物、听他讲讲自己</color></size>", 16);
            if (S.HasShop)
            {
                Place mine = S.PlayerShopPlace;
                Line(w.Content, $"你现在的店：{UnitName(mine)}，{GameSession.SizeName(mine.Size)}，月租 {S.CurrentShopRent}，押金 {S.Lease.Deposit}。签别的铺子就是搬店（搬家费 {RealEstate.MoveFee}，旧押金退还）。", 16);
            }
            else
            {
                Line(w.Content, $"<color=#FFD54A>你还没有店铺。</color>现金 {S.Ledger.Balance}，签一间小铺再留点钱进货。", 17);
            }

            RectTransform list = Ui.Scroll(w.Content, 470);
            Ui.Size((RectTransform)list.parent.parent, -1, -1, 1);
            var listings = S.Listings.OrderBy(l => l.OpenDay > today ? 1 : 0).ThenBy(l => l.OpenDay).ThenBy(l => l.Rent.Cents).ToList();
            foreach (ShopListing listing in listings)
            {
                ShopListing l = listing;
                Place place = S.ListingPlace(l);
                bool open = l.OpenDay <= today;
                string blocker = S.LeaseBlocker(l);
                string when = open
                    ? $"<color=#7CE38B>现在就能签</color>，挂牌还剩 {l.CloseDay - today + 1} 天"
                    : $"<color=#FFD54A>现租户租约还剩 {l.OpenDay - today} 天（约 {Mathf.Max(1, Mathf.RoundToInt((l.OpenDay - today) / 30f))} 个月）</color>，到期后挂 {l.CloseDay - l.OpenDay + 1} 天";
                string who = S.IsVacant(place) ? "空铺" : $"现在是「{place.Name}」";
                string text =
                    $"<b>{UnitName(place)}</b>  {GameSession.SizeName(l.Size)} {RealEstate.SizeArea(l.Size)}  <size=14><color=#9AA3AD>{who}</color></size>\n" +
                    $"<size=15>离地铁站约 {S.StationDistance(place):0} 米 · 客流系数 ×{S.LocationFactorOf(place):0.00} · 月租 <color=#7CE38B>{l.Rent}</color></size>\n" +
                    $"<size=15>首月 {l.Rent} + 押金 {RealEstate.Deposit(l.Rent)} + 中介费 {S.AgentFeeFor(l.Rent)}{(off > 0 ? $"<color=#7CE38B>（标价 {RealEstate.AgentFee(l.Rent)}）</color>" : "")}</size>\n" +
                    $"<size=15>{when}{(blocker != null && open ? $"　<color=#E0806A>{blocker}</color>" : "")}</size>";
                RectTransform row = Ui.Row(list, 104);
                Ui.Size(Ui.Label(row, text, 17), 560);
                Ui.Size(Ui.Button(row, "去看看", () =>
                {
                    w.Close();
                    _ctrl.GoToPlace(place);
                }, 16), 80);
                string sign = S.HasShop ? $"签约搬过去\n<size=13>付 {S.LeaseCost(l)}</size>" : $"签约租下\n<size=13>付 {S.LeaseCost(l)}</size>";
                Ui.Size(Ui.Button(row, sign, () =>
                {
                    if (S.SignLease(l.PlaceId)) Toast($"签下了{place.Name}那间铺子。");
                    w.Rebuild();
                }, 16, Ui.ButtonHot, blocker == null), 140);
            }
            if (listings.Count == 0) Line(list, "这几天没有在租的铺子，过几天再来看看。", 17, Ui.Dim);
            if (S.HasShop && !S.InPlayerShop)
            {
                Ui.Size(Ui.Button(w.Content, "去我的店", () =>
                {
                    w.Close();
                    _ctrl.GoToPlace(S.PlayerShopPlace);
                }, 18, Ui.ButtonBg), -1, 42);
            }
        }

        /// <summary>铺面的叫法：本来就空着的叫原名（街尾空铺），有店的叫“xx原址”。</summary>
        private static string UnitName(Place p) => p.Business == RealEstate.VacantBusiness ? p.Name : p.Name + "原址";
    }
}
