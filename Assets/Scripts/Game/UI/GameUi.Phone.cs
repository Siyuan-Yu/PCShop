using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 手机：顶栏只有一个“手机”按钮。没有智能手机打开的是按键机（只能打电话、发短信）；
    /// 买了智能手机打开的是主屏幕，要网购、日程、聊天、1788 都得先去应用商店下载 app。
    /// </summary>
    public sealed partial class GameUi
    {
        private string _phoneScreen = "home";
        private Window _phoneWindow;

        public void OpenPhone()
        {
            if (S == null) return;
            if (_phoneWindow != null && !_phoneWindow.Closed) return;
            _phoneScreen = "home";
            _phoneWindow = S.HasSmartphone
                ? OpenWindow("手机", 440, 800, BuildSmartphone)
                : OpenWindow("手机（按键机）", 380, 760, BuildFeaturePhone);
        }

        public void ClosePhone()
        {
            if (_phoneWindow != null && !_phoneWindow.Closed) _phoneWindow.Close();
        }

        public void OpenPhoneStore()
        {
            OpenPhone();
            if (_phoneWindow == null || _phoneWindow.Closed || !S.HasSmartphone) return;
            _phoneScreen = "store";
            _phoneWindow.Rebuild();
        }

        // ---------- 按键机 ----------

        private void BuildFeaturePhone(Window w)
        {
            Image screen = Ui.Panel(w.Content, "Screen", new Color(0.55f, 0.66f, 0.50f));
            Ui.Size(screen, -1, 300);
            Ui.VList(screen.rectTransform, 6, 10);
            Color ink = new Color(0.08f, 0.16f, 0.08f);
            Ui.Size(Ui.Label(screen.transform, $"信号 ||||　　{GameSession.ClockText(S.Clock.MinuteOfDay)}", 17, TextAnchor.MiddleLeft, ink), -1, 28);
            Color lcd = new Color(0.22f, 0.33f, 0.20f);
            Ui.Size(Ui.Button(screen.transform, "1  通讯录", () => LaunchFeatureApp(w, 1), 20, lcd), -1, 54);
            Ui.Size(Ui.Button(screen.transform, "2  短信", () => LaunchFeatureApp(w, 2), 20, lcd), -1, 54);
            Ui.Size(Ui.Button(screen.transform, "3  上网（按键机上不了网）", null, 18, lcd, false), -1, 54);

            Color key = new Color(0.30f, 0.31f, 0.34f);
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#" };
            for (int r = 0; r < 4; r++)
            {
                RectTransform row = Ui.Row(w.Content, 54, 10);
                for (int c = 0; c < 3; c++)
                {
                    string k = keys[r * 3 + c];
                    int num = k.Length == 1 && char.IsDigit(k[0]) ? k[0] - '0' : -1;
                    Ui.Size(Ui.Button(row, k, () =>
                    {
                        if (num == 1 || num == 2) LaunchFeatureApp(w, num);
                        else Toast("按键机上这个键没用，试试 1（通讯录）或 2（短信）。");
                    }, 24, key), 100, -1, 1);
                }
            }

            Ui.Size(Ui.Label(w.Content, "按键机只能打电话、发短信。现在大家都不习惯看短信，常常没人回。要网购、上 1788、下载 app，得去街上的手机店买一部智能手机。", 15, TextAnchor.UpperLeft, Ui.Dim), -1, 70);
        }

        private void LaunchFeatureApp(Window w, int n)
        {
            w.Close();
            if (n == 1) OpenContactsPanel();
            else OpenChatPanel();
        }

        // ---------- 智能手机 ----------

        private static Color AppColor(string id)
        {
            switch (id)
            {
                case PhoneApps.Store: return new Color(0.20f, 0.50f, 0.90f);
                case PhoneApps.Contacts: return new Color(0.25f, 0.65f, 0.35f);
                case PhoneApps.Schedule: return new Color(0.90f, 0.45f, 0.25f);
                case PhoneApps.Shop: return new Color(0.88f, 0.28f, 0.35f);
                case PhoneApps.Chat: return new Color(0.18f, 0.72f, 0.58f);
                case PhoneApps.Wholesale: return new Color(0.62f, 0.40f, 0.82f);
                default: return Ui.ButtonBg;
            }
        }

        private static string AppGlyph(string id)
        {
            switch (id)
            {
                case PhoneApps.Store: return "商";
                case PhoneApps.Contacts: return "话";
                case PhoneApps.Schedule: return "历";
                case PhoneApps.Shop: return "购";
                case PhoneApps.Chat: return "聊";
                case PhoneApps.Wholesale: return "批";
                default: return "?";
            }
        }

        private void BuildSmartphone(Window w)
        {
            RectTransform bar = Ui.Row(w.Content, 28);
            Ui.Size(Ui.Label(bar, GameSession.ClockText(S.Clock.MinuteOfDay), 19, TextAnchor.MiddleLeft), 120);
            Ui.Size(Ui.Label(bar, "5G　电量 100%", 16, TextAnchor.MiddleRight, Ui.Dim), -1, -1, 1);

            if (_phoneScreen == "store")
            {
                BuildPhoneStore(w);
                return;
            }

            Image wall = Ui.Panel(w.Content, "Wallpaper", new Color(0.10f, 0.20f, 0.36f));
            Ui.Size(wall, -1, 610);
            Ui.VList(wall.rectTransform, 14, 14);
            var apps = PhoneApps.All.Where(a => S.HasApp(a.Id)).ToList();
            for (int i = 0; i < apps.Count; i += 4)
            {
                RectTransform row = Ui.Row(wall.transform, 104, 10);
                for (int j = i; j < apps.Count && j < i + 4; j++)
                {
                    PhoneAppDef a = apps[j];
                    Button b = Ui.Button(row, $"<size=34><b>{AppGlyph(a.Id)}</b></size>\n<size=13>{a.Name}</size>", () => LaunchApp(w, a.Id), 13, AppColor(a.Id));
                    Ui.Size(b, 84, 100);
                }
            }
            Ui.Size(Ui.Label(wall.transform, S.HasApp(PhoneApps.Shop) && S.HasApp(PhoneApps.Chat) && S.HasApp(PhoneApps.Wholesale)
                ? "所有 app 都装好了。" : "想要更多功能，到「应用商店」里下载。", 15, TextAnchor.MiddleCenter, Ui.Dim), -1, 36);
        }

        private void LaunchApp(Window w, string id)
        {
            if (id == PhoneApps.Store)
            {
                _phoneScreen = "store";
                w.Rebuild();
                return;
            }
            string blocker = S.AppBlocker(id);
            if (blocker != null)
            {
                Toast(blocker);
                return;
            }
            w.Close();
            switch (id)
            {
                case PhoneApps.Contacts: OpenContactsPanel(); break;
                case PhoneApps.Schedule: OpenSchedulePanel(); break;
                case PhoneApps.Shop: OpenOnlinePanel(); break;
                case PhoneApps.Chat: OpenChatPanel(); break;
                case PhoneApps.Wholesale: OpenWholesaleApp(); break;
            }
        }

        private void BuildPhoneStore(Window w)
        {
            RectTransform top = Ui.Row(w.Content, 44);
            Ui.Size(Ui.Button(top, "‹ 返回", () =>
            {
                _phoneScreen = "home";
                w.Rebuild();
            }, 17, Ui.ButtonBg), 96);
            Ui.Size(Ui.Label(top, "应用商店", 22, TextAnchor.MiddleCenter), -1, -1, 1);

            Ui.Size(Ui.Label(w.Content, "下载都是免费的，装好后主屏幕上就有图标。", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 26);
            RectTransform list = Ui.Scroll(w.Content, 600);
            foreach (PhoneAppDef def in PhoneApps.All.Where(a => a.Id != PhoneApps.Store))
            {
                PhoneAppDef a = def;
                bool have = S.HasApp(a.Id);
                RectTransform row = Ui.Row(list, 84, 8);
                Ui.Size(Ui.Button(row, $"<size=28><b>{AppGlyph(a.Id)}</b></size>", null, 22, AppColor(a.Id), false), 64, 64);
                Ui.Size(Ui.Label(row, $"<b>{a.Name}</b>\n<size=14><color=#ABB5A8>{a.Description}</color></size>", 18), 200, -1, 1);
                Ui.Size(Ui.Button(row, have ? "已安装" : "下载", () =>
                {
                    S.InstallApp(a.Id);
                    w.Rebuild();
                }, 16, have ? Ui.ButtonBg : Ui.ButtonHot, !have), 80);
            }
        }

        // ---------- 通讯录 / 聊天 ----------

        /// <summary>通讯录：认识的人，打电话约出去。</summary>
        public void OpenContactsPanel()
        {
            OpenWindow("通讯录", 700, 620, w =>
            {
                RectTransform driver = Ui.Row(w.Content, 52);
                Ui.Size(Ui.Label(driver, $"<b>{GameSession.TruckDriverName}</b>  <size=15><color=#A8B0BA>开面包车，随叫随到，帮你把东西搬到家里或任意一家店</color></size>", 17), -1, -1, 1);
                var call = Ui.Button(driver, "打电话叫车", () =>
                {
                    w.Close();
                    OpenTruckWindow();
                }, 15, Ui.ButtonHot);
                call.name = "CallTruck";
                Ui.Size(call, 150);
                var met = Npcs.All.Where(n => S.IsMet(n.Id)).ToList();
                if (met.Count == 0)
                {
                    Line(w.Content, "别的号码还没有。认识了人才会存进来（留心店里的随机事件、去档口逛逛）。", 17, Ui.Dim);
                    return;
                }
                RectTransform list = Ui.Scroll(w.Content, 440);
                foreach (NpcDef def in met)
                {
                    NpcDef n = def;
                    int rel = S.Relation(n.Id);
                    RectTransform row = Ui.Row(list, 48);
                    Ui.Size(Ui.Label(row, $"<b>{n.Name}</b>  {n.Role}", 17), 260);
                    Ui.Size(Ui.Label(row, $"{Npcs.LevelName(rel)} {rel}", 16, TextAnchor.MiddleLeft, Ui.Good), 100);
                    Ui.Size(Ui.Button(row, "打电话约出去", () => OpenInviteWindow(n, w), 15, Ui.ButtonHot), 150);
                    Ui.Size(Ui.Button(row, "发消息", () =>
                    {
                        w.Close();
                        OpenChatPanel();
                    }, 15, Ui.ButtonBg), 90);
                }
                Line(w.Content, "送礼要当面送，在“目标”里或去档口。", 15, Ui.Dim);
            });
        }

        /// <summary>聊天 app：每天和每个人聊一次，涨一点交情。</summary>
        public void OpenChatPanel()
        {
            bool app = S.HasChatApp;
            OpenWindow(app ? "聊天" : "短信", 760, 600, w =>
            {
                Line(w.Content, app
                    ? $"每天和每个人聊一次，一定有回，交情 +{GameSession.ChatGain}（只涨到 {GameSession.ChatMaxRelation}）。再往上得当面送礼、约出去。"
                    : $"每天给每个人发一条短信。现在大家都不习惯看短信，只有大约 {GameSession.SmsReplyChance * 100:0}% 的把握有人回；回了交情 +{GameSession.ChatGain}（只涨到 {GameSession.ChatMaxRelation}）。装了聊天 app 一定有回。", 15, Ui.Dim);
                var met = Npcs.All.Where(n => S.IsMet(n.Id)).ToList();
                if (met.Count == 0)
                {
                    Line(w.Content, "还没有联系方式。认识了人才会存进来（留心店里的随机事件、去档口逛逛）。", 17, Ui.Dim);
                    return;
                }
                RectTransform list = Ui.Scroll(w.Content, 460);
                foreach (NpcDef def in met)
                {
                    NpcDef n = def;
                    int rel = S.Relation(n.Id);
                    RectTransform row = Ui.Row(list, 48);
                    Ui.Size(Ui.Label(row, $"<b>{n.Name}</b>  {n.Role}", 17), 250);
                    Ui.Size(Ui.Label(row, $"{Npcs.LevelName(rel)} {rel}", 16, TextAnchor.MiddleLeft, Ui.Good), 100);
                    Ui.Bar(row, rel / 100f, Ui.Accent, 100, 14);
                    string blocker = S.ChatBlocker(n.Id);
                    Ui.Size(Ui.Button(row, blocker ?? (app ? "聊两句" : "发短信"), () =>
                    {
                        S.Chat(n.Id);
                        w.Rebuild();
                    }, 15, Ui.ButtonHot, blocker == null), 130);
                }
            });
        }
    }
}
