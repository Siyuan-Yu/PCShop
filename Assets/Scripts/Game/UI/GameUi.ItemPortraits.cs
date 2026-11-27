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
        private sealed class ItemArtRegion
        {
            public float x, y, width, height;
            public Rect Rect => new Rect(x, y, width, height);
        }

        [Serializable]
        private sealed class ItemArtSheet
        {
            public string texture;
            public string[] items;
            public ItemArtRegion[] regions;
        }

        private static Dictionary<string, (string sheet, Rect region)> _itemArt;
        private static readonly Dictionary<string, Texture2D> ItemSheets = new Dictionary<string, Texture2D>();

        private static Dictionary<string, (string sheet, Rect region)> LoadItemArt()
        {
            var result = new Dictionary<string, (string, Rect)>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>(ItemArtPath + "catalog");
            if (asset == null) return result;
            foreach (var sheet in JsonUtility.FromJson<ItemArtCatalog>(asset.text).sheets)
                for (int cell = 0; cell < sheet.items.Length; cell++)
                    if (!string.IsNullOrEmpty(sheet.items[cell]))
                    {
                        float unit = 1f / ItemSheetColumns;
                        Rect region = sheet.regions != null && cell < sheet.regions.Length ? sheet.regions[cell].Rect
                            : new Rect(cell % ItemSheetColumns * unit, 1 - (cell / ItemSheetColumns + 1) * unit, unit, unit);
                        result.Add(sheet.items[cell], (sheet.texture, region));
                    }
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
                case "used": return id == "5" ? "oldmarket:camera" : id == "7" ? "oldmarket:speaker" : key;
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
            var art = Ui.Rect(rect, "PaintedItem").Fill();
            var image = art.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            string id = ItemArtId(key);
            if (id.StartsWith("vehicle:", StringComparison.Ordinal))
            {
                string model = id.Substring("vehicle:".Length);
                if (Enum.TryParse(model, out VehicleKind kind)) model = MotorCatalog.Legacy(kind)?.Id;
                image.texture = MotorPortraitArt.Get(model);
                if (image.texture != null)
                {
                    var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
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
            // 图集自带透明通道；旧的蓝底色键会误删显卡、衣服和鱼的蓝色。
            image.uvRect = entry.region;
            var aspect = art.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = entry.region.width * texture.width / (entry.region.height * texture.height);
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
