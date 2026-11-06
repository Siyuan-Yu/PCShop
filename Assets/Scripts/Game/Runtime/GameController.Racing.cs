using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;
namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private RaceTrackView _raceView;private bool _raceOverview;
        private float _raceCameraSize;
        private float _raceCameraYaw,_raceCameraPitch;
        private bool _raceOrbiting;
        private bool UpdateRacing(float dt)
        {
            var race=Session.ActiveRace;if(race==null)return false;
            // Driving uses real seconds; city speed shortcuts and UI cannot accelerate it.
            if(Session.Time.Speed!=1)Session.Time.SetSpeed(1);
            if(_raceView==null)
            {
                _raceView=World.Places[race.Track.Id].Root.GetComponentInChildren<RaceTrackView>();_raceView.Begin(race);_raceOverview=false;
                _raceCameraSize=16f;
                _raceCameraYaw=_raceView.transform.eulerAngles.y+(float)race.Heading*Mathf.Rad2Deg;
                _raceCameraPitch=56f;_raceOrbiting=false;
                _rig.SetInterior(true);_rig.MinSize=10;_rig.MaxSize=150;Ui.BeginDrivingHud();
            }
            if(!Application.isFocused)race.Paused=true;
            if(Input.GetKeyDown(KeyCode.Escape))race.Paused=!race.Paused;
            if(Input.GetKeyDown(KeyCode.R)&&!race.Paused)race.Recover();
            if(Input.GetKeyDown(KeyCode.Tab))_raceOverview=!_raceOverview;
            if(Input.GetMouseButtonDown(1)&&!GameUi.PointerOverUi)_raceOrbiting=true;
            if(!Input.GetMouseButton(1))_raceOrbiting=false;
            if(_raceOrbiting)
            {
                _raceCameraYaw=Mathf.Repeat(_raceCameraYaw+Input.GetAxis("Mouse X")*4f,360f);
                _raceCameraPitch=Mathf.Clamp(_raceCameraPitch-Input.GetAxis("Mouse Y")*3f,CameraRig.MinPitch,CameraRig.MaxPitch);
            }
            float gas=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
            float turn=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            race.Step(dt,gas,turn,Input.GetKey(KeyCode.Space));_raceView.Sync(race);
            var forward=Quaternion.Euler(0,_raceCameraYaw,0)*Vector3.forward;
            var position=_raceView.transform.TransformPoint(new Vector3((float)race.X,.23f,(float)race.Y));
            // Steering changes only the vehicle; the mouse owns camera yaw and pitch.
            _rig.SetPlaceAngles(_raceCameraYaw,_raceCameraPitch);
            float speed=Mathf.Clamp01((float)race.DisplayKph/250f);
            Vector3 focus=_raceOverview?_raceView.transform.position:position+forward*Mathf.Lerp(5,10,speed);
            float angle=(_raceCameraYaw-_raceView.transform.eulerAngles.y)*Mathf.Deg2Rad;
            float width=Mathf.Abs(Mathf.Cos(angle))*(float)race.Track.Width+Mathf.Abs(Mathf.Sin(angle))*(float)race.Track.Depth;
            float depth=Mathf.Abs(Mathf.Sin(angle))*(float)race.Track.Width+Mathf.Abs(Mathf.Cos(angle))*(float)race.Track.Depth;
            float targetSize=_raceOverview?Mathf.Max(width/(2*Mathf.Max(.1f,Cam.aspect)),depth*Mathf.Sin(_raceCameraPitch*Mathf.Deg2Rad)*.5f)+8:Mathf.Lerp(16,27,speed);
            _raceCameraSize=_raceOverview?targetSize:Mathf.Lerp(_raceCameraSize,targetSize,1-Mathf.Exp(-Mathf.Min(dt,.2f)*5));
            _rig.Snap(focus,_raceCameraSize);Ui.UpdateDrivingHud();
            if(race.Finished)
            {
                _raceView.End();_raceView=null;string report=Session.FinishRace();_autosaveDue=true;Ui.EndDrivingHud(report);SnapCamera();
            }
            return true;
        }
    }
}
