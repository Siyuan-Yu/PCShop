using System;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 常驻 HUD 瘦身：顶栏次要入口收进“更多”；窗口里的页签条统一成轻量二级页签样式。
    /// </summary>
    public sealed partial class GameUi
    {
        private GameObject _hudMore;

        private void BuildHudMoreMenu(Transform row)
        {
            var panel = Ui.FlatPanel(_dockCanvas.transform, "HudMorePanel", Ui.FlatBand, Ui.FlatHotEdge);
            _hudMore = panel.gameObject;
            int entries = GameSession.FirstChapterTrial ? 4 : 5;
            panel.rectTransform.Anchor(1, 1, 1, 1, -166, -(68 + entries * 42 + 20), 8, 68);
            Ui.VList(panel.rectTransform, 4, 8);

            void Entry(string label, Action action, string name, string tip)
            {
                var b = Ui.Button(panel.transform, label, () => { _hudMore.SetActive(false); action(); }, 17);
                b.name = name; Ui.Size(b, -1, 38); b.Tip(tip);
            }
            Entry("人物", OpenRelationsPanel, "HudRelations", "<b>人脉</b>\n认识的人和你有多熟、他们彼此之间的关系（网状图）。");
            Entry("员工", OpenEmployeesPanel, "HudEmployees", "所有店的员工列表、明天调店安排、进货清单和车辆占用。");
            Entry("日程", OpenSchedulePanel, "HudCalendar", "日程、邀约与已安排的活动。");
            Entry("品牌", OpenBrandOverview, "HudBrand", "品牌管理、KOL签约、直播排班与直播间。");
            if (!GameSession.FirstChapterTrial)
                Entry("赛车", OpenRacingDestinations, "HudRacing", "查看赛场开放条件、入门路线和比赛安排。");
            _brandButton = panel.transform.Find("HudBrand")?.GetComponent<Button>();
            _hudMore.SetActive(false);

            var more = Ui.Size(Ui.Button(row, "更多 ▾", () =>
            {
                bool open = !_hudMore.activeSelf;
                _hudMore.SetActive(open);
                if (open) _hudMore.transform.SetAsLastSibling();
            }, 17), 76);
            more.name = "HudMore";
            more.Tip(GameSession.FirstChapterTrial ? "人物、员工、日程、品牌" : "人物、员工、日程、品牌、赛车");
        }

        private void UpdateHudMore()
        {
            if (_hudMore != null && _hudMore.activeSelf && AnyModalOpen) _hudMore.SetActive(false);
        }

        /// <summary>弹出 / 窗口里的页签条：整行撑满的金色大按钮 → 固定宽度的二级页签（选中浅蓝底加金字）。</summary>
        internal static class TabStrips
        {
            private sealed class Done : MonoBehaviour { }

            private static bool IsHot(Color c) =>
                Mathf.Abs(c.r - Ui.ButtonHot.r) + Mathf.Abs(c.g - Ui.ButtonHot.g) + Mathf.Abs(c.b - Ui.ButtonHot.b) < .03f;

            public static void Restyle(Transform content)
            {
                if (content == null) return;
                foreach (var row in content.GetComponentsInChildren<HorizontalLayoutGroup>(true))
                {
                    if (row.GetComponent<Done>() != null || row.name == "DialogueOptionRow") continue;
                    var buttons = new List<Button>();
                    bool onlyButtons = true;
                    foreach (Transform child in row.transform)
                    {
                        var button = child.GetComponent<Button>();
                        if (button == null) { onlyButtons = false; break; }
                        buttons.Add(button);
                    }
                    if (!onlyButtons || buttons.Count < 3 || buttons.Count > 6) continue;
                    var rowSize = row.GetComponent<LayoutElement>();
                    if (rowSize != null && rowSize.preferredHeight > 48) continue;

                    int selected = -1, hot = 0; bool ok = true;
                    for (int i = 0; i < buttons.Count && ok; i++)
                    {
                        var b = buttons[i]; var text = b.GetComponentInChildren<Text>();
                        var size = b.GetComponent<LayoutElement>();
                        if (b.image == null || text == null || size == null || size.flexibleWidth <= 0 || text.text.Length > 8 || text.text.Length == 0) { ok = false; break; }
                        if (b.name.StartsWith("Shortcut")) { ok = false; break; }
                        if (IsHot(b.image.color)) { hot++; selected = i; }
                    }
                    if (!ok || hot != 1) continue;

                    row.gameObject.AddComponent<Done>();
                    row.childForceExpandWidth = false;
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        var b = buttons[i]; var text = b.GetComponentInChildren<Text>(); var size = b.GetComponent<LayoutElement>();
                        bool on = i == selected;
                        size.flexibleWidth = 0; size.minWidth = 0;
                        size.preferredWidth = text.text.Length > 5 ? 150 : 124;
                        size.preferredHeight = size.minHeight = 34;
                        b.image.color = on ? Ui.FlatRow : Ui.FlatBand;
                        text.color = on ? Ui.Accent : Ui.Dim;
                        text.fontSize = Mathf.Min(text.fontSize, 17);
                    }
                    if (rowSize != null) rowSize.preferredHeight = rowSize.minHeight = 36;
                    var spacer = Ui.Rect(row.transform, "TabStripSpacer");
                    var spacerSize = spacer.gameObject.AddComponent<LayoutElement>(); spacerSize.flexibleWidth = 1; spacerSize.minWidth = 0;
                }
            }
        }
    }
}
