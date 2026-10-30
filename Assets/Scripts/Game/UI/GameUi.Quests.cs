using System;
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

        public void ResetGuideState()
        {
            _questSeen = null;
            _questInitialized = false;
            _questWindow = null;
            _businessTab = 0;
            _showAllBusinessTabs = false;
        }

        private void BuildQuestTracker(Transform root)
        {
            _questPanel = Ui.Panel(root, "QuestTracker", Ui.PanelBg);
            _questPanel.rectTransform.Anchor(0, 1, 0, 1, 16, -248, -456, 82);
            var btn = _questPanel.gameObject.AddComponent<Button>();
            btn.targetGraphic = _questPanel;
            btn.onClick.AddListener(OpenQuestWindow);
            _questText = Ui.Label(_questPanel.transform, "", 16, TextAnchor.UpperLeft);
            _questText.rectTransform.Anchor(0, 0, 1, 1, 14, 54, 14, 12);
            _questText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _questText.verticalOverflow = VerticalWrapMode.Truncate;
            var help = Ui.Button(_questPanel.transform, "怎么做", OpenQuestWindow, 16);
            ((RectTransform)help.transform).Anchor(0, 0, 1, 0, 12, 10, 12, -46);
            _questPanel.gameObject.SetActive(false);
        }

        public string QuestTrackerText => _questPanel != null && _questPanel.gameObject.activeSelf ? _questText.text : null;
        public void DebugRefreshQuests() => UpdateQuestTracker();

        private void UpdateQuestTracker()
        {
            if (_questPanel == null) return;
            QuestDef q = S.CurrentQuest();
            if (_questInitialized && _questSeen != q?.Id && !S.IsGameOver)
            {
                if (_questSeen != null && S.QuestCompleted(_questSeen))
                    Toast($"<color=#A9C9A9>完成：{Quests.Get(_questSeen).Title}</color>" + (q != null ? $"\n接下来：{q.Title}" : "\n继续经营小店吧。"));
                else if (q != null)
                    Toast("新任务：" + q.Title);
            }
            _questInitialized = true;
            _questSeen = q?.Id;
            _questPanel.gameObject.SetActive(q != null && !S.IsGameOver && !_ctrl.Placing);
            _questText.text = q == null ? "" : $"<b><color=#DDC18B>{q.Title}</color></b>\n{q.Short}\n<size=14><color=#ABB5A8>{q.Progress(S).Split('\n')[0]}</color></size>";
        }

        public void OpenQuestWindow()
        {
            UpdateQuestTracker();
            if (S.CurrentQuest() == null) return;
            if (_questWindow != null && !_questWindow.Closed) { _questWindow.Rebuild(); return; }
            _questWindow = OpenWindow("当前任务", 700, 480, BuildGuideWindow, onClosed: () => _questWindow = null);
        }

        private void BuildGuideWindow(Window w)
        {
            var q = S.CurrentQuest();
            if (q == null)
            {
                Para(w.Content, "这一步已经完成，继续经营吧。", 20);
                Ui.Size(Ui.Button(w.Content, "继续", w.Close, 18, Ui.ButtonHot), -1, 42);
                return;
            }
            Line(w.Content, q.Title, 23, Ui.Accent);
            // Guidance explains where/how; it must never move the player or open a destination page.
            Para(w.Content, GuideLocation(q.Target), 17, Ui.Accent);
            Para(w.Content, q.Hint, 18);
            Para(w.Content, q.Progress(S), 16, Ui.Dim);
            if (q.Target == QuestTarget.Enterprise)
                Para(w.Content, "已有合同：回到主店，在右侧“订单”页查看要装的电脑。", 15, Ui.Dim);
            if (q.Target == QuestTarget.Direct)
                Para(w.Content, "与老周来往：顶栏“菜单” → “目标与人脉”。", 15, Ui.Dim);
            if (S.HasShop && NeedsTimeHint(q.Target))
                Para(w.Content, "等顾客、招聘或到货时，关闭弹窗；在店内可手动切到“柜台”页，让时间继续。", 15, Ui.Dim);
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
            Para(card, q.Progress(S), 14, Ui.Dim);
            var row = Ui.Row(card, 34, 6);
            Ui.Size(Ui.Button(row, "怎么做", OpenQuestWindow, 15), -1, -1, 1);
            if (q.Lesson)
                Ui.Size(Ui.Button(row, "已了解，完成这一步", () => { S.CompleteQuestLesson(q.Id); UpdateQuestTracker(); refresh(); }, 15, Ui.ButtonHot, q.LessonReady == null || q.LessonReady(S)), -1, -1, 1);
            if (S.HasShop && NeedsTimeHint(target))
                Para(card, "等待时关闭弹窗，或在店内手动切到“柜台”页，让时间继续。", 14, Ui.Dim);
        }

        private static bool NeedsTimeHint(QuestTarget target) => target == QuestTarget.Orders || target == QuestTarget.Staff || target == QuestTarget.Restock || target == QuestTarget.Enterprise || target == QuestTarget.Park || target == QuestTarget.Ventures;

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
                case QuestTarget.Agency: return "地点：商业街 · 安居房产中介。沿街看招牌，用 WASD 走到店门，按 F 进入。";
                case QuestTarget.Renovate: return "位置：自己的店铺 → 右侧“装修”页。缺少家具时，到商业街的“宜居家具城”购买。";
                case QuestTarget.Stock: return "位置：自己的店铺 → 右侧“库存”页；也可走近货架按 F 设置。";
                case QuestTarget.Market: return "地点：电子市场的配件档口。沿路前往市场，走到档口门口按 F 进入。";
                case QuestTarget.Counter: return "位置：自己的店铺 · 收银台。走近柜台，顾客排队时按 F 结账。";
                case QuestTarget.Staff: return "位置：自己的店铺 → 右侧“招人”页。";
                case QuestTarget.PhoneShop: return "地点：商业街 · 手机店。沿街找招牌，走到门口按 F 进入。";
                case QuestTarget.Upgrades: return "位置：自己的店铺 → 右侧“升级”页。";
                case QuestTarget.Pricing: return "位置：自己的店铺 → 右侧“定价”页。";
                case QuestTarget.Orders: return "位置：自己的店铺 → 右侧“订单”页。";
                case QuestTarget.Restock: return "入口：顶栏“手机” → 1788 → “自动补货”；还没安装时先在手机应用商店下载。";
                case QuestTarget.Finance: return "入口：顶栏“经营” → “资产与负债”。";
                case QuestTarget.Enterprise: return "入口：顶栏“经营” → “企业合同”。";
                case QuestTarget.Direct: return "入口：顶栏“经营” → “直供采购”。";
                case QuestTarget.Ventures: return "管理入口：顶栏“经营” → “分店经营”。签新铺面要亲自到安居房产中介办理。";
                case QuestTarget.Park: return "规划入口：顶栏“经营” → “园区布局”。买地要亲自到安居房产中介办理。";
                case QuestTarget.Creators: return "入口：顶栏“经营” → “推广合作”。";
                case QuestTarget.Property: return "入口：顶栏“经营” → “物业与住处”。";
                default: return "";
            }
        }
    }
}
