using System;
using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private const string ItemArtPath = "ItemPortraits/";
        private const int ItemSheetColumns = 5;

        [Serializable]
        private sealed class ItemArtCatalog
        {
            public ItemArtSheet[] sheets;
        }

        [Serializable]
        private sealed class ItemArtSheet
        {
            public string texture;
            public string[] items;
        }

        private static Dictionary<string, (string sheet, int cell)> _itemArt;
        private static readonly Dictionary<string, Texture2D> ItemSheets = new Dictionary<string, Texture2D>();

        private static Dictionary<string, (string sheet, int cell)> LoadItemArt()
        {
            var result = new Dictionary<string, (string, int)>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>(ItemArtPath + "catalog");
            if (asset == null) return result;
            foreach (var sheet in JsonUtility.FromJson<ItemArtCatalog>(asset.text).sheets)
                for (int cell = 0; cell < sheet.items.Length; cell++)
                    if (!string.IsNullOrEmpty(sheet.items[cell])) result.Add(sheet.items[cell], (sheet.texture, cell));
            return result;
        }

        private static string ItemArtId(string key)
        {
            if (string.IsNullOrEmpty(key)) return "unknown";
            int source = key.IndexOf('|');
            if (source >= 0) key = key.Substring(source + 1);
            int separator = key.IndexOf(':');
            if (separator < 0) return key;
            string id = key.Substring(separator + 1);
            switch (key.Substring(0, separator))
            {
                case "gift": case "stockgift": case "goods": case "wholesale": case "expo":
                case "clothing": case "outfit": case "ing": case "produce":
                    return ItemArtId(id);
                case "furn": return id == FurnitureCatalog.ClothingRack || id == FurnitureCatalog.ClothingFold ? id : FurnitureCatalog.Canonical(id);
                case "part": return id.Split(':')[0];
                case "upgrade" when id == "phone": return id;
                case "lunch": case "cooked":
                    if (id.StartsWith("fast:", StringComparison.Ordinal)) return id;
                    int fish = id.IndexOf('+');
                    if (fish >= 0) return "meal:fish";
                    return "cooked:" + (id == "rice" ? "plain" : id);
                case "oldmarket": return key.EndsWith(":faulty", StringComparison.Ordinal) ? key.Substring(0, key.Length - 7) : key;
                default: return key;
            }
        }

        private static RectTransform ItemPortrait(Transform row, string key, float size = 52)
        {
            var slot = Ui.Size(Ui.Rect(row, "ItemPortraitSlot"), size, size);
            var square = Ui.Rect(slot, "ItemPortraitSquare");
            square.anchorMin = square.anchorMax = new Vector2(.5f, .5f);
            square.sizeDelta = new Vector2(size, size);
            FillItemPortrait(square, key).Fill();
            return slot;
        }

        private static RectTransform FillItemPortrait(Transform parent, string key)
        {
            var rect = Ui.Rect(parent, "ItemPortrait_" + key);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            string id = ItemArtId(key);
            if (id.StartsWith("vehicle:", StringComparison.Ordinal))
            {
                string model = id.Substring("vehicle:".Length);
                if (Enum.TryParse(model, out VehicleKind kind)) model = MotorCatalog.Legacy(kind)?.Id;
                image.texture = MotorPortraitArt.Get(model);
                if (image.texture != null)
                {
                    var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
                    fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    fitter.aspectRatio = (float)image.texture.width / image.texture.height;
                    return rect;
                }
            }

            _itemArt ??= LoadItemArt();
            if (!_itemArt.TryGetValue(id, out var entry) && !_itemArt.TryGetValue("unknown", out entry))
            {
                image.enabled = false;
                return rect;
            }
            if (!ItemSheets.TryGetValue(entry.sheet, out var texture))
                ItemSheets[entry.sheet] = texture = Resources.Load<Texture2D>(ItemArtPath + entry.sheet);
            if (texture == null)
            {
                image.enabled = false;
                return rect;
            }
            image.texture = texture;
            float cellSize = 1f / ItemSheetColumns;
            float insetX = 1f / texture.width, insetY = 1f / texture.height;
            int col = entry.cell % ItemSheetColumns, row = entry.cell / ItemSheetColumns;
            image.uvRect = new Rect(col * cellSize + insetX, 1f - (row + 1) * cellSize + insetY,
                cellSize - 2 * insetX, cellSize - 2 * insetY);
            return rect;
        }

        private static Text ItemLabel(Transform parent, string key, string text, int fontSize = 16, float size = 34, Color? color = null)
        {
            var row = Ui.Row(parent, size + 4, 8);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            ItemPortrait(row, key, size);
            return Ui.Size(Ui.Label(row, text, fontSize, color: color), 0, -1, 1);
        }

        private static void ItemButtonPortrait(Button button, string key, float size)
        {
            var slot = Ui.Rect(button.transform, "ItemPortraitSlot");
            slot.anchorMin = slot.anchorMax = new Vector2(0, .5f);
            slot.pivot = new Vector2(0, .5f);
            slot.anchoredPosition = new Vector2(5, 0);
            slot.sizeDelta = new Vector2(size, size);
            FillItemPortrait(slot, key).Fill();
            var caption = button.GetComponentInChildren<Text>();
            caption.rectTransform.Anchor(0, 0, 1, 1, size + 10, 0, 4, 0);
        }
    }
}
