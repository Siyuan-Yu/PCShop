// 复制到隔离工程 Assets/Tests/EditMode。先 capture-baseline，后 capture-after，复用完全相同的初始 JSON。
using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;
namespace PCShopSell.Tests
{
    public class SimulationPerformanceCapture
    {
        [TestCase("sleep")][TestCase("faint")][TestCase("hospital")]
        public void SameInitialSaveMustProduceIdenticalFinalSave(string scenario)
        {
            var timings = new System.Collections.Generic.Dictionary<string, (int count, double total, double max)>();
            RuntimePerformanceProbe.Listener = (name, ms) => { timings.TryGetValue(name, out var t); timings[name] = (t.count + 1, t.total + ms, Math.Max(t.max, ms)); };
            try {
            string output=Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS");
            string fixtures=Path.Combine(Path.GetDirectoryName(output),"state-fixtures");Directory.CreateDirectory(fixtures);
            bool baseline=output.Contains("capture-baseline");
            string input=Path.Combine(fixtures,scenario+"-initial.json"),expected=Path.Combine(fixtures,scenario+"-expected.json");
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            if(baseline)
            {
                using(var seed=new GameSession(city,7,Money.FromYuan(5000),GameTimestamp.From(new GameDate(1),511),true,false,false))
                {
                    seed.EventsEnabled=false;seed.DebugPrepareDistrictPrerequisites(1);
                    if(scenario=="faint")seed.Player.Energy=0;
                    if(scenario=="hospital"){seed.Player.Hunger=0;seed.Player.Health=14;}
                    File.WriteAllText(input,JsonUtility.ToJson(seed.CreateSave()));
                }
            }
            using(var s=GameSession.Load(city,JsonUtility.FromJson<SaveData>(File.ReadAllText(input))))
            {
                s.EventsEnabled=false;int gc0=GC.CollectionCount(0);long bytes=GC.GetAllocatedBytesForCurrentThread();
                var watch=Stopwatch.StartNew();
                if(scenario=="sleep")Assert.True(s.SleepUntil(480));else s.AdvanceClock(scenario=="faint"?240:1);
                double elapsed=watch.Elapsed.TotalMilliseconds;long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
                string state=JsonUtility.ToJson(s.CreateSave());
                if(baseline)File.WriteAllText(expected,state);else Assert.AreEqual(File.ReadAllText(expected),state,"全部持久化状态必须与优化前一致");
                string record=$"{scenario}\tms={elapsed:0.00}\tallocated_bytes={(allocated==0?-1:allocated)}\tgen0={GC.CollectionCount(0)-gc0}\tpeople={s.CityPeople.Count}\tday={s.Clock.Today.TotalDays}\tminute={s.Clock.MinuteOfDay}";
                File.AppendAllText(Path.Combine(output,"simulation-performance.tsv"),record+"\n");UnityEngine.Debug.Log(record);
            }
            foreach (var timing in timings)
                File.AppendAllText(Path.Combine(output,"core-timings.tsv"), $"{scenario}\t{timing.Key}\t{timing.Value.count}\t{timing.Value.total:0.000}\t{timing.Value.max:0.000}\n");
            } finally { RuntimePerformanceProbe.Listener = null; }
        }
    }
}
