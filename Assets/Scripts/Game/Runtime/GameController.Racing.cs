using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private const float RaceDefaultPitch=42f;
        private const float RaceFollowDistance=24f, RaceFastFollowDistance=30f;
        private RaceTrackView _raceView;
        private bool _raceOverview, _raceOrbiting, _raceWasOrthographic, _raceNavigationEvents;
        private float _raceCameraYaw, _raceCameraPitch, _raceCameraOffset, _raceZoom, _raceOldFov;
        private Vector3 _raceCameraFocus;
        private bool UpdateRacing(float dt)
        {
            var race=Session.ActiveRace;if(race==null)return false;
            if(Session.Time.Speed!=1)Session.Time.SetSpeed(1);
            bool entering=_raceView==null;
            if(entering)
            {
                _raceView=World.Places[race.Track.Id].Root.GetComponentInChildren<RaceTrackView>();
                _raceView.Begin(race);_raceOverview=false;
                _raceCameraYaw=_raceView.transform.eulerAngles.y+(float)race.Heading*Mathf.Rad2Deg;
                _raceCameraPitch=RaceDefaultPitch;_raceCameraOffset=0;_raceZoom=1;_raceOrbiting=false;
                _raceWasOrthographic=Cam.orthographic;_raceOldFov=Cam.fieldOfView;
                // Space belongs to driving pause, not the UI Submit binding on a focused button.
                if(EventSystem.current!=null)
                {
                    _raceNavigationEvents=EventSystem.current.sendNavigationEvents;
                    EventSystem.current.sendNavigationEvents=false;
                    EventSystem.current.SetSelectedGameObject(null);
                }
                _rig.SetInterior(true);Ui.BeginDrivingHud();
            }
            if(!Application.isFocused)race.Paused=true;
            else if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Escape))race.Paused=!race.Paused;
            if(Input.GetKeyDown(KeyCode.R)&&!race.Paused)race.Recover();
            if(Input.GetKeyDown(KeyCode.Tab))_raceOverview=!_raceOverview;
            if(Input.GetMouseButtonDown(1)&&!GameUi.PointerOverUi)_raceOrbiting=true;
            if(!Input.GetMouseButton(1))_raceOrbiting=false;
            if(_raceOrbiting)
            {
                _raceCameraOffset=Mathf.DeltaAngle(0,_raceCameraOffset+Input.GetAxis("Mouse X")*4f);
                _raceCameraPitch=Mathf.Clamp(_raceCameraPitch-Input.GetAxis("Mouse Y")*3f,12f,70f);
            }
            if(!GameUi.PointerOverUi)
                _raceZoom=Mathf.Clamp(_raceZoom*(1-Input.mouseScrollDelta.y*.1f),.65f,2f);
            if(Input.GetKeyDown(KeyCode.V)){_raceCameraOffset=0;_raceCameraPitch=RaceDefaultPitch;_raceZoom=1;}
            float gas=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
            float turn=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            race.Step(dt,gas,turn,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
            _raceView.Sync(race);
            float blend=1-Mathf.Exp(-Mathf.Min(dt,.2f)*6);
            float heading=_raceView.transform.eulerAngles.y+(float)race.Heading*Mathf.Rad2Deg;
            // Manual orbit is an offset from the chase direction, retained as the car turns.
            _raceCameraYaw=Mathf.LerpAngle(_raceCameraYaw,heading+_raceCameraOffset,_raceOrbiting?1:blend);
            var forward=Quaternion.Euler(0,heading,0)*Vector3.forward;
            var position=_raceView.transform.TransformPoint(new Vector3((float)race.X,.23f,(float)race.Y));
            float speed=Mathf.Clamp01((float)race.DisplayKph/250f);
            if(_raceOverview)
            {
                Cam.orthographic=true;
                _rig.SetPlaceAngles(_raceCameraYaw,60);
                float angle=(_raceCameraYaw-_raceView.transform.eulerAngles.y)*Mathf.Deg2Rad;
                float width=Mathf.Abs(Mathf.Cos(angle))*(float)race.Track.Width+Mathf.Abs(Mathf.Sin(angle))*(float)race.Track.Depth;
                float depth=Mathf.Abs(Mathf.Sin(angle))*(float)race.Track.Width+Mathf.Abs(Mathf.Cos(angle))*(float)race.Track.Depth;
                float size=Mathf.Max(width/(2*Mathf.Max(.1f,Cam.aspect)),depth*Mathf.Sin(60*Mathf.Deg2Rad)*.5f)+8;
                _rig.Snap(_raceView.transform.position,size*_raceZoom);
            }
            else
            {
                Cam.orthographic=false;Cam.fieldOfView=60;
                var rotation=Quaternion.Euler(_raceCameraPitch,_raceCameraYaw,0);
                Vector3 focus=position+Vector3.up*1.15f+forward*Mathf.Lerp(4f,6f,speed)+rotation*Vector3.right*.65f;
                _raceCameraFocus=entering?focus:Vector3.Lerp(_raceCameraFocus,focus,blend);
                Cam.transform.SetPositionAndRotation(_raceCameraFocus-rotation*Vector3.forward*(Mathf.Lerp(RaceFollowDistance,RaceFastFollowDistance,speed)*_raceZoom),rotation);
            }
            Ui.UpdateDrivingHud();
            if(race.Finished)
            {
                _raceView.End();_raceView=null;
                Cam.orthographic=_raceWasOrthographic;Cam.fieldOfView=_raceOldFov;
                if(EventSystem.current!=null)EventSystem.current.sendNavigationEvents=_raceNavigationEvents;
                string report=Session.FinishRace();_autosaveDue=true;Ui.EndDrivingHud(report);SnapCamera();
            }
            return true;
        }
    }
}
