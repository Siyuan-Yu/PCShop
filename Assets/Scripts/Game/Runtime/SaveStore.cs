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
        public const int SlotCount = 9;

        private static string SlotPath(int slot, bool automatic)
        {
            if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            if (automatic && slot == 2) return AutoPath;
            return Path.Combine(SaveDirectory, automatic ? $"save{slot}.auto.json" : $"save{slot}.json");
        }

        private static int LegacySlot(bool automatic) => automatic ? 2 : 1;
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
        public static bool Exists => Enumerable.Range(1, SlotCount).Any(IsOccupied);
        public static int FirstEmptySlot => Enumerable.Range(1, SlotCount).FirstOrDefault(slot => !IsOccupied(slot));

        private static bool HasFile(string path) => File.Exists(path) || File.Exists(path + ".bak");
        public static bool IsOccupied(int slot) => HasFile(SlotPath(slot, false)) || HasFile(SlotPath(slot, true));

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
            var info = new SlotInfo { Automatic = automatic };
            string path = SlotPath(slot, automatic);
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
            var paths = includeLegacyFallback
                ? new[] { preferred, preferred + ".bak", other, other + ".bak" }
                : new[] { preferred, preferred + ".bak" };
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    if (data == null || string.IsNullOrEmpty(data.rngState)) throw new InvalidDataException("存档数据不完整");
                    GameSession session = GameSession.Load(city, data);
                    bool legacyFallback = path == other || path == other + ".bak";
                    campaignId = CampaignId(data.saveCampaignId, legacyFallback ? LegacySlot(!automatic) : slot, legacyFallback ? !automatic : automatic);
                    LastLoadMessage = path == preferred ? "读档成功。" : "已从备用存档恢复：" + Path.GetFileName(path);
                    return session;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is FormatException
                    || e is OverflowException || e is InvalidOperationException || e is KeyNotFoundException)
                {
                    Debug.LogWarning("无法读取 " + Path.GetFileName(path) + "：" + e.Message);
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
            for (int slot = 1; slot <= SlotCount; slot++)
            foreach (bool automatic in new[] { false, true })
            {
                string path = SlotPath(slot, automatic);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            PCShop_SyncStorage();
#endif
        }
    }
}
