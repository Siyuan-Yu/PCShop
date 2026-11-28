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
        Backpack, Phone, Racing, Walk, Car, Truck, Profile, Play, Save, Folder, Help, About, Door, Power,
    }

    public static partial class Ui
    {
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
                "我的" or "个人资料" => UiIcon.Profile,
                "背包" => UiIcon.Backpack, "手机" or "按键手机" => UiIcon.Phone,
                "应用商店" => UiIcon.AppStore, "联系人" or "通讯录" => UiIcon.Contacts,
                "关系" or "关系图" => UiIcon.Relations, "日程" or "日历" => UiIcon.Calendar,
                "任务" or "当前任务" => UiIcon.Quests, "通知" or "通知记录" => UiIcon.Notice,
                "经营" or "经营趋势" or "经营报表" => UiIcon.Report, "员工" => UiIcon.Employees,
                "物价" or "定价" => UiIcon.Pricing, "品牌" => UiIcon.Brand, "赛车" => UiIcon.Racing,
                "现金流水" or "账本" => UiIcon.Ledger, "叫货车" => UiIcon.Truck,
                "睡觉" => UiIcon.Sleep, "电饭锅" or "电饭煲" => UiIcon.Cooker, "冰箱" => UiIcon.Fridge,
                "工作台" or "居家工作台" => UiIcon.Workbench, "衣柜" => UiIcon.Wardrobe, "维修" => UiIcon.Repair,
                "装机台" => UiIcon.Assembly, "车站" => UiIcon.Station, "车库" or "车行" => UiIcon.Garage,
                "房产中介" => UiIcon.Agency, "设置" => UiIcon.Settings, "帮助" => UiIcon.Help, "关于" => UiIcon.About,
                "读档" or "加载存档" => UiIcon.Folder, "保存游戏" or "存档" => UiIcon.Save,
                _ => UiIcon.None
            };
        }

        private static Sprite IconSprite(UiIcon key)
        {
            if (key == UiIcon.None) return null;
            if (Icons.TryGetValue(key, out var icon)) return icon;
            icon = Resources.Load<Sprite>("UiArt/Icons/" + key);
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

        internal static void ButtonArtwork(Button button, UiIcon key, string shortcut = null, bool stacked = false,
            int size = 40, int trailingSpace = 6)
        {
            var caption = button.GetComponentInChildren<Text>();
            var icon = Artwork(button.transform, key);
            if (icon != null) UseCaptionIcon(button, icon);
            float right = trailingSpace;
            if (!string.IsNullOrEmpty(shortcut))
            {
                float keyWidth = shortcut.Length == 1 ? 20f : 30f;
                var hint = Label(button.transform, shortcut, 17, TextAnchor.MiddleCenter,
                    button.image.color == ChromeButton ? ChromeDim : Dim);
                hint.name = "NavigationShortcut";
                hint.rectTransform.Anchor(1, .5f, 1, .5f, -keyWidth - 4, -11, 4, -11);
                right = keyWidth + 6;
            }

            caption.verticalOverflow = VerticalWrapMode.Truncate;
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = Mathf.Min(caption.fontSize, 17);
            caption.resizeTextMaxSize = caption.fontSize;
            button.gameObject.AddComponent<ButtonArtworkLayout>().Setup(caption, icon, Mathf.Max(36, size), right, stacked);
        }
    }

    [ExecuteAlways]
    internal sealed class ButtonArtworkLayout : MonoBehaviour
    {
        private Text _caption;
        private Image _icon;
        private int _size;
        private float _right;
        private bool _stacked;

        internal void Setup(Text caption, Image icon, int size, float right, bool stacked)
        {
            _caption = caption;
            _icon = icon;
            _size = size;
            _right = right;
            _stacked = stacked;
            Arrange();
        }

        private void OnRectTransformDimensionsChange() => Arrange();
        private void OnEnable() => Arrange();

        private void Arrange()
        {
            if (_caption == null) return;
            var bounds = ((RectTransform)transform).rect.size;
            int textHeight = Mathf.Max(23, _caption.fontSize + 4);
            bool show = _icon != null && (_stacked
                ? bounds.x >= _size + 8 && bounds.y >= _size + textHeight + 8
                : bounds.x >= _size + _right + 50 && bounds.y >= _size + 4);
            if (_icon != null) _icon.rectTransform.localScale = show ? Vector3.one : Vector3.zero;
            if (show && _stacked)
            {
                _icon.rectTransform.Anchor(.5f, 1, .5f, 1, -_size * .5f, -6 - _size, -_size * .5f, 6);
                _caption.rectTransform.Anchor(0, 0, 1, 1, 3, 1, 3, _size + 7);
                _caption.alignment = TextAnchor.MiddleCenter;
            }
            else
            {
                if (show) _icon.rectTransform.Anchor(0, .5f, 0, .5f, 7, -_size * .5f, -7 - _size, -_size * .5f);
                _caption.rectTransform.Anchor(0, 0, 1, 1, show ? _size + 12 : 6, 1, _right, 1);
                _caption.alignment = show ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            }
        }
    }
}
