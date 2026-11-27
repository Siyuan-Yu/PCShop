// 安装到独立预览工程的 Assets/Editor。仅编译、摆拍和真实界面复拍，不调用测试框架。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PetItemArtCapture
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    const string Pending = "PCShop.PetItemArtCapture";
    static string Output => Environment.GetEnvironmentVariable("PCSHOP_PET_ITEM_OUTPUT");
    [Serializable] class Catalog { public Sheet[] sheets; }
    [Serializable] class Sheet { public string texture; public string[] items; }
    static PetItemArtCapture() { EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false)) EditorApplication.delayCall += Capture; }; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static Type Models => typeof(Prims).Assembly.GetType("PCShopSell.Game.Runtime.PetSceneModels");
    static GameObject Animal(Transform parent, bool dog, int coat)
    {
        var method = Models.GetMethods(F).Single(m => m.Name == "BuildAnimal" && m.GetParameters()[2].ParameterType == typeof(int));
        return (GameObject)method.Invoke(null, new object[] { parent, dog, coat, null, null, null });
    }
    static void CameraAt(Camera camera, Vector3 point, float pitch, float size)
    {
        camera.orthographic = true; camera.orthographicSize = size; camera.aspect = 16f / 9;
        camera.transform.rotation = Quaternion.Euler(pitch, 0, 0); camera.transform.position = point - camera.transform.forward * 30;
    }
    static void PetCamera(GameController game)
    {
        var camera = game.Cam;camera.orthographic=true;camera.orthographicSize=game.Interior.PetCameraSize;camera.aspect=16f/9;
        camera.transform.rotation=Quaternion.Euler(43,CameraRig.DefaultYaw,0);camera.transform.position=game.Interior.PetHouseWorld-camera.transform.forward*30;
    }
    static void Photo(Camera camera, string name, int width = 1920, int height = 1080)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var scroll in UnityEngine.Object.FindObjectsOfType<ScrollRect>()) typeof(ScrollRect).GetMethod("LateUpdate", F).Invoke(scroll, null);
        Canvas.ForceUpdateCanvases();
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
        var previous = RenderTexture.active; camera.targetTexture = rt; camera.Render(); camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image); camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
    }
    static void Audit(Catalog catalog)
    {
        var known = new HashSet<string>(catalog.sheets.SelectMany(s => s.items).Where(id => !string.IsNullOrEmpty(id)));
        var rows = new List<(string key, string label)>();
        rows.AddRange(Gifts.All.Select(x => (x.Id, x.Name)));
        rows.AddRange(OldMarketCatalog.Gifts.Select(x => (x.Id, x.Name)));
        rows.AddRange(new[] { RetailProducts.CompanyApparel, RetailProducts.CompanyModel }.Select(x => (x.Id, x.Name)));
        rows.AddRange(FurnitureCatalog.All.Select(x => ("furn:" + x.Id, x.Name)));
        rows.AddRange(PartCatalog.All.Select(x => ("part:" + x.Id + ":box", x.Name)));
        rows.AddRange(FishingCatalog.Fish.Select(x => (x.Id, x.Name)));
        rows.AddRange(Enumerable.Range(0, 8).Select(i => ("used:" + i, "二手物品 " + i)));
        // 事件物品也逐一覆盖，包括全部灯笼和图书。
        foreach (var type in new[] { typeof(GameSession).Assembly.GetType("PCShopSell.Core.Session.LifeEventGoods"), typeof(GameSession).Assembly.GetType("PCShopSell.Core.Session.NeighborhoodGoods") })
            if (type != null) foreach (var field in type.GetFields(F).Where(f => f.IsStatic))
            {
                object value = field.GetValue(null);
                if (value is GiftDef gift) rows.Add((gift.Id, gift.Name));
                if (value is IEnumerable<GiftDef> gifts) rows.AddRange(gifts.Select(x => (x.Id, x.Name)));
            }
        var normalize = typeof(GameUi).GetMethod("ItemArtId", F);
        var report = new List<string> { "原始键\t物品\t归一键\t命中" };
        foreach (var row in rows.Distinct())
        {
            string id = (string)normalize.Invoke(null, new object[] { row.key });
            report.Add(row.key + "\t" + row.label + "\t" + id + "\t" + (known.Contains(id) ? "是" : "缺图"));
        }
        File.WriteAllLines(Path.Combine(Output, "商品映射核对.tsv"), report);
    }
    static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
    }
    static void Icons(GameController game, Catalog catalog)
    {
        var canvas = new GameObject("商品图标实际组件复拍", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = game.Cam; canvas.planeDistance = 1; canvas.sortingOrder = 500;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        foreach (var sheet in catalog.sheets)
        {
            var paper = Rect(canvas.transform, "纸面", 0, 0, 1920, 1080); paper.gameObject.AddComponent<Image>().color = ArtDirection.Hex("EFECE6");
            var title = Ui.Label(paper, "商品图标 · " + sheet.texture + "    88 / 52 / 34 px", 28, color: ArtDirection.Ink);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0, 1); title.rectTransform.pivot = new Vector2(0, 1); title.rectTransform.anchoredPosition = new Vector2(24, -12); title.rectTransform.sizeDelta = new Vector2(1800, 48);
            for (int i = 0; i < sheet.items.Length; i++)
            {
                string id = sheet.items[i]; if (string.IsNullOrEmpty(id)) continue;
                float x = i % 5 * 384 + 20, y = i / 5 * 198 + 80;
                var label = Ui.Label(paper, id, 16, color: ArtDirection.Ink); label.rectTransform.SetParent(Rect(paper, "标签", x, y, 360, 36), false); label.rectTransform.Fill();
                foreach (var pair in new[] { (size:88,dx:0), (size:52,dx:128), (size:34,dx:228) })
                {
                    var slot = Rect(paper, "实际物品格", x + pair.dx, y + 48, pair.size, pair.size);
                    ((RectTransform)typeof(GameUi).GetMethod("FillItemPortrait", F).Invoke(null, new object[] { slot, id })).Fill();
                }
            }
            Photo(game.Cam, "图标-" + sheet.texture); UnityEngine.Object.DestroyImmediate(paper.gameObject);
        }
        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
    }

    static void Context(GameController game, Transform stage)
    {
        var session = game.Session; session.EventsEnabled = false; session.Time.SetPaused(true); session.DebugAddCash(2000000); session.SetSmartphone(true);
        session.DebugRentStarterShop(false);
        session.StopTrip(); session.Leave(); session.ArriveAtNode(session.PlayerShopPlace.NodeId); session.Enter(session.PlayerShopPlace); game.Step(0);
        var shop = session.CurrentShop;
        var stays = new[] {
            new PetStay { Id=1001, Dog=true, Coat=0, Name="豆豆", Weeks=2, Condition=85 },
            new PetStay { Id=1002, Dog=false, Coat=0, Name="团子", Weeks=2, Condition=88 },
            new PetStay { Id=1003, Dog=true, Coat=3, Name="奶盖", Weeks=2, Condition=90 },
            new PetStay { Id=1004, Dog=false, Coat=2, Name="芝麻", Weeks=2, Condition=86 }
        };
        var state = new PetBoardingState {ShopId=shop.Place.Id,Stays=stays,NextOfferAt=long.MaxValue};
        ((IDictionary)typeof(GameSession).GetField("_petBoarding",F).GetValue(session))[shop.Place.Id]=state;
        game.Step(0); game.Interior.Update(session,0,0);
        var owner = game.Interior.Root.Find("Owner");
        var actors = (IDictionary)typeof(ShopInteriorView).GetField("_petActors",F).GetValue(game.Interior);
        File.WriteAllText(Path.Combine(Output,"入口记录.txt"),"实际寄养区生成："+actors.Count+" 只\n");

        // 用实际事件状态触发生产入口。仅在临时预览存档中准备状态。
        typeof(GameSession).GetField("_lifeDog",F).SetValue(session,new LifeDogState {Stage="adopting",Place=shop.Place.Id});
        typeof(GameSession).GetField("_shopCat",F).SetValue(session,new ShopCatVisit {ShopId=shop.Place.Id,Until=session.Clock.Now.TotalMinutes+1440,Water=true});
        typeof(GameController).GetMethod("SyncLifeEventViews",F).Invoke(game,null);
        typeof(GameController).GetMethod("SyncShopEventViews",F).Invoke(game,null);
        var dog = (GameObject)typeof(GameController).GetField("_lifeDogView",F).GetValue(game);
        var cat = (GameObject)typeof(GameController).GetField("_eventCatView",F).GetValue(game);
        foreach (var pair in new[] {(figure:dog,x:-.55f),(figure:cat,x:.55f)})
        {
            pair.figure.transform.SetParent(stage,false); pair.figure.transform.localPosition=new Vector3(pair.x,0,0);pair.figure.transform.localRotation=Quaternion.Euler(0,145,0);
            pair.figure.GetComponent<PetCompanionMotion>().enabled=false;
        }
        cat.transform.Find("WaterBowl").gameObject.SetActive(false);
        game.Interior.Root.gameObject.SetActive(false); CameraAt(game.Cam,stage.position+Vector3.up*.33f,35,.72f); Photo(game.Cam,"事件猫狗-实际入口");
        for(int frame=0;frame<24;frame++)
        {
            foreach(var figure in new[]{dog,cat})
            {
                figure.transform.position+=new Vector3(.016f,0,-.012f);
                typeof(PetCompanionMotion).GetMethod("Advance",F).Invoke(figure.GetComponent<PetCompanionMotion>(),new object[]{1f/24,true});
            }
            CameraAt(game.Cam,stage.position+new Vector3(.016f*(frame+1),.33f,-.012f*(frame+1)),25,.72f);Photo(game.Cam,$"事件猫狗-走路-{frame:00}");
        }
        // 大幅点头与摆尾端点也保留原始帧，检查接缝。
        foreach(var figure in new[]{dog,cat})
        {
            figure.transform.localPosition=new Vector3(figure==dog?-.55f:.55f,0,0);figure.transform.localRotation=Quaternion.Euler(0,145,0);
            figure.transform.Find("PetHead").localRotation=Quaternion.Euler(32,0,8);figure.transform.Find("TailPivot").localRotation=Quaternion.Euler(5,39,0);
        }
        CameraAt(game.Cam,stage.position+Vector3.up*.33f,25,.72f);Photo(game.Cam,"事件猫狗-点头摆尾端点");
        // 恢复事件的生产位置和寄养室内。
        stage.gameObject.SetActive(false);game.Interior.Root.gameObject.SetActive(true);
        ((Transform)typeof(GameController).GetField("_actors",F).GetValue(game)).gameObject.SetActive(true);game.World.Root.SetActive(false);
        typeof(GameController).GetMethod("SyncLifeEventViews",F).Invoke(game,null);typeof(GameController).GetMethod("SyncShopEventViews",F).Invoke(game,null);
        foreach(var figure in new[]{dog,cat})figure.GetComponent<PetCompanionMotion>().enabled=true;
        game.Cam.farClipPlane=2000;
        ((DayNightLighting)typeof(GameController).GetField("_lighting",F).GetValue(game)).Apply(720,true,false);
        game.Cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        game.Ui.CloseAllWindows();game.Ui.CloseDock();game.DebugShopOverview(true);game.SnapCamera(); Photo(game.Cam,"宠物店-正常距离");
        PetCamera(game);Photo(game.Cam,"宠物店-聚焦寄养区");
        var actionReport = new List<string>();
        foreach(var action in new[]{(id:1001L,toy:PetShopCatalog.Ball,name:"捡球"),(id:1002L,toy:PetShopCatalog.Wand,name:"逗猫")})
        {
            game.Interior.AnimatePetAction(action.id,action.toy);int shot=0;
            for(int frame=0;frame<900&&game.Interior.PetActionPlaying;frame++)
            {
                game.Interior.Update(session,0,1f/30);FigureMotion.AdvanceAll(1f/30,true);
                bool approach=typeof(ShopInteriorView).GetField("_petApproach",F).GetValue(game.Interior)!=null;
                float time=(float)typeof(ShopInteriorView).GetField("_petActionTime",F).GetValue(game.Interior), duration=(float)typeof(ShopInteriorView).GetField("_petActionDuration",F).GetValue(game.Interior);
                if(!approach&&shot<12&&time/duration>=shot/12f)
                {PetCamera(game);Photo(game.Cam,action.name+$"-{shot:00}");shot++;}
            }
            actionReport.Add(action.name+"：实际交互帧 "+shot);game.Interior.EndPetInteraction();
        }
        File.AppendAllLines(Path.Combine(Output,"入口记录.txt"),actionReport);
        state.Offer=new PetStay {Id=1010,OwnerId="res_auntie",Dog=true,Coat=1,Name="栗子",Weeks=2};
        var visitor=session.CityPersonState("res_auntie");visitor.LocationPlaceId=shop.Place.Id;visitor.Route=null;visitor.Task=CityPersonTask.Shopping;visitor.TaskLabel="到店办理宠物代养";
        game.Interior.Update(session,0,0);
        var dropoff=(GameObject)typeof(ShopInteriorView).GetField("_petBeingDroppedOff",F).GetValue(game.Interior);
        if(dropoff==null)throw new InvalidOperationException("交接入口未生成动物");
        CameraAt(game.Cam,dropoff.transform.position+Vector3.up*.55f,25,1.15f);Photo(game.Cam,"宠物店-主人送来寄养");
        File.AppendAllText(Path.Combine(Output,"入口记录.txt"),"主人交接：复用 "+dropoff.GetComponent<SkinnedMeshRenderer>().sharedMesh.name+"\n");
        state.Offer=null;game.Interior.Update(session,0,0);
        // 实际背包、电脑配件采购和宠物用品目录。
        game.Cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        foreach(var c in game.Ui.Canvases){c.gameObject.SetActive(true);c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=game.Cam;c.planeDistance=1;}
        foreach(var id in new[]{Gifts.Snack,Gifts.Drink,Gifts.Cig,Gifts.Wine,"pet:catfood","pet:wand","clothing:company_basic","model:harbor:flagship"})session.Backpack.AddGift(id,1);
        session.Backpack.AddFurniture("fan_new",1);game.Ui.CloseAllWindows();game.Ui.OpenBackpackPanel(); Photo(game.Cam,"实际界面-背包与后仓");
        game.Ui.CloseAllWindows();game.Ui.OpenWholesaleApp();Photo(game.Cam,"实际界面-电脑配件采购");
        game.Ui.CloseAllWindows();game.Ui.OpenCityProcurement();Photo(game.Cam,"实际界面-城市进货");
        game.Ui.CloseAllWindows();foreach(var c in game.Ui.Canvases)c.gameObject.SetActive(false);
        var life=(LifeDogState)typeof(GameSession).GetField("_lifeDog",F).GetValue(session);life.Stage="adopted";life.Place=session.Home.Id;
        session.Leave();session.ArriveAtNode(session.Home.NodeId);session.Enter(session.Home);game.Step(0);
        CameraAt(game.Cam,dog.transform.position+Vector3.up*.4f,25,1.05f);Photo(game.Cam,"陪伴小狗-领养回家");
        File.AppendAllText(Path.Combine(Output,"入口记录.txt"),"领养回家："+(dog.transform.parent==game.HomeRoom.Root)+"\n");
    }
    static void Capture()
    {
        try
        {
            SessionState.SetBool(Pending, false); Directory.CreateDirectory(Output); SaveStore.DirectoryOverride = Path.Combine(Output, "临时存档");
            var game = new GameObject("猫狗与商品复拍").AddComponent<GameController>(); game.Bootstrap(false, 914); game.enabled = false;
            game.Ui.CloseAllWindows(); foreach (var c in game.Ui.Canvases) c.gameObject.SetActive(false);
            game.World.Root.SetActive(false); ((Transform)typeof(GameController).GetField("_actors", F).GetValue(game)).gameObject.SetActive(false);
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.62f, .63f, .65f); RenderSettings.ambientIntensity = 1;
            foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>())
            { light.enabled = light.type == LightType.Directional && light.name == "Sun"; if (light.enabled) { light.color = Color.white; light.intensity = .9f; light.transform.rotation = Quaternion.Euler(52, -22, 0); } }
            var camera = game.Cam; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = ArtDirection.Hex("EFECE6"); camera.nearClipPlane = .1f; camera.farClipPlane = 200;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var stage = new GameObject("动物样板").transform; stage.position = new Vector3(0, 300, 0);
            Prims.Shape(PrimitiveType.Cube, stage, "Ground", new Vector3(0, -.012f, 0), new Vector3(20, .02f, 20), Mats.Lit(ArtDirection.Hex("EFECE6"), 0));
            var figures = new List<GameObject>(); var report = new List<string> { "样本\t基础形体\tRenderer\t材质批次\t顶点\t三角面\t关节" };
            foreach (bool dog in new[] { true, false })
            {
                for (int coat = 0; coat < 4; coat++)
                {
                    var animal = Animal(stage, dog, coat); animal.name = (dog ? "狗" : "猫") + coat; animal.transform.localPosition = new Vector3((coat - 1.5f) * .80f, 0, 0); figures.Add(animal);
                    var renderers = animal.GetComponentsInChildren<SkinnedMeshRenderer>();
                    int shapes = renderers.Sum(m => int.Parse(m.sharedMesh.name.Split('(')[1].Split(' ')[0]));
                    report.Add(animal.name + "\t" + shapes + "\t" + renderers.Length + "\t" + renderers.Sum(r => r.sharedMaterials.Length) + "\t" + renderers.Sum(m => m.sharedMesh.vertexCount) + "\t" + renderers.Sum(m => m.sharedMesh.triangles.Length / 3) + "\t" + animal.GetComponentsInChildren<Transform>().Count(t => t.name == "PetHead" || t.name == "PetHip" || t.name == "TailPivot"));
                }
                foreach (var angle in new[] { (name:"正面",yaw:180,pitch:8), (name:"游玩角度",yaw:145,pitch:45), (name:"侧面",yaw:90,pitch:8), (name:"背面",yaw:0,pitch:8) })
                {
                    foreach (var figure in figures) figure.transform.localRotation = Quaternion.Euler(0, angle.yaw, 0);
                    CameraAt(camera, stage.position + Vector3.up * .3f, angle.pitch, 1.03f); Photo(camera, (dog ? "狗" : "猫") + "-" + angle.name);
                }
                for(int i=1;i<figures.Count;i++)figures[i].SetActive(false);
                figures[0].transform.localPosition=Vector3.zero;figures[0].transform.localRotation=Quaternion.Euler(0,215,0);
                CameraAt(camera,stage.position+Vector3.up*.31f,24,.44f);Photo(camera,(dog?"狗":"猫")+"-概念同向近景");
                foreach (var figure in figures) { figure.SetActive(false); UnityEngine.Object.DestroyImmediate(figure); } figures.Clear();
            }
            File.WriteAllLines(Path.Combine(Output, "动物网格记录.tsv"), report);
            if(Environment.GetEnvironmentVariable("PCSHOP_PET_ITEM_CLOSEUPS")!="1")
            {
                var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("ItemPortraits/catalog").text); Audit(catalog); Icons(game, catalog);
                Context(game,stage);
            }
            Debug.Log("PET_ITEM_CAPTURE_DONE"); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
