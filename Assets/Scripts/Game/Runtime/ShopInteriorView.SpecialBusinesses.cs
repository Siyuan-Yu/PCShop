using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class ShopInteriorView
    {
        public bool NearSpecialFurniture {get;private set;}
        public bool NearUsedBuyDesk {get;private set;}
        public Vector3 UsedBuyDeskWorld {get;private set;}
        public Vector3 SpecialFurnitureWorld {get;private set;}
        public string SpecialFurnitureName {get;private set;}
        private readonly Dictionary<string,GameObject> _specialFigures=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,GameObject> _specialGoods=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,NpcRoute> _diningCheckoutRoutes=new Dictionary<string,NpcRoute>();
        private void UpdateSpecialBusinessVisuals(GameSession session,float realDt)
        {
            var shop=session.CurrentShop;
            NearSpecialFurniture=false;
            NearUsedBuyDesk=false;
            if(!session.IsSpecialBusiness(shop)&&_specialFigures.Count==0&&_specialGoods.Count==0)return;
            if(session.IsSpecialBusiness(shop))
            {
                var owner=_owner.transform.localPosition;
                var nearest=shop.Layout.Where(p=>p.Id=="restaurant_stove"||p.Id=="restaurant_table"||p.Id.StartsWith("used_"))
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
            GameObject Person(string key,Vector3 target,bool keepPosition=false)
            {
                wanted.Add(key);
                if(!_specialFigures.TryGetValue(key,out var figure))
                {
                    figure=Prims.Figure(_root,"SpecialCustomer_"+key,new Color(.36f,.62f,.72f),new Color(.95f,.8f,.65f),.95f);
                    _specialFigures[key]=figure;
                    figure.transform.position = ClampIndoorNpc(_root.TransformPoint(target));
                }
                figure.SetActive(true);
                if(!keepPosition)figure.transform.position = ClampIndoorNpc(_root.TransformPoint(target));
                return figure;
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
                    foreach(var order in state.Orders.Where(o=>!o.TableCleared))
                        if(order.Table>=0&&order.Table<tables.Length)
                        {
                            var table=tables[order.Table];var position=new Vector3((float)table.X,0,(float)table.Z);
                            if(order.Stage!=DiningStage.Checkout&&order.Stage!=DiningStage.Completed&&order.Stage!=DiningStage.Left)
                                Person("dining:"+order.Id,position+new Vector3(0,0,-.7f));
                            if(order.Stage==DiningStage.Eating||order.Stage==DiningStage.Checkout||order.Stage==DiningStage.Completed
                                ||(order.Stage==DiningStage.Left&&order.EatUntil>0))
                                Good("meal:"+order.Id,position+Vector3.up*.95f,new Color(.9f,.7f,.25f));
                        }
                    if(_hasCounter)
                    {
                        float step=session.Time.IsRunning
                            ? OwnerWalkSpeed*(float)CityTravelTiming.NpcWalkSpeedFactor*realDt*session.Time.Speed : 0f;
                        int index=0;
                        foreach(var order in state.Orders.Where(o=>o.Stage==DiningStage.Checkout).OrderBy(o=>o.CheckoutAt).ThenBy(o=>o.Id))
                        {
                            string key="dining:"+order.Id;
                            Vector3 spot=QueueSpot(index++);
                            bool approaching=session.Clock.Now.TotalMinutes<order.CheckoutAt;
                            var table=order.Table>=0&&order.Table<tables.Length?tables[order.Table]:null;
                            Vector3 start=approaching&&table!=null?new Vector3((float)table.X,0,(float)table.Z-.7f):_root.InverseTransformPoint(spot);
                            var figure=Person(key,start,keepPosition:true);
                            if(!_diningCheckoutRoutes.TryGetValue(key,out var route))_diningCheckoutRoutes[key]=route=new NpcRoute();
                            if(step>0)MoveNpc(figure.transform,route,spot,step,true);
                        }
                    }
                    var stoves=shop.Layout.Where(p=>p.Id=="restaurant_stove").ToArray();
                    if (stoves.Length > 0)
                    {
                        var readyStoves = state.Orders
                            .Where(o => o.Stage == DiningStage.Ready && !state.Workers.Any(w => w.OrderId == o.Id && w.Carrying))
                            .Select(o => o.Stove >= 0 && o.Stove < stoves.Length ? o.Stove : 0)
                            .Distinct();
                        foreach (int index in readyStoves)
                        {
                            var stove = stoves[index];
                            Good("ready-stove:" + index, new Vector3((float)stove.X + .26f, 1.05f, (float)stove.Z), new Color(.9f,.7f,.25f));
                        }
                    }
                    foreach(var order in state.Orders.Where(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt))
                    {
                        if(order.Stove<0||order.Stove>=stoves.Length)continue;
                        var stove=stoves[order.Stove];
                        Color color=order.Stage==DiningStage.Burnt?Color.black:session.Clock.Now.TotalMinutes>=order.ReadyAt?new Color(.3f,.8f,.25f):new Color(.95f,.6f,.2f);
                        Good("fire:"+order.Id,new Vector3((float)stove.X,1.13f,(float)stove.Z),color);
                    }
                }
                else
                {
                    var desk=shop.Layout.FirstOrDefault(p=>p.Id=="used_buydesk");
                    var seller=state.Used.FirstOrDefault(i=>!i.Owned&&!i.Sold);
                    if(desk!=null)
                    {
                        var bounds = RoomPlanner.Footprint(session.ShopRoom,FurnitureCatalog.Get(desk.Id),desk);
                        Vector3 player = _owner.transform.localPosition;
                        var edge = new Vector2(Mathf.Clamp(player.x,(float)bounds.MinX,(float)bounds.MaxX),
                            Mathf.Clamp(player.z,(float)bounds.MinZ,(float)bounds.MaxZ));
                        NearUsedBuyDesk = Vector2.Distance(edge,new Vector2(player.x,player.z))<1.15f;
                        UsedBuyDeskWorld = _root.TransformPoint(new Vector3((float)desk.X,1.35f,(float)desk.Z));
                        if(seller!=null){Person("seller:"+seller.Id,new Vector3((float)desk.X,0,(float)desk.Z-.8f));Good("offer:"+seller.Id,new Vector3((float)desk.X,.95f,(float)desk.Z),new Color(.68f,.5f,.32f));}
                    }
                    var displays=shop.Layout.Where(p=>p.Id=="used_display").ToArray();int slot=0;
                    foreach(var item in state.Used.Where(i=>i.Owned&&!i.Listed&&!i.Sold))
                    {
                        if(slot/4>=displays.Length)break;var display=displays[slot/4];int offset=slot++%4;
                        Good("used:"+item.Id,new Vector3((float)display.X+(offset%2==0?-.22f:.22f),1.2f,(float)display.Z+(offset<2?-.16f:.16f)),item.Condition==UsedCondition.Broken?Color.gray:new Color(.7f,.55f,.35f));
                    }
                }
            }
            foreach(var key in _specialFigures.Keys.Where(k=>!wanted.Contains(k)).ToArray()){if(Application.isPlaying)Object.Destroy(_specialFigures[key]);else Object.DestroyImmediate(_specialFigures[key]);_specialFigures.Remove(key);_diningCheckoutRoutes.Remove(key);}
            foreach(var key in _specialGoods.Keys.Where(k=>!goods.Contains(k)).ToArray()){if(Application.isPlaying)Object.Destroy(_specialGoods[key]);else Object.DestroyImmediate(_specialGoods[key]);_specialGoods.Remove(key);}
        }
    }
}
