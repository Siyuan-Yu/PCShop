using UnityEngine;
using UnityEngine.UI;

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
            Image panel = Ui.Panel(_tipCanvas.transform, "Toast", new Color(0.07f, 0.11f, 0.1f, 0.97f));
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

        /// <summary>顶栏按钮的快捷键（没开窗口、不在摆放模式时有效）。返回是否处理了。</summary>
        public bool TryHotkey(KeyCode key)
        {
            if (AnyModalOpen || S == null || S.IsGameOver) return false;
            switch (key)
            {
                case KeyCode.M: OpenMyPanel(); return true;
                case KeyCode.C: OpenSchedulePanel(); return true;
                case KeyCode.P: OpenPhone(); return true;
                case KeyCode.J: OpenBusinessPanel(); return true;
                case KeyCode.H: OpenMarketPanel(); return true;
                case KeyCode.T: OpenQuests(); return true;
                case KeyCode.Escape: OpenSystemPanel(); return true;
            }
            return false;
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
            "<b>饥饿</b>：越低越饿，每天 12:00 和 18:30 自动吃饭（先吃背包里的饭盒、家里锅里的饭）；太低会掉健康、昏倒。\n" +
            "<b>精力</b>：干活、走路消耗，回家睡觉恢复；/ 后面是今天的精力上限，缺觉会变低。\n" +
            "<b>健康</b>：饥饿、精力过低或居住环境差会下降，太低要去医院。\n" +
            "<b>知名度</b>：越高，路过的顾客越愿意进店。";

        private string VehicleTip(Core.Session.VehicleSpec v)
        {
            string s = $"<b>{v.Name}</b>\n速度：每游戏分钟 {v.SpeedPerMinute:0} 米　最多载 {v.CargoCapacity:0} 公斤";
            if (v.Illegal) s += "\n<color=#D79780>城里不让上路，会被交警追</color>";
            if (!S.CanUse(v.Kind)) s += v.Purchasable ? $"\n还没有：约 {v.Price}" : "\n暂时用不了";
            return s;
        }
    }
}