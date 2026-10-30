using System;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Image _questPanel;
        private Text _questText;
        private string _questSeen;
        private bool _questInitialized;
        private Window _questWindow;
        private Window _questPopup;
        private bool _questHidden;
        private bool _popupPending;
        private QuestDef _pendingDone, _pendingNext;

        /// <summary>任务完成 / 换任务时强制弹窗（冒烟测试脚本会关掉，免得挡住脚本流程）。</summary>
        public bool QuestPopups = true;

        public void ResetGuideState()
        {
            _questSeen = null;
            _questInitialized = false;
            _questWindow = null;
            _questPopup = null;
            _questHidden = false;
            _popupPending = false;
            _pendingDone = _pendingNext = null;
            _businessTab = 0;
        }

        private void BuildQuestTracker(Transform root)
        {
            _questPanel = Ui.Panel(root, "QuestTracker", Ui.PanelBg);
            _questPanel.rectTransform.Anchor(0, 1, 0, 1, 16, -248, -456, 82);
            var btn = _questPanel.gameObject.AddComponent<Button>();
            btn.targetGraphic = _questPanel;
            btn.onClick.AddListener(OpenQuestWindow);
            _questText = Ui.Label(_questPanel.transform, "", 16, TextAnchor.UpperLeft);
            _questText.rectTransform.Anchor(0, 0, 1, 1, 14, 54, 44, 12);
            _questText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _questText.verticalOverflow = VerticalWrapMode.Truncate;
            var help = Ui.Button(_questPanel.transform, "怎么做", OpenQuestWindow, 16);
            ((RectTransform)help.transform).Anchor(0, 0, 1, 0, 12, 10, 12, -46);
            var close = Ui.Button(_questPanel.transform, "×", () =>
            {
                _questHidden = true;
                UpdateQuestTracker();
                Toast("任务条收起来了，点顶栏“任务”（或按 T）随时再打开。");
            }, 20);
            ((RectTransform)close.transform).Anchor(1, 1, 1, 1, -38, -36, 6, 6);
            close.Tip("收起任务条（顶栏“任务”按钮可重新打开）");
            _questPanel.gameObject.SetActive(false);
        }

        /// <summary>顶栏“任务”按钮：打开当前任务说明，并重新显示左上角的任务条。</summary>
        public void OpenQuests()
        {
            _questHidden = false;
            UpdateQuestTracker();
            if (_questWindow != null && !_questWindow.Closed) { _questWindow.Rebuild(); return; }
            _questWindow = OpenWindow("当前任务", 700, 520, BuildGuideWindow, onClosed: () => _questWindow = null);
        }

        /// <summary>冒烟截图用：弹出“完成了租铺任务、接下来去家具城”的任务完成窗口。</summary>
        public void DebugQuestPopup() => OpenQuestPopup(Quests.Get("rent"), Quests.Get("mall"));

        public string QuestTrackerText => _questPanel != null && _questPanel.gameObject.activeSelf ? _questText.text : null;
        public void DebugRefreshQuests() => UpdateQuestTracker();

        private void UpdateQuestTracker()
        {
            if (_questPanel == null) return;
            QuestDef q = S.CurrentQuest();
            if (_questInitialized && _questSeen != q?.Id && !S.IsGameOver)
            {
                QuestDef done = _questSeen != null && S.QuestCompleted(_questSeen) ? Quests.Get(_questSeen) : null;
                if (QuestPopups && (done != null || q != null))
                {
                    _pendingDone = _pendingDone ?? done;
                    _pendingNext = q;
                    _popupPending = true;
                }
                else if (done != null) Toast($"<color=#A9C9A9>完成：{done.Title}</color>" + (q != null ? $"\n接下来：{q.Title}" : "\n继续经营小店吧。"));
                else if (q != null) Toast("新任务：" + q.Title);
            }
            // Wait until result popups (e.g. "签约成功") are dismissed, so they read first.
            if (_popupPending && !PopupOpen && (_questPopup == null || _questPopup.Closed) && !S.IsGameOver)
            {
                OpenQuestPopup(_pendingDone, _pendingNext);
                _popupPending = false;
                _pendingDone = _pendingNext = null;
            }
            _questInitialized = true;
            _questSeen = q?.Id;
            _questPanel.gameObject.SetActive(q != null && !_questHidden && !S.IsGameOver && !_ctrl.Placing);
            string progress = q == null ? null : ProgressOf(q);
            _questText.text = q == null ? "" : $"<b><color=#DDC18B>{q.Title}</color></b>\n{q.Short}" + (progress != null ? $"\n<size=14><color=#ABB5A8>{progress.Split('\n')[0]}</color></size>" : "");
        }

        private string ProgressOf(QuestDef q)
        {
            string text = q.Progress?.Invoke(S);
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private void ProgressLine(Transform parent, QuestDef q, int size)
        {
            string text = ProgressOf(q);
            if (text != null) Para(parent, text, size, Ui.Dim);
        }

        public void OpenQuestWindow()
        {
            UpdateQuestTracker();
            if (S.CurrentQuest() == null) return;
            if (_questWindow != null && !_questWindow.Closed) { _questWindow.Rebuild(); return; }
            _questWindow = OpenWindow("当前任务", 700, 520, BuildGuideWindow, onClosed: () => _questWindow = null);
        }

        /// <summary>任务变化时强制弹出：先单独弹“任务完成”（完成了什么、拿到什么），关掉后再弹“新任务”。</summary>
        private void OpenQuestPopup(QuestDef done, QuestDef next)
        {
            if (_questPopup != null && !_questPopup.Closed) _questPopup.Close();
            if (done == null)
            {
                if (next != null) OpenNewQuestPopup(next);
                return;
            }
            _questPopup = OpenWindow("任务完成", 560, 240, w =>
            {
                Line(w.Content, "✓ " + done.Title, 23, Ui.Good);
                Para(w.Content, done.Reward != null ? "获得：" + done.Reward : "继续加油。", 19, Ui.Accent);
                if (next == null) Para(w.Content, "暂时没有新任务。", 16, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "知道了", w.Close, 18, Ui.ButtonHot), -1, 42);
            }, onClosed: () =>
            {
                _questPopup = null;
                if (next != null && S.CurrentQuest()?.Id == next.Id) OpenNewQuestPopup(next);
            });
        }

        private void OpenNewQuestPopup(QuestDef next)
        {
            _questPopup = OpenWindow("新任务", 560, 280, w =>
            {
                Line(w.Content, next.Title, 23, Ui.Accent);
                Para(w.Content, GuideLocation(next.Target), 16, Ui.Accent);
                Para(w.Content, next.Hint, 18);
                if (next.Reward != null) Para(w.Content, "完成奖励：" + next.Reward, 16, Ui.Good);
                ProgressLine(w.Content, next, 15);
                Ui.Size(Ui.Button(w.Content, "知道了", w.Close, 18, Ui.ButtonHot), -1, 42);
            }, onClosed: () => _questPopup = null);
        }

        private void BuildGuideWindow(Window w)
        {
            var q = S.CurrentQuest();
            if (q == null)
            {
                int n = Quests.All.Count(x => S.QuestCompleted(x.Id));
                Para(w.Content, $"暂时没有新任务（已完成 {n} 个）。继续经营，达到下一阶段的条件时会弹出新任务。", 20);
                Ui.Size(Ui.Button(w.Content, "继续", w.Close, 18, Ui.ButtonHot), -1, 42);
                return;
            }
            Line(w.Content, q.Title, 23, Ui.Accent);
            // Guidance explains where/how; it must never move the player or open a destination page.
            Para(w.Content, GuideLocation(q.Target), 17, Ui.Accent);
            Para(w.Content, q.Hint, 18);
            if (q.Reward != null) Para(w.Content, "完成奖励：" + q.Reward, 16, Ui.Good);
            ProgressLine(w.Content, q, 16);
            Ui.Size(Ui.Button(w.Content, "知道了", w.Close, 18, Ui.ButtonHot), -1, 42);
        }

        private static void ResetGuideScroll(Window w)
        {
            Canvas.ForceUpdateCanvases();
            w.Content.parent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
        }

        private void GuideHint(Transform parent, QuestTarget target, Action refresh)
        {
            var q = S.CurrentQuest();
            if (q == null || q.Target != target) return;
            S.VisitQuestTarget(target);
            var card = Ui.Panel(parent, "GuideHint", Ui.PanelBg2).rectTransform;
            Ui.VList(card, 6, 12);
            Para(card, "<color=#DDC18B><b>当前任务 · " + q.Title + "</b></color>\n" + (q.Lesson ? q.Hint : q.Short), 16);
            ProgressLine(card, q, 14);
            var row = Ui.Row(card, 34, 6);
            Ui.Size(Ui.Button(row, "怎么做", OpenQuestWindow, 15), -1, -1, 1);
            if (q.Lesson)
                Ui.Size(Ui.Button(row, "已了解，完成这一步", () => { S.CompleteQuestLesson(q.Id); UpdateQuestTracker(); refresh(); }, 15, Ui.ButtonHot, q.LessonReady == null || q.LessonReady(S)), -1, -1, 1);
        }

        private static QuestTarget TargetFor(ShopTab tab)
        {
            switch (tab)
            {
                case ShopTab.Stock: return QuestTarget.Stock;
                case ShopTab.Pricing: return QuestTarget.Pricing;
                case ShopTab.Orders: return QuestTarget.Orders;
                case ShopTab.Renovate: return QuestTarget.Renovate;
                case ShopTab.Upgrades: return QuestTarget.Upgrades;
                case ShopTab.Staff: return QuestTarget.Staff;
                default: return QuestTarget.Counter;
            }
        }
        private static readonly QuestTarget[] BusinessTargets = { QuestTarget.Finance, QuestTarget.Direct, QuestTarget.Enterprise, QuestTarget.Ventures, QuestTarget.Creators, QuestTarget.Property, QuestTarget.Park };
        private static string GuideLocation(QuestTarget target)
        {
            switch (target)
            {
                case QuestTarget.Agency: return "地点：商业街 · <color=#FFC966>安居房产中介</color>";
                case QuestTarget.Renovate: return "位置：自己的店 → 右侧“装修”页";
                case QuestTarget.Stock: return "位置：自己的店 · 货架";
                case QuestTarget.Market: return "地点：<color=#FFC966>电子市场</color> · 配件档口";
                case QuestTarget.Counter: return "位置：自己的店 · 收银台";
                case QuestTarget.Staff: return "位置：自己的店 → 右侧“招人”页";
                case QuestTarget.PhoneShop: return "地点：商业街 · <color=#FFC966>手机店</color>";
                case QuestTarget.Mall: return "地点：商业街 · <color=#FFC966>宜居家具城</color>";
                case QuestTarget.Upgrades: return "位置：自己的店 → 右侧“升级”页";
                case QuestTarget.Pricing: return "位置：自己的店 → 右侧“定价”页";
                case QuestTarget.Orders: return "位置：自己的店 → 右侧“订单”页";
                case QuestTarget.Restock: return "入口：手机 → 1788";
                case QuestTarget.Finance: return "入口：经营 → “资产与负债”。";
                case QuestTarget.Enterprise: return "入口：经营 → “企业合同”。";
                case QuestTarget.Direct: return "入口：经营 → “直供采购”。";
                case QuestTarget.Ventures: return "地点：安居房产中介";
                case QuestTarget.Park: return "入口：经营 → 园区布局";
                case QuestTarget.Creators: return "入口：经营 → “推广合作”。";
                case QuestTarget.Property: return "入口：经营 → “物业与住处”。";
                default: return "";
            }
        }
    }
}
