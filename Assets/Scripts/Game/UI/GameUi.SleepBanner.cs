using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private const int FaintMinute = 4 * 60;
        private const int SleepWarnMinute = 23 * 60;

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
            int m = S.Clock.Now.MinuteOfDay;
            bool late = m >= SleepWarnMinute || m < FaintMinute;
            bool show = late && !S.IsGameOver && !_ctrl.Placing;
            _sleepBanner.gameObject.SetActive(show);
            if (!show) return;
            int left = (FaintMinute - m + 24 * 60) % (24 * 60);
            float urgency = 1f - left / 300f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(3f, 9f, urgency));
            string where = _ctrl.InHome ? "走到床边按 F 睡觉" : "快回家睡觉（底栏“回家”）";
            _sleepText.text = $"<b>太晚了，该睡觉了！</b>\n<size=22>再不睡 {left / 60} 小时 {left % 60} 分钟后会晕倒 · {where}</size>";
            _sleepText.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f), pulse);
            _sleepBanner.color = Color.Lerp(new Color(0.3f, 0.06f, 0.05f, 0.7f), new Color(0.75f, 0.1f, 0.08f, 0.9f), pulse * (0.4f + 0.6f * urgency));
        }
    }
}
