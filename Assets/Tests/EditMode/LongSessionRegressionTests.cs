using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class LongSessionRegressionTests
    {
        [TestCase(914,365)] [TestCase(7,1095)]
        public void ResumeArchivedStressCheckpointToTarget(int seed,int target)
        {
            string source=Environment.GetEnvironmentVariable("PCSHOP_QA_RESUME_ROOT");
            if(string.IsNullOrEmpty(source))Assert.Ignore("仅在需要续跑的检查点目录已明确指定时执行。");
            string original=Path.Combine(source,$"ledger-{seed}-{target}-checkpoint.json");
            Assert.True(File.Exists(original),"必须使用实际已跑出的检查点");
            using var fixture=GameSessionTests.Make();
            var s=PCShopSell.Core.Session.GameSession.Load(fixture.City,JsonUtility.FromJson<PCShopSell.Core.Session.SaveData>(File.ReadAllText(original)));
            s.EventsEnabled=false;
            string output=Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS")??Path.GetTempPath();
            string prefix=Path.Combine(output,$"resume-{seed}-{target}");
            var timer=Stopwatch.StartNew();
            try
            {
                while(s.Clock.Today.TotalDays<target&&!s.IsGameOver)
                {
                    int day=s.Clock.Today.TotalDays;var random=new System.Random(seed+day);
                    if(day%7==0)s.TradeCityInvestment("rival_pc",random.Next(1,20),true,true);
                    if(day%7==3)s.TradeCompanyBlock("rival_pc","capital_qing",random.Next(1,20),false);
                    CompanyWellbeingRegressionTests.Advance(s,1440);CompanyWellbeingRegressionTests.Invariants(s);
                    if(s.Clock.Today.TotalDays%30==0||s.Clock.Today.TotalDays>=target)
                    {
                        var cash=s.Ledger.Balance;var worth=s.NetWorth();long at=s.Clock.Now.TotalMinutes;
                        var json=JsonUtility.ToJson(s.CreateSave());File.WriteAllText(prefix+"-checkpoint.json",json);
                        var next=CompanyWellbeingRegressionTests.Reload(s);
                        Assert.AreEqual(cash,next.Ledger.Balance);Assert.AreEqual(worth,next.NetWorth());Assert.AreEqual(at,next.Clock.Now.TotalMinutes);
                        s.Dispose();s=next;
                        File.AppendAllText(prefix+"-progress.txt",$"day={s.Clock.Today.TotalDays} seconds={timer.Elapsed.TotalSeconds:F2} bytes={System.Text.Encoding.UTF8.GetByteCount(json)} cash={cash.Cents}\n");
                    }
                    Assert.Less(timer.Elapsed.TotalSeconds,1800,"续跑超过三十分钟，保留检查点并报告");
                }
                Assert.False(s.IsGameOver,s.GameOverReason);Assert.GreaterOrEqual(s.Clock.Today.TotalDays,target);
                if(target>=1095)Assert.IsNotNull(s.EndingId);
                File.AppendAllText(prefix+"-progress.txt",$"FINAL target={target} reached={s.Clock.Today.TotalDays} ending={s.EndingId}\n");
            }
            finally{s.Dispose();}
        }
        // 小地图的长期账务/员工/证券压力测试，不冒充四区新档通关。
        [TestCase(7,30)] [TestCase(41,90)] [TestCase(914,365)] [TestCase(7,1095)]
        public void CompanyPayrollAndEquityLongHorizon(int seed,int days)
        {
            var s=CompanyWellbeingRegressionTests.Ready();var random=new System.Random(seed);var timer=Stopwatch.StartNew();
            string root=Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS")??Path.GetTempPath();
            string prefix=Path.Combine(root,$"ledger-{seed}-{days}");
            s.MainShop.Employee=new PCShopSell.Core.Session.Employee{Id="stress_employee",Name="压力测试员工",DailyWageYuan=120,ShiftStart=540,ShiftEnd=1020,Intelligence=70,Dexterity=70,Stamina=70,Charm=70};
            try
            {
                for(int day=0;day<days&&!s.IsGameOver;day++)
                {
                    if(day%7==0)s.TradeCityInvestment("rival_pc",random.Next(1,20),true,true);
                    if(day%7==3)s.TradeCompanyBlock("rival_pc","capital_qing",random.Next(1,20),false);
                    CompanyWellbeingRegressionTests.Advance(s,1440);
                    CompanyWellbeingRegressionTests.Invariants(s);
                    if(day%30==0||day==days-1)
                    {
                        var cash=s.Ledger.Balance;var worth=s.NetWorth();long at=s.Clock.Now.TotalMinutes;
                        string json=JsonUtility.ToJson(s.CreateSave());File.WriteAllText(prefix+"-checkpoint.json",json);
                        var loaded=CompanyWellbeingRegressionTests.Reload(s);
                        Assert.AreEqual(cash,loaded.Ledger.Balance);Assert.AreEqual(worth,loaded.NetWorth());Assert.AreEqual(at,loaded.Clock.Now.TotalMinutes);
                        s.Dispose();s=loaded;
                        File.AppendAllText(prefix+"-progress.txt",$"day={day+1} seconds={timer.Elapsed.TotalSeconds:F2} bytes={System.Text.Encoding.UTF8.GetByteCount(json)} entries={s.Ledger.Entries.Count} cash={cash.Cents}\n");
                    }
                    Assert.Less(timer.Elapsed.TotalSeconds,360,"单个长期压力用例超过六分钟");
                }
                File.AppendAllText(prefix+"-progress.txt",$"FINAL requested={days} reached={s.Clock.Today.TotalDays} gameOver={s.IsGameOver} reason={s.GameOverReason}\n");
                Assert.False(s.IsGameOver,"充足现金和受控健康的账务压力场景不应意外终止："+s.GameOverReason);
                Assert.GreaterOrEqual(s.Clock.Today.TotalDays,days);
                if(days>=1095)Assert.IsNotNull(s.EndingId,"跑到三年必须产生结局");
            }
            finally{s.Dispose();}
        }
        // 旧经济机器人包含直接购置升级的简化；本组验证长期稳定性，不声称替代新档主线实机通关。
        [TestCase(7,7,false,false)] [TestCase(41,7,true,false)] [TestCase(914,7,true,true)]
        [TestCase(7,30,false,false)] [TestCase(41,30,true,false)] [TestCase(914,30,true,true)]
        [TestCase(7,90,false,false)] [TestCase(41,90,true,false)] [TestCase(914,90,true,true)]
        [TestCase(7,365,false,false)] [TestCase(41,365,true,false)] [TestCase(914,365,true,true)]
        [TestCase(7,1095,false,false)] [TestCase(41,1095,true,false)] [TestCase(914,1095,true,true)]
        public void EconomySurvivesBoundedDaysAndRepeatedReload(int seed,int days,bool events,bool lazy)
        {
            var bot=new EconomyBot((ulong)seed,events,lazy);var timer=Stopwatch.StartNew();
            string root=Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS")??Path.GetTempPath();
            string prefix=Path.Combine(root,$"economy-{seed}-{days}-{events}-{lazy}");
            try
            {
                for(int day=0;day<days&&!bot.Session.IsGameOver;day++)
                {
                    int before=bot.Session.Clock.Today.TotalDays;
                    bot.Run(1);
                    Assert.True(bot.Session.IsGameOver||bot.Session.Clock.Today.TotalDays>before,"一天结束后时间必须向前");
                    if(day%7==0||day==days-1)
                    {
                        var save=bot.Session.CreateSave();string json=JsonUtility.ToJson(save);
                        File.WriteAllText(prefix+"-checkpoint.json",json);
                        var cash=bot.Session.Ledger.Balance;var now=bot.Session.Clock.Now.TotalMinutes;var worth=bot.Session.NetWorth();
                        bot.ReloadCheckpoint();Assert.AreEqual(cash,bot.Session.Ledger.Balance);Assert.AreEqual(now,bot.Session.Clock.Now.TotalMinutes);Assert.AreEqual(worth,bot.Session.NetWorth());
                        CompanyWellbeingRegressionTests.Invariants(bot.Session);
                        File.AppendAllText(prefix+"-progress.txt",$"day={before} elapsed={timer.Elapsed.TotalSeconds:F2}s bytes={System.Text.Encoding.UTF8.GetByteCount(json)} cash={cash.Cents} worth={worth.Cents}\n");
                    }
                    Assert.Less(timer.Elapsed.TotalSeconds,180,"单种子超过 180 秒；检查检查点和性能，不能无限挂起");
                }
                TestContext.Progress.WriteLine($"seed={seed} requested={days} reached={bot.Session.Clock.Today.TotalDays} gameOver={bot.Session.IsGameOver} reason={bot.Session.GameOverReason}");
                File.AppendAllText(prefix+"-progress.txt",$"FINAL requested={days} reached={bot.Session.Clock.Today.TotalDays} gameOver={bot.Session.IsGameOver} reason={bot.Session.GameOverReason}\n");
                Assert.True(bot.Session.IsGameOver||bot.Session.Clock.Today.TotalDays>=days);
            }
            finally{bot.Session.Dispose();}
        }
    }
}
