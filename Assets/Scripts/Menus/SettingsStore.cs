using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>Where the settings live. PlayerPrefs on the device, memory in tests (so tests never touch the real prefs).</summary>
    public interface ISettingsStore
    {
        bool TryGetFloat(string key, out float value);
        void SetFloat(string key, float value);
        void Save();
    }

    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        private readonly string prefix;

        public PlayerPrefsSettingsStore(string keyPrefix = "ic3d.settings.")
        {
            prefix = keyPrefix;
        }

        public bool TryGetFloat(string key, out float value)
        {
            value = 0f;
            if (!PlayerPrefs.HasKey(prefix + key))
            {
                return false;
            }

            value = PlayerPrefs.GetFloat(prefix + key);
            return true;
        }

        public void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(prefix + key, value);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>Removes this store's keys (tests / reset).</summary>
        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(prefix + key);
        }
    }

    public sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly System.Collections.Generic.Dictionary<string, float> values = new();

        public int SaveCount { get; private set; }

        public bool TryGetFloat(string key, out float value)
        {
            return values.TryGetValue(key, out value);
        }

        public void SetFloat(string key, float value)
        {
            values[key] = value;
        }

        public void Save()
        {
            SaveCount++;
        }
    }
}
