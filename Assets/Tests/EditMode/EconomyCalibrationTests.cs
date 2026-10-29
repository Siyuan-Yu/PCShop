using System.Text;
using NUnit.Framework;

namespace PCShopSell.Tests
{
    /// <summary>
    /// 数值校准：让"普通玩家机器人"跑完三年，打印各阶段身家，作为长线目标门槛的依据。
    /// 断言很松（跑得完、不报错），数值本身看输出。
    /// </summary>
    public class EconomyCalibrationTests
    {
        [Test]
        public void Bot_PlaysThreeYears_AndReportsNetWorth()
        {
            var sb = new StringBuilder();
            sb.AppendLine("seed | day30 | day90 | day180 | day365 | day730 | day1095 | gameover");
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var bot = new EconomyBot(seed);
                bot.Run(1095, 30, 90, 180, 365, 730, 1095);
                sb.Append(seed).Append(" |");
                foreach (var snap in bot.Snapshots)
                    sb.Append($" {snap.NetWorth}(rep{snap.Reputation:0}) |");
                sb.Append(bot.GameOverDay >= 0 ? $" 破产@{bot.GameOverDay}" : " -");
                sb.Append($" 结局={bot.Session.EndingId} 店面={bot.Session.PlayerShop.Size} 交情=" + string.Join("/", System.Linq.Enumerable.Select(PCShopSell.Core.Session.Npcs.All, n => bot.Session.Relation(n.Id))) + $" 涨租={bot.Session.RentHikes} y1={bot.Session.MilestonePassed("y1")} y2={bot.Session.MilestonePassed("y2")}");
                sb.AppendLine();
            }
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var lazy = new EconomyBot(seed, true, true);
                lazy.Run(1095, 30, 90, 180, 365, 730, 1095);
                sb.Append("懒 ").Append(seed).Append(" |");
                foreach (var snap in lazy.Snapshots) sb.Append($" {snap.NetWorth}(rep{snap.Reputation:0}) |");
                sb.Append(lazy.GameOverDay >= 0 ? $" 破产@{lazy.GameOverDay}" : " -");
                sb.AppendLine($" 结局={lazy.Session.EndingId} 涨租={lazy.Session.RentHikes} y1={lazy.Session.MilestonePassed("y1")} y2={lazy.Session.MilestonePassed("y2")}");
            }
            var d = new EconomyBot(1);
            d.Run(120);
            foreach (var win in new[] { (10, 30), (50, 70), (100, 120) })
            {
                sb.Append($"seed1 日均 day{win.Item1}-{win.Item2}:");
                foreach (string cat in new[] { "零售", "装机", "进货", "工资", "事件", "餐饮" })
                {
                    var sum = d.Session.Ledger.SumCategory(cat, new PCShopSell.Core.Time.GameDate(win.Item1), new PCShopSell.Core.Time.GameDate(win.Item2));
                    sb.Append($" {cat}={sum.Cents / 100 / (win.Item2 - win.Item1)}");
                }
                sb.AppendLine();
            }
            TestContext.Progress.WriteLine(sb.ToString());
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcshop_economy.txt"), sb.ToString());
            Assert.Pass();
        }
    }
}
