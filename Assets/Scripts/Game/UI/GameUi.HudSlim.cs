using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private RectTransform _hudTopRow;
        private float _fittedHudWidth;
        private bool _fittedHudHasBrand;

        private void FitHudTopRow()
        {
            float availableWidth = ((RectTransform)_hudTopRow.parent).rect.width;
            bool hasBrand = _brandButton.gameObject.activeSelf;
            if (availableWidth <= 0 || (Mathf.Approximately(availableWidth, _fittedHudWidth) && hasBrand == _fittedHudHasBrand)) return;
            _fittedHudWidth = availableWidth;
            _fittedHudHasBrand = hasBrand;

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
