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
        private string _garagePerformanceMotorId;
        private readonly Dictionary<string, string> _motorTransferSources = new Dictionary<string, string>();

        public void OpenMotorGarage()
        {
            // Physical interaction only; this is not a global garage shortcut.
            if(S.CurrentPlace?.Kind!=PlaceKind.Garage){Toast("请到车库建筑旁按 F 打开管理。");return;}
            OpenPlaceWindow(S.CurrentPlace,BuildRegionalGarage,1340,690);
        }

        private void BuildMotorClassTabs(Transform parent, int category, Action<int> choose, int[] counts = null)
        {
            var row = Ui.Row(parent, 40, 8);
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                string label = MotorCatalog.ClassName(i) + (counts == null ? "" : $" · {counts[i]}");
                Ui.Size(Ui.Button(row, label, () => choose(selected), 19,
                    Ui.SelectedButton(category == i)), -1, 40, 1).name = "MotorClass_" + i;
            }
        }
        private void BuildDealerMotorTabs(Transform parent,int category,Action<int> choose)
        {
            var row=Ui.Row(parent,40,8);
            for(int i=0;i<4;i++){int choice=i;Ui.Size(Ui.Button(row,MotorCatalog.DealerClassName(i),()=>choose(choice),19,Ui.SelectedButton(category==i)),-1,40,1);}
        }

        private void BuildRegionalGarage(Window w, Place p)
        {
            var garage = RegionalGarages.Get(p.Id);
            if (S.RacingGarageRegistrationOnly(p.Id))
            {
                BuildGarageRacingEnrollment(w);
                return;
            }
            if (p.Id == GameSession.StarterRaceGarageId && S.RacingEnrollmentAvailable)
                BuildRacingIntroduction(w, compact: true);

            var columns = Ui.Row(w.Content, -1, 14);
            columns.GetComponent<LayoutElement>().flexibleHeight = 1;

            var vehicles = Ui.Rect(columns, "GarageVehicles");
            Ui.Size(vehicles, 0, -1, 2);
            Ui.VList(vehicles, 8, 0);
            var motors = S.Garage.Where(v => v.Commuter || v.GarageId == garage.Id).ToLookup(
                v => MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind));
            BuildMotorClassTabs(vehicles, _garageMotorClass, category =>
                { _garageMotorClass = category; _garagePerformanceMotorId = null; w.Rebuild(); },
                Enumerable.Range(0, 3).Select(category => motors[category].Count()).ToArray());

            var categoryMotors = motors[_garageMotorClass];
            BuildGarageCommuter(vehicles, garage, categoryMotors.FirstOrDefault(v => v.Commuter), w);
            Ui.Size(Ui.Panel(vehicles, "VehicleSectionDivider", Ui.Accent), -1, 2);

            var parked = categoryMotors.Where(v => !v.Commuter).ToArray();
            var heading = Ui.Row(vehicles, 36);
            Ui.Size(Ui.Label(heading, $"已停入本区车库 · {parked.Length} 辆", 20, TextAnchor.MiddleLeft, Ui.Accent), 0, 36, 1);
            Ui.Size(Ui.Button(heading, "从其他车库调入", () =>
                OpenWindow("调入" + MotorCatalog.ClassName(_garageMotorClass), 680, 575, transfer =>
                    BuildMotorTransferPanel(transfer, garage, _garageMotorClass, "garage:" + p.Id,
                        () => { transfer.Close(); w.Rebuild(); })), 17), 168, 36);

            var list = Ui.Scroll(vehicles);
            Ui.Size((RectTransform)list.parent.parent, -1, 0).GetComponent<LayoutElement>().flexibleHeight = 1;
            foreach (var car in parked)
            {
                var model = MotorCatalog.Get(car.ModelId);
                var performance = MotorCatalog.Performance(car);
                var card = Ui.Panel(list, "GarageVehicle", Ui.PanelBg2).rectTransform;
                Ui.VList(card, 6, 10);
                var row = Ui.Row(card, 58, 10);
                MotorThumbnail(row, model.Id, 58);
                var summary = Ui.Rect(row, "VehicleSummary");
                Ui.Size(summary, 0, -1, 1);
                Ui.VList(summary, 2, 0).childAlignment = TextAnchor.MiddleLeft;
                Line(summary, model.Name, 19);
                Line(summary, $"极速 {performance.Speed:0} km/h · 加速 {performance.Acceleration:0.0} · 车况 {car.Condition:0}%", 15, Ui.Dim);
                bool expanded = _garagePerformanceMotorId == car.Id;
                var actions = Ui.Row(row, 36);
                Ui.Size(actions, 236);
                actions.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                Ui.Size(Ui.Button(actions, expanded ? "性能 ▴" : "性能 ▾", () =>
                {
                    _garagePerformanceMotorId = expanded ? null : car.Id;
                    w.Rebuild();
                }, 16), 92, 36);
                Ui.Size(Ui.Button(actions, "设为通勤车", () =>
                {
                    void SetCommuter() { S.SetMotorCommuter(car.Id); w.Rebuild(); }
                    if (S.CommuteMotor(model.Kind) == null) SetCommuter();
                    else ConfirmBusiness("更换通勤车", "原通勤车将停入本区车库。", SetCommuter);
                }, 17, Ui.ButtonBg, S.MotorAvailable(car)), 138, 36);
                if (expanded) BuildMotorRadar(card, performance, car.Condition);
            }
            if (parked.Length == 0) Line(list, "本区没有这类入库车辆", 17, Ui.Dim);

            var parking = Ui.Rect(columns, "GarageParking");
            Ui.Size(parking, 0, -1, 1);
            Ui.VList(parking, 8, 0);
            BuildGarageParking(parking, garage, w);
        }

        private void BuildGarageRacingEnrollment(Window w)
        {
            var card = BusinessCard(w.Content, "车库负责人 · 赛车登记");
            var row = Ui.Row(card, 46, 12);
            string blocker = S.RacingEnrollmentBlocker;
            Ui.Size(Ui.Label(row, blocker ?? "资格已满足，免费登记后即可解锁赛车。", 17,
                TextAnchor.MiddleLeft, blocker == null ? Ui.Good : Ui.Warn), 0, -1, 1);
            Ui.Size(Ui.Button(row, "办理赛车登记", () =>
            {
                if (S.UnlockRacingAtGarage()) w.Rebuild();
            }, 18, Ui.ButtonHot, blocker == null), 190, 42);
        }

        private void BuildGarageCommuter(Transform parent, RegionalGarage garage, GarageVehicle car, Window w)
        {
            var frame = Ui.Panel(parent, "GarageCommuter", Ui.Accent).rectTransform;
            Ui.VList(frame, 0, 1);
            var card = Ui.Panel(frame, "CommuterContent", Ui.PanelBg2).rectTransform;
            Ui.VList(card, 3, 10);
            Line(card, "当前通勤车", 22, Ui.Accent);
            if (car == null)
            {
                Line(card, "这类车辆还没有指定通勤车", 16, Ui.Dim);
                return;
            }

            var model = MotorCatalog.Get(car.ModelId);
            var row = Ui.Row(card, 68, 10);
            MotorThumbnail(row, model.Id, 68);
            var summary = Ui.Rect(row, "CommuterSummary");
            Ui.Size(summary, 0, -1, 1);
            Ui.VList(summary, 3, 0).childAlignment = TextAnchor.MiddleLeft;
            Line(summary, model.Name, 22);
            Line(summary, TravelSpeedText(MotorCatalog.CommuteSpec(model.Id)) + " · 不占车位", 16, Ui.Dim);
            bool hasSpace = S.GarageFreeSpaces(garage.Id) > 0;
            var park = Ui.Button(row, "停入车库", () => ConfirmBusiness("将通勤车停入车库？",
                model.Name + "入库后将取消通勤用途，占用本区 1 个车位。",
                () => { S.ParkCommuteMotor(car.Id, garage.Id); w.Rebuild(); }), 17, Ui.ButtonHot,
                hasSpace && S.MotorAvailable(car));
            Ui.Size(park, 138, 40);
            if (!hasSpace) park.Tip("本区没有空余车位，可以在右侧购买。");
        }

        private void BuildGarageParking(Transform parent, RegionalGarage garage, Window w)
        {
            var overview = BusinessCard(parent, "本区车位 · 全部车型");
            int used = S.ParkedMotorCount(garage.Id), total = S.GarageCapacity(garage.Id);
            var lease = S.GarageLease(garage.Id);
            Line(overview, $"空余 {S.GarageFreeSpaces(garage.Id)} 位", 28, Ui.Accent);
            Line(overview, $"已使用 {used} / {total} 位", 18);
            var track = Ui.Panel(overview, "ParkingOccupancy", Ui.FlatWell);
            Ui.Size(track, -1, 6);
            if (total > 0 && used > 0)
                Ui.Panel(track.transform, "Occupied", Ui.Accent).rectTransform.Anchor(0, 0, Mathf.Clamp01((float)used / total), 1, 0, 0, 0, 0);
            Line(overview, $"已购 {lease?.OwnedSpaces ?? 0} 位", 16, Ui.Dim);
            Ui.Size(Ui.Panel(overview, "ParkingPurchaseDivider", Ui.ButtonBg), -1, 1);
            Line(overview, "购买车位", 20, Ui.Accent);
            var quantityRow = Ui.Row(overview, 38, 6);
            Ui.Size(Ui.Label(quantityRow, "购买数量", 16, TextAnchor.MiddleLeft), 0, 38, 1);
            int count = _garageSpaceQuantity;
            var quantity = Ui.TextInput(quantityRow, count.ToString(), "数量", null);
            quantity.contentType = InputField.ContentType.IntegerNumber;
            Ui.Size(quantity, 82, 38);
            var minus = Ui.Size(Ui.Button(quantityRow, "−", () => StepQuantity(-1), 18), 36, 38);
            minus.transform.SetSiblingIndex(quantity.transform.GetSiblingIndex());
            Ui.Size(Ui.Button(quantityRow, "+", () => StepQuantity(1), 18), 36, 38);
            Ui.Size(Ui.Label(quantityRow, "位", 16), 24, 38);
            Line(overview, $"单价 ¥{garage.PriceYuan:N0} / 位", 16, Ui.Dim);
            var quote = Line(overview, "", 22, Ui.Accent);
            var buy = Ui.Size(Ui.Button(overview, "全款购买", () => ConfirmBusiness("购买车位",
                $"{garage.Name} · {count} 位 · 本次支付 {S.GarageSpaceQuote(garage.Id, count)}",
                () => { S.AcquireGarage(garage.Id, count); w.Rebuild(); }), 18, Ui.ButtonHot), -1, 44);
            var buyText = buy.GetComponentInChildren<Text>();
            quantity.onValueChanged.AddListener(value =>
            {
                count = int.TryParse(value, out int parsed) ? Math.Max(1, parsed) : 1;
                _garageSpaceQuantity = count;
                UpdateQuote();
            });
            quantity.onEndEdit.AddListener(_ => quantity.SetTextWithoutNotify(count.ToString()));
            UpdateQuote();

            if (lease?.RentedSpaces > 0)
            {
                Line(overview, $"现有租用车位 {lease.RentedSpaces} 位 · 下次续租 {new GameDate(lease.NextRentDay)}", 15, Ui.Dim);
                int returnable = S.ReturnableGarageSpaces(garage.Id);
                if (returnable > 0)
                    Ui.Size(Ui.Button(overview, $"退租空闲车位 · {returnable} 位", () => ConfirmBusiness("退租空闲车位",
                        $"退租 {returnable} 个车位，已付租金不退。",
                        () => { S.EndGarageLease(garage.Id, returnable); w.Rebuild(); }), 16), -1, 36);
            }

            void StepQuantity(int step) => quantity.text = Math.Min(int.MaxValue, Math.Max(1L, (long)count + step)).ToString();
            void UpdateQuote()
            {
                quote.text = $"本次合计 {S.GarageSpaceQuote(garage.Id, count)}";
                buyText.text = $"全款购买 {count} 个车位";
            }
        }

        private void BuildMotorTransferPanel(Window w, RegionalGarage target, int category, string context, Action onTransferred)
        {
            var card = BusinessCard(w.Content, "调入" + target.Name);
            if (!S.HasGarage(target.Id)) { Para(card, "请先到本区车库建筑旁按 F，购买车位后再调入。", 17, Ui.Warn); return; }
            Line(card, "已持有车位的空位 " + S.GarageFreeSpaces(target.Id) + " / " + S.GarageCapacity(target.Id) + " · 调运后可在当前页面选用", 16, Ui.Dim);
            var all = S.Garage;
            var sources = RegionalGarages.All.Where(g => g.Id != target.Id && S.HasGarage(g.Id)).ToArray();
            if (sources.Length == 0) { Line(card, "其他车库暂无可用车位", 17, Ui.Dim); return; }
            _motorTransferSources.TryGetValue(context, out string sourceId);
            if (!sources.Any(g => g.Id == sourceId)) sourceId = sources.FirstOrDefault(g => all.Any(v => !v.Commuter && v.GarageId == g.Id && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == category))?.Id ?? sources[0].Id;
            _motorTransferSources[context] = sourceId;
            var row = Ui.Row(card, 38, 6);
            foreach (var g in sources)
            {
                var source = g; int count = all.Count(v => !v.Commuter && v.GarageId == source.Id && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == category);
                Ui.Size(Ui.Button(row, GameSession.DistrictName(source.Tier) + " · " + count + "辆",
                    () => { _motorTransferSources[context] = source.Id; w.Rebuild(); }, 17, Ui.SelectedButton(source.Id == sourceId)), -1, 38, 1);
            }
            var vehicles = all.Where(v => !v.Commuter && v.GarageId == sourceId && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == category).ToArray();
            foreach (var v in vehicles)
            {
                var car = v; var fee = S.MotorTransferFee(car.Id, target.Id); string why = S.MotorTransferBlocker(car.Id, target.Id);
                MotorIdentity(card, car.ModelId, MotorCatalog.Get(car.ModelId).Name, S.MotorLocationText(car));
                Ui.Size(Ui.Button(card, "支付 " + fee + " 并调入", () => ConfirmBusiness("确认车辆调运",
                    MotorCatalog.Get(car.ModelId).Name + "：" + RegionalGarages.Get(car.GarageId).Name + " → " + target.Name + "，调运费 " + fee + "。",
                    () =>
                    {
                        if (!S.TransferMotor(car.Id, target.Id)) { w.Rebuild(); return; }
                        _raceSelection = car.Id;
                        onTransferred();
                    }),
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
                Ui.Size(Ui.Button(purpose, "用于通勤", () => { forCommute = true; w.Rebuild(); }, 18, Ui.SelectedButton(forCommute)), -1, 42, 1);
                Ui.Size(Ui.Button(purpose, "停入车库", () => { forCommute = false; w.Rebuild(); }, 18, Ui.SelectedButton(!forCommute)), -1, 42, 1);
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
                            () => { garageId = target.Id; w.Rebuild(); }, 17, Ui.SelectedButton(garageId == g.Id),
                            lease != null && accessible), -1, 39);
                    }
                }
                string why = withdraw ? S.MotorStorageBlocker(modelId, forCommute, garageId) : S.MotorPurchaseBlocker(modelId, forCommute, garageId);
                if (why != null) Para(w.Content, why, 17, Ui.Warn);
                Ui.Size(Ui.Button(w.Content, withdraw ? "确认转为自用" : "确认购买 · ¥" + model.Price.ToString("N0"), () =>
                {
                    bool ok = withdraw ? S.WithdrawUsedMotor(usedId, forCommute, garageId) : S.BuyMotor(modelId, forCommute, garageId);
                    if (!ok) return;
                    w.Close();
                    if (!withdraw && model.Kind == VehicleKind.EBike && S.ScooterTutorialPaused)
                    {
                        parent?.Close();
                        UpdateVehicleButtons();
                    }
                    else parent?.Rebuild();
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
                    Ui.SelectedButton(_assetMotorRegion == g.Tier)), -1, 40, 1);
            }
            BuildMotorClassTabs(w.Content, _assetMotorClass, c => { _assetMotorClass = c; w.Rebuild(); });
            var garage = RegionalGarages.All[_assetMotorRegion]; var lease = S.GarageLease(garage.Id); var all = S.Garage;
            var commuters = all.Where(v => v.Commuter && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == _assetMotorClass).ToArray();
            var commute = BusinessCard(w.Content, "通勤车辆 · 不占车库车位");
            foreach (var v in commuters)
                MotorIdentity(commute, v.ModelId, MotorCatalog.Get(v.ModelId).Name,
                    TravelSpeedText(MotorCatalog.CommuteSpec(v.ModelId)) + " · " + S.MotorLocationText(v));
            if (commuters.Length == 0) Line(commute, "这一分类尚未指定通勤车", 17, Ui.Dim);
            var info = BusinessCard(w.Content, garage.Name + " · 车位资产");
            Line(info,"已购 "+(lease?.OwnedSpaces??0)+"位 · 停车 "+S.ParkedMotorCount(garage.Id)+"辆 · 空余 "+S.GarageFreeSpaces(garage.Id)+"位",18,Ui.Accent);
            Line(info,"自有车位参考价值 ¥"+(garage.PriceYuan*(lease?.OwnedSpaces??0)).ToString("N0"),17);
            if (lease?.RentedSpaces>0) Line(info, $"现有租用 {lease.RentedSpaces} 位 · 下次续租 {new GameDate(lease.NextRentDay)} · ¥{garage.RentYuan * lease.RentedSpaces:N0}", 16, Ui.Dim);
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
            Para(w.Content, "管理车辆或购买车位，请到车库建筑旁按 F。", 16, Ui.Dim);
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
