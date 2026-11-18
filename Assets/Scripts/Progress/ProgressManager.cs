using System;
using UnityEngine;

namespace ItemCollector3D.Progress
{
    /// <summary>
    /// Scene hook for the persistent progress: creates the <see cref="ProgressService"/> (file store on the device, or whatever
    /// <see cref="StoreFactoryOverride"/> returns, e.g. an in-memory store in tests) and relays its area events.
    /// Add one to every gameplay scene; the existing TreasureHuntController reads gold from it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProgressManager : MonoBehaviour
    {
        /// <summary>Test / tool hook: when set, this store is used instead of the save file. Must be set BEFORE the scene loads.</summary>
        public static Func<IProgressStore> StoreFactoryOverride;

        [SerializeField] private AreaCatalog catalog;
        [Tooltip("Save file name inside Application.persistentDataPath.")]
        [SerializeField] private string fileName = "item_collector_progress.json";

        public ProgressService Service { get; private set; }
        public bool IsReady => Service != null;

        /// <summary>Relays <see cref="ProgressService.AreaCompleted"/> (message: "AREA COMPLETE").</summary>
        public event Action<string> AreaCompleted;

        /// <summary>Relays <see cref="ProgressService.AreaUnlocked"/> (message: "NEW AREA UNLOCKED").</summary>
        public event Action<string> AreaUnlocked;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (Service != null)
            {
                return;
            }

            IProgressStore store = StoreFactoryOverride != null ? StoreFactoryOverride() : new FileProgressStore(fileName);
            Service = ProgressService.Create(catalog, store);
            Service.AreaCompleted += HandleAreaCompleted;
            Service.AreaUnlocked += HandleAreaUnlocked;
        }

        private void OnDestroy()
        {
            if (Service != null)
            {
                Service.AreaCompleted -= HandleAreaCompleted;
                Service.AreaUnlocked -= HandleAreaUnlocked;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Service != null)
            {
                Service.Save(); // progress is already saved on every change; this is a cheap safety net on Android
            }
        }

        [ContextMenu("Reset Saved Progress")]
        public void ResetSavedProgress()
        {
            Initialize();
            Service.ResetAll();
        }

        private void HandleAreaCompleted(string areaId)
        {
            AreaCompleted?.Invoke(areaId);
        }

        private void HandleAreaUnlocked(string areaId)
        {
            AreaUnlocked?.Invoke(areaId);
        }
    }
}
