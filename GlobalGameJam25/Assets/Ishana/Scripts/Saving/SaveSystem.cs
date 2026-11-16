using System;
using System.IO;
using UnityEngine;

namespace Ishana.Saving
{
    /// <summary>Reads and writes the single save file as JSON in Application.persistentDataPath.</summary>
    public static class SaveSystem
    {
        const string FileName = "ishana_save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists => File.Exists(FilePath);

        public static SaveData Load()
        {
            string path = FilePath;
            if (!File.Exists(path)) return null;

            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null || data.version > SaveData.CurrentVersion)
                {
                    Debug.LogWarning($"SaveSystem: save file at {path} is from a newer version or empty. Ignoring it.");
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                // Keep the unreadable file for inspection instead of silently overwriting it
                Debug.LogError($"SaveSystem: could not read save file at {path}: {e.Message}");
                try { File.Copy(path, path + ".corrupt", true); } catch { /* best effort */ }
                return null;
            }
        }

        public static bool Save(SaveData data)
        {
            if (data == null) return false;
            string path = FilePath;
            string temp = path + ".tmp";

            try
            {
                data.version = SaveData.CurrentVersion;
                data.savedAtUtc = DateTime.UtcNow.ToString("o");

                // Write to a temp file first so a crash mid-write can't destroy the existing save
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem: could not write save file at {path}: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem: could not delete save file: {e.Message}");
            }
        }
    }
}
