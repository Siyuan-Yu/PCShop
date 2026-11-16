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
        private PlacedFurniture _usedBuyDesk;
        private readonly List<Vector3> _usedSellerStands = new List<Vector3>();
        private readonly Dictionary<int, UsedSellerFigure> _usedSellers = new Dictionary<int, UsedSellerFigure>();
        private readonly List<int> _departedUsedSellers = new List<int>();
        private string _usedSellerShopId;

        private sealed class UsedSellerFigure
        {
            public GameObject Figure;
            public readonly NpcRoute Route = new NpcRoute();
            public bool Waiting;
        }

        private static Vector3 UsedDeskCenter(PlacedFurniture desk) => new Vector3((float)desk.X, 0, (float)desk.Z);

        private static Vector3 UsedDeskStand(PlacedFurniture desk, bool seller)
        {
            var front = Quaternion.Euler(0, 90f * desk.Rot, 0) * Vector3.back;
            float distance = (float)FurnitureCatalog.Get(desk.Id).Depth * .5f + .45f;
            return UsedDeskCenter(desk) + front * (seller ? distance : -distance);
        }

        private static void FaceUsedDesk(Transform person, PlacedFurniture desk)
        {
            var direction = UsedDeskCenter(desk) - person.localPosition;
            direction.y = 0;
            if (direction.sqrMagnitude > .01f) person.localRotation = Quaternion.LookRotation(direction);
        }

        private void RebuildUsedSellerStands()
        {
            _usedSellerStands.Clear();
            if (_usedBuyDesk == null) return;

            Vector3 front = UsedDeskStand(_usedBuyDesk, seller: true);
            Vector3 direction = (front - UsedDeskCenter(_usedBuyDesk)).normalized;
            var occupied = new List<Vector3>
            {
                UsedDeskStand(_usedBuyDesk, seller: false),
                new Vector3(_doorX, 0, .7f),
            };
            Vector3 preferred = front;
            for (int slot = 0; slot < GameSession.UsedSellerQueueLimit; slot++)
            {
                if (!TryFindIndoorStand(preferred, occupied, BodyRadius, out Vector3 stand)) break;
                _usedSellerStands.Add(stand);
                occupied.Add(stand);
                preferred = stand + direction * .95f;
                if (slot == 0 && _hasCounter)
                {
                    occupied.Add(QueueSpot(0) - Origin);
                    occupied.Add(QueueSpot(1) - Origin);
                }
            }
        }

        private UsedItem UpdateUsedSellerQueue(SpecialBusinessState state, float dt)
        {
            if (_usedSellerShopId != state.ShopId) ClearUsedSellerFigures();
            foreach (var actor in _usedSellers.Values) actor.Waiting = false;
            UsedItem atDesk = null;
            int slot = 0;
            foreach (var seller in state.Used)
            {
                if (!seller.WaitingForPurchase) continue;
                if (slot >= _usedSellerStands.Count) break;
                Vector3 stand = _root.TransformPoint(_usedSellerStands[slot]);
                if (!_usedSellers.TryGetValue(seller.Id, out var actor))
                {
                    var color = CrowdView.PersonClothing("used-seller-" + seller.SellerId);
                    var figure = Prims.Figure(_root, "SpecialCustomer_seller:" + seller.Id, color, new Color(.95f, .8f, .65f), .95f);
                    figure.transform.position = _usedSellerShopId != null ? _door : stand;
                    actor = new UsedSellerFigure { Figure = figure };
                    _usedSellers.Add(seller.Id, actor);
                }
                actor.Waiting = true;
                Transform person = actor.Figure.transform;
                if (MoveNpc(person, actor.Route, stand, dt * 3f, indoorGoal: true))
                    FaceUsedDesk(person, _usedBuyDesk);
                if (slot == 0 && (person.position - stand).sqrMagnitude < .1f)
                {
                    atDesk = seller;
                    _emotes.Add(new EmoteInfo { Pos = person.position + Vector3.up * 2.05f, Text = "等候收购",
                        Color = new Color(.12f, .13f, .16f), Size = 18, Bubble = true });
                }
                slot++;
            }
            _usedSellerShopId = state.ShopId;

            _departedUsedSellers.Clear();
            foreach (var entry in _usedSellers)
            {
                var actor = entry.Value;
                if (!actor.Waiting && MoveNpc(actor.Figure.transform, actor.Route, _door, dt * 3f, indoorGoal: false))
                {
                    Kill.It(actor.Figure);
                    _departedUsedSellers.Add(entry.Key);
                }
            }
            foreach (int id in _departedUsedSellers) _usedSellers.Remove(id);
            return atDesk;
        }

        private void ClearUsedSellerFigures()
        {
            foreach (var actor in _usedSellers.Values) Kill.It(actor.Figure);
            _usedSellers.Clear();
            _usedSellerShopId = null;
        }

        private readonly Dictionary<string,GameObject> _specialFigures=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,GameObject> _specialGoods=new Dictionary<string,GameObject>();
        public int NearbyDiningTable { get; private set; } = -1;
        private void UpdateSpecialBusinessVisuals(GameSession session, float dt)
        {
            var shop=session.CurrentShop;
            NearSpecialFurniture=false;
            NearUsedBuyDesk=false;
            NearbyDiningTable = -1;
            if (shop.RetailKind != "secondhand" || _usedBuyDesk == null) ClearUsedSellerFigures();
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
                    if (NearSpecialFurniture && nearest.Id == "restaurant_table")
                        NearbyDiningTable = shop.Layout.TakeWhile(p => p != nearest).Count(p => p.Id == "restaurant_table");
                    SpecialFurnitureWorld=_root.TransformPoint(position+Vector3.up);
                    SpecialFurnitureName=PCShopSell.Core.Home.FurnitureCatalog.Get(nearest.Id).Name;
                }
            }
            var wanted=new HashSet<string>();
            var goods=new HashSet<string>();
            Transform Person(string key,Vector3 target)
            {
                wanted.Add(key);
                if(!_specialFigures.TryGetValue(key,out var figure))
                {
                    figure=Prims.Figure(_root,"SpecialCustomer_"+key,new Color(.36f,.62f,.72f),new Color(.95f,.8f,.65f),.95f);
                    _specialFigures[key]=figure;
                }
                figure.SetActive(true);
                figure.transform.position = ClampIndoorNpc(_root.TransformPoint(target));
                return figure.transform;
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
                            if(order.Stage!=DiningStage.Completed&&order.Stage!=DiningStage.Left)
                                Person("dining:"+order.Id,position+new Vector3(0,0,-.7f));
                            if(order.Stage==DiningStage.Eating||order.Stage==DiningStage.Checkout||order.Stage==DiningStage.Completed
                                ||(order.Stage==DiningStage.Left&&order.EatUntil>0))
                                Good("meal:"+order.Id,position+Vector3.up*.95f,new Color(.9f,.7f,.25f));
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
                    var desk = _usedBuyDesk;
                    if(desk!=null)
                    {
                        var bounds = RoomPlanner.Footprint(session.ShopRoom,FurnitureCatalog.Get(desk.Id),desk);
                        Vector3 player = _owner.transform.localPosition;
                        var edge = new Vector2(Mathf.Clamp(player.x,(float)bounds.MinX,(float)bounds.MaxX),
                            Mathf.Clamp(player.z,(float)bounds.MinZ,(float)bounds.MaxZ));
                        NearUsedBuyDesk = Vector2.Distance(edge,new Vector2(player.x,player.z))<1.15f;
                        var center = UsedDeskCenter(desk);
                        UsedBuyDeskWorld = _root.TransformPoint(center + Vector3.up * 1.35f);
                        var seller = UpdateUsedSellerQueue(state, dt);
                        if (seller != null)
                            Good("offer:" + seller.Id, center + Vector3.up * .95f, new Color(.68f, .5f, .32f));
                    }
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
