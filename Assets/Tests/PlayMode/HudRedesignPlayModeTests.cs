#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Treasure;
using ItemCollector3D.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// The redesigned gameplay HUD (stage 9): it fits the target phone screens (16:9 and wider) without overlapping or covering the play area,
    /// uses the menu font, shows the detector as NO SIGNAL / SLOW / FAST / RAPID, and goes away while the pause menu or the settings panel
    /// opened from it is shown. The data connections (gold, DIG / COLLECT, progress) are covered by the existing treasure and progress tests.
    /// </summary>
    public sealed class HudRedesignPlayModeTests
    {
        private const string GameplayPath = "Assets/Scenes/GameplayTest.unity";

        /// <summary>48 dp on a typical Android phone (1080 px high, 2.625 px per dp) is 126 reference units.</summary>
        private const float MinTouchUnits = 126f;

        /// <summary>Every HUD piece keeps at least this much room to the screen edge (reference units).</summary>
        private const float EdgeMargin = 16f;

        /// <summary>All HUD pieces together may cover at most this share of the screen: the play area stays free.</summary>
        private const float MaxHudShare = 0.2f;

        private static readonly (string name, int width, int height)[] TargetScreens =
        {
            ("16:9", 1920, 1080), ("18:9", 2160, 1080), ("19.5:9", 2340, 1080), ("20:9", 2400, 1080), ("21:9", 2520, 1080), ("2.6:1", 2808, 1080),
        };

        private Scene loadedScene;
        private TestGameView gameView;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore(); // never touch the real PlayerPrefs
            CharacterSelection.ResetCurrent();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GamePause.Resume();
            if (gameView != null)
            {
                yield return gameView.Restore(); // also when an assertion failed in the test
                gameView = null;
            }

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
        }

        private IEnumerator LoadGameplay()
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(GameplayPath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null, GameplayPath);
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(GameplayPath);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
            yield return null;
        }

        private T Find<T>(string name) where T : Component
        {
            foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            {
                if (item.name == name && item.gameObject.scene == loadedScene)
                {
                    return item;
                }
            }

            Assert.Fail($"{typeof(T).Name} '{name}' not found in {loadedScene.name}");
            return null;
        }

        private static Rect CanvasRect(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var canvasTransform = (RectTransform)canvas.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = canvasTransform.InverseTransformPoint(corners[0]);
            Vector3 max = canvasTransform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax - 0.5f && a.xMax > b.xMin + 0.5f && a.yMin < b.yMax - 0.5f && a.yMax > b.yMin + 0.5f;
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        // ------------------------------------------------------------------ layout on the target screens

        [UnityTest]
        public IEnumerator Hud_FitsEveryTargetScreen_WithoutOverlapOrCoveringThePlayArea()
        {
            yield return LoadGameplay();

            // Measured on every target screen with the Game View really set to that resolution (restored by TearDown).
            gameView = new TestGameView();
            foreach ((string name, int width, int height) screen in TargetScreens)
            {
                gameView.SetResolution(screen.width, screen.height);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(screen.width, screen.height)),
                    $"the Game View did not switch to {screen.name} ({screen.width}x{screen.height}); the layout check would measure the wrong screen");

                try
                {
                    AssertHudLayout();
                }
                catch (AssertionException e)
                {
                    throw new AssertionException($"On the {screen.name} screen ({screen.width}x{screen.height}): {e.Message}", e);
                }
            }
        }

        private void AssertHudLayout()
        {
            Canvas canvas = Find<HudController>("HUD").GetComponentInParent<Canvas>().rootCanvas;
            Rect screen = CanvasRect((RectTransform)canvas.transform);
            var onScreen = Rect.MinMaxRect(screen.xMin + EdgeMargin, screen.yMin + EdgeMargin, screen.xMax - EdgeMargin, screen.yMax - EdgeMargin);

            string[] names = { "LevelPlate", "PauseButton", "HeaderPanel", "DetectorIndicator", "MovementJoystick", "DigButton", "CollectButton" };
            var rects = new Dictionary<string, Rect>();
            foreach (string name in names)
            {
                Transform part = Find<Transform>(name);
                Assert.That(part.GetComponentInParent<SafeAreaFitter>(), Is.Not.Null, $"{name} lives under the safe-area root, so notches never cover it");
                Rect rect = CanvasRect((RectTransform)part);
                rects[name] = rect;
                Assert.That(onScreen.Contains(rect.min) && onScreen.Contains(rect.max), Is.True, $"{name} stays on screen with a margin ({rect})");
            }

            // every piece sits in its corner: PAUSE + level name top left, gold top right, joystick bottom left, DIG / COLLECT bottom right, detector top centre
            Vector2 centre = screen.center;
            Assert.That(rects["PauseButton"].center.x, Is.LessThan(centre.x));
            Assert.That(rects["PauseButton"].center.y, Is.GreaterThan(centre.y));
            Assert.That(rects["LevelPlate"].center.y, Is.GreaterThan(centre.y));
            Assert.That(rects["HeaderPanel"].center.x, Is.GreaterThan(centre.x));
            Assert.That(rects["HeaderPanel"].center.y, Is.GreaterThan(centre.y));
            Assert.That(rects["MovementJoystick"].center.x, Is.LessThan(centre.x));
            Assert.That(rects["MovementJoystick"].center.y, Is.LessThan(centre.y));
            Assert.That(rects["DigButton"].center.x, Is.GreaterThan(centre.x));
            Assert.That(rects["DigButton"].center.y, Is.LessThan(centre.y));
            Assert.That(rects["DetectorIndicator"].center.x, Is.EqualTo(centre.x).Within(1f));
            Assert.That(rects["DetectorIndicator"].center.y, Is.GreaterThan(centre.y));

            // DIG and COLLECT share ONE big slot (HudController shows whichever applies) and are easy to hit
            Assert.That(rects["CollectButton"].center.x, Is.EqualTo(rects["DigButton"].center.x).Within(0.5f));
            Assert.That(rects["CollectButton"].center.y, Is.EqualTo(rects["DigButton"].center.y).Within(0.5f));
            Assert.That(rects["DigButton"].width, Is.GreaterThanOrEqualTo(MinTouchUnits * 2f), "DIG / COLLECT are big buttons");
            Assert.That(rects["DigButton"].height, Is.GreaterThanOrEqualTo(MinTouchUnits * 1.3f));
            Assert.That(rects["DigButton"].height, Is.GreaterThan(rects["PauseButton"].height), "the action button is the biggest touch target");
            Assert.That(rects["PauseButton"].width, Is.GreaterThanOrEqualTo(MinTouchUnits));
            Assert.That(rects["PauseButton"].height, Is.GreaterThanOrEqualTo(MinTouchUnits));
            Assert.That(rects["MovementJoystick"].width, Is.GreaterThanOrEqualTo(MinTouchUnits * 2f));

            // nothing overlaps, except what is meant to: the name plate tucks under the round PAUSE button, DIG and COLLECT share a slot
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = i + 1; j < names.Length; j++)
                {
                    bool intended = (names[i] == "LevelPlate" && names[j] == "PauseButton") || (names[i] == "DigButton" && names[j] == "CollectButton");
                    if (!intended)
                    {
                        Assert.That(Overlaps(rects[names[i]], rects[names[j]]), Is.False, $"'{names[i]}' overlaps '{names[j]}'");
                    }
                }
            }

            // the play area stays free: a small share of the screen and nothing in the middle where the miner walks
            float covered = 0f;
            foreach (string name in new[] { "LevelPlate", "PauseButton", "HeaderPanel", "DetectorIndicator", "MovementJoystick", "DigButton" })
            {
                covered += rects[name].width * rects[name].height;
            }

            Assert.That(covered / (screen.width * screen.height), Is.LessThan(MaxHudShare), "the HUD covers only a small part of the screen");
            var middle = new Rect(centre.x - screen.width * 0.15f, centre.y - screen.height * 0.2f, screen.width * 0.3f, screen.height * 0.4f);
            foreach (string name in names)
            {
                Assert.That(Overlaps(rects[name], middle), Is.False, $"{name} stays out of the middle of the screen");
            }
        }

        [UnityTest]
        public IEnumerator Hud_UsesTheMenuFont_AndTheMenuButtonFamily()
        {
            yield return LoadGameplay();
            foreach (string name in new[] { "LevelTitle", "GoldText", "SignalLabel" })
            {
                TMP_Text text = Find<TMP_Text>(name);
                Assert.That(text.font.name, Is.EqualTo("IC3D Bangers SDF"), $"{name} uses the same font as the menus");
            }

            // DIG and COLLECT are the same chunky 9-slice buttons as the menu buttons (gold / green), with an icon and a label
            foreach (string name in new[] { "DigButton", "CollectButton" })
            {
                Transform button = Find<Button>(name).transform;
                Assert.That(button.Find("Face").GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced), $"{name} is a 9-slice button");
                Assert.That(button.Find("Icon"), Is.Not.Null, $"{name} has an icon");
                Assert.That(button.Find("Label").GetComponent<TMP_Text>().font.name, Is.EqualTo("IC3D Bangers SDF"));
            }

            Assert.That(Find<Button>("DigButton").transform.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("DIG"));
            Assert.That(Find<Button>("CollectButton").transform.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("COLLECT"));
        }

        // ------------------------------------------------------------------ detector meter

        [Test]
        public void SignalLevels_FollowProximity_AtTheDocumentedBoundaries()
        {
            Assert.That(DetectorSignalIndicator.LevelFor(false, 0.9f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.None), "no treasure in range: no signal, whatever the proximity");
            Assert.That(DetectorSignalIndicator.LevelFor(true, 0f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.Slow));
            Assert.That(DetectorSignalIndicator.LevelFor(true, 0.399f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.Slow));
            Assert.That(DetectorSignalIndicator.LevelFor(true, 0.4f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.Fast));
            Assert.That(DetectorSignalIndicator.LevelFor(true, 0.749f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.Fast));
            Assert.That(DetectorSignalIndicator.LevelFor(true, 0.75f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.Rapid));
            Assert.That(DetectorSignalIndicator.LevelFor(true, 1f), Is.EqualTo(DetectorSignalIndicator.SignalLevel.Rapid));

            Assert.That(DetectorSignalIndicator.LabelFor(DetectorSignalIndicator.SignalLevel.None), Is.EqualTo("NO SIGNAL"));
            Assert.That(DetectorSignalIndicator.LabelFor(DetectorSignalIndicator.SignalLevel.Slow), Is.EqualTo("SLOW"));
            Assert.That(DetectorSignalIndicator.LabelFor(DetectorSignalIndicator.SignalLevel.Fast), Is.EqualTo("FAST"));
            Assert.That(DetectorSignalIndicator.LabelFor(DetectorSignalIndicator.SignalLevel.Rapid), Is.EqualTo("RAPID"));
        }

        [UnityTest]
        public IEnumerator DetectorMeter_ShowsTheDetectorReading_NoSignalSlowFastRapid()
        {
            yield return LoadGameplay();
            TreasureSpot spot = Object.FindAnyObjectByType<TreasureSpot>();
            TreasureDetector detector = Object.FindAnyObjectByType<TreasureDetector>();
            DetectorSignalIndicator indicator = Object.FindAnyObjectByType<DetectorSignalIndicator>();
            Transform player = GameObject.Find("Player").transform;
            TMP_Text label = indicator.transform.Find("SignalLabel").GetComponent<TMP_Text>();
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden), "a fresh, in-memory save: the coin is still buried");

            float detection = spot.Definition.DetectionRadius;
            float dig = spot.Definition.DigRadius;
            (string text, DetectorSignalIndicator.SignalLevel level, int bars, float distance)[] readings =
            {
                ("NO SIGNAL", DetectorSignalIndicator.SignalLevel.None, 0, detection + 6f),
                ("SLOW", DetectorSignalIndicator.SignalLevel.Slow, 1, detection - 0.2f * (detection - dig)),
                ("FAST", DetectorSignalIndicator.SignalLevel.Fast, 2, detection - 0.55f * (detection - dig)),
                ("RAPID", DetectorSignalIndicator.SignalLevel.Rapid, 3, detection - 0.9f * (detection - dig)),
                ("NO SIGNAL", DetectorSignalIndicator.SignalLevel.None, 0, detection + 6f), // and back out again
            };

            foreach ((string text, DetectorSignalIndicator.SignalLevel level, int bars, float distance) reading in readings)
            {
                Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -reading.distance));
                yield return null;
                yield return null;
                yield return null;

                string context = $"at {reading.distance:F1} m (proximity {detector.Proximity:F2})";
                Assert.That(label.text, Is.EqualTo(reading.text), context);
                Assert.That(indicator.Level, Is.EqualTo(reading.level), context);
                Assert.That(indicator.LitBarCount, Is.EqualTo(reading.bars), "the level meter lights one bar per step: " + context);
                Assert.That(detector.HasSignal, Is.EqualTo(reading.level != DetectorSignalIndicator.SignalLevel.None), "the HUD only shows what the detector reports");
            }
        }

        // ------------------------------------------------------------------ joystick

        [UnityTest]
        public IEnumerator Joystick_IsTranslucentAtRest_AndFullyVisibleWhileHeld()
        {
            yield return LoadGameplay();
            VirtualJoystick joystick = Find<VirtualJoystick>("MovementJoystick");
            Image baseImage = joystick.GetComponent<Image>();
            float idleAlpha = baseImage.color.a;
            Assert.That(idleAlpha, Is.InRange(0.5f, 0.85f), "translucent at rest, so it never hides the ground");

            var pointer = new PointerEventData(EventSystem.current)
            {
                pointerId = 7,
                position = RectTransformUtility.WorldToScreenPoint(null, joystick.transform.position),
            };
            joystick.OnPointerDown(pointer);
            Assert.That(baseImage.color.a, Is.EqualTo(1f).Within(1e-3f), "fully visible while a finger is on it");
            joystick.OnPointerUp(pointer);
            Assert.That(baseImage.color.a, Is.EqualTo(idleAlpha).Within(1e-3f), "and calm again on release");
            Assert.That(joystick.Value, Is.EqualTo(Vector2.zero));
            yield return null;
        }

        // ------------------------------------------------------------------ pause

        [UnityTest]
        public IEnumerator Pause_HidesTheHud_AlsoForSettingsOpenedFromPause_AndResumeBringsItBack()
        {
            yield return LoadGameplay();
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();
            HideWhilePaused[] hiders = Object.FindObjectsByType<HideWhilePaused>(FindObjectsInactive.Include);
            var names = new List<string>();
            foreach (HideWhilePaused hider in hiders)
            {
                names.Add(hider.name);
            }

            Assert.That(names, Is.SupersetOf(new[] { "HUD", "MobileControls", "DetectorIndicator" }), "gold panel + level plate + DIG / COLLECT, joystick and detector all hide");

            void AssertHud(bool hidden, string state)
            {
                foreach (HideWhilePaused hider in hiders)
                {
                    CanvasGroup group = hider.GetComponent<CanvasGroup>();
                    Assert.That(hider.IsHidden, Is.EqualTo(hidden), $"{hider.name} {state}");
                    Assert.That(group.alpha, Is.EqualTo(hidden ? 0f : 1f), $"{hider.name} alpha {state}");
                    Assert.That(group.blocksRaycasts, Is.EqualTo(!hidden), $"{hider.name} raycasts {state}");
                }
            }

            AssertHud(false, "while playing");

            pause.Pause();
            yield return null;
            AssertHud(true, "on the pause menu");

            pause.OpenSettings();
            yield return null;
            Assert.That(pause.IsSettingsOpen, Is.True);
            AssertHud(true, "behind the settings panel opened from pause (this is what used to show through)");

            pause.Resume();
            yield return null;
            AssertHud(false, "after RESUME");
            Assert.That(Find<Button>("PauseButton").gameObject.activeSelf, Is.True, "the PAUSE button is back too");
        }
    }
}
#endif
