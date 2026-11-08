using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class ShopInteriorView
    {
        private bool _restaurantPlayerWorking;
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
        private void ApplyRestaurantActor(GameSession session,RestaurantWorker worker,GameObject figure)
        {
            var pos=worker.Position(session.Clock.Now.TotalMinutes+session.Time.FractionalMinute);
            var target=ClampIndoorNpc(Origin+new Vector3((float)pos.x,0,(float)pos.z))-Origin;
            var delta=target-figure.transform.localPosition;
            figure.transform.localPosition=target;
            if(delta.sqrMagnitude>.00001f)figure.transform.localRotation=Quaternion.LookRotation(delta);
            else if(worker.Busy&&worker.OperateUntil>=0)
            {
                var station=session.CurrentShop.Layout.Find(p=>p.Id==worker.Station);
                if(station!=null)
                {
                    var face=new Vector3((float)station.X,0,(float)station.Z)-target;
                    if(face.sqrMagnitude>.001f)figure.transform.localRotation=Quaternion.LookRotation(face);
                }
            }
            var motion=figure.GetComponent<FigureMotion>();if(motion!=null)motion.ActivityKind=worker.Busy&&worker.OperateUntil>=0?"restaurant":null;
            RestaurantCarry(figure,worker.Carrying);
        }
        private void UpdateRestaurantPlayer(GameSession session)
        {
            var worker=session.RestaurantActor(session.CurrentShop.Place.Id,GameSession.RestaurantPlayer);
            if(worker!=null&&(worker.Busy||_restaurantPlayerWorking))ApplyRestaurantActor(session,worker,_owner);
            else
            {
                RestaurantCarry(_owner,false);
                if(_restaurantPlayerWorking)_owner.GetComponent<FigureMotion>().ActivityKind=null;
            }
            _restaurantPlayerWorking=worker?.Busy==true;
        }
    }
}
