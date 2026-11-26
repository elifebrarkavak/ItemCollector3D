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
        private readonly HashSet<string> celebrated = new();
        private readonly HashSet<string> pendingCelebration = new();
        private readonly HashSet<string> pendingAchievements = new();
        private readonly HashSet<string> celebratedAchievements = new();
        private GameProgressData data;

        public AreaCatalog Catalog { get; }
        public ProgressLoadStatus LoadStatus { get; private set; }

        /// <summary>Why the data was recovered (empty unless <see cref="LoadStatus"/> is Recovered).</summary>
        public string RecoveryNote { get; private set; } = string.Empty;

        /// <summary>
        /// True when a saved game exists: the save file was there when the service started (even with 0 gold: a game in progress is not a first launch), or this service has written one since
        /// (NEW GAME, a collected treasure...). False only on a real first launch, when no save file existed at all. The Main Menu uses this to tell START ADVENTURE from CONTINUE GAME (stage 18B).
        /// A save that had to be recovered counts as a saved game.
        /// </summary>
        public bool HasSave { get; private set; }

        /// <summary>How many areas of the catalog are open at the moment (the first one always is).</summary>
        public int UnlockedAreaCount
        {
            get
            {
                int count = 0;
                foreach (AreaEntry area in Catalog.Areas)
                {
                    if (unlocked.Contains(area.id))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int TotalGold => data.totalGold;

        /// <summary>The Green Mine tutorial: Pending only after NEW GAME (and until it is finished or skipped); older saves read as NotOffered.</summary>
        public TutorialStatus Tutorial => (TutorialStatus)Mathf.Clamp(data.tutorialState, 0, (int)TutorialStatus.Finished);
        public IReadOnlyList<string> CollectedTreasureIds => data.collectedTreasureIds;
        public IReadOnlyList<string> UnlockedAreaIds => data.unlockedAreaIds;
        public IReadOnlyList<string> CompletedAreaIds => data.completedAreaIds;
        public IReadOnlyList<string> CelebratedAreaIds => data.celebratedAreaIds;
        public IReadOnlyList<string> PendingCelebrationAreaIds => data.pendingCelebrationAreaIds;
        public IReadOnlyList<string> PendingAchievementIds => data.pendingAchievementIds;
        public IReadOnlyList<string> CelebratedAchievementIds => data.celebratedAchievementIds;

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

        /// <summary>
        /// True once the LEVEL COMPLETE celebration (every treasure of the area collected, stage 17C) was shown for this area. This is NOT <see cref="IsAreaCompleted"/>: that one is the cumulative
        /// gold goal (e.g. 500 gold in Green Mine) and can be true long before the last treasure is found.
        /// </summary>
        public bool IsCompletionCelebrated(string areaId)
        {
            return areaId != null && celebrated.Contains(areaId);
        }

        /// <summary>True while the last treasure of the area was collected but the player has not answered its LEVEL COMPLETE celebration yet (it is shown the next time the area is entered).</summary>
        public bool IsCompletionPending(string areaId)
        {
            return areaId != null && pendingCelebration.Contains(areaId);
        }

        // ------------------------------------------------------------------ final achievements (stage 17D)

        /// <summary>
        /// How many of the 60 treasures of the three maps are in the save. Counted against <see cref="TreasureCatalog"/> by id: unknown ids (the sandbox coin), blanks and duplicates never count, and gold is not looked at.
        /// </summary>
        public int CollectedTreasureCount => CountCollected(TreasureCatalog.AllIds);

        /// <summary>How many treasures of one area (an AreaCatalog id) are in the save, counted against <see cref="TreasureCatalog"/>.</summary>
        public int CollectedTreasureCountIn(string areaId)
        {
            return CountCollected(TreasureCatalog.IdsOf(areaId));
        }

        /// <summary>True once every one of the 60 treasures is in the save.</summary>
        public bool IsCollectionComplete => CollectedTreasureCount >= TreasureCatalog.TotalCount;

        /// <summary>True once the cumulative total gold is at least <see cref="FinalAchievements.Goal"/> (gold above it is kept: nothing is capped at 2500).</summary>
        public bool IsGameCompleteReached => data.totalGold >= FinalAchievements.Goal;

        /// <summary>"Is the goal reached?": a fact of the saved gold / treasures, independent of any celebration.</summary>
        public bool IsAchievementReached(string achievementId)
        {
            return achievementId switch
            {
                FinalAchievements.GameComplete => IsGameCompleteReached,
                FinalAchievements.Collection => IsCollectionComplete,
                _ => false,
            };
        }

        /// <summary>"Is the celebration waiting to be shown?": the goal was crossed by a COLLECT and the player has not answered the panel yet.</summary>
        public bool IsAchievementPending(string achievementId)
        {
            return achievementId != null && pendingAchievements.Contains(achievementId);
        }

        /// <summary>"Was the celebration already shown and answered?" (until the next NEW GAME).</summary>
        public bool IsAchievementCelebrated(string achievementId)
        {
            return achievementId != null && celebratedAchievements.Contains(achievementId);
        }

        /// <summary>The next owed final celebration in the order GAME COMPLETE, then 100% COLLECTION, or null when none is owed.</summary>
        public string NextPendingAchievement()
        {
            foreach (string id in FinalAchievements.Order)
            {
                if (pendingAchievements.Contains(id))
                {
                    return id;
                }
            }

            return null;
        }

        /// <summary>
        /// Records that the player answered the celebration of a final achievement (and clears its pending mark in the same save), so it never opens again until NEW GAME. Saves, raises no events and changes no
        /// gold, treasure, unlock or area-celebration data. Refused for an unknown achievement or one whose goal is not reached; marking twice is fine.
        /// </summary>
        /// <returns>true if the achievement is recorded as celebrated (and the save succeeded when something changed).</returns>
        public bool MarkAchievementCelebrated(string achievementId)
        {
            if (!FinalAchievements.IsKnown(achievementId) || !IsAchievementReached(achievementId))
            {
                return false;
            }

            bool wasPending = pendingAchievements.Remove(achievementId);
            if (wasPending)
            {
                data.pendingAchievementIds.Remove(achievementId);
            }

            if (!celebratedAchievements.Add(achievementId))
            {
                return !wasPending || Save();
            }

            data.celebratedAchievementIds.Add(achievementId);
            return Save();
        }

        /// <summary>A COLLECT just crossed a goal: its celebration is owed. Only the first crossing counts (never for an achievement that is already owed or answered). Written by the caller's save.</summary>
        private void OweAchievement(string achievementId)
        {
            if (celebratedAchievements.Contains(achievementId) || !pendingAchievements.Add(achievementId))
            {
                return;
            }

            data.pendingAchievementIds.Add(achievementId);
        }

        /// <summary>How many of the given treasure ids were collected (by id, so it never depends on gold). Blank ids are ignored.</summary>
        public int CountCollected(IEnumerable<string> treasureIds)
        {
            int count = 0;
            if (treasureIds != null)
            {
                foreach (string id in treasureIds)
                {
                    if (IsCollected(id))
                    {
                        count++;
                    }
                }
            }

            return count;
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

            bool goalWasReached = IsGameCompleteReached;
            bool collectionWasComplete = IsCollectionComplete;

            collected.Add(id);
            data.collectedTreasureIds.Add(id);
            data.totalGold = (int)Math.Min((long)data.totalGold + gold, MaxTotalGold);

            List<(string areaId, bool isUnlock)> raised = EvaluateThresholds();

            // Stage 17D: crossing 2500 gold, or the 60th treasure, owes the final celebration. It is written in the SAME save as the gold and the treasure id, so a crash or a quit right after the COLLECT cannot
            // lose it. Only the crossing counts: a save that was already past a goal before this version never celebrates it, and gold above the goal is kept as it is.
            if (!goalWasReached && IsGameCompleteReached)
            {
                OweAchievement(FinalAchievements.GameComplete);
            }

            if (!collectionWasComplete && IsCollectionComplete)
            {
                OweAchievement(FinalAchievements.Collection);
            }

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

        /// <summary>Records the tutorial state and saves (nothing else changes; no events). Returns whether the save succeeded.</summary>
        public bool SetTutorial(TutorialStatus status)
        {
            if (Tutorial == status && data.tutorialState == (int)status)
            {
                return true;
            }

            data.tutorialState = (int)status;
            return Save();
        }

        /// <summary>
        /// The last treasure of an area was collected: its LEVEL COMPLETE celebration is now owed to the player and stays owed (in the save) until <see cref="MarkCompletionCelebrated"/>. Saves, raises no events and
        /// changes no gold, treasure or unlock data. Refused for an unknown or already celebrated area; marking twice is harmless.
        /// </summary>
        /// <returns>true if the celebration is recorded as pending (and the save succeeded when something changed).</returns>
        public bool MarkCompletionPending(string areaId)
        {
            if (string.IsNullOrWhiteSpace(areaId) || Catalog.Get(areaId) == null || celebrated.Contains(areaId))
            {
                return false;
            }

            if (!pendingCelebration.Add(areaId))
            {
                return true;
            }

            data.pendingCelebrationAreaIds.Add(areaId);
            return Save();
        }

        /// <summary>
        /// Records that the player answered the LEVEL COMPLETE celebration of an area (and clears its pending mark in the same save), so it never opens again until the next NEW GAME. Saves, raises no events and
        /// changes no gold, treasure or unlock data. Unknown areas are refused; marking twice is fine.
        /// </summary>
        /// <returns>true if the area is recorded as celebrated (and the save succeeded when something changed).</returns>
        public bool MarkCompletionCelebrated(string areaId)
        {
            if (string.IsNullOrWhiteSpace(areaId) || Catalog.Get(areaId) == null)
            {
                return false;
            }

            bool wasPending = pendingCelebration.Remove(areaId);
            if (wasPending)
            {
                data.pendingCelebrationAreaIds.Remove(areaId);
            }

            if (!celebrated.Add(areaId))
            {
                return !wasPending || Save();
            }

            data.celebratedAreaIds.Add(areaId);
            return Save();
        }

        public bool Save()
        {
            bool written = store.Write(JsonUtility.ToJson(data));
            if (written)
            {
                HasSave = true;
            }

            return written;
        }

        /// <summary>Erases the saved progress (tools / tests / a future "new game"). Raises no area events.</summary>
        public void ResetAll()
        {
            store.Delete();
            ApplyData(NewData());
            HasSave = false;
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
            fresh.tutorialState = (int)TutorialStatus.Pending; // a new game gets the Green Mine tutorial (stage 17B)
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
            HasSave = true;
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
            HasSave = LoadStatus != ProgressLoadStatus.NewGame; // no file at all = a first launch; a file with 0 gold is a game in progress

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
            celebrated.Clear();
            pendingCelebration.Clear();
            pendingAchievements.Clear();
            celebratedAchievements.Clear();
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

            foreach (string id in data.celebratedAreaIds)
            {
                celebrated.Add(id);
            }

            foreach (string id in data.pendingCelebrationAreaIds)
            {
                pendingCelebration.Add(id);
            }

            data.pendingAchievementIds ??= new List<string>();
            data.celebratedAchievementIds ??= new List<string>();
            foreach (string id in data.pendingAchievementIds)
            {
                pendingAchievements.Add(id);
            }

            foreach (string id in data.celebratedAchievementIds)
            {
                celebratedAchievements.Add(id);
            }
        }

        /// <summary>Makes loaded data safe and consistent. Returns a description of what was repaired, or null if nothing was.</summary>
        private string Sanitize(GameProgressData d)
        {
            var notes = new List<string>();

            d.tutorialState = Mathf.Clamp(d.tutorialState, 0, (int)TutorialStatus.Finished);
            d.collectedTreasureIds ??= new List<string>();
            d.unlockedAreaIds ??= new List<string>();
            d.completedAreaIds ??= new List<string>();

            // Celebration flags (17C): a save from before it has none. Tidied silently (no "repair": an old save must load exactly as it was and is never rewritten just for this).
            var cleanCelebrated = new List<string>();
            foreach (string raw in d.celebratedAreaIds ?? new List<string>())
            {
                string id = raw?.Trim();
                if (!string.IsNullOrEmpty(id) && Catalog.Get(id) != null && !cleanCelebrated.Contains(id))
                {
                    cleanCelebrated.Add(id);
                }
            }

            d.celebratedAreaIds = cleanCelebrated;

            // Pending celebrations (a save from before has none): known areas only, once each, and never for an area that was already celebrated. Also tidied silently.
            var cleanPending = new List<string>();
            foreach (string raw in d.pendingCelebrationAreaIds ?? new List<string>())
            {
                string id = raw?.Trim();
                if (!string.IsNullOrEmpty(id) && Catalog.Get(id) != null && !cleanCelebrated.Contains(id) && !cleanPending.Contains(id))
                {
                    cleanPending.Add(id);
                }
            }

            d.pendingCelebrationAreaIds = cleanPending;

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

            TidyAchievements(d);

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

        /// <summary>
        /// Final achievement flags (17D; a save from before has none): known ids only, once each. A celebration can only be owed while its goal is reached and it was not answered yet (an owed record for a goal
        /// that is not reached, or for an answered one, is dropped). Tidied silently, like the area flags of 17C: an old save is never "repaired" or rewritten for this and nothing is ever added here by loading.
        /// </summary>
        private static void TidyAchievements(GameProgressData d)
        {
            var celebratedIds = new List<string>();
            foreach (string raw in d.celebratedAchievementIds ?? new List<string>())
            {
                string id = raw?.Trim();
                if (FinalAchievements.IsKnown(id) && !celebratedIds.Contains(id))
                {
                    celebratedIds.Add(id);
                }
            }

            d.celebratedAchievementIds = celebratedIds;

            int collectedKnown = 0;
            var seenKnown = new HashSet<string>();
            foreach (string id in d.collectedTreasureIds)
            {
                if (TreasureCatalog.IsKnown(id) && seenKnown.Add(id))
                {
                    collectedKnown++;
                }
            }

            var pendingIds = new List<string>();
            foreach (string raw in d.pendingAchievementIds ?? new List<string>())
            {
                string id = raw?.Trim();
                bool reached = id == FinalAchievements.GameComplete ? d.totalGold >= FinalAchievements.Goal
                    : id == FinalAchievements.Collection && collectedKnown >= TreasureCatalog.TotalCount;
                if (FinalAchievements.IsKnown(id) && reached && !celebratedIds.Contains(id) && !pendingIds.Contains(id))
                {
                    pendingIds.Add(id);
                }
            }

            d.pendingAchievementIds = pendingIds;
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
