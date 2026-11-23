using System.IO;
using NUnit.Framework;
using PCShopSell.Game;

namespace PCShopSell.Tests
{
    /// <summary>主存档损坏（无论读取阶段抛什么异常）都必须回退到 .bak，并保留原文件。</summary>
    public sealed class SaveStoreFallbackTests
    {
        private string _dir;
        private string _previous;

        [SetUp] public void SetUp()
        {
            _previous = SaveStore.DirectoryOverride;
            _dir = Path.Combine(Path.GetTempPath(), "pcshop-save-fallback-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            SaveStore.DirectoryOverride = _dir;
        }

        [TearDown] public void TearDown()
        {
            SaveStore.DirectoryOverride = _previous;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [TestCase("{\"rngState\":\"1\",\"day\":-5,\"cityPeople\":[null]}", TestName = "PrimaryThrowsWhileRestoring")]
        [TestCase("not json at all", TestName = "PrimaryNotJson")]
        public void CorruptPrimary_FallsBackToBackup_AndKeepsTheBrokenFile(string brokenContent)
        {
            using var session = DistrictProgressionTests.Make();
            Assert.IsTrue(SaveStore.TrySave(session, 1));
            session.Clock.Restore(new PCShopSell.Core.Time.GameDate(session.Clock.Today.TotalDays + 1), 600);
            Assert.IsTrue(SaveStore.TrySave(session, 1), "第二次存档会把第一次的内容挤到 .bak");
            string primary = Path.Combine(_dir, "save1.json");
            Assert.IsTrue(File.Exists(primary + ".bak"));

            File.WriteAllText(primary, brokenContent);
            using var loaded = SaveStore.LoadSlot(session.City, 1, false, out _);
            Assert.NotNull(loaded, "主档损坏时应从 .bak 恢复：" + SaveStore.LastLoadMessage);
            StringAssert.Contains("备用", SaveStore.LastLoadMessage);
            Assert.AreEqual(brokenContent, File.ReadAllText(primary), "损坏的原文件应保留，不被覆盖或删除");
        }

        [Test] public void BothCorrupt_ReturnsNullAndKeepsFiles()
        {
            using var session = DistrictProgressionTests.Make();
            string primary = Path.Combine(_dir, "save1.json");
            File.WriteAllText(primary, "not json");
            File.WriteAllText(primary + ".bak", "{\"rngState\":\"1\",\"day\":-5}");
            Assert.IsNull(SaveStore.LoadSlot(session.City, 1, false, out _));
            StringAssert.Contains("原文件已保留", SaveStore.LastLoadMessage);
            Assert.IsTrue(File.Exists(primary) && File.Exists(primary + ".bak"));
        }
    }
}
