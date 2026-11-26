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
            const float windowWidth = 1480;
            const float cardHeight = 132;
            const float gap = 10;
            OpenWindow(L(title), windowWidth, 780, w =>
            {
                MenuTitle(w, title);
                float availableWidth = Mathf.Min(windowWidth, w.Root.rect.width - 32) - 46;
                int columns = Mathf.Clamp(Mathf.FloorToInt((availableWidth + gap) / (234 + gap)), 2, 5);
                var header = Ui.Row(w.Content, cardHeight, gap);
                Ui.Size(BuildSaveSlot(header, w, SaveStore.AutomaticSlot, saving, confirmProgress), 0, -1, 1);
                var hint = Ui.Label(header, L(saving ? "pick_save_slot" : "pick_load_slot"), 17, TextAnchor.MiddleLeft, Ui.Dim);
                Ui.Size(hint, (columns - 2) * gap, -1, columns - 1);

                for (int firstSlot = 1; firstSlot <= SaveStore.SlotCount; firstSlot += columns)
                {
                    var row = Ui.Row(w.Content, cardHeight, gap);
                    for (int slot = firstSlot; slot < firstSlot + columns; slot++)
                    {
                        if (slot <= SaveStore.SlotCount)
                            Ui.Size(BuildSaveSlot(row, w, slot, saving, confirmProgress), 0, -1, 1);
                        else
                            Ui.Size(Ui.Rect(row, "SaveSlotSpacer"), 0, -1, 1);
                    }
                }
            }, allowDuringOpeningMeal: true, fitContent: true);
        }

        private Button BuildSaveSlot(Transform row, Window window, int slot, bool saving, bool confirmProgress)
        {
            bool automatic = slot == SaveStore.AutomaticSlot;
            var latest = SaveStore.InspectSlot(slot, automatic);
            bool occupied = latest.Exists;
            bool active = !automatic && _ctrl.ActiveSaveSlot == slot;
            string number = slot.ToString("00");
            string title = automatic ? L("auto_save") : MenuLocalization.Format("save_slot_number", number);

            var card = Ui.Button(row, "", () =>
            {
                if (saving && !automatic)
                    SaveIntoSlot(window, slot, number);
                else
                    LoadSaveVersion(slot, latest, confirmProgress);
            }, bg: Ui.SelectedButton(active),
                interactable: automatic ? !saving && occupied : saving || occupied);

            var name = SlotLabel(card.transform, title, 21, 1, -35, 7, Ui.TextColor);
            name.fontStyle = FontStyle.Bold;
            if (active)
                SlotLabel(card.transform, L("current_save_slot"), 14, 1, -35, 7, Ui.Accent, TextAnchor.MiddleRight);
            Ui.Panel(card.transform, "SlotDivider", Ui.FlatButtonEdge)
                .rectTransform.Anchor(0, 1, 1, 1, 12, -39, 12, 38);

            if (!latest.Readable)
            {
                SlotLabel(card.transform, L(occupied ? "unreadable_save_slot" : "empty_save_slot"), 18, 1, -90, 53, occupied ? Ui.Warn : Ui.Dim);
                if (saving && !automatic && !occupied)
                    SlotLabel(card.transform, L("save_in_empty_slot"), 15, 0, 7, -29, Ui.Accent);
                return card;
            }

            string place = !string.IsNullOrEmpty(latest.ShopName) ? latest.ShopName
                : _ctrl.City.HasPlace(latest.Place) ? _ctrl.City.GetPlace(latest.Place).Name : L("slot_in_town");
            var placeText = SlotLabel(card.transform, place, 20, 1, -73, 44, Ui.TextColor);
            placeText.fontStyle = FontStyle.Bold;
            placeText.resizeTextForBestFit = true;
            placeText.resizeTextMinSize = 14;
            placeText.resizeTextMaxSize = 20;
            var gameTime = SlotLabel(card.transform, SaveGameTime(latest), 15, 1, -99, 75, Ui.Dim);
            gameTime.rectTransform.Anchor(0, 1, .57f, 1, 12, -99, 2, 75);
            gameTime.resizeTextForBestFit = true;
            gameTime.resizeTextMinSize = 12;
            gameTime.resizeTextMaxSize = 15;
            var cash = SlotLabel(card.transform, Money.FromCents(latest.BalanceCents).ToString(), 18, 1, -99, 75, Ui.Good, TextAnchor.MiddleRight);
            cash.rectTransform.Anchor(.57f, 1, 1, 1, 2, -99, 12, 75);
            cash.resizeTextForBestFit = true;
            cash.resizeTextMinSize = 11;
            cash.resizeTextMaxSize = 18;
            SlotLabel(card.transform, latest.SavedAt ?? "", 14, 0, 7, -29, Ui.Dim);
            SlotLabel(card.transform, L(VersionKey(latest)), 14, 0, 7, -29, Ui.Accent, TextAnchor.MiddleRight);
            return card;
        }

        private static Text SlotLabel(Transform parent, string text, int size, float anchorY, int bottom, int top,
            Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Ui.Label(parent, text, size, alignment, color);
            label.supportRichText = false;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.rectTransform.Anchor(0, anchorY, 1, anchorY, 12, bottom, 12, top);
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

        private void LoadSaveVersion(int slot, SaveStore.SlotInfo version, bool confirmProgress)
        {
            if (!version.Readable)
            {
                OpenMenuMessage("load_failed", "load_failed_body");
                return;
            }
            void Load() => _ctrl.EnterGame(() =>
            {
                if (!_ctrl.TryLoadGame(slot, version.Automatic)) OpenMenuMessage("load_failed", "load_failed_body");
            });
            if (confirmProgress) ConfirmMenuAction("load", "load_confirm", "load", Load);
            else Load();
        }
    }
}
