using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class ShopInteriorView
    {
        private bool _restaurantPlayerWorking;
        private OwnedShop _restaurantStockShop;
        private float _restaurantStockRefreshAt;
        private string _restaurantStockWarning;

        private void UpdateRestaurantStockWarning(GameSession session)
        {
            var shop = session.CurrentShop;
            if (!session.InPlayerShop || shop?.RetailKind != "restaurant") return;

            if (_restaurantStockShop != shop || _now >= _restaurantStockRefreshAt)
            {
                _restaurantStockShop = shop;
                _restaurantStockRefreshAt = _now + .25f;
                _restaurantStockWarning = null;
                if (!session.CanOpenFridge)
                    _restaurantStockWarning = "<b>未摆放冰箱</b>\n<size=14>摆好冰箱后才能备料</size>";
                else
                {
                    var missing = session.RestaurantMissingIngredients(shop, session.SpecialBusiness(shop.Place.Id));
                    if (missing.Count > 0)
                    {
                        var names = string.Join("、", missing.Take(3).Select(item => item.Name));
                        if (missing.Count > 3) names += $"等 {missing.Count} 种";
                        string supply = missing.Any(item => item.InWarehouse) ? "后仓有货，可移入冰箱" : "请补充食材";
                        _restaurantStockWarning = $"<b>缺食材 · {names}</b>\n<size=14>{supply}</size>";
                    }
                    else if (shop.Fridge.WeightKg <= 1e-9)
                        _restaurantStockWarning = "<b>冰箱空了</b>\n<size=14>请补充食材</size>";
                }
            }
            if (_restaurantStockWarning == null) return;

            Vector3 position;
            if (NearFridge)
                position = NearFridgeWorld + Vector3.up * .8f;
            else
            {
                var station = shop.Layout.FirstOrDefault(item => item.Id == "restaurant_stove")
                    ?? shop.Layout.FirstOrDefault(item => FurnitureCatalog.Get(item.Id)?.Role == FurnitureRole.Fridge);
                if (station == null) return;
                position = Origin + new Vector3((float)station.X, 2.6f, (float)station.Z);
            }
            _emotes.Add(new EmoteInfo
            {
                Pos = position,
                Text = _restaurantStockWarning,
                Size = 17,
                Color = new Color(.61f, .27f, .20f),
                Bubble = true
            });
        }

        private static void UpdateRestaurantSounds(SpecialBusinessState state)
        {
            if (state.Orders.Any(order => order.Stage == DiningStage.Cooking)
                || state.Workers.Any(worker => worker.Job == RestaurantJob.Cook && worker.OperateUntil >= 0))
                SoundFx.Play(Sfx.Cooking);
            if (state.Orders.Any(order => !order.IsFishOrder && order.Stage == DiningStage.Eating))
                SoundFx.Play(Sfx.Dining);
        }

        private static void RestaurantCarry(GameObject figure,bool carrying)
        {
            var dish=figure.transform.Find("RestaurantCarriedDish");
            if(dish==null&&carrying)
            {
                dish=Prims.Shape(PrimitiveType.Cylinder,figure.transform,"RestaurantCarriedDish",new Vector3(0,1.05f,.37f),new Vector3(.32f,.035f,.32f),Mats.Lit(ArtDirection.Cream)).transform;
                Prims.Shape(PrimitiveType.Sphere,dish,"Food",new Vector3(0,.7f,0),new Vector3(.7f,1.3f,.7f),Mats.Lit(new Color(.85f,.66f,.25f)));
            }
            if(dish!=null)dish.gameObject.SetActive(carrying);
        }
        private Vector3 RestaurantActorPosition(GameSession session, RestaurantWorker worker)
        {
            var pos=worker.Position(session.Clock.Now.TotalMinutes+session.Time.FractionalMinute);
            return ClampIndoorNpc(Origin+new Vector3((float)pos.x,0,(float)pos.z))-Origin;
        }

        private void ApplyRestaurantActor(GameSession session,RestaurantWorker worker,GameObject figure,List<Vector3> occupied = null)
        {
            var target = RestaurantActorPosition(session, worker);
            if (occupied != null)
            {
                const float spacing = (float)GameSession.RestaurantStandSpacing;
                if (occupied.Any(p => (p - target).sqrMagnitude < spacing * spacing)
                    && TryFindIndoorStand(target, occupied, .2f, out var nearby, 1.3f)) target = nearby;
                occupied.Add(target);
            }
            var delta=target-figure.transform.localPosition;
            figure.transform.localPosition=target;
            bool operating = worker.Busy && worker.OperateUntil >= 0;
            if (operating)
            {
                var station=session.RestaurantWorkStation(session.CurrentShop,worker);
                if(station!=null)
                {
                    var face=new Vector3((float)station.X,0,(float)station.Z)-target;
                    if(face.sqrMagnitude>.001f)figure.transform.localRotation=Quaternion.LookRotation(face);
                }
            }
            else if (worker.Busy && delta.sqrMagnitude > .00001f)
                figure.transform.localRotation = Quaternion.LookRotation(delta);

            var motion = figure.GetComponent<FigureMotion>();
            motion.WorkAction = !operating ? FigureWorkAction.None : worker.Job switch
            {
                RestaurantJob.Cook => FigureWorkAction.Cook,
                RestaurantJob.Plate => FigureWorkAction.Plate,
                RestaurantJob.Serve => worker.Carrying ? FigureWorkAction.Serve : FigureWorkAction.Plate,
                RestaurantJob.Clear => FigureWorkAction.Clear,
                RestaurantJob.Deliver => FigureWorkAction.Serve,
                _ => FigureWorkAction.None
            };
            RestaurantCarry(figure,worker.Carrying);
        }
        private void UpdateRestaurantPlayer(GameSession session)
        {
            var worker=session.RestaurantActor(session.CurrentShop.Place.Id,GameSession.RestaurantPlayer);
            if(worker!=null&&(worker.Busy||_restaurantPlayerWorking))ApplyRestaurantActor(session,worker,_owner);
            else
            {
                RestaurantCarry(_owner,false);
                if(_restaurantPlayerWorking)_owner.GetComponent<FigureMotion>().WorkAction=FigureWorkAction.None;
            }
            _restaurantPlayerWorking=worker?.Busy==true;
        }
    }
}
