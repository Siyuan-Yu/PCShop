using PCShopSell.Game.Runtime;
using UnityEngine;
namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private RaceTrackView _raceView;private bool _raceOverview;
        private bool UpdateRacing(float dt)
        {
            var race=Session.ActiveRace;if(race==null)return false;
            if(_raceView==null)
            {
                _raceView=World.Places[race.Track.Id].Root.GetComponentInChildren<RaceTrackView>();_raceView.Begin(race);_raceOverview=false;
                _rig.SetInterior(true);_rig.SetPlaceAngles(0,68);_rig.MinSize=10;_rig.MaxSize=150;Ui.BeginDrivingHud();
            }
            if(!Application.isFocused)race.Paused=true;
            if(Input.GetKeyDown(KeyCode.Escape))race.Paused=!race.Paused;
            if(Input.GetKeyDown(KeyCode.R)&&!race.Paused)race.Recover();
            if(Input.GetKeyDown(KeyCode.Tab))_raceOverview=!_raceOverview;
            float gas=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
            float turn=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            race.Step(dt,gas,turn,Input.GetKey(KeyCode.Space));_raceView.Sync(race);
            Vector3 focus=_raceView.transform.position+(_raceOverview?Vector3.zero:new Vector3((float)race.X,0,(float)race.Y));
            float size=_raceOverview?Mathf.Max((float)race.Track.Depth*.65f,(float)race.Track.Width/Cam.aspect*.6f):Mathf.Lerp(16,27,(float)(race.DisplayKph/250));
            _rig.Snap(focus,size);Ui.UpdateDrivingHud();
            if(race.Finished)
            {
                _raceView.End();_raceView=null;string report=Session.FinishRace();_autosaveDue=true;Ui.EndDrivingHud(report);SnapCamera();
            }
            return true;
        }
    }
}
