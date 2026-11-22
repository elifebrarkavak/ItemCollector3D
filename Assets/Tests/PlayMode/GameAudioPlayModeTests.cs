#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Audio;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using ItemCollector3D.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Game audio (stage 12A): the synthesised sounds are short, soft and click-free; the detector beep follows the pulse rhythm; dig / reveal / collect
    /// sounds play only at their own moments; the area ambience cross-fades outdoors / mine, follows the SFX volume and obeys Pause. As everywhere in
    /// this project, these tests check that sounds are TRIGGERED with the right parameters, not how they sound on a device. In-memory stores keep
    /// the developer's save and settings untouched.
    /// </summary>
    public sealed class GameAudioPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/GreenMine.unity";

        private Scene scene;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.Current.ResetToDefaults();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            GamePause.Resume();
            GameSettings.Current.ResetToDefaults();
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            scene = default;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator LoadScene()
        {
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
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

            Assert.Fail("Not found in GreenMine: " + typeof(T).Name);
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

        private TreasureSpot Spot(string id)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TreasureSpot spot in root.GetComponentsInChildren<TreasureSpot>(true))
                {
                    if (spot.SpotId == id)
                    {
                        return spot;
                    }
                }
            }

            Assert.Fail("Treasure spot not found: " + id);
            return null;
        }

        private Transform Player
        {
            get
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Player")
                    {
                        return root.transform;
                    }
                }

                Assert.Fail("Player not found");
                return null;
            }
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        /// <summary>A free standing place on open ground at the given horizontal distance from the spot.</summary>
        private static Vector3 PlaceAtDistance(TreasureSpot spot, float distance)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                Vector3 p = spot.transform.position + new Vector3(Mathf.Cos(a) * distance, 0f, Mathf.Sin(a) * distance);
                if (Physics.Raycast(new Vector3(p.x, spot.transform.position.y + 3f, p.z), Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)
                    && !Physics.CheckCapsule(hit.point + Vector3.up * 0.6f, hit.point + Vector3.up * 1.5f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    return hit.point + Vector3.up * 1.1f;
                }
            }

            Assert.Fail($"No free standing place {distance} m from {spot.SpotId}");
            return default;
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

        private static float Peak(float[] data)
        {
            float peak = 0f;
            foreach (float f in data)
            {
                Assert.That(float.IsFinite(f), Is.True, "no NaN / infinity in the audio");
                peak = Mathf.Max(peak, Mathf.Abs(f));
            }

            return peak;
        }

        private static readonly string[] Miners = { CharacterSelection.MaleId, CharacterSelection.FemaleId };

        // ================================================================ the sounds themselves

        [Test]
        public void SynthesisedOneShots_AreShort_Soft_AndStartAndEndInSilence()
        {
            var clips = new Dictionary<string, (AudioClip clip, float maxSeconds)>
            {
                ["beep"] = (SoundSynth.Beep(), 0.1f),
                ["dig"] = (SoundSynth.DigStroke(), 0.3f),
                ["reveal"] = (SoundSynth.Reveal(), 0.8f),
                ["coin"] = (SoundSynth.CollectCoin(), 0.9f),
                ["epic"] = (SoundSynth.CollectEpic(), 2f),
            };
            try
            {
                foreach (KeyValuePair<string, (AudioClip clip, float maxSeconds)> pair in clips)
                {
                    AudioClip clip = pair.Value.clip;
                    float[] data = Samples(clip);
                    Assert.That(clip.length, Is.LessThanOrEqualTo(pair.Value.maxSeconds), pair.Key + " is short");
                    Assert.That(clip.length, Is.GreaterThan(0.05f), pair.Key);
                    float peak = Peak(data);
                    Assert.That(peak, Is.InRange(0.3f, 0.9f), pair.Key + " is audible but never near clipping (soft)");
                    Assert.That(Mathf.Abs(data[0]), Is.LessThan(1e-4f), pair.Key + " starts in silence (no click)");
                    Assert.That(Mathf.Abs(data[data.Length - 1]), Is.LessThan(1e-4f), pair.Key + " ends in silence (no click)");
                    Assert.That(clip.channels, Is.EqualTo(1), pair.Key + " is mono (small, cheap on a phone)");
                }

                Assert.That(clips["epic"].clip.length, Is.GreaterThan(clips["coin"].clip.length * 1.5f), "the Epic reward is a longer, more special sound");
                Assert.That(clips["beep"].clip.length, Is.LessThan(DetectorSignalIndicator.MinBeepSpacing), "a beep ends before the next one may start");
            }
            finally
            {
                foreach (var pair in clips)
                {
                    Object.DestroyImmediate(pair.Value.clip);
                }
            }
        }

        [Test]
        public void AmbienceLoops_AreSeamless_Calm_AndSmall()
        {
            var forest = SoundSynth.ForestAmbience();
            var cave = SoundSynth.CaveAmbience();
            try
            {
                foreach (AudioClip clip in new[] { forest, cave })
                {
                    float[] data = Samples(clip);
                    Assert.That(clip.length, Is.InRange(8f, 15f), clip.name + " is a short loop");
                    Assert.That(clip.frequency, Is.LessThanOrEqualTo(22050), clip.name + " is low-rate (small, mobile friendly)");
                    Assert.That(Peak(data), Is.InRange(0.3f, 0.7f), clip.name + " is calm, never loud");

                    // seamless: the step from the last sample back to the first is no bigger than the biggest ordinary step inside the loop
                    float biggest = 0f;
                    for (int i = 1; i < data.Length; i++)
                    {
                        biggest = Mathf.Max(biggest, Mathf.Abs(data[i] - data[i - 1]));
                    }

                    float seam = Mathf.Abs(data[0] - data[data.Length - 1]);
                    Assert.That(seam, Is.LessThanOrEqualTo(biggest), clip.name + " loops without a click");

                    double mean = 0;
                    foreach (float f in data)
                    {
                        mean += f;
                    }

                    Assert.That(System.Math.Abs(mean / data.Length), Is.LessThan(0.01), clip.name + " has no DC offset");
                }
            }
            finally
            {
                Object.DestroyImmediate(forest);
                Object.DestroyImmediate(cave);
            }
        }

        [Test]
        public void BeepPitch_RisesSteadily_FromSlowToFastToRapid()
        {
            float slow = DetectorSignalIndicator.BeepPitchFor(0.2f);
            float fast = DetectorSignalIndicator.BeepPitchFor(0.55f);
            float rapid = DetectorSignalIndicator.BeepPitchFor(0.9f);
            Assert.That(DetectorSignalIndicator.LevelFor(true, 0.2f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.Slow));
            Assert.That(DetectorSignalIndicator.LevelFor(true, 0.55f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.Fast));
            Assert.That(DetectorSignalIndicator.LevelFor(true, 0.9f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.Rapid));
            Assert.That(slow, Is.LessThan(fast));
            Assert.That(fast, Is.LessThan(rapid));
            Assert.That(DetectorSignalIndicator.BeepPitchFor(-1f), Is.EqualTo(DetectorSignalIndicator.FarthestBeepPitch));
            Assert.That(DetectorSignalIndicator.BeepPitchFor(5f), Is.EqualTo(DetectorSignalIndicator.NearestBeepPitch));
            Assert.That(660f * DetectorSignalIndicator.NearestBeepPitch, Is.LessThan(1000f), "the highest beep stays well below a shrill range");
        }

        // ================================================================ detector

        [UnityTest]
        public IEnumerator DetectorBeeps_GetFaster_Higher_AndLouder_AsTheTreasureGetsCloser_WithoutPilingUp()
        {
            foreach (string miner in Miners)
            {
                CharacterSelection.Select(miner);
                yield return LoadScene();
                var indicator = One<DetectorSignalIndicator>();
                var detector = One<TreasureDetector>();
                float[] proximity = { 0.2f, 0.55f, 0.92f };
                TreasureSpot spot = Spot("green_mine.forest_03");
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (TreasureSpot other in root.GetComponentsInChildren<TreasureSpot>(true))
                    {
                        if (other != spot)
                        {
                            other.gameObject.SetActive(false); // the neighbours' detection circles overlap everywhere: test this detector reading alone
                        }
                    }
                }

                float digRadius = spot.Definition.DigRadius, detectionRadius = spot.Definition.DetectionRadius;

                var rate = new float[3];
                var pitch = new float[3];
                var volume = new float[3];
                var levels = new[] { DetectorSignalIndicator.SignalLevel.Slow, DetectorSignalIndicator.SignalLevel.Fast, DetectorSignalIndicator.SignalLevel.Rapid };
                float[] seconds = { 4.4f, 2.6f, 1.6f };
                float minSpacing = float.MaxValue;
                for (int phase = 0; phase < 3; phase++)
                {
                    Teleport(Player, PlaceAtDistance(spot, detectionRadius - proximity[phase] * (detectionRadius - digRadius)));
                    yield return new WaitForSecondsRealtime(0.4f);
                    Assert.That(indicator.Level, Is.EqualTo(levels[phase]), $"{miner}: the HUD level for phase {phase} (the detector maths is unchanged)");

                    int beeps = indicator.BeepCount, pulses = indicator.PulseCount;
                    float start = Time.realtimeSinceStartup, lastBeepAt = -1f;
                    int seen = beeps;
                    while (Time.realtimeSinceStartup - start < seconds[phase])
                    {
                        yield return null;
                        if (indicator.BeepCount != seen)
                        {
                            float now = Time.realtimeSinceStartup;
                            if (lastBeepAt > 0f)
                            {
                                minSpacing = Mathf.Min(minSpacing, now - lastBeepAt);
                            }

                            lastBeepAt = now;
                            seen = indicator.BeepCount;
                        }
                    }

                    int newBeeps = indicator.BeepCount - beeps;
                    Assert.That(newBeeps, Is.GreaterThan(0), $"{miner}: phase {phase} beeps");
                    Assert.That(newBeeps, Is.EqualTo(indicator.PulseCount - pulses), $"{miner}: every detector pulse is voiced exactly once, none extra");
                    rate[phase] = newBeeps / seconds[phase];
                    pitch[phase] = indicator.LastBeepPitch;
                    volume[phase] = indicator.LastBeepVolume;
                }

                Assert.That(rate[0], Is.LessThan(rate[1]), $"{miner}: SLOW beeps less often than FAST ({rate[0]:F2}/s vs {rate[1]:F2}/s)");
                Assert.That(rate[1], Is.LessThan(rate[2]), $"{miner}: FAST beeps less often than RAPID ({rate[1]:F2}/s vs {rate[2]:F2}/s)");
                Assert.That(pitch[0], Is.LessThan(pitch[1]).And.LessThan(pitch[2]), $"{miner}: the pitch rises");
                Assert.That(pitch[1], Is.LessThan(pitch[2]), miner);
                Assert.That(volume[0], Is.LessThan(volume[2]), $"{miner}: and so does the loudness");
                Assert.That(volume[2], Is.LessThan(0.6f), $"{miner}: even the loudest beep is soft");
                Assert.That(minSpacing, Is.GreaterThanOrEqualTo(DetectorSignalIndicator.MinBeepSpacing * 0.8f), $"{miner}: beeps never pile up");
                Debug.Log($"Detector audio ({miner}): beeps/s {rate[0]:F2} / {rate[1]:F2} / {rate[2]:F2}, pitch {pitch[0]:F2} / {pitch[1]:F2} / {pitch[2]:F2}, volume {volume[0]:F2} / {volume[1]:F2} / {volume[2]:F2}, closest spacing {minSpacing:F2}s");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        // ================================================================ dig / reveal / collect

        [UnityTest]
        public IEnumerator DigRevealCollect_Sounds_PlayOnlyAtTheirOwnMoments_AndEpicIsSpecial()
        {
            foreach (string miner in Miners)
            {
                CharacterSelection.Select(miner);
                yield return LoadScene();
                var digSound = One<DigSoundPlayer>();
                var collectSound = One<CollectSoundPlayer>();
                var animator = One<PlayerDigAnimator>();
                var hunt = One<TreasureHuntController>();
                Button dig = FindButton("DigButton"), collect = FindButton("CollectButton");

                // standing around with a hidden treasure far away: nothing plays
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.That(digSound.PlayCount + digSound.RevealCount + collectSound.PlayCount, Is.EqualTo(0), $"{miner}: silent before any dig");

                float normalLength = 0f;
                foreach (string id in new[] { "green_mine.camp_01", "green_mine.mine_room_01" })
                {
                    TreasureSpot spot = Spot(id);
                    bool epic = spot.Definition.Rarity == TreasureRarity.Epic;
                    Teleport(Player, PlaceAtDistance(spot, 1.5f));
                    yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
                    yield return new WaitForSecondsRealtime(0.3f);
                    Assert.That(dig.gameObject.activeInHierarchy, Is.True, $"{miner}: DIG offered at {id}");
                    int strokesBefore = digSound.PlayCount, revealsBefore = digSound.RevealCount, collectsBefore = collectSound.PlayCount;

                    dig.onClick.Invoke();
                    yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
                    Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), $"{miner}: {id} is revealed");
                    yield return null;
                    Assert.That(digSound.PlayCount - strokesBefore, Is.EqualTo(animator.Strokes), $"{miner}: one dig sound per stroke");
                    Assert.That(digSound.RevealCount - revealsBefore, Is.EqualTo(1), $"{miner}: exactly one discovery sound when the treasure shows");
                    Assert.That(collectSound.PlayCount, Is.EqualTo(collectsBefore), $"{miner}: no reward sound before COLLECT");

                    yield return new WaitForSecondsRealtime(0.5f);
                    Assert.That(collectSound.PlayCount, Is.EqualTo(collectsBefore), $"{miner}: still none while the treasure just lies there");
                    Assert.That(digSound.RevealCount - revealsBefore, Is.EqualTo(1), $"{miner}: and no second discovery sound");

                    yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
                    collect.onClick.Invoke();
                    yield return null;
                    Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
                    Assert.That(collectSound.PlayCount - collectsBefore, Is.EqualTo(1), $"{miner}: exactly one reward sound on COLLECT");
                    Assert.That(collectSound.LastWasSpecial, Is.EqualTo(epic), $"{miner}: only Epic treasure gets the special sound ({id})");
                    Assert.That(collectSound.IsPlaying, Is.True);
                    float length = collectSound.GetComponent<AudioSource>().clip.length;
                    if (!epic)
                    {
                        normalLength = length;
                    }
                    else
                    {
                        Assert.That(length, Is.GreaterThan(normalLength * 1.5f), $"{miner}: the Epic reward sound is longer / richer than the normal one");
                    }

                    // collecting again (or pressing DIG on it) makes no sound
                    int strokesAfter = digSound.PlayCount, collectsAfter = collectSound.PlayCount;
                    collect.onClick.Invoke();
                    dig.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.3f);
                    Assert.That(collectSound.PlayCount, Is.EqualTo(collectsAfter), miner);
                    Assert.That(digSound.PlayCount, Is.EqualTo(strokesAfter), miner);
                }

                // a dig the player walks away from: no discovery sound, no reward sound
                TreasureSpot far = Spot("green_mine.forest_03");
                Teleport(Player, PlaceAtDistance(far, 1.5f));
                yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
                int reveals = digSound.RevealCount, collects = collectSound.PlayCount;
                dig.onClick.Invoke();
                Teleport(Player, PlaceAtDistance(far, far.Definition.DigRadius + 3f));
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(far.State, Is.EqualTo(TreasureState.Hidden), $"{miner}: the cancelled dig revealed nothing");
                Assert.That(digSound.RevealCount, Is.EqualTo(reveals), $"{miner}: so no discovery sound");
                Assert.That(collectSound.PlayCount, Is.EqualTo(collects), $"{miner}: and no reward sound");
                Assert.That(hunt.IsDigging, Is.False);

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Detector_StepsBackWhileDigging_SoTheDigSoundStaysClear()
        {
            yield return LoadScene();
            var indicator = One<DetectorSignalIndicator>();
            var hunt = One<TreasureHuntController>();
            TreasureSpot spot = Spot("green_mine.camp_01");
            Button dig = FindButton("DigButton");
            Teleport(Player, PlaceAtDistance(spot, 1.5f));
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            yield return WaitUntil(() => indicator.BeepCount >= 2, 3f);
            float normal = indicator.LastBeepVolume;
            Assert.That(normal, Is.GreaterThan(0f));

            // make the dig long enough to catch a pulse inside it
            typeof(TreasureHuntController).GetField("digDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(hunt, 1.2f);
            dig.onClick.Invoke();
            int before = indicator.BeepCount;
            yield return WaitUntil(() => hunt.IsDigging && indicator.BeepCount > before, 2f);
            Assert.That(hunt.IsDigging, Is.True, "a beep came during the dig");
            Assert.That(indicator.LastBeepVolume, Is.EqualTo(normal * DetectorSignalIndicator.DiggingBeepFactor).Within(normal * 0.05f), "and it was ducked while digging");
        }

        // ================================================================ ambience

        [UnityTest]
        public IEnumerator Ambience_CrossfadesGentlyFromOutdoorsToTheMine_AndBack()
        {
            foreach (string miner in Miners)
            {
                CharacterSelection.Select(miner);
                yield return LoadScene();
                var ambience = One<AreaAmbience>();
                AudioSource outdoor = ambience.OutdoorSource, cave = ambience.CaveSource;
                Assert.That(outdoor.clip, Is.Not.Null);
                Assert.That(cave.clip, Is.Not.Null);
                Assert.That(outdoor.loop && cave.loop, Is.True, "both are loops");
                Assert.That(outdoor.ignoreListenerPause || cave.ignoreListenerPause, Is.False, "the ambience pauses with the game (only the music keeps playing)");

                Teleport(Player, new Vector3(6.3f, 1.3f, 10.5f));
                yield return new WaitForSecondsRealtime(2.2f);
                Assert.That(outdoor.isPlaying && cave.isPlaying, Is.True, $"{miner}: both loops run");
                Assert.That(ambience.InteriorBlend, Is.EqualTo(0f).Within(0.01f), $"{miner}: outdoors");
                Assert.That(outdoor.volume, Is.GreaterThan(0.05f), $"{miner}: the nature loop is heard outdoors");
                Assert.That(outdoor.volume, Is.LessThan(0.35f), $"{miner}: but it stays light");
                Assert.That(cave.volume, Is.LessThan(0.005f), $"{miner}: and the cave loop is silent");
                Assert.That(ambience.OutdoorCutoff, Is.GreaterThan(20000f), $"{miner}: nothing is muffled outdoors");
                float outdoorVolume = outdoor.volume;

                // into the tunnel: watch every frame, nothing may jump
                Teleport(Player, new Vector3(6.3f, 1.3f, 20.5f));
                float lastOut = outdoor.volume, lastCave = cave.volume, biggestStep = 0f, start = Time.realtimeSinceStartup;
                bool cutoffFell = false;
                while (Time.realtimeSinceStartup - start < 3.2f)
                {
                    yield return null;
                    biggestStep = Mathf.Max(biggestStep, Mathf.Abs(outdoor.volume - lastOut), Mathf.Abs(cave.volume - lastCave));
                    lastOut = outdoor.volume;
                    lastCave = cave.volume;
                    cutoffFell |= ambience.OutdoorCutoff < 20000f;
                }

                Assert.That(ambience.InteriorBlend, Is.EqualTo(1f).Within(0.01f), $"{miner}: inside the mine");
                Assert.That(cave.volume, Is.GreaterThan(outdoor.volume * 3f), $"{miner}: the cave loop leads inside");
                Assert.That(cave.volume, Is.GreaterThan(0.05f), miner);
                Assert.That(outdoor.volume, Is.LessThan(outdoorVolume * 0.2f), $"{miner}: the nature loop is only a faint trace through the tunnel mouth");
                Assert.That(ambience.OutdoorCutoff, Is.LessThan(1500f), $"{miner}: and muffled");
                Assert.That(cutoffFell, Is.True);
                Assert.That(biggestStep, Is.LessThan(0.02f), $"{miner}: the change is smooth (largest frame-to-frame volume step {biggestStep:F4})");

                // and back out
                Teleport(Player, new Vector3(6.3f, 1.3f, 10.5f));
                yield return new WaitForSecondsRealtime(3.2f);
                Assert.That(ambience.InteriorBlend, Is.EqualTo(0f).Within(0.01f), $"{miner}: outdoors again");
                Assert.That(outdoor.volume, Is.EqualTo(outdoorVolume).Within(0.01f), miner);
                Assert.That(cave.volume, Is.LessThan(0.005f), miner);
                Debug.Log($"Ambience ({miner}): outdoors {outdoorVolume:F3}, inside cave {lastCave:F3} / outdoor {lastOut:F3}, biggest frame step {biggestStep:F4}");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Ambience_FollowsTheSfxVolume_AndDoesNotTouchTheMusic()
        {
            yield return LoadScene();
            var ambience = One<AreaAmbience>();
            var music = One<AmbientMusicPlayer>();
            Teleport(Player, new Vector3(6.3f, 1.3f, 10.5f));
            yield return new WaitForSecondsRealtime(2f);

            float full = ambience.OutdoorSource.volume;
            float musicVolume = music.CurrentVolume;
            Assert.That(full, Is.GreaterThan(0f));
            Assert.That(musicVolume, Is.GreaterThan(full), "the music stays the leading layer: the ambience is the quietest");

            GameSettings.Current.SetSfxVolume(0.5f);
            yield return null;
            yield return null;
            Assert.That(ambience.OutdoorSource.volume, Is.EqualTo(full * 0.5f).Within(0.002f), "half SFX volume = half ambience");
            Assert.That(music.CurrentVolume, Is.EqualTo(musicVolume).Within(1e-4f), "the SFX slider never moves the music");

            GameSettings.Current.SetSfxVolume(0f);
            yield return null;
            Assert.That(ambience.OutdoorSource.volume, Is.EqualTo(0f).Within(1e-5f), "SFX 0 = silent ambience");
            Assert.That(ambience.CaveSource.volume, Is.EqualTo(0f).Within(1e-5f));

            GameSettings.Current.SetMusicVolume(0.4f);
            GameSettings.Current.SetSfxVolume(1f);
            yield return null;
            yield return null;
            Assert.That(ambience.OutdoorSource.volume, Is.EqualTo(full).Within(0.002f), "the music slider never moves the ambience");
            Assert.That(music.IsPlaying, Is.True, "the music system is untouched");
        }

        [UnityTest]
        public IEnumerator Ambience_DucksWhileDigging_AndAfterAReward()
        {
            yield return LoadScene();
            var ambience = One<AreaAmbience>();
            var hunt = One<TreasureHuntController>();
            TreasureSpot spot = Spot("green_mine.camp_01");
            Button dig = FindButton("DigButton"), collect = FindButton("CollectButton");
            Teleport(Player, PlaceAtDistance(spot, 1.5f));
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(ambience.Duck, Is.EqualTo(1f).Within(0.01f), "normal level while nothing happens");

            typeof(TreasureHuntController).GetField("digDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(hunt, 1.0f);
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            dig.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(hunt.IsDigging, Is.True);
            Assert.That(ambience.Duck, Is.EqualTo(AreaAmbience.DiggingDuck).Within(0.02f), "ducked while digging");

            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            collect.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(ambience.Duck, Is.LessThan(0.6f), "still ducked right after the reward");
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.That(ambience.Duck, Is.EqualTo(1f).Within(0.01f), "and back to normal afterwards");
        }

        // ================================================================ pause / resume / scene change

        [UnityTest]
        public IEnumerator Pause_FreezesAmbienceAndBeeps_TheMusicKeepsPlaying_AndResumeContinues()
        {
            yield return LoadScene();
            var ambience = One<AreaAmbience>();
            var music = One<AmbientMusicPlayer>();
            var indicator = One<DetectorSignalIndicator>();
            TreasureSpot spot = Spot("green_mine.forest_03");
            Teleport(Player, PlaceAtDistance(spot, spot.Definition.DetectionRadius - 0.5f));
            yield return new WaitForSecondsRealtime(2f);
            AudioSource source = ambience.OutdoorSource;
            Assert.That(source.isPlaying, Is.True);

            int a = source.timeSamples;
            int m = music.Source.timeSamples;
            yield return new WaitForSecondsRealtime(0.4f);
            Assume.That(source.timeSamples, Is.Not.EqualTo(a), "the audio engine is running in this environment");

            int beepsBefore = indicator.BeepCount;
            GamePause.Pause();
            yield return null;
            int p0 = source.timeSamples;
            int musicP0 = music.Source.timeSamples;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(source.timeSamples, Is.EqualTo(p0).Within(1500), "the ambience loop stands still while paused");
            Assert.That(ambience.CaveSource.timeSamples, Is.Not.Negative);
            Assert.That(music.Source.timeSamples, Is.Not.EqualTo(musicP0), "the music keeps playing in the pause menu");
            Assert.That(indicator.BeepCount, Is.EqualTo(beepsBefore), "no beeps while paused");

            GamePause.Resume();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(source.timeSamples, Is.Not.EqualTo(p0), "the ambience carries on after Resume");
            Assert.That(source.isPlaying, Is.True);
            Assert.That(ambience.OutdoorSource.volume, Is.GreaterThan(0.05f), "at its normal level");
        }

        [UnityTest]
        public IEnumerator LeavingTheArea_RemovesTheAmbience_AndNothingKeepsPlaying()
        {
            yield return LoadScene();
            var ambience = One<AreaAmbience>();
            AudioSource outdoor = ambience.OutdoorSource, cave = ambience.CaveSource;
            GamePause.Pause();
            Assert.That(AudioListener.pause, Is.True);
            GamePause.Resume(); // what the scene navigator does before it loads another scene
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return null;
            Assert.That(outdoor == null && cave == null, Is.True, "the ambience sources go with the scene");
            Assert.That(AudioListener.pause, Is.False);
        }
    }
}
#endif
