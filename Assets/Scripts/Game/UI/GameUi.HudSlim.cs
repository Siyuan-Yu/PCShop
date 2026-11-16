using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private RectTransform _hudTopRow;
        private LayoutElement _hudShortcutsSize;
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
            var panel = Ui.FlatPanel(parent, "HudTimeControls", Ui.FlatWell, Ui.FlatButtonEdge);
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
            Ui.SetButtonFill(_pauseButton, paused ? Ui.Warn : Ui.ButtonBg);
            _pauseLabel.text = S.ManualActivityClockPaused ? "活动暂停" : S.Time.IsHeld ? "窗口暂停"
                : S.Time.IsUserPaused ? "继续" : S.OpeningClockFrozen ? "教学暂停" : "暂停";
            foreach (var (button, speed) in _speedButtons)
                Ui.SetButtonFill(button, Ui.SelectedButton(S.Time.Speed == speed));
        }

        private void BuildHudShortcuts(Transform parent)
        {
            const float gap = 3;
            var shortcuts = Ui.Size(Ui.Row(parent, 48, gap), 700, -1, 0);
            shortcuts.name = "HudShortcuts";
            _hudShortcutsSize = shortcuts.GetComponent<LayoutElement>();

            var rows = Ui.Size(Ui.Rect(shortcuts, "HudShortcutRows"), 0, -1, 1);
            Ui.VList(rows, gap, 0, expandHeight: true);
            var daily = Ui.Row(rows, 0, gap);
            daily.name = "HudDailyShortcuts";
            var business = Ui.Row(rows, 0, gap);
            business.name = "HudBusinessShortcuts";

            HudPanelButton(daily, "我的", KeyCode.M, OpenMyPanel, "身体状况、住处、饮食与人物关系。", bg: Ui.SelectedButton(true));
            HudPanelButton(daily, "背包", KeyCode.B, ToggleBackpack, "身上带的货和负重；在家或店里一键全放下。");
            HudPanelButton(daily, "手机", KeyCode.P, OpenPhone, "通讯录与智能手机应用。");
            HudPanelButton(daily, "任务", KeyCode.T, OpenQuests, "当前任务与待办；左上角的任务条收起后可从这里再打开。");
            HudPanelButton(daily, "日程", KeyCode.C, OpenSchedulePanel, "按天查看已经安排的事项。").name = "HudCalendar";
            _noticeButtonText = HudPanelButton(daily, "通知", NoticeHotkey, OpenNoticeHistory, "查看本次会话的通知记录。").GetComponentInChildren<Text>();

            HudPanelButton(business, "员工", KeyCode.U, OpenEmployeesPanel, "各门店的员工、岗位与排班。").name = "HudEmployees";
            HudPanelButton(business, "资产", KeyCode.J, OpenBusinessPanel, "门店收支、物业与车辆。").name = "HudAssets";
            HudPanelButton(business, "采购", KeyCode.G, OpenCityProcurement, "商铺采购、补货目标、在途订单与直供协议。").name = "HudProcurement";
            HudPanelButton(business, "物价", KeyCode.H, OpenMarketPanel, "查看本期物价、历史涨跌和各品类报价走势。").name = "HudPrices";
            _brandButton = HudPanelButton(business, "品牌", KeyCode.L, OpenBrandOverview, "品牌、展会、代言人与直播。");
            _brandButton.name = "HudBrand";
            if (!GameSession.FirstChapterTrial)
                HudPanelButton(business, "赛车", KeyCode.R, OpenRacingDestinations, "赛场开放条件、入门路线与比赛安排。").name = "HudRacing";

            Ui.Size(HudPanelButton(shortcuts, "菜单", KeyCode.Escape, OpenSystemPanel, "存档、读档、重新开始和音效设置。"), 54, -1, 0).name = "HudMenu";
        }

        private void FitHudTopRow()
        {
            float availableWidth = ((RectTransform)_hudTopRow.parent).rect.width;
            if (availableWidth <= 0 || Mathf.Approximately(availableWidth, _fittedHudWidth)) return;
            _fittedHudWidth = availableWidth;
            _hudShortcutsSize.preferredWidth = Mathf.Max(700, availableWidth * .364f);

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
