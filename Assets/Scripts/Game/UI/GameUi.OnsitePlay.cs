using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _onsiteWindow;
        private Text _onsiteStatus,_onsiteReadout;
        private Image _onsitePower;
        private Text _onsiteBasketballControls, _onsiteBasketballAimDot;
        private bool _onsiteBasketballMouseLook, _onsiteBasketballCursorLocked, _onsiteBasketballMouseHeld;
        private RectTransform _onsiteRoundResult;
        private bool _onsiteIllustratedBar;
        private bool _onsiteHeld,_onsiteWasHeld;
        private string _onsiteLayoutKey;
        private string _onsiteSportActivityId;
        public bool OnsiteInputActive=>_onsiteWindow!=null&&!_onsiteWindow.Closed;
        public void OpenOnsitePlay()
        {
            if (OnsiteInputActive || S.CurrentOnsite is not OnsitePlayState state) return;
            var activity = S.RunningCityActivity;
            bool sport = GameSession.IsOnsiteSport(activity);
            _onsiteSportActivityId = sport ? activity.Id : null;
            CloseAllWindows();
            _onsiteHeld = _onsiteWasHeld = _onsiteBasketballMouseHeld = false;
            _onsiteBasketballMouseLook = state.IsBasketball && !OnsiteReadyToFinish(activity);
            if (_onsiteBasketballMouseLook) S.CancelOnsiteBasketballCharge();
            _onsiteWindow=OpenWindow("现场活动",1920,1080,BuildOnsitePlay,holds:false,closable:false,onClosed:()=>
            {
                _onsiteHeld = _onsiteWasHeld = _onsiteBasketballMouseLook = _onsiteBasketballMouseHeld = false;
                CaptureBasketballMouse(false);
                _onsiteWindow = null;
                _onsiteSportActivityId = null;
                _ctrl.HideOnsiteScene();
            },scrollContent:false,showTitleBar:false);
        }
        private string OnsiteLayoutKey(OnsitePlayState s)=>s==null?"ended":s.Number+":"+s.Phase+":"+s.Pending+":"+s.Round+":"+s.Feedback+":"+s.Attempts+":"+s.Subject+":"+s.Base+":"+s.Flavor+":"+s.Sweet+":"+string.Join(",",s.TalkMasks)+":"+s.ProcurementDecided+":"+s.Mode+":"+s.Selected;
        private void BuildOnsitePlay(Window w)
        {
            var s=S.CurrentOnsite;if(s==null){Line(w.Content,"本场已结束，关闭后可查看日程结算。",22);return;}
            _onsiteLayoutKey=OnsiteLayoutKey(s);_onsiteStatus=_onsiteReadout=null;_onsitePower=null;
            _onsiteRoundResult = null;
            _onsiteBasketballControls = _onsiteBasketballAimDot = null;
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
            bool readyToFinish = OnsiteReadyToFinish(activity);
            if (s.IsBasketball)
            {
                var score = Ui.Panel(w.Content, "BasketballPracticeScore", Ui.Chrome).rectTransform.Anchor(.025f, .80f, .22f, .882f);
                string scoreText = s.Number == 56 ? $"比分 {s.BasketballPoints} : {s.OpponentScore}"
                    : venueBasketball ? $"已投 {s.Round} 球　得分 {s.BasketballPoints} 分"
                    : $"完成 {s.Round} 回合　得分 {s.BasketballPoints} 分";
                Ui.Label(score, scoreText, 20, TextAnchor.MiddleCenter, Ui.Accent)
                    .rectTransform.Fill(6);
                var hint = Ui.Panel(w.Content, "BasketballControls", Ui.Chrome).rectTransform;
                hint.Anchor(.025f, .71f, .315f, .785f);
                _onsiteBasketballControls = Ui.Label(hint, BasketballControlHint(readyToFinish), 17, TextAnchor.MiddleCenter);
                _onsiteBasketballControls.rectTransform.Fill(6);
                if (!s.BasketballDrill && !readyToFinish)
                {
                    _onsiteBasketballAimDot = Ui.Label(w.Content, "•", 27, TextAnchor.MiddleCenter, Color.white);
                    _onsiteBasketballAimDot.gameObject.name = "BasketballAimDot";
                    _onsiteBasketballAimDot.raycastTarget = false;
                    _onsiteBasketballAimDot.enabled = _onsiteBasketballMouseLook && s.Phase != "flight";
                    _onsiteBasketballAimDot.rectTransform.Anchor(.5f, .5f, .5f, .5f, -15, -15, -15, -15);
                    var outline = _onsiteBasketballAimDot.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0, 0, 0, .55f);
                    outline.effectDistance = new Vector2(1, -1);
                }
            }
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
                string tally = s.Number == 56 ? $"{(s.Round == 5 ? "终场" : "当前")}比分 {s.BasketballPoints} : {s.OpponentScore}"
                    : s.IsBasketball ? $"累计得分 {s.BasketballPoints} 分" : $"累计命中 {s.Success} 次";
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
                    var row = Ui.Row(list, 38, 5);
                    string[] topics = { "路线", "预算", "近况" };
                    for (int j = 0; j < topics.Length; j++)
                    {
                        int topic = j;
                        Ui.Size(Ui.Button(row, topics[j], () =>
                        {
                            if (!S.TalkOnsiteGuest(person, topic)) return;
                            string personId = guests[person].PersonId;
                            string reply = topic switch
                            {
                                0 => "去湖边的话，我会从市场后面那条路绕。虽然多走一点，但中途有地方歇脚。",
                                1 => "我习惯先把车费和吃饭的钱算好。想加个项目，我们再看看每个人愿意出多少。",
                                _ => S.CityPerson(personId)?.Topics?.ElementAtOrDefault(2) ?? "这阵子手头挺忙的。今天能和你们坐下来，我挺高兴。"
                            };
                            OpenActivityReply(personId, reply, "回到聚会");
                            w.RequestRebuild();
                        }, 16, Ui.ButtonBg, s.Pending == null && s.TalkMasks[i] == 0), -1, 36, 1);
                    }
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
                    for (int i = 0; i < replies.Length; i++)
                    {
                        int option = i;
                        Button(replies[i], () =>
                        {
                            if (!S.ChooseTastingDialogue(option)) return;
                            string reply = option switch
                            {
                                0 => "好，我先挤点柠檬，糖就少放一点。你喝的时候留意一下，酸味过后还有没有香气。",
                                1 => "可以，我先用一点蜂蜜打底。咖啡一倒进去香气就起来了，甜味留在后面。",
                                _ => "好，姜先泡一会儿，等味道出来再加乌龙。我调好你尝尝，不够甜我们再添。"
                            };
                            OpenActivityReply("resident_zengyu", reply, "回到吧台");
                        });
                    }
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
                if (!s.IsBasketball)
                {
                    bool canAim = s.Phase is not ("charging" or "flight");
                    var aim = Ui.Row(list, 40, 6);
                    void MoveAim(double degrees)
                    {
                        if (S.CurrentOnsite is OnsitePlayState current) S.AimOnsiteSport(current.Angle + degrees);
                    }
                    Ui.Size(Ui.Button(aim, "← 方向", () => MoveAim(-3), 17, interactable: canAim), -1, 38, 1);
                    Ui.Size(Ui.Button(aim, "方向 →", () => MoveAim(3), 17, interactable: canAim), -1, 38, 1);
                    Label("方向 " + s.Angle.ToString("0") + "° · 回合 " + s.Round);
                }
                if (s.Number == 55 && s.Round >= 3 && s.Mask == 7)
                {
                    string[] captions = { "反馈抓地力", "反馈急停脚感", "反馈回弹" };
                    string[] replies = { "我记下了。下次我换双鞋在同一块位置试，看看鞋底会不会影响抓地力。",
                        "我会再试试急停那一步。等地面用过几天，我们找同一条线再跑几次。", "我把回弹记下来。下次拿同一个球去旧地板试一下，差别就听得出来了。" };
                    for (int i = 0; i < captions.Length; i++)
                    {
                        int topic = i;
                        Button(captions[i], () =>
                        {
                            if (S.OnsiteCourtFeedback(topic)) ReplyFromCompanion(replies[topic]);
                        }, s.Feedback < 0);
                    }
                }
                if (s.Number == 58 && s.Round >= 3)
                {
                    string[] captions = { "谈用途、型号和保修", "谈预算、数量和交期", "这次不承诺供货" };
                    string[] replies = { "我们先把实际用来做什么聊清楚，再定配置。保修和出问题找谁，也一起写在报价里吧。",
                        "先把能交付的数量和日期给我。预算可以另谈，交货时间得留够。", "行，今天就聊到这儿。后面真有合适的单子，我再把需求发给你。" };
                    for (int i = 0; i < captions.Length; i++)
                    {
                        int topic = i;
                        Button(captions[i], () =>
                        {
                            if (S.DecideOnsiteProcurement(topic)) ReplyFromCompanion(replies[topic] + "\n\n<size=16>" + s.Message + "</size>");
                        }, !s.ProcurementDecided);
                    }
                }
                if (!s.IsBasketball) Label("按住屏幕下方按钮或空格蓄力");
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
            if (activity.DefinitionId == "golf" && s.Phase is not ("charging" or "flight"))
            {
                var definition = S.CityActivityService(activity);
                foreach (int topic in ActivityTopicIndices(activity, definition))
                    Button(definition.Choices[topic], () => SelectCityActivityOption(activity, definition, topic, "回到练习场"),
                        !activity.Choices.Contains(topic.ToString()));
            }
            var rail=Ui.Panel(w.Content,"OnsiteBottom",Ui.Chrome);
            if (s.IsBasketball) rail.rectTransform.Anchor(.25f, .03f, .75f, .135f);
            else rail.rectTransform.Anchor(0, 0, 1, .19f);
            _onsiteStatus=Ui.Label(rail.transform,_onsiteRoundResult == null ? s.Message : "",21,color:Ui.Accent);_onsiteStatus.rectTransform.Anchor(.025f,s.Number==53?.18f:.48f,.70f,.98f);
            _onsiteReadout=Ui.Label(rail.transform,"",18);_onsiteReadout.rectTransform.Anchor(.025f,.10f,.70f,.48f);
            if (s.IsBasketball)
            {
                _onsiteStatus.fontSize = 17;
                _onsiteStatus.rectTransform.Anchor(.035f, .06f, .70f, .39f);
                _onsiteReadout.text = $"蓄力 {s.Power:P0}";
                _onsiteReadout.rectTransform.Anchor(.035f, .62f, .70f, .95f);
                var meter = Ui.Panel(rail.transform, "PowerBackground", Ui.PanelBg2);
                meter.rectTransform.Anchor(.035f, .41f, .70f, .59f);
                meter.raycastTarget = false;
                _onsitePower = Ui.Panel(meter.transform, "Power", Ui.ButtonHot);
                _onsitePower.rectTransform.Anchor(0, 0, (float)s.Power, 1);
                _onsitePower.raycastTarget = false;
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
                string chargeLabel = canCharge ? (s.IsBasketball ? "左键 / 空格蓄力" : "按住蓄力") + "\n松手" + action
                    : s.BasketballDrill ? "正在练习…" : "球在飞行中…";
                var charge = Ui.Button(rail.transform, chargeLabel, null, 23, Ui.ButtonHot, canCharge);
                charge.GetComponent<RectTransform>().Anchor(.73f,.2f,.98f,.85f);
                if (canCharge) charge.gameObject.AddComponent<FishingHoldInput>().Held = v => _onsiteHeld = v;
                if (!s.IsBasketball)
                {
                    var meter = Ui.Panel(rail.transform, "PowerBackground", Ui.ButtonBg);
                    meter.rectTransform.Anchor(.035f, .02f, .653f, .09f);
                    _onsitePower = Ui.Panel(meter.transform, "Power", Ui.ButtonHot);
                    _onsitePower.raycastTarget = false;
                }
            }
            else if (!drinkTasting)
                Ui.Button(rail.transform,"完成现场步骤后结算",null,19,Ui.ButtonBg,false).GetComponent<RectTransform>().Anchor(.74f,.3f,.98f,.75f);

            void ReplyFromCompanion(string line)
            {
                var person = ActivitySpeakers(activity, null).FirstOrDefault();
                if (person != null) OpenActivityReply(person.Id, line, "回到场上");
            }
        }

        private bool OnsiteReadyToFinish(CityActivity activity) => GameSession.IsVenueSport(activity)
            ? S.CanFinishOnsiteSport && S.ManualAttemptsLeft == 0
            : S.CanFinishManualCityActivity(activity.Id);

        private string BasketballControlHint(bool readyToFinish) => readyToFinish ? "本场已完成 · 点击「结束并结算」"
            : _onsiteBasketballMouseLook
            ? "WASD / 方向键移动 · 鼠标转视角\nTab 显示鼠标 · V 重置镜头"
            : "Tab 继续瞄准";

        private void CaptureBasketballMouse(bool capture)
        {
            if (!Application.isPlaying || Application.isBatchMode || _onsiteBasketballCursorLocked == capture) return;
            _onsiteBasketballCursorLocked = capture;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
            if (capture) EventSystem.current?.SetSelectedGameObject(null);
        }

        private void UpdateBasketballControls(OnsitePlayState shot, float dt)
        {
            if (_onsiteBasketballCursorLocked && Cursor.lockState != CursorLockMode.Locked)
                _onsiteBasketballMouseLook = false;
            if (Input.GetKeyDown(KeyCode.Tab)) _onsiteBasketballMouseLook = !_onsiteBasketballMouseLook;
            if (Input.GetKeyDown(KeyCode.Escape)) _onsiteBasketballMouseLook = false;
            bool wasLocked = _onsiteBasketballCursorLocked;
            CaptureBasketballMouse(_onsiteBasketballMouseLook);
            if (_onsiteBasketballMouseLook)
            {
                if (wasLocked)
                {
                    _ctrl.TurnOnsiteBasketballView(Input.GetAxisRaw("Mouse X") * 3.2f, -Input.GetAxisRaw("Mouse Y") * 2.8f);
                    _onsiteBasketballMouseHeld = Input.GetMouseButton(0)
                        && (_onsiteBasketballMouseHeld || Input.GetMouseButtonDown(0));
                }
                Vector2 movement = Vector2.ClampMagnitude(DirectionalInput.Held, 1);
                if (movement != Vector2.zero && dt > 0 && _ctrl.MoveOnsiteBasketball(movement * (4f * Mathf.Min(dt, .1f))))
                    shot = S.CurrentOnsite;
            }
            else if (_onsiteBasketballMouseHeld)
            {
                _onsiteBasketballMouseHeld = _onsiteWasHeld = false;
                S.CancelOnsiteBasketballCharge();
            }
            if (Input.GetKeyDown(KeyCode.V)) _ctrl.ResetOnsiteBasketballView();
            if (!shot.BasketballDrill && shot.Phase != "flight") _ctrl.AimOnsiteBasketballFromView(shot);
            if (_onsiteBasketballControls != null) _onsiteBasketballControls.text = BasketballControlHint(false);
            if (_onsiteBasketballAimDot != null)
                _onsiteBasketballAimDot.enabled = _onsiteBasketballMouseLook && shot.Phase != "flight";
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
            if (_windows.LastOrDefault() != _onsiteWindow
                || s.IsBasketball && Application.isPlaying && !Application.isBatchMode && !Application.isFocused)
            {
                if (s.IsBasketball)
                {
                    CaptureBasketballMouse(false);
                    S.CancelOnsiteBasketballCharge();
                    _onsiteHeld = _onsiteWasHeld = _onsiteBasketballMouseHeld = false;
                }
                return;
            }
            var runningActivity = S.RunningCityActivity;
            bool ballGameFinished = (s.IsBasketball || s.IsFootballPenalty) && OnsiteReadyToFinish(runningActivity);
            bool held = !ballGameFinished && (_onsiteHeld || Input.GetKey(KeyCode.Space));
            if (GameSession.IsOnsiteSport(runningActivity))
            {
                if (s.IsFootballPenalty && !ballGameFinished) UpdateFootballAim(s);
                else if (s.IsBasketball && !ballGameFinished)
                {
                    UpdateBasketballControls(s, dt);
                    held |= _onsiteBasketballMouseHeld;
                }
                if (held && !_onsiteWasHeld) S.BeginOnsiteCharge();
                if (!held && _onsiteWasHeld) S.ReleaseOnsiteShot();
                S.StepOnsiteSport(dt);
            }
            _onsiteWasHeld=held;s=S.CurrentOnsite;
            if (s == null) { ReturnFromOnsitePlay(); return; }
            if (s.IsBasketball && (ballGameFinished || OnsiteReadyToFinish(runningActivity)))
            {
                _onsiteBasketballMouseLook = _onsiteBasketballMouseHeld = _onsiteHeld = _onsiteWasHeld = false;
                CaptureBasketballMouse(false);
                if (_onsiteBasketballControls != null) _onsiteBasketballControls.text = BasketballControlHint(true);
                if (_onsiteBasketballAimDot != null) _onsiteBasketballAimDot.enabled = false;
            }
            if(!s.IsFootballPenalty&&OnsiteLayoutKey(s)!=_onsiteLayoutKey&&s.Phase!="charging")_onsiteWindow.RequestRebuild();
            bool showRoundResult = _onsiteRoundResult != null && s.Phase == "result";
            if (_onsiteRoundResult != null) _onsiteRoundResult.gameObject.SetActive(showRoundResult);
            if (!_onsiteIllustratedBar) _ctrl.RenderOnsiteScene();
            if (s.IsFootballPenalty)
            {
                RefreshFootballPenalty(s);
                return;
            }
            if (_onsiteStatus != null)
                _onsiteStatus.text = showRoundResult ? "" : s.Message;
            if (_onsiteReadout != null)
            {
                if (s.IsBasketball)
                    _onsiteReadout.text = $"蓄力 {s.Power:P0}";
                else _onsiteReadout.text=s.Number is 51 or 53?"":s.Pending!=null?"现场操作中":s.Number>=57&&s.Number<=58?$"力度{s.Power:P0} · 方向{s.Angle:0}° · 已打{s.Round}杆 · 目标({s.TargetX:0}, {s.TargetZ:0})米 · 风偏{s.Wind:+0.0;-0.0;0}米":s.Number==76?$"已保存{s.PhotoCount}张 · 合格{s.GoodPhotos}张 · 当前构图{S.OnsitePhotoScore}分":"聊完后选择「结束并结算」。";
            }
            if (_onsitePower != null) _onsitePower.rectTransform.Anchor(0, 0, (float)s.Power, 1);
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
