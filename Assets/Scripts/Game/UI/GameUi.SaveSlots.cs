using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenSaveMenu()
        {
            if (!S.CanSave)
            {
                Toast(L("cannot_save"));
                return;
            }
            if (S.IsCityLifeTest)
            {
                Toast(L(_ctrl.SaveGame() ? "saved" : "save_failed"));
                return;
            }
            OpenSaveSlots(true, false);
        }

        public void OpenLoadMenu(bool confirmProgress = true) => OpenSaveSlots(false, confirmProgress);

        private void OpenSaveSlots(bool saving, bool confirmProgress)
        {
            string title = saving ? "save" : "load";
            OpenWindow(L(title), 1140, 664, w =>
            {
                MenuTitle(w, title);
                Ui.Size(Ui.Label(w.Content, L(saving ? "pick_save_slot" : "pick_load_slot"), 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 34);
                RectTransform row = null;
                for (int slot = 1; slot <= SaveStore.SlotCount; slot++)
                {
                    if ((slot - 1) % 3 == 0) row = Ui.Row(w.Content, 172, 12);
                    BuildSaveSlot(row, w, slot, saving, confirmProgress);
                }
            }, scrollContent: false, allowDuringOpeningMeal: true);
        }

        private void BuildSaveSlot(Transform row, Window window, int slot, bool saving, bool confirmProgress)
        {
            var manual = SaveStore.InspectSlot(slot, false);
            var auto = SaveStore.InspectSlot(slot, true);
            bool occupied = manual.Exists || auto.Exists;
            // An automatic snapshot from a previous campaign must not appear next to its replacement.
            bool hasVersions = manual.Readable && auto.Readable && manual.Campaign == auto.Campaign;
            var latest = auto.Readable && (!manual.Readable || (hasVersions && auto.Stamp > manual.Stamp)) ? auto : manual;
            bool active = _ctrl.ActiveSaveSlot == slot;
            string number = slot.ToString("00");
            string title = MenuLocalization.Format("save_slot_number", number);

            var card = Ui.Size(Ui.Button(row, "", () =>
            {
                if (saving)
                    SaveIntoSlot(window, slot, number);
                else if (hasVersions)
                    ChooseSaveVersion(slot, title, latest, latest.Automatic ? manual : auto, confirmProgress);
                else
                    LoadSaveVersion(slot, latest, confirmProgress);
            }, bg: active ? Ui.SelectedButton(true) : occupied ? Ui.ButtonBg : Ui.FlatWell, interactable: saving || occupied), 0, -1, 1);

            var name = SlotLabel(card.transform, title, 22, 1, -44, 11, Ui.TextColor);
            name.fontStyle = FontStyle.Bold;
            if (active)
                SlotLabel(card.transform, L("current_save_slot"), 14, 1, -42, 12, Ui.Accent, TextAnchor.MiddleRight);
            Ui.Panel(card.transform, "SlotDivider", active ? Ui.FlatHotEdge : Ui.FlatButtonEdge)
                .rectTransform.Anchor(0, 1, 1, 1, 16, -48, 16, 47);

            if (!latest.Readable)
            {
                SlotLabel(card.transform, L(occupied ? "unreadable_save_slot" : "empty_save_slot"), 18, 1, -113, 72, occupied ? Ui.Warn : Ui.Dim);
                if (saving && !occupied)
                    SlotLabel(card.transform, L("save_in_empty_slot"), 15, 0, 9, -36, Ui.Accent);
                return;
            }

            string place = !string.IsNullOrEmpty(latest.ShopName) ? latest.ShopName
                : _ctrl.City.HasPlace(latest.Place) ? _ctrl.City.GetPlace(latest.Place).Name : L("slot_in_town");
            var placeText = SlotLabel(card.transform, place, 21, 1, -84, 53, Ui.TextColor);
            placeText.fontStyle = FontStyle.Bold;
            var gameTime = SlotLabel(card.transform, SaveGameTime(latest), 16, 1, -112, 85, Ui.Dim);
            gameTime.rectTransform.Anchor(0, 1, .56f, 1, 16, -112, 4, 85);
            var cash = SlotLabel(card.transform, Money.FromCents(latest.BalanceCents).ToString(), 18, 1, -112, 85, Ui.Good, TextAnchor.MiddleRight);
            cash.rectTransform.Anchor(.56f, 1, 1, 1, 4, -112, 16, 85);
            cash.resizeTextForBestFit = true;
            cash.resizeTextMinSize = 11;
            cash.resizeTextMaxSize = 18;
            SlotLabel(card.transform, latest.SavedAt ?? "", 14, 0, 11, -40, Ui.Dim);
            SlotLabel(card.transform, L(hasVersions ? "slot_versions" : VersionKey(latest)), 14, 0, 11, -40, Ui.Accent, TextAnchor.MiddleRight);
        }

        private static Text SlotLabel(Transform parent, string text, int size, float anchorY, int bottom, int top,
            Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Ui.Label(parent, text, size, alignment, color);
            label.supportRichText = false;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.rectTransform.Anchor(0, anchorY, 1, anchorY, 16, bottom, 16, top);
            return label;
        }

        private static string VersionKey(SaveStore.SlotInfo info) => info.Automatic ? "auto_save" : "manual_save";

        private static string SaveGameTime(SaveStore.SlotInfo info)
        {
            var date = new GameDate(info.Day);
            return MenuLocalization.Format("slot_game_date", date.Year) + $" · {date.Month:00}/{date.Day:00} {Ui.Hm(info.Minute)}";
        }

        private void SaveIntoSlot(Window window, int slot, string number)
        {
            void Write()
            {
                if (_ctrl.SaveGame(slot))
                {
                    window.Close();
                    Toast(MenuLocalization.Format("saved_in_slot", number));
                }
                else Toast(L(S.CanSave ? "save_failed" : "cannot_save"));
            }
            if (SaveStore.IsOccupied(slot))
                ConfirmMenuAction("overwrite_save", "overwrite_save_prompt", "overwrite_save", Write, number);
            else Write();
        }

        private void ChooseSaveVersion(int slot, string title, SaveStore.SlotInfo latest, SaveStore.SlotInfo older, bool confirmProgress)
        {
            OpenWindow(title, 640, 330, w =>
            {
                w.TitleText.text = title;
                foreach (var version in new[] { latest, older })
                {
                    string when = version.SavedAt == null ? "" : " · " + version.SavedAt;
                    string caption = L(VersionKey(version)) + when + "\n" + SaveGameTime(version);
                    Ui.Size(Ui.Button(w.Content, caption, () => LoadSaveVersion(slot, version, confirmProgress), 21,
                        Ui.SelectedButton(version == latest)), -1, 69);
                }
            }, allowDuringOpeningMeal: true);
        }

        private void LoadSaveVersion(int slot, SaveStore.SlotInfo version, bool confirmProgress)
        {
            if (!version.Readable)
            {
                OpenMenuMessage("load_failed", "load_failed_body");
                return;
            }
            void Load()
            {
                if (!_ctrl.TryLoadGame(slot, version.Automatic)) OpenMenuMessage("load_failed", "load_failed_body");
            }
            if (confirmProgress) ConfirmMenuAction("load", "load_confirm", "load", Load);
            else Load();
        }
    }
}
