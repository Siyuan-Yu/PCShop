using System.IO;
using System.Linq;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void ResidentialAgencyFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/residential-agency");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            void Click(string name)=>_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.name==name).onClick.Invoke();
            void Capture(string name)
            {
                foreach(var c in _game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=_game.Cam;c.planeDistance=1;}
                Frame(0);_game.SnapCamera();Shot(name,1600,1000);
            }
            var s=_game.Session;var agency=s.City.GetPlace("agency");s.Clock.Restore(s.Clock.Today,600);s.Leave();s.ArriveAtNode(agency.NodeId);
            Check(s.Enter(agency),"营业时间实际进入城郊中介");
            _game.Ui.OnEnteredPlace(agency);Click("AgencyResidentialTab");
            Check(!_game.Ui.BusinessUnlocked,"住宅入口不依赖后期经营解锁");Capture("starter-residences");
            _game.Ui.CloseAllWindows();_game.StartCityLifePreset("brand_expo");s=_game.Session;s.DebugAddCash(500000);
            agency=s.AgencyFor(s.ResidencePlace(1));s.ArriveAtNode(agency.NodeId);Check(s.Enter(agency),"实际进入住宅所属中介");
            _game.Ui.OnEnteredPlace(agency);
            Click("AgencyResidentialTab");Click("ResidenceMap_home_tier1");Capture("community-residence");
            long cash=s.Ledger.Balance.Cents;Click("ResidenceBuyCash");Capture("purchase-confirmation");
            Check(s.HomeTier==0&&s.Ledger.Balance.Cents==cash,"看房与预览不搬家不扣钱");
            Click("ResidenceConfirm");Check(s.HomeTier==1&&s.HomePriceCents>0,"直接全款购入并搬入");Capture("owned-residence");
            Click("AgencyCommercialTab");Capture("commercial-version");
            Check(_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<AgencyStreetMap>()).Any(),"商铺版仍显示真实鸟瞰地图");
            Log("RESIDENTIAL AGENCY GRAPHICS COMPLETE");
        });
    }
}
