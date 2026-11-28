using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        /// <summary>
        /// 界面缺陷普查二期（只读）：小字号、低对比、过小点击区、按钮重叠、被屏幕边缘裁掉（多种画面比例）。
        /// 命令：Unity -batchmode -executeMethod PCShopSell.EditorTools.GameSceneBuilder.UiExtraAuditFromBatch
        /// 输出：docs/previews/ui-extra/extra.txt
        /// </summary>
        public static void UiExtraAuditFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/ui-extra"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            for (int i = 0; i < 3; i++) s.AdvanceClock(24 * 60);
            var ui = _game.Ui; var t = typeof(GameUi);
            var report = new StringBuilder();
            var sizes = new[] { (1920, 1080), (1920, 1200), (1600, 1200), (2560, 1080), (1280, 800) };
            var windows = new[]
            {
                "OpenBackpackPanel", "OpenEmployeesPanel", "OpenMyPanel", "OpenBusinessPanel", "OpenQuests", "OpenPhone", "OpenLedger",
                "OpenCityProcurement", "OpenCityProperties", "OpenBrandOverview", "OpenSchedulePanel", "OpenContactsPanel",
                "OpenReportWindow", "OpenRelationsPanel", "OpenCityWardrobe", "OpenCityClimate", "OpenBenchWindow", "OpenIndustryNews",
                "OpenCityModelCatalog", "OpenMotorWorkshop", "OpenRacingDestinations", "OpenPetOrders", "OpenProcurementBudget",
                "OpenQuarterlyExpo", "OpenExpoReception", "OpenCityResidence", "OpenSpecialBusinessPanel", "OpenMotorGarage",
                "OpenNoticeHistory", "OpenCityFinanceOverview", "OpenCityInvestments", "OpenCityStories", "OpenEconomicReport",
                "OpenFleetPanel", "OpenGoalsPanel", "OpenHomeWorkbench", "OpenDisplayWindow", "OpenRetailTypeChooser",
            };
            var seen = new HashSet<string>();
            var counts = new Dictionary<string, int>();

            void Prepare(int w, int h, string name)
            {
                if (_extraTarget != null) { _game.Cam.targetTexture = null; _extraTarget.Release(); }
                _extraTarget = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                _game.Cam.targetTexture = _extraTarget;
                foreach (var scaler in UnityEngine.Object.FindObjectsOfType<CanvasScaler>()) scaler.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                Canvas.ForceUpdateCanvases(); Frame(0.05f); _game.SnapCamera();
                _game.Cam.targetTexture = null; Shot(name, w, h); Shot(name, w, h);
                _game.Cam.targetTexture = _extraTarget;
                foreach (var scaler in UnityEngine.Object.FindObjectsOfType<CanvasScaler>()) scaler.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                Canvas.ForceUpdateCanvases(); Frame(0); Canvas.ForceUpdateCanvases();
            }

            void Scan(string ctx, int w, int h)
            {
                Prepare(w, h, Sanitize(ctx) + "-" + w + "x" + h);
                ExtraChecks($"{ctx}@{w}x{h}", w, h, ui, report, seen, counts);
            }

            void Call(string method, int w, int h)
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var m = t.GetMethod(method, flags, null, Type.EmptyTypes, null);
                object[] args = null;
                if (m == null)
                {
                    m = t.GetMethods(flags).FirstOrDefault(x => x.Name == method && x.GetParameters().All(p => p.IsOptional));
                    if (m != null) args = new object[m.GetParameters().Length];
                }
                if (m == null) return;
                ui.CloseAllWindows();
                try { m.Invoke(ui, args); Scan(method, w, h); }
                catch (Exception e) { Log("FAIL " + method + " :: " + (e.InnerException ?? e).Message); }
            }

            var shop = s.MyShops.FirstOrDefault();
            foreach (var (w, h) in sizes)
            {
                ui.CloseAllWindows(); s.Leave();
                Scan("HUD@Street", w, h);
                if (shop != null)
                {
                    Teleport(s, shop.Place.Id); Frame(0.1f); Scan("HUD@MyShop", w, h);
                    ui.OpenDock(); Scan("ShopDock", w, h);
                    ui.CloseAllWindows(); s.Leave();
                }
                if (w == 1920 && h == 1080 || w == 1920 && h == 1200 || w == 1280 && h == 800 || w == 2560 && h == 1080)
                    foreach (var name in windows) Call(name, w, h);
                ui.CloseAllWindows();
            }
            File.WriteAllText(Path.Combine(_outDir, "extra.txt"), report.ToString(), new UTF8Encoding(true));
            foreach (var kv in counts.OrderBy(k => k.Key)) Log($"EXTRA {kv.Key} = {kv.Value}");
            Log("UI EXTRA AUDIT COMPLETE");
        });

        private static RenderTexture _extraTarget;

        private static double Luminance(Color c)
        {
            static double Lin(float v) => v <= .03928f ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4);
            return .2126 * Lin(c.r) + .7152 * Lin(c.g) + .0722 * Lin(c.b);
        }

        private static double Contrast(Color a, Color b)
        {
            double l1 = Luminance(a), l2 = Luminance(b);
            return (Math.Max(l1, l2) + .05) / (Math.Min(l1, l2) + .05);
        }

        private static void ExtraChecks(string ctx, int sw, int sh, GameUi ui, StringBuilder report, HashSet<string> seen, Dictionary<string, int> counts)
        {
            void Add(string kind, string key, string line)
            {
                if (!seen.Add(kind + "|" + key)) return;
                counts[kind] = counts.TryGetValue(kind, out var n) ? n + 1 : 1;
                report.AppendLine($"[{kind}] {ctx} | {line}");
            }

            foreach (var canvas in ui.Canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                var root = canvas.rootCanvas.transform; float unit = root.lossyScale.x; if (unit <= 0) continue;
                var canvasRect = new Vector3[4]; ((RectTransform)root).GetWorldCorners(canvasRect);

                foreach (var text in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text)) continue;
                    if (text.GetComponentInParent<InputField>() != null) continue;
                    string plain = System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]+>", "").Replace('\n', ' ');
                    string path = PathOf(text.transform);
                    var gen = text.cachedTextGenerator;
                    int used = text.resizeTextForBestFit && gen != null && gen.fontSizeUsedForBestFit > 0 ? gen.fontSizeUsedForBestFit : text.fontSize;
                    // 小字号：实际使用字号，或富文本 <size=N>
                    int minRich = int.MaxValue;
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text.text, "<size=(\\d+)>"))
                        minRich = Math.Min(minRich, int.Parse(m.Groups[1].Value));
                    int smallest = Math.Min(used, minRich);
                    if (smallest < 15) Add("小字<15", path + plain, $"\"{Cut(plain)}\" | 字号{smallest} | {path}");
                    // 低对比：文字色 vs 底板色
                    var surface = SurfaceOfExtra(text);
                    if (surface != null)
                    {
                        var img = surface.GetComponent<Image>();
                        var bg = img.color; var ink = text.color;
                        if (bg.a > .5f && ink.a > .5f)
                        {
                            double ratio = Contrast(ink, bg);
                            bool dimmedButton = text.GetComponentInParent<Button>() is Button b && !b.IsInteractable();
                            if (ratio < 3.0 && !dimmedButton)
                                Add("低对比<3", path + plain, $"\"{Cut(plain)}\" | 对比度{ratio:0.0} 字#{ColorUtility.ToHtmlStringRGB(ink)} 底#{ColorUtility.ToHtmlStringRGB(bg)} | {path}");
                            else if (ratio < 2.0 && dimmedButton)
                                Add("不可点对比<2", path + plain, $"\"{Cut(plain)}\" | 对比度{ratio:0.0} | {path}");
                        }
                    }
                    // 被屏幕/画布边缘裁掉
                    var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                    if (corners[0].x < canvasRect[0].x - 1 || corners[2].x > canvasRect[2].x + 1 || corners[0].y < canvasRect[0].y - 1 || corners[2].y > canvasRect[2].y + 1)
                        Add("出画面", path + plain, $"\"{Cut(plain)}\" | {path}");
                    // 文字被竖向截断
                    if (text.verticalOverflow == VerticalWrapMode.Truncate && gen != null)
                    {
                        float h = text.rectTransform.rect.height;
                        if (gen.lineCount > 0 && text.preferredHeight > h + 2)
                            Add("文字被截断", path + plain, $"\"{Cut(plain)}\" | 需要高{text.preferredHeight:0} 实际{h:0} | {path}");
                    }
                }

                var buttons = canvas.GetComponentsInChildren<Button>(false).Where(b => b.IsActive() && b.IsInteractable() && b.targetGraphic != null).ToArray();
                var rects = new List<(Button b, Rect r)>();
                foreach (var b in buttons)
                {
                    var rt = (RectTransform)b.transform; var c = new Vector3[4]; rt.GetWorldCorners(c);
                    var r = Rect.MinMaxRect(c[0].x / unit, c[0].y / unit, c[2].x / unit, c[2].y / unit);
                    string caption = Cut(b.GetComponentInChildren<Text>(true)?.text?.Replace('\n', ' ') ?? "");
                    if ((r.width < 40 || r.height < 32) && r.width > 1)
                        Add("点击区过小", PathOf(b.transform) + caption, $"\"{caption}\" | {r.width:0}x{r.height:0} | {PathOf(b.transform)}");
                    if (c[0].x < canvasRect[0].x - 1 || c[2].x > canvasRect[2].x + 1 || c[0].y < canvasRect[0].y - 1 || c[2].y > canvasRect[2].y + 1)
                        Add("按钮出画面", PathOf(b.transform) + caption, $"\"{caption}\" | {PathOf(b.transform)}");
                    rects.Add((b, r));
                }
                for (int i = 0; i < rects.Count; i++)
                    for (int j = i + 1; j < rects.Count; j++)
                    {
                        var a = rects[i]; var b = rects[j];
                        if (a.b.transform.IsChildOf(b.b.transform) || b.b.transform.IsChildOf(a.b.transform)) continue;
                        float ox = Mathf.Min(a.r.xMax, b.r.xMax) - Mathf.Max(a.r.xMin, b.r.xMin);
                        float oy = Mathf.Min(a.r.yMax, b.r.yMax) - Mathf.Max(a.r.yMin, b.r.yMin);
                        if (ox <= 2 || oy <= 2) continue;
                        float overlap = ox * oy / Mathf.Min(a.r.width * a.r.height, b.r.width * b.r.height);
                        if (overlap < .15f) continue;
                        string ca = Cut(a.b.GetComponentInChildren<Text>(true)?.text?.Replace('\n', ' ') ?? ""), cb = Cut(b.b.GetComponentInChildren<Text>(true)?.text?.Replace('\n', ' ') ?? "");
                        Add("按钮重叠", PathOf(a.b.transform) + ca + PathOf(b.b.transform) + cb,
                            $"\"{ca}\" 与 \"{cb}\" 重叠{overlap * 100:0}% | {PathOf(a.b.transform, 3)} ↔ {PathOf(b.b.transform, 3)}");
                    }
            }
        }

        private static string Cut(string value) => value.Length <= 24 ? value : value.Substring(0, 24) + "…";

        private static RectTransform SurfaceOfExtra(Text text)
        {
            var button = text.GetComponentInParent<Button>();
            if (button != null && button.image != null) return button.image.rectTransform;
            int depth = 0;
            for (var cur = text.transform.parent; cur != null && depth < 4; cur = cur.parent, depth++)
            {
                var image = cur.GetComponent<Image>();
                if (image == null || !image.enabled || image.color.a < .5f) continue;
                if (cur.GetComponent<Mask>() != null || cur.GetComponent<RectMask2D>() != null) continue;
                return (RectTransform)cur;
            }
            return null;
        }
    }
}
