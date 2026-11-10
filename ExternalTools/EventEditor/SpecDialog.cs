using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PCShopSell.Core.Session;

namespace PCShopSell.EventEditor
{
    /// <summary>条件 / 效果的编辑框：按 schema 只显示这个 type 用到的字段。target 是 EventCond 或 EventOp，用反射读写同名字段。</summary>
    public sealed class SpecDialog : Form
    {
        private readonly object _target;
        private readonly TypeSpec[] _specs;
        private readonly bool _isCond;
        private readonly ComboBox _type = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
        private readonly TableLayoutPanel _form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(8) };
        private readonly CheckBox _not = new CheckBox { Text = "取反（不满足时才算数）", AutoSize = true };
        private readonly Dictionary<string, Control> _inputs = new Dictionary<string, Control>();
        private readonly Label _hint = new Label { Dock = DockStyle.Bottom, Height = 32, ForeColor = Color.DimGray, Padding = new Padding(8, 0, 8, 0) };

        public static bool Edit(IWin32Window owner, object target, bool isCond)
        {
            using (var d = new SpecDialog(target, isCond)) return d.ShowDialog(owner) == DialogResult.OK;
        }

        internal SpecDialog(object target, bool isCond)
        {
            _target = target; _isCond = isCond; _specs = isCond ? EventScriptSchema.Conds : EventScriptSchema.Ops;
            Text = isCond ? "编辑条件" : "编辑效果";
            Width = 640; Height = 520; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable; MinimizeBox = false; MaximizeBox = false;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 8, 8, 0) };
            top.Controls.Add(Ui.Lbl("类型")); top.Controls.Add(_type);
            foreach (var s in _specs) _type.Items.Add(s.Name + "  [" + s.Type + "]");
            string cur = Get<string>("type"); int idx = Array.FindIndex(_specs, s => s.Type == cur);
            _type.SelectedIndex = idx >= 0 ? idx : 0;
            _not.Checked = isCond && Get<bool>("not");
            Controls.Add(_form); Controls.Add(top); Controls.Add(_hint);
            Ui.DialogButtons(this, out var ok);
            _type.SelectedIndexChanged += (s, e) => Rebuild();
            Rebuild();
            ok.Click += (s, e) => Apply();
        }

        private TypeSpec Spec => _specs[Math.Max(0, _type.SelectedIndex)];

        private T Get<T>(string field)
        {
            var f = _target.GetType().GetField(field); var v = f.GetValue(_target);
            return v == null ? default(T) : (T)Convert.ChangeType(v, typeof(T));
        }

        private void Rebuild()
        {
            _form.SuspendLayout(); _form.Controls.Clear(); _form.RowStyles.Clear(); _inputs.Clear();
            var spec = Spec;
            foreach (string def in spec.Fields)
            {
                string field = def.Substring(0, def.IndexOf(':')), label = spec.Label(field);
                var fi = _target.GetType().GetField(field);
                Control input = MakeInput(field, label, fi.FieldType);
                _inputs[field] = input;
                if (input is CheckBox) { _form.Controls.Add(new Label()); _form.Controls.Add(input); }
                else { _form.Controls.Add(Ui.Lbl(label.Split('；')[0])); _form.Controls.Add(input); }
            }
            if (_isCond) { _form.Controls.Add(new Label()); _form.Controls.Add(_not); }
            if (spec.Fields.Length == 0 && !_isCond) _form.Controls.Add(Ui.Lbl("这种效果不需要填写参数。"));
            _hint.Text = spec.Fields.Where(f => f.Contains("；")).Select(f => f.Substring(f.IndexOf(':') + 1)).FirstOrDefault() ?? "";
            _form.ResumeLayout();
        }

        private Control MakeInput(string field, string label, Type t)
        {
            if (t == typeof(bool)) return new CheckBox { Text = label, AutoSize = true, Checked = Get<bool>(field) };
            bool npc = field == "npc" || label.Contains("人物") || label.Contains("出借人");
            if (npc)
            {
                var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 420 };
                foreach (var n in NpcList.All) c.Items.Add(n.ToString());
                string id = Get<string>(field); var hit = NpcList.All.FirstOrDefault(n => n.Id == id);
                c.Text = hit != null ? hit.ToString() : id ?? "";
                return c;
            }
            if (field == "kind")
            {
                var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 420 };
                c.Items.AddRange(new object[] { "dinner", "market" }); c.Text = Get<string>(field) ?? ""; return c;
            }
            if (t == typeof(string))
            {
                bool multi = field == "text" || field == "alt";
                return new TextBox { Width = 420, Height = multi ? 90 : 24, Multiline = multi, ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None, Text = (Get<string>(field) ?? "").Replace("\n", "\r\n") };
            }
            int dec = t == typeof(double) ? 2 : 0;
            var n2 = Ui.Num(-1000000000m, 1000000000m, dec, dec > 0 ? 0.5m : 1m);
            Ui.SetValue(n2, Convert.ToDecimal(Get<double>(field)));
            return n2;
        }

        internal void Apply()
        {
            var spec = Spec;
            foreach (var fi in _target.GetType().GetFields())
                if (fi.Name != "type" && fi.Name != "not") fi.SetValue(_target, fi.FieldType == typeof(string) ? null : Activator.CreateInstance(fi.FieldType));
            _target.GetType().GetField("type").SetValue(_target, spec.Type);
            if (_isCond) _target.GetType().GetField("not").SetValue(_target, _not.Checked);
            foreach (var kv in _inputs)
            {
                var fi = _target.GetType().GetField(kv.Key); object v;
                if (kv.Value is CheckBox cb) v = cb.Checked;
                else if (kv.Value is NumericUpDown nud) v = Convert.ChangeType(nud.Value, fi.FieldType);
                else
                {
                    string s = kv.Value.Text.Replace("\r\n", "\n").Trim();
                    var npc = NpcList.All.FirstOrDefault(n => n.ToString() == s);
                    v = npc != null ? npc.Id : (s.Length == 0 ? null : s);
                    if (npc == null && s.Contains("  ") && NpcList.All.Any(n => s.StartsWith(n.Id + "  "))) v = s.Substring(0, s.IndexOf("  "));
                }
                fi.SetValue(_target, v);
            }
        }
    }

    public static class Describe
    {
        public static string Cond(EventCond c)
        {
            var spec = EventScriptSchema.Cond(c.type);
            if (spec == null) return "??? " + c.type;
            return (c.not ? "【非】" : "") + spec.Name + Fields(spec, c);
        }

        public static string Op(EventOp o)
        {
            var spec = EventScriptSchema.Op(o.type);
            if (spec == null) return "??? " + o.type;
            return spec.Name + Fields(spec, o);
        }

        private static string Fields(TypeSpec spec, object target)
        {
            var parts = new List<string>();
            foreach (string def in spec.Fields)
            {
                string f = def.Substring(0, def.IndexOf(':')); var v = target.GetType().GetField(f).GetValue(target);
                if (v is bool b) { if (b) parts.Add(spec.Label(f).Split('；')[0]); continue; }
                if (v == null || (v is string s && s.Length == 0)) continue;
                if (v is double d && d == 0 && f != "value") continue;
                string shown = f == "npc" || (f == "key" && spec.Label(f).Contains("人物")) ? NpcList.Name(v as string) : v is double dd ? dd.ToString(System.Globalization.CultureInfo.InvariantCulture) : v.ToString();
                if (shown.Length > 40) shown = shown.Substring(0, 40) + "…";
                parts.Add(f == "text" || f == "alt" ? "「" + shown + "」" : shown);
            }
            return parts.Count == 0 ? "" : " " + string.Join(" · ", parts);
        }
    }
}