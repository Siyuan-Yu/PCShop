using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using PCShopSell.Core.World;

namespace PCShopSell.Game.UI
{
    /// <summary>悬停说明 + 顶栏快捷键。</summary>
    public sealed partial class GameUi
    {
        private Canvas _tipCanvas;
        private RectTransform _tipPanel;
        private Text _tipText;
        private TipTarget _tipFor;
        private float _tipTimer;

        /// <summary>冒烟：假装鼠标在画布的这个位置（画布中心为原点，截图用）。</summary>
        public Vector2? DebugMouse;

        public bool TipVisible => _tipPanel != null && _tipPanel.gameObject.activeSelf;
        public string TipDebug => $"hover={(TipTarget.Hover != null)} for={(_tipFor != null)} timer={_tipTimer:0.00} active={TipVisible} pos={_tipPanel.anchoredPosition} size={_tipPanel.rect.size}";

        private void BuildTip()
        {
            _tipCanvas = Ui.CreateCanvas("UI_Tip", 60);
            Image panel = Ui.Panel(_tipCanvas.transform, "Toast", Ui.PanelBg);
            panel.raycastTarget = false;
            _tipPanel = panel.rectTransform;
            _tipPanel.anchorMin = _tipPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _tipPanel.pivot = new Vector2(0, 1);
            var v = _tipPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 8, 8);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            var fit = _tipPanel.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tipText = Ui.Label(_tipPanel, "", 16, TextAnchor.UpperLeft);
            _tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var le = _tipText.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 330;
            _tipPanel.gameObject.SetActive(false);
        }

