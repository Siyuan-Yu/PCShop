using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _parkSlot, _parkMoveFrom=-1;
        private void BuildPark(Window w)
        {
            if(!S.HasPark)
            {
                AgencyLink(w);
                Para(w.Content,"将一处大型空置物业改造成电脑乐园：买下产权，另付 ¥50,000 公共区改造费，7 天后开放 2×3 铺位。不同业态上下左右相邻时各提供 8% 导流，上限 24%；同业不会重复导流。",17);
                bool any=false;
                foreach(var listing in S.Listings.Where(x=>x.Size==ShopSize.Large))
                {
                    any=true;var l=listing;var c=BusinessCard(w.Content,S.City.GetPlace(l.PlaceId).Name+" · 园区候选地");
                    Line(c,$"物业价格 {S.ParkLandPrice(l)} + 公共区改造 ¥50,000",17);
                    string why=S.ParkPurchaseBlocker(l);if(why!=null)Para(c,why,16,Ui.Dim);
                    Ui.Size(Ui.Button(c,"购入并开始改造…",()=>ConfirmBusiness("建设电脑乐园",$"支付 {S.ParkLandPrice(l)+Money.FromYuan(50000)}，7 天后可招商。园区每月维护费 {S.ParkLandPrice(l).Scale(.001)}，各铺位另需装修和周转金。",()=>{S.BuyPark(l.PlaceId);w.Rebuild();}),18,Ui.ButtonHot,S.InAgency && why==null),-1,42);
                }
                if(!any)Para(w.Content,"当前没有大型铺面挂牌。保留资金，等待中介的新房源。",17,Ui.Dim);
                return;
            }
            bool ready=S.Clock.Today.TotalDays>=S.ParkReadyDay;
            Line(w.Content,"电脑乐园 · "+S.City.GetPlace(S.ParkPlaceId).Name+(ready ? "  [已开放]" : $"  [还有 {S.ParkReadyDay-S.Clock.Today.TotalDays} 天完工]"),20,Ui.Accent);
            Para(w.Content,"点击铺位选择。相邻的不同营业业态各导流 8%，最多 24%；停业不再导流。经营数据与现金在“分店经营”管理，整体物业在“物业与住处”管理。",16,Ui.Dim);
            for(int r=0;r<2;r++)
            {
                var row=Ui.Row(w.Content,165,10);
                for(int col=0;col<3;col++)
                {
                    int slot=r*3+col;var v=S.ParkUnit(slot);
                    var card=BusinessCard(row,(slot+1)+" 号铺"+(_parkSlot==slot ? " · 已选择" : ""));Ui.Size(card,342,-1);
                    Line(card,v==null ? "空铺位" : Ventures.Get(v.Kind).Name+(v.Paused ? " · 停业" : ""),17);
                    Line(card,v==null ? "等待招商" : $"邻铺导流 +{(S.ParkTrafficBonus(v)-1)*100:0}%",15,Ui.Good);
                    Ui.Size(Ui.Button(card,"选择铺位",()=>{_parkSlot=slot;w.Rebuild();},16,slot==_parkSlot ? Ui.ButtonHot : Ui.ButtonBg),-1,32);
                }
            }
            var selected=S.ParkUnit(_parkSlot);
            if(_parkMoveFrom>=0)
            {
                var row=Ui.Row(w.Content,40);
                BusinessAction(row,$"将 {_parkMoveFrom+1} 号铺搬到 {_parkSlot+1} 号铺 / 互换 · ¥1,000",()=>{bool ok=S.MoveParkUnit(_parkMoveFrom,_parkSlot);if(ok)_parkMoveFrom=-1;return ok;},w,ready && _parkMoveFrom!=_parkSlot && S.Ledger.Balance>=Money.FromYuan(1000));
                Ui.Size(Ui.Button(row,"取消搬迁",()=>{_parkMoveFrom=-1;w.Rebuild();},17),180);
            }
            else if(selected!=null)
            {
                var row=Ui.Row(w.Content,40);
                Ui.Size(Ui.Button(row,"调整布局：选择搬迁目的地",()=>{_parkMoveFrom=_parkSlot;w.Rebuild();},17),-1,-1,1);
                Ui.Size(Ui.Button(row,"管理门店收支",()=>{_businessTab=3;w.Rebuild();},17,Ui.ButtonHot),-1,-1,1);
            }
            else
            {
                var d=Ventures.All[_ventureKind];var row=Ui.Row(w.Content,40);
                Ui.Size(Ui.Button(row,"上一业态",()=>{_ventureKind=(_ventureKind+Ventures.All.Length-1)%Ventures.All.Length;w.Rebuild();},17),160);
                Ui.Size(Ui.Label(row,d.Name,20,TextAnchor.MiddleCenter,Ui.Accent),600);
                Ui.Size(Ui.Button(row,"下一业态",()=>{_ventureKind=(_ventureKind+1)%Ventures.All.Length;w.Rebuild();},17),160);
                Para(w.Content,d.Description+$" 入驻现金 {Money.FromYuan(d.Capital+5000)}（含 ¥5,000 周转金），无需另付店租。",16);
                string why=S.ParkUnitBlocker(_parkSlot,d.Id);if(why!=null)Line(w.Content,why,16,Ui.Dim);
                int target=_parkSlot;
                Ui.Size(Ui.Button(w.Content,$"让{d.Name}入驻 {target+1} 号铺…",()=>ConfirmBusiness("园区招商",$"支付 {Money.FromYuan(d.Capital+5000)}。"+(d.HardwareCount>0 ? $"另调拨 {d.HardwareCount} 套硬件。" : "")+"店长从次日开始结算。",()=>{S.OpenParkUnit(target,d.Id);w.Rebuild();}),17,Ui.ButtonHot,why==null),-1,40);
            }
        }
    }
}
