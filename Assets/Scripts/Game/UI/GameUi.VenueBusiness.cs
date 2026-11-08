using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenVenueManagement(Place place)
        {
            if(!S.IsOwnedVenue(place))return;
            var shop=S.ShopAt(place);
            OpenWindow(S.ShopTitle(shop)+" · 场馆管理",1000,820,w=>
            {
                var state=S.VenueOperation(place.Id);
                Line(w.Content,"专用场地 · "+S.VenueBusinessName(place)+" · "+(S.VenueOpen(place)?"营业中":state.Accepting?"等待营业时间或值守到岗":"暂停接客"),22,Ui.Accent);
                Para(w.Content,"保留场地原用途。原教练不默认受雇；自己值守或另聘员工。老板自用免费，正常消耗时间与体力。关闭管理窗口后时间继续。",17,Ui.Dim);
                Line(w.Content,"今日顾客 "+state.Customers+" 人 · 收入 "+Money.FromCents(state.RevenueCents)+" · 累计收入 "+Money.FromCents(state.TotalRevenueCents),20,Ui.Good);
                Line(w.Content,"场地占用（含在途预约） "+state.CustomersHere.Length+" / "+S.VenueCapacity(place)+" 人 · 周租 "+shop.Lease.Rent+" · 维护 ¥"+S.VenueMaintenanceYuan(place)+"/天",18);
                var hours=Ui.Row(w.Content,42,8);
                Ui.Size(Ui.Label(hours,"营业时间",18),100);
                var options=Enumerable.Range(0,24).Select(h=>h.ToString("00")+":00").ToArray();
                Ui.Size(Ui.Choice(hours,options,state.Open/60,i=>{S.ConfigureVenue(place.Id,state.Accepting,i*60,state.Close,state.PriceCents);w.Rebuild();}),150,42).name="VenueOpeningTime";
                Ui.Size(Ui.Choice(hours,options,state.Close/60%24,i=>{S.ConfigureVenue(place.Id,state.Accepting,state.Open,i*60,state.PriceCents);w.Rebuild();}),150,42).name="VenueClosingTime";
                Ui.Size(Ui.Button(hours,state.Accepting?"暂停接客":"开启接客",()=>{S.ConfigureVenue(place.Id,!state.Accepting,state.Open,state.Close,state.PriceCents);w.Rebuild();},18,Ui.ButtonHot),160,42).name="VenueToggleOpen";
                var prices=Ui.Row(w.Content,42,8);
                Ui.Size(Ui.Label(prices,"单次价格（按当前价预约）",18),250);
                Ui.Size(Ui.Button(prices,"− ¥5",()=>{S.ConfigureVenue(place.Id,state.Accepting,state.Open,state.Close,System.Math.Max(100,state.PriceCents-500));w.Rebuild();},18),90,42);
                Ui.Size(Ui.Label(prices,Money.FromCents(state.PriceCents)+"/次",21),150);
                Ui.Size(Ui.Button(prices,"+ ¥5",()=>{S.ConfigureVenue(place.Id,state.Accepting,state.Open,state.Close,System.Math.Min(100000,state.PriceCents+500));w.Rebuild();},18),90,42);
                Line(w.Content,"值守："+(shop.Employee==null?"尚未雇佣值守员":shop.Employee.Name+" · 日薪 "+shop.Employee.DailyWage)+"；"+(S.VenueHasAttendant(shop)?"当前有人值守":"当前无人值守，不能接待新顾客"),18);
                if(shop.Employee!=null)
                {
                    Ui.Size(Ui.Button(w.Content,"调整值守班次",()=>OpenEmployeeShift(shop.Employee.Id,w),18),-1,42);
                    Ui.Size(Ui.Button(w.Content,"员工与排班（沿用实际工资结算）",OpenEmployeesPanel,18),-1,42);
                }
                if(S.StaffCommitted(shop)<GameSession.StaffLimit(shop.Place.Size))
                {
                    Ui.Size(Ui.Button(w.Content,"发布值守招聘（"+GameSession.JobAdFee+"）",()=>{S.PostJobAd();w.Rebuild();},17,Ui.ButtonBg,S.JobAdDay<0),-1,40);
                    foreach(var candidate in S.Candidates.Select((person,index)=>new{person,index}).ToArray())
                    {
                        var c=candidate;
                        Ui.Size(Ui.Button(w.Content,"雇佣 "+c.person.Name+" 值守 · 日薪 "+c.person.DailyWage,()=>{S.Hire(c.index);w.Rebuild();},17,Ui.ButtonBg,S.CandidateAvailable(c.person)),-1,40);
                    }
                }
                Line(w.Content,"在场顾客 / 在途预约",18,Ui.Accent);
                var customers=GoodsList(w.Content,120);
                foreach(var customer in state.CustomersHere)
                    Line(customers,(S.CityPerson(customer.PersonId)?.Name??customer.PersonId)+" · "+(customer.Started<0?"正在赴场，到场后收费":"使用中，剩余 "+System.Math.Max(0,customer.Until-S.Clock.Now.TotalMinutes)+" 分钟")+" · "+Money.FromCents(customer.PriceCents),17);
                if(state.CustomersHere.Length==0)Line(customers,"暂无顾客。开启接客、到营业时间并有人值守后，居民会按实际路线到场。",17,Ui.Dim);
                var actions=Ui.Row(w.Content,44,8);
                Ui.Size(Ui.Button(actions,"自己使用场地（免费）",()=>{w.Close();OpenCityVenue(place);},17,Ui.ButtonHot),-1,44,1).name="VenueOwnerUse";
                Ui.Size(Ui.Button(actions,"原有物品 / 搬运",()=>OpenVenueLegacyAssets(shop),17),-1,44,1);
                Ui.Size(Ui.Button(actions,"退租 / 结束自营",()=>ConfirmCloseShop(shop),17,Ui.Warn),-1,44,1);
                Ui.Size(Ui.Button(w.Content,"返回场地",w.Close,18),-1,42);
                Ui.Size(Ui.Button(w.Content,"返回街上",()=>{CloseAllWindows();S.Leave();},18),-1,42);
            });
        }
        private void OpenVenueLegacyAssets(OwnedShop shop)
        {
            OpenWindow("原有物品 · 保留并可转出",850,650,w=>
            {
                Para(w.Content,"以前放入的库存、后仓、家具和展示整机继续保留；它们不会在场馆内零售，也不会自动被覆盖。货物可用货车转出。",18);
                Line(w.Content,"原库存占用 "+shop.Sim.Stock.UsedSpace.ToString("0.#")+" 格 · 原装修 "+shop.Layout.Count+" 件",18,Ui.Accent);
                Ui.Size(Ui.Button(w.Content,"查看后仓与临时存放",OpenShopLockerWindow,18),-1,42);
                Ui.Size(Ui.Button(w.Content,"叫货车转移物品",OpenTruckWindow,18),-1,42);
                foreach(var tier in shop.Sim.DisplayPcs.ToArray())
                {
                    int value=tier;
                    Ui.Size(Ui.Button(w.Content,"拆回原展示整机（档位 "+tier+"）",()=>{shop.Sim.DisassembleDisplay(value);w.Rebuild();},17),-1,40);
                }
                foreach(var furniture in shop.Layout.Select((item,index)=>new{item,index}).ToArray())
                {
                    var f=furniture;
                    Ui.Size(Ui.Button(w.Content,"收回旧家具："+(FurnitureCatalog.Get(f.item.Id)?.Name??f.item.Id),()=>{S.RecoverVenueFurniture(shop.Place.Id,f.index);w.Rebuild();},17),-1,40);
                }
            });
        }
    }
}
