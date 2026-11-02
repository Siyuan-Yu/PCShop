using PCShopSell.Core.Session;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        public bool RequestRestaurantWork(int orderId,RestaurantJob job)
        {
            if(!InInterior||Ui.AnyModalOpen||Placing)return false;
            var at=Interior.OwnerWorld-Interior.Root.position;
            bool result=Session.RequestRestaurantWork(orderId,job,at.x,at.z);
            if(result)
            {
                Session.CancelCityTravel();_cityExitIntent=null;Interior.CancelCityDoorWalk();CancelHomeBedTrip();
                Ui.Toast("已安排"+GameSession.RestaurantJobName(job)+"，角色会走到对应位置操作；WASD可接管。");
            }
            return result;
        }
    }
}
