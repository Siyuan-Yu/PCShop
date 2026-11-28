// 复制到隔离工程 Assets/Editor 执行；准备状态后用真实 UI 回调操作，不修改主项目场景和正式存档。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.Common;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[InitializeOnLoad]
public static class QaRuntimeCapture
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Key="PCShop.QaRuntimeCapture";
    static string Output=>Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS");
    static GameController game;
    static int errors;
    static double next;
    static int stage;
    static string officeDestination;
    static string worker="qa_runtime_worker";
    static QaRuntimeCapture(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}};}
    public static void Run(){CityMapBaker.EnsureBaked(false);Directory.CreateDirectory(Output);SessionState.SetBool(Key,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
    static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Path.Combine(Output,"errors.txt"),message+"\n"+stack+"\n");}}
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,F).Invoke(target,args);
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);File.AppendAllText(Path.Combine(Output,"checks.txt"),"通过："+message+"\n");}
    static Button Button(string name,bool caption=false)=>game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).First(b=>caption?b.GetComponentInChildren<Text>()?.text==name:b.name==name);
    static InputField ActiveInput()=>game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<InputField>()).Single();
    static void Enter(string id)
    {
        var s=game.Session;var place=s.City.GetPlace(id);
        game.Ui.CloseAllWindows();s.StopTrip();s.Leave();s.ArriveAtNode(place.NodeId);
        Check(s.Enter(place),"进入 "+place.Name);
        game.Ui.OnEnteredPlace(place);game.Step(0);game.SnapCamera();
    }
    static void Click(string name,bool caption=false)
    {
        var button=Button(name,caption);Check(button.IsActive()&&button.IsInteractable(),"按钮可操作 "+name);
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        game.Ui.Update(.5f);Canvas.ForceUpdateCanvases();
    }
    static bool HasText(string fragment) => game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>())
        .Any(t=>t.text.Contains(fragment));
    static void AdvanceTo(long minute)
    {
        var s=game.Session;
        while (s.Clock.Now.TotalMinutes<minute)
        {
            s.Player.Health=s.Player.Hunger=s.Player.Energy=100;
            long previous=s.Clock.Now.TotalMinutes;
            s.AdvanceClock((int)Math.Min(30,minute-previous));
            if (s.Clock.Now.TotalMinutes<=previous) throw new InvalidOperationException("营业时钟停止推进");
        }
    }
    static void WalkToOfficeReception()
    {
        var visual=game.World.Places[game.Session.CurrentPlace.Id];
        var transform=visual.Root.transform;
        int steps=Mathf.RoundToInt((float)visual.Place.Width*.25f/.31f);
        for(int i=0;i<steps;i++) Call(game,"WalkOffice",-transform.right,.1f);
        for(int i=0;i<12;i++)
        {
            if(game.TryGetInteract(out _,out string label) && label=="写字楼前台") return;
            Call(game,"WalkOffice",transform.forward,.1f);
        }
        throw new InvalidOperationException("走到左侧接待台后未出现前台交互");
    }
    static void Shot(string name,int width=1920,int height=1080)
    {
        foreach(var c in game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=game.Cam;c.planeDistance=1;}
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);var previous=RenderTexture.active;
        game.Cam.targetTexture=rt;
        Canvas.ForceUpdateCanvases();game.Ui.Update(.5f);Canvas.ForceUpdateCanvases();
        foreach(var fit in UnityEngine.Object.FindObjectsOfType<WindowFit>())fit.Fit();Canvas.ForceUpdateCanvases();
        game.Cam.Render();game.Cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        game.Cam.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
        File.WriteAllLines(Path.Combine(Output,name+"-文字.txt"),game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Select(t=>t.text));
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.3;
        try
        {
            if(stage==0 && game==null)
            {
                SaveStore.DirectoryOverride=Path.Combine(Output,"saves");
                game=new GameObject("测试实机界面").AddComponent<GameController>();game.Bootstrap(false,914);game.enabled=false;
                var s=game.Session;s.EventsEnabled=false;game.Ui.QuestPopups=false;s.DebugAddCash(200000000);s.SetSmartphone(true);s.DebugRentStarterShop(true);s.DebugSetHomeWorkbench(true);
                ((DistrictProgress)typeof(GameSession).GetField("_district",F).GetValue(s)).UnlockedTier=3;s.Time.SetPaused(true);
                s.StopTrip();s.Leave();s.ArriveAtNode(s.PlayerShopPlace.NodeId);s.Enter(s.PlayerShopPlace);
                s.MainShop.Employee=new Employee{Id=worker,Name="测试员工",DailyWageYuan=120,ShiftStart=540,ShiftEnd=1020,Intelligence=65,Dexterity=65,Stamina=65,Charm=65};
                s.EmployeeWellbeingOf(worker);game.Step(0);game.Ui.CloseAllWindows();game.Ui.CloseDock();game.SnapCamera();
                // 让正式 Canvas 更新完整一帧，避免把首次布局尚未稳定的缩放当作 UI 故障。
                return;
            }
            else if(stage==0){Shot("01-店内实机");game.Ui.OpenEmployeesPanel();}
            else if(stage==1){Shot("02-员工名单");Click("EmployeePay_"+worker);}
            else if(stage==2)
            {
                Shot("03-员工调薪");var edit=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<InputField>()).Single(i=>i.transform.parent.parent.name=="EmployeeInline_pay");
                edit.text="180";Click("保存日薪",true);var e=game.Session.FindEmployee(worker).Person;
                Check(e.DailyWageYuan==180||e.Wellbeing.PendingWage==180,"调薪按钮已修改员工状态");
                game.Ui.CloseAllWindows();Call(game.Ui,"OpenCompanyGovernance","rival_pc");
            }
            else if(stage==3){Shot("04-公司股权");Click("董事会",true);}
            else if(stage==4){Shot("05-董事会");Click("控制权与门店",true);}
            else if(stage==5){Shot("06-公司控制权");game.Ui.CloseAllWindows();game.Ui.OpenCityInvestments();}
            else if(stage==6){Shot("07-证券");game.Ui.CloseAllWindows();var s=game.Session;s.Leave();s.ArriveAtNode(s.Home.NodeId);s.Enter(s.Home);game.Step(0);game.Ui.CloseAllWindows();game.Ui.OpenHomeWorkbench();}
            else if(stage==7){Check(Button("WorkbenchTab_0")!=null,"居家工作台打开");Shot("08-工作台今日");Click("WorkbenchTab_1");}
            else if(stage==8){Shot("09-工作台门店");Click("WorkbenchTab_2");}
            else if(stage==9){Shot("10-工作台投资");Click("WorkbenchTab_3");}
            else if(stage==10){Shot("11-工作台财务");Click("WorkbenchTab_4");}
            else if(stage==11){Shot("12-工作台联系人");Shot("13-工作台联系人-1366",1366,768);game.Ui.CloseAllWindows();Check(!game.Session.Time.IsHeld,"关闭全部界面后解除界面暂停");}
            else if(stage==12)
            {
                for(int i=0;i<20;i++){game.Ui.OpenCityInvestments();game.Ui.Update(.5f);game.Ui.CloseAllWindows();}
                Check(!game.Session.Time.IsHeld,"重复开关证券 20 次未泄漏暂停锁");
                var s=game.Session;var company=s.GovernanceOf("rival_pc");
                s.Clock.Restore(new GameDate(0),600);
                foreach(var lesson in new[]{"first_meal","rent","mall","place","shelf","stock","sale","hire"}) s.Flags.Add("tutorial:"+lesson);
                long first=s.ShareholderShares(company,"founder:rival_pc");
                Check(s.TradeCompanyBlock(company.Ticker,"founder:rival_pc",first,true),"向创始人买入股份");
                Check(s.TradeCompanyBlock(company.Ticker,"capital_qing",company.TotalShares/2+1-first,true),"向机构买入至过半");
                Call(game.Ui,"OpenCompanyGovernance",company.Ticker);
                Click("控制权与门店",true);
                Click("发起接管…",true);
                ActiveInput().text="29876";
                Click("你",true);
                Check(ActiveInput().text=="29876","选择新经理后保留已填预算");
                Shot("14-接管确认表单");
                Click("确认接管…",true);Click("确认接管",true);
                Check(company.HandoverAt>s.Clock.Now.TotalMinutes,"接管确认排入交接");
                game.Ui.CloseAllWindows();
                s.Player.Hunger=s.Player.Energy=s.Player.Health=100;
                s.AdvanceClock((int)(company.HandoverAt-s.Clock.Now.TotalMinutes));
                Check(company.ControllerId=="player"&&company.ManagerId=="player"&&company.DailyBudgetCents==2987600,"实际交接使用确认的经理和预算");
                s.Clock.Restore(s.Clock.Today,600);
                for(int i=0;i<5;i++)game.Ui.Update(.5f);
                Check(HasText("公司交接完成") && HasText("继续由你经营的个人门店"),"交接完成后自动显示经营归属");
                Check(HasText("这家同行公司的经营按整体规模结算"),"同行公司清楚说明经营方式");
                Shot("15-接管首日交接通知");
                Check(HasText("预计路程"),"显示预计路程，方便选择出行方式");
                var officeButton=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>())
                    .First(b=>b.GetComponentInChildren<Text>()?.text is string text && text.StartsWith("前往") && text.EndsWith("写字楼"));
                Click(officeButton.GetComponentInChildren<Text>().text,true);
                officeDestination=s.CityIntent?.TargetPlaceId??s.CurrentPlace?.Id;
                Check(company.ReviewedPlayerHandoverAt==company.LastPlayerHandoverAt,"看过交接通知后记录已读");
                Check(s.City.HasPlace(officeDestination)&&s.City.GetPlace(officeDestination).Model=="office_tower","通知按钮启动真实写字楼路线");
            }
            else if(stage==13)
            {
                var s=game.Session;
                for(int i=0;i<3600 && s.CurrentPlace?.Id!=officeDestination;i++)
                {
                    s.Player.Health=s.Player.Hunger=s.Player.Energy=100;
                    game.Step(.1f);
                }
                Check(s.CurrentPlace?.Id==officeDestination,"按实际街道路由到达并进入写字楼");
                var id=s.CurrentPlace.Id;
                var visual=game.World.Places[id];var interior=visual.VenueInterior;
                Check(interior!=null&&interior.activeInHierarchy&&interior.transform.parent!=visual.Root.transform,"办公大厅切换到室内渲染");
                WalkToOfficeReception();
                game.Step(0);game.SnapCamera();
                Shot("16-走到写字楼前台");
                Call(game,"InteractOffice");
                Check(HasText("兴华电脑城")&&HasText("门店和用工按整体经营规模结算"),"接管公司的前台业务正确定位");
                Click("查看经营与门店",true);
                Check(HasText("经营规模"),"前台打开同行公司的真实经营数据");
                game.Ui.CloseAllWindows();
            }
            else if(stage==14)
            {
                var s=game.Session;
                Check((bool)Call(s,"CreateBrandAndCompany","computer","街角电脑", "value",new[]{s.MainShop.Place.Id}),"准备有门店和员工的公司");
                game.Ui.OpenEmployeesPanel();Click("ManagementCompanies");
                Check(HasText("写字楼")&&HasText("控股与参股公司")&&HasText("街角电脑"),"经营内能同时找到写字楼、自营公司和控股公司");
                Shot("17-经营公司与写字楼");
                string currentOffice=s.CurrentPlace.Name+" · 当前所在";
                Click(currentOffice,true);
                Call(game.Ui,"OpenNoticeHistory");
                Check(HasText("兴华电脑城 · 查看交接结果"),"可从通知记录重开交接结果");
                game.Ui.CloseAllWindows();
                // 经营页已经选中实体公司，返回当前写字楼时沿用这家公司的前台上下文。
                game.Ui.OpenEmployeesPanel();Click("ManagementCompanies");
                Click(currentOffice,true);
                Call(game,"InteractOffice");
                Check(HasText("街角电脑")&&HasText("你可经营"),"前台展示实际门店与管理权限");
                Shot("18-前台查看门店与人事");
                Click("任命人事负责人",true);
                var workerButton=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>())
                    .First(b=>b.GetComponentInChildren<Text>()?.text.StartsWith("测试员工\n")==true);
                Click(workerButton.GetComponentInChildren<Text>().text,true);
                Click("确认原店空缺",true);Click("确认任命",true);
                var company=s.Companies.Single();
                long due=company.PendingPosts.Single().EffectiveAt;
                Check(!Button("核准招聘",true).IsInteractable(),"到任前明确锁定招聘");
                game.Ui.CloseAllWindows();
                AdvanceTo(due);
                Call(game,"InteractOffice");
                Check(HasText("人事负责人　测试员工"),"次班到任后前台显示负责人");
                Click("核准招聘",true);
                Click(s.ShopTitle(s.MainShop)+$" · {s.StaffCommitted(s.MainShop)}/{GameSession.StaffLimit(s.MainShop.Place.Size)}",true);
                Check(s.RecruitmentNoticePosted&&s.RecruitmentInterviewPlace==s.MainShop.Place,"前台核准实际门店招聘与面试地点");
                Shot("19-前台已核准招聘");
                game.Ui.CloseAllWindows();
            }
            else if(stage==15)
            {
                var s=game.Session;
                Enter(s.MainShop.Place.Id);game.Ui.CloseAllWindows();
                Check(s.RecruitmentNoticePosted,"保留写字楼已核准的招聘告示");
                AdvanceTo(GameTimestamp.From(s.Clock.Today.AddDays(1),GameSession.CandidateMinute+1).TotalMinutes);
                game.Ui.CloseAllWindows();game.Ui.OpenDock();Click("员工",true);
                Check(s.Candidates.Count>0,"招聘告示次日有应聘者");
            }
            else if(stage==16)
            {
                int count=game.Session.Candidates.Count;
                Click("聘用并安排工作",true);
                var roles=game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<RectTransform>()).Single(t=>t.name=="RecruitRoleChoices");
                var cashier=roles.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>()?.text.Contains("收银")??false);
                Click(cashier.GetComponentInChildren<Text>().text,true);
                Shot("20-门店招聘与岗位");
                Click("确认聘用并安排工作",true);
                Check(game.Session.Candidates.Count==count-1,"招聘按钮聘用候选人并安排岗位");
            }
            else if(stage==17)
            {
                const string person="lin";
                var s=game.Session;
                Enter(s.Home.Id);game.Ui.CloseAllWindows();
                var state=s.CityPersonState(person);state.Route=null;state.LocationPlaceId=s.Home.Id;
                game.Ui.OpenPersonDialogue(person);Click("聊一聊",true);
                if(!s.HasStartedRelationshipStory(person)) Click("聊一聊",true);
                Check(s.HasStartedRelationshipStory(person),"对话开启林远航的故事");
                game.Ui.CloseAllWindows();
                s.Clock.Restore(s.Clock.Today.AddDays(1),600);
                game.Ui.OpenPersonDialogue(person);Click("RelationshipStory_Invite");
                Shot("21-人物邀约");
                Click("约好时间",true);
                Check(s.RelationshipStoryAppointmentId(person)!=null,"选择约好时间后写入实际日程");
            }
            else if(stage==18)
            {
                File.WriteAllText(Path.Combine(Output,"result.txt"),"完成实机界面回调巡检；错误="+errors+"；准备状态，不是从新档通关；1366 图为渲染目标尺寸，不等于独立窗口分辨率验收。\n");
                SessionState.SetBool(Key,false);EditorApplication.Exit(errors==0?0:1);return;
            }
            stage++;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Output,"failure.txt"),"stage="+stage+"\n"+e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
}
