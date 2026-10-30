using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>新手任务：左上角的任务条（告诉玩家现在该去哪、做什么），点它看这一步的详细说明。做完一步才给下一步，目标（三年路线）是另一回事，不在这里。</summary>
    public sealed partial class GameUi
    {
        private Image _questPanel;
        private Text _questText;
        private int _questSeen = -1;

        private void BuildQuestTracker(Transform root)
        {
            _questPanel = Ui.Panel(root, "QuestTracker", new Color(0.05f, 0.06f, 0.08f, 0.82f));
            _questPanel.rectTransform.Anchor(0, 1, 0, 1, 16, -208, -476, 70);
            var btn = _questPanel.gameObject.AddComponent<Button>();
            btn.targetGraphic = _questPanel;
            btn.onClick.AddListener(OpenQuestWindow);
            _questText = Ui.Label(_questPanel.transform, "", 16, TextAnchor.UpperLeft);
            _questText.rectTransform.Fill(8);
            _questText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _questText.verticalOverflow = VerticalWrapMode.Truncate;
            _questPanel.gameObject.SetActive(false);
        }

        /// <summary>任务条的当前文字（冒烟测试也会看）。</summary>
        public string QuestTrackerText => _questPanel != null && _questPanel.gameObject.activeSelf ? _questText.text : null;

        /// <summary>冒烟 / 调试：马上刷新任务条。</summary>
        public void DebugRefreshQuests() => UpdateQuestTracker();

        private void UpdateQuestTracker()
        {
            if (_questPanel == null) return;
            int idx = S.QuestIndex();
            if (_questSeen >= 0 && idx > _questSeen)
            {
                QuestDef nxt = idx < Quests.All.Count ? Quests.All[idx] : null;
                Toast($"<color=#7CE38B>任务完成 ✔ {Quests.All[_questSeen].Title}</color>" + (nxt != null ? $"\n下一步：{nxt.Title}" : "\n新手任务全部完成！"));
            }
            _questSeen = idx;

            string text;
            if (S.IsGameOver)
            {
                _questPanel.gameObject.SetActive(false);
                return;
            }
            if (idx < Quests.All.Count)
            {
                QuestDef q = Quests.All[idx];
                text = $"<b><color=#FFD54A>任务：{q.Title}</color></b>\n{q.Short}\n<size=14><color=#9AA3AD>{q.Progress(S)}</color></size>";
            }
            else
            {
                _questPanel.gameObject.SetActive(false); // 新手引导做完了，任务条就消失
                return;
            }
            _questPanel.gameObject.SetActive(!_ctrl.Placing); // 编辑模式整屏留给房间
            _questText.text = text;
        }

        /// <summary>点任务条：只看当前这一步的详细说明（后面的步骤做完这步才会给）。</summary>
        public void OpenQuestWindow()
        {
            QuestDef q = S.CurrentQuest();
            if (q == null) return;
            OpenWindow("任务", 640, 330, w =>
            {
                QuestDef cur = S.CurrentQuest();
                if (cur == null)
                {
                    Ui.Size(Ui.Label(w.Content, "任务做完了。", 20), -1, 40);
                    return;
                }
                Ui.Size(Ui.Label(w.Content, $"<b><color=#FFD54A>{cur.Title}</color></b>", 24), -1, 40);
                Ui.Size(Ui.Label(w.Content, cur.Hint, 18, TextAnchor.UpperLeft), -1, 120);
                Ui.Size(Ui.Label(w.Content, $"<color=#9AA3AD>{cur.Progress(S)}</color>", 17, TextAnchor.UpperLeft), -1, 60);
            });
        }    }
}
