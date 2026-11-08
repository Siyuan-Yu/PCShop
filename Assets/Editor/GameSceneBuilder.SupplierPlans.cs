using System.IO;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void SupplierPlansFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/supplier-plans");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.DebugAddCash(500000);Check(s.DebugRentStarterShop(computerScenario: true),"租下测试门店");s.Clock.Restore(new GameDate(0),600);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            var offer=s.EmployeePurchaseCatalog().First(o=>o.Kind==PurchaseGoodsKind.Part&&o.Source.Id=="stall_gpu"&&o.Id=="gpu0");
            var employees=new Employee[3];
            for(int i=0;i<3;i++)
            {
                var employee=employees[i]=new Employee{Id="allocation-ui-"+i,Name="采购员"+(i+1),DailyWageYuan=150,CheckoutMinutes=3};
                if(i==0)s.MainShop.Buyer=employee;else s.MainShop.AdditionalEmployees.Add(new AdditionalShopEmployee{Role=EmployeeRole.Buyer,Person=employee});
                employee.PurchasePlan=new EmployeePurchasePlan{Daily=i<2,HasWeeklySchedule=true,Frequency=i==2?PurchaseFrequency.MondayWednesdayFriday:PurchaseFrequency.Daily,Transport=VehicleKind.Taxi,
                    BrowseSourceSelected=true,BrowseSourceId=offer.Source.Id,Items=i<2?new[]{offer.Line(i==0?6:4)}:new PurchaseLine[0]};
                if(i<2)Check(s.ConfigureEmployeePurchase(employee.Id,employee.PurchasePlan),"保存前两份共享配额安排");
            }
            Teleport(s,s.MainShop.Place.Id);_game.Ui.CloseAllWindows();_game.Ui.OpenEmployeePurchase(employees[2].Id);Frame(0);
            var search=_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<InputField>()).Single(input=>input.placeholder is Text hint&&hint.text=="搜索商品");search.text=offer.Name;search.onEndEdit.Invoke(offer.Name);Frame(0);
            var text=_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Select(t=>t.text).ToArray();
            Check(text.Any(t=>t=="每天一次")&&text.Any(t=>t=="周一、三、五")&&text.Any(t=>t=="每周三"),"只显示三种星期频率");
            Check(text.Any(t=>t.Contains("周三 10")),"20份实货减去6+4后显示周三可分配10份");
            Shot("allocation_remaining");Shot("allocation_remaining_720",1280,720);
            var maximum=_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.name=="PurchaseMaximum_"+offer.Key);maximum.onClick.Invoke();Frame(0);
            Shot("allocation_maximum");ClickGuideButton("完成");Frame(0);
            var saved=employees[2].PurchasePlan.Items.Single();Check(saved.BuyMaximum&&saved.ReservedByWeekday[2]==10,"实际最大按钮保存10份周三额度");
            Check(s.PurchaseRemaining(offer)==20,"配置预留不扣实货、不扣货款");
            Log("SUPPLIER PLAN GRAPHICS COMPLETE");
        });
    }
}
