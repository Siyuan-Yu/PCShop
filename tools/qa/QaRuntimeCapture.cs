// 复制到隔离工程 Assets/Editor 执行；准备状态后用真实 UI 回调操作，不修改主项目场景和正式存档。
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
    static string worker="qa_runtime_worker";
    static QaRuntimeCapture(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}};}
    public static void Run(){Directory.CreateDirectory(Output);SessionState.SetBool(Key,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
    static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Path.Combine(Output,"errors.txt"),message+"\n"+stack+"\n");}}
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,F).Invoke(target,args);
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);File.AppendAllText(Path.Combine(Output,"checks.txt"),"通过："+message+"\n");}
    static Button Button(string name,bool caption=false)=>game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).First(b=>caption?b.GetComponentInChildren<Text>()?.text==name:b.name==name);
    static void Click(string name,bool caption=false)
    {
        var button=Button(name,caption);Check(button.IsActive()&&button.IsInteractable(),"按钮可操作 "+name);
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        game.Ui.Update(.5f);Canvas.ForceUpdateCanvases();
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
                var s=game.Session;s.EventsEnabled=false;s.DebugAddCash(200000000);s.SetSmartphone(true);s.DebugRentStarterShop(true);s.DebugSetHomeWorkbench(true);
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
                File.WriteAllText(Path.Combine(Output,"result.txt"),"完成实机界面回调巡检；错误="+errors+"；准备状态，不是从新档通关；1366 图为渲染目标尺寸，不等于独立窗口分辨率验收。\n");
                SessionState.SetBool(Key,false);EditorApplication.Exit(errors==0?0:1);return;
            }
            stage++;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Output,"failure.txt"),"stage="+stage+"\n"+e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
}
