using System.Linq;
using System.Text;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>毛坯铺子的开店清单：货架、收银台、展示桌、操作台要自己买、自己摆。</summary>
    public sealed partial class GameUi
    {
        /// <summary>开店清单（富文本，一项一行）。</summary>
        private string FitChecklistText()
        {
            if(S.IsSpecialBusiness(S.CurrentShop))return S.SpecialFitGap(S.CurrentShop)==null?"专用经营设施已摆齐。":"还需摆好："+S.SpecialFitGap(S.CurrentShop);
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
            sb.Append(S.RetailKind == "computer" ? $"\n{shelfState} 货架（建议 6 个：每类配件一个，至少一个）已摆 {shelf.Placed}/{shelf.Want}" : $"\n{shelfState} 商品货架（至少一个，按销售品类设置）已摆 {shelf.Placed} 个");
            if (shelf.Owned > 0) sb.Append($"，买了没摆 {shelf.Owned} 个");
            int unset = S.ShelfIndexes().Count(ix => S.ShelfKindAt(ix) == null);
            if (unset > 0) sb.Append($"<color=#ABB5A8>　{unset} 个货架还没设置摆什么（去“库存”页设置）</color>");

            if (S.IsPetShop(S.CurrentShop))
                sb.Append('\n').Append(FitLine(list.First(n => n.FurnitureId == PetShopCatalog.SmallHouse), "宠物屋（接代养用）", true));
            foreach (FixtureNeed n in list.Where(x => S.RetailKind == "computer" && (x.Kind == FixtureKind.Display || x.Kind == FixtureKind.Bench)))
                sb.Append('\n').Append(FitLine(n, n.Kind == FixtureKind.Display ? "展示台（可选品类：电脑整机或其他商品）" : "装机操作台（接装机订单用）", false));
            return sb.ToString();
        }

        private static string FitLine(FixtureNeed n, string label, bool required)
        {
            string mark = n.Placed >= n.Want ? "<color=#A9C9A9>✓</color>" : required ? "<color=#D79780>✗</color>" : "<color=#DDC18B>○</color>";
            string s = $"{mark} {label}　已摆 {n.Placed}/{n.Want}";
            if (n.Owned > 0) s += $"，买了没摆 {n.Owned} 个";
            return s;
        }

        private const float DockContentBottom = 54;
        private const float ShopFitWarningMinHeight = 78;
        private const float ShopFitDetailTop = 34;
        private const float ShopFitDetailBottom = 8;
        private RectTransform _shopFitWarning;
        private Text _shopFitTitle, _shopFitDetail, _shopFitActionLabel;

        private void BuildShopFitWarning()
        {
            _shopFitWarning = Ui.Panel(_dock, "ShopFitWarning", new Color32(91, 47, 31, 255)).rectTransform
                .Anchor(0, 0, 1, 0, 12, DockContentBottom, 12, -(DockContentBottom + ShopFitWarningMinHeight));
            var highlight = new Color32(255, 198, 134, 255);
            Ui.Panel(_shopFitWarning, "Accent", highlight).rectTransform.Anchor(0, 0, 0, 1, 0, 0, -4, 0);
            _shopFitTitle = Ui.Label(_shopFitWarning, "", 21, color: highlight);
            _shopFitTitle.fontStyle = FontStyle.Bold;
            _shopFitTitle.rectTransform.Anchor(0, 1, 1, 1, 14, -31, 124, 6);
            _shopFitDetail = Ui.Label(_shopFitWarning, "", 17, TextAnchor.UpperLeft, Color.white);
            _shopFitDetail.rectTransform.Anchor(0, 0, 1, 1, 14, ShopFitDetailBottom, 124, ShopFitDetailTop);
            var action = Ui.Button(_shopFitWarning, "去装修", GoToShopFitAction, 17, Ui.ButtonHot);
            action.name = "ShopFitWarningAction";
            ((RectTransform)action.transform).Anchor(1, .5f, 1, .5f, -110, -20, 10, -20);
            _shopFitActionLabel = action.GetComponentInChildren<Text>();
            _shopFitWarning.gameObject.SetActive(false);
        }

        private void RefreshShopFitWarning()
        {
            bool fitted = S.ShopFitted;
            bool visible = !fitted || S.IsRenovating;
            bool visibilityChanged = _shopFitWarning.gameObject.activeSelf != visible;
            if (visibilityChanged) _shopFitWarning.gameObject.SetActive(visible);

            float contentBottom = DockContentBottom;
            if (visible)
            {
                string missing = fitted ? null : S.ShopFitGap();
                string detail = fitted ? "今天可以继续摆放设施。" : missing == null ? "请先确认经营类型。" : $"缺少：{missing}";
                _shopFitTitle.text = fitted ? "今日歇业装修 · 明天恢复营业" : "暂不能开业";
                _shopFitActionLabel.text = !fitted && missing == null ? "选店型" : _tab == ShopTab.Renovate ? "摆放设施" : "去装修";
                if (visibilityChanged || _shopFitDetail.text != detail)
                {
                    _shopFitDetail.text = detail;
                    float height = Mathf.Max(ShopFitWarningMinHeight,
                        ShopFitDetailTop + Mathf.Ceil(_shopFitDetail.preferredHeight) + ShopFitDetailBottom);
                    _shopFitWarning.offsetMax = new Vector2(_shopFitWarning.offsetMax.x, DockContentBottom + height);
                }
                contentBottom = _shopFitWarning.offsetMax.y + 8;
            }

            var viewportFrame = (RectTransform)_dockContent.parent.parent;
            if (!Mathf.Approximately(viewportFrame.offsetMin.y, contentBottom))
                viewportFrame.offsetMin = new Vector2(viewportFrame.offsetMin.x, contentBottom);
        }

        private void GoToShopFitAction()
        {
            if (!S.ShopFitted && S.ShopFitGap() == null) RefreshOpeningKindChoice();
            else if (_tab == ShopTab.Renovate) StartPlacement(true);
            else SelectTab(ShopTab.Renovate);
        }

        /// <summary>“装修”页：这间店现在是什么状态、开店清单，以及进入编辑模式（俯视图）摆货架、柜台的入口。</summary>
        private void BuildRenovate()
        {
            _renovationStockSignature=RenovationStockSignature();
            Line(C,GameSession.SizeName(S.PlayerShop.Size)+" · 最多 "+S.ShopShelfLimit+" 个货架 · 已摆 "+S.ShelvesPlaced+" 个",18,Ui.Accent).name="RenovationShopCapacity";
            Line(C, "开业设施清单", 18, Ui.Accent);
            Para(C, FitChecklistText(), 15);
            if (S.IsPetShop(S.CurrentShop)) BuildPetRenovation();
            string label = S.IsRenovating ? "继续摆放" : S.ShopFitted ? "重新布置" : "开始装修";
            Ui.Size(Ui.Button(C, label, () => StartPlacement(true), 20, Ui.ButtonHot), -1, 48);
            var furniture=S.FurnitureToPlaceList(true).Where(kv=>kv.Value>0&&FurnitureCatalog.Get(kv.Key)!=null)
                .OrderBy(kv=>FurnitureCatalog.Get(kv.Key).IsFixture?0:1).ThenBy(kv=>FurnitureCatalog.Get(kv.Key).Name).ToArray();
            Line(C,"待摆放物品 · "+furniture.Length+" 种",18,Ui.Accent).name="RenovationInventoryHeading";
            if(furniture.Length==0)Line(C,"本店暂无待摆放物品。",16,Ui.Dim);
            foreach(var item in furniture)
            {
                string id=item.Key;var def=FurnitureCatalog.Get(id);
                var row=Ui.Row(C,56,8);row.name="RenovationItem_"+id;
                Ui.Size(Ui.Label(row,def.Name+" ×"+item.Value,18),-1,-1,1);
                Ui.Size(Ui.Button(row,"摆放",()=>StartPlacement(true,id),18,Ui.ButtonHot),88,-1);
            }
        }

        private string _renovationStockSignature;
        private string RenovationStockSignature()=>S.PlayerShop.Size+"|"+S.ShelvesPlaced+"|"+S.IsRenovating+"|"+S.ShopFitGap()+"|"
            +string.Join(";",S.FurnitureToPlaceList(true).Where(kv=>kv.Value>0).OrderBy(kv=>kv.Key).Select(kv=>kv.Key+":"+kv.Value));
    }
}
