using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>居家工作台的小组件：数值行、进度条行、状态标签、表格行、可点击分组条。风格对应设计稿的平面深蓝样式。</summary>
    public sealed partial class GameUi
    {
        private static readonly Color WbRowOpen = new Color32(0x4A, 0x3F, 0x25, 255);

        private static Color WbKindFill(char kind)
        {
            switch (kind) { case 'r': return new Color32(0x5A, 0x2F, 0x2A, 255); case 'y': return new Color32(0x54, 0x45, 0x1F, 255); case 'g': return new Color32(0x2A, 0x4A, 0x38, 255); default: return new Color32(0x33, 0x46, 0x5F, 255); }
        }
        private static Color WbKindEdge(char kind)
        {
            switch (kind) { case 'r': return new Color32(0xB5, 0x60, 0x3E, 255); case 'y': return Ui.ButtonHot; case 'g': return new Color32(0x4F, 0x9B, 0x6A, 255); default: return Ui.FlatWellEdge; }
        }
        private static Color WbKindInk(char kind)
        {
            switch (kind) { case 'r': return new Color32(0xF0, 0xB0, 0xA0, 255); case 'y': return new Color32(0xF0, 0xD8, 0x90, 255); case 'g': return Ui.Good; default: return Ui.FlatInk; }
        }

        /// <summary>金色小标题。</summary>
        private static Text WbHeading(Transform parent, string text, int size = 19) =>
            Bold(Ui.Size(Ui.Label(parent, text, size, TextAnchor.MiddleLeft, Ui.Accent), -1, size * 1.5f));

        private static Text Bold(Text t) { t.fontStyle = FontStyle.Bold; return t; }

        /// <summary>紧凑操作按钮：固定宽度、不撑满整行；放在行里靠左，尾部用 WbFlex 把剩余空间让出去。</summary>
        private static Button WbAction(Transform parent, string label, Action action, string name, float width = 150, bool primary = false, float height = 32)
        {
            var b = Ui.Button(parent, label, action, 15, primary ? Ui.ButtonHot : Ui.ButtonBg);
            b.name = name; Ui.Size(b, width, height, 0); return b;
        }

        /// <summary>入口行：左对齐文字 + 右侧小箭头，用于“点进去看详情”的入口，比整条金色按钮安静。</summary>
        private static Button WbLink(Transform parent, string label, Action action, string name, float height = 40)
        {
            var b = Ui.Button(parent, label, action, 16, Ui.FlatBand);
            b.name = name;
            var text = b.GetComponentInChildren<Text>();
            if (text != null) { text.alignment = TextAnchor.MiddleLeft; text.rectTransform.Anchor(0, 0, 1, 1, 14, 0, 34, 0); }
            var arrow = Ui.Label(b.transform, "›", 24, TextAnchor.MiddleRight, WbDim); arrow.raycastTarget = false;
            arrow.rectTransform.Anchor(0, 0, 1, 1, 0, 0, 14, 0);
            Ui.Size(b, -1, height); return b;
        }

        /// <summary>小号分段切换（周期等筛选）：选中浅蓝底加金字。</summary>
        private static void WbSegment(Transform row, string[] labels, int selected, Action<int> pick, string namePrefix, float width = 76, float height = 28)
        {
            for (int i = 0; i < labels.Length; i++) { int n = i; WbSubTab(row, labels[i], () => pick(n), namePrefix + i, selected == i, width, height); }
        }

        /// <summary>状态小标签：g 绿 / y 黄 / r 红 / n 灰。</summary>
        private static void WbChip(Transform parent, string text, char kind = 'n')
        {
            var chip = Ui.FlatPanel(parent, "Chip", WbKindFill(kind), WbKindEdge(kind)).rectTransform;
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 1, 1); layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false; layout.childAlignment = TextAnchor.MiddleCenter;
            Ui.Label(chip, text, 13, TextAnchor.MiddleCenter, WbKindInk(kind)).horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Size(chip, -1, 22, 0);
        }

        private static RectTransform WbFlex(Transform parent) { var spacer = Ui.Rect(parent, "Spacer"); Ui.Size(spacer, 0, -1, 1); return spacer; }

        private static void WbDivider(RectTransform row, Color? color = null)
        {
            var line = Ui.Panel(row, "Divider", color ?? new Color32(0x2F, 0x42, 0x62, 255)); line.raycastTarget = false;
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -1);
        }

        /// <summary>「标签 ……… 数值」一行：标签灰、数值右对齐，底部细分隔线。</summary>
        private static Text WbStat(Transform parent, string label, string value, Color? valueColor = null, int size = 16)
        {
            var row = Ui.Row(parent, size + 12, 8);
            Ui.Size(Ui.Label(row, label, size, TextAnchor.MiddleLeft, WbDim), 0, -1, 1);
            var text = Ui.Label(row, value, size, TextAnchor.MiddleRight, valueColor ?? WbInk); text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Size(text, -1, -1, 0);
            WbDivider(row);
            return text;
        }

        /// <summary>「标签 [细条] 数值」一行；warn 时条变红。</summary>
        private static void WbMeter(Transform parent, string label, double value, double max, string valueText = null, bool warn = false)
        {
            var row = Ui.Row(parent, 24, 8); row.name = "Meter_" + label;
            Ui.Size(Ui.Label(row, label, 15, TextAnchor.MiddleLeft, WbDim), 58, -1, 0);
            var track = Ui.Panel(row, "MeterTrack", Ui.FlatWell); Ui.FlatBorder(track, Ui.FlatButtonEdge); Ui.Size(track, 0, 10, 1);
            var fill = Ui.Panel(track.transform, "Value", warn ? Ui.Warn : Ui.Accent); fill.raycastTarget = false;
            fill.rectTransform.Anchor(0, 0, (float)Math.Max(0, Math.Min(1, value / Math.Max(1, max))), 1);
            var number = Ui.Label(row, valueText ?? value.ToString("0"), 15, TextAnchor.MiddleRight, WbInk); number.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Size(number, 78, -1, 0);
        }

        /// <summary>表格行：第一格靠左并占满剩余宽度，其余格固定宽度、右对齐。header 为灰色小字表头。</summary>
        private static RectTransform WbTable(Transform parent, string[] cells, float[] widths, bool header = false, Color[] inks = null, float height = 0)
        {
            var row = Ui.Row(parent, height > 0 ? height : header ? 22 : 34, 10);
            for (int i = 0; i < cells.Length; i++)
            {
                Color ink = header ? WbDim : (inks != null && i < inks.Length && inks[i].a > 0 ? inks[i] : WbInk);
                var text = Ui.Label(row, cells[i], header ? 13 : 16, i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, ink);
                text.horizontalOverflow = i == 0 ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                if (i == 0) Ui.Size(text, 0, -1, 1); else Ui.Size(text, widths[i - 1], -1, 0);
            }
            if (!header) WbDivider(row, new Color32(0x2A, 0x3A, 0x55, 255));
            return row;
        }

        /// <summary>分组条：左侧金色竖条、深色底、金色标题；点击可折叠。</summary>
        private static RectTransform WbGroupBar(Transform parent, string name, string title, string info, Action click, char chipKind = 'n', string chipText = null)
        {
            var image = Ui.Panel(parent, name, Ui.FlatBand); var bar = image.rectTransform;
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14; layout.padding = new RectOffset(0, 12, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true; layout.childAlignment = TextAnchor.MiddleLeft;
            Ui.Size(bar, -1, 34, 0);
            Ui.Size(Ui.Panel(bar, "Rail", Ui.Accent), 5, -1, 0);
            var head = Ui.Label(bar, title, 17, TextAnchor.MiddleLeft, Ui.Accent); head.fontStyle = FontStyle.Bold; head.horizontalOverflow = HorizontalWrapMode.Overflow; Ui.Size(head, -1, -1, 0);
            var dim = Ui.Label(bar, info, 13, TextAnchor.MiddleLeft, WbDim); dim.horizontalOverflow = HorizontalWrapMode.Overflow;
            WbFlex(bar);
            if (!string.IsNullOrEmpty(chipText)) WbChip(bar, chipText, chipKind);
            if (click != null)
            {
                var button = bar.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => click());
                var colors = button.colors; colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f); button.colors = colors;
            }
            return bar;
        }

        /// <summary>手风琴行：深蓝底、细边，展开时变金棕色；点击切换。内容由调用方往返回的行里加。</summary>
        private static RectTransform WbAccordionRow(Transform parent, string name, bool open, Action click)
        {
            var image = Ui.FlatPanel(parent, name, open ? WbRowOpen : Ui.FlatRow, open ? Ui.ButtonHot : Ui.FlatCardEdge); var row = image.rectTransform;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14; layout.padding = new RectOffset(14, 14, 5, 5);
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true; layout.childAlignment = TextAnchor.MiddleLeft;
            Ui.Size(row, -1, 52, 0);
            var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => click());
            var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); button.colors = colors;
            return row;
        }

        /// <summary>手风琴行里的一格指标：上面灰色小字名称，下面数值。</summary>
        private static void WbCell(Transform parent, string head, string value, float width = 120, Color? valueColor = null)
        {
            var cell = Ui.Rect(parent, "Cell_" + head); Ui.VList(cell, 0, 0); Ui.Size(cell, width, -1, 0);
            Ui.Size(Ui.Label(cell, head, 13, TextAnchor.MiddleLeft, WbDim), -1, 17);
            var text = Ui.Label(cell, value, 16, TextAnchor.MiddleLeft, valueColor ?? WbInk); text.horizontalOverflow = HorizontalWrapMode.Overflow; Ui.Size(text, -1, 22);
        }
    }
}
