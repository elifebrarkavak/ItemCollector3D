using System;
using UnityEngine;

namespace ItemCollector3D.Characters
{
    /// <summary>The playable miners. Both have the same abilities: the choice is purely visual.</summary>
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Item Collector/Character Catalog")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string displayName;
            [Tooltip("Short line shown under the name on the selection screen.")]
            public string tagline;
            [Tooltip("Stylized miner model (feet at y = 0, about 2 m tall, facing +Z).")]
            public GameObject prefab;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public int Count => entries.Length;
        public Entry this[int index] => entries[index];

        public int IndexOf(string id)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        public Entry Find(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? entries[index] : null;
        }

        /// <summary>Test / tool helper: replaces the entries.</summary>
        public void SetEntries(Entry[] newEntries)
        {
            entries = newEntries ?? Array.Empty<Entry>();
        }
    }
}
