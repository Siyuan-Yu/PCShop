using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private const KeyCode QuestHotkey = KeyCode.R;

        private RectTransform _hudTopRow;
        private RectTransform _hudFunctionBar;
        private Button _hudFoldButton;
        private bool _hudFunctionsCollapsed;
        private float HudFunctionOffset => _hudFunctionsCollapsed ? 0 : 54;
        private float _appliedHudFunctionOffset = -1;
        private RectTransform _appliedShopDock, _appliedHomeDock;
        private Button _brandButton;
        private float _fittedHudWidth;
        private Button _pauseButton;
        private Text _pauseLabel;
        private readonly List<(Button button, int speed)> _speedButtons = new List<(Button, int)>();

        private void BuildHudTimeControls(Transform parent)
        {
            const int padding = 3;
            const int gap = 2;
            const int pauseWidth = 72;
            const int speedWidth = 40;
            var panel = Ui.FlatPanel(parent, "HudTimeControls", Ui.ChromeWell, Ui.ChromeEdge);
            Ui.Size(panel, padding * 2 + pauseWidth + TimeController.Speeds.Length * (speedWidth + gap));
            var controls = Ui.Row(panel.transform, 42, gap).Fill();
            controls.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(padding, padding, 5, 5);

            _pauseButton = Ui.Size(Ui.Button(controls, "暂停", () => S.Time.TogglePause(), 16), pauseWidth);
            _pauseButton.name = "HudPauseTime";
            _pauseLabel = _pauseButton.GetComponentInChildren<Text>();
            _pauseLabel.rectTransform.Anchor(0, 0, 1, 1, padding, 0, padding, 0);
            _pauseLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _pauseButton.Tip(() => S.Time.IsHeld
                ? "窗口打开或装修时自动暂停，结束后恢复。"
                : S.Time.IsUserPaused ? "继续 · 快捷键 空格" : "暂停 · 快捷键 空格");
            for (int i = 0; i < TimeController.Speeds.Length; i++)
            {
                int speed = TimeController.Speeds[i];
                string label = TimeController.SpeedLabel(speed);
                var button = Ui.Size(Ui.Button(controls, label, () => S.Time.SetSpeed(speed), 16), speedWidth);
                button.name = "HudSpeed_" + speed;
                var caption = button.GetComponentInChildren<Text>();
                caption.rectTransform.Anchor(0, 0, 1, 1, padding, 0, padding, 0);
                caption.horizontalOverflow = HorizontalWrapMode.Overflow;
                button.Tip($"{label} 游戏速度 · 快捷键 {i + 1}");
                _speedButtons.Add((button, speed));
            }
        }

        private void RefreshHudTimeControls()
        {
            bool paused = !S.Time.IsRunning || S.OpeningClockFrozen || S.ManualActivityClockPaused;
            Ui.SetButtonFill(_pauseButton, paused ? Ui.FlatRow : Ui.ButtonBg);
            _pauseLabel.text = S.ManualActivityClockPaused ? "活动暂停" : S.Time.IsHeld ? "窗口暂停"
                : S.Time.IsUserPaused ? "继续" : S.OpeningClockFrozen ? "教学暂停" : "暂停";
            foreach (var (button, speed) in _speedButtons)
                Ui.SetButtonFill(button, S.Time.Speed == speed ? Ui.ButtonHot : Ui.ButtonBg);
        }

        private void BuildHudSystemControls(Transform parent)
        {
            _hudFunctionsCollapsed = PlayerPrefs.GetInt("petshop:hud-functions-collapsed", 0) != 0;
            _hudFoldButton = Ui.Size(Ui.Button(parent, "收起功能栏 ▴", ToggleHudFunctions, 16), 142);
            _hudFoldButton.name = "HudFoldFunctions";
            _hudFoldButton.Tip("Tab · 收起或展开功能栏");
            _noticeButtonText = Ui.Size(HudPanelButton(parent, "通知", NoticeHotkey, OpenNoticeHistory,
                "查看本次会话的通知记录。"), 94, -1, 0).GetComponentInChildren<Text>();
            _noticeButtonText.transform.parent.name = "HudNotifications";
            Ui.Size(HudPanelButton(parent, "菜单", KeyCode.Escape, OpenSystemPanel,
                "存档、读档、重新开始和音效设置。"), 76, -1, 0).name = "HudMenu";
        }

        private void BuildHudShortcuts(Transform parent)
        {
            _hudFunctionBar = Ui.Panel(parent, "HudFunctionBar", Ui.Chrome).rectTransform
                .Anchor(0, 1, 1, 1, 12, -126, 12, 76);
            var row = Ui.Row(_hudFunctionBar, 46, 8).Fill();
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 10, 5, 5);
            HudPanelButton(row, "我的", KeyCode.M, OpenMyPanel, "个人资料、饮食、住处、我的资产与人物关系。");
            HudPanelButton(row, "背包", KeyCode.B, ToggleBackpack, "身上带的物品与负重。");
            HudPanelButton(row, "手机", KeyCode.P, OpenPhone, "通讯录与智能手机应用。");
            HudPanelButton(row, "任务", QuestHotkey, OpenQuests, "当前任务与待办。");
            HudPanelButton(row, "日程", KeyCode.C, OpenSchedulePanel, "按天查看已经安排的事项。").name = "HudCalendar";
            HudPanelButton(row, "经营", KeyCode.H, OpenEmployeesPanel, "各门店的员工、岗位、排班、采购与补货。").name = "HudManagement";
            HudPanelButton(row, "物价", KeyCode.I, OpenMarketPanel, "查看本期物价、历史涨跌和报价走势。").name = "HudPrices";
            _brandButton = HudPanelButton(row, "品牌", KeyCode.L, OpenBrandOverview, "品牌、展会、代言人与直播。");
            _brandButton.name = "HudBrand";
            if (!GameSession.FirstChapterTrial)
                HudPanelButton(row, "赛车", KeyCode.T, OpenRacingDestinations, "赛场开放条件与比赛安排。").name = "HudRacing";
            _hudFunctionBar.gameObject.SetActive(!_hudFunctionsCollapsed);
        }

        private void ToggleHudFunctions()
        {
            _hudFunctionsCollapsed = !_hudFunctionsCollapsed;
            PlayerPrefs.SetInt("petshop:hud-functions-collapsed", _hudFunctionsCollapsed ? 1 : 0);
            ApplyHudFunctionLayout();
        }

        private void ApplyHudFunctionLayout()
        {
            if (_appliedHudFunctionOffset == HudFunctionOffset && _appliedShopDock == _dock && _appliedHomeDock == _homeDock)
                return;
            _appliedHudFunctionOffset = HudFunctionOffset;
            _appliedShopDock = _dock;
            _appliedHomeDock = _homeDock;
            _hudFunctionBar.gameObject.SetActive(!_hudFunctionsCollapsed);
            _hudFoldButton.GetComponentInChildren<Text>().text = _hudFunctionsCollapsed ? "展开功能栏 ▾" : "收起功能栏 ▴";
            float shift = HudFunctionOffset;
            if (_questPanel != null)
            {
                var rect = _questPanel.rectTransform;
                rect.offsetMax = new Vector2(rect.offsetMax.x, -82 - shift);
                rect.offsetMin = new Vector2(16, -(82 + shift - _questText.rectTransform.offsetMax.y + 118));
            }
            foreach (var button in new[] { _shopDockToggle, _homeDockToggle })
                if (button != null)
                {
                    var rect = (RectTransform)button.transform;
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -93 - shift);
                }
            foreach (var dock in new[] { _dock, _homeDock })
                if (dock != null) dock.offsetMax = new Vector2(dock.offsetMax.x, -120 - shift);
            if (_storageWarning != null)
            {
                var rect = (RectTransform)_storageWarning.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -90 - shift);
            }
            if (_chaseText != null)
            {
                var rect = (RectTransform)_chaseText.transform.parent;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -102.5f - shift);
            }
            if (_hungerBanner != null)
            {
                var rect = _hungerBanner.rectTransform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -176 - shift);
            }
            RefreshToastPlacement();
        }

        private void FitHudTopRow()
        {
            float availableWidth = ((RectTransform)_hudTopRow.parent).rect.width;
            if (availableWidth <= 0 || Mathf.Approximately(availableWidth, _fittedHudWidth)) return;
            _fittedHudWidth = availableWidth;

            var layout = _hudTopRow.GetComponent<HorizontalLayoutGroup>();
            float preferredWidth = layout.padding.horizontal;
            int visible = 0;
            foreach (RectTransform child in _hudTopRow)
            {
                if (!child.gameObject.activeSelf) continue;
                preferredWidth += LayoutUtility.GetPreferredWidth(child);
                visible++;
            }
            preferredWidth += Mathf.Max(0, visible - 1) * layout.spacing;
            float rowWidth = Mathf.Max(availableWidth, preferredWidth);
            _hudTopRow.sizeDelta = new Vector2(rowWidth - availableWidth, 0);
            _hudTopRow.localScale = Vector3.one * (availableWidth / rowWidth);
        }

    }
}
