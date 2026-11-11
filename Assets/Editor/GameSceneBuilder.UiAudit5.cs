using System;
using System.IO;
using System.Linq;
using System.Text;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        /// <summary>验证通用筛选维度：先打印数据，再截家具城与进货员两处界面。</summary>
        public static void UiAudit5FromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/ui-audit5"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; var ui = _game.Ui; s.EventsEnabled = false; s.Time.SetPaused(true); s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            var person = new Employee { Id = "audit-buyer", Name = "采购员阿福", DailyWageYuan = 150, CheckoutMinutes = 3 };
            s.MainShop.Buyer = person;
            s.Clock.Restore(s.Clock.Today, 9 * 60 + 5);
            void Snap(string name)
            {
                Frame(0);
                PumpList();
                _game.SnapCamera();
                Shot(name, 1920, 1080);
                Shot(name, 1920, 1080);
            }
            // 批处理里不跑 Unity 玩家循环，虚拟列表的 LateUpdate 不会触发；这里手动泵一次。
            void PumpList()
            {
                foreach (var behaviour in ui.Canvases.SelectMany(c => c.GetComponentsInChildren<MonoBehaviour>(true)))
                {
                    if (behaviour == null || behaviour.GetType().Name != "PurchaseListViewport") continue;
                    behaviour.GetType().GetMethod("Refresh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(behaviour, null);
                }
            }
            void Click(string name)
            {
                var button = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>()).FirstOrDefault(b => b.name == name);
                if (button == null) { Log("NO BUTTON " + name); return; }
                button.onClick.Invoke();
            }
            void Dump(string tag)
            {
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                foreach (var mb in ui.Canvases.SelectMany(c => c.GetComponentsInChildren<MonoBehaviour>(true)))
                {
                    if (mb == null || mb.GetType().Name != "PurchaseListViewport") continue;
                    var t = mb.GetType();
                    object Field(string n) => t.GetField(n, F)?.GetValue(mb);
                    var vp = (RectTransform)Field("_viewport");
                    var ct = (RectTransform)Field("_content");
                    Log($"LIST {tag}: count={Field("_count")} rowH={Field("_rowHeight")} first={Field("_first")} end={Field("_end")}" +
                        $" vp={(vp == null ? "null" : vp.rect.width + "x" + vp.rect.height)}" +
                        $" contentY={(ct == null ? 0 : ct.anchoredPosition.y)}" +
                        $" contentH={(ct == null ? 0 : ct.sizeDelta.y)}");
                    if (Field("_visibleRows") is System.Collections.IDictionary rows)
                        foreach (System.Collections.DictionaryEntry e in rows)
                        {
                            var row = (RectTransform)e.Value;
                            Log($"   row{e.Key}: active={row.gameObject.activeSelf} rect={row.rect.width.ToString("0")}x{row.rect.height.ToString("0")}" +
                                $" pos={row.anchoredPosition} children={row.childCount}" +
                                $" world={row.position.y.ToString("0")}");
                        }
                }
            }

            // ---- 数据层：维度是否真的挂上了 ----
            var catalog = s.EmployeePurchaseCatalog();
            Log("CATALOG offers=" + catalog.Count);
            foreach (string key in PurchaseFacets.Order)
            {
                var values = catalog.Select(o => o.Facet(key)).Where(v => v != null)
                    .SelectMany(v => v.Split(PurchaseOffer.FacetSeparator)).Distinct().ToList();
                Log("FACET " + PurchaseFacets.Label(key) + " (" + key + ") count=" + values.Count + " => " + string.Join(" / ", values));
            }
            var furniture = catalog.Where(o => o.Kind == PurchaseGoodsKind.Furniture).ToArray();
            Log("FURNITURE offers=" + furniture.Length);
            foreach (var o in furniture)
                Log("  FURN " + o.Name + " => shops[" + o.Facet(PurchaseFacets.Shop) + "] universal=" + FurnitureShops.IsUniversal(FurnitureCatalog.Get(o.Id)));
            var seasonal = catalog.Where(o => o.Facet(PurchaseFacets.Season) != null).ToArray();
            Log("SEASONAL offers=" + seasonal.Length + " sample:");
            foreach (var o in seasonal.Take(6))
                Log("  SEASON " + o.Name + " => " + o.Category + " / " + o.Facet(PurchaseFacets.Season));
            var clothing = seasonal.FirstOrDefault();
            if (clothing != null)
            {
                var onlySummer = new System.Collections.Generic.Dictionary<string, string> { [PurchaseFacets.Season] = "夏装" };
                Log("MATCH 夏装 filter on catalog: " + catalog.Count(o => o.Matches(onlySummer))
                    + "（其中服装 " + catalog.Count(o => o.Matches(onlySummer) && o.Facet(PurchaseFacets.Season) != null) + "）");
            }
            var shopFilter = new System.Collections.Generic.Dictionary<string, string> { [PurchaseFacets.Shop] = "电脑店" };
            Log("MATCH 电脑店 filter on furniture: " + furniture.Count(o => o.Matches(shopFilter)) + " / " + furniture.Length);

            // ---- 家具城：第一层应当是店型 ----
            ui.CloseAllWindows(); Teleport(s, "shop12"); ui.OnEnteredPlace(s.CurrentPlace); Frame(0.15f);
            Snap("80-mall-shoptypes");
            Click("ShopTab_电脑店"); Snap("81-mall-computer");
            Click("ShopTab_宠物用品店"); Snap("82-mall-pet");
            Click("ShopTab_餐馆"); Click("ShopTab_facility_电器"); Snap("83-mall-restaurant-electric");
            ui.CloseAllWindows();

            void ClearDrafts()
            {
                var field = ui.GetType().GetField("_purchaseDrafts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                (field?.GetValue(ui) as System.Collections.IDictionary)?.Clear();
            }
            // ---- 进货员 · 家具城：应出现「适用店铺」层 ----
            var buyer = s.Employees.First(x => x.Role == EmployeeRole.Buyer);
            var furnitureOffers = catalog.Where(o => o.Kind == PurchaseGoodsKind.Furniture).ToArray();
            foreach (string key in new[] { PurchaseFacets.Season, PurchaseFacets.Shop })
            {
                int n = furnitureOffers.Select(o => o.Facet(key)).Where(v => v != null)
                    .SelectMany(v => v.Split(PurchaseOffer.FacetSeparator)).Distinct().Count();
                Log("FURNITURE facet " + key + " values=" + n + " -> bar " + (n > 1 ? "SHOWN" : "HIDDEN"));
            }
            ClearDrafts();
            Log("REMEMBER shop12=" + s.RememberEmployeePurchaseSource(buyer.Person.Id, "shop12"));
            ui.OpenEmployeePurchase(buyer.Person.Id); Frame(0.3f); Snap("86-buyer-furniture"); Dump("furniture");
            Click("BuyerFacet_shop_餐馆"); Frame(0.2f); Snap("87-buyer-furniture-restaurant"); Dump("furniture-restaurant");
            ui.CloseAllWindows();

            // ---- 进货员 · 服装批发：应出现「分类 + 季节」两层 ----
            ClearDrafts();
            var clothingPlace = s.City.GetPlace("clothing_wholesale");
            Log("DEST clothing offers=" + catalog.Count(o => s.PurchaseDestination(o.Source) == clothingPlace));
            Log("REMEMBER clothing_wholesale=" + s.RememberEmployeePurchaseSource(buyer.Person.Id, "clothing_wholesale"));
            ui.OpenEmployeePurchase(buyer.Person.Id); Frame(0.3f); Snap("84-buyer-clothing"); Dump("clothing");
            Click("BuyerFacet_season_夏装"); Frame(0.2f); Snap("85-buyer-clothing-summer"); Dump("clothing-summer");
            ui.CloseAllWindows();

            Log("UI AUDIT 5 COMPLETE");
        });
    }
}
