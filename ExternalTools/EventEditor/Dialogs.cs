using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PCShopSell.Core.Session;

namespace PCShopSell.EventEditor
{
    public static class Factory
    {
        public static ListEditor<EventCond> Conds(IWin32Window owner, Action changed = null) => new ListEditor<EventCond>
        {
            Describe = Describe.Cond, Dock = DockStyle.Fill,
            New = () => { var c = new EventCond { type = EventScriptSchema.Conds[0].Type }; return SpecDialog.Edit(owner, c, true) ? c : null; },
            EditItem = c => SpecDialog.Edit(owner, c, true),
            Clone = Ui.Shallow,
        };

        public static ListEditor<EventOp> Ops(IWin32Window owner) => new ListEditor<EventOp>
        {
            Describe = Describe.Op, Dock = DockStyle.Fill,
            New = () => { var o = new EventOp { type = EventScriptSchema.Ops[0].Type }; return SpecDialog.Edit(owner, o, false) ? o : null; },
            EditItem = o => SpecDialog.Edit(owner, o, false),
            Clone = Ui.Shallow,
        };

        public static EventChoiceEntry CloneChoice(EventChoiceEntry c)
        {
            var n = Ui.Shallow(c);
            n.requires = c.requires.Select(Ui.Shallow).ToList();
            n.effects = c.effects.Select(Ui.Shallow).ToList();
            n.onHit = c.onHit.Select(Ui.Shallow).ToList();
            n.onMiss = c.onMiss.Select(Ui.Shallow).ToList();
            n.chanceBonus = c.chanceBonus.Select(b => new EventChanceBonus { add = b.add, when = b.when.Select(Ui.Shallow).ToList() }).ToList();
            return n;
        }

        public static EventEntry CloneEvent(EventEntry e)
        {
            var n = Ui.Shallow(e);
            n.when = e.when.Select(Ui.Shallow).ToList();
            n.whenAny = e.whenAny.Select(Ui.Shallow).ToList();
            n.choices = e.choices.Select(CloneChoice).ToList();
            return n;
        }
    }

    /// <summary>概率加成：满足 when 时概率 + add。</summary>
    public sealed class BonusDialog : Form
    {
        public static bool Edit(IWin32Window owner, EventChanceBonus b)
        {
            var copy = new EventChanceBonus { add = b.add, when = b.when.Select(Ui.Shallow).ToList() };
            using (var d = new BonusDialog(copy, owner)) { if (d.ShowDialog(owner) != DialogResult.OK) return false; }
            b.add = copy.add; b.when = copy.when; return true;
        }

