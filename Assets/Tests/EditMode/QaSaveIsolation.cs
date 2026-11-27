using System;
using System.IO;
using NUnit.Framework;
using PCShopSell.Game;

namespace PCShopSell.Tests
{
    [SetUpFixture]
    public sealed class QaSaveIsolation
    {
        string previous;
        [OneTimeSetUp] public void Begin()
        {
            previous=SaveStore.DirectoryOverride;
            string root=Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS");
            if(string.IsNullOrEmpty(root))root=Path.Combine(Path.GetTempPath(),"pcshop-tests-"+Guid.NewGuid().ToString("N"));
            SaveStore.DirectoryOverride=Path.Combine(root,"saves");Directory.CreateDirectory(SaveStore.DirectoryOverride);
        }
        [OneTimeTearDown] public void End(){SaveStore.DirectoryOverride=previous;}
    }
}
