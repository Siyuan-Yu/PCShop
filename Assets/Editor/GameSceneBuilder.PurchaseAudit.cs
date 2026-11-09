using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        /// <summary>只截图：补货、进货、买东西相关窗口，用来审视哪里要下拉太多、哪里可以收紧。</summary>
        public static void PurchaseAuditFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/purchase-audit"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            for (int i = 0; i < 3; i++) s.AdvanceClock(24 * 60);
            var ui = _game.Ui; var t = typeof(GameUi);
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            void Snap2(string name) { Frame(0); _game.SnapCamera(); Shot(name, 1920, 1080); Shot(name, 1920, 1080); }
            void Call(string method, params object[] args) { var m = t.GetMethod(method, All); if (m == null) { Log("NO METHOD " + method); return; } m.Invoke(ui, args); Frame(0.05f); }
            void Field(string name, object value) { var f = t.GetField(name, All); if (f == null) Log("NO FIELD " + name); else f.SetValue(ui, value); }

            var shop = s.MyShops.FirstOrDefault();
            if (shop != null) { Teleport(s, shop.Place.Id); Frame(0.1f); }
            s.SetRestockTarget("ram0", 8); s.SetRestockTarget("ssd1", 6);

            // 手机 1788 批发 app 与自动补货页
            ui.CloseAllWindows(); ui.OpenWholesaleApp(true); Frame(0.05f); Snap2("01-app-restock");
            ui.CloseAllWindows(); ui.OpenWholesaleApp(); Frame(0.05f); Snap2("02-app-wholesale");

            // 商铺采购与补货三个页签
            ui.CloseAllWindows(); Field("_procurementOrders", false); Field("_procurementAgreement", false); Call("OpenCityProcurement"); Snap2("03-procurement-goods");
            ui.CloseAllWindows(); Field("_procurementOrders", true); Field("_procurementAgreement", false); Call("OpenCityProcurement"); Snap2("04-procurement-orders");
            ui.CloseAllWindows(); Field("_procurementOrders", false); Field("_procurementAgreement", true); Call("OpenCityProcurement"); Snap2("05-procurement-agreement");
            Field("_procurementOrders", false); Field("_procurementAgreement", false);

            // 补货策略窗口
            ui.CloseAllWindows();
            var item = s.EmployeePurchaseCatalog().FirstOrDefault(o => o.Kind == PurchaseGoodsKind.Part);
            if (item != null && shop != null) { Call("OpenCityProcurementPolicy", shop.Place.Id, item.Kind, item.Id, item.Name, null); Snap2("06-procurement-policy"); }
            ui.CloseAllWindows(); Call("OpenProcurementBudget"); Snap2("07-procurement-budget");

            // 实体店买东西：电子批发市场、商场
            foreach (var place in new[] { ("stall_cpu", "08-store-wholesale"), (s.City.Places.FirstOrDefault(p => p.Business == GameSession.MallBusiness)?.Id ?? "stall_cpu", "09-store-mall") })
            {
                ui.CloseAllWindows(); Teleport(s, place.Item1); ui.OnEnteredPlace(s.CurrentPlace); Frame(0.1f); Snap2(place.Item2);
            }
            ui.CloseAllWindows(); Call("OpenTruckWindow"); Snap2("10-truck");
            ui.CloseAllWindows();
            Log("PURCHASE AUDIT COMPLETE");
        });

        /// <summary>只截图：购物车装了东西后的版面、进货员采购清单窗口。</summary>
        public static void PurchaseAudit2FromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/purchase-audit"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; var ui = _game.Ui; s.EventsEnabled = false; s.Time.SetPaused(true); s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            var person = new Employee { Id = "audit-buyer", Name = "采购员阿福", DailyWageYuan = 150, CheckoutMinutes = 3 };
            s.MainShop.Buyer = person;
            s.Clock.Restore(s.Clock.Today, 9 * 60 + 5);
            void Snap2(string name) { Frame(0); _game.SnapCamera(); Shot(name, 1920, 1080); Shot(name, 1920, 1080); }
            var buyer = s.Employees.First(x => x.Role == EmployeeRole.Buyer);
            var offers = s.EmployeePurchaseCatalog();
            var picks = offers.Where(o => o.Kind == PurchaseGoodsKind.Part).Take(4).Select(o => o.Line(2)).ToArray();
            s.ConfigureEmployeePurchase(buyer.Person.Id, new EmployeePurchasePlan { Transport = VehicleKind.Taxi, Items = picks });
            ui.OpenEmployeePurchase(buyer.Person.Id); Snap2("11-employee-purchase");
            ui.CloseAllWindows();
            Teleport(s, "stall_cpu");
            var stall = s.City.GetPlace("stall_cpu");
            foreach (var id in new[] { "cpu0", "cpu1", "ram0", "ram1", "ssd0", "gpu1" })
            {
                try { s.CartAddStall(stall, PCShopSell.Core.Catalog.PartCatalog.Get(id), 3); } catch { }
            }
            ui.OnEnteredPlace(s.CurrentPlace); Frame(0.1f); Snap2("12-store-with-cart");
            ui.CloseAllWindows();
            Log("PURCHASE AUDIT2 COMPLETE");
        });
        /// <summary>只截图：采购两层结构 + 进货员安排。</summary>
        public static void PurchaseAudit4FromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/purchase-audit"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; var ui = _game.Ui; s.EventsEnabled = false; s.Time.SetPaused(true); s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            var person = new Employee { Id = "audit-buyer", Name = "采购员阿福", DailyWageYuan = 150, CheckoutMinutes = 3 };
            s.MainShop.Buyer = person;
            s.Clock.Restore(s.Clock.Today, 9 * 60 + 5);
            void Snap2(string name) { Frame(0); _game.SnapCamera(); Shot(name, 1920, 1080); Shot(name, 1920, 1080); }
            void Click(string name) => ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>()).First(b => b.name == name).onClick.Invoke();
            var buyer = s.Employees.First(x => x.Role == EmployeeRole.Buyer);
            var parts = s.EmployeePurchaseCatalog().Where(o => o.Kind == PurchaseGoodsKind.Part).Take(7).ToArray();
            int n = 0;
            foreach (var o in parts)
            {
                n++;
                s.SetProcurementPolicy(new ProcurementPolicy { ShopId = s.MainShop.Place.Id, Kind = o.Kind, ProductId = o.Id, Target = 4 + n * 2, Lower = 2 + n, Primary = ProcurementMethod.Employee, EmployeeId = buyer.Person.Id });
            }
            ui.OpenCityProcurementFor(s.MainShop); Snap2("21-proc-detail");
            Click("ProcurementAdd"); Snap2("22-proc-add");
            ui.CloseAllWindows();
            s.ConfigureEmployeePurchase(buyer.Person.Id, new EmployeePurchasePlan { Transport = VehicleKind.Taxi, Items = s.EmployeePurchaseCatalog().Where(o => o.Kind == PurchaseGoodsKind.Part).Take(4).Select(o => o.Line(2)).ToArray() });
            ui.OpenEmployeePurchase(buyer.Person.Id); UnityEngine.Canvas.ForceUpdateCanvases(); foreach (var sr in ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))) sr.verticalNormalizedPosition = 1; foreach (var vp in ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true)).Where(m => m != null && m.GetType().Name == "PurchaseListViewport")) vp.GetType().GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(vp, null); UnityEngine.Canvas.ForceUpdateCanvases(); Snap2("23-buyer-arrangement");
            Click("BuyerModeThreshold"); Snap2("24-buyer-auto");
            ui.CloseAllWindows();
            try
            {
                var spot = s.City.Places.FirstOrDefault(pl => s.IsVacant(pl)); Log("SPOT " + spot?.Id + " " + spot?.Name); s.DebugAddListing(spot.Id, 0, 60, PCShopSell.Core.Common.Money.FromYuan(1000)); Teleport(s, "agency");
                Log("LEASE " + s.SignLease(spot.Id, true)); Teleport(s, spot.Id);
            }
            catch (System.Exception e) { Log("RENT2 FAILED " + e.Message); }
            ui.OpenCityProcurement(); Snap2("20-proc-shops");
            ui.CloseAllWindows();
            foreach (var tab in new[] { 0, 2, 3, 4 })
            {
                typeof(PCShopSell.Game.UI.GameUi).GetField("_onlineTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(ui, tab);
                ui.OpenOnlinePanel();
                if (tab == 0 || tab == 2) { var cart = (System.Collections.Generic.Dictionary<string, int>)typeof(PCShopSell.Game.UI.GameUi).GetField("_parcelCart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ui); cart.Clear(); }
                Snap2("30-online-tab" + tab);
                ui.CloseAllWindows();
            }            Log("PURCHASE AUDIT4 COMPLETE");
        });        /// <summary>复现：在电子批发市场本体（不是档口）里买东西。</summary>
        public static void PurchaseAudit5FromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/purchase-audit"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; var ui = _game.Ui; s.EventsEnabled = false; s.Time.SetPaused(true); s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            void Snap2(string name) { Frame(0); _game.SnapCamera(); Shot(name, 1920, 1080); Shot(name, 1920, 1080); }
            var market = s.City.Places.First(pl => pl.Kind == PCShopSell.Core.World.PlaceKind.Wholesaler && pl.Name.Contains("电子"));
            Log("MARKET " + market.Id + " " + market.Name);
            Teleport(s, market.Id); s.Clock.Restore(s.Clock.Today, 18 * 60); s.AdvanceClock(5); Log("PEOPLE " + s.PeopleInPlace(market.Id).Count());
            try { ui.OnEnteredPlace(s.CurrentPlace); } catch (System.Exception e) { Log("EXC1 " + e); }
            Frame(0.1f); Snap2("40-market-all");
            foreach (var label in new[] { "电源机箱", "CPU" })
            {
                try
                {
                    var b = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>()).FirstOrDefault(x => x.GetComponentInChildren<UnityEngine.UI.Text>()?.text == label);
                    Log("TAB " + label + " found=" + (b != null));
                    b?.onClick.Invoke();
                }
                catch (System.Exception e) { Log("EXC2 " + e); }
                Frame(0.1f);
                try { var plus = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>()).FirstOrDefault(x => x.GetComponentInChildren<UnityEngine.UI.Text>()?.text == "+"); Log("PLUS " + (plus != null)); plus?.onClick.Invoke(); } catch (System.Exception e) { Log("EXC3 " + e); }
                Frame(0.1f); Snap2("41-market-" + label);
            }
            foreach (var tr in ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Transform>(true))) if (tr.name.StartsWith("Quantity_")) Log("QKEY " + tr.name);
            ui.CloseAllWindows();
            Log("PURCHASE AUDIT5 COMPLETE");
        });        /// <summary>只截图：各类店铺与网购的购买界面（左商品右购物车）。</summary>
        public static void PurchaseAudit3FromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/purchase-audit"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; var ui = _game.Ui; s.EventsEnabled = false; s.Time.SetPaused(true); s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            void Snap2(string name) { Frame(0); _game.SnapCamera(); Shot(name, 1920, 1080); Shot(name, 1920, 1080); }
            var ids = new (string, System.Func<PCShopSell.Core.World.Place, bool>)[]
            {
                ("v4-fastfood", p => p.Business == GameSession.FastFoodBusiness),
                ("v4-farm", p => p.Business == GameSession.FarmBusiness),
                ("v4-phone", p => p.Business == GameSession.PhoneBusiness),
                ("v4-tobacco", p => p.Business == PCShopSell.Core.Home.Gifts.TobaccoBusiness),
                ("v4-clothing", p => s.IsCityClothingRetail(p)),
                ("v4-mall", p => p.Business == GameSession.MallBusiness),
            };
            foreach (var (name, match) in ids)
            {
                var place = s.City.Places.FirstOrDefault(match);
                if (place == null) { Log("NO PLACE " + name); continue; }
                ui.CloseAllWindows(); Teleport(s, place.Id);
                try { ui.OnEnteredPlace(s.CurrentPlace); } catch (System.Exception e) { Log("ERR " + name + " " + e.Message); }
                Frame(0.1f); Snap2(name);
            }
            ui.CloseAllWindows();
            var cart = typeof(GameUi).GetField("_cart", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui) as System.Collections.Generic.Dictionary<string, int>;
            foreach (var id in new[] { "cpu0", "ram0", "ssd1" }) cart[id] = 2;
            ui.OpenWholesaleApp(); Frame(0.05f); Snap2("v4-online-parts");
            ui.CloseAllWindows();
            Log("PURCHASE AUDIT3 COMPLETE");
        });    }
}
