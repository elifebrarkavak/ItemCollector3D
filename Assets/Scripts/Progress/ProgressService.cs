using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.Progress
{
    public enum ProgressLoadStatus
    {
        /// <summary>No saved data existed: a fresh game.</summary>
        NewGame,

        /// <summary>Saved data was read and was valid.</summary>
        Loaded,

        /// <summary>Saved data was unreadable or inconsistent and was replaced / repaired (the bad text is backed up).</summary>
        Recovered
    }

    public enum RegisterResult
    {
        Accepted,
        AlreadyCollected,
        InvalidId,
        InvalidGold
    }

    /// <summary>
    /// The single source of truth for persistent progress: cumulative total gold, collected treasure ids, and which areas are
    /// unlocked / completed. Plain C# (no MonoBehaviour) so it is easy to test. It does not own the HUD - the existing
    /// <c>TreasureHuntController</c> reads the total from here and keeps driving the existing HUD.
    /// </summary>
    public sealed class ProgressService
    {
        public const string AreaCompleteMessage = "AREA COMPLETE";
        public const string NewAreaUnlockedMessage = "NEW AREA UNLOCKED";
        public const int MaxTotalGold = 99_999_999;

        private readonly IProgressStore store;
        private readonly HashSet<string> collected = new();
        private readonly HashSet<string> unlocked = new();
        private readonly HashSet<string> completed = new();
        private GameProgressData data;

        public AreaCatalog Catalog { get; }
        public ProgressLoadStatus LoadStatus { get; private set; }

        /// <summary>Why the data was recovered (empty unless <see cref="LoadStatus"/> is Recovered).</summary>
        public string RecoveryNote { get; private set; } = string.Empty;

        public int TotalGold => data.totalGold;
        public IReadOnlyList<string> CollectedTreasureIds => data.collectedTreasureIds;
        public IReadOnlyList<string> UnlockedAreaIds => data.unlockedAreaIds;
        public IReadOnlyList<string> CompletedAreaIds => data.completedAreaIds;

        /// <summary>Raised ONCE per area when its goal is reached (never again, not even after a reload).</summary>
        public event Action<string> AreaCompleted;

        /// <summary>Raised ONCE per area when it unlocks.</summary>
        public event Action<string> AreaUnlocked;

        /// <summary>Raised after any change to the saved data.</summary>
        public event Action Changed;

        private ProgressService(AreaCatalog catalog, IProgressStore progressStore)
        {
            store = progressStore ?? new MemoryProgressStore();

            if (catalog == null)
            {
                catalog = AreaCatalog.CreateDefault();
            }
            else if (!catalog.Validate(out string error))
            {
                ProgressLog.Warning("AreaCatalog is invalid (" + error + "); using the built-in default areas.");
                catalog = AreaCatalog.CreateDefault();
            }

            Catalog = catalog;
        }

        /// <summary>Creates the service and loads the saved data (never throws; bad data is recovered).</summary>
        public static ProgressService Create(AreaCatalog catalog, IProgressStore store)
        {
            var service = new ProgressService(catalog, store);
            service.Load();
            return service;
        }

        public bool IsCollected(string treasureId)
        {
            return !string.IsNullOrWhiteSpace(treasureId) && collected.Contains(treasureId.Trim());
        }

        public bool IsAreaUnlocked(string areaId)
        {
            return areaId != null && unlocked.Contains(areaId);
        }

        public bool IsAreaCompleted(string areaId)
        {
            return areaId != null && completed.Contains(areaId);
        }

        /// <summary>Cumulative total gold that completes the area, or -1 for an unknown area.</summary>
        public int GetGoal(string areaId)
        {
            AreaEntry area = Catalog.Get(areaId);
            return area != null ? area.goalTotalGold : -1;
        }

        public string GetDisplayName(string areaId)
        {
            AreaEntry area = Catalog.Get(areaId);
            return area != null ? area.displayName : null;
        }

        /// <summary>
        /// Adds a collected treasure: its gold goes into the cumulative total exactly once per treasure id, thresholds are evaluated
        /// (AreaCompleted / AreaUnlocked fire once each) and the progress is saved.
        /// </summary>
        public RegisterResult RegisterCollected(string treasureId, int gold)
        {
            if (string.IsNullOrWhiteSpace(treasureId))
            {
                return RegisterResult.InvalidId;
            }

            if (gold < 0)
            {
                return RegisterResult.InvalidGold;
            }

            string id = treasureId.Trim();
            if (collected.Contains(id))
            {
                return RegisterResult.AlreadyCollected;
            }

            collected.Add(id);
            data.collectedTreasureIds.Add(id);
            data.totalGold = (int)Math.Min((long)data.totalGold + gold, MaxTotalGold);

            List<(string areaId, bool isUnlock)> raised = EvaluateThresholds();
            Save();
            Changed?.Invoke();

            foreach ((string areaId, bool isUnlock) in raised)
            {
                if (isUnlock)
                {
                    AreaUnlocked?.Invoke(areaId);
                }
                else
                {
                    AreaCompleted?.Invoke(areaId);
                }
            }

            return RegisterResult.Accepted;
        }

        public bool Save()
        {
            return store.Write(JsonUtility.ToJson(data));
        }

        /// <summary>Erases the saved progress (tools / tests / a future "new game"). Raises no area events.</summary>
        public void ResetAll()
        {
            store.Delete();
            ApplyData(NewData());
            Changed?.Invoke();
        }

        /// <summary>
        /// NEW GAME: replaces the saved progress with a fresh start (0 gold, no collected treasure, only the first area unlocked, nothing completed).
        /// The fresh data is WRITTEN first (the store writes atomically); only when that succeeded is the in-memory state replaced, so a failed
        /// write leaves both the old save and the running game exactly as they were. Raises no area events. Settings and the chosen miner live
        /// elsewhere and are not touched.
        /// </summary>
        /// <returns>true if the new game was saved and applied; false if nothing was changed.</returns>
        public bool StartNewGame()
        {
            GameProgressData fresh = NewData();
            bool written;
            try
            {
                written = store.Write(JsonUtility.ToJson(fresh));
            }
            catch (Exception exception)
            {
                ProgressLog.Warning("NEW GAME could not be saved: " + exception.Message);
                return false;
            }

            if (!written)
            {
                ProgressLog.Warning("NEW GAME could not be saved: the progress was left unchanged.");
                return false;
            }

            ApplyData(fresh);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ loading

        private void Load()
        {
            GameProgressData loaded = null;
            string note = null;

            if (!store.TryRead(out string json))
            {
                LoadStatus = ProgressLoadStatus.NewGame;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    note = "The save file was empty.";
                }
                else
                {
                    try
                    {
                        loaded = JsonUtility.FromJson<GameProgressData>(json);
                    }
                    catch (Exception exception)
                    {
                        note = "The save file could not be parsed: " + exception.Message;
                    }

                    if (loaded == null && note == null)
                    {
                        note = "The save file contained no usable data.";
                    }
                }

                if (loaded != null && loaded.version != GameProgressData.CurrentVersion)
                {
                    note = $"Unsupported save version {loaded.version} (expected {GameProgressData.CurrentVersion}).";
                    loaded = null;
                }

                if (loaded == null)
                {
                    store.BackupCorrupt(json);
                    LoadStatus = ProgressLoadStatus.Recovered;
                    RecoveryNote = note ?? string.Empty;
                    ProgressLog.Warning("Progress was reset: " + RecoveryNote);
                }
                else
                {
                    string repairNote = Sanitize(loaded);
                    if (repairNote != null)
                    {
                        store.BackupCorrupt(json);
                        LoadStatus = ProgressLoadStatus.Recovered;
                        RecoveryNote = repairNote;
                        ProgressLog.Warning("Progress was repaired: " + repairNote);
                    }
                    else
                    {
                        LoadStatus = ProgressLoadStatus.Loaded;
                    }
                }
            }

            ApplyData(loaded ?? NewData());

            if (LoadStatus == ProgressLoadStatus.Recovered)
            {
                Save(); // replace the bad file with clean data
            }
        }

        private GameProgressData NewData()
        {
            var fresh = new GameProgressData();
            fresh.unlockedAreaIds.Add(Catalog.Areas[0].id); // the first area is open from the start
            return fresh;
        }

        private void ApplyData(GameProgressData newData)
        {
            data = newData;
            collected.Clear();
            unlocked.Clear();
            completed.Clear();
            foreach (string id in data.collectedTreasureIds)
            {
                collected.Add(id);
            }

            foreach (string id in data.unlockedAreaIds)
            {
                unlocked.Add(id);
            }

            foreach (string id in data.completedAreaIds)
            {
                completed.Add(id);
            }
        }

        /// <summary>Makes loaded data safe and consistent. Returns a description of what was repaired, or null if nothing was.</summary>
        private string Sanitize(GameProgressData d)
        {
            var notes = new List<string>();

            d.collectedTreasureIds ??= new List<string>();
            d.unlockedAreaIds ??= new List<string>();
            d.completedAreaIds ??= new List<string>();

            if (d.totalGold < 0 || d.totalGold > MaxTotalGold)
            {
                d.totalGold = Mathf.Clamp(d.totalGold, 0, MaxTotalGold);
                notes.Add("total gold out of range");
            }

            var cleanIds = new List<string>();
            var seen = new HashSet<string>();
            foreach (string raw in d.collectedTreasureIds)
            {
                string id = raw?.Trim();
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                {
                    continue;
                }

                cleanIds.Add(id);
            }

            if (cleanIds.Count != d.collectedTreasureIds.Count)
            {
                notes.Add("empty or duplicate treasure ids removed");
            }

            d.collectedTreasureIds = cleanIds;

            // Area flags follow from the (cumulative, never decreasing) gold: repair anything that disagrees.
            var expectedUnlocked = new List<string>();
            var expectedCompleted = new List<string>();
            foreach (AreaEntry area in Catalog.Areas)
            {
                bool isUnlocked = area == Catalog.Areas[0] || d.totalGold >= area.unlockTotalGold;
                if (isUnlocked)
                {
                    expectedUnlocked.Add(area.id);
                    if (d.totalGold >= area.goalTotalGold)
                    {
                        expectedCompleted.Add(area.id);
                    }
                }
            }

            if (!SameSet(d.unlockedAreaIds, expectedUnlocked) || !SameSet(d.completedAreaIds, expectedCompleted))
            {
                notes.Add("area unlock / complete flags did not match the saved gold");
            }

            d.unlockedAreaIds = expectedUnlocked;
            d.completedAreaIds = expectedCompleted;

            return notes.Count == 0 ? null : string.Join("; ", notes);
        }

        private static bool SameSet(List<string> a, List<string> b)
        {
            return new HashSet<string>(a).SetEquals(b) && a.Count == b.Count;
        }

        // ------------------------------------------------------------------ thresholds

        private List<(string areaId, bool isUnlock)> EvaluateThresholds()
        {
            var raised = new List<(string, bool)>();
            foreach (AreaEntry area in Catalog.Areas)
            {
                if (!unlocked.Contains(area.id) && data.totalGold >= area.unlockTotalGold)
                {
                    unlocked.Add(area.id);
                    data.unlockedAreaIds.Add(area.id);
                    raised.Add((area.id, true));
                }

                if (unlocked.Contains(area.id) && !completed.Contains(area.id) && data.totalGold >= area.goalTotalGold)
                {
                    completed.Add(area.id);
                    data.completedAreaIds.Add(area.id);
                    raised.Add((area.id, false));
                }
            }

            return raised;
        }
    }
}
