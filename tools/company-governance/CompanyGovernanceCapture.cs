// 隔离工程的界面复拍。仅准备展示状态，不运行测试框架、成交模拟或跨日模拟。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CompanyGovernanceCapture
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    const string Pending="PCShop.CompanyGovernanceCapture";
    static string Output=>Environment.GetEnvironmentVariable("PCSHOP_GOVERNANCE_OUTPUT");
    static CompanyGovernanceCapture(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Capture;};}
    public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static object Invoke(object target,string method,params object[] args)=>target.GetType().GetMethod(method,F).Invoke(target,args);
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,F).SetValue(target,value);
    static Window Last(GameUi ui)=>((List<Window>)typeof(GameUi).GetField("_windows",F).GetValue(ui)).Last(w=>!w.Closed);
    static void Open(GameController game,string ticker,int page,string file)
    {game.Ui.CloseAllWindows();Set(game.Ui,"_governancePage",page);Invoke(game.Ui,"OpenCompanyGovernance",ticker);Photo(game,file);}
    static void Photo(GameController game,string name)
    {
        Canvas.ForceUpdateCanvases();foreach(var scroll in UnityEngine.Object.FindObjectsOfType<ScrollRect>())typeof(ScrollRect).GetMethod("LateUpdate",F).Invoke(scroll,null);Canvas.ForceUpdateCanvases();
        var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);var prior=RenderTexture.active;
        game.Cam.targetTexture=rt;game.Cam.Render();game.Cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);game.Cam.targetTexture=null;RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);
        File.WriteAllLines(Path.Combine(Output,name+"-文字.txt"),UnityEngine.Object.FindObjectsOfType<Text>().Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.text));
    }
    static OwnedShop ExtraShop(GameSession session)
    {
        var place=session.CityCommercialProperties.First(p=>session.ShopAt(p.Id)==null&&p.Kind==PlaceKind.Shop&&session.DistrictTierOf(p)==0);
        Invoke(session,"OpenNewShop",place,Money.Zero,Money.Zero);var shop=session.ShopAt(place.Id);shop.RetailKind="computer";return shop;
    }
    static void Holdings(GameSession s,CompanyGovernance g,long player,long founder,long institution)
    {
        g.Holders=new[]{new CompanyShareholder{OwnerId="player",Shares=player},new CompanyShareholder{OwnerId="founder:"+g.Ticker,Shares=founder},
            new CompanyShareholder{OwnerId="capital_xing",Shares=institution},new CompanyShareholder{OwnerId="public",Shares=g.TotalShares-player-founder-institution}};
        if(s.StockState(g.Ticker)!=null)s.StockState(g.Ticker).Units=player;
    }
    static void Capture()
    {
        try
        {
            SessionState.SetBool(Pending,false);Directory.CreateDirectory(Output);SaveStore.DirectoryOverride=Path.Combine(Output,"临时存档");
            var game=new GameObject("公司治理界面复拍").AddComponent<GameController>();game.Bootstrap(false,914);game.enabled=false;
            var s=game.Session;s.EventsEnabled=false;s.DebugAddCash(200000000);s.SetSmartphone(true);s.DebugSetHomeWorkbench(true);s.DebugRentStarterShop(true);s.Time.SetPaused(true);
            var district=(DistrictProgress)typeof(GameSession).GetField("_district",F).GetValue(s);district.UnlockedTier=3;
            s.StopTrip();s.Leave();s.ArriveAtNode(s.PlayerShopPlace.NodeId);s.Enter(s.PlayerShopPlace);game.Step(0);game.Ui.CloseAllWindows();game.Ui.CloseDock();game.SnapCamera();
            foreach(var canvas in game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=game.Cam;canvas.planeDistance=1;}
            game.Cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var npc=s.GovernanceOf("rival_pc");Holdings(s,npc,80000,450000,200000);
            Open(game,npc.Ticker,0,"01-股东名册与大宗交易");
            npc.Resolutions=new[]{new CompanyResolution{Id=1,Kind=CompanyResolutionKind.Director,CreatedAt=s.Clock.Now.TotalMinutes,DueAt=s.Clock.Now.TotalMinutes+1440,PlayerVote=CompanyVote.Support}};
            Open(game,npc.Ticker,1,"02-董事提名与按股表决");
            npc.PlayerDirector=true;npc.Resolutions=new[]{new CompanyResolution{Id=2,Kind=CompanyResolutionKind.Budget,Value=3000000,CreatedAt=s.Clock.Now.TotalMinutes,DueAt=s.Clock.Now.TotalMinutes+1440,PlayerVote=CompanyVote.Support}};
            Open(game,npc.Ticker,1,"03-董事会经营议案");
            Invoke(game.Ui,"OpenGovernanceProposal",Last(game.Ui),npc,CompanyResolutionKind.Issue);Photo(game,"04-增发认购与稀释预览");
            Holdings(s,npc,550000,250000,100000);npc.PlayerDirector=true;npc.Resolutions=Array.Empty<CompanyResolution>();
            Open(game,npc.Ticker,2,"05-控股后的主动接管");
            Invoke(game.Ui,"OpenCompanyTakeover",Last(game.Ui),npc);Photo(game,"12-接管经理与预算选择");
            var main=s.MainShop;var second=ExtraShop(s);second.CustomName="社区演示分店";var personal=ExtraShop(s);personal.CustomName="个人创业店";
            Invoke(s,"CreateBrandAndCompany","computer","街坊电脑","value",new[]{main.Place.Id,second.Place.Id});
            var own=s.Companies.First();var g=s.GovernanceOf(s.CompanyTicker(own));own.ListedDay=0;own.ListingPriceCents=400;own.ReserveCents=50000000;
            Invoke(s,"EnsureCompanyTicker",own);g.TotalShares=1250000;
            g.Holders=new[]{new CompanyShareholder{OwnerId="player",Shares=575000},new CompanyShareholder{OwnerId="capital_xing",Shares=650000},new CompanyShareholder{OwnerId="public",Shares=25000}};
            s.StockState(g.Ticker).Units=575000;g.ControllerId=g.ManagerId="player";g.PendingControllerId=g.PendingManagerId="capital_xing";g.HandoverAt=(s.Clock.Now.TotalMinutes/1440+1)*1440;g.RetentionOfferShopId=main.Place.Id;
            Open(game,g.Ticker,2,"06-反向控股与门店留任邀请");
            g.ControllerId=g.ManagerId="capital_xing";g.HandoverAt=-1;g.PendingControllerId=g.PendingManagerId=null;g.RetainedShopIds=new[]{main.Place.Id};
            Open(game,g.Ticker,2,"07-交接后保留指定门店经营");
            game.Ui.CloseAllWindows();game.Ui.OpenStoreManagement();
            var view=typeof(GameUi).GetField("_managementShops",F).GetValue(game.Ui);Invoke(view,"Select",second.Place.Id);Last(game.Ui).Rebuild();Photo(game,"08-公司接管门店只读界面");
            game.Ui.CloseAllWindows();Invoke(game.Ui,"OpenRegisteredCompany",own.BrandId);Set(game.Ui,"_companyPage",4);Last(game.Ui).Rebuild();Photo(game,"09-公司页内嵌股权治理");
            g.HandoverAt=(s.Clock.Now.TotalMinutes/1440+1)*1440;g.PendingControllerId=g.PendingManagerId="capital_xing";
            game.Ui.CloseAllWindows();s.Leave();s.ArriveAtNode(s.Home.NodeId);s.Enter(s.Home);game.Step(0);Set(game.Ui,"_workbenchTab",0);game.Ui.OpenHomeWorkbench();Photo(game,"10-居家工作台交接待办");
            district.UnlockedTier=2;Open(game,npc.Ticker,0,"11-花园开放前的权限提示");
            File.WriteAllText(Path.Combine(Output,"展示状态说明.txt"),"本组为 Unity 原始界面帧。持股、议案、董事席位及交接状态均为隔离预览准备的展示数据。\n未运行测试框架、未进行真实交易／跨日经营／保存重载验证，未修改正式存档。\n");
            Debug.Log("COMPANY_GOVERNANCE_UI_CAPTURE_DONE");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
