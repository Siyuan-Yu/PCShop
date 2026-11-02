using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private sealed class NavigationPage
        {
            public string Id, Title;
            public Action<Window> Build;
            public List<Vector2> Scroll;
            public float Width, Height;
        }
        private sealed class NavigationWindow
        {
            public Window Window;
            public NavigationPage Page;
            public readonly List<NavigationPage> Back = new List<NavigationPage>();
        }
        private readonly Dictionary<string, NavigationWindow> _navigation = new Dictionary<string, NavigationWindow>();
        private readonly Dictionary<string, double> _multiplierDrafts = new Dictionary<string, double>();

        private void PriceMultiplierEditor(Transform parent, string key, string title, double current, double minimum, double maximum, double step, Action<double> apply, Action refresh, string draftKey = null)
        {
            draftKey = draftKey ?? key;
            double draft = _multiplierDrafts.TryGetValue(draftKey, out var previous) ? previous : current;
            draft = Math.Max(minimum, Math.Min(maximum, draft));
            var label = Line(parent, "", 17, Ui.Good); Button save = null;
            void Preview()
            {
                bool changed = Math.Abs(draft - current) > .001;
                label.text = title + $" {draft:0.00}×" + (changed ? " · 待确认" : " · 已保存");
                if (save != null) save.interactable = changed;
            }
            Ui.Slider(parent, (float)minimum, (float)maximum, (float)draft, value =>
            { draft = Math.Max(minimum, Math.Min(maximum, Math.Round(value / step) * step)); _multiplierDrafts[draftKey] = draft; Preview(); }).name = key;
            var row = Ui.Row(parent, 36, 6);
            save = Ui.Size(Ui.Button(row, "确认" + title, () => { apply(draft); _multiplierDrafts.Remove(draftKey); refresh(); }, 17, Ui.ButtonHot, false), -1, 36, 1);
            save.name = "Save_" + key;
            Ui.Size(Ui.Button(row, "还原", () => { _multiplierDrafts.Remove(draftKey); refresh(); }, 16), 90, 36);
            Preview();
        }

        // Primary screens reuse one window. Detail dialogs keep their parent and its draft.
        private Window OpenWorkspace(string group, string page, string title, float width, float height, Action<Window> build)
        {
            if (!_navigation.TryGetValue(group, out var state) || state.Window == null || state.Window.Closed)
            {
                state = new NavigationWindow(); _navigation[group] = state;
            }
            bool changing = state.Page != null && state.Page.Id != page;
            if (changing)
            {
                state.Page.Scroll = Ui.SaveScroll(state.Window.Root);
                int previous = state.Back.FindIndex(p => p.Id == page);
                if (previous >= 0) state.Back.RemoveRange(previous, state.Back.Count - previous);
                else { state.Back.Add(state.Page); if (state.Back.Count > 12) state.Back.RemoveAt(0); }
            }
            state.Page = new NavigationPage { Id = page, Title = title, Build = build, Width = width, Height = height };
            void Render(Window w)
            {
                if (state.Back.Count > 0)
                    Ui.Size(Ui.Button(w.Content, "‹ 返回上一页", () => BackWorkspace(state), 16), -1, 30).name = "WorkspaceBack";
                if (group == "business") BuildBusinessNavigation(w, page);
                build(w);
            }
            if (state.Window == null || state.Window.Closed)
                state.Window = OpenWindow(title, width, height, Render);
            else
            {
                var w = state.Window; w.TitleText.text = title;
                ((RectTransform)w.Root.Find("Panel")).sizeDelta = new Vector2(width, height);
                w.Builder = Render; w.Root.SetAsLastSibling(); w.Rebuild(); w.Fitter.Fit();
                if (changing) { Canvas.ForceUpdateCanvases(); foreach (var scroll in w.Root.GetComponentsInChildren<ScrollRect>()) scroll.verticalNormalizedPosition = 1; }
            }
            state.Window.Root.name = "Workspace_" + group;
            return state.Window;
        }

        private void BackWorkspace(NavigationWindow state)
        {
            if (state.Back.Count == 0) return;
            var page = state.Back[state.Back.Count - 1]; state.Back.RemoveAt(state.Back.Count - 1);
            state.Page = page;
            var w = state.Window;
            w.TitleText.text = page.Title;
            ((RectTransform)w.Root.Find("Panel")).sizeDelta = new Vector2(page.Width, page.Height);
            w.Builder = win =>
            {
                if (state.Back.Count > 0) Ui.Size(Ui.Button(win.Content, "‹ 返回上一页", () => BackWorkspace(state), 16), -1, 30).name = "WorkspaceBack";
                if (w.Root.name == "Workspace_business") BuildBusinessNavigation(win, page.Id);
                page.Build(win);
            };
            w.Rebuild(); w.Fitter.Fit(); Ui.RestoreScroll(w.Root, w.Root, page.Scroll);
        }

        private void BuildBusinessNavigation(Window w, string selected)
        {
            var row = Ui.Row(w.Content, 38, 6);
            void Link(string id, string label, Action action)
            { Ui.Size(Ui.Button(row, label, action, 17, selected == id ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1).name = "BusinessNav_" + id; }
            Link("finance", "收支", OpenCityFinanceOverview);
            Link("procurement", "采购", OpenCityProcurement);
            Link("properties", "物业", OpenCityProperties);
            Link("investments", "投资", OpenCityInvestments);
            Link("services", "合同与合作", OpenBusinessServices);
        }

        private void HelpLink(Transform parent, string title, string text)
        {
            Ui.Size(Ui.Button(parent, "说明 · " + title, () => OpenWorkspace("help", title, title, 720, 580, w => Para(w.Content, text, 18)), 15), -1, 30).name = "Help_" + title;
        }

        private static void DraftQuantityHints(Transform parent)
        {
            foreach(var button in parent.GetComponentsInChildren<Button>())
                if(button.name.StartsWith("QuantityPlus_")||button.name.StartsWith("QuantityMinus_"))
                    button.Tip("点击或长按调整草稿，保存后生效。");
        }

        private Place PreferredAgency(Place property = null)
        {
            if (property != null) return S.AgencyFor(property);
            if (S.InAgency) return S.CurrentPlace;
            if (S.PlayerShopPlace != null) return S.AgencyFor(S.PlayerShopPlace);
            return S.City.OfKind(PlaceKind.Agency).OrderBy(p => { var a = S.DoorOf(p); return a.X * a.X + a.Y * a.Y; }).FirstOrDefault();
        }

        private void GoToPropertyAgency(Place property = null)
        {
            var agency = PreferredAgency(property);
            if (agency == null) { Toast("当前没有可前往的中介。"); return; }
            CloseModalWindows();
            if (S.CurrentPlace == agency) OpenStreetPlace(agency);
            else _ctrl.GoToPlace(agency);
        }
    }
}
