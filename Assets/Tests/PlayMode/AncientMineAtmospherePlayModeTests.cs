#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ItemCollector3D.Audio;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using ItemCollector3D.UI;
using ItemCollector3D.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Ancient Mine atmosphere (stage 16D): the five areas are told apart and named; the ambience has one cave bed + one detail loop per area + the grotto's water, cross-fades in about two seconds
    /// without jumps or cut-offs, survives quick back-and-forth, follows the SFX setting, leaves the music alone, pauses with the game, steps back for the detector / DIG / COLLECT and stays far
    /// below the detector beep; the crystal glow is ONE manager with slow, staggered, shared-material animation; the particle places are tiny, capped and only active nearby; the area-name banner
    /// shows once per entry without spam and out of the HUD's way; the Royal Vault announces itself on the first entry only; the 25 treasures / 1240 gold are untouched; both miners walk the main
    /// route with the atmosphere running. In-memory stores and settings everywhere; the developer's real save file is hashed before the first and after the last test. What the sound and the
    /// light LOOK and SOUND like is not decided by these tests.
    /// </summary>
    public sealed class AncientMineAtmospherePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/AncientMine.unity";

        // 1200 gold and the area unlocked, so DIG / COLLECT really work here (in memory only)
        private const string OpenMineSave = "{\"version\":1,\"totalGold\":1200,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\",\"ancient_mine\"],\"completedAreaIds\":[]}";

        private static string realSavePath;
        private static string realSaveHashBefore;

        private Scene scene;
        private float previousTimeScale;
        private MemoryProgressStore store;

        private static string HashOfRealSave()
        {
            if (!File.Exists(realSavePath))
            {
                return "(no file)";
            }

            using SHA256 sha = SHA256.Create();
            return System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(realSavePath)));
        }

        [OneTimeSetUp]
        public void HashTheRealSave()
        {
            realSavePath = Path.Combine(Application.persistentDataPath, "item_collector_progress.json");
            realSaveHashBefore = HashOfRealSave();
        }

        [OneTimeTearDown]
        public void RealSaveIsUntouched()
        {
            Assert.That(HashOfRealSave(), Is.EqualTo(realSaveHashBefore), "the developer's real save file must be byte-for-byte unchanged by these tests");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            GamePause.Resume();
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            scene = default;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.StoreFactoryOverride = null;
            GameSettings.ResetCurrent();
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator Load(string miner = null)
        {
            if (miner != null)
            {
                CharacterSelection.Select(miner);
            }

            store = new MemoryProgressStore(OpenMineSave);
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null, "Could not load " + ScenePath);
            yield return load;
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
            Physics.SyncTransforms();
        }

        private GameObject RootObject(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private T One<T>() where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            Assert.Fail("Not found: " + typeof(T).Name);
            return null;
        }

        private Button FindButton(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.name == name)
                    {
                        return button;
                    }
                }
            }

            Assert.Fail("Button not found: " + name);
            return null;
        }

        private Transform Env => RootObject("AncientMineEnvironment").transform;
        private Transform Player => RootObject("Player").transform;

        private static readonly string[] ZoneRoots = { "A_AncientGate", "B_HallOfPillars", "C_CrystalCaverns", "D_ForgottenWorkshop", "E_RoyalVault" };

        private Vector3 AnchorOf(MineZone zone)
        {
            Transform t = Env.Find(ZoneRoots[(int)zone] + "/Anchor");
            Assert.That(t, Is.Not.Null, "anchor of " + zone);
            return t.position;
        }

        /// <summary>The walkable floor at (x, z): the highest "Floor" collider under y = 60.</summary>
        private static float FloorY(float x, float z)
        {
            float best = float.NaN;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.name == "Floor" && (float.IsNaN(best) || hit.point.y > best))
                {
                    best = hit.point.y;
                }
            }

            return best;
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private void PlaceAt(float x, float z)
        {
            float y = FloorY(x, z);
            Assert.That(float.IsNaN(y), Is.False, $"a floor at {x:F1},{z:F1}");
            Teleport(Player, new Vector3(x, y + 1.05f, z));
        }

        private void PlaceInZone(MineZone zone)
        {
            Vector3 a = AnchorOf(zone);
            PlaceAt(a.x, a.z);
        }

        /// <summary>Teleports into an area and waits long enough for it to become the current one and for the audio to settle.</summary>
        private IEnumerator Go(MineZone zone, float seconds)
        {
            PlaceInZone(zone);
            One<MineZoneTracker>().Refresh(true);
            yield return new WaitForSecondsRealtime(seconds);
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        private static float[] Samples(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(data, 0), Is.True, clip.name + " must be readable");
            return data;
        }

        /// <summary>RMS of a signal after a crude 500 .. 1600 Hz band-pass: the band the detector beep lives in.</summary>
        private static float BeepBandRms(float[] data, int rate)
        {
            float lowCoeff = 1f - Mathf.Exp(-2f * Mathf.PI * 1600f / rate);
            float highCoeff = 1f - Mathf.Exp(-2f * Mathf.PI * 500f / rate);
            float lp = 0f, hp = 0f, sum = 0f;
            foreach (float x in data)
            {
                lp += lowCoeff * (x - lp);
                hp += highCoeff * (lp - hp);
                float band = lp - hp;
                sum += band * band;
            }

            return Mathf.Sqrt(sum / data.Length);
        }

        private static Rect Rectangle(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        // ================================================================ areas

        [UnityTest]
        public IEnumerator FiveAreas_AreToldApart_AndNamed_AndEveryAreaIsActivated()
        {
            yield return Load();
            var tracker = One<MineZoneTracker>();
            Assert.That(MineZoneTracker.DisplayNames, Is.EqualTo(new[] { "ANCIENT GATE", "HALL OF PILLARS", "CRYSTAL CAVERNS", "FORGOTTEN WORKSHOP", "ROYAL VAULT" }));
            yield return null;
            tracker.Refresh(true);
            Assert.That(tracker.HasZone, Is.True);
            Assert.That(tracker.Current, Is.EqualTo(MineZone.Gate), "the miner starts at the Ancient Gate");

            var ambience = One<MineAmbience>();
            var glow = One<MineCrystalGlow>();
            foreach (MineZone zone in System.Enum.GetValues(typeof(MineZone)))
            {
                yield return Go(zone, 0.3f);
                Assert.That(tracker.Current, Is.EqualTo(zone), "the anchor of " + zone);
                Assert.That(tracker.Weight(zone), Is.EqualTo(1f));
                Assert.That(One<CaveZoneMood>().CurrentZone, Is.EqualTo(ZoneRoots[(int)zone]), "the lighting mood reads the same area");
            }

            // every area has its own sound profile and its own glow entries
            for (int z = 0; z < MineZoneTracker.ZoneCount; z++)
            {
                Assert.That(MineAmbience.DetailGain[z], Is.GreaterThan(0f));
            }

            Assert.That(MineAmbience.BedCutoff[(int)MineZone.Vault], Is.LessThan(MineAmbience.BedCutoff[(int)MineZone.Gate] * 0.5f), "the vault is dull and deep, the gate open");
            Assert.That(MineAmbience.BedPitch[(int)MineZone.Vault], Is.LessThan(MineAmbience.BedPitch[(int)MineZone.Caverns]));
            var glowZones = new HashSet<MineZone>();
            for (int i = 0; i < glow.EntryCount; i++)
            {
                glowZones.Add(glow.GetEntry(i).zone);
            }

            Assert.That(glowZones.Count, Is.EqualTo(5), "every area glows");
            Assert.That(ambience.IsReady, Is.True);
        }

        // ================================================================ sound structure, cross-fade, flicker

        [UnityTest]
        public IEnumerator Ambience_SevenSources_CrossFadeInTwoSeconds_WithoutJumps_AndQuickBackAndForthNeverCutsASound()
        {
            yield return Load();
            var ambience = One<MineAmbience>();
            var tracker = One<MineZoneTracker>();
            yield return WaitUntil(() => ambience.IsReady, 2f);
            Assert.That(ambience.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(7), "one bed + five details + the grotto water: nothing else");
            Assert.That(MineAmbience.CrossfadeSeconds, Is.EqualTo(2f));

            yield return Go(MineZone.Hall, 3.2f);
            AudioSource hall = ambience.DetailSource(MineZone.Hall), caverns = ambience.DetailSource(MineZone.Caverns);
            Assert.That(hall.isPlaying, Is.True);
            Assert.That(caverns.isPlaying, Is.False, "an area that is not audible does not run");
            Assert.That(ambience.PlayingSourceCount, Is.LessThanOrEqualTo(3), "bed + one detail (+ the grotto water only near it)");

            // quick back and forth between two far-apart areas: the area never changes (it needs 0.4 s on the new floor) and nothing is cut
            int entries = tracker.EntryCount;
            for (int i = 0; i < 12; i++)
            {
                PlaceInZone(i % 2 == 0 ? MineZone.Caverns : MineZone.Hall);
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(hall.isPlaying, Is.True, "the Hall loop is never cut by flicker");
                Assert.That(ambience.BedSource.isPlaying, Is.True);
            }

            PlaceInZone(MineZone.Hall);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(tracker.Current, Is.EqualTo(MineZone.Hall));
            Assert.That(tracker.EntryCount - entries, Is.LessThanOrEqualTo(1), "walking back and forth over a border does not register a string of entries");

            // a real change: the weights move at 1 / 2 s, the volumes never jump, the sum of the two details keeps its level (no dip)
            yield return new WaitForSecondsRealtime(2.6f);
            float previousBed = ambience.BedSource.volume, previousHall = hall.volume, previousCaverns = caverns.volume;
            float biggestStep = 0f, lowestSum = float.MaxValue, highestSum = 0f, crossedAt = -1f;
            PlaceInZone(MineZone.Caverns);
            float start = Time.realtimeSinceStartup;
            float elapsed = 0f;
            while (elapsed < 4.2f)
            {
                yield return null;
                elapsed = Time.realtimeSinceStartup - start;
                biggestStep = Mathf.Max(biggestStep, Mathf.Abs(ambience.BedSource.volume - previousBed), Mathf.Abs(hall.volume - previousHall), Mathf.Abs(caverns.volume - previousCaverns));
                previousBed = ambience.BedSource.volume;
                previousHall = hall.volume;
                previousCaverns = caverns.volume;
                float w = ambience.SmoothedWeight(MineZone.Caverns);
                if (elapsed > 0.5f && elapsed < 2.4f)
                {
                    float sum = ambience.SmoothedWeight(MineZone.Hall) + w;
                    lowestSum = Mathf.Min(lowestSum, sum);
                    highestSum = Mathf.Max(highestSum, sum);
                }

                if (crossedAt < 0f && w >= 0.5f)
                {
                    crossedAt = elapsed;
                }

                Assert.That(hall.isPlaying || ambience.SmoothedWeight(MineZone.Hall) < 0.01f, Is.True, "the old loop runs until it has faded out");
            }

            Assert.That(biggestStep, Is.LessThan(0.02f), "no volume jumps: the biggest change in one frame");
            Assert.That(lowestSum, Is.GreaterThan(0.95f), "the two area weights always add up to one: no dip in the cross-fade");
            Assert.That(highestSum, Is.LessThan(1.05f));
            Assert.That(crossedAt, Is.InRange(0.4f + 0.7f, 0.4f + 1.5f), "half way through the 2 s fade (after the 0.4 s settling time)");
            Assert.That(hall.isPlaying, Is.False, "and the old loop stops once it is silent");
            Assert.That(caverns.isPlaying, Is.True);
            Debug.Log($"Ancient Mine cross-fade: biggest per-frame step {biggestStep:F4}, weight sum {lowestSum:F3}..{highestSum:F3}, half way at {crossedAt:F2} s");
        }

        [UnityTest]
        public IEnumerator Ambience_FollowsTheSfxSetting_LeavesTheMusicAlone_AndPausesWithTheGame()
        {
            yield return Load();
            var ambience = One<MineAmbience>();
            var music = One<AmbientMusicPlayer>();
            yield return WaitUntil(() => ambience.IsReady, 2f);
            yield return Go(MineZone.Vault, 3.5f);
            AudioSource bed = ambience.BedSource, detail = ambience.DetailSource(MineZone.Vault);
            float bedFull = bed.volume, detailFull = detail.volume, musicFull = music.Source.volume;
            yield return new WaitForSecondsRealtime(6f); // the first-entry swell is gone
            bedFull = bed.volume;
            detailFull = detail.volume;
            Assert.That(bedFull, Is.GreaterThan(0.02f));
            Assert.That(music.Source.ignoreListenerPause, Is.True, "the music keeps playing while paused");
            Assert.That(bed.ignoreListenerPause, Is.False);

            GameSettings.Current.SetSfxVolume(0.5f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(bed.volume, Is.EqualTo(bedFull * 0.5f).Within(bedFull * 0.12f), "half the SFX volume, half the bed");
            Assert.That(detail.volume, Is.EqualTo(detailFull * 0.5f).Within(detailFull * 0.12f + 0.002f));
            GameSettings.Current.SetSfxVolume(0f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(bed.volume, Is.EqualTo(0f).Within(0.0005f), "SFX volume 0: silence");
            Assert.That(detail.volume, Is.EqualTo(0f).Within(0.0005f));
            Assert.That(music.Source.volume, Is.EqualTo(musicFull).Within(0.0001f), "the SFX slider never moves the music");
            GameSettings.Current.SetSfxVolume(1f);
            GameSettings.Current.SetMusicVolume(0.2f);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(bed.volume, Is.EqualTo(bedFull).Within(bedFull * 0.12f), "back to full SFX volume");

            int a = bed.timeSamples;
            yield return new WaitForSecondsRealtime(0.4f);
            Assume.That(bed.timeSamples, Is.Not.EqualTo(a), "the audio engine is running in this environment");
            GamePause.Pause();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(AudioListener.pause, Is.True);
            int pausedAt = bed.timeSamples;
            float volumeWhilePaused = bed.volume;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(bed.timeSamples, Is.EqualTo(pausedAt).Within(2048), "the bed does not advance while the game is paused");
            Assert.That(bed.volume, Is.EqualTo(volumeWhilePaused).Within(0.0001f));
            Assert.That(music.IsPlaying, Is.True, "the music keeps playing");
            GamePause.Resume();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(bed.timeSamples, Is.Not.EqualTo(pausedAt), "and continues after Resume");
            Assert.That(bed.isPlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator Ambience_StepsBackForTheDetector_TheDig_AndTheReward_AndStaysFarUnderTheBeep()
        {
            yield return Load();
            var ambience = One<MineAmbience>();
            var hunt = One<TreasureHuntController>();
            var indicator = One<DetectorSignalIndicator>();
            yield return WaitUntil(() => ambience.IsReady, 2f);

            // measurable: the loudest possible ambience (bed, the loudest detail with the vault swell, the grotto water) in the beep's own band, against the weakest beep
            var so = new SerializedObject(indicator);
            float weakestBeep = so.FindProperty("weakestBeepVolume").floatValue * DetectorSignalIndicator.BeepGain;
            float beepBand = BeepBandRms(Samples(SoundSynth.Beep()), SoundSynth.SfxRate) * weakestBeep;
            float bedBand = BeepBandRms(Samples(ambience.BedSource.clip), 22050) * MineAmbience.BaseVolume * Mathf.Max(MineAmbience.BedGain);
            float loudestDetail = 0f;
            foreach (MineZone zone in System.Enum.GetValues(typeof(MineZone)))
            {
                float d = BeepBandRms(Samples(ambience.DetailSource(zone).clip), 22050) * MineAmbience.BaseVolume * MineAmbience.DetailGain[(int)zone] * (zone == MineZone.Vault ? 1f + MineAmbience.VaultSwellBoost : 1f);
                loudestDetail = Mathf.Max(loudestDetail, d);
            }

            float waterBand = BeepBandRms(Samples(ambience.WaterSource.clip), 22050) * MineAmbience.BaseVolume * MineAmbience.WaterGain;
            float together = Mathf.Sqrt(bedBand * bedBand + loudestDetail * loudestDetail + waterBand * waterBand);
            Debug.Log($"Ancient Mine ambience vs beep (band 500..1600 Hz): bed {bedBand:F4}, loudest detail {loudestDetail:F4}, water {waterBand:F4}, together {together:F4}, weakest beep {beepBand:F4}");
            Assert.That(together, Is.LessThan(0.35f * beepBand), $"in the beep's band the whole ambience ({together:F4}) stays at least 9 dB below the weakest beep ({beepBand:F4})");
            Assert.That(MineAmbience.LoudestPossible, Is.LessThan(0.55f), "overall, the ambience never reaches the loudness of the sound effects (0.7 for the dig, 0.7 for the collect)");

            // the detector, the dig and the reward make the ambience step back
            TreasureSpot spot = null;
            foreach (TreasureSpot s in RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true))
            {
                if (s.SpotId == "ancient_mine.gate_01")
                {
                    spot = s;
                }
            }

            Assert.That(spot, Is.Not.Null);
            Button dig = FindButton("DigButton"), collect = FindButton("CollectButton");
            foreach (TreasureSpot other in RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true))
            {
                if (other != spot)
                {
                    other.gameObject.SetActive(false); // neighbouring detection circles overlap: only this treasure may speak
                }
            }

            yield return Go(MineZone.Hall, 2f);
            Assert.That(ambience.Duck, Is.EqualTo(1f).Within(0.01f), "no treasure near: the normal level");
            Vector3 p = spot.transform.position + new Vector3(0.8f, 0f, 0f);
            PlaceAt(p.x, p.z);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(ambience.Duck, Is.EqualTo(MineAmbience.RapidDuck).Within(0.03f), "RAPID: the ambience steps back so the detector beep stays on top");

            typeof(TreasureHuntController).GetField("digDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(hunt, 1.0f);
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            dig.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(hunt.IsDigging, Is.True);
            Assert.That(ambience.Duck, Is.EqualTo(MineAmbience.DiggingDuck).Within(0.03f), "ducked while digging");
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            collect.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(ambience.Duck, Is.LessThanOrEqualTo(MineAmbience.RewardDuck + 0.03f), "still ducked right after the reward");
            Assert.That(One<ProgressManager>().Service.TotalGold, Is.EqualTo(1210), "the reward was paid as usual (a gold coin, 10 gold on top of the 1200 saved)");
        }

        [Test]
        public void TheLoops_AreSoftBoundedAndSeamless()
        {
            foreach ((string name, System.Func<AudioClip> make, float seconds) in new (string, System.Func<AudioClip>, float)[]
            {
                ("bed", SoundSynth.MineBed, SoundSynth.MineBedSeconds),
                ("gate", SoundSynth.GateDetail, SoundSynth.GateSeconds),
                ("hall", SoundSynth.HallDetail, SoundSynth.HallSeconds),
                ("caverns", SoundSynth.CavernsDetail, SoundSynth.CavernsSeconds),
                ("workshop", SoundSynth.WorkshopDetail, SoundSynth.WorkshopSeconds),
                ("vault", SoundSynth.VaultDetail, SoundSynth.VaultSeconds),
                ("grotto water", SoundSynth.GrottoWater, SoundSynth.GrottoSeconds),
            })
            {
                AudioClip clip = make();
                float[] data = Samples(clip);
                float peak = 0f, sum = 0f;
                foreach (float f in data)
                {
                    Assert.That(float.IsFinite(f), Is.True, name + ": no NaN / infinity");
                    peak = Mathf.Max(peak, Mathf.Abs(f));
                    sum += f * f;
                }

                float rms = Mathf.Sqrt(sum / data.Length);
                Debug.Log($"Ancient Mine loop {name}: {clip.length:F1} s, peak {peak:F2}, rms {rms:F3}");
                Assert.That(clip.length, Is.EqualTo(seconds).Within(0.01f), name);
                Assert.That(clip.frequency, Is.EqualTo(22050), name + ": cheap mono loop");
                Assert.That(clip.channels, Is.EqualTo(1), name);
                Assert.That(peak, Is.InRange(0.5f, 0.65f), name + ": peak-normalised below the sound effects");
                Assert.That(rms, Is.InRange(0.02f, 0.2f), name + ": a calm bed, not a wall of noise");
                float biggest = 0f;
                for (int i = 1; i < data.Length; i++)
                {
                    biggest = Mathf.Max(biggest, Mathf.Abs(data[i] - data[i - 1]));
                }

                Assert.That(Mathf.Abs(data[0] - data[data.Length - 1]), Is.LessThanOrEqualTo(biggest), name + ": the loop point is no click");
                Object.DestroyImmediate(clip);
            }
        }

        // ================================================================ crystals

        [UnityTest]
        public IEnumerator CrystalGlow_IsOneManager_SlowAndStaggered_OnSharedMaterials_WithNoLights()
        {
            yield return Load();
            var glows = Object.FindObjectsByType<MineCrystalGlow>(FindObjectsInactive.Include);
            Assert.That(glows.Length, Is.EqualTo(1), "ONE manager for the whole map, no per-crystal component");
            MineCrystalGlow glow = glows[0];
            Assert.That(glow.EntryCount, Is.EqualTo(8), "5 crystal meshes + the gold inlay of the hall, the caverns and the vault");

            // the model: slow, never dark, staggered, strongest in the caverns and the vault
            var crystalPhases = new List<float>();
            float calm = 0f, bold = 0f;
            for (int i = 0; i < glow.EntryCount; i++)
            {
                MineCrystalGlow.Entry e = glow.GetEntry(i);
                Assert.That(e.renderer, Is.Not.Null);
                Assert.That(e.period, Is.GreaterThanOrEqualTo(6f), "a breath takes at least six seconds");
                float minL = float.MaxValue, maxL = 0f, fastest = 0f, previous = -1f;
                for (float t = 0f; t < 120f; t += 0.1f)
                {
                    Color c = MineCrystalGlow.Evaluate(e, t, 0f);
                    float l = (c.r + c.g + c.b) / 3f;
                    minL = Mathf.Min(minL, l);
                    maxL = Mathf.Max(maxL, l);
                    if (previous >= 0f)
                    {
                        fastest = Mathf.Max(fastest, Mathf.Abs(l - previous) / 0.1f);
                    }

                    previous = l;
                }

                Assert.That(minL, Is.GreaterThan(0.45f), $"entry {i} never goes dark");
                Assert.That(maxL, Is.LessThan(1.9f), $"entry {i} never blows out");
                Assert.That(maxL - minL, Is.GreaterThan(0.05f), $"entry {i} really breathes");
                Assert.That(fastest, Is.LessThan(0.35f), $"entry {i} changes slowly (per second)");
                if (e.materialIndex < 0)
                {
                    crystalPhases.Add(e.phase);
                    if (e.zone == MineZone.Caverns || e.zone == MineZone.Vault)
                    {
                        bold = Mathf.Max(bold, e.swing);
                    }
                    else
                    {
                        calm = Mathf.Max(calm, e.swing);
                    }
                }
            }

            Assert.That(bold, Is.GreaterThan(calm * 1.8f), "the caverns and the vault glow stronger than the other areas");
            for (int a = 0; a < crystalPhases.Count; a++)
            {
                for (int b = a + 1; b < crystalPhases.Count; b++)
                {
                    float diff = Mathf.Abs(Mathf.DeltaAngle(crystalPhases[a] * Mathf.Rad2Deg, crystalPhases[b] * Mathf.Rad2Deg));
                    Assert.That(diff, Is.GreaterThan(30f), "the areas never glow in step");
                }
            }

            // the channels drift apart: blue, violet and turquoise crystals of the same mesh do not peak together
            MineCrystalGlow.Entry caverns = null;
            for (int i = 0; i < glow.EntryCount; i++)
            {
                if (glow.GetEntry(i).zone == MineZone.Caverns && glow.GetEntry(i).materialIndex < 0)
                {
                    caverns = glow.GetEntry(i);
                }
            }

            float apart = 0f;
            for (float t = 0f; t < 60f; t += 0.5f)
            {
                Color c = MineCrystalGlow.Evaluate(caverns, t, 0f);
                apart = Mathf.Max(apart, Mathf.Abs(c.r - c.g));
            }

            Assert.That(apart, Is.GreaterThan(0.25f), "the red and green channels drift apart in the caverns");

            // runtime: only near renderers are written, at a low rate, through property blocks; materials and lights unchanged
            yield return Go(MineZone.Caverns, 1.5f);
            int writes0 = glow.WriteCount;
            yield return new WaitForSecondsRealtime(1f);
            int perSecond = glow.WriteCount - writes0;
            Assert.That(perSecond, Is.InRange(5, 8 * 15 + 10), "at most about 15 updates a second per renderer, and not every renderer is near");
            for (int i = 0; i < glow.EntryCount; i++)
            {
                MineCrystalGlow.Entry e = glow.GetEntry(i);
                if (e.zone == MineZone.Caverns)
                {
                    Assert.That(glow.LastColor(i), Is.Not.EqualTo(Color.white), "the caverns glow is running");
                }

                if (e.zone == MineZone.Vault && e.renderer.bounds.SqrDistance(Player.position) > 62f * 62f)
                {
                    Assert.That(glow.LastColor(i), Is.EqualTo(Color.white), "a renderer far from the player is left alone");
                }
            }

            var names = new HashSet<string>();
            foreach (Renderer r in Env.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    names.Add(m.name);
                }
            }

            Assert.That(names.Count, Is.EqualTo(2), "still just AM_Palette and AM_Glow: " + string.Join(", ", names));
            int enabledLights = 0;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                enabledLights += l.enabled ? 1 : 0;
            }

            Assert.That(enabledLights, Is.EqualTo(1), "still only the sun: no real-time light was added");
        }

        // ================================================================ particles

        [UnityTest]
        public IEnumerator Motes_AreTiny_Capped_SharedMaterial_AndOnlyActiveWhereThePlayerIs()
        {
            yield return Load();
            MineMotes[] places = RootObject("MineAtmosphere").GetComponentsInChildren<MineMotes>(true);
            Assert.That(places.Length, Is.EqualTo(4), "crystal caverns, grotto, workshop, royal vault");
            Assert.That(places.Sum(m => m.MaxParticles), Is.LessThanOrEqualTo(52), "the live specks of the whole map are capped");
            var materials = new HashSet<Material>();
            foreach (MineMotes m in places)
            {
                ParticleSystem ps = m.Particles;
                Assert.That(ps.main.maxParticles, Is.EqualTo(m.MaxParticles));
                Assert.That(ps.main.startSize.constantMax, Is.LessThanOrEqualTo(0.13f), m.name + ": specks are a few centimetres");
                Assert.That(ps.collision.enabled, Is.False);
                Assert.That(ps.trails.enabled, Is.False);
                Assert.That(ps.subEmitters.enabled, Is.False);
                Assert.That(ps.lights.enabled, Is.False);
                Assert.That(m.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0), "specks never block the walk or the camera");
                var pr = ps.GetComponent<ParticleSystemRenderer>();
                Assert.That(pr.sharedMaterial.GetFloat("_Surface"), Is.EqualTo(0f), "opaque: no transparent overdraw");
                materials.Add(pr.sharedMaterial);
            }

            Assert.That(materials.Count, Is.EqualTo(1), "one shared material");

            // far from every place nothing runs
            yield return Go(MineZone.Gate, 2.5f);
            foreach (MineMotes m in places)
            {
                Assert.That(m.Particles.particleCount, Is.EqualTo(0), m.name + " is idle at the gate");
                Assert.That(m.CurrentRate, Is.EqualTo(0f));
            }

            // in the vault: its sparks float, never more than their cap, and they are the only ones
            yield return Go(MineZone.Vault, 9f);
            MineMotes vault = places.First(m => m.Zone == MineZone.Vault);
            Assert.That(vault.Particles.particleCount, Is.InRange(1, vault.MaxParticles), "a few slowly floating sparks");
            int live = 0;
            foreach (MineMotes m in places)
            {
                live += m.Particles.particleCount;
            }

            Assert.That(live, Is.LessThanOrEqualTo(vault.MaxParticles + 4), "and nobody else is emitting in the vault: " + live);

            // the workshop dust follows the player but only inside the workshop
            yield return Go(MineZone.Workshop, 10f);
            MineMotes dust = places.First(m => m.Zone == MineZone.Workshop);
            Assert.That(dust.Particles.particleCount, Is.InRange(1, dust.MaxParticles));
            yield return Go(MineZone.Hall, 3f);
            Assert.That(dust.CurrentRate, Is.EqualTo(0f), "no dust outside the workshop");
            Debug.Log($"Ancient Mine motes: caps {string.Join("+", places.Select(m => m.MaxParticles))} = {places.Sum(m => m.MaxParticles)}");
        }

        // ================================================================ the area banner

        [UnityTest]
        public IEnumerator AreaNames_ShowOncePerEntry_WithoutSpam_OutOfTheHudsWay_AndWaitWhilePaused()
        {
            yield return Load();
            var notice = One<ZoneEntryNotice>();
            NotificationBanner banner = notice.Banner;
            var progressBanner = One<ProgressNotificationPresenter>().GetComponentInChildren<NotificationBanner>(true);
            Assert.That(banner, Is.Not.EqualTo(progressBanner), "the area banner is its own instance");
            Assert.That(banner.TotalDuration, Is.InRange(2.0f, 3.0f), "about two and a half seconds");

            yield return WaitUntil(() => banner.IsShowing, 3f);
            Assert.That(banner.IsShowing, Is.True);
            Assert.That(banner.CurrentTitle, Is.EqualTo("ANCIENT GATE"));
            var group = banner.GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts, Is.False, "never takes a touch");
            Assert.That(group.interactable, Is.False);

            Rect box = Rectangle((RectTransform)banner.transform);
            Transform canvas = banner.GetComponentInParent<Canvas>().rootCanvas.transform;
            Transform hud = canvas.Find("SafeAreaRoot/HUD");
            foreach (string name in new[] { "LevelPlate", "HeaderPanel", "ActionButtons" })
            {
                Assert.That(box.Overlaps(Rectangle((RectTransform)hud.Find(name))), Is.False, "the area banner does not cover " + name);
            }

            foreach (RectTransform rt in canvas.Find("SafeAreaRoot/DetectorIndicator").GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.GetComponent<Graphic>() != null)
                {
                    Assert.That(box.Overlaps(Rectangle(rt)), Is.False, "the area banner does not cover the detector (" + rt.name + ")");
                }
            }

            Assert.That(box.Overlaps(Rectangle((RectTransform)canvas.Find("SafeAreaRoot/MobileControls/MovementJoystick"))), Is.False, "nor the joystick");
            Rect safe = Rectangle((RectTransform)canvas.Find("SafeAreaRoot"));
            Assert.That(safe.Contains(box.min) && safe.Contains(box.max), Is.True, "inside the safe area, and the longest name fits: " + box);
            yield return new WaitForSecondsRealtime(banner.TotalDuration + 0.4f);
            Assert.That(banner.IsShowing, Is.False, "gone again");
            Assert.That(notice.ShownCount, Is.EqualTo(1));

            // flicker between two areas (0.3 s each): no area is entered, nothing is shown
            for (int i = 0; i < 16; i++)
            {
                PlaceInZone(i % 2 == 0 ? MineZone.Caverns : MineZone.Hall);
                yield return new WaitForSecondsRealtime(0.3f);
            }

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(notice.ShownCount, Is.EqualTo(1), "a border flicker shows no banner");
            Assert.That(banner.IsShowing, Is.False);

            // the longest name fits on one line
            yield return Go(MineZone.Workshop, 0f);
            yield return WaitUntil(() => banner.IsShowing, 4f);
            Assert.That(banner.CurrentTitle, Is.EqualTo("FORGOTTEN WORKSHOP"));
            var text = banner.GetComponentInChildren<TMPro.TMP_Text>();
            Assert.That(text.preferredWidth, Is.LessThan(((RectTransform)text.transform).rect.width), "FORGOTTEN WORKSHOP is not wrapped or cut");
            yield return new WaitForSecondsRealtime(banner.TotalDuration + 0.4f);

            // back and forth soon after: no repeated name
            yield return Go(MineZone.Hall, 4.5f);
            Assert.That(notice.ShownCount, Is.EqualTo(3), "the hall once");
            yield return Go(MineZone.Workshop, 4.5f);
            Assert.That(notice.ShownCount, Is.EqualTo(3), "the workshop was shown a few seconds ago: no repeat");

            // not while the game is paused
            PlaceInZone(MineZone.Vault);
            One<MineZoneTracker>().Refresh(true);
            yield return new WaitForSecondsRealtime(0.1f);
            GamePause.Pause();
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(banner.IsShowing, Is.False, "nothing pops up behind the pause menu");
            GamePause.Resume();
            yield return WaitUntil(() => banner.IsShowing, 3f);
            Assert.That(banner.CurrentTitle, Is.EqualTo("ROYAL VAULT"));
            Assert.That(notice.ShownCount, Is.EqualTo(4));
        }

        // ================================================================ the Royal Vault's first entry

        [UnityTest]
        public IEnumerator RoyalVault_AnnouncesItselfOnTheFirstEntryOnly_AndTheGlowIsBoldest()
        {
            yield return Load();
            var ambience = One<MineAmbience>();
            var glow = One<MineCrystalGlow>();
            var tracker = One<MineZoneTracker>();
            yield return WaitUntil(() => ambience.IsReady, 2f);
            Assert.That(ambience.VaultSwell, Is.EqualTo(0f));
            Assert.That(glow.Swell, Is.EqualTo(0f));
            yield return Go(MineZone.Hall, 1f);
            Assert.That(ambience.VaultSwell, Is.EqualTo(0f), "no swell anywhere else");

            PlaceInZone(MineZone.Vault);
            tracker.Refresh(true);
            yield return null;
            Assert.That(tracker.Visits(MineZone.Vault), Is.EqualTo(1));
            Assert.That(ambience.VaultSwell, Is.GreaterThan(0.9f), "the first entry: the vault's sound swells");
            Assert.That(glow.Swell, Is.GreaterThan(0.9f), "and its glow");
            yield return new WaitForSecondsRealtime(3.2f);
            float volumeInSwell = ambience.DetailSource(MineZone.Vault).volume;
            Assert.That(volumeInSwell, Is.GreaterThan(MineAmbience.BaseVolume * MineAmbience.DetailGain[4] * 0.95f), "louder than its normal level while it swells");
            yield return new WaitForSecondsRealtime(MineAmbience.VaultSwellSeconds);
            Assert.That(ambience.VaultSwell, Is.EqualTo(0f).Within(0.001f), "and it settles again");
            Assert.That(ambience.DetailSource(MineZone.Vault).volume, Is.EqualTo(MineAmbience.BaseVolume * MineAmbience.DetailGain[4]).Within(0.004f));
            Assert.That(MineAmbience.LoudestPossible, Is.LessThan(0.55f));

            // leave and come back: ordinary entry, no second announcement
            yield return Go(MineZone.Hall, 1f);
            PlaceInZone(MineZone.Vault);
            tracker.Refresh(true);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tracker.Visits(MineZone.Vault), Is.EqualTo(2));
            Assert.That(ambience.VaultSwell, Is.EqualTo(0f), "only the very first entry is announced");
            Assert.That(glow.Swell, Is.EqualTo(0f));

            // the vault entries are the boldest glow
            for (int i = 0; i < glow.EntryCount; i++)
            {
                MineCrystalGlow.Entry e = glow.GetEntry(i);
                if (e.zone == MineZone.Vault && e.vaultSwell)
                {
                    Color calmColor = MineCrystalGlow.Evaluate(e, 3f, 0f), swellColor = MineCrystalGlow.Evaluate(e, 3f, 1f);
                    Assert.That(swellColor.g, Is.GreaterThan(calmColor.g * 1.3f), "the first entry brightens the vault glow");
                }
            }
        }

        // ================================================================ treasures, gold, other maps

        [UnityTest]
        public IEnumerator TwentyFiveTreasures_1240Gold_AreUntouched_AndOtherMapsHaveNoMineAtmosphere()
        {
            yield return Load();
            TreasureSpot[] spots = RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true);
            Assert.That(spots.Length, Is.EqualTo(25));
            Assert.That(spots.Sum(s => s.Definition.GoldValue), Is.EqualTo(1240));
            Assert.That(spots.Select(s => s.SpotId).Distinct().Count(), Is.EqualTo(25), "unique ids");
            Assert.That(spots.All(s => s.SpotId.StartsWith("ancient_mine.")), Is.True);
            Assert.That(One<ProgressManager>().Service.TotalGold, Is.EqualTo(1200), "the in-memory save's gold, nothing else");

            foreach (string path in new[] { "Assets/Scenes/GreenMine.unity", "Assets/Scenes/RockyCanyon.unity" })
            {
                string text = File.ReadAllText(path);
                Assert.That(text.Contains("MineAmbience") || text.Contains("MineZoneTracker") || text.Contains("MineCrystalGlow") || text.Contains("MineMotes"), Is.False, path + " has no Ancient Mine atmosphere");
            }

            Assert.That(Object.FindObjectsByType<CanyonAmbience>(FindObjectsInactive.Include).Length, Is.EqualTo(0), "nothing of Rocky Canyon's ambience in this scene");
        }

        // ================================================================ walking with the atmosphere running

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator Barnaby_WalksTheMainRoute_WithTheAtmosphereRunning()
        {
            yield return WalkMain(CharacterSelection.MaleId, "Barnaby");
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator Ruby_WalksTheMainRoute_WithTheAtmosphereRunning()
        {
            yield return WalkMain(CharacterSelection.FemaleId, "Ruby");
        }

        private IEnumerator WalkMain(string miner, string who)
        {
            yield return Load(miner);
            Time.timeScale = 2f;
            Transform player = Player;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            Camera camera = Camera.main;
            var tracker = One<MineZoneTracker>();
            var ambience = One<MineAmbience>();
            var notice = One<ZoneEntryNotice>();
            var entered = new List<MineZone>();
            if (tracker.HasZone)
            {
                entered.Add(tracker.Current); // the starting area was entered before this test subscribed
            }

            tracker.ZoneEntered += z => entered.Add(z);
            var titles = new List<string>();
            yield return new WaitForSecondsRealtime(1.2f);

            Transform routes = Env.Find("Markers/Routes/Main");
            var points = new List<Vector3>();
            foreach (Transform t in routes)
            {
                points.Add(t.position);
            }

            int frames = 0, cameraInWall = 0, maxSources = 0;
            float minCamera = float.MaxValue;
            string lastTitle = "";
            foreach (Vector3 target in points)
            {
                Vector2 goal = new(target.x, target.z);
                float legTime = 0f, checkTime = 0f, sidestepUntil = 0f, sidestepSign = 1f;
                float legStart = Vector2.Distance(new Vector2(player.position.x, player.position.z), goal);
                Vector3 checkPosition = player.position;
                while (true)
                {
                    Vector3 delta = new(goal.x - player.position.x, 0f, goal.y - player.position.z);
                    if (delta.magnitude < 1.6f)
                    {
                        break;
                    }

                    legTime += Time.deltaTime;
                    Assert.That(legTime, Is.LessThan(legStart / 2.5f + 8f), $"{who} stuck on the way to {target} at {player.position}");
                    checkTime += Time.deltaTime;
                    if (checkTime >= 0.5f)
                    {
                        if ((player.position - checkPosition).magnitude < 0.6f)
                        {
                            sidestepUntil = Time.time + 0.8f;
                            sidestepSign = -sidestepSign;
                        }

                        checkPosition = player.position;
                        checkTime = 0f;
                    }

                    Vector3 f = camera.transform.forward;
                    f.y = 0f;
                    f.Normalize();
                    Vector3 r = new(f.z, 0f, -f.x);
                    Vector3 d = delta.normalized;
                    if (Time.time < sidestepUntil)
                    {
                        d = Quaternion.Euler(0f, 75f * sidestepSign, 0f) * d;
                    }

                    controller.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
                    yield return null;
                    frames++;
                    if (Physics.CheckSphere(camera.transform.position, 0.05f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        cameraInWall++;
                    }

                    minCamera = Mathf.Min(minCamera, Vector3.Distance(camera.transform.position, player.position + Vector3.up * 1.4f));
                    maxSources = Mathf.Max(maxSources, ambience.PlayingSourceCount);
                    NotificationBanner banner = notice.Banner;
                    if (banner.IsShowing && banner.CurrentTitle != lastTitle)
                    {
                        lastTitle = banner.CurrentTitle;
                        titles.Add(lastTitle);
                    }
                    else if (!banner.IsShowing)
                    {
                        lastTitle = "";
                    }
                }
            }

            controller.ClearMoveInput();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(tracker.Current, Is.EqualTo(MineZone.Vault), who + " ends the main route in the Royal Vault");
            int g = entered.IndexOf(MineZone.Gate), h = entered.IndexOf(MineZone.Hall), c = entered.IndexOf(MineZone.Caverns), v = entered.IndexOf(MineZone.Vault);
            Assert.That(g >= 0 && h > g && c > h && v > c, Is.True, who + ": the areas are entered in route order: " + string.Join(", ", entered));
            Assert.That(entered.Count, Is.LessThanOrEqualTo(6), who + ": no flicker of entries along the way: " + string.Join(", ", entered));
            Assert.That(titles.Distinct().Count(), Is.EqualTo(titles.Count), who + ": no banner repeated: " + string.Join(", ", titles));
            Assert.That(titles, Contains.Item("CRYSTAL CAVERNS"));
            Assert.That(titles.Last(), Is.EqualTo("ROYAL VAULT"));
            Assert.That(cameraInWall, Is.EqualTo(0), who + ": the camera is never inside a wall");
            Assert.That(minCamera, Is.GreaterThan(1.3f), who + ": never glued to the miner's head");
            Assert.That(maxSources, Is.LessThanOrEqualTo(4), who + ": never more than four ambience sources sounding at once");
            Debug.Log($"Ancient Mine atmosphere walk ({who}): {frames} frames, areas {string.Join(" > ", entered)}, banners {string.Join(", ", titles)}, at most {maxSources} sources, closest camera {minCamera:F2} m");
        }
    }
}
#endif
