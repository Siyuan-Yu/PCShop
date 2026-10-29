using System.IO;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game
{
    /// <summary>存档读写：persistentDataPath/save1.json（JsonUtility）。</summary>
    public static class SaveStore
    {
        public static string PathOf => Path.Combine(Application.persistentDataPath, "save1.json");

        public static bool Exists => File.Exists(PathOf);

        public static void Save(GameSession session)
        {
            SaveData data = session.CreateSave();
            data.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            File.WriteAllText(PathOf, JsonUtility.ToJson(data));
        }

        public static GameSession Load(CityLayout city)
        {
            if (!Exists) return null;
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOf));
            return GameSession.Load(city, data);
        }

        public static void Delete()
        {
            if (Exists) File.Delete(PathOf);
        }
    }
}
