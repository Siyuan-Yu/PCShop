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
        /// 界面缺陷普查（只读，不改任何界面）：
        ///  1. 文字贴边：任一可见 Text 与其所在底板（按钮/卡片）的左右/上下留白不足，或文字溢出底板。
        ///  2. 强调色被降灰：按钮声明了 Ui.ButtonHot，但实际渲染色不是琥珀（被 ResolveButtonEmphasis 单赢家规则降成灰色）。
        /// 结果写到 docs/previews/ui-defects/defects.txt，并同时输出到 Editor 日志。
        /// 命令：Unity -batchmode -executeMethod PCShopSell.EditorTools.GameSceneBuilder.UiDefectAuditFromBatch
        /// </summary>
        public static void UiDefectAuditFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/ui-defects"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            for (int i = 0; i < 3; i++) s.AdvanceClock(24 * 60);
            var ui = _game.Ui; var t = typeof(GameUi);
            var report = new StringBuilder();
            int edgeTotal = 0, demotedTotal = 0;

            void Scan(string ctx)
            {
                Frame(0.05f); _game.SnapCamera();
                Shot("ctx-" + Sanitize(ctx), 1920, 1080); Shot("ctx-" + Sanitize(ctx), 1920, 1080);
                if (_scanTarget == null) _scanTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _game.Cam.targetTexture = _scanTarget;
                foreach (var scaler in UnityEngine.Object.FindObjectsOfType<CanvasScaler>()) scaler.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                Canvas.ForceUpdateCanvases(); Frame(0); Canvas.ForceUpdateCanvases();
                var (edge, demoted) = ScanCurrentUi(ctx, ui, report);
                edgeTotal += edge; demotedTotal += demoted;
                Log($"CTX {ctx}: edge={edge} demoted={demoted}");
            }

            void Call(string method)
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var m = t.GetMethod(method, flags, null, Type.EmptyTypes, null);
                object[] args = null;
                if (m == null)
                {
                    m = t.GetMethods(flags).FirstOrDefault(x => x.Name == method && x.GetParameters().All(p => p.IsOptional));
                    if (m != null) args = new object[m.GetParameters().Length];
                }
                if (m == null) { Log("NO METHOD " + method); return; }
                ui.CloseAllWindows();
                try { m.Invoke(ui, args); Scan(method); }
                catch (Exception e) { Log("FAIL " + method + " :: " + (e.InnerException ?? e).Message); }
            }

            var shop = s.MyShops.FirstOrDefault();
            if (shop != null)
            {
                Teleport(s, shop.Place.Id); Frame(0.1f); Scan("HUD@MyShop");
                var tabType = t.GetNestedType("ShopTab", BindingFlags.NonPublic | BindingFlags.Public);
                var select = t.GetMethod("SelectTab", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (tabType != null && select != null)
                    foreach (var tab in Enum.GetValues(tabType))
                    {
                        ui.OpenDock(); select.Invoke(ui, new[] { tab }); Scan("ShopDock/" + tab);
                    }
            }
            ui.CloseAllWindows(); s.Leave(); Scan("HUD@Street");
            foreach (var name in new[]
            {
                "OpenBackpackPanel", "OpenEmployeesPanel", "OpenMyPanel", "OpenBusinessPanel", "OpenQuests", "OpenPhone", "OpenLedger",
                "OpenCityProcurement", "OpenCityProperties", "OpenBrandOverview", "OpenSchedulePanel", "OpenContactsPanel",
                "OpenReportWindow", "OpenShopBuyers", "OpenRelationsPanel", "OpenCityPeople", "OpenCityWardrobe", "OpenCityClimate",
                "OpenBenchWindow", "OpenIndustryNews", "OpenCityModelCatalog", "OpenMotorWorkshop", "OpenRacingDestinations", "OpenPetOrders",
                "OpenProcurementBudget", "OpenQuarterlyExpo", "OpenExpoReception", "OpenCityResidence", "OpenSpecialBusinessPanel",
                "OpenStockFees", "OpenMotorGarage", "OpenNoticeHistory", "OpenCityFinanceOverview", "OpenCityInvestments",
                "OpenCityStories", "OpenEconomicReport", "OpenFleetPanel", "OpenGoalsPanel", "OpenHomeWorkbench",
                "OpenDisplayWindow", "OpenRetailTypeChooser",
            })
                Call(name);
            ui.CloseAllWindows();

            File.WriteAllText(Path.Combine(_outDir, "defects.txt"), report.ToString(), new UTF8Encoding(true));
            Log($"UI DEFECT AUDIT COMPLETE edge={edgeTotal} demoted={demotedTotal}");
        });

        private static RenderTexture _scanTarget;

        private static string Sanitize(string value) => new string(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        private static string PathOf(Transform tr, int depth = 5)
        {
            var parts = new List<string>();
            for (var cur = tr; cur != null && parts.Count < depth; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static (int edge, int demoted) ScanCurrentUi(string ctx, GameUi ui, StringBuilder report)
        {
            int edge = 0, demoted = 0, hot = 0, hotAmber = 0, scrollRows = 0;
            var flatType = typeof(Ui).GetNestedType("FlatStyledButton", BindingFlags.NonPublic);
            var fillField = flatType?.GetField("_requestedFill", BindingFlags.Instance | BindingFlags.NonPublic);
            var exclusiveField = flatType?.GetField("_exclusiveEmphasis", BindingFlags.Instance | BindingFlags.NonPublic);
            var seen = new HashSet<string>();
            foreach (var canvas in ui.Canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                float unit = canvas.rootCanvas.transform.lossyScale.x;
                if (unit <= 0) continue;
                if (canvas.name == "UI_Dock") { var cr = ((RectTransform)canvas.transform).rect; Log($"CANVAS {ctx} {canvas.name} {cr.width:0}x{cr.height:0} scale={canvas.scaleFactor:0.###}"); }

                if (flatType != null)
                    foreach (var button in canvas.GetComponentsInChildren<Button>(false))
                    {
                        if (!flatType.IsInstanceOfType(button) || !button.IsActive() || !button.IsInteractable()) continue;
                        var requested = (Color?)fillField.GetValue(button);
                        if (!requested.HasValue || requested.Value != Ui.ButtonHot) continue;
                        hot++;
                        if (button.image.color == Ui.ButtonHot) { hotAmber++; continue; }
                        string caption = button.GetComponentInChildren<Text>(true)?.text ?? "";
                        string key = ctx + "|D|" + PathOf(button.transform) + "|" + caption;
                        if (!seen.Add(key)) continue;
                        demoted++;
                        report.AppendLine($"[降灰] {ctx} | \"{caption.Replace('\n', ' ')}\" | {PathOf(button.transform)} | exclusive={exclusiveField.GetValue(button)}");
                    }

                foreach (var scroll in canvas.GetComponentsInChildren<ScrollRect>(false))
                {
                    if (scroll.content == null || scroll.viewport == null && scroll.GetComponent<RectTransform>() == null) continue;
                    var viewport = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                    float viewH = viewport.rect.height, contentH = scroll.content.rect.height;
                    if (viewH < 60) continue;
                    float over = contentH - viewH;
                    string key = ctx + "|S|" + PathOf(scroll.transform);
                    if (!seen.Add(key)) continue;
                    scrollRows++;
                    report.AppendLine($"[滚动{(over > 1 ? "/超一屏" : "")}] {ctx} | 视口高{viewH:0} 内容高{contentH:0} 超出{Mathf.Max(0, over):0} ({(viewH > 0 ? contentH / viewH : 0):0.00}屏) | {PathOf(scroll.transform)}");
                }

                foreach (var text in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text)) continue;
                    if (text.GetComponentInParent<InputField>() != null) continue;
                    var gen = text.cachedTextGenerator;
                    if (gen == null || gen.vertexCount < 4) continue;
                    var rt = text.rectTransform;
                    float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                    var verts = gen.verts;
                    int count = Mathf.Min(gen.vertexCount, verts.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var p = verts[i].position / text.pixelsPerUnit;
                        minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                        minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                    }
                    if (minX > maxX) continue;
                    var surface = SurfaceOf(text);
                    if (surface == null) continue;
                    var corners = new Vector3[4]; surface.GetWorldCorners(corners);
                    var left = rt.TransformPoint(new Vector3(minX, minY)); var right = rt.TransformPoint(new Vector3(maxX, maxY));
                    float marginL = (left.x - corners[0].x) / unit, marginR = (corners[2].x - right.x) / unit;
                    float marginB = (left.y - corners[0].y) / unit, marginT = (corners[2].y - right.y) / unit;
                    float width = (corners[2].x - corners[0].x) / unit;
                    if (width < 24) continue;
                    bool overflow = marginL < 0 || marginR < 0 || marginB < -1 || marginT < -1;
                    bool tight = marginL < 8 || marginR < 8 || marginT < 3 || marginB < 3;
                    if (!overflow && !tight) continue;
                    string key = ctx + "|E|" + PathOf(text.transform) + "|" + text.text;
                    if (!seen.Add(key)) continue;
                    edge++;
                    report.AppendLine($"[贴边{(overflow ? "/溢出" : "")}] {ctx} | \"{text.text.Replace('\n', ' ')}\" | 字号{text.fontSize} | 留白 L{marginL:0} R{marginR:0} T{marginT:0} B{marginB:0} | 底板宽{width:0} | {PathOf(text.transform)}");
                }
            }
            Log($"HOT {ctx}: 声明琥珀且可点 {hot}，实际琥珀 {hotAmber}");
            return (edge, demoted);
        }

        private static RectTransform SurfaceOf(Text text)
        {
            var own = text.rectTransform;
            var button = text.GetComponentInParent<Button>();
            if (button != null && button.image != null) return button.image.rectTransform;
            int depth = 0;
            for (var cur = text.transform.parent; cur != null && depth < 4; cur = cur.parent, depth++)
            {
                var image = cur.GetComponent<Image>();
                if (image == null || !image.enabled || image.color.a < .3f) continue;
                if (cur.GetComponent<Mask>() != null || cur.GetComponent<RectMask2D>() != null) continue;
                var rect = (RectTransform)cur;
                if (rect.rect.width * rect.lossyScale.x + .5f < own.rect.width * own.lossyScale.x * .5f) continue;
                return rect;
            }
            return null;
        }
    }
}
