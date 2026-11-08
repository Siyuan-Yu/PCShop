using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private GameObject _travelBar, _worldHintStrip;
        private RectTransform _petFocusHud, _petFocusChoices;
        private Text _petFocusTitle, _petFocusCoverage, _petDetailName, _petCondition, _petHappiness, _petCareStatus, _petDetailInfo, _petToyLabel;
        private Button _petPatButton, _petToyButton;
        private readonly List<(long id, Button button)> _petFocusButtons = new();
        private readonly Dictionary<PetCareAction,(Text status, Button button)> _sharedPetCare = new();
        private float _nextPetFocusRefresh;
        private static readonly Color PetUnattended = Ui.Warn;

        public void OpenPetOrders()
        {
            if (!S.InPlayerShop || !S.IsPetShop(S.CurrentShop)) return;
            OpenDock();
            SelectTab(ShopTab.Orders);
        }

        public void OpenPetOwnerVisit(long id)
        {
            var shop = S.CurrentShop;
            var state = S.PetBoarding(shop);
            bool dropOff = state?.Offer?.Id == id;
            var pet = dropOff ? state.Offer : S.PetGuests(shop).FirstOrDefault(p=>p.Id==id);
            if (!S.PetOwnerHere(shop,pet)) return;
            string owner = S.CityPerson(pet.OwnerId)?.Name ?? "宠物主人";
            OpenWindow(owner+" · "+(dropOff?"送养":"探望"),620,385,w=>
            {
                PersonSummary(w.Content, pet.OwnerId, owner, pet.Name + (pet.Dog ? " · 狗" : " · 猫"));
                string reason = dropOff ? S.PetAcceptBlocker(shop) : S.PetVisitBlocker(shop,pet);
                string action = dropOff ? "接单并入住" : pet.PaidWeeks+1 >= pet.Weeks ? "结算并接回" : "结算本周代养";
                Para(w.Content,dropOff ? $"代养 {pet.Weeks} 周 · 基础价 ¥{pet.WeeklyPrice}/周\n每周探望时按宠物状态结算。"
                    : $"状态 {pet.Condition}/100 · 快乐 {pet.Happiness}/100\n第 {pet.PaidWeeks+1}/{pet.Weeks} 周实收 {GameSession.PetWeeklyPayment(pet)}",18);
                Ui.Size(Ui.Button(w.Content,reason??action,()=>
                {
                    if (!(dropOff ? S.AcceptPetStay() : S.SettlePetStay(id))) return;
                    w.Close();
                    RebuildDock();
                },18,Ui.ButtonHot,reason==null),-1,42);
            });
        }

        private string PetOrdersSignature()
        {
            if (!S.IsPetShop(S.CurrentShop)) return "";
            var shop = S.CurrentShop;
            var state = S.PetBoarding(shop);
            return "|" + S.PetCapacity(shop) + "|" + state?.Offer?.Id + "|" + S.PetAcceptBlocker(shop) + "|" + state?.LastPickup
                + "|" + string.Join(",",S.PetGuests(shop).Select(p => p.Id + ":" + S.PetVisitBlocker(shop,p)
                    + ":" + p.Condition + ":" + p.Happiness + ":" + p.PaidWeeks + ":" + PetCareStatus(shop,p).text));
        }

        private (string text, bool covered) PetCareStatus(OwnedShop shop, PetStay pet)
        {
            var keeper = S.PetKeeperOf(shop,pet);
            if (keeper == null) return ("没有人照顾",false);
            return S.CityEmployeeAvailable(keeper,shop)
                ? ($"照顾人 · {keeper.Name}",true)
                : ($"没有人照顾（{keeper.Name}未在岗）",false);
        }

        private void BuildPetOrders()
        {
            var shop = S.CurrentShop;
            int capacity = S.PetCapacity(shop);
            var state = S.PetBoarding(shop);
            var guests = S.PetGuests(shop);
            Line(C,"宠物代养 · " + guests.Count + " / " + capacity + " 只",21,Ui.Accent);
            if (capacity == 0)
            {
                Line(C,"在「装修」摆好宠物屋后开始接单。",17,Ui.Dim);
                Ui.Size(Ui.Button(C,"打开装修",() => SelectTab(ShopTab.Renovate),17),-1,40);
                return;
            }
            if (state?.Offer is PetStay offer)
            {
                PersonSummary(C, offer.OwnerId, "送养 · " + offer.Name + (offer.Dog ? " · 狗" : " · 猫"),
                    (S.CityPerson(offer.OwnerId)?.Name ?? "主人") + $" · {offer.Weeks} 周 · 基础价 ¥{offer.WeeklyPrice}/周");
                string blocker = S.PetAcceptBlocker(shop);
                var accept = Ui.Button(C,blocker ?? "接单并入住",() => { if (S.AcceptPetStay()) RebuildDock(); },18,Ui.ButtonHot);
                accept.interactable = blocker == null;
                Ui.Size(accept,-1,40);
            }
            else Line(C,guests.Count >= capacity ? "已满员，交接后继续接新单。" : "暂无送养请求，营业时会有主人来店。",16,Ui.Dim);

            foreach (var pet in guests)
            {
                var visit = new GameTimestamp(pet.NextVisitAt);
                PersonSummary(C, pet.OwnerId, pet.Name + (pet.Dog ? " · 狗" : " · 猫"),
                    (S.CityPerson(pet.OwnerId)?.Name ?? "主人") + $" · {pet.PaidWeeks+1}/{pet.Weeks} 周 · {visit.Date.Month}/{visit.Date.Day} {Ui.Hm(visit.MinuteOfDay)} 探望");
                Line(C,"状态 " + pet.Condition + "/100 · 快乐 "+pet.Happiness+"/100 · 预计收 " + GameSession.PetWeeklyPayment(pet),16);
                var care = PetCareStatus(shop,pet);
                Line(C,care.text,16,care.covered ? Ui.Good : PetUnattended);
                if (S.Clock.Now.TotalMinutes < pet.NextVisitAt) continue;
                string blocker = S.PetVisitBlocker(shop,pet);
                if (blocker != null) { Line(C,blocker,16,Ui.Dim); continue; }
                string label = pet.PaidWeeks+1 >= pet.Weeks ? "接回并结算本周" : "接待主人并结算本周";
                Ui.Size(Ui.Button(C,label,() => { if (S.SettlePetStay(pet.Id)) RebuildDock(); },17,Ui.ButtonHot),-1,38);
            }
            if (!string.IsNullOrEmpty(state?.LastPickup))
                Para(C,"最近结算 · " + state.LastPickup,16,Ui.Good);
        }

        private void BuildPetRenovation()
        {
            var shop = S.CurrentShop;
            int capacity = S.PetCapacity(shop), maximum = PetShopCatalog.MaximumCapacity(shop.Place.Size);
            Line(C,"宠物屋 · 当前 " + capacity + " 只 / 商铺上限 " + maximum + " 只",18,Ui.Accent);
            string next = capacity == 0 ? PetShopCatalog.SmallHouse
                : capacity == 4 ? PetShopCatalog.MediumHouse : capacity == 8 ? PetShopCatalog.LargeHouse : null;
            if (next != null && PetShopCatalog.HouseCapacity(next) <= maximum && S.FurnitureToPlace(true,next) == 0)
            {
                var def = FurnitureCatalog.Get(next);
                Ui.Size(Ui.Button(C,"购置" + def.Name + " · " + def.Price,() =>
                {
                    ConfirmBusiness("购置" + def.Name,"购置后可在装修中摆放。新宠物屋摆好时会替换原有宠物屋。",() =>
                    {
                        if (S.BuyPetHouse(next)) RebuildDock();
                    });
                },17),-1,38);
            }
            foreach (string id in new[] { PetShopCatalog.Wand, PetShopCatalog.Ball })
            {
                string fixture = PetShopCatalog.ToyFixture(id);
                if (S.PetToyReady(shop,id) || S.FurnitureToPlace(true,fixture) > 0) continue;
                int count = shop.Sim.Stock.GoodsCount(id);
                if (count <= 0) continue;
                Ui.Size(Ui.Button(C,"从库存取用并摆放" + Gifts.Get(id).Name,() =>
                {
                    if (S.PreparePetToy(id)) { RebuildDock(); StartPlacement(true,fixture); }
                },16),-1,34);
            }
            Line(C,"玩具可摆在围栏内或宠物屋旁，进入近景后即可拿起来。",15,Ui.Dim);
        }

        public void OpenPetFocusHud()
        {
            CloseDock();
            _travelBar.SetActive(false);
            _worldHintStrip.SetActive(false);
            _labelCanvas.enabled = false;
            _petFocusHud = Ui.Panel(_modalCanvas.transform,"PetFocusHud",new Color(.09f,.13f,.16f,.94f)).rectTransform;
            _petFocusHud.Anchor(0,0,1,0,20,18,20,-258);
            Ui.VList(_petFocusHud,6,9);
            var title = Ui.Row(_petFocusHud,36,8);
            _petFocusTitle = Ui.Size(Ui.Label(title,"",23,color:Ui.Accent),-1,36,1);
            _petFocusCoverage = Ui.Size(Ui.Label(title,"",19,TextAnchor.MiddleCenter),-1,36,2);
            Ui.Size(Ui.Button(title,"返回店铺 · Esc",_ctrl.ExitPetFocus,17),148,36);
            _petFocusChoices = Ui.Row(_petFocusHud,36,6);
            var body = Ui.Row(_petFocusHud,144,10);
            BuildSharedPetCare(body);
            BuildPetFocusDetails(body);
            RefreshPetFocusHud();
        }

        public void ClosePetFocusHud()
        {
            if (_petFocusHud != null)
            {
                _petFocusHud.gameObject.SetActive(false);
                Kill.It(_petFocusHud.gameObject);
            }
            _petFocusHud = null;
            _petFocusChoices = null;
            _petFocusButtons.Clear();
            _sharedPetCare.Clear();
            _travelBar.SetActive(true);
            _worldHintStrip.SetActive(true);
            _labelCanvas.enabled = !ExpoHudOpen;
            RefreshShopDockVisibility();
        }

        public void RefreshPetFocusHud(bool immediate = true)
        {
            if (_petFocusHud == null || (!immediate && UnityEngine.Time.unscaledTime < _nextPetFocusRefresh)) return;
            _nextPetFocusRefresh = UnityEngine.Time.unscaledTime + .15f;
            var shop = S.CurrentShop;
            var pets = S.PetGuests(shop);
            _petFocusTitle.text = $"宠物屋 · {pets.Count} / {S.PetCapacity(shop)} 只";
            var (coveredPets, missingKeepers) = S.PetKeeperCoverage(shop);
            string staffing = pets.Count == 0 ? "暂不需要饲养员"
                : missingKeepers == 0 ? "饲养员已配齐" : $"还需安排 {missingKeepers} 名饲养员";
            _petFocusCoverage.text = $"在岗照顾 {coveredPets} / {pets.Count} 只 · {staffing}";
            _petFocusCoverage.color = coveredPets < pets.Count ? PetUnattended : pets.Count > 0 ? Ui.Good : Ui.Dim;
            if (_petFocusChoices.childCount == 0 || !_petFocusButtons.Select(entry=>entry.id).SequenceEqual(pets.Select(pet=>pet.Id)))
            {
                Ui.Clear(_petFocusChoices);
                _petFocusButtons.Clear();
                if (pets.Count == 0)
                    Ui.Size(Ui.Label(_petFocusChoices,"宠物屋空着 · 可在柜台或订单页接单",17,color:Ui.Dim),-1,36,1);
                foreach (var pet in pets)
                {
                    var button = Ui.Size(Ui.Button(_petFocusChoices,pet.Name,()=>_ctrl.SelectBoardedPet(pet.Id),17),-1,36,1);
                    _petFocusButtons.Add((pet.Id,button));
                }
            }
            foreach (var (id,button) in _petFocusButtons)
                button.image.color = id == _ctrl.SelectedPet ? Ui.ButtonHot : Ui.ButtonBg;
            RefreshSharedPetCare(shop);
            RefreshPetFocusDetails(shop,pets.FirstOrDefault(p=>p.Id==_ctrl.SelectedPet));
        }

        private void BuildSharedPetCare(Transform parent)
        {
            var care = Ui.Panel(parent,"SharedPetCare",Ui.PanelBg2).rectTransform;
            Ui.Size(care,-1,144,3);
            Ui.VList(care,4,9);
            Ui.Size(Ui.Label(care,"宠物屋共用照料",20,color:Ui.Accent),-1,28);
            Ui.Size(Ui.Label(care,"水盆全屋共用，猫砂和猫狗粮按种类每天补一次。",15,color:Ui.Dim),-1,21);
            var supplies = Ui.Row(care,65,6);
            foreach (var action in GameSession.PetCareActions)
            {
                var cell = Ui.Size(Ui.Rect(supplies,"Care_"+action),-1,65,1);
                Ui.VList(cell,3,0);
                var status = Ui.Size(Ui.Label(cell,"",15,TextAnchor.MiddleCenter),-1,25);
                var button = Ui.Size(Ui.Button(cell,GameSession.PetCareName(action),()=>
                {
                    S.CareForBoardedPets(action);
                    RefreshPetFocusHud();
                },16,Ui.ButtonHot),-1,32);
                _sharedPetCare.Add(action,(status,button));
            }
        }

        private void RefreshSharedPetCare(OwnedShop shop)
        {
            foreach (var entry in _sharedPetCare)
            {
                var action = entry.Key;
                var (status,button) = entry.Value;
                bool water = action == PetCareAction.Water;
                bool done = S.PetCareDoneToday(shop,action);
                bool needed = S.HasPetsForCare(shop,action);
                int stock = S.PetCareStock(shop,action);
                status.text = done ? "今日已完成" : !needed ? "暂无需补充" : water ? "待换水" : $"待补 · 库存 {stock} 袋";
                status.color = done ? Ui.Good : needed ? Ui.Accent : Ui.Dim;
                button.interactable = needed && !done && (water || stock > 0);
            }
        }

        private void BuildPetFocusDetails(Transform parent)
        {
            var details = Ui.Panel(parent,"SelectedPetDetails",Ui.PanelBg2).rectTransform;
            Ui.Size(details,-1,144,2);
            Ui.VList(details,4,9);
            _petDetailName = Ui.Size(Ui.Label(details,"",21,color:Ui.Accent),-1,27);
            var metrics = Ui.Row(details,24,18);
            _petCondition = Ui.Size(Ui.Label(metrics,"",18,color:Ui.Good),-1,24,1);
            _petHappiness = Ui.Size(Ui.Label(metrics,"",18,color:Ui.Good),-1,24,1);
            _petCareStatus = Ui.Size(Ui.Label(metrics,"",17),-1,24,1);
            _petDetailInfo = Ui.Size(Ui.Label(details,"",15,color:Ui.Dim),-1,21);
            var controls = Ui.Row(details,32,8);
            _petPatButton = Ui.Size(Ui.Button(controls,"摸摸",()=>_ctrl.InteractWithPet(false),17,Ui.ButtonHot),100,32);
            _petToyButton = Ui.Size(Ui.Button(controls,"",UseSelectedPetToy,17),168,32);
            _petToyLabel = _petToyButton.GetComponentInChildren<Text>();
        }

        private void RefreshPetFocusDetails(OwnedShop shop,PetStay pet)
        {
            bool playing = _ctrl.Interior.PetActionPlaying;
            _petPatButton.interactable = pet != null && !playing;
            _petToyButton.gameObject.SetActive(pet != null);
            if (pet == null)
            {
                _petDetailName.text = "宠物详情";
                _petCondition.text = "";
                _petHappiness.text = "";
                _petCareStatus.text = "";
                _petDetailInfo.text = "入住后可点名字或点场景里的猫狗。";
                return;
            }

            _petDetailName.text = pet.Name + (pet.Dog ? " · 狗" : " · 猫");
            _petCondition.text = $"状态 {pet.Condition}/100";
            _petHappiness.text = $"快乐 {pet.Happiness}/100";
            var care = PetCareStatus(shop,pet);
            _petCareStatus.text = care.text;
            _petCareStatus.color = care.covered ? Ui.Good : PetUnattended;
            _petDetailInfo.text = pet.LastPlayDay == S.Clock.Today.TotalDays ? "今天已陪玩" : "今天还没陪玩";
            string toy = pet.Dog ? PetShopCatalog.Ball : PetShopCatalog.Wand;
            bool ready = S.PetToyReady(shop,toy);
            _petToyButton.interactable = !playing && ready;
            _petToyLabel.text = !ready ? (pet.Dog ? "尚未摆放软球" : "尚未摆放逗猫棒")
                : _ctrl.HeldPetToy == toy ? (pet.Dog ? "抛球 · F" : "挥动逗猫棒 · F")
                : (pet.Dog ? "拿球" : "拿逗猫棒");
        }

        private void UseSelectedPetToy()
        {
            var pet = S.PetGuests(S.CurrentShop).FirstOrDefault(p=>p.Id==_ctrl.SelectedPet);
            if (pet == null) return;
            string toy = pet.Dog ? PetShopCatalog.Ball : PetShopCatalog.Wand;
            if (_ctrl.HeldPetToy == toy) _ctrl.InteractWithPet(true);
            else _ctrl.PickUpPetToy(toy);
        }
    }
}
