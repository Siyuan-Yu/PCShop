using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildUsedMotorShop(Window w,Place place)
        {
            string id=place.Id;Line(w.Content,"二手车行 · 车源、人脉与成交",23,Ui.Accent);
            Para(w.Content,"小/中/大店分别有2/4/6个展位。车友每周介绍车源；检测后可整备或挂牌。每3天可能收到一位买家报价，报价保留2天；售价过高可能无人问津。",17,Ui.Dim);
            var stock=S.UsedMotors.Where(c=>c.ShopId==id).ToArray();Line(w.Content,"展位 "+stock.Length+" / "+S.UsedMotorCapacity(id),20);
            if(S.VentureAt(id)?.Paused==true)Ui.Size(Ui.Button(w.Content,"恢复车行营业",()=>{S.ToggleVenture(id);w.Rebuild();},18),-1,38);
            foreach(var s in stock)
            {
                var c=s;var m=MotorCatalog.Get(c.Vehicle.ModelId);var card=BusinessCard(w.Content,m.Name);
                Line(card,"累计投入 ¥"+c.Cost.ToString("N0")+" · "+(c.Inspected?"车况 "+c.Vehicle.Condition.ToString("0")+"%":"车况待检测"),17);
                var row=Ui.Row(card,36,6);
                Ui.Size(Ui.Button(row,"检测 ¥300",()=>{S.InspectUsedMotor(c.Vehicle.Id);w.Rebuild();},16,Ui.ButtonBg,!c.Inspected),-1,36,1);
                Ui.Size(Ui.Button(row,c.Inspected?"整备 ¥"+S.MotorRepairPrice(c.Vehicle).ToString("N0"):"先检测再整备",()=>{S.RepairUsedMotor(c.Vehicle.Id);w.Rebuild();},16,Ui.ButtonBg,c.Inspected&&c.Vehicle.Condition<100),-1,36,1);
                Ui.Size(Ui.Button(row,"转为自用车辆",()=>OpenMotorAcquisition(w,c.Vehicle.ModelId,true,c.Vehicle.Id),16),-1,36,1);
                if(c.Inspected)
                {
                    long suggested=(long)(m.Price*(.45+.004*c.Vehicle.Condition));long current=c.ListPrice>0?c.ListPrice:suggested;
                    Line(card,c.ListPrice>0?"挂牌 ¥"+c.ListPrice.ToString("N0"):"未挂牌 · 建议 ¥"+suggested.ToString("N0"),17,Ui.Accent);
                    var pricing=Ui.Row(card,36,6);
                    Ui.Size(Ui.Button(pricing,"降价10%",()=>{S.ListUsedMotor(c.Vehicle.Id,Math.Max(1000,current*9/10));w.Rebuild();},16),-1,36,1);
                    Ui.Size(Ui.Button(pricing,"按建议价挂牌",()=>{S.ListUsedMotor(c.Vehicle.Id,suggested);w.Rebuild();},16,Ui.ButtonHot),-1,36,1);
                    Ui.Size(Ui.Button(pricing,"提价10%",()=>{S.ListUsedMotor(c.Vehicle.Id,current*11/10);w.Rebuild();},16),-1,36,1);
                }
                if(c.Offer>0&&c.OfferUntil>=S.Clock.Today.TotalDays)
                    Ui.Size(Ui.Button(card,"接受买家 ¥"+c.Offer.ToString("N0")+" · 毛利 ¥"+(c.Offer-c.Cost).ToString("N0"),()=>ConfirmBusiness("交付二手车","收款并移交这台车，成交后从展位移除。",()=>{S.AcceptMotorBuyer(c.Vehicle.Id);w.Rebuild();}),18,Ui.ButtonHot),-1,42);
            }
            Line(w.Content,"车友介绍的车源 · 本周",21,Ui.Accent);
            foreach(var lead in S.MotorLeads())
            {
                var l=lead;Ui.Size(Ui.Button(w.Content,S.CityPerson(l.PersonId).Name+"介绍 · "+MotorCatalog.Get(l.ModelId).Name+" · 收购 ¥"+l.Price.ToString("N0"),()=>ConfirmBusiness("收购二手车","成交后车辆进入本店展位，需要支付检测与可能的整备费用。",()=>{S.BuyMotorLead(id,l.Id);w.Rebuild();}),17,Ui.ButtonBg,stock.Length<S.UsedMotorCapacity(id)),-1,44);
            }
            if(!S.MotorLeads().Any())Para(w.Content,"暂无新车源，去各级赛场认识车友；下周会有新的介绍。",17,Ui.Dim);
            foreach(var v in S.Garage.Where(v=>MotorCatalog.ClassOf(MotorCatalog.Get(v.ModelId).Kind)==2))
            {var car=v;Ui.Size(Ui.Button(w.Content,"从车库移入展位 · "+MotorCatalog.Get(car.ModelId).Name,()=>{S.ConsignMotor(id,car.Id);w.Rebuild();},16,Ui.ButtonBg,stock.Length<S.UsedMotorCapacity(id)&&S.MotorAvailable(car)),-1,36);}
        }
    }
}
