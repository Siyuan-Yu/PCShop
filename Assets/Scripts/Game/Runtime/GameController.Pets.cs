using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private string _petFocusShop, _petToy;
        private float _petPreviousSize, _petPreviousYaw, _petPreviousPitch;
        private long _selectedPet = -1;
        private bool _petActionWasPlaying;
        public bool PetFocusActive => _petFocusShop != null;
        public long SelectedPet => _selectedPet;
        public string HeldPetToy => _petToy;

        private void EnterPetFocus()
        {
            if (!InInterior || !Interior.NearPetHouse || PetFocusActive) return;
            _petFocusShop = Session.CurrentShop.Place.Id;
            _petPreviousSize = _rig.Size;
            _petPreviousYaw = CameraRig.Yaw;
            _petPreviousPitch = CameraRig.Pitch;
            _rig.SetPlaceAngles(CameraRig.DefaultYaw,43);
            _rig.Size = Interior.PetCameraSize;
            Interior.SetPetReachSide(CameraRig.Rotation * Vector3.back);
            _selectedPet = Session.PetGuests(Session.CurrentShop).FirstOrDefault()?.Id ?? -1;
            Ui.OpenPetFocusHud();
        }

        public void ExitPetFocus()
        {
            if (!PetFocusActive) return;
            _petFocusShop = null;
            _petToy = null;
            _selectedPet = -1;
            _petActionWasPlaying = false;
            Interior?.EndPetInteraction();
            _rig.Size = _petPreviousSize;
            _rig.SetPlaceAngles(_petPreviousYaw,_petPreviousPitch);
            Ui.ClosePetFocusHud();
        }

        private void UpdatePetFocus()
        {
            if (!PetFocusActive) return;
            if (!InInterior || Session.IsGameOver || !Interior.PetHousePresent || Session.CurrentShop.Place.Id != _petFocusShop)
            {
                ExitPetFocus();
                return;
            }
            var pets = Session.PetGuests(Session.CurrentShop);
            var selected = pets.FirstOrDefault(p=>p.Id==_selectedPet);
            if (selected == null)
                SelectBoardedPet(pets.FirstOrDefault()?.Id ?? -1);
            else ClearIncompatiblePetToy(selected);
            Interior.MarkPet(_selectedPet);
            bool playing = Interior.PetActionPlaying;
            Ui.RefreshPetFocusHud(_petActionWasPlaying != playing);
            _petActionWasPlaying = playing;
        }

        public void SelectBoardedPet(long id)
        {
            if (!PetFocusActive || Interior.PetActionPlaying || _selectedPet == id) return;
            var pet = Session.PetGuests(Session.CurrentShop).FirstOrDefault(p=>p.Id==id);
            if (id >= 0 && pet == null) return;
            _selectedPet = id;
            ClearIncompatiblePetToy(pet);
            Interior.MarkPet(id);
            Ui.RefreshPetFocusHud();
        }

        private void ClearIncompatiblePetToy(PetStay pet)
        {
            if (_petToy == null || Interior.PetActionPlaying) return;
            if (pet != null && _petToy == (pet.Dog ? PetShopCatalog.Ball : PetShopCatalog.Wand)) return;
            _petToy = null;
            Interior.HoldPetToy(null);
        }

        public void PickUpPetToy(string toy)
        {
            if (!PetFocusActive || Interior.PetActionPlaying) return;
            var station = Session.PetToy(Session.CurrentShop,toy);
            if (station == null) return;
            var pet = Session.PetGuests(Session.CurrentShop).FirstOrDefault(p => p.Id == _selectedPet);
            if (pet == null || toy != (pet.Dog ? PetShopCatalog.Ball : PetShopCatalog.Wand)) return;
            _petToy = toy;
            Interior.HoldPetToy(toy,station);
            Ui.RefreshPetFocusHud();
        }

        public void InteractWithPet(bool useToy)
        {
            if (!PetFocusActive || Interior.PetActionPlaying || useToy && _petToy == null) return;
            string toy = useToy ? _petToy : null;
            if (!Session.PlayWithBoardedPet(_selectedPet,toy)) return;
            if (!useToy && _petToy != null)
            {
                _petToy = null;
                Interior.HoldPetToy(null);
            }
            Interior.AnimatePetAction(_selectedPet,toy);
            _petActionWasPlaying = Interior.PetActionPlaying;
            Ui.RefreshPetFocusHud();
        }

        private void HandlePetFocusKeys()
        {
            if (Ui.AnyModalOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Ui.CloseTopWindow();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { ExitPetFocus(); return; }
            if (Input.GetKeyDown(KeyCode.Space)) Session.Time.TogglePause();
            HandleTimeSpeedKeys();
            HandleHudPanelKeys();
            if (Ui.AnyModalOpen) return;
            if (Input.GetKeyDown(KeyCode.F)) InteractWithPet(_petToy != null);
            if (!Input.GetMouseButtonDown(0) || GameUi.PointerOverUi) return;
            var ray = Cam.ScreenPointToRay(Input.mousePosition);
            long picked = Interior.PickPet(ray,out float petDistance);
            if (Interior.TryPickPetCare(ray,out var care,out float propDistance) && propDistance < petDistance)
            {
                Session.CareForBoardedPets(care);
                Ui.RefreshPetFocusHud();
                return;
            }
            if (picked >= 0) { SelectBoardedPet(picked); return; }
            var stay = Session.PetGuests(Session.CurrentShop).FirstOrDefault(p => p.Id == _selectedPet);
            if (stay != null)
            {
                string toyId = stay.Dog ? PetShopCatalog.Ball : PetShopCatalog.Wand;
                var toy = Session.PetToy(Session.CurrentShop,toyId);
                if (toy != null)
                {
                    var point = ShopInteriorView.Origin + new Vector3((float)toy.X,.14f,(float)toy.Z);
                    if (new Bounds(point,new Vector3(.40f,.32f,.40f)).IntersectRay(ray))
                    {
                        PickUpPetToy(toyId);
                        return;
                    }
                }
            }
        }
    }
}
