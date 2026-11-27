// 独立预览工程的界面复拍入口。只准备展示状态，不运行测试框架或正式存档。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EmployeeWellbeingCapture
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    const string Pending="PCShop.EmployeeWellbeingCapture";
    static string Output=>Environment.GetEnvironmentVariable("PCSHOP_EMPLOYEE_OUTPUT");
    static EmployeeWellbeingCapture(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Capture;};}
    public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Set(GameUi ui,string field,object value)=>typeof(GameUi).GetField(field,F).SetValue(ui,value);
    static Window Window(GameUi ui)=>(Window)typeof(GameUi).GetField("_managementWindow",F).GetValue(ui);
    static void Photo(GameController game,string name)
    {
        Canvas.ForceUpdateCanvases();foreach(var scroll in UnityEngine.Object.FindObjectsOfType<ScrollRect>())typeof(ScrollRect).GetMethod("LateUpdate",F).Invoke(scroll,null);Canvas.ForceUpdateCanvases();
        var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);var prior=RenderTexture.active;
        game.Cam.targetTexture=rt;game.Cam.Render();game.Cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);game.Cam.targetTexture=null;RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);
        File.WriteAllLines(Path.Combine(Output,name+"-文字.txt"),UnityEngine.Object.FindObjectsOfType<Text>().Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.text));
    }
    static Employee Person(GameSession s,string id,string person,string name,int pay,int morale,int energy,EmployeeRequestKind kind)
    {
        var e=new Employee {Id=id,PersonId=person,Name=name,DailyWageYuan=pay,CheckoutMinutes=3,Dexterity=65,Intelligence=68,Stamina=62,Charm=58};
        e.Wellbeing=new EmployeeWellbeing {JoinedAt=s.Clock.Now.TotalMinutes,LastTickAt=s.Clock.Now.TotalMinutes,StartingWage=pay,ExpectedWage=pay,Satisfaction=morale,Energy=energy,CompletedShifts=6,LongShiftStreak=4,
            Changes=new[]{new EmployeeWellbeingChange{At=s.Clock.Now.TotalMinutes,Delta=morale<60?-2:2,Reasons=new[]{morale<60?"连续长班 4 班":"班次合理",morale<30?"日薪低于岗位期望":"两班间休息充足"}}}};
        if(kind!=EmployeeRequestKind.None)e.Wellbeing.Request=new EmployeeWorkRequest {Kind=kind,Reason=kind==EmployeeRequestKind.Pay?"升任后薪资未调整":"连续长班，希望缩短晚班",CreatedAfterShift=4,DueAfterShift=8,TargetWage=160};
        return e;
    }
    static void Tool(GameController game,Employee employee,string tool,string file)
    {typeof(GameUi).GetMethod("SelectEmployeeTool",F).Invoke(game.Ui,new object[]{employee,tool,Window(game.Ui)});Window(game.Ui).Rebuild();Photo(game,file);}
    static void Capture()
    {
        try
        {
            SessionState.SetBool(Pending,false);Directory.CreateDirectory(Output);SaveStore.DirectoryOverride=Path.Combine(Output,"临时存档");
            var game=new GameObject("员工界面复拍").AddComponent<GameController>();game.Bootstrap(false,914);game.enabled=false;
            var s=game.Session;s.EventsEnabled=false;s.DebugAddCash(2000000);s.SetSmartphone(true);s.DebugSetHomeWorkbench(true);s.DebugRentStarterShop(true);s.Time.SetPaused(true);
            s.StopTrip();s.Leave();s.ArriveAtNode(s.PlayerShopPlace.NodeId);s.Enter(s.PlayerShopPlace);
            var qiao=Person(s,"employee_demo_qiao","life_qiao","乔禾",120,48,58,EmployeeRequestKind.ShorterShift);
            var ning=Person(s,"employee_demo_ning","life_ning","顾宁",180,85,92,EmployeeRequestKind.None);
            var ming=Person(s,"employee_demo_ming","life_ming","许明",100,23,24,EmployeeRequestKind.Pay);ming.Wellbeing.ExpectedWage=160;ming.Wellbeing.NoticeAfterShift=8;
            s.MainShop.Employee=qiao;s.MainShop.Assembler=ning;s.MainShop.Buyer=ming;game.Step(0);game.Ui.CloseAllWindows();game.Ui.CloseDock();game.SnapCamera();
            foreach(var c in game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=game.Cam;c.planeDistance=1;}
            game.Cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            game.Ui.OpenEmployeesPanel();Set(game.Ui,"_employeeFocus",qiao.Id);Window(game.Ui).Rebuild();Photo(game,"01-个人员工-满意度与请求");
            Tool(game,qiao,"pay","02-同屏调薪");Tool(game,qiao,"shift","03-同屏调班");Tool(game,qiao,"leave","04-同屏休假");
            Set(game.Ui,"_employeeToolId",null);Set(game.Ui,"_employeeFocus",ming.Id);Window(game.Ui).Rebuild();Photo(game,"05-离职预告");
            Set(game.Ui,"_employeeFocus",qiao.Id);s.RespondEmployeeRequest(qiao.Id,EmployeeRequestResponse.Promised);Window(game.Ui).Rebuild();Photo(game,"06-同意请求-等待执行");
            File.WriteAllText(Path.Combine(Output,"展示状态说明.txt"),"这些界面使用临时预览存档中的展示员工，满意度及请求为准备的展示状态。\n同意调班后的满意度："+qiao.Wellbeing.Satisfaction+"；请求状态："+qiao.Wellbeing.Request.Response+"。\n未运行测试框架或自动长线模拟。\n");
            typeof(GameSession).GetMethod("CreateBrandAndCompany",F).Invoke(s,new object[]{"computer","街坊电脑","value",new[]{s.MainShop.Place.Id}});
            var company=s.Companies.First();
            typeof(GameSession).GetMethod("MoveEmployeeWork",F).Invoke(s,new object[]{s.FindEmployee(ning.Id),null,EmployeeRole.Unassigned});
            company.Posts=new[]{new CompanyPost{EmployeeId=ning.Id,Department=CompanyDepartment.Personnel,Leader=true}};
            game.Ui.CloseAllWindows();typeof(GameUi).GetMethod("ShowEmployeeProfile",F).Invoke(game.Ui,new object[]{qiao.Id});Photo(game,"07-公司员工-共用满意度");
            Set(game.Ui,"_companyAttentionOnly",true);Set(game.Ui,"_companyPerson",ming.Id);Window(game.Ui).Rebuild();Photo(game,"08-公司员工-请求筛选");
            Set(game.Ui,"_companyPage",0);Window(game.Ui).Rebuild();Photo(game,"09-公司总览-员工提醒");
            game.Ui.CloseAllWindows();s.Leave();s.ArriveAtNode(s.Home.NodeId);s.Enter(s.Home);game.Step(0);Set(game.Ui,"_workbenchTab",0);game.Ui.OpenHomeWorkbench();Photo(game,"10-居家工作台-员工待办");
            Debug.Log("EMPLOYEE_WELLBEING_UI_CAPTURE_DONE");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
