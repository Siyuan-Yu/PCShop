using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game
{
    /// <summary>手动和自动存档分开写；原子替换保留上一个有效版本。</summary>
    public static class SaveStore
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void PCShop_SyncStorage();
#endif
        public static string DirectoryOverride { get; set; }
        private static string SaveDirectory => DirectoryOverride ?? (GameSession.FirstChapterTrial
            ? Path.Combine(Application.persistentDataPath, "first-chapter-trial") : Application.persistentDataPath);
        public static string PathOf => Path.Combine(SaveDirectory, "save1.json");
        public static string AutoPath => Path.Combine(SaveDirectory, "autosave.json");
        public static string CityTestPath => Path.Combine(SaveDirectory,"city-life-test.json");
        public const int SlotCount = 20;
        public const int AutomaticSlot = 0;

        private static string SlotPath(int slot, bool automatic)
        {
            if (automatic) return AutoPath;
            if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(SaveDirectory, $"save{slot}.json");
        }

        private static int LegacySlot(bool automatic) => automatic ? AutomaticSlot : 1;
        private static IEnumerable<string> AutomaticPaths() => new[] { AutoPath }
            .Concat(Enumerable.Range(1, SlotCount).Select(slot => Path.Combine(SaveDirectory, $"save{slot}.auto.json")));
        private static string CampaignId(string savedId, int slot, bool automatic) =>
            string.IsNullOrEmpty(savedId) ? $"legacy-{slot}-{automatic}" : savedId;

        public static string LastSaveMessage { get; private set; }
        public static bool TrySave(GameSession session, bool automatic = false) =>
            TrySave(session, LegacySlot(automatic), automatic);

        public static bool TrySave(GameSession session, int slot, bool automatic = false, string campaignId = null)
        {
            try { Save(session, slot, automatic, campaignId); LastSaveMessage = "存档成功。"; return true; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                LastSaveMessage = "存档失败：无法写入存档目录，原存档已保留。";
                Debug.LogWarning(LastSaveMessage + e.Message); return false;
            }
        }
        public static string LastLoadMessage { get; private set; }
        public static bool Exists => AutomaticPaths().Any(HasFile) || Enumerable.Range(1, SlotCount).Any(IsOccupied);
        public static int FirstEmptySlot => Enumerable.Range(1, SlotCount).FirstOrDefault(slot => !IsOccupied(slot));

        private static bool HasFile(string path) => File.Exists(path) || File.Exists(path + ".bak");
        public static bool IsOccupied(int slot) => HasFile(SlotPath(slot, false));

        [Serializable]
        private sealed class SlotHeader
        {
            public string savedAt, saveCampaignId, rngState, currentPlace, shopCustomName;
            public int day, minute;
            public long balanceCents;
        }

        public sealed class SlotInfo
        {
            public bool Exists;
            public bool Readable;
            public bool Automatic;
            public string SavedAt;
            public string Campaign;
            public string Place;
            public string ShopName;
            public int Day, Minute;
            public long BalanceCents;
            public DateTime Stamp;
        }

        public static SlotInfo InspectSlot(bool automatic) => InspectSlot(LegacySlot(automatic), automatic);

        public static SlotInfo InspectSlot(int slot, bool automatic)
        {
            if (!automatic) return InspectPath(SlotPath(slot, false), slot, false);
            var candidates = new List<SlotInfo> { InspectPath(AutoPath, 2, true) };
            for (int legacySlot = 1; legacySlot <= SlotCount; legacySlot++)
                candidates.Add(InspectPath(Path.Combine(SaveDirectory, $"save{legacySlot}.auto.json"), legacySlot, true));
            return candidates.Where(info => info.Readable).OrderByDescending(info => info.Stamp).FirstOrDefault()
                ?? new SlotInfo { Automatic = true, Exists = candidates.Any(info => info.Exists) };
        }

        private static SlotInfo InspectPath(string path, int slot, bool automatic)
        {
            var info = new SlotInfo { Automatic = automatic };
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                info.Exists = true;
                try
                {
                    var data = JsonUtility.FromJson<SlotHeader>(File.ReadAllText(candidate));
                    if (data != null && ulong.TryParse(data.rngState, out _) && data.day >= 0)
                    {
                        info.SavedAt = data.savedAt;
                        info.Campaign = CampaignId(data.saveCampaignId, slot, automatic);
                        info.Day = data.day;
                        info.Minute = data.minute;
                        info.Place = data.currentPlace;
                        info.ShopName = data.shopCustomName;
                        info.BalanceCents = data.balanceCents;
                        info.Stamp = File.GetLastWriteTimeUtc(candidate);
                        info.Readable = true;
                        return info;
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { }
            }
            return info;
        }

        public static void Save(GameSession session, bool automatic = false) =>
            Save(session, LegacySlot(automatic), automatic, null);

        private static void Save(GameSession session, int slot, bool automatic, string campaignId)
        {
            SaveData data = session.CreateSave();
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            data.saveCampaignId = CampaignId(campaignId, slot, automatic);
            string path = session.IsCityLifeTest ? CityTestPath : SlotPath(slot, automatic);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data));
#if UNITY_WEBGL && !UNITY_EDITOR
            if (File.Exists(path)) { File.Copy(path, path + ".bak", true); File.Delete(path); }
            File.Move(temp, path);
            PCShop_SyncStorage();
#else
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
#endif
        }

        public static GameSession Load(CityLayout city, bool automatic = false) =>
            LoadPaths(city, LegacySlot(automatic), automatic, out _, includeLegacyFallback: true);

        public static GameSession LoadSlot(CityLayout city, int slot, bool automatic, out string campaignId) =>
            LoadPaths(city, slot, automatic, out campaignId, includeLegacyFallback: false);

        private static GameSession LoadPaths(CityLayout city, int slot, bool automatic, out string campaignId, bool includeLegacyFallback)
        {
            LastLoadMessage = null;
            campaignId = null;
            string preferred = SlotPath(slot, automatic);
            string other = SlotPath(LegacySlot(!automatic), !automatic);
            var paths = automatic ? AutomaticPaths().SelectMany(path => new[] { path, path + ".bak" })
                    .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).ToArray()
                : new[] { preferred, preferred + ".bak" };
            if (includeLegacyFallback)
            {
                var fallback = automatic ? new[] { other, other + ".bak" }
                    : AutomaticPaths().SelectMany(path => new[] { path, path + ".bak" })
                        .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
                paths = paths.Concat(fallback).Distinct().ToArray();
            }
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    if (data == null || string.IsNullOrEmpty(data.rngState)) throw new InvalidDataException("存档数据不完整");
                    GameSession session = GameSession.Load(city, data);
                    bool sourceAutomatic = AutomaticPaths().Any(autoPath => path == autoPath || path == autoPath + ".bak");
                    int sourceSlot = slot;
                    if (sourceAutomatic)
                    {
                        sourceSlot = 2;
                        if (path != AutoPath && path != AutoPath + ".bak")
                            int.TryParse(Path.GetFileName(path).Substring(4).Split('.')[0], out sourceSlot);
                    }
                    else if (automatic) sourceSlot = 1;
                    campaignId = CampaignId(data.saveCampaignId, sourceSlot, sourceAutomatic);
                    LastLoadMessage = path == preferred ? "读档成功。" : "已从备用存档恢复：" + Path.GetFileName(path);
                    return session;
                }
                catch (Exception e)  // 含空引用/越界等：任何读档失败都继续尝试下一个候选（.bak），原文件保留
                {
                    Debug.LogWarning("无法读取 " + Path.GetFileName(path) + "：" + e);
                }
            }
            LastLoadMessage = paths.Any(File.Exists) ? "存档无法读取，原文件已保留。" : "没有存档。";
            return null;
        }
        public static GameSession LoadCityLifeTest(CityLayout city)
        {
            foreach(string path in new[]{CityTestPath,CityTestPath+".bak"})
            {
                if(!File.Exists(path))continue;
                try
                {
                    var data=JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    if(data==null||!data.cityLifeTest)throw new InvalidDataException("不是独立测试档");
                    return GameSession.Load(city,data);
                }
                catch(Exception e) when(e is IOException||e is ArgumentException||e is InvalidOperationException||e is FormatException)
                {Debug.LogWarning("测试档无法读取，原文件保留："+e.Message);}
            }
            return null;
        }

        public static void Delete()
        {
            var paths = Enumerable.Range(1, SlotCount).Select(slot => SlotPath(slot, false)).Concat(AutomaticPaths());
            foreach (string path in paths)
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            PCShop_SyncStorage();
#endif
        }
    }
}
