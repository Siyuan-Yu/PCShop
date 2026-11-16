using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class ShopInteriorView
    {
        private bool _restaurantPlayerWorking;

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
