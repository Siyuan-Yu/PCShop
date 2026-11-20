using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _onsiteWindow;
        private Text _onsiteStatus,_onsiteReadout;
        private Image _onsitePower;
        private RectTransform _onsiteRoundResult;
        private bool _onsiteIllustratedBar;
        private bool _onsiteHeld,_onsiteWasHeld;
        private string _onsiteLayoutKey;
        private string _onsiteSportActivityId;
        public bool OnsiteInputActive=>_onsiteWindow!=null&&!_onsiteWindow.Closed;
        public void OpenOnsitePlay()
        {
            if (OnsiteInputActive || S.CurrentOnsite == null) return;
            var activity = S.RunningCityActivity;
            bool sport = GameSession.IsOnsiteSport(activity);
            _onsiteSportActivityId = sport ? activity.Id : null;
            CloseAllWindows();
            _onsiteHeld=_onsiteWasHeld=false;
            _onsiteWindow=OpenWindow("现场活动",1920,1080,BuildOnsitePlay,holds:false,closable:false,onClosed:()=>{_onsiteHeld=_onsiteWasHeld=false;_onsiteWindow=null;_onsiteSportActivityId=null;_ctrl.HideOnsiteScene();},scrollContent:false,showTitleBar:false);
        }
        private string OnsiteLayoutKey(OnsitePlayState s)=>s==null?"ended":s.Number+":"+s.Phase+":"+s.Pending+":"+s.Round+":"+s.Feedback+":"+s.Attempts+":"+s.Subject+":"+s.Base+":"+s.Flavor+":"+s.Sweet+":"+string.Join(",",s.TalkMasks)+":"+s.ProcurementDecided+":"+s.Mode+":"+s.Angle+":"+s.Selected;
        private void BuildOnsitePlay(Window w)
        {
            var s=S.CurrentOnsite;if(s==null){Line(w.Content,"本场已结束，关闭后可查看日程结算。",22);return;}
            _onsiteLayoutKey=OnsiteLayoutKey(s);_onsiteStatus=_onsiteReadout=null;_onsitePower=null;
            _onsiteRoundResult = null;
            var panel=w.Root.Find("Panel") as RectTransform;if(w.Fitter!=null)w.Fitter.enabled=false;if(panel!=null){panel.localScale=Vector3.one;panel.Fill();}
            w.Content.GetComponent<VerticalLayoutGroup>().enabled=false;w.Content.Fill();
            if (s.IsFootballPenalty)
            {
                _onsiteIllustratedBar = false;
                BuildFootballPenalty(w, s);
                return;
            }
            _onsiteIllustratedBar = s.Number is >= 51 and <= 53 && BuildLeisureBackdrop(w.Content, "event_lighthouse_bar");
            if (!_onsiteIllustratedBar)
            {
                _ctrl.RenderOnsiteScene();
                var view = Ui.Rect(w.Content, "ActualOnsiteScene").Fill();
                var raw = view.gameObject.AddComponent<RawImage>();
                raw.texture = _ctrl.OnsiteTexture;
                raw.raycastTarget = false;
            }
            var header=Ui.Panel(w.Content,"OnsiteHeader",Ui.Chrome);header.rectTransform.Anchor(0,.91f,1,1);
            Ui.Label(header.transform,S.CityActivityService(S.RunningCityActivity)?.Name,27,color:Ui.Accent).rectTransform.Anchor(.02f,0,.53f,1);
            var activity = S.RunningCityActivity;
            bool sport = GameSession.IsOnsiteSport(activity);
            bool drinkTasting = GameSession.IsDrinkTasting(activity);
            bool practice = GameSession.IsVenueSport(activity);
            bool venueBasketball = GameSession.IsVenueBasketball(activity);
            if (venueBasketball)
            {
                var score = Ui.Panel(w.Content, "BasketballPracticeScore", Ui.Chrome).rectTransform.Anchor(.025f, .80f, .185f, .882f);
                Ui.Label(score, $"已投 {s.Round} 球　命中 {s.Success} 球", 20, TextAnchor.MiddleCenter, Ui.Accent)
                    .rectTransform.Fill(6);
            }
            bool readyToFinish = practice ? S.CanFinishOnsiteSport && S.ManualAttemptsLeft == 0
                : S.CanFinishManualCityActivity(activity.Id);
            if (drinkTasting)
                Ui.Label(header.transform, $"时间暂停 · 尝完三杯计入 {activity.Duration} 分钟", 19).rectTransform.Anchor(.52f,0,.83f,1);
            else if (practice)
                Ui.Label(header.transform, $"剩余 {S.ManualAttemptsLeft} 次 · 时间暂停", 19).rectTransform.Anchor(.52f,0,.83f,1);
            else
                Ui.Label(header.transform, $"时间暂停 · 完成后结算 {activity.Duration} 分钟", 19).rectTransform.Anchor(.52f,0,.83f,1);
            if (sport && s.Phase == "result")
            {
                _onsiteRoundResult = Ui.Panel(w.Content, "OnsiteRoundResult", Ui.PanelBg2).rectTransform;
                _onsiteRoundResult.Anchor(.32f, .77f, .68f, .875f);
                bool golf = s.Number >= 57;
                string unit = golf ? "杆" : "回合";
                string tally = s.Number == 56 ? $"{(s.Round == 5 ? "终场" : "当前")}比分 {s.Success * 2} : {s.OpponentScore}"
                    : $"累计{(golf || venueBasketball ? "命中" : "成功")} {s.Success} 次";
                Ui.Label(_onsiteRoundResult, $"第 {s.Round} {unit} · {tally}", 20, TextAnchor.MiddleCenter, Ui.Dim)
                    .rectTransform.Anchor(0, .60f, 1, .96f, 12, 0, 12, 0);
                Color resultColor = golf ? Ui.Accent : s.LastSportResult switch
                {
                    OnsiteSportResult.Success => Ui.Good,
                    OnsiteSportResult.Miss or OnsiteSportResult.Blocked => Ui.Warn,
                    _ => Ui.Accent
                };
                Ui.Label(_onsiteRoundResult, s.Message, 28, TextAnchor.MiddleCenter, resultColor)
                    .rectTransform.Anchor(0, .05f, 1, .62f, 12, 0, 12, 0);
            }
            var activityId = activity.Id;
            void QuitActivity()
            {
                string explanation = drinkTasting ? "提前离开会结束这次品尝。" : "已付费用会保留。";
                if (activity.Participants.Length > 0) explanation += "基础好感按实际完成进度结算。";
                ConfirmAction("提前结束活动", explanation, "确认离开", () => S.CancelCityActivity(activityId, early: true));
            }
            if (!practice || !readyToFinish)
                Ui.Button(header.transform, practice ? "提前结算" : "提前离开", () =>
            {
                if (practice) { if (S.FinishOnsiteSport()) ReturnFromOnsitePlay(); }
                else QuitActivity();
            }, 19, Ui.ButtonBg, !practice || S.CanFinishOnsiteSport).GetComponent<RectTransform>().Anchor(.85f,.18f,.985f,.8f);
            var body=Ui.Panel(w.Content,"OnsiteActions",Ui.Chrome);
            body.rectTransform.Anchor(.735f, .22f, .988f, _onsiteIllustratedBar ? .83f : .90f);
            body.gameObject.SetActive(!venueBasketball);
            var list=Ui.Rect(body.transform,"Actions");list.Anchor(0,0,1,1,16,12,16,12);var layout=list.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=5;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
            void Label(string text)=>Ui.Size(Ui.Label(list,text,18),-1,34);
            void Button(string label, Action action, bool enabled = true, bool selected = false)
                => Ui.Size(Ui.Button(list, label, () => { action(); if (!w.Closed) w.RequestRebuild(); },
                    17, Ui.SelectedButton(selected), enabled), -1, 38);
            if (drinkTasting)
            {
                var menu = GameSession.DrinkTastingMenu;
                Label($"已品尝 {s.Round}/{menu.Count} 杯");
                for (int i = 0; i < menu.Count; i++)
                {
                    int drink = i;
                    bool tasted = (s.Mask & (1 << drink)) != 0;
                    Button((tasted ? "✓ 已尝 · " : "品尝 · ") + menu[drink], () =>
                    {
                        if (S.SipOnsiteDrink(drink) && S.RunningCityActivity?.Id != activityId) w.Close();
                    }, !tasted);
                }
            }
            else if(s.Number==52)
            {
                Label(S.RunningCityActivity.PlayerTreat?"本场你请客，完成后额外交情":"请客人与话题分别影响相处反馈");
                var guests=S.RunningCityActivity.Participants;for(int i=0;i<guests.Length;i++)
                {
                    int person=i;Label((s.TalkMasks[i]!=0?"✓ ":"")+S.CityPerson(guests[i].PersonId)?.Name);
                    var row=Ui.Row(list,38,5);for(int j=0;j<3;j++){int topic=j;Ui.Size(Ui.Button(row,new[]{"路线","预算","近况"}[j],()=>{S.TalkOnsiteGuest(person,topic);w.RequestRebuild();},16,Ui.ButtonBg,s.Pending==null&&s.TalkMasks[i]==0),-1,36,1);}
                }
            }
            else if(s.Number==53)
            {
                if(s.Pending=="mix")Label("曾玉正在吧台调饮品。");
                else if(s.Pending=="taste")Label("你接过杯子，慢慢尝了一口。");
                else if(s.Phase=="mixed")
                {
                    Ui.Size(Ui.Label(list,"曾玉：这杯好了，尝尝看？",21,color:Ui.Accent),-1,64);
                    Button("接过来，尝一口",()=>S.TasteOnsiteMix());
                }
                else if(s.Attempts>=3)
                    Ui.Size(Ui.Label(list,"曾玉：今天先喝到这里吧，下次我再换个搭配。",21,color:Ui.Accent),-1,100);
                else
                {
                    Ui.Size(Ui.Label(list,s.Round==0?"曾玉：我想试试几个搭配，你想先尝哪杯？":"曾玉：要不要再换个口味？",21,color:Ui.Accent),-1,92);
                    string[] replies={"柠檬苏打吧，清爽一点。","蜂蜜咖啡，浓一些怎么样？","我想尝尝偏甜的姜汁乌龙。"};
                    for(int i=0;i<replies.Length;i++){int option=i;Button(replies[i],()=>S.ChooseTastingDialogue(option));}
                }
            }
            else if (readyToFinish)
                Label("现场项目已完成。");
            else if(s.Number>=54&&s.Number<=58)
            {
                Label(s.BasketballDrill ? "体验场地，控制练习力度"
                    : s.Number == 56 ? "留意防守位置，松手投篮"
                    : s.Number <= 56 ? "瞄准篮筐，松手投篮" : "选杆、方向和力度，观察落点");
                if (!venueBasketball)
                {
                    string[] modes=s.Number==55?new[]{"运球节奏","急停转向","跳投"}:s.Number>=57?new[]{"铁杆 · 100米","木杆 · 180米","挖起杆 · 55米"}:s.Number==54?new[]{"定点投篮","上篮","防守脚步"}:new[]{"外线投篮","突破上篮","调整站位"};
                    for (int i = 0; i < modes.Length; i++)
                    {
                        int mode = i;
                        Button(modes[i], () => S.SelectOnsiteSportMode(mode),
                            s.Phase is not ("charging" or "flight"), selected: s.Mode == i);
                    }
                }
                if (!s.BasketballDrill)
                {
                    bool canAim = s.Phase is not ("charging" or "flight");
                    var aim = Ui.Row(list, 40, 6);
                    Ui.Size(Ui.Button(aim, "← 方向", () => { S.AimOnsiteSport(s.Angle - 3); w.RequestRebuild(); }, 17, interactable: canAim), -1, 38, 1);
                    Ui.Size(Ui.Button(aim, "方向 →", () => { S.AimOnsiteSport(s.Angle + 3); w.RequestRebuild(); }, 17, interactable: canAim), -1, 38, 1);
                    Label("方向 " + s.Angle.ToString("0") + "° · 回合 " + s.Round);
                }
                if(s.Number==55&&s.Round>=3&&s.Mask==7)for(int i=0;i<3;i++){int n=i;Button(new[]{"反馈抓地力","反馈急停脚感","反馈回弹"}[i],()=>S.OnsiteCourtFeedback(n),s.Feedback<0);}
                if(s.Number==58&&s.Round>=3)
                {Button("谈用途、型号和保修",()=>S.DecideOnsiteProcurement(0),!s.ProcurementDecided);Button("谈预算、数量和交期",()=>S.DecideOnsiteProcurement(1),!s.ProcurementDecided);Button("这次不承诺供货",()=>S.DecideOnsiteProcurement(2),!s.ProcurementDecided);}
                Label(s.Number == 55 && s.BasketballDrill ? s.Mode == 0 ? "运球约 55% 力度" : "急停约 70% 力度"
                    : "按住屏幕下方按钮或空格蓄力");
            }
            else if(s.Number==76)
            {
                Label("调整角度和远近，拍下喜欢的构图");for(int i=0;i<3;i++){int n=i;Button((s.Subject==i?"● ":"")+Gifts.Get(GameSession.OnsitePhotoSubjects[i])?.Name,()=>FramePhoto(n,s.PhotoYaw,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure));}
                void Frame(double yaw,double pitch,double distance,double offset,int exposure)=>FramePhoto(s.Subject,yaw,pitch,distance,offset,exposure);
                void Pair(string left,string right,Action a,Action b){var row=Ui.Row(list,38,5);Ui.Size(Ui.Button(row,left,()=>{a();w.RequestRebuild();},17),-1,36,1);Ui.Size(Ui.Button(row,right,()=>{b();w.RequestRebuild();},17),-1,36,1);}
                Pair("向左绕拍","向右绕拍",()=>Frame(s.PhotoYaw-15,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure),()=>Frame(s.PhotoYaw+15,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure));
                Pair("降低机位","抬高机位",()=>Frame(s.PhotoYaw,s.PhotoPitch-10,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure),()=>Frame(s.PhotoYaw,s.PhotoPitch+10,s.PhotoDistance,s.PhotoOffset,(int)s.Exposure));
                Pair("拉近","拉远",()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance-.5,s.PhotoOffset,(int)s.Exposure),()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance+.5,s.PhotoOffset,(int)s.Exposure));
                Pair("构图左移","构图右移",()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset-.3,(int)s.Exposure),()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset+.3,(int)s.Exposure));
                Button("曝光："+new[]{"偏暗","正常","偏亮"}[(int)s.Exposure],()=>Frame(s.PhotoYaw,s.PhotoPitch,s.PhotoDistance,s.PhotoOffset,((int)s.Exposure+1)%3));
                Button("拍照并保存",()=>{if(!_ctrl.CaptureOnsitePhoto())Toast("照片未保存，请检查现场、相机和写入空间。");});Button("查看相册",OpenOnsiteAlbum);
            }
            var rail=Ui.Panel(w.Content,"OnsiteBottom",Ui.Chrome);
            if (venueBasketball) rail.rectTransform.Anchor(.23f, .03f, .77f, .175f);
            else rail.rectTransform.Anchor(0, 0, 1, .19f);
            _onsiteStatus=Ui.Label(rail.transform,_onsiteRoundResult == null ? s.Message : "",21,color:Ui.Accent);_onsiteStatus.rectTransform.Anchor(.025f,s.Number==53?.18f:.48f,.70f,.98f);
            _onsiteReadout=Ui.Label(rail.transform,"",18);_onsiteReadout.rectTransform.Anchor(.025f,.10f,.70f,.48f);
            if (venueBasketball && !readyToFinish)
            {
                _onsiteStatus.rectTransform.Anchor(.035f, .63f, .47f, .98f);
                _onsiteReadout.rectTransform.Anchor(.035f, .06f, .70f, .28f);
                bool canAim = s.Phase is not ("charging" or "flight");
                Ui.Button(rail.transform, "←", () => S.AimOnsiteSport(s.Angle - 3), 19, Ui.ButtonBg, canAim)
                    .GetComponent<RectTransform>().Anchor(.49f, .64f, .565f, .91f);
                Ui.Button(rail.transform, "→", () => S.AimOnsiteSport(s.Angle + 3), 19, Ui.ButtonBg, canAim)
                    .GetComponent<RectTransform>().Anchor(.578f, .64f, .653f, .91f);
            }
            if (readyToFinish)
            {
                Ui.Button(rail.transform, "结束并结算", () =>
                {
                    if (practice)
                    {
                        if (S.FinishOnsiteSport()) ReturnFromOnsitePlay();
                    }
                    else if (S.FinishManualCityActivity(activityId)) w.Close();
                }, 22, Ui.ButtonHot).GetComponent<RectTransform>().Anchor(.73f,.2f,.98f,.85f);
            }
            else if(s.Number>=54&&s.Number<=58)
            {
                string action = s.BasketballDrill ? s.Number == 54 ? "练习脚步" : s.Mode == 0 ? "运球" : "急停转向"
                    : s.Number == 55 ? "跳投" : s.Number <= 56 ? "投篮" : "击球";
                bool canCharge = s.Phase != "flight";
                string chargeLabel = canCharge ? "按住蓄力\n松手" + action : s.BasketballDrill ? "正在练习…" : "球在飞行中…";
                var charge = Ui.Button(rail.transform, chargeLabel, null, 23, Ui.ButtonHot, canCharge);
                charge.GetComponent<RectTransform>().Anchor(.73f,.2f,.98f,.85f);
                if (canCharge) charge.gameObject.AddComponent<FishingHoldInput>().Held = v => _onsiteHeld = v;
                var meter = Ui.Panel(rail.transform, "PowerBackground", Ui.ButtonBg);
                meter.rectTransform.Anchor(.035f, venueBasketball ? .35f : .02f, .653f, venueBasketball ? .51f : .09f);
                _onsitePower = Ui.Panel(meter.transform, "Power", Ui.ButtonHot);
                _onsitePower.raycastTarget = false;
                if (venueBasketball)
                {
                    // Strength interval that reaches the rim when aimed straight ahead.
                    float start = (float)((s.TargetZ - OnsitePlayState.BasketballRimTolerance) / s.BasketballShotRange);
                    float end = (float)((s.TargetZ + OnsitePlayState.BasketballRimTolerance) / s.BasketballShotRange);
                    var interval = Ui.Panel(meter.transform, "RimRange", new Color(.39f, .64f, .39f, .7f));
                    interval.rectTransform.Anchor(start, 0, end, 1);
                    interval.raycastTarget = false;
                }
            }
            else if (!drinkTasting)
                Ui.Button(rail.transform,"完成现场步骤后结算",null,19,Ui.ButtonBg,false).GetComponent<RectTransform>().Anchor(.74f,.3f,.98f,.75f);
        }
        private void FramePhoto(int subject,double yaw,double pitch,double distance,double offset,int exposure)=>S.SetOnsitePhotoFrame(subject,yaw,pitch,distance,offset,exposure);
        private void ReturnFromOnsitePlay()
        {
            var id = _onsiteSportActivityId;
            _onsiteWindow?.Close();
            var activity = S.CityActivities.FirstOrDefault(item => item.Id == id);
            if (!GameSession.IsVenueSport(activity) || S.CurrentPlace?.Id != activity.VenueId || S.IsGameOver) return;
            OpenSportVenue(S.CurrentPlace, null);
            OpenCityActivitySettlement(activity.Id);
        }
        private void UpdateOnsitePlay(float dt)
        {
            if (!OnsiteInputActive)
            {
                var activity = S.RunningCityActivity;
                if (!AnyModalOpen && !S.IsGameOver && !OpeningMealRestricted
                    && (GameSession.IsOnsiteSport(activity) || GameSession.IsOnsiteActivity(activity)))
                    OpenOnsitePlay();
                return;
            }
            var s = S.CurrentOnsite;
            if (s == null) { ReturnFromOnsitePlay(); return; }
            if (_windows.LastOrDefault() != _onsiteWindow) return;
            bool held=_onsiteHeld||Input.GetKey(KeyCode.Space);
            if (GameSession.IsOnsiteSport(S.RunningCityActivity))
            {
                if (s.IsFootballPenalty) UpdateFootballAim(s);
                else if (s.Number is >= 54 and <= 56 && !s.BasketballDrill)
                {
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) S.AimOnsiteSport(s.Angle - 3);
                    if (Input.GetKeyDown(KeyCode.RightArrow)) S.AimOnsiteSport(s.Angle + 3);
                }
                if (held && !_onsiteWasHeld) S.BeginOnsiteCharge();
                if (!held && _onsiteWasHeld) S.ReleaseOnsiteShot();
                S.StepOnsiteSport(dt);
            }
            _onsiteWasHeld=held;s=S.CurrentOnsite;
            if (s == null) { ReturnFromOnsitePlay(); return; }
            if(OnsiteLayoutKey(s)!=_onsiteLayoutKey&&s.Phase!="charging")_onsiteWindow.RequestRebuild();
            bool showRoundResult = _onsiteRoundResult != null && s.Phase == "result";
            if (_onsiteRoundResult != null) _onsiteRoundResult.gameObject.SetActive(showRoundResult);
            if (!_onsiteIllustratedBar) _ctrl.RenderOnsiteScene();
            if (s.IsFootballPenalty)
            {
                RefreshFootballPenalty(s);
                return;
            }
            if (_onsiteStatus != null) _onsiteStatus.text = showRoundResult ? "" : s.Message;
            if (_onsiteReadout != null)
            {
                if (s.Number is >= 54 and <= 56)
                {
                    string direction = s.BasketballDrill ? "" : $" · 方向 {s.Angle:0}°";
                    string count = s.Number == 56 ? $"比分 {s.Success * 2} : {s.OpponentScore}"
                        : $"{(GameSession.IsVenueBasketball(S.RunningCityActivity) ? "命中" : "成功")} {s.Success} 次";
                    _onsiteReadout.text = $"力度 {s.Power:P0}{direction} · 完成 {s.Round} 回合 · {count}";
                }
                else _onsiteReadout.text=s.Number is 51 or 53?"":s.Pending!=null?"现场操作中":s.Number>=57&&s.Number<=58?$"力度{s.Power:P0} · 方向{s.Angle:0}° · 已打{s.Round}杆 · 目标({s.TargetX:0}, {s.TargetZ:0})米 · 风偏{s.Wind:+0.0;-0.0;0}米":s.Number==76?$"已保存{s.PhotoCount}张 · 合格{s.GoodPhotos}张 · 当前构图{S.OnsitePhotoScore}分":"聊完后选择「结束并结算」。";
            }
            if (_onsitePower != null)
            {
                _onsitePower.rectTransform.Anchor(0, 0, (float)s.Power, 1);
                if (GameSession.IsVenueBasketball(S.RunningCityActivity))
                    _onsitePower.color = Math.Abs(s.Power * s.BasketballShotRange - s.TargetZ)
                        < OnsitePlayState.BasketballRimTolerance ? Ui.Good : Ui.ButtonHot;
            }
        }
        public void OpenOnsiteAlbum()
        {
            OpenWindow("拍摄相册",850,720,w=>
            {
                foreach(var p in S.OnsitePhotos.Reverse())
                {var photo=p;Line(w.Content,(Gifts.Get(p.SubjectId)?.Name??p.SubjectId)+" · 构图"+p.Score+"分 · "+p.Width+"×"+p.Height,18);Ui.Size(Ui.Button(w.Content,"查看照片",()=>OpenOnsitePhoto(photo),17),-1,38);}
                if(S.OnsitePhotos.Length==0)Line(w.Content,"尚无已保存照片。",18);
            });
        }
        private void OpenOnsitePhoto(OnsitePhoto photo)
        {
            Texture2D texture=null;
            OpenWindow("照片 · "+(Gifts.Get(photo.SubjectId)?.Name??photo.SubjectId),1000,700,w=>
            {
                string path=_ctrl.OnsitePhotoPath(photo.File);if(!File.Exists(path)){Para(w.Content,"此设备上没有原照片，拍摄记录仍保留。",18);return;}
                texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));var rect=Ui.Rect(w.Content,"ActualSavedPhoto");Ui.Size(rect,-1,550);var image=rect.gameObject.AddComponent<RawImage>();image.texture=texture;
            },onClosed:()=>{if(texture!=null)UnityEngine.Object.Destroy(texture);});
        }
    }
}
