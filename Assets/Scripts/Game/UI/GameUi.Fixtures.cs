using System.Linq;
using System.Text;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    /// <summary>毛坯铺子的开店清单：货架、收银台、展示桌、操作台要自己买、自己摆。</summary>
    public sealed partial class GameUi
    {
        /// <summary>开店清单（富文本，一项一行）。</summary>
        private string FitChecklistText()
        {
            var sb = new StringBuilder();
            var list = S.ShopFitList();
            string gap = S.ShopFitGap();
            sb.Append(gap == null
                ? "<color=#A9C9A9>开业条件满足：收银台和货架都摆好了。</color>"
                : $"<color=#D79780>还不能开业，差：{gap}。</color>");

            FixtureNeed counter = list.First(n => n.Kind == FixtureKind.Counter);
            sb.Append('\n').Append(FitLine(counter, "收银台", true));

            FixtureNeed shelf = list.First(n => n.Kind == FixtureKind.Shelf);
            string shelfState = shelf.Placed >= shelf.Want ? "<color=#A9C9A9>✓</color>" : shelf.Placed >= FurnitureCatalog.MinShelves ? "<color=#DDC18B>◐</color>" : "<color=#D79780>✗</color>";
            sb.Append($"\n{shelfState} 货架（建议 6 个：每类配件一个，至少一个）已摆 {shelf.Placed}/{shelf.Want}");
            if (shelf.Owned > 0) sb.Append($"，买了没摆 {shelf.Owned} 个");
            int unset = S.ShelfIndexes().Count(ix => S.ShelfKindAt(ix) == null);
            if (unset > 0) sb.Append($"<color=#ABB5A8>　{unset} 个货架还没设置摆什么（去“库存”页设置）</color>");

            foreach (FixtureNeed n in list.Where(x => x.Kind == FixtureKind.Display || x.Kind == FixtureKind.Bench))
                sb.Append('\n').Append(FitLine(n, n.Kind == FixtureKind.Display ? "整机展示桌（每张摆 2 台整机，店越大摆得越多）" : "装机操作台（接装机订单用）", false));
            return sb.ToString();
        }

        private static string FitLine(FixtureNeed n, string label, bool required)
        {
            string mark = n.Placed >= n.Want ? "<color=#A9C9A9>✓</color>" : required ? "<color=#D79780>✗</color>" : "<color=#DDC18B>○</color>";
            string s = $"{mark} {label}　已摆 {n.Placed}/{n.Want}";
            if (n.Owned > 0) s += $"，买了没摆 {n.Owned} 个";
            return s;
        }

        /// <summary>柜台页顶部的一行提示：毛坯 / 装修中，点一下去“装修”页。装修好了且今天能开业就不显示。</summary>
        private void FitBanner(Transform parent)
        {
            if (S.ShopFitted && !S.IsRenovating) return;
            string head = S.ShopFitted
                ? "<color=#DDC18B>今天歇业装修，明天恢复营业。</color>"
                : "<color=#DDC18B>店还没布置好，开不了业：</color>" + S.ShopFitGap();
            Para(parent, head + "　<color=#9AA7B5>（在“装修”页里摆）</color>", 15);
        }

        /// <summary>“装修”页：这间店现在是什么状态、开店清单，以及进入编辑模式（俯视图）摆货架、柜台的入口。</summary>
        private void BuildRenovate()
        {
            string state = S.IsRenovating
                ? "<color=#DDC18B>装修中：今天歇业，明天恢复营业。</color>"
                : !S.ShopFitted
                    ? "<color=#DDC18B>毛坯：还不能开业。</color>"
                    : "<color=#A9C9A9>已经布置好，正常营业。</color>";
            Line(C, "店面装修　" + state, 18);
            Para(C, "货架、收银台、整机展示桌、装机操作台和装饰都在这里摆放。进入编辑模式后镜头会转成俯视，带网格和坐标；装修要歇业一天，摆完第二天才开业。", 15, Ui.Dim);
            Para(C, FitChecklistText(), 15);
            string label = S.IsRenovating ? "继续摆放（进入编辑模式）" : S.ShopFitted ? "重新布置（今天歇业装修）" : "开始装修（今天歇业，进入编辑模式）";
            Ui.Size(Ui.Button(C, label, () => StartPlacement(true), 20, Ui.ButtonHot), -1, 48);
            Line(C, "要买的设施去家具城（商业街）或手机上的 1788 下单，买来的东西在“我的”里。", 15, Ui.Dim);
        }
    }
}