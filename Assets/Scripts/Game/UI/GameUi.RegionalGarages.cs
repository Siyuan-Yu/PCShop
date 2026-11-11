using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _garageMotorClass, _raceMotorClass, _workshopMotorClass, _assetMotorClass, _assetMotorRegion;
        private int _garageSpaceQuantity=1;
        private readonly HashSet<string> _motorTransfersExpanded = new HashSet<string>();
        private readonly Dictionary<string, string> _motorTransferSources = new Dictionary<string, string>();

        public void OpenMotorGarage()
        {
            // Physical interaction only; this is not a global garage shortcut.
            if(S.CurrentPlace?.Kind!=PlaceKind.Garage){Toast("请到车库建筑旁按 F 打开管理。");return;}
            OpenPlaceWindow(S.CurrentPlace,BuildRegionalGarage,1160,900);
        }

        private void BuildMotorClassTabs(Transform parent, int category, Action<int> choose)
        {
            var row = Ui.Row(parent, 40, 8);
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                Ui.Size(Ui.Button(row, MotorCatalog.ClassName(i), () => choose(selected), 19,
                    category == i ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1).name = "MotorClass_" + i;
            }
        }
        private void BuildDealerMotorTabs(Transform parent,int category,Action<int> choose)
        {
            var row=Ui.Row(parent,40,8);
            for(int i=0;i<4;i++){int choice=i;Ui.Size(Ui.Button(row,MotorCatalog.DealerClassName(i),()=>choose(choice),19,category==i?Ui.ButtonHot:Ui.ButtonBg),-1,40,1);}
        }

        private void BuildRegionalGarage(Window w, Place p)
        {
            var garage = RegionalGarages.Get(p.Id); var lease = S.GarageLease(p.Id);
            var motors = S.Garage;
            Line(w.Content, garage.Name + " · 已停车 " + S.ParkedMotorCount(p.Id) + " / " + S.GarageCapacity(p.Id)+"位", 23, Ui.Accent);
            Line(w.Content,"已购车位 "+(lease?.OwnedSpaces??0)+" · 租用车位 "+(lease?.RentedSpaces??0)+" · 空位 "+S.GarageFreeSpaces(p.Id),18);
            Para(w.Content,"公共车库建筑不出售、不出租。车位按需增加，供应不限；每辆入库车占1位，通勤车不占位。",16,Ui.Dim);
            var quantityRow=Ui.Row(w.Content,38,6);
            Ui.Size(Ui.Label(quantityRow,"增加车位数量",17),142,38);
            var quantity=Ui.TextInput(quantityRow,_garageSpaceQuantity.ToString(),"数量",value=>
            {_garageSpaceQuantity=int.TryParse(value,out int n)&&n>0?n:1;w.RequestRebuild();});
            quantity.contentType=InputField.ContentType.IntegerNumber;Ui.Size(quantity,110,38);
            foreach(int amount in new[]{1,5,10}){int n=amount;Ui.Size(Ui.Button(quantityRow,n+"个",()=>{_garageSpaceQuantity=n;w.Rebuild();},17),70,38);}
            void AddSpaces(bool buy)
            {
                int count=_garageSpaceQuantity;var price=S.GarageSpaceQuote(p.Id,buy,count);
                string detail=buy?"全款购买后不收该车位租金。":"后续每30天按租用数量自动续租；本期新租车位按剩余天数计费，与现有租用车位同日续租。";
                ConfirmBusiness(buy?"购买车位":"租用车位",garage.Name+" · "+count+"个车位，支付 "+price+"。"+detail,
                    ()=>{S.AcquireGarage(p.Id,buy,count);w.Rebuild();});
            }
            var contract = Ui.Row(w.Content, 42, 8);
            Ui.Size(Ui.Button(contract,"租用车位 · ¥"+garage.RentYuan+" / 位 / 30天",()=>AddSpaces(false),17,Ui.ButtonBg),-1,42,1);
            Ui.Size(Ui.Button(contract,"全款购买车位 · ¥"+garage.PriceYuan.ToString("N0")+" / 位",()=>AddSpaces(true),17,Ui.ButtonHot),-1,42,1);
            if (lease?.RentedSpaces>0)
            {
                Line(w.Content,"下次车位租金："+new GameDate(lease.NextRentDay)+" · ¥"+(garage.RentYuan*lease.RentedSpaces).ToString("N0"),16,Ui.Dim);
                int count=S.ReturnableGarageSpaces(p.Id);
                Ui.Size(Ui.Button(w.Content,"退租空闲租用车位 · "+count+"个",()=>ConfirmBusiness("退租空闲车位","退租"+count+"个空位，已付租金不退。",
                    ()=>{S.EndGarageLease(p.Id,count);w.Rebuild();}),16,Ui.ButtonBg,count>0),-1,34);
            }
            BuildMotorClassTabs(w.Content, _garageMotorClass, c => { _garageMotorClass = c; w.Rebuild(); });
            var commute = motors.Where(v => v.Commuter && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == _garageMotorClass).ToArray();
            foreach (var v in commute)
            {
                var car = v; var model = MotorCatalog.Get(car.ModelId);
                var card = MotorCard(w.Content, model, $"{model.Name} · {Vehicles.Get(model.Kind).Name}");
                var badgeRow = Ui.Row(card, 38);
                badgeRow.SetAsFirstSibling();
                var badge = Ui.Size(Ui.Panel(badgeRow, "CommuteVehicleBadge", Ui.Accent), 112, 38);
                badge.raycastTarget = false;
                var badgeText = Ui.Label(badge.transform, "通勤车", 24, TextAnchor.MiddleCenter, Ui.FlatWell);
                badgeText.fontStyle = FontStyle.Bold;
                badgeText.rectTransform.Fill();
                Line(card, S.MotorLocationText(car) + " · 不可用于比赛或练习", 16, Ui.Dim);
                Ui.Size(Ui.Button(card, "停入车库", () => ConfirmBusiness("取消通勤并停入车库？",
                    model.Name + "目前是你的" + Vehicles.Get(model.Kind).Name + "通勤车。是否取消通勤用途，并停入" + garage.Name + "？入库后将占用1个车位，这种通勤方式暂不可用；可再指定另一辆同类型车辆通勤。",
                    () => { S.ParkCommuteMotor(car.Id, p.Id); w.Rebuild(); }), 17, Ui.ButtonBg,
                    lease != null && S.GarageFreeSpaces(p.Id) > 0 && S.MotorAvailable(car)), -1, 38);
            }
            if (commute.Length == 0) Line(w.Content, "这一分类尚未指定通勤车", 16, Ui.Dim);
            Line(w.Content, "本区入库车辆 · " + MotorCatalog.ClassName(_garageMotorClass), 20, Ui.Accent);
            var parked = motors.Where(v => !v.Commuter && v.GarageId == p.Id && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == _garageMotorClass).ToArray();
            foreach (var v in parked)
            {
                var car = v; var model = MotorCatalog.Get(car.ModelId); var card = MotorCard(w.Content, model);
                BuildMotorPerformance(card, MotorCatalog.Performance(car), car.Condition); Line(card, S.MotorLocationText(car), 15, Ui.Dim);
                var old = S.CommuteMotor(model.Kind);
                Ui.Size(Ui.Button(card, "设为" + Vehicles.Get(model.Kind).Name + "通勤车" + (old != null ? "（原通勤车留在本库）" : ""),
                    () => { S.SetMotorCommuter(car.Id); w.Rebuild(); }, 17, Ui.ButtonBg, S.MotorAvailable(car)), -1, 38);
            }
            if (parked.Length == 0) Para(w.Content, lease == null ? "先租用或购买车位，再将车辆停入；入库车辆可用于本区赛场的比赛和练习。" : "本区车库还没有这一类车辆。", 17, Ui.Dim);
            BuildMotorTransfer(w, garage, _garageMotorClass, "garage:" + p.Id);
            Para(w.Content, "小电驴、三轮车、轿车各可指定1辆通勤车，通勤车不占车位。货车仅供员工采购，不使用本区车位。", 15, Ui.Dim);
        }

        private void BuildMotorTransfer(Window w, RegionalGarage target, int category, string context)
        {
            bool expanded = _motorTransfersExpanded.Contains(context);
            Ui.Size(Ui.Button(w.Content, (expanded ? "收起" : "从其他车库调入") + " · " + MotorCatalog.ClassName(category),
                () => { if (!_motorTransfersExpanded.Add(context)) _motorTransfersExpanded.Remove(context); w.Rebuild(); }, 18), -1, 40).name = "MotorTransfer_" + context;
            if (!expanded) return;
            var card = BusinessCard(w.Content, "调入" + target.Name);
            if (!S.HasGarage(target.Id)) { Para(card, "请先到本区车库建筑旁按 F，租用或购买车位后再调入。", 17, Ui.Warn); return; }
            Line(card, "已持有车位的空位 " + S.GarageFreeSpaces(target.Id) + " / " + S.GarageCapacity(target.Id) + " · 调运后可在当前页面选用", 16, Ui.Dim);
            var all = S.Garage;
            var sources = RegionalGarages.All.Where(g => g.Id != target.Id && S.HasGarage(g.Id)).ToArray();
            if (sources.Length == 0) { Line(card, "其他车库暂无已租购车位", 17, Ui.Dim); return; }
            _motorTransferSources.TryGetValue(context, out string sourceId);
            if (!sources.Any(g => g.Id == sourceId)) sourceId = sources.FirstOrDefault(g => all.Any(v => !v.Commuter && v.GarageId == g.Id && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == category))?.Id ?? sources[0].Id;
            _motorTransferSources[context] = sourceId;
            var row = Ui.Row(card, 38, 6);
            foreach (var g in sources)
            {
                var source = g; int count = all.Count(v => !v.Commuter && v.GarageId == source.Id && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == category);
                Ui.Size(Ui.Button(row, GameSession.DistrictName(source.Tier) + " · " + count + "辆",
                    () => { _motorTransferSources[context] = source.Id; w.Rebuild(); }, 17, source.Id == sourceId ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1);
            }
            var vehicles = all.Where(v => !v.Commuter && v.GarageId == sourceId && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == category).ToArray();
            foreach (var v in vehicles)
            {
                var car = v; var fee = S.MotorTransferFee(car.Id, target.Id); string why = S.MotorTransferBlocker(car.Id, target.Id);
                MotorIdentity(card, car.ModelId, MotorCatalog.Get(car.ModelId).Name, S.MotorLocationText(car));
                Ui.Size(Ui.Button(card, "支付 " + fee + " 并调入", () => ConfirmBusiness("确认车辆调运",
                    MotorCatalog.Get(car.ModelId).Name + "：" + RegionalGarages.Get(car.GarageId).Name + " → " + target.Name + "，调运费 " + fee + "。",
                    () => { if (S.TransferMotor(car.Id, target.Id)) { _raceSelection = car.Id; _motorTransfersExpanded.Remove(context); } w.Rebuild(); }),
                    17, Ui.ButtonHot, why == null), -1, 38);
                if (why != null) Line(card, why, 15, Ui.Warn);
            }
            if (vehicles.Length == 0) Line(card, "所选车库没有这一类入库车辆", 17, Ui.Dim);
        }

        private void OpenMotorAcquisition(Window parent, string modelId, bool withdraw, string usedId = null)
        {
            var model = MotorCatalog.Get(modelId); bool forCommute = S.CommuteMotor(model.Kind) == null;
            if(model.Kind==VehicleKind.Van)
            {
                OpenWindow(withdraw?"转入进货车队":"购买货车",760,400,w=>
                {
                    MotorIdentity(w.Content,model.Id,model.Name,withdraw?"转入进货车队":"¥"+model.Price.ToString("N0"));
                    Para(w.Content,"货车仅供员工外出进货使用，不可通勤、比赛或练习，也不占用任何车库车位。",18);
                    string why=withdraw?S.MotorStorageBlocker(modelId,false,null):S.MotorPurchaseBlocker(modelId,false,null);
                    if(why!=null)Para(w.Content,why,17,Ui.Warn);
                    Ui.Size(Ui.Button(w.Content,withdraw?"确认转入进货车队":"购买并加入进货车队",()=>
                    {bool ok=withdraw?S.WithdrawUsedMotor(usedId,false,null):S.BuyMotor(modelId,false,null);if(ok){w.Close();parent?.Rebuild();}},19,Ui.ButtonHot,why==null),-1,44);
                });return;
            }
            string garageId = S.GarageLeases.FirstOrDefault(g => S.GarageFreeSpaces(g.PlaceId) > 0)?.PlaceId ?? S.GarageLeases.FirstOrDefault()?.PlaceId;
            OpenWindow(withdraw ? "自用车辆 · 车辆用途" : "购买确认 · 车辆用途", 800, 680, w =>
            {
                MotorIdentity(w.Content, model.Id, model.Name, withdraw ? "从二手车行展位转出" : "¥" + model.Price.ToString("N0"));
                var current = S.CommuteMotor(model.Kind);
                var purpose = Ui.Row(w.Content, 42, 8);
                Ui.Size(Ui.Button(purpose, "用于通勤", () => { forCommute = true; w.Rebuild(); }, 18, forCommute ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                Ui.Size(Ui.Button(purpose, "停入车库", () => { forCommute = false; w.Rebuild(); }, 18, !forCommute ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                bool needsSpace = !forCommute || current != null;
                if (forCommute)
                    Para(w.Content, current == null ? "设为" + Vehicles.Get(model.Kind).Name + "通勤车，免车库车位。通勤车不能比赛或练习。"
                        : "新车用于通勤；原通勤车“" + MotorCatalog.Get(current.ModelId).Name + "”需要停入下面选择的车库。", 17);
                else Para(w.Content, "车辆停入所选车库，可在对应片区赛场比赛或练习。", 17);
                if (needsSpace)
                {
                    Line(w.Content, "选择停放车库", 20, Ui.Accent);
                    foreach (var g in RegionalGarages.All.Where(garage => !GameSession.FirstChapterTrial || garage.Tier <= S.DistrictAccessTier))
                    {
                        var target = g; var lease = S.GarageLease(g.Id); bool accessible = S.DistrictAccessTier >= g.Tier;
                        Ui.Size(Ui.Button(w.Content, g.Name + (lease == null ? " · 未持有车位" : " · 空余 " + S.GarageFreeSpaces(g.Id) + " / "+lease.Capacity+"位"),
                            () => { garageId = target.Id; w.Rebuild(); }, 17, garageId == g.Id ? Ui.ButtonHot : Ui.ButtonBg,
                            lease != null && accessible), -1, 39);
                    }
                }
                string why = withdraw ? S.MotorStorageBlocker(modelId, forCommute, garageId) : S.MotorPurchaseBlocker(modelId, forCommute, garageId);
                if (why != null) Para(w.Content, why, 17, Ui.Warn);
                Ui.Size(Ui.Button(w.Content, withdraw ? "确认转为自用" : "确认购买 · ¥" + model.Price.ToString("N0"), () =>
                {
                    bool ok = withdraw ? S.WithdrawUsedMotor(usedId, forCommute, garageId) : S.BuyMotor(modelId, forCommute, garageId);
                    if (ok) { w.Close(); parent?.Rebuild(); }
                }, 20, Ui.ButtonHot, why == null), -1, 46);
            });
        }

        private void BuildAssetVehicles(Window w)
        {
            var trucks=S.Garage.Where(v=>MotorCatalog.Get(v.ModelId).Kind==VehicleKind.Van).ToArray();
            if(trucks.Length>0)
            {
                var fleet=BusinessCard(w.Content,"员工进货货车 · 全城车队 · "+trucks.Length+"辆 · 免车位");
                foreach(var v in trucks)MotorIdentity(fleet,v.ModelId,MotorCatalog.Get(v.ModelId).Name,S.MotorLocationText(v)+" · 参考残值 ¥"+MotorCatalog.ResaleValue(v).ToString("N0"));
            }
            var region = Ui.Row(w.Content, 40, 8);
            foreach (var g in RegionalGarages.All.Where(garage => !GameSession.FirstChapterTrial || garage.Tier <= S.DistrictAccessTier))
            {
                var target = g;
                Ui.Size(Ui.Button(region, GameSession.DistrictName(g.Tier), () => { _assetMotorRegion = target.Tier; w.Rebuild(); }, 19,
                    _assetMotorRegion == g.Tier ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);
            }
            BuildMotorClassTabs(w.Content, _assetMotorClass, c => { _assetMotorClass = c; w.Rebuild(); });
            var garage = RegionalGarages.All[_assetMotorRegion]; var lease = S.GarageLease(garage.Id); var all = S.Garage;
            var commuters = all.Where(v => v.Commuter && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == _assetMotorClass).ToArray();
            var commute = BusinessCard(w.Content, "通勤车辆 · 不占车库车位");
            foreach (var v in commuters) MotorIdentity(commute, v.ModelId, MotorCatalog.Get(v.ModelId).Name, S.MotorLocationText(v));
            if (commuters.Length == 0) Line(commute, "这一分类尚未指定通勤车", 17, Ui.Dim);
            var info = BusinessCard(w.Content, garage.Name + " · 车位资产");
            Line(info,"已购 "+(lease?.OwnedSpaces??0)+"位 · 租用 "+(lease?.RentedSpaces??0)+"位 · 停车 "+S.ParkedMotorCount(garage.Id)+"辆 · 空余 "+S.GarageFreeSpaces(garage.Id)+"位",18,Ui.Accent);
            Line(info,"自有车位参考价值 ¥"+(garage.PriceYuan*(lease?.OwnedSpaces??0)).ToString("N0")+" · 每30天租金 ¥"+(garage.RentYuan*(lease?.RentedSpaces??0)).ToString("N0"),17);
            if (lease?.RentedSpaces>0) Line(info, "下次续租 " + new GameDate(lease.NextRentDay), 16, Ui.Dim);
            Ui.Size(Ui.Button(info, "地图定位车库", () => OpenGarageLocation(garage.Id), 17), -1, 36);
            var parked = all.Where(v => !v.Commuter && v.GarageId == garage.Id && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == _assetMotorClass).ToArray();
            Line(w.Content, GameSession.DistrictName(garage.Tier) + " · " + MotorCatalog.ClassName(_assetMotorClass) + " · " + parked.Length + "辆", 21, Ui.Accent);
            foreach (var v in parked)
            {
                var card = MotorCard(w.Content, MotorCatalog.Get(v.ModelId));
                Line(card, S.MotorLocationText(v) + " · 参考残值 ¥" + ((long)(MotorCatalog.Get(v.ModelId).Price * .6 * v.Condition / 100)).ToString("N0"), 17);
                BuildMotorPerformance(card, MotorCatalog.Performance(v), v.Condition);
            }
            if (parked.Length == 0) Line(w.Content, "本区车库暂无这一类车辆", 18, Ui.Dim);
            Para(w.Content, "管理车辆与增租、购买车位请前往车库建筑旁按 F。公共车库建筑本身不属于玩家。", 16, Ui.Dim);
        }

        private void OpenGarageLocation(string id)
        {
            OpenWindow("车库位置", 960, 660, w =>
            {
                Line(w.Content, RegionalGarages.Get(id).Name + " · " + RaceTracks.Get(RegionalGarages.Get(id).TrackId).Name + "接待建筑旁", 21, Ui.Accent);
                var stage = Ui.Rect(w.Content, "GarageLocationMap"); Ui.Size(stage, -1, 450); stage.gameObject.AddComponent<RectMask2D>();
                var drawing = Ui.Rect(stage, "Drawing").Fill(); var map = drawing.gameObject.AddComponent<AssetPropertyMap>();
                // This public building is only a location marker, never player-owned real estate.
                map.Initialize(S, new AssetPropertyMap.ViewState(), Array.Empty<Place>(),Array.Empty<Place>(), id);
                Canvas.ForceUpdateCanvases(); map.FocusPlace(S.City.GetPlace(id));
                Line(w.Content, "滚轮缩放 · 拖动地图 · 到车库门口按 F 管理", 16, Ui.Dim);
            });
        }
    }
}
