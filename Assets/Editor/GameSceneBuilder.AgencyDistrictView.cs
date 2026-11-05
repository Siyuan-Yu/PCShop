using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void AgencyDistrictViewFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/agency-district-view");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.Time.SetPaused(true);s.Clock.Restore(s.Clock.Today,600);
            typeof(GameSession).GetField("_district",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new DistrictProgress{UnlockedTier=3,Chapter=3});
            void Capture(string name)
            {
                foreach(var c in _game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=_game.Cam;c.planeDistance=1;}
                Frame(0);_game.SnapCamera();Shot(name,1600,1000);
            }
            foreach(var agency in s.City.OfKind(PlaceKind.Agency))
            {
                _game.Ui.CloseAllWindows();s.Leave();s.ArriveAtNode(agency.NodeId);Check(s.Enter(agency),"进入中介 "+agency.Id);_game.Ui.OnEnteredPlace(agency);
                var view=_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<AgencyStreetMap>()).Single();
                var units=s.City.Places.Where(p=>s.AgencyServes(agency,p)&&AgencyStreetMap.IsDistrictOverviewPlace(p)).ToArray();
                foreach(var unit in units)
                {
                    var b=AgencyStreetMap.BuildingBounds(_game.World.Places[unit.Id]);var screen=view.Project(b.center);
                    Check(screen.x>=0&&screen.x<=view.Width&&screen.y>=0&&screen.y<=view.Height,"默认视角包含辖区商铺 "+unit.Id);
                }
                Capture(agency.Id);
                var target=units.First(p=>p.Kind==PlaceKind.Shop);
                var position=s.PlayerPosition;view.Focus(target);
                var at=view.Project(_game.World.Places[target.Id].Entrance);
                Check(Mathf.Abs(at.x/view.Width-.5f)<.06f&&Mathf.Abs(at.y/view.Height-.5f)<.12f,"边缘房源可放大居中 "+target.Id);
                Check(position.Equals(s.PlayerPosition)&&s.CurrentPlace==agency,"看房不移动玩家");
                if(agency.Id=="agency_s_services")Capture("community-property-closeup");
                view.ResetView();
            }
            Log("AGENCY DISTRICT VIEW GRAPHICS COMPLETE");
        });
    }
}
