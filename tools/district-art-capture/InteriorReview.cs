using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEngine;

public static partial class TownMatchCamera
{
    static partial void CaptureInteriors(GameController game, ref bool handled)
    {
        handled = true;
        if(!Resources.Load<Cubemap>("OldTown/Surfaces/roomReflection")) throw new Exception("室内反射环境未作为立方体导入");
        var s=game.Session;
        var cam=game.Cam;
        var log=new List<string>();
        game.enabled=false;
        s.EventsEnabled=false;
        game.Ui.QuestPopups=false;
        var story=(DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s);
        story.UnlockedTier=3;
        var lighting=(DayNightLighting)typeof(GameController).GetField("_lighting",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
        s.DebugAddCash(90000000);
        s.DebugRentStarterShop(true);
        s.Leave();
        s.Clock.Restore(s.Clock.Today,720);
        game.Step(.02f);
        foreach(var canvas in game.Ui.Canvases) canvas.gameObject.SetActive(false);
        cam.farClipPlane=2500; cam.nearClipPlane=.07f; cam.aspect=16f/9;
        void Go(Place p)
        {
            s.Leave();
            s.ArriveAtNode(p.NodeId);
            if(!s.Enter(p)) throw new InvalidOperationException(p.Id+" 进门失败 "+s.ClosedReason(p));
            game.Step(.05f);game.SnapCamera();
            foreach(var canvas in game.Ui.Canvases) canvas.gameObject.SetActive(false);
        }
        void Room(string id,Transform t,float w,float d)
        {
            var at=t.TransformPoint(new Vector3(w*.50f,.90f,d*.50f));
            float size=(w+d)*.212f+1.28f;
            lighting.Apply(720,true,oldRoom:id.StartsWith("outskirts",StringComparison.Ordinal));
            Shot(cam,id,at,45,31,size,1440);
            Shot(cam,id+"B",at,24,40,size*1.07f,1280);
            if(id.EndsWith("Medium"))
            {
                var corner=t.TransformPoint(new Vector3(w*.73f,1.12f,d*.73f));
                Shot(cam,id+"Finish",corner,41,27,2.62f,1440);
            }
            if(id.EndsWith("Medium") || id.EndsWith("Home"))
            {
                s.Clock.Restore(s.Clock.Today,1260);
                if(id.EndsWith("Home") && game.InHome) game.HomeRoom.Update(s,0,1260);
                lighting.Apply(1260,true,oldRoom:id.StartsWith("outskirts",StringComparison.Ordinal));
                foreach(var light in t.GetComponentsInChildren<InteriorWindowLight>())
                    light.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);
                Shot(cam,id+"Night",at,45,31,size,1440);
                s.Clock.Restore(s.Clock.Today,720);
                if(id.EndsWith("Home") && game.InHome) game.HomeRoom.Update(s,0,720);
                lighting.Apply(720,true,oldRoom:id.StartsWith("outskirts",StringComparison.Ordinal));
                foreach(var light in t.GetComponentsInChildren<InteriorWindowLight>())
                    light.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);
            }
        }
        var stage=new GameObject("新增室内尺寸复核").transform;
        for(int k=0;k<4;k++)
        {
            string region=Regions[k];
            var house=s.City.GetPlace(k==0?"home":"home_tier"+k);
            string closed=s.ClosedReason(house);
            if(closed!=null && k>0)
            {
                var agency=s.City.GetPlace(s.City.AgencyDistrictFor(house.StreetId).agencyId);
                Go(agency);
                if(s.RentResidentialHome(house.Id)) closed=s.ClosedReason(house);
                s.Leave();
            }
            if(closed==null)
            {
                Go(house);
                var view=game.InHome?game.HomeRoom:game.VisitRoom;
                Room(region+"Home",view.Root,view.Width,view.Depth);
                log.Add(house.Id+" live-home district="+view.District+" width="+view.Width+" depth="+view.Depth);
                s.Leave();game.Step(.01f);
            }
            else
            {
                var spec=s.RoomForResidence(house);
                var home=new HomeRoomView(stage,spec,k);
                home.Furniture.Sync(s.CreateVisitResidenceLayout(house,false),spec);
                game.World.Root.SetActive(false);
                Room(region+"Home",home.Root,home.Width,home.Depth);
                log.Add(house.Id+" renderer-preview; entry restricted: "+closed+" width="+home.Width+" depth="+home.Depth);
                home.SetActive(false);home.Destroy();game.World.Root.SetActive(true);
            }
            var place=s.City.Places.FirstOrDefault(p=>s.City.AgencyDistrictFor(p.StreetId)?.id==region &&
                GameSession.IsCommercialPremises(p)&&!s.IsMyShop(p)&&!s.IsCityClothingRetail(p)&&!s.IsCityActivityVenue(p)&&s.ClosedReason(p)==null);
            if(place!=null)
            {
                Go(place);
                if(!game.Interior.Root.gameObject.activeInHierarchy || game.Interior.District!=k) throw new Exception("商铺入口没有选择当前区域 "+place.Id);
                Room(region+"ActualShop",game.Interior.Root,game.Interior.Width,game.Interior.Depth);
                log.Add(place.Id+" live-shop district="+game.Interior.District+" size="+place.Size+" vacant="+s.IsVacant(place));
                s.Leave();game.Step(.02f);
            }
            game.World.Root.SetActive(false);
            foreach(ShopSize size in new[]{ShopSize.Small,ShopSize.Medium,ShopSize.Large})
            {
                var view=new ShopInteriorView(stage,size,k);
                view.SetActive(true);
                typeof(ShopInteriorView).GetMethod("Visit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,
                    new object[]{"comparison-"+region,true,"gpu",.4f,720f});
                Room(region+size,view.Root,view.Width,view.Depth);
                log.Add(region+" size-variant "+size+" "+view.Width+"x"+view.Depth);
                view.SetActive(false);view.Destroy();
            }
            game.World.Root.SetActive(true);
        }
        var walkups=s.City.Places.Where(p=>p.Kind==PlaceKind.Home && p.HousingForm==ResidenceForm.Walkup
            && s.City.AgencyDistrictFor(p.StreetId)?.id=="outskirts").ToArray();
        if(walkups.Length==0) throw new Exception("缺少城郊步梯住宅");
        var largeHome=walkups.FirstOrDefault(p=>s.ClosedReason(p)==null) ?? walkups[0];
        if(s.ClosedReason(largeHome)==null)
        {
            Go(largeHome);
            var view=game.InHome?game.HomeRoom:game.VisitRoom;
            Room("outskirtsWalkup",view.Root,view.Width,view.Depth);
            log.Add(largeHome.Id+" live walkup "+view.Width+"x"+view.Depth);
            s.Leave();game.Step(.03f);
        }
        else
        {
            var spec=s.RoomForResidence(largeHome);var view=new HomeRoomView(stage,spec,0);
            game.World.Root.SetActive(false);
            view.Furniture.Sync(s.CreateVisitResidenceLayout(largeHome,false),spec);
            Room("outskirtsWalkup",view.Root,view.Width,view.Depth);
            log.Add(largeHome.Id+" housing-size preview "+view.Width+"x"+view.Depth);
            view.SetActive(false);view.Destroy();game.World.Root.SetActive(true);
        }
        foreach(string region in new[]{"park","garden"})
        {
            var p=s.City.GetPlace(region+"_office_tower");
            var v=game.World.Places[p.Id];
            var t=v.Root.transform;
            float h=region=="park"?28.5f:45.6f;
            Shot(cam,region+"Tower",t.TransformPoint(new Vector3(0,h*.45f,(float)p.Depth*.5f)),t.eulerAngles.y+38,26,h*.70f,1440);
            Go(p);
            if(v.Exterior.activeSelf||!v.VenueInterior.activeInHierarchy)throw new Exception("大堂未切换 "+p.Id);
            var at=t.TransformPoint(new Vector3(0,1.1f,(float)p.Depth*.52f));
            float extent=(float)(p.Width+p.Depth)*.216f+1.4f;
            Shot(cam,region+"Office",at,t.eulerAngles.y+38,32,extent,1440);
            Shot(cam,region+"OfficeB",at,t.eulerAngles.y+16,42,extent*1.1f,1440);
            var move=typeof(GameController).GetMethod("WalkOffice",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int i=0;i<17;i++)move.Invoke(game,new object[]{t.forward,.09f});
            game.Step(.03f);
            var pos=(Vector3)typeof(GameController).GetField("_officeLocal",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
            if(pos.z<3 || s.CurrentPlace!=p)throw new Exception("大堂通行失败 "+p.Id);
            s.Leave();game.Step(.03f);
            if(!v.Exterior.activeSelf||v.VenueInterior.activeSelf)throw new Exception("退出后外观未恢复 "+p.Id);
            log.Add(p.Id+" entered/walked "+pos.z.ToString("F2")+"m/exited "+p.Width+"x"+p.Depth);
        }
        if(!game.World.Root.TryGetComponent<BakedCityMap>(out var map))throw new Exception("未加载烘焙地图");
        log.Add("layout="+map.LayoutHash+" source="+map.SourceHash+" sites="+map.Places.Length);
        File.WriteAllLines(Path.Combine(Output,"rooms.txt"),log);
        Debug.Log("CONCEPT_CAMERA_CAPTURED interiors "+log.Count);
        EditorApplication.Exit(0);
    }
}
