using System;
using System.IO;
using UnityEngine;

namespace VN
{
    public static class VNAutosave
    {
        private const string FileName = "vn_autosave.json";

        public static string GetPath() => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(VNState state)
        {
            if (state == null) return;
            try
            {
                var json = JsonUtility.ToJson(state, true);
                File.WriteAllText(GetPath(), json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VNAutosave] Save failed: {e.Message}");
            }
        }

        public static bool TryLoad(out VNState state)
        {
            state = null;
            try
            {
                var path = GetPath();
                if (!File.Exists(path)) return false;

                var json = File.ReadAllText(path);
                state = JsonUtility.FromJson<VNState>(json);
                if (state == null) return false;

                state.EnsureSlots();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VNAutosave] Load failed: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            try
            {
                var path = GetPath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VNAutosave] Delete failed: {e.Message}");
            }
        }
    }
}