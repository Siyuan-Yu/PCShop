using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.Common;
using PCShopSell.Core.World;
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
        private bool _communityRevealPending, _communityRevealActive;
        private const float CommunityDeparture = .8f, CommunityOverviewArrival = 5.8f;
        private const float CommunityPushIn = 7.4f, CommunityAgencyArrival = 10f, CommunityRevealEnd = 13f;
        private const float CommunityOverviewYaw = 45f, CommunityOverviewPitch = 60f;
        private const float CommunityAgencyPitch = 43f;
        private float _communityRevealTime, _communityRevealZoom, _communityStartZoom;
        private Vector2 _communityRestoreAngles, _communityStartAngles;
        private Vector3 _communityStartFocus, _communityOverview, _communityAgency;
        private Bounds _communityBounds;
        private float _communityAgencyZoom, _communityAgencyYaw;

        private void QueueCommunityReveal() => _communityRevealPending = true;

        private bool UpdateCommunityReveal(float dt)
        {
            if (_communityRevealPending)
            {
                _communityRevealPending = false;
                var places = City.Places.Where(p => Session.DistrictTierOf(p) == 1).ToArray();
                if (places.Length == 0)
                {
                    Ui.CloseAllWindows();
                    ShowCommunityUnlocked();
                    return true;
                }
                _communityBounds = new Bounds(World.NodeWorld(places[0].NodeId), Vector3.zero);
                foreach (var place in places)
                {
                    bool hasVisual = World.Places.TryGetValue(place.Id, out var visual);
                    var entrance = hasVisual ? visual.Entrance : World.NodeWorld(place.NodeId);
                    var center = hasVisual ? visual.LabelPos : entrance + Vector3.up * 4f;
                    float width = Mathf.Max(6f, (float)Math.Max(place.Width, place.Depth) * 1.45f);
                    _communityBounds.Encapsulate(entrance);
                    _communityBounds.Encapsulate(new Bounds(center, new Vector3(width, 8f, width)));
                }
                _communityOverview = _communityBounds.center;
                string agencyId = City.AgencyDistricts.FirstOrDefault(d => d.id == "community")?.agencyId
                    ?? places.FirstOrDefault(p => p.Kind == PlaceKind.Agency)?.Id;
                _communityAgency = _communityOverview;
                _communityAgencyZoom = 22f;
                _communityAgencyYaw = CommunityOverviewYaw;
                if (agencyId != null && World.Places.TryGetValue(agencyId, out var agency))
                {
                    var building = agency.BuildingBounds();
                    _communityAgency = building.center;
                    _communityAgencyZoom = Mathf.Max(15f, Mathf.Max(building.size.x, building.size.z) * .9f);
                    float front = agency.Root.transform.eulerAngles.y;
                    float left = front - 40f, right = front + 40f;
                    _communityAgencyYaw = Mathf.Abs(Mathf.DeltaAngle(CommunityOverviewYaw, left)) <=
                        Mathf.Abs(Mathf.DeltaAngle(CommunityOverviewYaw, right)) ? left : right;
                }
                _communityStartFocus = _rig.Focus;
                _communityStartZoom = Cam.orthographicSize;
                Vector3 angles = Cam.transform.eulerAngles;
                _communityStartAngles = new Vector2(angles.y, angles.x);
                _communityRestoreAngles = new Vector2(CameraRig.Yaw, CameraRig.Pitch);
                _communityRevealZoom = _rig.Size;
                _communityRevealTime = 0;
                _communityRevealActive = true;
                Session.Time.Hold();
                Session.SetSteer(0, 0);
                Ui.CloseAllWindows();
                Ui.BeginCommunityReveal();
            }
            if (!_communityRevealActive) return false;
            _communityRevealTime += dt;
            if (_communityRevealTime >= CommunityRevealEnd || _communityRevealTime > .35f && Input.GetKeyDown(KeyCode.Escape))
            {
                ResetCommunityReveal();
                ShowCommunityUnlocked();
                return true;
            }

            float travel = Ease(CommunityDeparture, CommunityOverviewArrival);
            float pushIn = Ease(CommunityPushIn, CommunityAgencyArrival);
            CameraRig.Yaw = Mathf.LerpAngle(Mathf.LerpAngle(_communityStartAngles.x, CommunityOverviewYaw, travel), _communityAgencyYaw, pushIn);
            CameraRig.Pitch = Mathf.LerpAngle(Mathf.LerpAngle(_communityStartAngles.y, CommunityOverviewPitch, travel), CommunityAgencyPitch, pushIn);
            var focus = Vector3.Lerp(Vector3.Lerp(_communityStartFocus, _communityOverview, travel), _communityAgency, pushIn);
            float aspect = Mathf.Max(.1f, Cam.aspect);
            float overviewZoom = CommunityOverviewZoom(aspect);
            float agencyZoom = _communityAgencyZoom / Mathf.Min(1f, aspect / 1.45f);
            _rig.Snap(focus, Mathf.Lerp(Mathf.Lerp(_communityStartZoom, overviewZoom, travel), agencyZoom, pushIn));
            SyncViews(dt);
            Ui.SetCommunityRevealTitle(Ease(CommunityAgencyArrival, CommunityAgencyArrival + .6f));
            return true;

            float Ease(float start, float end) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, _communityRevealTime));
        }

        private float CommunityOverviewZoom(float aspect)
        {
            var rotation = Quaternion.Euler(CommunityOverviewPitch, CommunityOverviewYaw, 0f);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var extents = _communityBounds.extents;
            float halfWidth = Mathf.Abs(right.x) * extents.x + Mathf.Abs(right.z) * extents.z;
            float halfHeight = Mathf.Abs(up.x) * extents.x + Mathf.Abs(up.y) * extents.y + Mathf.Abs(up.z) * extents.z;
            return Mathf.Max(46f, Mathf.Max(halfWidth / aspect, halfHeight) * 1.12f + 5f);
        }

        private void ResetCommunityReveal()
        {
            _communityRevealPending = false;
            if (!_communityRevealActive) return;
            _communityRevealActive = false;
            Ui.EndCommunityReveal();
            Session.Time.Release();
            CameraRig.Yaw = _communityRestoreAngles.x;
            CameraRig.Pitch = _communityRestoreAngles.y;
            _rig.Snap(FocusPoint(_communityRevealZoom), _communityRevealZoom);
        }

        private void ShowCommunityUnlocked()
        {
            // An event already raised before the reveal remains pending behind the unlock notice.
            if (Session.PendingEvent is EventDef pending) OnEventRaised(pending);
            Ui.Popup("第二章 · 社区片区已解锁", "通往社区片区的道路已开放。现在可以去社区逛街、看房和选店址。");
        }

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
