using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private QuarterlyExpoView _expoView;
        private string _expoFocus;
        private bool _expoWasOpen;
        private QuarterlyExpoView ExpoView
        {
            get
            {
                if(_expoView==null&&World!=null&&World.Places.TryGetValue("life_expo",out var place)&&place.VenueInterior!=null)
                    _expoView=place.VenueInterior.GetComponent<QuarterlyExpoView>();
                return _expoView;
            }
        }
        private void SyncExpoView(float dt)
        {
            var view=ExpoView;if(view==null)return;
            view.Sync(Session,dt);
            _avatar.PresentInExpo(view.Open?(Vector3?)view.PlayerPoint(view.FocusZone):null,
                view.transform.rotation*Quaternion.Euler(0,180,0),dt);
            if(view.Open!=_expoWasOpen||view.Open&&_expoFocus!=view.FocusZone)
            { _expoWasOpen=view.Open;_expoFocus=view.FocusZone;SnapCamera(true); }
        }
        public void FocusExpo(string zone)
        {
            var view=ExpoView;if(view?.Open!=true)return;
            view.Focus(zone);_expoFocus=view.FocusZone;SnapCamera(true);
        }
        private Vector3 ExpoCameraFocus(float size)
        {
            return ExpoView.WorldPoint(ExpoView.FocusZone)+Vector3.up+CameraRig.Rotation*Vector3.right*(size*Cam.aspect*.32f);
        }
        private void HandleExpoPointer()
        {
            if(GameUi.PointerOverUi||!Input.GetMouseButtonDown(0)||ExpoView?.Open!=true)return;
            string zone=ExpoView.Pick(Cam.ScreenPointToRay(Input.mousePosition));if(zone!=null)FocusExpo(zone);
        }
    }
}
