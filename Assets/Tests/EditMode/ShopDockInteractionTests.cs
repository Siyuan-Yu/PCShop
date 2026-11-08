using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Inventory;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class ShopDockInteractionTests
    {
        private static void WithUi(Action<GameSession,GameUi> action)
        {
            using(var s=GameSessionTests.Make())
            {
                GameSessionTests.Teleport(s,"pc");
                var root=new GameObject("ShopDockInteractionRegression");root.SetActive(false);
                var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);
                typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;
                var ui=new GameUi(controller);
                try{action(s,ui);}
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
        private static void Refresh(GameUi ui)=>typeof(GameUi).GetMethod("RefreshShopDockVisibility",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
        private static Button[] Buttons(GameUi ui)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).ToArray();
        [Test]
        public void CounterKeepsControlsAliveWhileUpdatingNumbersAndRebuildsChangedHours()
        {
            WithUi((s,ui)=>
            {
                s.EventsEnabled=false;s.Time.SetPaused(true);s.Clock.Restore(s.Clock.Today,600);
                ui.OpenDock();ui.DebugSelectTab(0);
                void TickDock()=>typeof(GameUi).GetMethod("RefreshDock",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                Button Category()=>Buttons(ui).Single(b=>b.name=="ShopMainCategory");
                Text StockText()=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Single(t=>t.text.StartsWith("后仓容量不限"));
                var control=Category();var text=StockText();string before=text.text;
                for(int i=0;i<12;i++)TickDock();
                Assert.AreSame(control,Category(),"暂停期间不应反复销毁柜台控件");
                s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("cpu0"),true,4);TickDock();
                Assert.AreSame(text,StockText(),"数字变化只更新原来的文字");Assert.AreNotEqual(before,text.text);
                Assert.AreSame(control,Category());
                s.PlayerShop.SetHours(660,1200);TickDock();
                Assert.AreNotSame(control,Category(),"营业时间变化必须刷新对应选项");
                Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text.Contains("11:00")));
            });
        }
        [Test]
        public void InteriorShowsEveryPresentEmployeeWithDistinctPositionsAndRealIdentity()
        {
            using(var s=GameSessionTests.Make())
            {
                GameSessionTests.Teleport(s,"pc");s.EventsEnabled=false;s.Clock.Restore(s.Clock.Today,10*60);
                var root=new GameObject("StaffInteriorRegression");
                try
                {
                    var cashier=new Employee{Id="cashier",Name="收银陈启明"};
                    var sales1=new Employee{Id="sales1",Name="销售赵成"};
                    var sales2=new Employee{Id="sales2",Name="销售小顾"};
                    var buyer=new Employee{Id="buyer",Name="进货小李"};
                    s.CurrentShop.Employee=cashier;
                    s.CurrentShop.AdditionalEmployees.Add(new AdditionalShopEmployee{Person=sales1,Role=EmployeeRole.Salesperson});
                    s.CurrentShop.AdditionalEmployees.Add(new AdditionalShopEmployee{Person=sales2,Role=EmployeeRole.Salesperson});
                    s.CurrentShop.AdditionalEmployees.Add(new AdditionalShopEmployee{Person=buyer,Role=EmployeeRole.Buyer});
                    foreach(var employee in new[]{cashier,sales1,sales2,buyer})CityTestPeople.AtAssignedWork(s,employee);
                    var view=new PCShopSell.Game.Runtime.ShopInteriorView(root.transform,s.PlayerShop.Size);
                    void Refresh()=>view.Update(s,0,0);
                    PCShopSell.Game.Runtime.CityPersonMarker[] Figures()=>root.GetComponentsInChildren<PCShopSell.Game.Runtime.CityPersonMarker>()
                        .Where(m=>m.name.StartsWith("ShopEmployee_")).ToArray();
                    Refresh();var figures=Figures();Assert.AreEqual(4,figures.Length);
                    CollectionAssert.AreEquivalent(new[]{cashier,sales1,sales2,buyer}.Select(e=>s.PersonIdOf(e)),figures.Select(f=>f.PersonId));
                    for(int i=0;i<figures.Length;i++)for(int j=i+1;j<figures.Length;j++)
                        Assert.GreaterOrEqual(Vector3.Distance(figures[i].transform.position,figures[j].transform.position),.89f);
                    foreach(var figure in figures)
                    {
                        var local=figure.transform.localPosition;
                        Assert.That(local.x,Is.InRange(.39f,view.Width-.39f));Assert.That(local.z,Is.InRange(.39f,view.Depth-.39f));
                        Assert.True(view.Emotes.Any(e=>e.PersonId==figure.PersonId));
                    }
                    var before=figures.Single(f=>f.PersonId==s.PersonIdOf(sales1)).gameObject;
                    Refresh();Assert.AreSame(before,Figures().Single(f=>f.PersonId==s.PersonIdOf(sales1)).gameObject,"稳定帧不重复生成模型");
                    s.CityPersonState(s.PersonIdOf(sales2)).LocationPlaceId=s.Home.Id;
                    Refresh();Assert.AreEqual(3,Figures().Length,"离场员工不应留在店内");
                    CityTestPeople.AtAssignedWork(s,sales2);Refresh();Assert.AreEqual(4,Figures().Length);
                    cashier.ShiftEnd=9*60;Refresh();Assert.AreEqual(3,Figures().Length,"下班收银员也应使用实际在岗规则");
                    Assert.False(Figures().Any(f=>f.PersonId==s.PersonIdOf(cashier)));
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
        [Test]
        public void EmployeeTabShowsLocalWorkersAndStandbyAndUsesTheSharedNpcDialogue()
        {
            WithUi((s,ui)=>
            {
                s.EventsEnabled=false;s.Clock.Restore(s.Clock.Today,10*60);
                var cashier=new Employee{Id="local-cashier",Name="本店陈启明",DailyWageYuan=150};
                var buyer=new Employee{Id="local-buyer",Name="本店小顾",DailyWageYuan=140};
                s.CurrentShop.Employee=cashier;s.CurrentShop.AdditionalEmployees.Add(new AdditionalShopEmployee{Person=buyer,Role=EmployeeRole.Buyer});
                CityTestPeople.AtAssignedWork(s,cashier);CityTestPeople.AtAssignedWork(s,buyer);
                var candidates=(System.Collections.Generic.List<Employee>)typeof(GameSession).GetField("_candidates",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s);
                candidates.Clear();candidates.Add(new Employee{Id="local-standby",Name="本店小刘",DailyWageYuan=120,CandidateLeavesAt=s.Clock.Now.TotalMinutes+3000});
                Assert.NotNull(s.RecruitEmployee(0));
                var idle=(System.Collections.Generic.List<Employee>)typeof(GameSession).GetField("_idleEmployees",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s);
                idle.Add(new Employee{Id="unrelated-standby",Name="其他待安排员工"});
                ui.OpenDock();ui.DebugSelectTab(5);
                Transform Dock()=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Single(t=>t.name=="ShopDock");
                Button Card(string id)=>Dock().GetComponentsInChildren<Button>().Single(b=>b.name=="ShopEmployeeChat_"+id);
                string DialogName()=>ui.Canvases.SelectMany(c=>c.transform.Cast<Transform>()).Single(t=>t.name.StartsWith("Window_")).name;
                Assert.AreEqual(3,Dock().GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("ShopEmployee_")&&!t.name.StartsWith("ShopEmployeeChat_")));
                StringAssert.Contains("收银员",Card(cashier.Id).GetComponentInChildren<Text>().text);
                StringAssert.Contains("收银值守",Card(cashier.Id).GetComponentInChildren<Text>().text);
                StringAssert.Contains("日薪",Card(cashier.Id).GetComponentInChildren<Text>().text);
                StringAssert.Contains("班次",Card(cashier.Id).GetComponentInChildren<Text>().text);
                StringAssert.Contains("尚未启用定期采购",Card(buyer.Id).GetComponentInChildren<Text>().text);
                StringAssert.Contains("待安排工作",Card("local-standby").GetComponentInChildren<Text>().text);
                Assert.False(Dock().GetComponentsInChildren<Text>().Any(t=>t.text.Contains("其他待安排员工")));
                var state=s.CityPersonState(s.PersonIdOf(cashier));string place=state.LocationPlaceId;long now=s.Clock.Now.TotalMinutes;
                Card(cashier.Id).onClick.Invoke();Assert.True(ui.AnyModalOpen);Assert.True(s.Time.IsHeld);
                Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text=="随便聊聊"));
                string fromCard=DialogName();ui.CloseTopWindow();
                ui.OpenPersonDialogue(s.PersonIdOf(cashier));Assert.AreEqual(fromCard,DialogName(),"与其他NPC复用完全相同的对话入口");
                Assert.AreEqual(place,state.LocationPlaceId);Assert.AreEqual(now,s.Clock.Now.TotalMinutes);
                ui.CloseTopWindow();Assert.False(s.Time.IsHeld);
                Assert.True(s.ArrangeEmployeeWork(cashier.Id,s.CurrentShop.Place.Id,EmployeeRole.Salesperson));
                typeof(GameUi).GetMethod("RefreshDock",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                StringAssert.Contains("销售员",Card(cashier.Id).GetComponentInChildren<Text>().text);
                StringAssert.Contains("导购",Card(cashier.Id).GetComponentInChildren<Text>().text);
                state.LocationPlaceId=s.Home.Id;
                typeof(GameUi).GetMethod("RefreshDock",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                StringAssert.DoesNotContain("导购与销售接待",Card(cashier.Id).GetComponentInChildren<Text>().text);
                Assert.True(Dock().GetComponentsInChildren<Text>().Any(t=>t.text.Contains("招聘告示")),"招聘入口保留");
            });
        }
        [Test]
        public void InventoryShowsDisplayedGoodsAboveWarehouseRemainderWithoutOpeningAnotherWindow()
        {
            WithUi((s,ui)=>
            {
                s.PlayerShop.Stock.Clear();s.CurrentShop.Layout.Clear();
                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.ShopBox});
                s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>());
                var cpu=PartCatalog.Get("cpu0");var clothing=Gifts.All.First(g=>ClothingProducts.IsClothing(g.Id));
                s.PlayerShop.Stock.ForceAdd(cpu,true,100);s.PlayerShop.Stock.ForceAddGoods(clothing.Id,500);
                s.ShopLocker.AddFurniture("shop_poster",3);s.HomeBox.AddFurniture(FurnitureCatalog.FridgeSmall,7);
                ui.OpenDock();ui.DebugSelectTab(1);
                Transform Dock()=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Single(t=>t.name=="ShopDock");
                Transform Find(string name)=>Dock().GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name==name);
                int Count(string name)
                {
                    var item=Find(name);return item==null?0:int.Parse(item.Find("StockQuantity").GetComponent<Text>().text.Substring(1));
                }
                void RefreshStock()=>typeof(GameUi).GetMethod("RefreshDock",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                Assert.AreEqual(0,Count("ShelfPart_cpu0_b"));Assert.AreEqual(100,Count("WarehouseItem_part:cpu0:b"));
                Assert.AreEqual(500,Count("WarehouseItem_stockgift:"+clothing.Id));
                Assert.AreEqual(3,Count("WarehouseItem_furn:shop_poster"));
                Assert.IsNull(Find("WarehouseItem_furn:"+FurnitureCatalog.FridgeSmall));
                Assert.False(Dock().GetComponentsInChildren<Text>().Any(t=>t.text.Contains("打开后仓")));
                Assert.Less(Find("StockShelvesHeading").GetSiblingIndex(),Find("StockWarehouseSection").GetSiblingIndex());

                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Shelf});Assert.True(s.SetShelfKind(1,"cpu"));
                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Shelf});Assert.True(s.SetShelfKind(2,ShelfKinds.ForGift(clothing.Id).Id));
                RefreshStock();
                Assert.IsNull(Find("ShelfPart_cpu0_b"));
                Assert.IsNull(Find("ShelfGoods_"+clothing.Id));
                Find("ToggleShelfItems_1").GetComponent<Button>().onClick.Invoke();
                Find("ToggleShelfItems_2").GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(s.PlayerShop.OnShelf(cpu),Count("ShelfPart_cpu0_b"));
                Assert.AreEqual(100,Count("ShelfPart_cpu0_b")+Count("WarehouseItem_part:cpu0:b"));
                Assert.AreEqual(s.PlayerShop.OnShelfGoods(clothing.Id),Count("ShelfGoods_"+clothing.Id));
                Assert.AreEqual(500,Count("ShelfGoods_"+clothing.Id)+Count("WarehouseItem_stockgift:"+clothing.Id));
                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Shelf});Assert.True(s.SetShelfKind(3,ShelfKinds.ForGift(clothing.Id).Id));
                RefreshStock();
                Assert.IsNull(Find("ShelfItems_3"));
                Find("ToggleShelfItems_3").GetComponent<Button>().onClick.Invoke();
                var displayed = Dock().GetComponentsInChildren<Transform>().Where(t=>t.name=="ShelfGoods_"+clothing.Id).ToArray();
                Assert.AreEqual(2,displayed.Length);
                Assert.AreEqual(500,displayed.Sum(t=>int.Parse(t.Find("StockQuantity").GetComponent<Text>().text.Substring(1)))+Count("WarehouseItem_stockgift:"+clothing.Id));
                Assert.Zero(displayed.Sum(t=>t.GetComponentsInChildren<Button>().Length));
                Assert.Zero(Find("WarehouseItem_stockgift:"+clothing.Id).GetComponentsInChildren<Button>().Length);
                Assert.Zero(Find("ShelfPart_cpu0_b").GetComponentsInChildren<Button>().Length);
                StringAssert.Contains("已满",Find("ShelfCapacity_1").GetComponent<Text>().text);
                s.ShopLocker.TakeFurniture("shop_poster",3);RefreshStock();Assert.IsNull(Find("WarehouseItem_furn:shop_poster"));
                s.PlayerShop.Stock.Clear();RefreshStock();Assert.AreEqual(0,Count("ShelfGoods_"+clothing.Id));
                Assert.AreEqual(0,Count("WarehouseItem_stockgift:"+clothing.Id));
            });
        }
        [Test]
        public void RenovationOnlyShowsUnplacedFurnitureAndRefreshesWhenStockChanges()
        {
            WithUi((s,ui)=>
            {
                s.ShopLocker.AddFurniture(FurnitureCatalog.Shelf,3);
                s.ShopLocker.AddFurniture("shop_plant",2);
                s.HomeBox.AddFurniture(FurnitureCatalog.FridgeSmall,9);
                s.Backpack.AddGift(Gifts.Cig,2);
                ui.OpenDock();ui.DebugSelectTab(4);
                Transform Dock()=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Single(t=>t.name=="ShopDock");
                string[] Labels()=>Dock().GetComponentsInChildren<Text>().Select(t=>t.text).ToArray();
                Transform Item(string id)=>Dock().GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name=="RenovationItem_"+id);
                Assert.False(Labels().Any(t=>t.Contains("进货员")||t.Contains("商品上架")||t.Contains("进货渠道")));
                Assert.False(Labels().Any(t=>t.Contains("镜头")||t.Contains("网格")||t.Contains("1788")));
                Assert.NotNull(Item(FurnitureCatalog.Shelf));Assert.NotNull(Item("shop_plant"));
                Assert.True(Item(FurnitureCatalog.Shelf).GetComponentsInChildren<Text>().Any(t=>t.text==FurnitureCatalog.Get(FurnitureCatalog.Shelf).Name+" ×3"));
                Assert.IsNull(Item(FurnitureCatalog.ShopCounter),"已摆出的柜台不应列为待摆库存");
                Assert.IsNull(Item(FurnitureCatalog.FridgeSmall),"不能混入家中后仓");
                s.ShopLocker.TakeFurniture(FurnitureCatalog.Shelf,3);
                typeof(GameUi).GetMethod("RefreshDock",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                Assert.IsNull(Item(FurnitureCatalog.Shelf));
                Item("shop_plant").GetComponentInChildren<Button>().onClick.Invoke();
                Assert.True(ui.AnyModalOpen,"营业店铺点击摆放沿用歇业确认");
                Assert.AreEqual(2,s.ShopLocker.FurnitureCount("shop_plant"),"确认和实际放下前不消耗物品");
                ui.CloseAllWindows();ui.OpenDock();ui.DebugSelectTab(1);
                Assert.True(Labels().Any(t=>t.StartsWith("进货员 ·")),"进货员入口继续留在库存页");
                s.ShopLocker.TakeFurniture("shop_plant",2);ui.DebugSelectTab(4);
                Assert.True(Labels().Contains("本店暂无待摆放物品。"));
            });
        }
        [Test]
        public void DockRemainsRecoverableAcrossPauseDialogsAndExplicitCollapse()
        {
            WithUi((s,ui)=>
            {
                ui.OpenDock();Refresh(ui);
                var toggle=Buttons(ui).Single(b=>b.name=="ShopDockToggle");
                Assert.True(toggle.gameObject.activeSelf);Assert.True(ui.DockOpen);
                ui.DebugSelectTab(1);s.Time.SetPaused(true);Refresh(ui);
                Assert.True(ui.DockOpen);Assert.True(s.Time.IsUserPaused);
                ui.OpenWindow("临时操作",400,300,w=>{});Assert.True(s.Time.IsHeld);
                ui.CloseAllWindows();Refresh(ui);Assert.True(ui.DockOpen,"关闭其他窗口后店铺面板应恢复");
                Assert.True(s.Time.IsUserPaused,"恢复侧栏不能改变玩家暂停状态");
                toggle.onClick.Invoke();Assert.False(ui.DockOpen);Refresh(ui);Assert.False(ui.DockOpen);
                s.Time.SetPaused(false);ui.OpenWindow("其他操作",400,300,w=>{});ui.CloseAllWindows();Refresh(ui);
                Assert.False(ui.DockOpen,"主动收起后不能被定时刷新或弹窗强行展开");
                toggle.onClick.Invoke();Assert.True(ui.DockOpen);Assert.False(s.Time.IsHeld);Assert.False(s.Time.IsUserPaused);
                Assert.False(Buttons(ui).Any(b=>b.name=="ShopMainCategory"),"重新展开应保留库存页");
                s.Leave();Refresh(ui);Assert.False(ui.DockOpen);Assert.False(toggle.gameObject.activeSelf);
                GameSessionTests.Teleport(s,"pc");Refresh(ui);Assert.True(ui.DockOpen);Assert.True(toggle.gameObject.activeSelf);
            });
        }
        [Test]
        public void OnlyCounterSelectsMainCategoryAndUpgradeTabIsRemoved()
        {
            WithUi((s,ui)=>
            {
                ui.OpenDock();Refresh(ui);Assert.AreEqual(6,ui.DebugTabCount);
                var dock=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Single(t=>t.name=="ShopDock");
                var labels=dock.Find("Tabs").GetComponentsInChildren<Text>().Select(t=>t.text.Replace("› ","")).ToArray();
                CollectionAssert.AreEqual(new[]{"柜台","库存","定价","订单","装修","员工"},labels);
                for(int i=0;i<ui.DebugTabCount;i++)
                {
                    ui.DebugSelectTab(i);
                    Assert.AreEqual(i==0?1:0,Buttons(ui).Count(b=>b.name=="ShopMainCategory"));
                    Assert.False(Buttons(ui).Any(b=>b.GetComponentInChildren<Text>()?.text=="主售品类"));
                }
                ui.DebugSelectTab(0);
                foreach(var dropdown in dock.GetComponentsInChildren<Dropdown>().Where(d=>d.name=="ShopOpeningTime"||d.name=="ShopClosingTime"))
                    Assert.True(dropdown.options.Where(o=>o.text.Contains(":")).All(o=>o.text.EndsWith(":00")),"营业时间只提供整点选项");
                Buttons(ui).Single(b=>b.name=="ShopMainCategory").onClick.Invoke();
                Assert.True(ui.AnyModalOpen);Assert.True(s.Time.IsHeld);
                ui.CloseAllWindows();Refresh(ui);Assert.True(ui.DockOpen);Assert.False(s.Time.IsHeld);
            });
        }
    }
}
