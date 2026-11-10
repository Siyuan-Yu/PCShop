using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCShopSell.EventEditor
{
    /// <summary>一个列表 + 添加/编辑/复制/删除/上移/下移 按钮。列表直接改传进来的 List。</summary>
    public sealed class ListEditor<T> : UserControl where T : class
    {
        private readonly ListBox _box = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        private readonly FlowLayoutPanel _bar = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 70, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        private List<T> _items = new List<T>();

        public Func<T, string> Describe = x => x.ToString();
        /// <summary>创建一个新条目；返回 null = 取消。</summary>
        public Func<T> New;
        /// <summary>就地编辑；返回 true = 点了确定。</summary>
        public Func<T, bool> EditItem;
        public Func<T, T> Clone;
        public event Action Changed;

        public ListEditor()
        {
            Height = 110;
            Controls.Add(_box); Controls.Add(_bar);
            AddButton("添加", () => { var n = New?.Invoke(); if (n == null) return; _items.Add(n); Reload(_items.Count - 1); Changed?.Invoke(); });
            AddButton("编辑", Edit);
            AddButton("复制", () => { if (_box.SelectedIndex < 0 || Clone == null) return; _items.Insert(_box.SelectedIndex + 1, Clone(_items[_box.SelectedIndex])); Reload(_box.SelectedIndex + 1); Changed?.Invoke(); });
            AddButton("删除", () => { int i = _box.SelectedIndex; if (i < 0) return; _items.RemoveAt(i); Reload(Math.Min(i, _items.Count - 1)); Changed?.Invoke(); });
            AddButton("上移", () => MoveSelected(-1));
            AddButton("下移", () => MoveSelected(1));
            _box.DoubleClick += (s, e) => Edit();
        }

        private void AddButton(string text, Action click)
        {
            var b = new Button { Text = text, Width = 62, Height = 24, Margin = new Padding(2, 1, 2, 1) };
            b.Click += (s, e) => click();
            _bar.Controls.Add(b);
        }

        public void Bind(List<T> items) { _items = items; Reload(-1); }
        public void SetEnabled(bool on) { _bar.Enabled = on; }

        private void Edit()
        {
            int i = _box.SelectedIndex; if (i < 0 || EditItem == null) return;
            if (EditItem(_items[i])) { Reload(i); Changed?.Invoke(); }
        }

        private void MoveSelected(int d)
        {
            int i = _box.SelectedIndex, j = i + d; if (i < 0 || j < 0 || j >= _items.Count) return;
            var t = _items[i]; _items[i] = _items[j]; _items[j] = t; Reload(j); Changed?.Invoke();
        }

        private void Reload(int select)
        {
            _box.BeginUpdate(); _box.Items.Clear();
            foreach (var it in _items) _box.Items.Add(Describe(it));
            _box.EndUpdate();
            if (select >= 0 && select < _box.Items.Count) _box.SelectedIndex = select;
        }
    }

    public static class Ui
    {
        public static Label Lbl(string text) => new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 6, 3) };

        public static NumericUpDown Num(decimal min, decimal max, int decimals, decimal step = 1) =>
            new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = step, Width = 110, ThousandsSeparator = false };

        public static void SetValue(NumericUpDown n, decimal v) => n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, v));

        public static Button DialogButtons(Form f, out Button ok)
        {
            ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Width = 80 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };
            f.AcceptButton = ok; f.CancelButton = cancel;
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(6) };
            bar.Controls.Add(cancel); bar.Controls.Add(ok);
            f.Controls.Add(bar);
            return cancel;
        }

        public static T Shallow<T>(T o) => (T)typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(o, null);
    }
}