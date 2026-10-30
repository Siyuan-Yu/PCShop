using System;
using System.IO;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game
{
    /// <summary>手动和自动存档分开写；原子替换保留上一个有效版本。</summary>
    public static class SaveStore
    {
        public static string DirectoryOverride { get; set; }
        private static string SaveDirectory => DirectoryOverride ?? Application.persistentDataPath;
        public static string PathOf => Path.Combine(SaveDirectory, "save1.json");
        public static string AutoPath => Path.Combine(SaveDirectory, "autosave.json");
        public static string LastSaveMessage { get; private set; }
        public static bool TrySave(GameSession session, bool automatic = false)
        {
            try { Save(session, automatic); LastSaveMessage = "存档成功。"; return true; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                LastSaveMessage = "存档失败：无法写入存档目录，原存档已保留。";
                Debug.LogWarning(LastSaveMessage + e.Message); return false;
            }
        }
        public static string LastLoadMessage { get; private set; }
        public static bool Exists => File.Exists(PathOf) || File.Exists(AutoPath) || File.Exists(PathOf + ".bak") || File.Exists(AutoPath + ".bak");

        public static void Save(GameSession session, bool automatic = false)
        {
            SaveData data = session.CreateSave();
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            string path = automatic ? AutoPath : PathOf;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }

        public static GameSession Load(CityLayout city, bool automatic = false)
        {
            LastLoadMessage = null;
            string preferred = automatic ? AutoPath : PathOf;
            string other = automatic ? PathOf : AutoPath;
            foreach (string path in new[] { preferred, preferred + ".bak", other, other + ".bak" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    if (data == null || string.IsNullOrEmpty(data.rngState)) throw new InvalidDataException("存档数据不完整");
                    GameSession session = GameSession.Load(city, data);
                    LastLoadMessage = path == preferred ? "读档成功。" : "已从备用存档恢复：" + Path.GetFileName(path);
                    return session;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is FormatException || e is OverflowException || e is InvalidOperationException)
                {
                    Debug.LogWarning("无法读取 " + Path.GetFileName(path) + "：" + e.Message);
                }
            }
            LastLoadMessage = Exists ? "存档无法读取，原文件已保留。" : "没有存档。";
            return null;
        }

        public static void Delete()
        {
            foreach (string path in new[] { PathOf, AutoPath, PathOf + ".bak", AutoPath + ".bak" })
                if (File.Exists(path)) File.Delete(path);
        }
    }
}