        /// <summary>悬停够久就在鼠标旁弹出说明；鼠标一移开就收。</summary>
        private void UpdateTip(float dt)
        {
            TipTarget hover = TipTarget.Hover;
            if (OnsiteInputActive && hover != null && !hover.transform.IsChildOf(_modalCanvas.transform)) hover = null;
            if (hover != _tipFor)
            {
                _tipFor = hover;
                _tipTimer = 0;
                _tipPanel.gameObject.SetActive(false);
            }
            if (hover == null || hover.Text == null) return;
            _tipTimer += dt;
            if (_tipTimer < 0.45f) return;
            string text = hover.Text();
            if (string.IsNullOrEmpty(text))
            {
                _tipPanel.gameObject.SetActive(false);
                return;
            }
            _tipText.text = text;
            _tipPanel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tipPanel);
            var canvasRt = (RectTransform)_tipCanvas.transform;
            Vector2 local;
            if (DebugMouse.HasValue) local = DebugMouse.Value;   // 冒烟：直接给画布坐标
            else RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, Input.mousePosition, _tipCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _tipCanvas.worldCamera, out local);
            Vector2 size = _tipPanel.rect.size;
            Rect area = canvasRt.rect;
            float x = Mathf.Min(local.x + 16, area.xMax - size.x - 8);
            float y = local.y - 22;
            if (y - size.y < area.yMin + 8) y = local.y + size.y + 18;   // 下面放不下就放到鼠标上面
            _tipPanel.anchoredPosition = new Vector2(Mathf.Max(area.xMin + 8, x), y);
        }

        private readonly Dictionary<KeyCode, Button> _hudPanelButtons = new Dictionary<KeyCode, Button>();

        private static string HotkeyLabel(KeyCode key) => key == KeyCode.Escape ? "Esc" : key.ToString();

        private Button HudPanelButton(Transform row, string title, KeyCode key, Action open, string description, Color? bg = null)
        {
            string shortcut = HotkeyLabel(key);
            var button = Ui.Size(Ui.Button(row, $"{title} {shortcut}", open, 13, bg), 0, -1, 1)
                .Tip($"<b>{title}</b>　快捷键：{shortcut}\n{description}");
            Ui.HudNavigationArt(button, title, key == KeyCode.Escape);
            _hudPanelButtons.Add(key, button);
            return button;
        }

        internal void HandleHudPanelKeys()
        {
            // Esc 由控制器优先处理，用于关闭最上层窗口。
            foreach (KeyCode key in _hudPanelButtons.Keys)
                if (key != KeyCode.Escape && Input.GetKeyDown(key) && TryHotkey(key)) return;
        }

        /// <summary>顶栏快捷键。背包键也可关闭已打开的背包。</summary>
        public bool TryHotkey(KeyCode key)
        {
            if (S == null || S.IsGameOver || S.IsWorking) return false;
            if (AnyModalOpen && (key != KeyCode.B || _backpackWin == null || _backpackWin.Closed)) return false;
            _hudPanelButtons.TryGetValue(key, out var button);
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()) return false;
            if (S.OpeningAgencyGuidePending && key != KeyCode.Escape) return true;
            if (OpeningMealRestricted && key != KeyCode.Escape)
            { RemindOpeningMeal(); return true; }
            button.onClick.Invoke();
            return true;
        }

        /// <summary>危险操作的二次确认：说清后果，确认按钮写动作本身（不是“确定”）。</summary>
        private void ConfirmAction(string title, string text, string confirmLabel, System.Action action)
        {
            OpenWindow(title, 560, 300, w =>
            {
                Para(w.Content, text, 18);
                RectTransform row = Ui.Row(w.Content, 46, 8);
                Ui.Size(Ui.Button(row, "取消", w.Close, 18), -1, -1, 1);
                Ui.Size(Ui.Button(row, confirmLabel, () => { CloseAllWindows(); action(); }, 18, Ui.Warn), -1, -1, 1);
            });
        }

        private string StatsTip() =>
            "<b>饱腹</b>：越低越饿，每天 12:00 和 18:30 会尝试自动吃饭。饱腹降到 0 时会持续损失健康。\n" +
            "<b>精力</b>：干活、走路消耗，回家睡觉恢复；/ 后面是今天的精力上限，缺觉会变低。\n" +
            "<b>健康</b>：饥饿、精力过低或居住环境差会下降，太低要去医院。\n" +
            $"<b>压力</b>：打工和疲劳会累积，睡觉和休闲能缓解。到岗时压力达到 {Core.Session.PlayerStats.HighStressThreshold:0}，当班表现奖金少 {Core.Session.GameSession.HighStressWorkPenalty}%。\n" +
            "<b>名望</b>：你本人在行业里的名气，所有店共用。租大铺、开分店、直供和企业订单会用到名望。按时交付装机、优质代养结算和卖出真品文物可提高名望。\n" +
            "<b>本店知名度</b>：每家店各自一个，越高路过的顾客越愿意进店（在店铺页里看）。";

        private static string TravelSpeedText(Core.Session.VehicleSpec vehicle)
        {
            double relativeSpeed = vehicle.SpeedPerMinute * CityTravelTiming.PlayerSpeedFactor(vehicle.Kind)
                / (Core.Session.Vehicles.Walk.SpeedPerMinute * CityTravelTiming.PlayerWalkSpeedFactor);
            return $"通勤速度 · 步行的 {relativeSpeed:0.##} 倍";
        }

        private string VehicleTip(Core.Session.VehicleSpec v)
        {
            string s = $"<b>{v.Name}</b>{(S.Vehicle == v.Kind ? "（当前使用）" : "")}\n{TravelSpeedText(v)}　最多载 {v.CargoCapacity * Core.Home.Backpack.KgPerSpace:0} 千克";
            if (v.Illegal) s += "\n<color=#9C4634>城里不让上路，会被交警追</color>";
            if (v.Purchasable) s += "\n" + S.VehicleUsage(v.Kind);
            if (!S.CanUse(v.Kind)) s += v.Purchasable ? S.Owns(v.Kind) ? "\n当前没有空闲车辆，可在员工管理查看使用人。" : $"\n去车行购买：{v.Price}/台" : "\n暂时用不了";
            return s;
        }
    }
}
