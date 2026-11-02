using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static RectTransform BuyerScroll(Transform parent)
        {
            var list=Ui.Scroll(parent);
            var area=(RectTransform)list.parent.parent;
            var layout=Ui.Size(area).GetComponent<LayoutElement>();
            layout.minHeight=100; layout.flexibleHeight=1;
            return list;
        }

        public void OpenShopBuyers()
        {
            if(!S.InPlayerShop)return;
            OpenShopBuyers(S.CurrentShop);
        }
        public void OpenShopBuyers(OwnedShop shop)
        {
            if(shop?.Rented!=true || !S.InPlayerShop&&!S.CanUseHomeWorkbench)return;
            OpenWindow("进货员 · "+S.ShopTitle(shop),960,800,w=>
            {
                if(!shop.Rented){w.Close();return;}
                var buyers=S.Employees.Where(x=>x.Shop==shop&&x.Role==EmployeeRole.Buyer).ToArray();
                Line(w.Content,$"本店 {buyers.Length} 名进货员 · 送达本店后仓（容量不限）",20,Ui.Accent);
                var list=BuyerScroll(w.Content);
                list.parent.parent.name="ShopBuyersList";
                if(buyers.Length==0)
                {
                    Line(list,"本店还没有进货员",22);
                    Para(list,"安排现有员工做进货员，或先到“员工”页聘用员工，再设置要采购的商品。",18,Ui.Dim);
                    Ui.Size(Ui.Button(list,"安排现有员工",()=>ManageBuyerEmployees(w),18,Ui.ButtonHot),-1,42);
                    Ui.Size(Ui.Button(list,S.InPlayerShop?"招聘进货员":"管理员工",()=>{if(S.InPlayerShop){w.Close();OpenDock();SelectTab(ShopTab.Staff);}else OpenEmployeesPanel();},18),-1,42);
                }
                foreach(var assignment in buyers)
                {
                    var person=assignment.Person;
                    var plan=person.PurchasePlan??new EmployeePurchasePlan();
                    Line(list,person.Name+" · "+S.EmployeeStatus(assignment),21,Ui.Accent);
                    string frequency=plan.Daily?plan.FrequencyText:"定期采购未启用 / 已暂停";
                    Line(list,frequency+" · "+(plan.InventoryThreshold?"库存阈值补货":"固定数量清单"),17);
                    if(plan.Daily)Line(list,S.EmployeePurchaseNextText(person.Id),15,Ui.Dim);
                    var items=plan.Items??Array.Empty<PurchaseLine>();
                    if(items.Length==0)Line(list,"还没有选择要进的货",17,Ui.Dim);
                    foreach(var line in items.Take(5))
                    {
                        var offer=S.EmployeePurchaseOffer(line,plan.SourceId);
                        Para(list,GameSession.PurchaseItemName(line)+" ×"+line.Count+" · "+(offer?.Source.Name??"货源不可用"),17);
                    }
                    if(items.Length>5)Line(list,"另有 "+(items.Length-5)+" 种商品，点“调整进货”查看完整清单。",16,Ui.Dim);
                    if(items.Length>0||plan.InventoryThreshold)
                    {
                        var quote=S.EstimateEmployeePurchase(person.Id);
                        Line(list,GameSession.PurchaseTransportName(plan.Transport)+(quote.Error==null?" · 计划每次预计 "+quote.Total:""),16,Ui.Dim);
                    }
                    var recent=S.EmployeePurchaseRecords(shop.Place.Id,person.Id).FirstOrDefault();
                    if(recent!=null)
                    {
                        Line(list,"最近一趟 · "+S.EmployeePurchaseTripStatus(recent),18,Ui.Good);
                        Para(list,PurchaseSources(recent)+"\n"+PurchaseTripTiming(recent),16,Ui.Dim);
                    }
                    else Line(list,"暂无进货记录",17,Ui.Dim);
                    if(person.LastPurchaseNote?.StartsWith("今日自动进货未出发")==true)Para(list,person.LastPurchaseNote,16,Ui.Warn);
                    var actions=Ui.Row(list,42);
                    Ui.Size(Ui.Button(actions,"进货记录",()=>OpenBuyerHistory(shop,person.Id,person.Name),18),-1,-1,1);
                    Ui.Size(Ui.Button(actions,items.Length==0?"设置进货":"调整进货",()=>OpenEmployeePurchase(person.Id,w),18,Ui.ButtonHot),-1,-1,1);
                    Line(list,"────────────────────────",16,Ui.Dim);
                }
                var footer=Ui.Row(w.Content,44);
                footer.name="ShopBuyersActions";
                Ui.Size(Ui.Button(footer,"管理员工",()=>ManageBuyerEmployees(w),18),-1,-1,1);
                Ui.Size(Ui.Button(footer,"本店进货记录",()=>OpenBuyerHistory(shop),18),-1,-1,1);
            },scrollContent:false);
        }

        private void ManageBuyerEmployees(Window parent)
        {
            OpenEmployeesPanel();
            _windows.Last().OnClosed+=()=>{parent.Rebuild();RebuildDock();};
        }

        private EmployeePurchaseReceiptLine[] PurchaseReceiptLines(EmployeePurchaseTrip trip) =>
            trip.ReceiptLines??(trip.Items??Array.Empty<PurchaseLine>()).Select(item=>new EmployeePurchaseReceiptLine
            {
                SourceId=item.SourceId, SourceName=S.City.HasPlace(item.SourceId)?S.City.GetPlace(item.SourceId).Name:"原货源",
                ProductName=GameSession.PurchaseItemName(item),Count=item.Count
            }).ToArray();

        private string PurchaseSources(EmployeePurchaseTrip trip) => string.Join(" → ",PurchaseReceiptLines(trip).Select(r=>r.SourceName).Distinct());
        private static string PurchaseTime(long minute) => new GameTimestamp(Math.Max(0,minute)).ToString();
        private string PurchaseTripTiming(EmployeePurchaseTrip trip)
        {
            if(trip.DepartAt<=0&&!trip.CostsRecorded)return "旧采购：未记录出发时间 · "+(trip.Delivered?"已送达":"预计送达 "+PurchaseTime(trip.DeliveryAt));
            long elapsed=Math.Max(0,Math.Min(S.Clock.Now.TotalMinutes,Math.Max(trip.ReturnAt,trip.DeliveryAt))-trip.DepartAt);
            return "出发 "+PurchaseTime(trip.DepartAt)+"\n"
                +(trip.Delivered?"已送达 ":"预计送达 ")+PurchaseTime(trip.DeliveryAt)+" · "
                +(trip.Returned&&trip.Delivered?"全程 "+(Math.Max(trip.ReturnAt,trip.DeliveryAt)-trip.DepartAt):"已用 "+elapsed
                    +" 分钟 · 预计全程 "+(Math.Max(trip.ReturnAt,trip.DeliveryAt)-trip.DepartAt))+" 分钟（游戏时间）";
        }

        private void OpenBuyerHistory(OwnedShop shop,string employeeId=null,string employeeName=null)
        {
            var expanded=new HashSet<int>{0};
            OpenWindow("进货记录 · "+(employeeName??S.ShopTitle(shop)),1000,820,w=>
            {
                var records=S.EmployeePurchaseRecords(shop.Place.Id,employeeId);
                Line(w.Content,"实际采购记录 · 按出发时间倒序 · 时间均为游戏时间",18,Ui.Accent);
                var list=BuyerScroll(w.Content);
                list.parent.parent.name="BuyerHistoryList";
                if(records.Length==0)Para(list,"暂无进货记录。首次出发后会记录采购地点、商品、耗时与费用。",19,Ui.Dim);
                for(int i=0;i<records.Length;i++)
                {
                    int index=i;var trip=records[i];
                    Line(list,trip.EmployeeName+" · "+S.EmployeePurchaseTripStatus(trip),21,Ui.Accent);
                    Para(list,PurchaseSources(trip),18);
                    Para(list,PurchaseTripTiming(trip),17);
                    Line(list,(trip.Returned?"员工已返店 ":"员工预计返店 ")+PurchaseTime(trip.ReturnAt),16,Ui.Dim);
                    Ui.Size(Ui.Button(list,expanded.Contains(i)?"收起采购明细":"展开采购明细",()=>{if(!expanded.Add(index))expanded.Remove(index);w.Rebuild();},17),-1,36);
                    if(expanded.Contains(i))BuildBuyerReceipt(list,trip);
                    Line(list,"────────────────────────",16,Ui.Dim);
                }
                Ui.Size(Ui.Button(w.Content,"返回进货员",w.Close,18,Ui.ButtonHot),-1,44).name="BuyerHistoryBack";
            },scrollContent:false);
        }

        private void BuildBuyerReceipt(Transform parent,EmployeePurchaseTrip trip)
        {
            var legs=trip.TravelLegs??Array.Empty<CityRoute>();
            foreach(var source in PurchaseReceiptLines(trip).GroupBy(r=>r.SourceId))
            {
                Line(parent,source.First().SourceName,18,Ui.Accent);
                var leg=legs.FirstOrDefault(r=>r.TargetPlaceId==source.Key);
                if(leg!=null)
                {
                    var next=legs.FirstOrDefault(r=>r.DepartMinute>=leg.ArriveMinute&&r!=leg);
                    long complete=next?.DepartMinute??trip.BoughtAt;
                    Para(parent,(S.Clock.Now.TotalMinutes>=leg.ArriveMinute?"到店 ":"预计到店 ")+PurchaseTime(leg.ArriveMinute)+"\n"
                        +(S.Clock.Now.TotalMinutes>=complete?"采购完成 ":"预计采购完成 ")+PurchaseTime(complete)
                        +" · 店内采购 "+Math.Max(0,complete-leg.ArriveMinute)+" 分钟",16,Ui.Dim);
                }
                else Line(parent,"旧采购未保存该货源的到店时间",16,Ui.Dim);
                foreach(var item in source)
                    Para(parent,item.ProductName+" ×"+item.Count+(trip.CostsRecorded?" · 单价 "+Money.FromCents(item.UnitCents):""),18);
            }
            Line(parent,GameSession.PurchaseTransportName(trip.Transport),17,Ui.Dim);
            if(trip.CostsRecorded)
                Para(parent,"本趟已付：货款 "+Money.FromCents(trip.GoodsCents)+" + 车费 "+Money.FromCents(trip.TravelCents)
                    +" + 运费 "+Money.FromCents(trip.FreightCents)+" = "+Money.FromCents(trip.GoodsCents+trip.TravelCents+trip.FreightCents),18,Ui.Good);
            else Line(parent,"旧采购未保存费用明细",17,Ui.Dim);
            if(!string.IsNullOrEmpty(trip.SkippedNote))Para(parent,"部分未购："+trip.SkippedNote,17,Ui.Warn);
            Line(parent,trip.Delivered?"本次货物已进入本店后仓。":"货物尚未入库。",17,trip.Delivered?Ui.Good:Ui.Dim);
        }
    }
}
