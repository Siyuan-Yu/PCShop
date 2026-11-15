using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 按键机可打开通讯录打电话，智能手机还可使用日历和下载应用。
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
                ? OpenWindow("手机", 440, 850, BuildSmartphone, scrollContent:false)
                : OpenWindow("按键手机", 380, 620, BuildFeaturePhone);
            if(S.HasSmartphone)
            {
                _phoneWindow.TitleText.rectTransform.offsetMax=new Vector2(-110,_phoneWindow.TitleText.rectTransform.offsetMax.y);
                _phoneWindow.TimeStatusText.gameObject.SetActive(false);
            }
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
            Ui.Size(screen, -1, 176);
            Ui.VList(screen.rectTransform, 6, 10);
            Color ink = new Color(0.08f, 0.16f, 0.08f);
            Ui.Size(Ui.Label(screen.transform, $"信号 ||||　　{GameSession.ClockText(S.Clock.MinuteOfDay)}", 17, TextAnchor.MiddleLeft, ink), -1, 28);
            Color lcd = new Color(0.22f, 0.33f, 0.20f);
            Ui.Size(Ui.Button(screen.transform, "1  通讯录", () => OpenContactsPanel(w), 20, lcd), -1, 54);
            Ui.Size(Ui.Button(screen.transform, "3  上网（需智能手机）", null, 18, lcd, false), -1, 54);

            Color key = new Color(0.30f, 0.31f, 0.34f);
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#" };
            for (int r = 0; r < 4; r++)
            {
                RectTransform row = Ui.Row(w.Content, 54, 10);
                for (int c = 0; c < 3; c++)
                {
                    string k = keys[r * 3 + c];
                    int num = k.Length == 1 && char.IsDigit(k[0]) ? k[0] - '0' : -1;
                    Ui.Size(Ui.Button(row, k, num == 1 ? (System.Action)(() => OpenContactsPanel(w)) : null, 24, key, num == 1), 100, -1, 1);
                }
            }

            Ui.Size(Ui.Label(w.Content, "从通讯录联系熟人。网购和下载应用需要智能手机。", 15, TextAnchor.UpperLeft, Ui.Dim), -1, 70);
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
                case PhoneApps.Loans: return new Color(0.82f, 0.59f, 0.19f);
                case PhoneApps.Live: return new Color(0.72f, 0.46f, 0.19f);
                case PhoneApps.Securities: return new Color(.58f,.34f,.28f);
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
                case PhoneApps.Loans: return "借";
                case PhoneApps.Live: return "播";
                case PhoneApps.Securities: return "券";
                default: return "?";
            }
        }

        private void BuildSmartphone(Window w)
        {
            var layout=w.Content.GetComponent<VerticalLayoutGroup>();
            layout.padding=new RectOffset(12,12,8,8);layout.spacing=6;
            RectTransform bar = Ui.Row(w.Content, 28);
            Ui.Size(Ui.Label(bar, GameSession.ClockText(S.Clock.MinuteOfDay), 19, TextAnchor.MiddleLeft), 120);
            Ui.Size(Ui.Label(bar, "5G　电量 100%", 16, TextAnchor.MiddleRight, Ui.Dim), -1, -1, 1);

            if (_phoneScreen == "store")BuildPhoneStore(w);
            else BuildPhoneDesktop(w);
            BuildPhoneSystemNavigation(w);
        }

        private void BuildPhoneDesktop(Window w)
        {

            Image wall = Ui.Panel(w.Content, "Wallpaper", new Color(0.10f, 0.20f, 0.36f));
            Ui.Size(wall).GetComponent<LayoutElement>().flexibleHeight=1;
            Ui.VList(wall.rectTransform, 14, 14);
            var apps = PhoneApps.All.Where(a => S.HasApp(a.Id)).ToList();
            for (int i = 0; i < apps.Count; i += 4)
            {
                RectTransform row = Ui.Row(wall.transform, 104, 10);
                for (int j = i; j < apps.Count && j < i + 4; j++)
                {
                    PhoneAppDef a = apps[j];
                    Button b = Ui.Button(row, $"<size=34><b>{AppGlyph(a.Id)}</b></size>\n<size=13>{a.Name}</size>", () => LaunchApp(w, a.Id), 13, AppColor(a.Id));
                    b.name = "PhoneApp_" + a.Id;
                    Ui.Size(b, 84, 100);
                    if(a.Id==PhoneApps.Securities)BuildStockAppIcon(b,true);
                }
            }
            Ui.Size(Ui.Label(wall.transform, PhoneApps.All.All(a => S.HasApp(a.Id))
                ? "" : "更多 app 在「应用商店」", 15, TextAnchor.MiddleCenter, Ui.Dim), -1, 36);
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
            switch (id)
            {
                case PhoneApps.Contacts: OpenContactsPanel(w); break;
                case PhoneApps.Schedule: OpenSchedulePanel(); break;
                case PhoneApps.Shop: OpenOnlinePanel(); break;
                case PhoneApps.Loans: OpenLoansApp(); break;
                case PhoneApps.Live: w.Close(); OpenKolLobby(); break;
                case PhoneApps.Securities: w.Close(); OpenCityInvestments(); break;
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

            Ui.Size(Ui.Label(w.Content, "免费下载", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 26);
            RectTransform list = PhoneFlexibleScroll(w.Content,"PhoneStoreList");
            foreach (PhoneAppDef def in PhoneApps.All.Where(a => a.Id != PhoneApps.Store))
            {
                PhoneAppDef a = def;
                bool have = S.HasApp(a.Id);
                RectTransform row = Ui.Row(list, 84, 8);
                var icon=Ui.Size(Ui.Button(row, $"<size=28><b>{AppGlyph(a.Id)}</b></size>", null, 22, AppColor(a.Id), false), 64, 64);
                if(a.Id==PhoneApps.Securities)BuildStockAppIcon(icon,false);
                Ui.Size(Ui.Label(row, $"<b>{a.Name}</b>\n<size=14><color=#ABB5A8>{a.Description}</color></size>", 18), 200, -1, 1);
                Ui.Size(Ui.Button(row, have ? "打开" : "下载", () =>
                {
                    if(have)LaunchApp(w,a.Id);
                    else if(S.InstallApp(a.Id))w.Rebuild();
                }, 16, have ? Ui.ButtonBg : Ui.ButtonHot), 70).name = "PhoneDownload_" + a.Id;
            }
        }

        private static RectTransform PhoneFlexibleScroll(Transform parent,string name)
        {
            var content=Ui.Scroll(parent);
            var frame=(RectTransform)content.parent.parent;frame.name=name;
            var size=Ui.Size(frame).GetComponent<LayoutElement>();size.minHeight=0;size.preferredHeight=0;size.flexibleHeight=1;
            var layout=content.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(6,6,6,6);layout.spacing=6;
            return content;
        }
        private static void BuildStockAppIcon(Button button,bool desktop)
        {
            var text=button.GetComponentInChildren<Text>();text.text=desktop?"证券":"";text.fontSize=13;text.alignment=TextAnchor.LowerCenter;
            var icon=Ui.Rect(button.transform,"SecuritiesChartIcon").Anchor(.2f,desktop?.36f:.2f,.8f,desktop?.84f:.8f);
            float[] heights={.32f,.56f,.85f};
            for(int i=0;i<heights.Length;i++)
            {
                var bar=Ui.Panel(icon,"StockAppBar",Ui.TextColor);bar.raycastTarget=false;
                bar.rectTransform.Anchor(i*.34f,.03f,i*.34f+.24f,heights[i]);
            }
        }
        private void BuildPhoneSystemNavigation(Window w)
        {
            var row=Ui.Row(w.Content,36,6);row.name="PhoneSystemNavigation";
            Ui.Size(Ui.Button(row,"‹ 返回",()=>
            {
                _phoneScreen="home";w.ResetScrollOnRebuild=true;w.Rebuild();
            },15),-1,36,1);
            Ui.Size(Ui.Button(row,"桌面",()=>{_phoneScreen="home";w.ResetScrollOnRebuild=true;w.Rebuild();},15),-1,36,1);
            Ui.Size(Ui.Button(row,"收起",w.Close,15),-1,36,1);
        }

        // ---------- 通讯录 ----------

        /// <summary>通讯录：查看人物资料或打开聊天。</summary>
        public void OpenContactsPanel() => OpenContactsPanel(null);

        private void OpenContactsPanel(Window phone)
        {
            OpenPeopleWorkspace("contacts", w =>
            {
                RectTransform driver = Ui.Row(w.Content, 52);
                PortraitThumb(driver, "truck_driver", 44, null);
                Ui.Size(Ui.Label(driver, $"<b>{GameSession.TruckDriverName}</b> · 同城送货", 17), -1, -1, 1);
                var call = Ui.Button(driver, "联系司机", () =>
                {
                    w.Close();
                    OpenTruckWindow();
                }, 15, Ui.ButtonHot);
                call.name = "CallTruck";
                Ui.Size(call, 150);
                var met = S.SavedConversationContacts().ToList();
                if (met.Count == 0)
                {
                    Line(w.Content, "还没有联系人 · 熟悉度达到 30 后可当面交换号码", 17, Ui.Dim);
                    return;
                }
                RectTransform list = Ui.Scroll(w.Content, 440);
                foreach (ContactInfo def in met)
                {
                    ContactInfo n = def;
                    int rel = S.Relation(n.Id);
                    RectTransform row = Ui.Row(list, 48);
                    PortraitThumb(row, n.Id, 44, () => OpenCityPersonProfile(n.Id));
                    Ui.Size(Ui.Label(row, $"<b>{n.Name}</b>  {n.Role}", 17), 260);
                    Ui.Size(Ui.Label(row, $"{Npcs.LevelName(rel)} {rel}", 16, TextAnchor.MiddleLeft, Ui.Good), 100);
                    Ui.Size(Ui.Button(row,"人物资料",()=>OpenCityPersonProfile(n.Id),15),100);
                    Ui.Size(Ui.Button(row, "发消息", () =>
                        OpenPersonDialogue(n.Id, parent: w, remote: true), 15, Ui.ButtonHot), 100);
                }
            }, phone);
        }

    }
}
