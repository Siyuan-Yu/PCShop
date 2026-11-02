using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class ShopInteriorView
    {
        public bool NearSpecialFurniture {get;private set;}
        public Vector3 SpecialFurnitureWorld {get;private set;}
        public string SpecialFurnitureName {get;private set;}
        private readonly Dictionary<string,GameObject> _specialFigures=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,GameObject> _specialGoods=new Dictionary<string,GameObject>();
        private void UpdateSpecialBusinessVisuals(GameSession session)
        {
            var shop=session.CurrentShop;
            NearSpecialFurniture=false;
            if(!session.IsSpecialBusiness(shop)&&_specialFigures.Count==0&&_specialGoods.Count==0)return;
            if(session.IsSpecialBusiness(shop))
            {
                var owner=_owner.transform.localPosition;
                var nearest=shop.Layout.Where(p=>p.Id.StartsWith("restaurant_")||p.Id.StartsWith("used_"))
                    .OrderBy(p=>Vector3.Distance(owner,new Vector3((float)p.X,0,(float)p.Z))).FirstOrDefault();
                if(nearest!=null)
                {
                    var position=new Vector3((float)nearest.X,0,(float)nearest.Z);
                    NearSpecialFurniture=Vector3.Distance(owner,position)<1.6f;
                    SpecialFurnitureWorld=_root.TransformPoint(position+Vector3.up);
                    SpecialFurnitureName=PCShopSell.Core.Home.FurnitureCatalog.Get(nearest.Id).Name;
                }
            }
            var wanted=new HashSet<string>();
            var goods=new HashSet<string>();
            void Person(string key,Vector3 target)
            {
                wanted.Add(key);
                if(!_specialFigures.TryGetValue(key,out var figure))
                {
                    figure=Prims.Figure(_root,"SpecialCustomer_"+key,new Color(.36f,.62f,.72f),new Color(.95f,.8f,.65f),.95f);
                    _specialFigures[key]=figure;
                }
                figure.SetActive(true);figure.transform.localPosition=target;
            }
            void Good(string key,Vector3 target,Color color)
            {
                goods.Add(key);
                if(!_specialGoods.TryGetValue(key,out var model))
                {
                    model=Prims.Box(_root,"SpecialGoods_"+key,target,new Vector3(.18f,.12f,.18f),Mats.Lit(color));_specialGoods[key]=model;
                }
                model.SetActive(true);model.transform.localPosition=target;
                var renderer=model.GetComponent<Renderer>();if(renderer!=null)renderer.sharedMaterial=Mats.Lit(color);
            }
            if(session.IsSpecialBusiness(shop))
            {
                var state=session.SpecialBusiness(shop.Place.Id);
                if(shop.RetailKind=="restaurant")
                {
                    var tables=shop.Layout.Where(p=>p.Id=="restaurant_table").ToArray();
                    foreach(var order in state.Orders.Where(o=>o.Stage!=DiningStage.Completed&&o.Stage!=DiningStage.Left))
                        if(order.Table>=0&&order.Table<tables.Length)
                        {
                            var table=tables[order.Table];var position=new Vector3((float)table.X,0,(float)table.Z);
                            Person("dining:"+order.Id,position+new Vector3(0,0,-.7f));
                            if(order.Stage==DiningStage.Eating)Good("meal:"+order.Id,position+Vector3.up*.95f,new Color(.9f,.7f,.25f));
                        }
                    var pass=shop.Layout.FirstOrDefault(p=>p.Id=="restaurant_pass");
                    if(pass!=null)foreach(var order in state.Orders.Where(o=>o.Stage==DiningStage.Ready&&!state.Workers.Any(w=>w.Busy&&w.OrderId==o.Id&&w.Job==RestaurantJob.Serve&&w.Carrying)))
                        Good("pass:"+order.Id,new Vector3((float)pass.X,.95f,(float)pass.Z),new Color(.9f,.7f,.25f));
                    var stoves=shop.Layout.Where(p=>p.Id=="restaurant_stove").ToArray();
                    foreach(var order in state.Orders.Where(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt))
                    {
                        if(order.Stove<0||order.Stove>=stoves.Length||state.Workers.Any(w=>w.Busy&&w.OrderId==order.Id&&w.Job==RestaurantJob.Plate&&w.Carrying))continue;
                        var stove=stoves[order.Stove];
                        Color color=order.Stage==DiningStage.Burnt?Color.black:session.Clock.Now.TotalMinutes>=order.ReadyAt?new Color(.3f,.8f,.25f):new Color(.95f,.6f,.2f);
                        Good("fire:"+order.Id,new Vector3((float)stove.X,1.13f,(float)stove.Z),color);
                    }
                }
                else
                {
                    var desk=shop.Layout.FirstOrDefault(p=>p.Id=="used_buydesk");
                    var seller=state.Used.FirstOrDefault(i=>!i.Owned&&!i.Sold);
                    if(desk!=null&&seller!=null){Person("seller:"+seller.Id,new Vector3((float)desk.X,0,(float)desk.Z-.8f));Good("offer:"+seller.Id,new Vector3((float)desk.X,.95f,(float)desk.Z),new Color(.68f,.5f,.32f));}
                    var displays=shop.Layout.Where(p=>p.Id=="used_display").ToArray();int slot=0;
                    foreach(var item in state.Used.Where(i=>i.Owned&&!i.Listed&&!i.Sold))
                    {
                        if(slot/4>=displays.Length)break;var display=displays[slot/4];int offset=slot++%4;
                        Good("used:"+item.Id,new Vector3((float)display.X+(offset%2==0?-.22f:.22f),1.2f,(float)display.Z+(offset<2?-.16f:.16f)),item.Condition==UsedCondition.Broken?Color.gray:new Color(.7f,.55f,.35f));
                    }
                }
            }
            foreach(var key in _specialFigures.Keys.Where(k=>!wanted.Contains(k)).ToArray()){if(Application.isPlaying)Object.Destroy(_specialFigures[key]);else Object.DestroyImmediate(_specialFigures[key]);_specialFigures.Remove(key);}
            foreach(var key in _specialGoods.Keys.Where(k=>!goods.Contains(k)).ToArray()){if(Application.isPlaying)Object.Destroy(_specialGoods[key]);else Object.DestroyImmediate(_specialGoods[key]);_specialGoods.Remove(key);}
        }
    }
}
