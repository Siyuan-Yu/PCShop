using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PCShopSell.Core.Session;

namespace PCShopSell.EventEditor
{
    public sealed class MainForm : Form
    {
        private string _path;
        private EventScriptFile _file = new EventScriptFile();
        private EventEntry _cur;
        private bool _loading, _dirty;

        private readonly TextBox _filter = new TextBox { Dock = DockStyle.Top };
        private readonly ListBox _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        private readonly List<EventEntry> _view = new List<EventEntry>();

        private readonly TextBox _id = new TextBox { Width = 260 }, _title = new TextBox { Dock = DockStyle.Fill }, _body = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 80 };
        private readonly ComboBox _group = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDown };
        private readonly Label _kind = new Label { AutoSize = true, ForeColor = Color.DarkRed, Margin = new Padding(3, 6, 3, 3) };
        private readonly TextBox _minDay = new TextBox { Width = 70 }, _maxDay = new TextBox { Width = 70 }, _cooldown = new TextBox { Width = 70 }, _weight = new TextBox { Width = 70 }, _chance = new TextBox { Width = 70 };
        private ListEditor<EventCond> _when, _whenAny;
        private ListEditor<EventChoiceEntry> _choices;
        private readonly ListBox _problems = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        private readonly Label _status = new Label { Dock = DockStyle.Bottom, Height = 22, Padding = new Padding(6, 3, 0, 0) };
        private readonly List<Control> _codeLocked = new List<Control>();

        public MainForm(string path)
        {
            _path = path;
            Width = 1280; Height = 860; StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9f);
            KeyPreview = true;
            BuildUi();
            NpcList.Load();
            if (path == null || !File.Exists(path)) { Pick(); } else OpenFile(path);
            FormClosing += (s, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
            KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.S) { Save(); e.SuppressKeyPress = true; } };
        }

        // ------------------------------------------------------------ 界面
        private void BuildUi()
        {
            var bar = new ToolStrip { Dock = DockStyle.Top };
            bar.Items.Add(new ToolStripButton("保存 (Ctrl+S)", null, (s, e) => Save()));
            bar.Items.Add(new ToolStripButton("重新载入", null, (s, e) => { if (ConfirmDiscard() && _path != null) OpenFile(_path); }));
            bar.Items.Add(new ToolStripButton("打开其他 events.json…", null, (s, e) => { if (ConfirmDiscard()) Pick(); }));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripButton("重新读取人物名单", null, (s, e) => { NpcList.Load(); _status.Text = "人物名单：" + NpcList.All.Count + " 人 " + NpcList.Source; }));

            // 左：事件列表
            var left = new Panel { Dock = DockStyle.Left, Width = 320, Padding = new Padding(4) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, WrapContents = true };
            foreach (var (text, act) in new (string, Action)[] { ("新建", NewEvent), ("复制", CopyEvent), ("删除", DeleteEvent), ("上移", () => MoveEvent(-1)), ("下移", () => MoveEvent(1)) })
            { var b = new Button { Text = text, Width = 58, Height = 26 }; b.Click += (s, e) => act(); buttons.Controls.Add(b); }
            left.Controls.Add(_list); left.Controls.Add(buttons);
            left.Controls.Add(_filter);
            _filter.TextChanged += (s, e) => Reload(_cur);
            _list.SelectedIndexChanged += (s, e) => { if (_loading) return; Select(_list.SelectedIndex >= 0 && _list.SelectedIndex < _view.Count ? _view[_list.SelectedIndex] : null); };
            var placeholder = new Label { Text = "筛选（id / 标题）", Dock = DockStyle.Top, Height = 18, ForeColor = Color.DimGray };
            left.Controls.Add(placeholder);

            // 右：详情
            var rightHost = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            rightHost.SplitterDistance = 640; rightHost.FixedPanel = FixedPanel.Panel2;
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };
            var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Width = 900 };
            stack.Dock = DockStyle.Top;
            stack.Controls.Add(BasicGroup());
            stack.Controls.Add(TimingGroup());
            stack.Controls.Add(ConditionGroup());
            stack.Controls.Add(ChoiceGroup());
            scroll.Controls.Add(stack);
            scroll.SizeChanged += (s, e) => stack.Width = Math.Max(600, scroll.ClientSize.Width - 8);
            rightHost.Panel1.Controls.Add(scroll);
            var pbox = new GroupBox { Text = "校验（保存前请清零）", Dock = DockStyle.Fill };
            pbox.Controls.Add(_problems); rightHost.Panel2.Controls.Add(pbox);

            Controls.Add(rightHost); Controls.Add(left); Controls.Add(bar); Controls.Add(_status);
        }

        private static GroupBox Box(string title, int height) => new GroupBox { Text = title, Height = height, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(8, 4, 8, 6) };

        private Control BasicGroup()
        {
            var g = Box("基本", 210); g.Dock = DockStyle.Fill; g.Width = 880;
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var idRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill }; idRow.Controls.Add(_id); idRow.Controls.Add(Ui.Lbl("分组")); idRow.Controls.Add(_group); idRow.Controls.Add(_kind);
            _group.Items.AddRange(new object[] { "", "npc" });
            t.Controls.Add(Ui.Lbl("id（唯一）")); t.Controls.Add(idRow);
            t.Controls.Add(Ui.Lbl("标题")); t.Controls.Add(_title);
            t.Controls.Add(Ui.Lbl("")); t.Controls.Add(new Label { Text = "正文里可以直接写人物对白；{item} 只在“随机丢一件货”之后的文字里有意义。", ForeColor = Color.DimGray, AutoSize = true });
            t.Controls.Add(Ui.Lbl("正文")); t.Controls.Add(_body);
            g.Controls.Add(t);
            _codeLocked.AddRange(new Control[] { _title, _body, _group });
            _id.TextChanged += (s, e) => Edit(() => { _cur.id = _id.Text.Trim(); RefreshRow(); });
            _title.TextChanged += (s, e) => Edit(() => { _cur.title = _title.Text; RefreshRow(); });
            _body.TextChanged += (s, e) => Edit(() => _cur.body = _body.Text.Replace("\r\n", "\n"));
            _group.TextChanged += (s, e) => Edit(() => _cur.group = _group.Text.Trim());
            return g;
        }

        private Control TimingGroup()
        {
            var g = Box("触发时机（每天 11:00 先按“每日出事件概率”掷一次；掷中后，只在下面这些都满足的事件里按权重抽一个）", 128);
            var f = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, WrapContents = true };
            void Add(string label, TextBox box, string tip) { f.Controls.Add(Ui.Lbl(label)); f.Controls.Add(box); new ToolTip().SetToolTip(box, tip); }
            Add("最早第几天", _minDay, "开局那天是第 0 天；留空 = 不限（游戏里至少第 2 天才有事件）");
            Add("最晚第几天", _maxDay, "超过这一天就不再触发；留空 = 没有上限。例：30 = 只在开局前 30 天内");
            Add("冷却天数", _cooldown, "同一事件两次之间至少隔几天；留空 = 默认 10。填 9999 = 一局只触发一次");
            Add("权重", _weight, "抽取时的比重，越大越容易；留空 = 默认 10");
            Add("触发概率(0–1)", _chance, "轮到它时再掷一次，没掷中就当没有这个事件；0.3 = 30%。留空 = 必定有资格");
            g.Controls.Add(new Label { Text = "留空 = 用默认；鼠标放在输入框上有说明。触发概率适合“有，但不一定每次都来”的事件。", Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.DimGray });
            g.Controls.Add(f);
            foreach (var box in new[] { _minDay, _maxDay, _cooldown, _weight, _chance }) { var b = box; b.TextChanged += (s, e) => Edit(() => ReadNumbers()); }
            return g;
        }

        private Control ConditionGroup()
        {
            var g = Box("触发条件（都不写 = 每天都可能出现）", 300);
            _when = Factory.Conds(this); _whenAny = Factory.Conds(this);
            foreach (var l in new[] { _when, _whenAny }) l.Changed += () => Edit(null);
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            t.Controls.Add(new Label { Text = "必须全部满足（且）", AutoSize = true });
            t.Controls.Add(_when);
            t.Controls.Add(new Label { Text = "再要求下面至少满足一条（或，可不写）", AutoSize = true });
            t.Controls.Add(_whenAny);
            g.Controls.Add(t);
            _codeLocked.AddRange(new Control[] { _when, _whenAny });
            return g;
        }

        private Control ChoiceGroup()
        {
            var g = Box("选项（至少 2 个，其中至少 1 个总能点）", 200);
            _choices = new ListEditor<EventChoiceEntry>
            {
                Dock = DockStyle.Fill,
                Describe = c => (c.requires.Count > 0 ? "[有条件] " : "") + (c.chance >= 0 ? "[掷骰 " + (int)Math.Round(c.chance * 100) + "%] " : "") + c.text + "    → " + string.Join("；", c.effects.Concat(c.onHit).Select(Describe.Op)).Replace('\n', ' '),
                New = () => { var c = new EventChoiceEntry { text = "新选项", effects = { new EventOp { type = "text", text = "……" } } }; return ChoiceDialog.Edit(this, c) ? c : null; },
                EditItem = c => ChoiceDialog.Edit(this, c),
                Clone = Factory.CloneChoice,
            };
            _choices.Changed += () => Edit(null);
            g.Controls.Add(_choices);
            _codeLocked.Add(_choices);
            return g;
        }

        // ------------------------------------------------------------ 文件
        private void Pick()
        {
            using (var d = new OpenFileDialog { Filter = "events.json|events.json|JSON|*.json", Title = "选择 events.json（Assets/Resources/Data）" })
                if (d.ShowDialog(this) == DialogResult.OK) OpenFile(d.FileName);
        }

        private void OpenFile(string path)
        {
            _path = path; _file = Program.Load(path); _dirty = false;
            _group.Items.Clear(); _group.Items.Add(""); foreach (string gr in _file.events.Select(e => e.group).Where(x => !string.IsNullOrEmpty(x)).Distinct()) _group.Items.Add(gr);
            Reload(_file.events.FirstOrDefault());
            Revalidate(); UpdateTitle();
            _status.Text = path + "    人物名单：" + (NpcList.All.Count == 0 ? "没找到 npcs.json（人物只能手填 id；在 Unity 里运行菜单“PCShop/事件/导出人物名单给事件编辑器”）" : NpcList.All.Count + " 人");
        }

        private void Save()
        {
            if (_path == null) return;
            var errors = ValidateAll();
            if (errors.Count > 0 && MessageBox.Show(this, errors.Count + " 个问题没解决，仍然保存？\n\n" + string.Join("\n", errors.Take(8)), "保存", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            Program.Save(_path, _file); _dirty = false; UpdateTitle();
            _status.Text = "已保存 " + DateTime.Now.ToString("HH:mm:ss") + "  " + _path;
        }

        private bool ConfirmDiscard() => !_dirty || MessageBox.Show(this, "有没保存的修改，丢弃？", "事件编辑器", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        private void UpdateTitle() => Text = "事件编辑器 — " + (_path == null ? "" : Path.GetFileName(Path.GetDirectoryName(_path)) + "/" + Path.GetFileName(_path)) + (_dirty ? " *" : "");

        // ------------------------------------------------------------ 列表
        private static string Label(EventEntry e) => (e.code ? "[C#] " : "") + e.id + "  " + (e.code ? "" : e.title);

        private void Reload(EventEntry select)
        {
            _loading = true;
            string f = _filter.Text.Trim();
            _view.Clear(); _list.BeginUpdate(); _list.Items.Clear();
            foreach (var e in _file.events)
            {
                if (f.Length > 0 && (e.id ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0 && (e.title ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
                _view.Add(e); _list.Items.Add(Label(e));
            }
            _list.EndUpdate();
            int idx = select == null ? -1 : _view.IndexOf(select);
            if (idx < 0 && _view.Count > 0) { idx = 0; select = _view[0]; }
            if (idx >= 0) _list.SelectedIndex = idx;
            _loading = false;
            Select(idx >= 0 ? _view[idx] : null);
        }

        private void RefreshRow()
        {
            int i = _view.IndexOf(_cur); if (i < 0) return;
            bool was = _loading; _loading = true; _list.Items[i] = Label(_cur); _loading = was;
        }

        private void Select(EventEntry e)
        {
            _cur = e; _loading = true;
            bool has = e != null;
            _id.Enabled = has; _minDay.Enabled = _maxDay.Enabled = _cooldown.Enabled = _weight.Enabled = _chance.Enabled = has;
            if (has)
            {
                _id.Text = e.id ?? ""; _title.Text = e.title ?? ""; _body.Text = (e.body ?? "").Replace("\n", "\r\n"); _group.Text = e.group ?? "";
                _minDay.Text = Opt(e.minDay); _maxDay.Text = Opt(e.maxDay); _cooldown.Text = Opt(e.cooldownDays); _weight.Text = Opt(e.weight); _chance.Text = e.triggerChance < 0 ? "" : e.triggerChance.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _when.Bind(e.when); _whenAny.Bind(e.whenAny); _choices.Bind(e.choices);
                _kind.Text = e.code ? "这是 C# 实现的事件：只能改右边的时间/权重/概率数字" : "";
            }
            foreach (var c in _codeLocked) c.Enabled = has && !e.code;
            _loading = false;
        }

        private static string Opt(int v) => v < 0 ? "" : v.ToString();

        private void Edit(Action change)
        {
            if (_loading || _cur == null) return;
            change?.Invoke();
            _dirty = true; UpdateTitle(); Revalidate();
        }

        private void ReadNumbers()
        {
            _cur.minDay = ParseInt(_minDay); _cur.maxDay = ParseInt(_maxDay); _cur.cooldownDays = ParseInt(_cooldown); _cur.weight = ParseInt(_weight);
            string s = _chance.Text.Trim(); double d = -1;
            bool ok = s.Length == 0 || (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) && d >= 0 && d <= 1);
            _chance.BackColor = ok ? SystemColors.Window : Color.MistyRose; _cur.triggerChance = ok && s.Length > 0 ? d : -1;
        }

        private static int ParseInt(TextBox box)
        {
            string s = box.Text.Trim(); int v = -1;
            bool ok = s.Length == 0 || (int.TryParse(s, out v) && v >= 0);
            box.BackColor = ok ? SystemColors.Window : Color.MistyRose; return ok && s.Length > 0 ? v : -1;
        }

        // ------------------------------------------------------------ 增删排
        private void NewEvent()
        {
            string id = "new_event"; int n = 1; while (_file.events.Any(e => e.id == id)) id = "new_event_" + (++n);
            var e = new EventEntry { id = id, title = "新事件", body = "（正文）", minDay = 3, cooldownDays = 10, weight = 10 };
            e.choices.Add(new EventChoiceEntry { text = "确定", effects = { new EventOp { type = "text", text = "……" } } });
            e.choices.Add(new EventChoiceEntry { text = "不理会", effects = { new EventOp { type = "text", text = "你没放在心上。" } } });
            int at = _cur == null ? _file.events.Count : _file.events.IndexOf(_cur) + 1;
            _file.events.Insert(at, e); _filter.Text = ""; Reload(e); Touch();
        }

        private void CopyEvent()
        {
            if (_cur == null || _cur.code) return;
            var e = Factory.CloneEvent(_cur); string id = _cur.id + "_copy"; int n = 1; while (_file.events.Any(x => x.id == id)) id = _cur.id + "_copy" + (++n);
            e.id = id; _file.events.Insert(_file.events.IndexOf(_cur) + 1, e); Reload(e); Touch();
        }

        private void DeleteEvent()
        {
            if (_cur == null) return;
            if (_cur.code) { MessageBox.Show(this, "C# 实现的事件不能在这里删除（要删请改代码）。", "删除"); return; }
            if (MessageBox.Show(this, "删除事件 " + _cur.id + "？", "删除", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            int i = _file.events.IndexOf(_cur); _file.events.RemoveAt(i);
            Reload(_file.events.Count == 0 ? null : _file.events[Math.Min(i, _file.events.Count - 1)]); Touch();
        }

        private void MoveEvent(int d)
        {
            if (_cur == null) return;
            if (_filter.Text.Trim().Length > 0) { _status.Text = "筛选中不能调整顺序，请先清空筛选。"; return; }
            int i = _file.events.IndexOf(_cur), j = i + d; if (j < 0 || j >= _file.events.Count) return;
            _file.events.RemoveAt(i); _file.events.Insert(j, _cur); Reload(_cur); Touch();
        }

        private void Touch() { _dirty = true; UpdateTitle(); Revalidate(); }

        // ------------------------------------------------------------ 校验
        private List<string> ValidateAll() => EventScript.Validate(_file, _file.events.Where(e => e.code).Select(e => e.id).ToList());

        private void Revalidate()
        {
            var errors = ValidateAll();
            _problems.Items.Clear();
            if (errors.Count == 0) _problems.Items.Add("没有发现问题。");
            else foreach (string e in errors) _problems.Items.Add(e);
        }
    }
}