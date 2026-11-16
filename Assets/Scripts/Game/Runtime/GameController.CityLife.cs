using System;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.Common;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private string _cityExitIntent;
        private SaveData _cityRealProgress;
        private int _cityRealSaveSlot;
        private string _cityRealCampaignId;

        private void RememberRealProgress()
        {
            if (Session == null || Session.IsCityLifeTest) return;
            _cityRealProgress = Session.CreateSave();
            _cityRealSaveSlot = ActiveSaveSlot;
            _cityRealCampaignId = _saveCampaignId;
        }
        private void OnCityPlaceEntered(PCShopSell.Core.World.Place place, string activityId)
        {
            if (place == null || Session.CurrentPlace != place) return;
            SoundFx.Play(Sfx.Arrival);
            Ui.OnEnteredPlace(place, activityId);
        }
        private void OnCityTravelRequested(CityTravelIntent intent)
        {
            if(intent==null)return;
            Session.CancelPlayerRestaurantWork();
            _pendingEnter=null;CancelHomeBedTrip();Ui.CloseDock();
            if(Session.CurrentPlace?.Id==intent.TargetPlaceId){Session.ActivateCityTravel(intent.Id);return;}
            if(InRoom)
            {
                Session.Time.SetPaused(false);
                _cityExitIntent=intent.Id;
                Interior?.CancelCityDoorWalk();ActiveResidence?.CancelBedWalk();
                Ui.Toast("先走到门口，再沿城市路网前往；WASD可接管。 ");
            }
            else Session.ActivateCityTravel(intent.Id);
        }
        private void UpdateCityTravel(float dt)
        {
            if(_cityExitIntent==null)return;
            if(Session.CityIntent==null||Session.CityIntent.Id!=_cityExitIntent||Session.IsGameOver||Session.IsWorking)
            {_cityExitIntent=null;Interior?.CancelCityDoorWalk();ActiveResidence?.CancelBedWalk();return;}
            if(!Session.Time.IsRunning||Ui.AnyModalOpen)return;
            float walkDt=IndoorWalkDelta(dt);
            int result=InInterior?Interior.WalkToDoor(walkDt):InHome||InVisit?ActiveResidence.WalkToDoor(walkDt):1;
            if(result==0)return;
            string intent=_cityExitIntent;_cityExitIntent=null;
            if(result<0){Session.CancelCityTravel();Ui.Toast("出门路线被家具挡住了，请清出通道后重新前往。");return;}
            if(InInterior)Interior.StepBackInside();
            if(Session.CurrentPlace==Session.Home)Session.PrepareCityClothingDeparture(Session.CityOutdoorTemperature(Session.Clock.Now.TotalMinutes));
            Session.Leave();
            if (Session.OpeningAgencyGuidePending) Session.CancelCityTravel();
            else Session.ActivateCityTravel(intent);
        }
        public void StartCityLifePreset(string scenario)
        {
            if(Session==null||!Session.CanSave){Ui.Toast("请先处理当前事件，再进入独立测试档。");return;}
            RememberRealProgress();
            var next=GameSession.CreateCityLifePreset(City,scenario);
            StartSession(next);Ui.CloseAllWindows();
            Ui.Toast("城市生活测试档："+scenario+"。保存使用独立测试槽，可从验收面板返回原进度。");
        }
        public void LoadCityLifeTestPreset()
        {
            if(Session!=null&&!Session.CanSave){Ui.Toast("请先处理当前事件。");return;}
            var next=SaveStore.LoadCityLifeTest(City);
            if(next==null){Ui.Toast("没有可读取的测试档，原文件保留。");return;}
            RememberRealProgress();
            StartSession(next);Ui.CloseAllWindows();Ui.Toast("已读取独立城市生活测试档。");
        }
        public void ReturnFromCityLifePreset()
        {
            if(_cityRealProgress==null){Ui.Toast("没有本次会话的原进度快照，请从主菜单加载正式存档。");return;}
            var data=_cityRealProgress;_cityRealProgress=null;StartSession(GameSession.Load(City,data),_cityRealSaveSlot,_cityRealCampaignId);Ui.CloseAllWindows();
            Ui.Toast("已返回进入测试档前的原进度。");
        }
    }
}
