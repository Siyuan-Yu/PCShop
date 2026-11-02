using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PCShopSell.Core.Time;
using Unity.Profiling;
using UnityEngine;

namespace PCShopSell.Game
{
    public sealed partial class GameController
    {
        private static readonly ProfilerMarker SimulationMarker = new ProfilerMarker("PCShop.Simulation");
        private static readonly ProfilerMarker ViewsMarker = new ProfilerMarker("PCShop.Views");
        private static readonly ProfilerMarker UiMarker = new ProfilerMarker("PCShop.UI");
        // Explicit opt-in, deterministic fresh sessions; never load or save the player's game.
        private bool TryStartPerformanceBenchmark()
        {
            var args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-pcshop-benchmark");
            if (flag < 0 || flag + 1 >= args.Length) return false;
            Bootstrap(false, 7);
            enabled = false;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            // Desktop clicks must not change pause/speed while a benchmark window renders.
            foreach (var canvas in Ui.Canvases)
            {
                var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
            }
            StartCoroutine(PerformanceBenchmark(Path.GetFullPath(args[flag + 1])));
            return true;
        }

        [Serializable] private sealed class PerformanceReport
        {
            public string unity, gpu, resolution;
            public int renderers, places, people;
            public List<PerformanceScenario> scenarios = new List<PerformanceScenario>();
        }
        [Serializable] private sealed class PerformanceScenario
        {
            public string name;
            public int frames;
            public long simulatedMinutes;
            public double frameMs, p95Ms, p99Ms, simulationMs, viewsMs, uiMs, allocatedBytes, drawCalls, triangles;
        }

        private IEnumerator PerformanceBenchmark(string output)
        {
            var report = new PerformanceReport { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                resolution = Screen.width + "x" + Screen.height,
                renderers = FindObjectsOfType<Renderer>(true).Length, places = City.Places.Count(), people = Session.CityPeople.Count() };
            using (var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1))
            {
                foreach (string scenario in new[] { "street-paused", "street-1x", "street-8x", "shop-paused", "shop-8x", "home-paused" })
                {
                    StartSession(NewSession(7));
                    Ui.QuestPopups = false; Session.EventsEnabled = false;
                    Session.DebugAddCash(500000); Session.DebugRentStarterShop();
                    Session.Clock.Restore(new GameDate(0), 600);
                    Session.StopTrip(); Session.Leave();
                    var place = scenario.StartsWith("home") ? Session.Home : Session.MainShop.Place;
                    Session.ArriveAtNode(place.NodeId);
                    if (!scenario.StartsWith("street")) { Session.Enter(place); Ui.OnEnteredPlace(place); }
                    Session.Time.SetSpeed(scenario.EndsWith("8x") ? 8 : 1);
                    Session.Time.SetPaused(scenario.EndsWith("paused"));
                    for (int i = 0; i < 90; i++) { Step(1f / 60); yield return null; }
                    GC.Collect();
                    var result = new PerformanceScenario { name = scenario, frames = 360 };
                    long startMinute = Session.Clock.Now.TotalMinutes;
                    var frameTimes = new double[result.frames];
                    for (int i = 0; i < result.frames; i++)
                    {
                        long bytes = GC.GetAllocatedBytesForCurrentThread();
                        long start = Stopwatch.GetTimestamp();
                        Session.Tick(1.0 / 60);
                        long tick = Stopwatch.GetTimestamp();
                        SyncViews(1f / 60);
                        long view = Stopwatch.GetTimestamp();
                        Ui.Update(1f / 60);
                        long ui = Stopwatch.GetTimestamp();
                        result.allocatedBytes += GC.GetAllocatedBytesForCurrentThread() - bytes;
                        double ms = 1000.0 / Stopwatch.Frequency;
                        result.simulationMs += (tick - start) * ms;
                        result.viewsMs += (view - tick) * ms;
                        result.uiMs += (ui - view) * ms;
                        yield return null;
                        frameTimes[i] = (Stopwatch.GetTimestamp() - start) * ms;
                        result.drawCalls += draws.LastValue;
                        result.triangles += triangles.LastValue;
                    }
                    result.frameMs = frameTimes.Average(); Array.Sort(frameTimes);
                    result.simulatedMinutes = Session.Clock.Now.TotalMinutes - startMinute;
                    result.p95Ms = frameTimes[(int)(result.frames * .95)]; result.p99Ms = frameTimes[(int)(result.frames * .99)];
                    result.simulationMs /= result.frames; result.viewsMs /= result.frames; result.uiMs /= result.frames;
                    result.allocatedBytes /= result.frames; result.drawCalls /= result.frames; result.triangles /= result.frames;
                    // Some Unity Mono players return zero for this .NET counter even
                    // when allocating. Report unsupported, never claim zero garbage.
                    if (result.allocatedBytes == 0) result.allocatedBytes = -1;
                    report.scenarios.Add(result);
                    UnityEngine.Debug.Log("PERFORMANCE " + JsonUtility.ToJson(result));
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            bool rendered = report.scenarios.All(s => s.drawCalls > 0);
            if (!rendered) UnityEngine.Debug.LogWarning("PERFORMANCE INVALID: rendering was suppressed; do not interpret these frame times as FPS.");
            Application.Quit(rendered ? 0 : 2);
        }
    }
}
