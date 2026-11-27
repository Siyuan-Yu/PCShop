using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Canvas _timeAdvanceCanvas;
        private Text _timeAdvanceText;
        private int _timeAdvancePercent = -1;

        internal void ShowTimeAdvance()
        {
            HoldRepeat.CancelActiveHold();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            if (_timeAdvanceCanvas == null)
            {
                _timeAdvanceCanvas = Ui.CreateCanvas("UI_TimeAdvance", 1000);
                var shield = Ui.Panel(_timeAdvanceCanvas.transform, "InputShield", new Color(0, 0, 0, .6f));
                shield.rectTransform.Fill();
                shield.raycastTarget = true;
                var card = Ui.Panel(shield.transform, "Progress", Ui.PanelBg2);
                card.rectTransform.Anchor(.5f, .5f, .5f, .5f, -240, -48, -240, -48);
                _timeAdvanceText = Ui.Label(card.transform, "", 24, TextAnchor.MiddleCenter);
                _timeAdvanceText.rectTransform.Fill(12);
                _timeAdvanceText.raycastTarget = false;
            }
            _timeAdvanceCanvas.gameObject.SetActive(true);
            _timeAdvancePercent = -1;
            RefreshTimeAdvance();
        }

        internal void HideTimeAdvance()
        {
            if (_timeAdvanceCanvas != null) _timeAdvanceCanvas.gameObject.SetActive(false);
        }

        internal void RefreshTimeAdvance()
        {
            if (!S.IsTimeAdvancing) { HideTimeAdvance(); return; }
            if (_timeAdvanceText == null) return;
            if (S.TimeAdvanceError != null) { _timeAdvanceText.text = S.TimeAdvanceError; return; }
            int percent = (int)(S.TimeAdvanceProgress * 100);
            if (percent == _timeAdvancePercent) return;
            _timeAdvancePercent = percent;
            _timeAdvanceText.text = S.TimeAdvanceLabel + " · " + percent + "%";
        }

        private Image _sleepBanner;
        private Text _sleepText;
        private Image _hungerBanner;
        private Text _hungerText;
        private const int HungerWarningThreshold = 25;
        private const int HungerCriticalThreshold = 10;
        private const string HungerWarningInk = "#FFD287";
        private const string HungerCriticalInk = "#FFB0A8";

        /// <summary>深夜提醒：屏幕正中间一条闪烁的横幅，不挡鼠标、不暂停时间。</summary>
        private void BuildSleepBanner(Transform root)
        {
            _sleepBanner = Ui.Panel(root, "SleepBanner", Ui.WarningSurface);
            _sleepBanner.rectTransform.Anchor(0.5f, 0.5f, 0.5f, 0.5f, -380, 70, -380, -170);
            _sleepBanner.raycastTarget = false;
            _sleepText = Ui.Label(_sleepBanner.transform, "", 30, TextAnchor.MiddleCenter, Ui.Warn);
            _sleepText.rectTransform.Fill(8);
            _sleepText.raycastTarget = false;
            _sleepBanner.gameObject.SetActive(false);
        }

        private void UpdateSleepBanner()
        {
            if (_sleepBanner == null) return;
            bool show = S.EnergyExhausted && S.EnergyAwakeMinutesLeft<=60 && !S.IsGameOver && !_ctrl.Placing;
            _sleepBanner.gameObject.SetActive(show);
            if (!show) return;
            int left = S.EnergyAwakeMinutesLeft;
            string where = S.CanSleepAt(S.CurrentPlace) ? "走到床边按 F 睡觉" : "从底栏快捷前往住宅休息";
            _sleepText.text = $"<b>精力耗尽，请安排休息</b>\n<size=22>剩余清醒时间 {left / 60} 小时 {left % 60} 分钟 · {where}</size>";
        }

        private void BuildHungerBanner(Transform root)
        {
            _hungerBanner = Ui.Panel(root, "HungerBanner", Ui.PendingSurface);
            _hungerBanner.rectTransform.Anchor(.5f, 1, .5f, 1, -246, -213, -246, 139);
            _hungerBanner.raycastTarget = false;
            _hungerText = Ui.Label(_hungerBanner.transform, "", 25, TextAnchor.MiddleCenter);
            _hungerText.rectTransform.Fill(7);
            _hungerBanner.gameObject.SetActive(false);
        }

        private static string HungerHudText(double hunger)
        {
            string status = $"饱腹 {System.Math.Ceiling(hunger):0}";
            if (hunger > HungerWarningThreshold) return status;
            string ink = hunger <= HungerCriticalThreshold ? HungerCriticalInk : HungerWarningInk;
            return $"<b><color={ink}>{status}</color></b>";
        }

        private void UpdateHungerBanner(double hunger)
        {
            bool show = !OpeningMealPreludePending && hunger <= HungerWarningThreshold && !S.IsGameOver && !AnyModalOpen && !_ctrl.Placing;
            _hungerBanner.gameObject.SetActive(show);
            if (!show) return;

            bool critical = hunger <= HungerCriticalThreshold;
            _hungerBanner.color = critical ? Ui.WarningSurface : Ui.PendingSurface;
            string title = hunger <= 0 ? "饱腹耗尽 · 正在损失健康"
                : critical ? "严重饥饿 · 请立即进食" : "饱腹偏低 · 请尽快进食";
            string advice = S.CurrentPlace == S.Home ? "吃背包里的食物，或用家里的电饭锅做饭" : "吃背包里的食物，或到快餐店买外带";
            _hungerText.color = critical ? Ui.Warn : Ui.Accent;
            _hungerText.text = $"<b>{title}</b>\n<size=18>{advice}</size>";
        }
    }
}
