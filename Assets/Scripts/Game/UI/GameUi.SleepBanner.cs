using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {

        private Image _sleepBanner;
        private Text _sleepText;

        /// <summary>深夜提醒：屏幕正中间一条闪烁的横幅，不挡鼠标、不暂停时间。</summary>
        private void BuildSleepBanner(Transform root)
        {
            _sleepBanner = Ui.Panel(root, "SleepBanner", new Color(0.35f, 0.05f, 0.05f, 0.82f));
            _sleepBanner.rectTransform.Anchor(0.5f, 0.5f, 0.5f, 0.5f, -380, 70, -380, -170);
            _sleepBanner.raycastTarget = false;
            _sleepText = Ui.Label(_sleepBanner.transform, "", 30, TextAnchor.MiddleCenter, Color.white);
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
            string where = _ctrl.InHome ? "走到床边按 F 睡觉" : "快回家睡觉（底栏“回家”）";
            _sleepText.text = $"<b>精力耗尽，请安排休息</b>\n<size=22>剩余清醒时间 {left / 60} 小时 {left % 60} 分钟 · {where}</size>";
            _sleepText.color = new Color(1f, 0.85f, 0.3f);
        }
    }
}
