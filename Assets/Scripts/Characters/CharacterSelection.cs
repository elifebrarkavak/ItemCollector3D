using System;
using UnityEngine;

namespace ItemCollector3D.Characters
{
    /// <summary>Where the chosen miner is stored. PlayerPrefs on the device, memory in tests.</summary>
    public interface ICharacterStore
    {
        bool TryGetString(string key, out string value);
        void SetString(string key, string value);
        void Save();
    }

    public sealed class PlayerPrefsCharacterStore : ICharacterStore
    {
        public bool TryGetString(string key, out string value)
        {
            value = null;
            if (!PlayerPrefs.HasKey(key))
            {
                return false;
            }

            value = PlayerPrefs.GetString(key);
            return true;
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }

    public sealed class MemoryCharacterStore : ICharacterStore
    {
        private readonly System.Collections.Generic.Dictionary<string, string> values = new();

        public int SaveCount { get; private set; }

        public bool TryGetString(string key, out string value)
        {
            return values.TryGetValue(key, out value);
        }

        public void SetString(string key, string value)
        {
            values[key] = value;
        }

        public void Save()
        {
            SaveCount++;
        }
    }

    /// <summary>
    /// The persistent miner choice (PlayerPrefs key <see cref="Key"/>). It never touches the progress / save system.
    /// An unknown or missing stored id falls back to the first catalog entry, so a bad value can never break the game.
    /// </summary>
    public static class CharacterSelection
    {
        public const string Key = "ic3d.character.selected";
        public const string MaleId = "barnaby";
        public const string FemaleId = "ruby";

        /// <summary>Test hook: when set the shared selection uses this store instead of PlayerPrefs.</summary>
        public static Func<ICharacterStore> StoreFactoryOverride;

        private static ICharacterStore store;
        private static string cached;

        /// <summary>Raised with the new id after <see cref="Select"/> changed the choice.</summary>
        public static event Action<string> Changed;

        private static ICharacterStore Store => store ??= StoreFactoryOverride != null ? StoreFactoryOverride() : new PlayerPrefsCharacterStore();

        /// <summary>The stored id (<see cref="MaleId"/> when nothing valid was stored yet).</summary>
        public static string SelectedId
        {
            get
            {
                if (cached == null)
                {
                    cached = Store.TryGetString(Key, out string stored) && !string.IsNullOrEmpty(stored) ? stored : MaleId;
                }

                return cached;
            }
        }

        /// <summary>The selected id if the catalog knows it, else the first entry's id.</summary>
        public static string ResolveId(CharacterCatalog catalog)
        {
            if (catalog == null || catalog.Count == 0)
            {
                return SelectedId;
            }

            return catalog.IndexOf(SelectedId) >= 0 ? SelectedId : catalog[0].id;
        }

        public static void Select(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            bool changed = id != SelectedId;
            cached = id;
            Store.SetString(Key, id);
            Store.Save();
            if (changed)
            {
                Changed?.Invoke(id);
            }
        }

        /// <summary>Drops the cached choice and store so the next access reloads (tests / tools).</summary>
        public static void ResetCurrent()
        {
            store = null;
            cached = null;
        }
    }
}
