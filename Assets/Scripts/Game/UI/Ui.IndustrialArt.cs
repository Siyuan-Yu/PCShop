using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    internal enum UiIcon
    {
        None,
        Shop, Home, Agency, Wholesale, Leisure, PublicServices, Station, Garage,
        Hunger, Energy, Health, Stress, Prestige, Cash, Clock, Load, Intelligence, Charm, Hands, Fitness,
        AppStore, Contacts, Settings, Calendar, MealDelivery, OnlineShopping, Loans, Live, Stocks,
        Counter, Stock, Pricing, Orders, Renovate, Employees, Shelves, Display, Assembly, Report, Procurement, Brand,
        Sleep, Cooker, Fridge, Wardrobe, Repair, Workbench, Parcel, Weather, Notice, Quests, Relations, Ledger,
        Backpack, Phone, Racing, Walk, Car, Truck,
    }

    public static partial class Ui
    {
        private static readonly Dictionary<string, Texture2D> IconSheets = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<UiIcon, Sprite> Icons = new Dictionary<UiIcon, Sprite>();

        internal static UiIcon PhoneIcon(string id) => id switch
        {
            PhoneApps.Store => UiIcon.AppStore,
            PhoneApps.Contacts => UiIcon.Contacts,
            PhoneApps.Settings => UiIcon.Settings,
            PhoneApps.Schedule => UiIcon.Calendar,
            PhoneApps.FoodDelivery => UiIcon.MealDelivery,
            PhoneApps.Shop => UiIcon.OnlineShopping,
            PhoneApps.Loans => UiIcon.Loans,
            PhoneApps.Live => UiIcon.Live,
            PhoneApps.Securities => UiIcon.Stocks,
            _ => UiIcon.None
        };

        internal static UiIcon WindowIcon(string title)
        {
            int separator = title.IndexOf('·');
            string name = separator >= 0 ? title.Substring(0, separator).TrimEnd() : title;
            return name switch
            {
                "背包" => UiIcon.Backpack, "手机" or "按键手机" => UiIcon.Phone,
                "应用商店" => UiIcon.AppStore, "联系人" or "通讯录" => UiIcon.Contacts,
                "关系" or "关系图" => UiIcon.Relations, "日程" or "日历" => UiIcon.Calendar,
                "任务" or "当前任务" => UiIcon.Quests, "通知" or "通知记录" => UiIcon.Notice,
                "经营" or "经营趋势" or "经营报表" => UiIcon.Report, "员工" => UiIcon.Employees,
                "物价" or "定价" => UiIcon.Pricing, "品牌" => UiIcon.Brand, "赛车" => UiIcon.Racing,
                "现金流水" or "账本" => UiIcon.Ledger, "叫货车" => UiIcon.Truck,
                "睡觉" => UiIcon.Sleep, "电饭锅" or "电饭煲" => UiIcon.Cooker, "冰箱" => UiIcon.Fridge,
                "工作台" => UiIcon.Workbench, "衣柜" => UiIcon.Wardrobe, "维修" => UiIcon.Repair,
                "装机台" => UiIcon.Assembly, "车站" => UiIcon.Station, "车库" or "车行" => UiIcon.Garage,
                "房产中介" => UiIcon.Agency, "设置" => UiIcon.Settings,
                _ => UiIcon.None
            };
        }

        private static readonly RectInt[] PhoneTiles = Tiles(
            new[] { 241, 543, 839 }, new[] { 193, 192, 193 }, new[] { 30, 259, 478 }, new[] { 175, 168, 169 });
        private static readonly RectInt[] StatusTiles = Tiles(
            new[] { 89, 385, 675, 970 }, new[] { 216, 215, 214, 214 }, new[] { 32, 266, 485 }, new[] { 177, 168, 165 });
        private static readonly RectInt[] ShopTiles = Tiles(
            new[] { 106, 388, 672, 952 }, new[] { 214, 213, 212, 212 }, new[] { 30, 263, 482 }, new[] { 183, 172, 171 });
        private static readonly RectInt[] HomeTiles = Tiles(
            new[] { 119, 397, 672, 943 }, new[] { 215, 210, 209, 210 }, new[] { 32, 266, 485 }, new[] { 181, 168, 162 });
        private static readonly RectInt[] PlaceTiles = Tiles(
            new[] { 57, 361, 663, 966 }, new[] { 246, 244, 247, 246 }, new[] { 49, 370 }, new[] { 239, 239 });
        private static readonly RectInt[] ControlTiles =
        {
            new RectInt(879, 362, 59, 59), new RectInt(955, 365, 55, 56),
            new RectInt(1106, 453, 58, 52), new RectInt(951, 534, 55, 54),
            new RectInt(874, 533, 67, 55), new RectInt(1179, 454, 65, 53)
        };

        private static RectInt[] Tiles(int[] x, int[] widths, int[] y, int[] heights)
        {
            var rects = new RectInt[x.Length * y.Length];
            for (int row = 0; row < y.Length; row++)
                for (int column = 0; column < x.Length; column++)
                    rects[row * x.Length + column] = new RectInt(x[column], y[row], widths[column], heights[row]);
            return rects;
        }

        private static Sprite IconSprite(UiIcon key)
        {
            if (key == UiIcon.None) return null;
            if (Icons.TryGetValue(key, out var icon) && icon != null) return icon;
            int id = (int)key;
            var (sheet, first, tiles) = id switch
            {
                >= (int)UiIcon.Backpack => ("Controls", UiIcon.Backpack, ControlTiles),
                >= (int)UiIcon.Sleep => ("Home", UiIcon.Sleep, HomeTiles),
                >= (int)UiIcon.Counter => ("Shop", UiIcon.Counter, ShopTiles),
                >= (int)UiIcon.AppStore => ("Phone", UiIcon.AppStore, PhoneTiles),
                >= (int)UiIcon.Hunger => ("Status", UiIcon.Hunger, StatusTiles),
                _ => ("Places", UiIcon.Shop, PlaceTiles)
            };
            if (!IconSheets.TryGetValue(sheet, out var texture) || texture == null)
            {
                texture = Resources.Load<Texture2D>("UiArt/" + sheet);
                if (texture == null) return null;
                IconSheets[sheet] = texture;
            }
            var tile = tiles[id - (int)first];
            float scaleX = texture.width / 1280f;
            float scaleY = texture.height / 720f;
            var crop = new Rect(tile.x * scaleX, (720 - tile.yMax) * scaleY, tile.width * scaleX, tile.height * scaleY);
            icon = Sprite.Create(texture, crop, Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            icon.name = "界面图标 / " + key;
            Icons[key] = icon;
            return icon;
        }

        internal static Image Artwork(Transform parent, UiIcon key)
        {
            var sprite = IconSprite(key);
            if (sprite == null) return null;
            var image = Rect(parent, "Artwork").gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        internal static void ButtonArtwork(Button button, UiIcon key, string shortcut = null, bool stacked = false)
        {
            var caption = button.GetComponentInChildren<Text>();
            var icon = Artwork(button.transform, key);
            if (icon != null) UseCaptionIcon(button, icon);
            if (stacked && icon != null)
            {
                icon.rectTransform.Anchor(.5f, 0, .5f, 1, -24, 23, -24, 6);
                caption.rectTransform.Anchor(0, 0, 1, 0, 2, 4, 2, -22);
                caption.alignment = TextAnchor.MiddleCenter;
                return;
            }

            float left = 6;
            float right = 6;
            if (icon != null)
            {
                icon.rectTransform.Anchor(0, .5f, 0, .5f, 7, -10, -27, -10);
                left = 31;
            }
            if (!string.IsNullOrEmpty(shortcut))
            {
                float keyWidth = shortcut.Length == 1 ? 20f : 30f;
                var hint = Label(button.transform, shortcut, 13, TextAnchor.MiddleCenter,
                    button.image.color == ChromeButton ? ChromeDim : Dim);
                hint.name = "NavigationShortcut";
                hint.rectTransform.Anchor(1, .5f, 1, .5f, -keyWidth - 4, -11, 4, -11);
                right = keyWidth + 6;
            }

            caption.rectTransform.Anchor(0, 0, 1, 1, left, 1, right, 1);
            caption.alignment = icon != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 12;
            caption.resizeTextMaxSize = caption.fontSize;
        }
    }
}
