#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using ItemCollector3D.UI;
using ItemCollector3D.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Rocky Canyon atmosphere (stage 15D): the five areas are told apart (the bridge deck from the gorge under it), every area has its own ambience that cross-fades without jumps, follows the SFX
    /// setting, leaves the music alone, pauses with the game and stays well below the detector beep and the DIG / COLLECT sounds; the ambient motion (swaying shrubs and streamers, dust, the ravine's
    /// vein shimmer) is small, shared-material, light-free and collider-free; the area-name banner shows for about two and a half seconds without covering the HUD; walls, the pocket's rock and the
    /// map edges hold. In-memory stores and settings everywhere; the developer's real save file is hashed before the first and after the last test.
    /// </summary>
    public sealed class RockyCanyonAtmospherePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/RockyCanyon.unity";

        private const string FinishedGreenMineSave =
            "{\"version\":1,\"totalGold\":500,\"collectedTreasureIds\":[\"green_mine.gold_coin_01\",\"green_mine.meadow_01\",\"green_mine.riverbank_01\"," +
            "\"green_mine.rocky_01\",\"green_mine.rocky_03\",\"green_mine.forest_01\",\"green_mine.mine_01\",\"green_mine.mine_tunnel_01\",\"green_mine.mine_room_01\"," +
            "\"green_mine.mine_room_02\",\"green_mine.rocky_04\",\"green_mine.bridge_01\"],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"completedAreaIds\":[\"green_mine\"]}";

        private static readonly string[] Miners = { CharacterSelection.MaleId, CharacterSelection.FemaleId };

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
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore(); // never touch the player's real settings
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

            store = new MemoryProgressStore(FinishedGreenMineSave);
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

        private Transform Env => RootObject("RockyCanyonEnvironment").transform;
        private Transform Atmosphere => RootObject("CanyonAtmosphere").transform;
        private Transform Player => RootObject("Player").transform;

        private TreasureSpot Spot(string id)
        {
            foreach (TreasureSpot spot in RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true))
            {
                if (spot.SpotId == id)
                {
                    return spot;
                }
            }

            Assert.Fail("Treasure spot not found: " + id);
            return null;
        }

        private float WalkSurface(float x, float z)
        {
            float best = float.MinValue;
            Collider terrain = Env.Find("Terrain").GetComponent<Collider>();
            if (terrain.Raycast(new Ray(new Vector3(x, 80f, z), Vector3.down), out RaycastHit hit, 200f))
            {
                best = hit.point.y;
            }

            foreach (string path in new[] { "C_OldRailway/Colliders/BridgeDeck", "D_CanyonOverlook/Colliders/LookoutDeck" })
            {
                Transform t = Env.Find(path);
                if (t != null && t.GetComponent<Collider>().Raycast(new Ray(new Vector3(x, 80f, z), Vector3.down), out hit, 200f))
                {
                    best = Mathf.Max(best, hit.point.y);
                }
            }

            Assert.That(best, Is.GreaterThan(float.MinValue), $"ground at {x:F1},{z:F1}");
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

        private void PlaceOn(float x, float z, float surface = float.NaN)
        {
            Teleport(Player, new Vector3(x, (float.IsNaN(surface) ? WalkSurface(x, z) : surface) + 1.05f, z));
        }

        private static void Push(ThirdPersonPlayerController controller, Camera camera, Vector3 worldDirection)
        {
            Vector3 f = camera.transform.forward;
            f.y = 0f;
            f.Normalize();
            Vector3 r = new(f.z, 0f, -f.x);
            Vector3 d = worldDirection;
            d.y = 0f;
            d.Normalize();
            controller.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
        }

        private IEnumerator WalkTo(ThirdPersonPlayerController controller, Camera camera, Vector2 target, string label)
        {
            Transform player = controller.transform;
            float legTime = 0f, checkTime = 0f, sidestepUntil = 0f, sidestepSign = 1f;
            float legStart = Vector2.Distance(new Vector2(player.position.x, player.position.z), target);
            Vector3 checkPosition = player.position;
            while (true)
            {
                Vector3 delta = new(target.x - player.position.x, 0f, target.y - player.position.z);
                if (delta.magnitude < 1.4f)
                {
                    break;
                }

                legTime += Time.deltaTime;
                Assert.That(legTime, Is.LessThan(legStart / 2.5f + 8f), $"stuck on the way to {label} at {player.position}");
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

                Vector3 d = delta.normalized;
                if (Time.time < sidestepUntil)
                {
                    d = Quaternion.Euler(0f, 75f * sidestepSign, 0f) * d;
                }

                Push(controller, camera, d);
                yield return null;
            }

            controller.ClearMoveInput();
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

        /// <summary>RMS of a signal after a crude 500 .. 1600 Hz band-pass (one-pole high-pass and low-pass): the band the detector beep lives in.</summary>
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

        // (name, x, z, expected area): well inside each area, away from the junctions
        private static readonly (string name, float x, float z, CanyonZone zone)[] Places =
        {
            ("outpost", -34f, -22f, CanyonZone.Outpost),
            ("riverbed", 6f, -14f, CanyonZone.Riverbed),
            ("railway", -18f, 16f, CanyonZone.Railway),
            ("overlook", 43f, 36f, CanyonZone.Overlook),
            ("ravine", -17f, 2f, CanyonZone.Ravine),
        };

        // ================================================================ areas

        [UnityTest]
        public IEnumerator Areas_AreTold_Apart_TheBridgeDeckFromTheGorge_AndNamed()
        {
            yield return Load();
            var tracker = One<CanyonZoneTracker>();
            Assert.That(tracker.PieceCount, Is.GreaterThanOrEqualTo(12), "the walkable floors of the map plus the bridge deck");
            Assert.That(CanyonZoneTracker.DisplayNames, Is.EqualTo(new[] { "DESERT OUTPOST", "DRY RIVERBED", "OLD RAILWAY", "CANYON OVERLOOK", "HIDDEN RAVINE" }));

            foreach ((string name, float x, float z, CanyonZone zone) in Places)
            {
                PlaceOn(x, z);
                tracker.Refresh();
                Assert.That(tracker.Current, Is.EqualTo(zone), name);
                Assert.That(tracker.Weight(zone), Is.GreaterThan(0.6f), name + ": the area's own weight dominates");
            }

            // the bridge deck (floor 5 m) and the gorge under it (floor 0.5 m) share the same map position but are different areas
            PlaceOn(3f, 18f);
            tracker.Refresh();
            Assert.That(WalkSurface(3f, 18f), Is.GreaterThan(4.5f), "standing on the deck");
            Assert.That(tracker.Current, Is.EqualTo(CanyonZone.Railway), "on the bridge deck: the Old Railway");
            Teleport(Player, new Vector3(3.2f, 0.4f + 1.05f, 16f));
            tracker.Refresh();
            Assert.That(tracker.Current, Is.EqualTo(CanyonZone.Riverbed), "right under the bridge, in the gorge: the Dry Riverbed");

            float sum = 0f;
            for (int z = 0; z < CanyonZoneTracker.ZoneCount; z++)
            {
                sum += tracker.Weight((CanyonZone)z);
            }

            Assert.That(sum, Is.EqualTo(1f).Within(0.001f), "the soft weights add up to one");
        }

        [UnityTest]
        public IEnumerator Walking_FromTheOutpostToTheRavine_EntersEachAreaOnce_WithoutFlicker_BothMiners_AndNamesThem()
        {
            foreach (string miner in Miners)
            {
                yield return Load(miner);
                var tracker = One<CanyonZoneTracker>();
                var notice = One<ZoneEntryNotice>();
                var controller = Player.GetComponent<ThirdPersonPlayerController>();
                Camera camera = Camera.main;
                tracker.Refresh();
                Assert.That(tracker.HasZone, Is.True);
                Assert.That(tracker.Current, Is.EqualTo(CanyonZone.Outpost), miner + ": the start is the Desert Outpost");
                Assert.That(tracker.EntryCount, Is.EqualTo(1), "entered once at the start");
                var entered = new List<CanyonZone> { tracker.Current };
                tracker.ZoneEntered += entered.Add;

                foreach ((Vector2 target, string label) in new[] { (new Vector2(-14f, -17f), "riverbed"), (new Vector2(6f, -14f), "basin"), (new Vector2(-6f, -6f), "ravine mouth"), (new Vector2(-16f, 2f), "pocket") })
                {
                    yield return WalkTo(controller, camera, target, label);
                }

                yield return new WaitForSecondsRealtime(1f);
                Assert.That(entered, Is.EqualTo(new[] { CanyonZone.Outpost, CanyonZone.Riverbed, CanyonZone.Ravine }), miner + ": each area was entered once, in order, with no flicker at the borders");
                Assert.That(notice.ShownCount, Is.GreaterThanOrEqualTo(2).And.LessThanOrEqualTo(3), miner + ": the area names were shown, never more often than the areas were entered");
                Assert.That(Player.position.y, Is.GreaterThan(1.5f), "the pocket was reached");
                tracker.ZoneEntered -= entered.Add;
                yield return TearDownScene();
            }
        }

        private IEnumerator TearDownScene()
        {
            GamePause.Resume();
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return null;
        }

        // ================================================================ ambience

        [UnityTest]
        public IEnumerator Ambience_EveryAreaHasItsOwnMix_CrossFadesWithoutJumps_AndNeverUsesMoreThanFourSources()
        {
            yield return Load();
            var ambience = One<CanyonAmbience>();
            yield return WaitUntil(() => ambience.IsReady, 2f);
            Assert.That(ambience.IsReady, Is.True, "all six loops are synthesised within a few frames");

            var sources = new List<AudioSource> { ambience.WindSource };
            foreach (CanyonZone zone in System.Enum.GetValues(typeof(CanyonZone)))
            {
                sources.Add(ambience.DetailSource(zone));
            }

            Assert.That(sources.Count, Is.EqualTo(6), "one wind bed and one detail loop per area");
            foreach (AudioSource s in sources)
            {
                Assert.That(s.loop, Is.True, s.name);
                Assert.That(s.ignoreListenerPause, Is.False, s.name + " pauses with the game");
                Assert.That(s.spatialBlend, Is.EqualTo(0f), s.name);
                Assert.That(s.priority, Is.GreaterThan(128), s.name + " gives way to the sound effects");
            }

            var previous = new float[sources.Count];
            for (int i = 0; i < previous.Length; i++)
            {
                previous[i] = sources[i].volume;
            }

            float biggestStep = 0f;
            int mostPlaying = 0;
            string badStop = null;
            var wasPlaying = new bool[CanyonZoneTracker.ZoneCount];

            IEnumerator Settle(float seconds)
            {
                float end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    for (int i = 0; i < sources.Count; i++)
                    {
                        biggestStep = Mathf.Max(biggestStep, Mathf.Abs(sources[i].volume - previous[i]));
                        previous[i] = sources[i].volume;
                    }

                    mostPlaying = Mathf.Max(mostPlaying, ambience.PlayingSourceCount);
                    for (int z = 0; z < CanyonZoneTracker.ZoneCount; z++)
                    {
                        bool playing = ambience.IsDetailPlaying((CanyonZone)z);
                        if (wasPlaying[z] && !playing && previous[z + 1] > 0.002f)
                        {
                            badStop = $"{(CanyonZone)z} stopped while still audible ({previous[z + 1]:F4})";
                        }

                        wasPlaying[z] = playing;
                    }
                }
            }

            var order = new[] { CanyonZone.Outpost, CanyonZone.Riverbed, CanyonZone.Railway, CanyonZone.Overlook, CanyonZone.Ravine, CanyonZone.Outpost };
            foreach (CanyonZone zone in order)
            {
                var place = System.Array.Find(Places, p => p.zone == zone);
                string name = place.name;
                PlaceOn(place.x, place.z);
                yield return Settle(3.0f);
                float gain = ambience.Duck * GameSettings.SfxScale; // the master fade-in is over by now
                int zi = (int)zone;
                Assert.That(ambience.DetailSource(zone).isPlaying, Is.True, name + ": its own detail loop plays");
                Assert.That(ambience.DetailSource(zone).volume, Is.EqualTo(CanyonAmbience.BaseVolume * CanyonAmbience.DetailGain[zi] * gain).Within(0.012f), name + ": detail volume");
                Assert.That(ambience.WindSource.volume, Is.EqualTo(CanyonAmbience.BaseVolume * CanyonAmbience.WindGain[zi] * gain).Within(0.012f), name + ": wind volume");
                Assert.That(ambience.WindSource.pitch, Is.EqualTo(CanyonAmbience.WindPitch[zi]).Within(0.04f), name + ": wind pitch");
                if (zone == CanyonZone.Ravine)
                {
                    Assert.That(ambience.CurrentWindCutoff, Is.LessThan(1200f), "the ravine wind is muffled");
                }
                else
                {
                    Assert.That(ambience.CurrentWindCutoff, Is.GreaterThan(8000f), name + ": the wind is open");
                }

                foreach (CanyonZone other in order)
                {
                    if (other != zone)
                    {
                        Assert.That(ambience.IsDetailPlaying(other), Is.False, $"{name}: {other}'s loop is not playing here");
                    }
                }
            }

            Assert.That(ambience.WindSource.volume, Is.GreaterThan(0f), "the open wind is the quietest bed, but it is there");
            Assert.That(CanyonAmbience.WindGain[(int)CanyonZone.Overlook], Is.GreaterThan(CanyonAmbience.WindGain[(int)CanyonZone.Outpost]), "stronger wind up high than at the outpost");
            Assert.That(CanyonAmbience.WindGain[(int)CanyonZone.Riverbed], Is.GreaterThan(CanyonAmbience.WindGain[(int)CanyonZone.Outpost]), "more noticeable in the open riverbed");
            Assert.That(biggestStep, Is.LessThan(0.012f), "no source ever jumps in volume: every change of area is a cross-fade (a hard cut would be about " + CanyonAmbience.BaseVolume + ")");
            Assert.That(badStop, Is.Null, "no sound is cut while it is still audible");
            Assert.That(mostPlaying, Is.LessThanOrEqualTo(4), "never more than four of the six sources at the same time");
            Debug.Log($"Rocky Canyon ambience: biggest volume step per frame {biggestStep:F4}, most sources playing {mostPlaying}");
        }

        [UnityTest]
        public IEnumerator Ambience_FollowsTheSfxSetting_LeavesTheMusicAlone_AndPausesWithTheGame()
        {
            yield return Load();
            var ambience = One<CanyonAmbience>();
            var music = One<AmbientMusicPlayer>();
            yield return WaitUntil(() => ambience.IsReady, 2f);
            PlaceOn(43f, 36f);
            yield return new WaitForSecondsRealtime(3.2f);
            AudioSource wind = ambience.WindSource, detail = ambience.DetailSource(CanyonZone.Overlook);
            float windFull = wind.volume, detailFull = detail.volume, musicFull = music.Source.volume;
            Assert.That(windFull, Is.GreaterThan(0.02f));
            Assert.That(music.Source.ignoreListenerPause, Is.True, "the music keeps playing while paused");
            Assert.That(wind.ignoreListenerPause, Is.False);

            GameSettings.Current.SetSfxVolume(0.5f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(wind.volume, Is.EqualTo(windFull * 0.5f).Within(windFull * 0.12f), "half the SFX volume, half the wind");
            Assert.That(detail.volume, Is.EqualTo(detailFull * 0.5f).Within(detailFull * 0.12f + 0.002f), "and half the details");
            GameSettings.Current.SetSfxVolume(0f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(wind.volume, Is.EqualTo(0f).Within(0.0005f), "SFX volume 0: silence");
            Assert.That(detail.volume, Is.EqualTo(0f).Within(0.0005f));
            Assert.That(music.Source.volume, Is.EqualTo(musicFull).Within(0.0001f), "the SFX slider never moves the music");
            GameSettings.Current.SetSfxVolume(1f);
            GameSettings.Current.SetMusicVolume(0.2f);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(wind.volume, Is.EqualTo(windFull).Within(windFull * 0.12f), "back to full SFX volume");
            Assert.That(wind.volume, Is.GreaterThan(0f), "the music slider never moves the ambience");

            // pause: the ambience stops where it is, the music goes on; resume continues
            int a = wind.timeSamples;
            yield return new WaitForSecondsRealtime(0.4f);
            Assume.That(wind.timeSamples, Is.Not.EqualTo(a), "the audio engine is running in this environment");
            GamePause.Pause();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(AudioListener.pause, Is.True);
            int pausedAt = wind.timeSamples;
            float volumeWhilePaused = wind.volume;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(wind.timeSamples, Is.EqualTo(pausedAt).Within(2048), "the wind does not advance while the game is paused");
            Assert.That(wind.volume, Is.EqualTo(volumeWhilePaused).Within(0.0001f));
            Assert.That(music.IsPlaying, Is.True, "the music keeps playing");
            GamePause.Resume();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(wind.timeSamples, Is.Not.EqualTo(pausedAt), "and continues after Resume");
            Assert.That(wind.isPlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator Ambience_StepsBackForTheDetector_TheDigAndTheReward_AndStaysUnderTheBeep()
        {
            yield return Load();
            var ambience = One<CanyonAmbience>();
            var hunt = One<TreasureHuntController>();
            var indicator = One<DetectorSignalIndicator>();
            yield return WaitUntil(() => ambience.IsReady, 2f);

            // measurable: the loudest possible ambience (wind at its strongest plus the loudest detail), in the beep's own band, against the weakest beep the detector plays
            var so = new SerializedObject(indicator);
            float weakestBeep = so.FindProperty("weakestBeepVolume").floatValue * DetectorSignalIndicator.BeepGain;
            float beepBand = BeepBandRms(Samples(SoundSynthBeep()), SoundSynthBeepRate) * weakestBeep;
            float windBand = BeepBandRms(Samples(ambience.WindSource.clip), 22050) * CanyonAmbience.BaseVolume * Mathf.Max(CanyonAmbience.WindGain);
            float loudestDetail = 0f;
            foreach (CanyonZone zone in System.Enum.GetValues(typeof(CanyonZone)))
            {
                float d = BeepBandRms(Samples(ambience.DetailSource(zone).clip), 22050) * CanyonAmbience.BaseVolume * CanyonAmbience.DetailGain[(int)zone];
                loudestDetail = Mathf.Max(loudestDetail, d);
            }

            float together = Mathf.Sqrt(windBand * windBand + loudestDetail * loudestDetail);
            Assert.That(together, Is.LessThan(0.35f * beepBand), $"in the beep's band the whole ambience ({together:F4}) stays at least 9 dB below the weakest beep ({beepBand:F4})");

            // the detector, the dig and the reward make the ambience step back
            TreasureSpot spot = Spot("rocky_canyon.outpost_01");
            Button dig = FindButton("DigButton"), collect = FindButton("CollectButton");
            PlaceOn(-34f, -22f);
            yield return new WaitForSecondsRealtime(3f);
            foreach (TreasureSpot other in RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true))
            {
                if (other != spot)
                {
                    other.gameObject.SetActive(false); // neighbouring detection circles overlap everywhere: only this treasure may speak
                }
            }

            yield return new WaitForSecondsRealtime(1f);
            Assert.That(ambience.Duck, Is.EqualTo(1f).Within(0.01f), "no treasure near: the normal level");
            Vector3 p = spot.transform.position + new Vector3(1.5f, 0f, 0f);
            PlaceOn(p.x, p.z);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(ambience.Duck, Is.EqualTo(CanyonAmbience.RapidDuck).Within(0.03f), "RAPID: the ambience steps back so the detector beep stays on top");

            typeof(TreasureHuntController).GetField("digDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(hunt, 1.0f);
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            dig.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(hunt.IsDigging, Is.True);
            Assert.That(ambience.Duck, Is.EqualTo(CanyonAmbience.DiggingDuck).Within(0.03f), "ducked while digging");
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            collect.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(ambience.Duck, Is.LessThanOrEqualTo(CanyonAmbience.RewardDuck + 0.03f), "still ducked right after the reward");
            Assert.That(One<ProgressManager>().Service.TotalGold, Is.EqualTo(510), "the reward was paid as usual (500 + the 10 gold of this treasure)");
            Debug.Log($"Rocky Canyon ambience vs beep (band 500..1600 Hz): ambience {together:F4}, weakest beep {beepBand:F4}");
        }

        private static AudioClip SoundSynthBeep() => ItemCollector3D.Audio.SoundSynth.Beep();
        private const int SoundSynthBeepRate = ItemCollector3D.Audio.SoundSynth.SfxRate;

        [Test]
        public void TheLoops_AreSoftBoundedAndSeamless()
        {
            foreach ((string name, System.Func<AudioClip> make, float seconds) in new (string, System.Func<AudioClip>, float)[]
            {
                ("wind", ItemCollector3D.Audio.SoundSynth.CanyonWind, ItemCollector3D.Audio.SoundSynth.CanyonWindSeconds),
                ("outpost", ItemCollector3D.Audio.SoundSynth.OutpostDetail, ItemCollector3D.Audio.SoundSynth.OutpostSeconds),
                ("riverbed", ItemCollector3D.Audio.SoundSynth.RiverbedDetail, ItemCollector3D.Audio.SoundSynth.RiverbedSeconds),
                ("railway", ItemCollector3D.Audio.SoundSynth.RailwayDetail, ItemCollector3D.Audio.SoundSynth.RailwaySeconds),
                ("overlook", ItemCollector3D.Audio.SoundSynth.OverlookDetail, ItemCollector3D.Audio.SoundSynth.OverlookSeconds),
                ("ravine", ItemCollector3D.Audio.SoundSynth.RavineDetail, ItemCollector3D.Audio.SoundSynth.RavineSeconds),
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
                Assert.That(clip.length, Is.EqualTo(seconds).Within(0.01f), name);
                Assert.That(clip.frequency, Is.EqualTo(22050), name + ": cheap mono loop");
                Assert.That(clip.channels, Is.EqualTo(1), name);
                Assert.That(peak, Is.InRange(0.5f, 0.65f), name + ": peak-normalised below the sound effects");
                Assert.That(rms, Is.InRange(0.04f, 0.2f), name + ": a calm bed, not a wall of noise");
                // seamless: the step from the last sample to the first is no bigger than the biggest ordinary step in the loop
                float biggest = 0f;
                for (int i = 1; i < data.Length; i++)
                {
                    biggest = Mathf.Max(biggest, Mathf.Abs(data[i] - data[i - 1]));
                }

                Assert.That(Mathf.Abs(data[0] - data[data.Length - 1]), Is.LessThanOrEqualTo(biggest), name + ": the loop point is no click");
                Object.DestroyImmediate(clip);
            }
        }

        // ================================================================ ambient motion

        [UnityTest]
        public IEnumerator AmbientMotion_IsSmall_SharedMaterials_NoLights_NoColliders_AndStandsStillWhenPaused()
        {
            yield return Load();
            Transform atmosphere = Atmosphere;

            Assert.That(atmosphere.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(0), "no real-time light was added");
            Assert.That(atmosphere.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0), "the swaying props and the bridge streamers have no colliders");
            var sway = atmosphere.GetComponentInChildren<AmbientSway>(true);
            Assert.That(sway.ItemCount, Is.InRange(20, 40), "a handful of shrubs, tufts and streamers");
            var materials = new HashSet<Material>();
            int renderers = 0;
            foreach (MeshRenderer r in atmosphere.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                Assert.That(r.sharedMaterials.Length, Is.EqualTo(1), r.name);
                materials.Add(r.sharedMaterial);
                Assert.That(r.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), r.name + ": no extra shadow casters");
                Assert.That(r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.LessThan(700), r.name + ": tiny meshes");
            }

            Assert.That(renderers, Is.EqualTo(sway.ItemCount), "every swaying prop is one renderer");
            Assert.That(materials.Count, Is.EqualTo(1), "all swaying props share the environment's palette material");
            foreach (Material m in materials)
            {
                Assert.That(m.name, Is.EqualTo("RC_Palette"));
            }

            // particles: tiny counts, one shared opaque material, no transparency, world-space
            int particleSystems = 0, totalCap = 0;
            foreach (ParticleSystem ps in atmosphere.GetComponentsInChildren<ParticleSystem>(true))
            {
                particleSystems++;
                totalCap += ps.main.maxParticles;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer.sharedMaterial.name, Is.EqualTo("RC_Mote"), ps.name);
                Assert.That(renderer.sharedMaterial.renderQueue, Is.LessThan(2500), ps.name + ": opaque, no overdraw");
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(ps.main.maxParticles, Is.LessThanOrEqualTo(AmbientDust.MaxParticles), ps.name);
                Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(ps.collision.enabled, Is.False);
                Assert.That(ps.subEmitters.enabled, Is.False);
                Assert.That(ps.main.startSize.constantMax, Is.LessThanOrEqualTo(0.12f), ps.name + ": specks, not clouds");
            }

            Assert.That(particleSystems, Is.EqualTo(3), "player dust, bridge dust, vein twinkle");
            Assert.That(totalCap, Is.LessThanOrEqualTo(AmbientDust.MaxParticles * 2 + GoldVeinShimmer.MaxParticles), "the particle budget of the whole canyon");

            // the sway: only props near the camera move, and nothing moves while paused
            Transform prop = sway.transform.GetChild(0);
            PlaceOn(prop.position.x + 3f, prop.position.z, prop.position.y);
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.That(sway.ActiveCount, Is.GreaterThan(0), "props near the camera sway");
            Quaternion before = prop.localRotation;
            float deviation = 0f;
            for (float t = 0f; t < 2.0f; t += Time.deltaTime)
            {
                yield return null;
                deviation = Mathf.Max(deviation, Quaternion.Angle(before, prop.localRotation));
            }

            Assert.That(deviation, Is.GreaterThan(0.05f).And.LessThan(8f), "a gentle tilt (a few degrees)");
            GamePause.Pause();
            yield return new WaitForSecondsRealtime(0.3f);
            Quaternion paused = prop.localRotation;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(Quaternion.Angle(paused, prop.localRotation), Is.LessThan(0.0001f), "stands still while paused");
            GamePause.Resume();

            // the vein shimmer: only near the veins, and only by a few per cent
            var veins = atmosphere.GetComponentInChildren<GoldVeinShimmer>(true);
            PlaceOn(-34f, -22f);
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.That(veins.IsActive, Is.False, "nothing happens far from the ravine");
            Assert.That(veins.CurrentFactor, Is.EqualTo(1f));
            PlaceOn(-14f, 1f);
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.That(veins.IsActive, Is.True, "the veins shimmer when the miner is in the ravine");
            float lowest = 9f, highest = 0f;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                lowest = Mathf.Min(lowest, veins.CurrentFactor);
                highest = Mathf.Max(highest, veins.CurrentFactor);
            }

            Assert.That(highest - lowest, Is.GreaterThan(0.03f), "the glow breathes");
            Assert.That(lowest, Is.GreaterThan(0.8f).And.LessThan(1.0f));
            Assert.That(highest, Is.LessThan(1.2f).And.GreaterThan(1.0f), "by a few per cent only");

            // dust: some specks drift in the riverbed, never more than the cap
            PlaceOn(6f, -14f);
            yield return new WaitForSecondsRealtime(9f);
            int live = 0;
            foreach (AmbientDust dust in atmosphere.GetComponentsInChildren<AmbientDust>(true))
            {
                live += dust.Particles.particleCount;
                Assert.That(dust.Particles.particleCount, Is.LessThanOrEqualTo(AmbientDust.MaxParticles));
            }

            Assert.That(live, Is.GreaterThan(0), "dust is drifting in the riverbed");
        }

        // ================================================================ the area-name banner

        [UnityTest]
        public IEnumerator AreaNames_ShowForAboutTwoAndAHalfSeconds_OncePerEntry_OutOfTheHudsWay()
        {
            yield return Load();
            var notice = One<ZoneEntryNotice>();
            NotificationBanner banner = notice.Banner;
            var progressBanner = One<ProgressNotificationPresenter>().GetComponentInChildren<NotificationBanner>(true);
            Assert.That(banner, Is.Not.EqualTo(progressBanner), "the area banner is its own instance, the AREA COMPLETE banner is untouched");
            Assert.That(banner.TotalDuration, Is.InRange(2.0f, 3.0f), "about two and a half seconds");

            // the start: DESERT OUTPOST after a short dwell
            yield return WaitUntil(() => banner.IsShowing, 3f);
            Assert.That(banner.IsShowing, Is.True);
            Assert.That(banner.CurrentTitle, Is.EqualTo("DESERT OUTPOST"));
            var group = banner.GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts, Is.False, "never takes a touch");
            Assert.That(group.interactable, Is.False);
            foreach (Graphic g in banner.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(g.raycastTarget || group.blocksRaycasts == false, Is.True);
            }

            // it must not sit on any HUD element: below the top bar (level plate, gold panel, detector and its hint), inside the safe area
            Rect Rectangle(RectTransform rt)
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            }

            Rect box = Rectangle((RectTransform)banner.transform);
            Transform canvas = banner.GetComponentInParent<Canvas>().rootCanvas.transform;
            Transform hud = canvas.Find("SafeAreaRoot/HUD");
            foreach (string name in new[] { "LevelPlate", "HeaderPanel", "ActionButtons" })
            {
                Rect other = Rectangle((RectTransform)hud.Find(name));
                Assert.That(box.Overlaps(other), Is.False, "the area banner does not cover " + name);
            }

            Transform detectorTransform = canvas.Find("SafeAreaRoot/DetectorIndicator");
            foreach (RectTransform rt in detectorTransform.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.GetComponent<Graphic>() != null)
                {
                    Assert.That(box.Overlaps(Rectangle(rt)), Is.False, "the area banner does not cover the detector (" + rt.name + ")");
                }
            }

            Assert.That(box.Overlaps(Rectangle((RectTransform)canvas.Find("SafeAreaRoot/MobileControls/MovementJoystick"))), Is.False, "nor the joystick");
            Rect safe = Rectangle((RectTransform)canvas.Find("SafeAreaRoot"));
            Assert.That(safe.Contains(box.min) && safe.Contains(box.max), Is.True, "inside the safe area");

            yield return new WaitForSecondsRealtime(banner.TotalDuration + 0.4f);
            Assert.That(banner.IsShowing, Is.False, "gone again after the pop, hold and fade");
            Assert.That(notice.ShownCount, Is.EqualTo(1));

            // a new area: shown once; going back and forth soon after never repeats a name
            PlaceOn(-17f, 2f);
            yield return WaitUntil(() => banner.IsShowing, 4f);
            Assert.That(banner.CurrentTitle, Is.EqualTo("HIDDEN RAVINE"));
            yield return new WaitForSecondsRealtime(banner.TotalDuration + 0.4f);
            PlaceOn(-34f, -22f);
            yield return new WaitForSecondsRealtime(banner.TotalDuration + 3.5f);
            Assert.That(notice.ShownCount, Is.EqualTo(2), "the outpost was shown a few seconds ago: no repeat");
            PlaceOn(-17f, 2f);
            yield return new WaitForSecondsRealtime(banner.TotalDuration + 3.5f);
            Assert.That(notice.ShownCount, Is.EqualTo(2), "nor the ravine");

            // not while the game is paused: the name waits until it resumes
            PlaceOn(43f, 36f);
            yield return new WaitForSecondsRealtime(0.1f);
            GamePause.Pause();
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(banner.IsShowing, Is.False, "nothing pops up behind the pause menu");
            GamePause.Resume();
            yield return WaitUntil(() => banner.IsShowing, 3f);
            Assert.That(banner.CurrentTitle, Is.EqualTo("CANYON OVERLOOK"));
            Assert.That(notice.ShownCount, Is.EqualTo(3));
        }

        // ================================================================ walls, the pocket's rock, the map edges

        [UnityTest]
        public IEnumerator Walls_TheMapEdges_AndThePocketsRock_HoldForBothMiners_WithAWellBehavedCamera()
        {
            foreach (string miner in Miners)
            {
                yield return Load(miner);
                var controller = Player.GetComponent<ThirdPersonPlayerController>();
                Camera camera = Camera.main;
                Collider terrain = Env.Find("Terrain").GetComponent<Collider>();

                // (a) pushed into the back wall of the hidden pocket: the rock mass there stops the miner, the camera stays out of the rock
                PlaceOn(-17f, 2.5f);
                yield return new WaitForSeconds(0.5f);
                int cameraInRock = 0;
                for (float t = 0f; t < 4f; t += Time.deltaTime)
                {
                    Push(controller, camera, Vector3.left);
                    yield return null;
                    cameraInRock += Physics.OverlapSphere(camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore).Length > 0 ? 1 : 0;
                }

                controller.ClearMoveInput();
                Transform outcrop = Env.Find("E_HiddenRavine/Colliders/PocketOutcrop");
                Assert.That(outcrop, Is.Not.Null, "the pocket's rock mass has a collider");
                Assert.That(Vector3.Distance(new Vector3(Player.position.x, outcrop.position.y, Player.position.z), outcrop.position), Is.GreaterThan(outcrop.GetComponent<SphereCollider>().radius), miner + ": the miner stands in front of the rock, not inside it");
                Assert.That(Player.position.x, Is.GreaterThan(-21f), miner + ": did not walk through the rock into the back wall");
                Assert.That(cameraInRock, Is.EqualTo(0), miner + ": the camera never ended up inside the rock");

                // (b) pushed into the nearest rock wall from several places: nobody climbs it, falls through the map or leaves the playable ground
                foreach ((float x, float z) in new[] { (-34f, -22f), (6f, -14f), (-18f, 16f), (43f, 36f), (-5f, -8f) })
                {
                    PlaceOn(x, z);
                    yield return new WaitForSeconds(0.4f);
                    float startY = Player.position.y;
                    Vector3 wall = Vector3.zero;
                    float best = 999f;
                    for (int i = 0; i < 16; i++)
                    {
                        Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                        if (terrain.Raycast(new Ray(Player.position + Vector3.up * 0.2f, d), out RaycastHit hit, 14f) && hit.distance < best && hit.normal.y < 0.6f)
                        {
                            best = hit.distance;
                            wall = d;
                        }
                    }

                    Assert.That(best, Is.LessThan(14f), $"a rock wall within reach of {x},{z}");
                    float maxY = startY, minY = startY;
                    for (float t = 0f; t < 4f; t += Time.deltaTime)
                    {
                        Push(controller, camera, wall);
                        yield return null;
                        maxY = Mathf.Max(maxY, Player.position.y);
                        minY = Mathf.Min(minY, Player.position.y);
                    }

                    controller.ClearMoveInput();
                    Assert.That(Physics.Raycast(Player.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit under, 3f, ~0, QueryTriggerInteraction.Ignore), Is.True, "ground under the miner");
                    Assert.That(under.normal.y, Is.GreaterThan(0.7f), $"{miner}: pushed into the wall near {x},{z}: stands on a floor, not on the wall (normal {under.normal})");
                    Assert.That(maxY - startY, Is.LessThan(5f), $"{miner}: pushed into the wall near {x},{z}: did not climb it (rose {maxY - startY:F2} m)");
                    Assert.That(minY - startY, Is.GreaterThan(-1.0f), $"{miner}: did not fall near {x},{z}");
                    Assert.That(Mathf.Abs(Player.position.x), Is.LessThan(52f), "inside the map in x");
                    Assert.That(Mathf.Abs(Player.position.z), Is.LessThan(42f), "inside the map in z");
                }

                yield return TearDownScene();
            }
        }

        // ================================================================ gameplay and Green Mine untouched

        [UnityTest]
        public IEnumerator TheTwentyTreasures_840Gold_AndTheThresholds_AreUntouched_AndGreenMineHasNoCanyonAtmosphere()
        {
            yield return Load();
            int count = 0, gold = 0;
            var ids = new HashSet<string>();
            foreach (TreasureSpot s in RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true))
            {
                count++;
                gold += s.Definition.GoldValue;
                ids.Add(s.SpotId);
                Assert.That(s.SpotId, Does.StartWith("rocky_canyon."));
            }

            Assert.That(count, Is.EqualTo(20));
            Assert.That(ids.Count, Is.EqualTo(20), "unique ids");
            Assert.That(gold, Is.EqualTo(840));
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Assert.That(root.GetComponentInChildren<MineInteriorZone>(true), Is.Null, "no Green Mine mine zone here");
                Assert.That(root.GetComponentInChildren<AreaAmbience>(true), Is.Null, "and no Green Mine forest ambience");
            }

            Assert.That(One<ProgressManager>().Service.IsAreaUnlocked("rocky_canyon"), Is.True);
            yield return TearDownScene();

            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/GreenMine.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/GreenMine.unity");
            SceneManager.SetActiveScene(scene);
            yield return null;
            int green = 0, greenGold = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Assert.That(root.name, Is.Not.EqualTo("CanyonAtmosphere"));
                Assert.That(root.GetComponentInChildren<CanyonAmbience>(true), Is.Null, "Green Mine keeps its own forest and cave ambience");
                Assert.That(root.GetComponentInChildren<CanyonZoneMood>(true), Is.Null);
                foreach (TreasureSpot s in root.GetComponentsInChildren<TreasureSpot>(true))
                {
                    green++;
                    greenGold += s.Definition.GoldValue;
                }
            }

            Assert.That(green, Is.EqualTo(15), "Green Mine's 15 treasures");
            Assert.That(greenGold, Is.EqualTo(625), "and its 625 gold");
            Assert.That(Object.FindAnyObjectByType<AreaAmbience>(), Is.Not.Null, "its AreaAmbience is still there");
        }
    }
}
#endif
