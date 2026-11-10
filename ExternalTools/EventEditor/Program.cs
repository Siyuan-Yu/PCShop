using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PCShopSell.Core.Session;

namespace PCShopSell.EventEditor
{
    internal static class Program
    {
        public static string Locate(string given)
        {
            if (!string.IsNullOrEmpty(given)) return Path.GetFullPath(given);
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string p = Path.Combine(dir.FullName, "Assets", "Resources", "Data", "events.json");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        public static EventScriptFile Load(string path) => EventScriptReader.Read(File.ReadAllText(path));

        public static void Save(string path, EventScriptFile file) => File.WriteAllText(path, EventScriptWriter.Write(file), new System.Text.UTF8Encoding(false));

        // 把文件里每个条件 / 效果 / 选项都过一遍编辑框（只构造、不弹出），再按原样写回，确认和原来一模一样。
        private static int SelfTest(string path)
        {
            AttachConsole(-1); ApplicationConfiguration.Initialize(); NpcList.Load();
            var file = Load(path); string before = EventScriptWriter.Write(file); int n = 0;
            foreach (var e in file.events.Where(x => !x.code))
            {
                foreach (var c in e.when.Concat(e.whenAny).Concat(e.choices.SelectMany(x => x.requires)).Concat(e.choices.SelectMany(x => x.chanceBonus.SelectMany(b => b.when)))) { using (var d = new SpecDialog(c, true)) { d.CreateControl(); d.Apply(); n++; } }
                foreach (var o in e.choices.SelectMany(x => x.effects.Concat(x.onHit).Concat(x.onMiss))) { using (var d = new SpecDialog(o, false)) { d.CreateControl(); d.Apply(); n++; } }
                for (int i = 0; i < e.choices.Count; i++) { using (var d = new ChoiceDialog(e.choices[i])) { d.CreateControl(); d.Collect(); n++; } }
            }
            foreach (var s in EventScriptSchema.Conds) using (var d = new SpecDialog(new EventCond { type = s.Type }, true)) { d.CreateControl(); n++; }
            foreach (var s in EventScriptSchema.Ops) using (var d = new SpecDialog(new EventOp { type = s.Type }, false)) { d.CreateControl(); n++; }
            string after = EventScriptWriter.Write(file);
            Console.WriteLine(before == after ? "自检通过：" + n + " 个编辑框，数据原样不变。" : "自检失败：编辑框改变了数据。");
            return before == after ? 0 : 1;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);

        [STAThread]
        private static int Main(string[] args)
        {
            // 命令行：EventEditor.exe --validate [events.json] / --reformat [events.json]
            string cmd = args.FirstOrDefault(a => a.StartsWith("--"));
            string given = args.FirstOrDefault(a => !a.StartsWith("--"));
            if (cmd == "--validate" || cmd == "--reformat")
            {
                AttachConsole(-1);
                string path = Locate(given);
                if (path == null || !File.Exists(path)) { Console.Error.WriteLine("找不到 events.json"); return 2; }
                var file = Load(path);
                var errors = EventScript.Validate(file, file.events.Where(e => e.code).Select(e => e.id).ToList());
                foreach (string e in errors) Console.Error.WriteLine(e);
                if (cmd == "--reformat" && file.readErrors.Count == 0) { Save(path, file); Console.WriteLine("已按标准格式重写：" + path); }
                else Console.WriteLine(errors.Count == 0 ? "OK：" + file.events.Count + " 个事件，没有问题。" : errors.Count + " 个问题。");
                return errors.Count == 0 ? 0 : 1;
            }
            if (cmd == "--selftest") return SelfTest(Locate(given));
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(Locate(given)));
            return 0;
        }
    }
}