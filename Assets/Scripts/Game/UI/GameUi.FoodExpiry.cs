using System;
using PCShopSell.Core.Home;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed class FoodExpiryBadge : MonoBehaviour
    {
        public Func<long> Now;
        public int? ExpireDay;
        public Text Label;
        private long _lastMinute = long.MinValue;
        private void Update() => Refresh();
        public static string Countdown(int? day, long now)
        {
            if (!day.HasValue) return "无期限";
            long left = Math.Max(0, FoodBatch.ExpiryMinute(day.Value) - now);
            if (left == 0) return "已到期";
            if (left >= 1440) return "剩" + ((left + 1439) / 1440) + "天";
            if (left >= 60) return "剩" + ((left + 59) / 60) + "小时";
            return "剩" + left + "分";
        }
        public void Refresh()
        {
            if (Now == null || Label == null) return;
            long now = Now(); if (_lastMinute == now) return; _lastMinute = now;
            Label.text = Countdown(ExpireDay, now);
            long left = ExpireDay.HasValue ? FoodBatch.ExpiryMinute(ExpireDay.Value) - now : long.MaxValue;
            Label.color = left <= 360 ? new Color(1, .65f, .53f) : left <= 1440 ? new Color(1, .85f, .38f) : new Color(.75f, .95f, .77f);
        }
    }

    public sealed partial class GameUi
    {
        private void AddFoodExpiry(Button cell, string key, int? day)
        {
            if(key.StartsWith("stockgift:"))key="gift:"+key.Substring(10);
            bool food = key == "rice" || key.StartsWith("ing:") || key.StartsWith("lunch:")
                || key.StartsWith("gift:") && !Gifts.IsCigarette(key.Substring(5));
            if (!food) return;
            var title = cell.GetComponentInChildren<Text>();
            title.rectTransform.Anchor(0, .23f, 1, 1, 3, 0, 3, 0);
            var panel = Ui.Panel(cell.transform, "ExpiryBadge_" + key, new Color(.07f, .12f, .1f, .88f));
            panel.rectTransform.Anchor(.05f, .01f, .98f, .23f);
            var label = Ui.Label(panel.transform, "", 11, TextAnchor.MiddleRight); label.rectTransform.Fill(2);
            var badge = panel.gameObject.AddComponent<FoodExpiryBadge>();
            badge.Now = () => S.Clock.Now.TotalMinutes; badge.ExpireDay = day; badge.Label = label; badge.Refresh();
            panel.rectTransform.Tip(day.HasValue ? "第" + (day.Value + 1) + "天结束时到期。同种食物优先使用最早到期的批次。" : "该物品在当前游戏规则中没有到期限制。");
        }
    }
}