        private BonusDialog(EventChanceBonus b, IWin32Window owner)
        {
            Text = "概率加成"; Width = 620; Height = 360; StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;
            var add = Ui.Num(-1, 1, 2, 0.05m); Ui.SetValue(add, (decimal)b.add);
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(8, 6, 8, 0) };
            top.Controls.Add(Ui.Lbl("满足下面全部条件时，概率增加（可负）：")); top.Controls.Add(add);
            var conds = Factory.Conds(this); conds.Bind(b.when);
            Controls.Add(conds); Controls.Add(top);
            Ui.DialogButtons(this, out var ok);
            ok.Click += (s, e) => b.add = (double)add.Value;
        }
    }

    /// <summary>一个选项：文字、可选条件、直接效果、掷骰（概率 + 命中/未命中效果）。</summary>
    public sealed class ChoiceDialog : Form
    {
        private readonly EventChoiceEntry _c;
        private readonly TextBox _text = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox _reason = new TextBox { Dock = DockStyle.Fill };
        private readonly CheckBox _roll = new CheckBox { Text = "这个选项要掷骰（按概率分命中 / 未命中两种结果）", AutoSize = true };
        private readonly NumericUpDown _chance = Ui.Num(0, 1, 2, 0.05m), _charm = Ui.Num(-1, 1, 3, 0.005m), _intel = Ui.Num(-1, 1, 3, 0.005m), _rel = Ui.Num(-1, 1, 3, 0.005m);
        private readonly ComboBox _npc = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 360 };
        private readonly Panel _rollBody = new Panel { Dock = DockStyle.Fill };

        public static bool Edit(IWin32Window owner, EventChoiceEntry c)
        {
            var copy = Factory.CloneChoice(c);
            using (var d = new ChoiceDialog(copy)) { if (d.ShowDialog(owner) != DialogResult.OK) return false; }
            c.text = copy.text; c.disabledReason = copy.disabledReason; c.requires = copy.requires; c.effects = copy.effects;
            c.chance = copy.chance; c.chanceCharm = copy.chanceCharm; c.chanceIntel = copy.chanceIntel; c.chanceRelation = copy.chanceRelation; c.chanceNpc = copy.chanceNpc;
            c.chanceBonus = copy.chanceBonus; c.onHit = copy.onHit; c.onMiss = copy.onMiss;
            return true;
        }

        internal ChoiceDialog(EventChoiceEntry c)
        {
            _c = c;
            Text = "编辑选项"; Width = 820; Height = 640; StartPosition = FormStartPosition.CenterParent; MinimizeBox = false;

            var head = new TableLayoutPanel { Dock = DockStyle.Top, Height = 66, ColumnCount = 2, Padding = new Padding(8, 6, 8, 0) };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            head.Controls.Add(Ui.Lbl("按钮文字")); head.Controls.Add(_text);
            head.Controls.Add(Ui.Lbl("灰掉的理由")); head.Controls.Add(_reason);
            _text.Text = c.text ?? ""; _reason.Text = c.disabledReason ?? "";

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(Page("选项条件（不满足就灰掉）", Group("全部满足才能点这个选项；留空 = 总能点。有条件时必须写“灰掉的理由”。", MakeConds(c.requires))));
            tabs.TabPages.Add(Page("直接效果", Group("点了就执行，按顺序。", MakeOps(c.effects))));
            tabs.TabPages.Add(Page("掷骰", BuildRoll(c)));
            Controls.Add(tabs); Controls.Add(head);
            Ui.DialogButtons(this, out var ok);
            ok.Click += (s, e) => Collect();
        }

        private ListEditor<EventCond> MakeConds(List<EventCond> list) { var l = Factory.Conds(this); l.Bind(list); return l; }
        private ListEditor<EventOp> MakeOps(List<EventOp> list) { var l = Factory.Ops(this); l.Bind(list); return l; }

        private static TabPage Page(string title, Control body) { var p = new TabPage(title) { Padding = new Padding(6) }; body.Dock = DockStyle.Fill; p.Controls.Add(body); return p; }

        private static Control Group(string hint, Control body)
        {
            var p = new Panel { Dock = DockStyle.Fill };
            p.Controls.Add(body); body.Dock = DockStyle.Fill;
            p.Controls.Add(new Label { Text = hint, Dock = DockStyle.Top, Height = 24, ForeColor = Color.DimGray });
            return p;
        }

        private Control BuildRoll(EventChoiceEntry c)
        {
            var root = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            _roll.Checked = c.chance >= 0;
            Ui.SetValue(_chance, (decimal)Math.Max(0, c.chance)); Ui.SetValue(_charm, (decimal)c.chanceCharm); Ui.SetValue(_intel, (decimal)c.chanceIntel); Ui.SetValue(_rel, (decimal)c.chanceRelation);
        foreach (var n in NpcList.All) _npc.Items.Add(n.ToString());
            var hit = NpcList.All.FirstOrDefault(n => n.Id == c.chanceNpc); _npc.Text = hit != null ? hit.ToString() : c.chanceNpc ?? "";

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 150, ColumnCount = 4, Padding = new Padding(4) };
            void Row(string a, Control ca, string b, Control cb) { grid.Controls.Add(Ui.Lbl(a)); grid.Controls.Add(ca); grid.Controls.Add(Ui.Lbl(b)); grid.Controls.Add(cb); }
            Row("基础概率(0–1)", _chance, "魅力每点加", _charm);
            Row("智力每点加", _intel, "交情的人物", _npc);
            Row("交情每点加", _rel, "", new Label());

            var bonus = new ListEditor<EventChanceBonus>
            {
                Dock = DockStyle.Fill,
                Describe = b => (b.add >= 0 ? "+" : "") + b.add.ToString(System.Globalization.CultureInfo.InvariantCulture) + "  当 " + (b.when.Count == 0 ? "（无条件）" : string.Join(" 且 ", b.when.Select(Describe.Cond))),
                New = () => { var b = new EventChanceBonus(); return BonusDialog.Edit(this, b) ? b : null; },
                EditItem = b => BonusDialog.Edit(this, b),
                Clone = b => new EventChanceBonus { add = b.add, when = b.when.Select(Ui.Shallow).ToList() },
            };
            bonus.Bind(c.chanceBonus);
            var lists = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            lists.RowStyles.Add(new RowStyle(SizeType.Percent, 34)); lists.RowStyles.Add(new RowStyle(SizeType.Percent, 33)); lists.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
            lists.Controls.Add(Titled("概率加成（满足条件时再加）", bonus));
            lists.Controls.Add(Titled("命中时的效果", MakeOps(c.onHit)));
            lists.Controls.Add(Titled("未命中时的效果", MakeOps(c.onMiss)));

            _rollBody.Controls.Add(lists); _rollBody.Controls.Add(grid);
            _rollBody.Enabled = _roll.Checked; _roll.CheckedChanged += (s, e) => _rollBody.Enabled = _roll.Checked;
            root.Controls.Add(_rollBody); root.Controls.Add(_roll);
            _roll.Dock = DockStyle.Top; _roll.Padding = new Padding(6, 6, 0, 6);
            return root;
        }

        private static Control Titled(string title, Control body)
        {
            var p = new Panel { Dock = DockStyle.Fill };
            p.Controls.Add(body); body.Dock = DockStyle.Fill;
            p.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 20 });
            return p;
        }

        internal void Collect()
        {
            _c.text = _text.Text.Trim(); _c.disabledReason = _reason.Text.Trim();
            if (_roll.Checked)
            {
                _c.chance = (double)_chance.Value; _c.chanceCharm = (double)_charm.Value; _c.chanceIntel = (double)_intel.Value; _c.chanceRelation = (double)_rel.Value;
                string s = _npc.Text.Trim(); var npc = NpcList.All.FirstOrDefault(n => n.ToString() == s);
                _c.chanceNpc = npc != null ? npc.Id : s.Length == 0 ? null : (s.Contains("  ") ? s.Substring(0, s.IndexOf("  ")) : s);
            }
            else
            {
                _c.chance = -1; _c.chanceCharm = _c.chanceIntel = _c.chanceRelation = 0; _c.chanceNpc = null;
                _c.chanceBonus = new List<EventChanceBonus>(); _c.onHit = new List<EventOp>(); _c.onMiss = new List<EventOp>();
            }
        }
    }
}