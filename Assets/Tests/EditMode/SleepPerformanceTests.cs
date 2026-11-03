using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class SleepPerformanceTests
    {
        [TestCase("sleep")] [TestCase("faint")] [TestCase("hospital")]
        public void SleepAndRecoveryRetainIdenticalSimulationWithCommuteEstimateCache(string scenario)
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            using(var seed=new GameSession(city,7,Money.FromYuan(5000),GameTimestamp.From(new GameDate(1),511),true,false,false))
            {
                seed.EventsEnabled=false;seed.DebugPrepareDistrictPrerequisites(1);
                if(scenario=="faint")seed.Player.Energy=0;
                if(scenario=="hospital"){seed.Player.Hunger=0;seed.Player.Health=14;}
                string initial=JsonUtility.ToJson(seed.CreateSave());
                using(var baseline=GameSession.Load(city,JsonUtility.FromJson<SaveData>(initial)))
                using(var optimized=GameSession.Load(city,JsonUtility.FromJson<SaveData>(initial)))
                {
                    baseline.EventsEnabled=false;optimized.EventsEnabled=false;
                    // Clearing after every minute reproduces the old repeated route estimates.
                    var cache=(IDictionary)typeof(GameSession).GetField("_cityCommuteMinutes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(baseline);
                    baseline.Clock.TimeAdvanced+=(from,to)=>cache.Clear();
                    int recoveries=0;
                    optimized.Popup+=(title,body)=>{if(title=="你晕倒了"||title=="住院了")recoveries++;};
                    void Run(GameSession s)
                    {
                        if(scenario=="sleep")Assert.True(s.SleepUntil(480));
                        else s.AdvanceClock(scenario=="faint"?240:1);
                    }
                    var watch=Stopwatch.StartNew();Run(baseline);
                    double before=watch.Elapsed.TotalMilliseconds;watch.Restart();
                    Run(optimized);double after=watch.Elapsed.TotalMilliseconds;
                    UnityEngine.Debug.Log($"RECOVERY_COMPARE {scenario} baseline_ms={before:0.00} optimized_ms={after:0.00}");
                    Assert.AreEqual(scenario=="faint"?1231:480,optimized.Clock.MinuteOfDay);
                    Assert.AreEqual(scenario=="faint"?1:scenario=="hospital"?3:2,optimized.Clock.Today.TotalDays);
                    Assert.AreEqual(scenario=="sleep"?0:1,recoveries);
                    if(scenario=="hospital")
                    {
                        var bills=optimized.Ledger.Entries.Where(e=>e.Category=="医疗").ToArray();
                        Assert.AreEqual(1,bills.Length);Assert.AreEqual(-400,bills[0].Amount.Yuan);
                        Assert.AreEqual(80,optimized.Player.Health);Assert.AreEqual(70,optimized.Player.Hunger);
                    }
                    Assert.AreEqual(JsonUtility.ToJson(baseline.CreateSave()),JsonUtility.ToJson(optimized.CreateSave()),"All persisted simulation outcomes must match, including people, businesses, needs and pending story invitation.");
                    Assert.IsNull(optimized.CurrentDistrictNode);
                    Assert.Greater(optimized.DistrictStoryState.InvitationDueAt,optimized.Clock.Now.TotalMinutes);
                }
            }
        }
    }
}
