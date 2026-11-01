using System;
using System.IO;
using System.Linq;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Core.Session;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    [InitializeOnLoad]
    public static class CityMainPlaySmoke
    {
        private const string Key="PCShop.CityMainSmoke";
        private static double _next;
        private static int _step,_errors;
        private static double _travelWait;
        private static long _selectionCash;
        static CityMainPlaySmoke()
        {
            if(!SessionState.GetBool(Key,false))return;
            SaveStore.DirectoryOverride=Path.GetFullPath("work/city-main-smoke-saves");
            Application.logMessageReceived+=OnLog;EditorApplication.update+=Update;
        }
        public static void RunFromBatch()
        {
            SessionState.SetBool(Key,true);
            SaveStore.DirectoryOverride=Path.GetFullPath("work/city-main-smoke-saves");
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        public static void RunEnergyPayrollFromBatch()
        {
            SessionState.SetBool(Key+"Energy",true);RunFromBatch();
        }
        public static void RunUnifiedDialogueFromBatch()
        {
            SessionState.SetBool(Key+"Unified",true);RunFromBatch();
        }
        public static void RunSelectionFromBatch()
        {
            SessionState.SetBool(Key+"Selection",true);RunFromBatch();
        }
        private static Dropdown Choice(GameController game,string name) => game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Dropdown>()).First(d=>d.name==name);
        private static void PickChoice(GameController game,string name,int value)
        {
            var choice=Choice(game,name);choice.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
            var list=choice.transform.Find("Dropdown List");
            if(list==null)throw new InvalidOperationException("Native choice list did not open");
            var toggle=list.GetComponentsInChildren<Toggle>().First(t=>t.GetComponentInChildren<Text>()?.text==value.ToString("00"));
            toggle.isOn=true;
        }
        private static void AssertOnlySearchInputs(GameController game)
        {
            foreach(var input in game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<InputField>()))
                if(!(input.placeholder is Text hint)||!hint.text.StartsWith("搜索",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected numeric/time keyboard entry");
        }
        private static void Click(GameController game,string text)
        {
            var button=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).First(b=>b.GetComponentInChildren<Text>()?.text.Replace("、","，")==text.Replace("、","，"));
            if(!button.interactable)throw new InvalidOperationException("Disabled conversation option: "+text);
            button.onClick.Invoke();
        }
        private static void AssertDialogue(GameController game)
        {
            var nodes=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).ToArray();
            if(!nodes.Any(t=>t.name=="Say")||!nodes.Any(t=>t.name=="Portrait"))throw new InvalidOperationException("Expected original portrait dialogue renderer");
            if(game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text.Contains("原有人脉互动")))throw new InvalidOperationException("Duplicate conversation bridge remains");
        }
        private static void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_errors++;}
        private static void Update()
        {
            if(!EditorApplication.isPlaying)return;
            double now=EditorApplication.timeSinceStartup;if(_next==0)_next=now+3;if(now<_next)return;
            try
            {
                var game=UnityEngine.Object.FindObjectOfType<GameController>();
                if(game?.Session==null||game.Ui==null)throw new InvalidOperationException("Main scene did not bootstrap");
                if(SessionState.GetBool(Key+"Selection",false))
                {
                    switch(_step)
                    {
                        case 0:game.Ui.CloseAllWindows();game.StartCityLifePreset("night");game.Ui.QuestPopups=false;_selectionCash=game.Session.Ledger.Balance.Cents;game.Ui.OpenEmployeesPanel();break;
                        case 1:Click(game,"调整班次");break;
                        case 2:AssertOnlySearchInputs(game);Choice(game,"ClockHour_上班").OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));break;
                        case 3:Capture("city_click_01_time_dropdown");game.Ui.CloseTopWindow();if(Choice(game,"ClockHour_上班")==null)throw new InvalidOperationException("Cancel closed the schedule instead of only the choice list");break;
                        case 4:PickChoice(game,"ClockHour_上班",9);break;
                        case 5:PickChoice(game,"ClockHour_下班",9);break;
                        case 6:
                            var save=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).First(b=>b.GetComponentInChildren<Text>()?.text=="保存排班");
                            if(save.interactable)throw new InvalidOperationException("Equal shift start/end must not save");PickChoice(game,"ClockHour_下班",19);break;
                        case 7:
                            Capture("city_click_02_schedule");Click(game,"保存排班");
                            var e=game.Session.MainShop.Employee;
                            if(e.Schedule.PendingStart!=540||e.Schedule.PendingEnd!=1140||game.Session.Ledger.Balance.Cents!=_selectionCash)throw new InvalidOperationException("Draft choice changed current workday or charged cash");
                            game.Ui.CloseAllWindows();game.Ui.OpenCityActivityOffer("dinner");break;
                        case 8:AssertOnlySearchInputs(game);Capture("city_click_03_activity_time");game.Ui.CloseAllWindows();game.Session.Player.Energy=100;game.Session.Time.SetSpeed(8);game.GoHomeToSleep();_travelWait=now;break;
                        case 9:
                            if(game.Session.CurrentPlace!=game.Session.Home||!game.Ui.AnyModalOpen){if(now-_travelWait>45)throw new InvalidOperationException("Normal home/bed route did not open sleep choices");_next=now+.3;return;}
                            AssertOnlySearchInputs(game);Capture("city_click_04_wake_time");game.Ui.CloseAllWindows();
                            typeof(PCShopSell.Game.UI.GameUi).GetMethod("OpenHoursPicker",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game.Ui,new object[]{game.Session.MainShop.Sim,true});break;
                        case 10:PickChoice(game,"ClockHour_开门",10);break;
                        case 11:PickChoice(game,"ClockMinute_开门",15);break;
                        case 12:AssertOnlySearchInputs(game);Capture("city_click_05_shop_hours");Click(game,"保存营业时间");if(game.Session.MainShop.Sim.OpenMinute!=615)throw new InvalidOperationException("Minute-level shop opening was not applied");game.Ui.CloseAllWindows();game.Ui.OpenCityProcurement();break;
                        case 13:Click(game,"比较来源 / 采购");break;
                        case 14:
                            AssertOnlySearchInputs(game);
                            var plus=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).First(b=>b.name.StartsWith("QuantityPlus_unified_buy_",StringComparison.Ordinal));plus.onClick.Invoke();break;
                        case 15:
                            var value=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).First(t=>t.name.StartsWith("QuantityValue_unified_buy_",StringComparison.Ordinal));
                            if(value.text!="2")throw new InvalidOperationException("Purchase quantity did not change without typing");Capture("city_click_06_quantity");break;
                        default:
                            Debug.Log("CITY CLICK SELECTION MAIN PLAY RESULT errors="+_errors+" scene="+gameObjectScene(game));SessionState.SetBool(Key+"Selection",false);SessionState.SetBool(Key,false);EditorApplication.Exit(_errors==0?0:1);return;
                    }
                    _step++;_next=now+2;return;
                }
                if(SessionState.GetBool(Key+"Unified",false))
                {
                    var supplier=Npcs.Get(Npcs.Li);var stall=game.Session.City.GetPlace(supplier.StallId);
                    switch(_step)
                    {
                        case 0:game.Ui.CloseAllWindows();game.StartCityLifePreset("staff");game.Ui.QuestPopups=false;game.Ui.OpenPersonDialogue("life_ming");AssertDialogue(game);break;
                        case 1:Capture("city_unified_01_employee");Click(game,"工作与近况");AssertDialogue(game);break;
                        case 2:Capture("city_unified_02_reply");Click(game,"兴趣、生活与朋友");break;
                        case 3:Click(game,"听个人经历");Capture("city_unified_03_story");game.Ui.CloseAllWindows();game.Ui.OpenCityPersonProfile("life_creator");break;
                        case 4:Capture("city_unified_04_profile");game.Ui.CloseAllWindows();game.StartCityLifePreset("clothing_summer");game.Ui.OpenCityClimate();break;
                        case 5:Capture("city_unified_05_weather");game.Ui.CloseAllWindows();game.Session.Player.Energy=100;game.Session.Time.SetSpeed(8);game.GoToPlace(stall);break;
                        case 6:
                            if(game.Session.PendingEvent==null&&game.Ui.AnyModalOpen&&game.Session.CurrentPlace!=stall)
                            {game.Ui.CloseAllWindows();_next=now+.2;return;}
                            if(game.Session.PendingEvent!=null)
                            {
                                var choice=game.Session.PendingEvent.Choices.Last(c=>game.Session.IsChoiceEnabled(c));
                                Click(game,choice.Text);_next=now+.2;return;
                            }
                            if(game.Session.CurrentPlace!=stall)
                            {
                                if(_travelWait==0)_travelWait=now;
                                if(now-_travelWait>30){Capture("city_unified_travel_debug");throw new InvalidOperationException("Travel did not finish: place="+game.Session.CurrentPlace?.Id+" intent="+game.Session.CityIntent?.TargetPlaceId+" trip="+game.Session.Trip?.TargetNode+" held="+game.Session.Time.IsHeld+" paused="+game.Session.Time.IsUserPaused+" clock="+game.Session.Clock.Now);}
                                _next=now+.5;return;
                            }
                            game.Ui.CloseAllWindows();game.Ui.OpenNpcWindow(supplier,stall);AssertDialogue(game);break;
                        case 7:Capture("city_unified_06_supplier");game.Ui.CloseAllWindows();game.Ui.OpenGreeterDialogue(stall);AssertDialogue(game);break;
                        case 8:Capture("city_unified_07_clerk");break;
                        default:
                            Debug.Log("CITY UNIFIED DIALOGUE MAIN PLAY RESULT errors="+_errors+" scene="+gameObjectScene(game));
                            SessionState.SetBool(Key+"Unified",false);SessionState.SetBool(Key,false);EditorApplication.Exit(_errors==0?0:1);return;
                    }
                    _step++;_next=now+2;return;
                }
                if(SessionState.GetBool(Key+"Energy",false))
                {
                    switch(_step)
                    {
                        case 0:game.Ui.CloseAllWindows();game.StartCityLifePreset("night");game.Ui.QuestPopups=false;game.Ui.OpenEmployeesPanel();break;
                        case 1:
                            Capture("city_night_01_employees");
                            var button=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).First(b=>b.GetComponentInChildren<Text>()?.text=="调整班次");button.onClick.Invoke();break;
                        case 2:
                            Capture("city_night_02_custom_shift");game.Ui.CloseAllWindows();
                            game.Session.Player.Energy=100;
                            var employee=game.Session.MainShop.Employee;
                            if(!employee.TryShift(game.Session.Clock.Now.TotalMinutes,out _,out long end))throw new InvalidOperationException("Night employee not on duty");
                            game.Session.AdvanceClock((int)(end-game.Session.Clock.Now.TotalMinutes));game.Ui.OpenEmployeesPanel();break;
                        case 3:
                            Capture("city_night_03_paid_overtime");game.Ui.CloseAllWindows();game.StartCityLifePreset("night");game.Session.AdvanceClock(210);break;
                        case 4:Capture("city_night_04_energy_warning");game.Ui.OpenCityAcceptance();break;
                        case 5:Capture("city_night_05_test_presets");break;
                        default:
                            Debug.Log("CITY ENERGY PAYROLL MAIN PLAY RESULT errors="+_errors+" scene="+gameObjectScene(game));
                            SessionState.SetBool(Key+"Energy",false);SessionState.SetBool(Key,false);EditorApplication.Exit(_errors==0?0:1);return;
                    }
                    _step++;_next=now+2;return;
                }
                switch(_step)
                {
                    case 0:game.Ui.CloseAllWindows();game.StartCityLifePreset("staff");game.Ui.QuestPopups=false;game.Ui.OpenCityFinanceOverview();break;
                    case 1:Capture("city_main_play_overview");game.Ui.CloseAllWindows();game.Ui.OpenCityMap();break;
                    case 2:Capture("city_main_play_map");game.Ui.CloseAllWindows();game.Ui.OpenCityAcceptance();break;
                    case 3:Capture("city_main_play_test_presets");game.Ui.CloseAllWindows();game.StartCityLifePreset("clothing");game.Ui.OpenCityWardrobe();break;
                    case 4:Capture("city_main_play_wardrobe");game.Ui.CloseAllWindows();game.Ui.OpenCityClimate();break;
                    case 5:Capture("city_main_play_climate");break;
                    default:
                        Debug.Log("CITY MAIN PLAY RESULT errors="+_errors+" scene="+gameObjectScene(game));
                        SessionState.SetBool(Key,false);EditorApplication.Exit(_errors==0?0:1);return;
                }
                _step++;_next=now+2;
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
        private static string gameObjectScene(GameController game)=>game.gameObject.scene.path;
        private static void Capture(string name)
        {
            string dir=Path.GetFullPath("docs/previews");Directory.CreateDirectory(dir);
            var game=UnityEngine.Object.FindObjectOfType<GameController>();var cam=game.Cam;
            foreach(var canvas in game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;}
            var rt=new RenderTexture(1280,720,24);var oldTarget=cam.targetTexture;var oldActive=RenderTexture.active;
            cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();cam.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(dir,name+".png"),texture.EncodeToPNG());
            cam.targetTexture=oldTarget;RenderTexture.active=oldActive;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(texture);
        }
    }
}
