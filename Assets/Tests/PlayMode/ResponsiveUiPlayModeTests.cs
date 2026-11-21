#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Final UI polish (stage 10), mobile fit: on every target phone screen (16:9 up to about 2.6:1) every button of the Main Menu, Character
    /// Selection and Pause menu is completely on screen (shadow included), the title lockup is whole, the canvas never shrinks below the 1080 unit
    /// reference height (the cause of the cut-off EXIT button), and with a simulated camera cut-out and gesture bar nothing tappable sits in the
    /// cut-out or in the system gesture zones. The Settings panel is covered by <see cref="CharacterAndUiPlayModeTests"/>, the HUD by
    /// <see cref="HudRedesignPlayModeTests"/>.
    /// </summary>
    public sealed class ResponsiveUiPlayModeTests
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayPath = "Assets/Scenes/GameplayTest.unity";

        /// <summary>48 dp on a typical Android phone (1080 px high, 2.625 px per dp) is 126 reference units: this is the real minimum, no tolerance.</summary>
        private const float MinTouchUnits = 126f;

        /// <summary>Everything stays at least this far (reference units) inside the screen edge.</summary>
        private const float EdgeMargin = 24f;

        private static readonly (string name, int width, int height)[] TargetScreens =
        {
            ("16:9", 1920, 1080), ("18:9", 2160, 1080), ("19.5:9", 2340, 1080), ("20:9", 2400, 1080), ("21:9", 2520, 1080), ("2.6:1", 2808, 1080),
        };

        private Scene loadedScene;
        private TestGameView gameView;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore(); // never touch the real PlayerPrefs
            CharacterSelection.ResetCurrent();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SafeAreaFitter.SafeAreaOverride = null;
            GamePause.Resume();
            SceneNavigator.LoadHook = null;
            if (gameView != null)
            {
                yield return gameView.Restore(); // also when an assertion failed
                gameView = null;
            }

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
        }

        private IEnumerator LoadScene(string path)
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null, path);
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.8f); // the menu pieces slide in: measure them at rest
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

        /// <summary>The rect in root-canvas units (the 1080 unit high reference space).</summary>
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

        /// <summary>The rect in screen pixels.</summary>
        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Contains(Rect outer, Rect inner, float margin = 0f)
        {
            return inner.xMin >= outer.xMin + margin && inner.xMax <= outer.xMax - margin && inner.yMin >= outer.yMin + margin && inner.yMax <= outer.yMax - margin;
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax - 0.5f && a.xMax > b.xMin + 0.5f && a.yMin < b.yMax - 0.5f && a.yMax > b.yMin + 0.5f;
        }

        /// <summary>Sets every target resolution in turn and runs <paramref name="check"/> on it; a failure names the screen.</summary>
        private IEnumerator OnEveryTargetScreen(Action check)
        {
            gameView ??= new TestGameView(); // restored by TearDown
            foreach ((string name, int width, int height) screen in TargetScreens)
            {
                gameView.SetResolution(screen.width, screen.height);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(screen.width, screen.height)),
                    $"the Game View did not switch to {screen.name}; the check would measure the wrong screen");
                try
                {
                    check();
                }
                catch (AssertionException e)
                {
                    throw new AssertionException($"On the {screen.name} screen ({screen.width}x{screen.height}): {e.Message}", e);
                }
            }
        }

        /// <summary>A button counts as fully visible only with its soft shadow too: that is what was cut off at the bottom of the screen.</summary>
        private static void AssertFullyOnScreen(string name, Transform button, Rect canvas)
        {
            Rect face = CanvasRect((RectTransform)button);
            Assert.That(Contains(canvas, face, EdgeMargin), Is.True, $"{name} is completely on screen with a margin ({face} in {canvas})");
            Transform shadow = button.Find("Shadow");
            if (shadow != null)
            {
                Assert.That(Contains(canvas, CanvasRect((RectTransform)shadow)), Is.True, $"{name}: even its shadow is on screen");
            }
        }

        // ------------------------------------------------------------------ canvas scaling

        [UnityTest]
        public IEnumerator Canvases_ExpandToTheReferenceLayout_SoTheHeightIsNeverCutOnWidePhones()
        {
            yield return LoadScene(MainMenuPath);
            CanvasScaler menuScaler = Find<CanvasScaler>("UI Canvas");
            Assert.That(menuScaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand), "Expand: the whole 1920 x 1080 layout always fits");
            Assert.That(menuScaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

            yield return OnEveryTargetScreen(() =>
            {
                Rect canvas = CanvasRect((RectTransform)menuScaler.transform);
                Assert.That(canvas.height, Is.EqualTo(1080f).Within(0.5f),
                    "the canvas is 1080 units high on every screen 16:9 or wider (with the old match 0.5 it shrank to about 890 at 2.6:1, below the layout's bottom edge)");
                Assert.That(canvas.width, Is.GreaterThanOrEqualTo(1919f), "a wider phone only adds width");
            });
        }

        [UnityTest]
        public IEnumerator GameplayCanvas_UsesTheSameScaling_AndTheGestureSafeMargin()
        {
            yield return LoadScene(GameplayPath);
            CanvasScaler scaler = Find<CanvasScaler>("UI Canvas");
            Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
            Assert.That(Find<SafeAreaFitter>("SafeAreaRoot").GestureMarginFraction, Is.GreaterThan(0.04f), "the HUD keeps clear of the Android gesture zones");
        }

        // ------------------------------------------------------------------ Main Menu

        [UnityTest]
        public IEnumerator MainMenu_EveryButtonIsCompletelyVisible_AndTheLogoIsWhole_OnEveryTargetScreen()
        {
            yield return LoadScene(MainMenuPath);
            string[] buttons = { "PlayButton", "CharacterButton", "SettingsButton", "ExitButton" };

            yield return OnEveryTargetScreen(() =>
            {
                Rect canvas = CanvasRect((RectTransform)Find<Canvas>("UI Canvas").transform);
                var rects = new List<Rect>();
                foreach (string name in buttons)
                {
                    Transform button = Find<Button>(name).transform;
                    AssertFullyOnScreen(name, button, canvas);
                    Rect rect = CanvasRect((RectTransform)button);
                    Assert.That(rect.height, Is.GreaterThanOrEqualTo(MinTouchUnits), $"{name} is at least 48 dp high");
                    Assert.That(rect.width, Is.GreaterThanOrEqualTo(MinTouchUnits));
                    rects.Add(rect);
                }

                // EXIT keeps a real margin above the bottom edge (it is anchored to the bottom of the safe area, not hung from the top)
                Assert.That(rects[3].yMin - canvas.yMin, Is.GreaterThanOrEqualTo(EdgeMargin * 2f), "EXIT keeps a margin above the bottom edge");

                // one tidy column: equal gaps, one left edge, PLAY first and biggest
                float gap1 = rects[0].yMin - rects[1].yMax;
                float gap2 = rects[1].yMin - rects[2].yMax;
                float gap3 = rects[2].yMin - rects[3].yMax;
                Assert.That(gap3, Is.EqualTo(gap2).Within(0.5f), "equal spacing between the secondary buttons");
                Assert.That(gap1, Is.EqualTo(gap2).Within(6f), "and the same gap below PLAY (PLAY breathes: its idle pulse scales it by up to 2.5 %)");
                for (int i = 1; i < rects.Count; i++)
                {
                    Assert.That(rects[i].xMin, Is.EqualTo(rects[0].xMin).Within(10f), buttons[i] + " shares the left edge of PLAY");
                    Assert.That(Overlaps(rects[i - 1], rects[i]), Is.False, buttons[i] + " does not overlap the button above");
                }

                // the logo: title and 3D badge are one lockup inside the screen, the badge never covers a letter, and it clears the buttons
                TMP_Text title = Find<TMP_Text>("Title");
                Transform badge = Find<Transform>("TitleBadge");
                Rect titleRect = CanvasRect(title.rectTransform);
                Rect badgeRect = CanvasRect((RectTransform)badge);
                float textRight = titleRect.center.x + title.preferredWidth * 0.5f;
                Assert.That(badgeRect.xMin, Is.GreaterThanOrEqualTo(textRight - 2f), "the 3D badge sits after the last letter");
                Assert.That(Contains(canvas, badgeRect, EdgeMargin), Is.True, "the badge is on screen");
                Assert.That(Contains(canvas, titleRect, 0f), Is.True, "the title is on screen");
                Assert.That(Mathf.Abs((titleRect.xMin + badgeRect.xMax) * 0.5f - canvas.center.x), Is.LessThan(40f), "title + badge are centred as one lockup");
                Assert.That(Overlaps(titleRect, rects[0]), Is.False, "the logo clears the PLAY button");

                // the icons are the same visible size in every secondary button and share one axis
                float axis = CanvasRect((RectTransform)Find<Button>("CharacterButton").transform.Find("Icon")).center.x;
                foreach (string name in new[] { "SettingsButton", "ExitButton" })
                {
                    float other = CanvasRect((RectTransform)Find<Button>(name).transform.Find("Icon")).center.x;
                    Assert.That(other, Is.EqualTo(axis).Within(6f), name + " icon sits on the same axis as the others (the helmet is a wide image, its centre may differ a little)");
                }
            });
        }

        /// <summary>Screens with a notch / home indicator: the safe area is smaller than the screen, so the menu has LESS height than on a plain 16:9 screen.</summary>
        private static readonly (string name, int width, int height, Rect? safeArea)[] PhoneSafeAreaScreens =
        {
            ("16:9, no inset", 1920, 1080, null),
            ("19.5:9 punch-hole phone (landscape side inset)", 2340, 1080, new Rect(96f, 0f, 2148f, 1080f)),
            ("21:9, no inset", 2520, 1080, null),
            ("iPhone 14 Pro landscape (2532x1170, home indicator 63 px)", 2532, 1170, new Rect(141f, 63f, 2250f, 1107f)),
            ("iPhone 15 Pro Max landscape (2796x1290, 59 pt sides, 21 pt bottom)", 2796, 1290, new Rect(177f, 63f, 2442f, 1227f)),
        };

        /// <summary>The logo keeps at least this much clear space above PLAY (the same 24 units every other edge keeps).</summary>
        private const float MinLogoGap = 24f;

        [UnityTest]
        public IEnumerator MainMenu_LogoClearsPlay_AndTheColumnStaysTidy_OnEveryScreenAlsoWithPhoneSafeAreas()
        {
            yield return LoadScene(MainMenuPath);
            gameView ??= new TestGameView(); // restored by TearDown
            string[] names = { "PlayButton", "CharacterButton", "SettingsButton", "ExitButton" };
            foreach ((string name, int width, int height, Rect? safeArea) screen in PhoneSafeAreaScreens)
            {
                gameView.SetResolution(screen.width, screen.height);
                yield return null;
                yield return null;
                SafeAreaFitter.SafeAreaOverride = () => screen.safeArea ?? new Rect(0f, 0f, Screen.width, Screen.height);
                yield return null;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(screen.width, screen.height)), $"the Game View did not switch to {screen.name}");

                try
                {
                    Rect canvas = CanvasRect((RectTransform)Find<Canvas>("UI Canvas").transform);
                    var rects = new List<Rect>();
                    foreach (string button in names)
                    {
                        Rect rect = CanvasRect((RectTransform)Find<Button>(button).transform);
                        Assert.That(rect.height, Is.GreaterThanOrEqualTo(MinTouchUnits), button + " keeps its 48 dp touch size (only the spaces are elastic)");
                        rects.Add(rect);
                    }

                    // the logo (title text and the 3D badge) clears PLAY with a real gap: this overlapped by ~3 units with a 63 px home indicator
                    Rect title = CanvasRect(Find<TMP_Text>("Title").rectTransform);
                    float gap = title.yMin - rects[0].yMax;
                    Debug.Log($"MainMenu layout on {screen.name}: logo-to-PLAY gap {gap:F1} units, button gaps {rects[0].yMin - rects[1].yMax:F1} / {rects[1].yMin - rects[2].yMax:F1} / {rects[2].yMin - rects[3].yMax:F1}, EXIT bottom {rects[3].yMin - canvas.yMin:F1} above the canvas edge.");
                    Assert.That(gap, Is.GreaterThanOrEqualTo(MinLogoGap), $"the logo clears PLAY by {gap:F1} units (needs {MinLogoGap})");
                    Assert.That(Overlaps(CanvasRect((RectTransform)Find<Transform>("TitleBadge")), rects[0]), Is.False, "the badge clears PLAY");

                    // equal, positive gaps in one column, nothing overlapping
                    float gap1 = rects[0].yMin - rects[1].yMax, gap2 = rects[1].yMin - rects[2].yMax, gap3 = rects[2].yMin - rects[3].yMax;
                    Assert.That(Mathf.Min(gap1, Mathf.Min(gap2, gap3)), Is.GreaterThanOrEqualTo(12f), $"the buttons keep a visible gap ({gap1:F1} / {gap2:F1} / {gap3:F1})");
                    Assert.That(gap3, Is.EqualTo(gap2).Within(0.5f), "equal spacing between the secondary buttons");
                    Assert.That(gap1, Is.EqualTo(gap2).Within(6f), "and the same gap below PLAY (PLAY breathes by up to 2.5 %)");
                    for (int i = 1; i < rects.Count; i++)
                    {
                        Assert.That(Overlaps(rects[i - 1], rects[i]), Is.False, names[i] + " does not overlap the button above");
                    }

                    // everything stays inside the safe area minus the gesture margin, and the logo inside the screen
                    SafeAreaFitter fitter = Find<SafeAreaFitter>("SafeAreaRoot");
                    Rect allowed = SafeAreaFitter.ShrinkForGestures(SafeAreaFitter.SafeAreaOverride(), fitter.GestureMarginFraction * Screen.height);
                    foreach (string button in names)
                    {
                        Rect px = ScreenRect((RectTransform)Find<Button>(button).transform);
                        Assert.That(Contains(allowed, px, -1f), Is.True, $"{button} ({px}) stays inside the safe area and gesture margin ({allowed})");
                    }

                    Assert.That(Contains(canvas, title, 0f), Is.True, "the title is on screen");
                    Rect badge = ScreenRect((RectTransform)Find<Transform>("TitleBadge"));
                    Assert.That(badge.yMax, Is.LessThanOrEqualTo(allowed.yMax + 1f), "the logo badge is not cut at the top");
                }
                catch (AssertionException e)
                {
                    throw new AssertionException($"On {screen.name}: {e.Message}", e);
                }
            }

            SafeAreaFitter.SafeAreaOverride = null;
        }

        // ------------------------------------------------------------------ Character Selection

        [UnityTest]
        public IEnumerator CharacterSelection_IsAlignedAndCompletelyVisible_OnEveryTargetScreen()
        {
            yield return LoadScene(MainMenuPath);
            CharacterSelectController screen = Find<CharacterSelectController>("CharacterPanel");
            Find<Button>("CharacterButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.7f);

            yield return OnEveryTargetScreen(() =>
            {
                Rect canvas = CanvasRect((RectTransform)Find<Canvas>("UI Canvas").transform);
                Transform root = screen.transform;
                Rect plate = CanvasRect((RectTransform)root.Find("NamePlate"));
                Rect select = CanvasRect((RectTransform)root.Find("SelectButton"));
                foreach (string name in new[] { "SelectButton", "CharacterBackButton", "LeftArrow", "RightArrow" })
                {
                    Transform button = root.Find(name);
                    AssertFullyOnScreen(name, button, canvas);
                    Rect rect = CanvasRect((RectTransform)button);
                    Assert.That(rect.width, Is.GreaterThanOrEqualTo(MinTouchUnits), name + " is at least 48 dp wide");
                    Assert.That(rect.height, Is.GreaterThanOrEqualTo(MinTouchUnits), name + " is at least 48 dp high");
                }

                Assert.That(Contains(canvas, plate, EdgeMargin), Is.True, "the name plate is on screen");
                Assert.That(Contains(canvas, CanvasRect((RectTransform)root.Find("TitleBanner")), 0f), Is.True, "the banner is on screen");

                // name, description and the select button share ONE centre axis (the middle of the screen) and one width
                Assert.That(plate.center.x, Is.EqualTo(canvas.center.x).Within(1f));
                Assert.That(select.center.x, Is.EqualTo(plate.center.x).Within(0.5f), "name plate and SELECT share one axis");
                Assert.That(select.width, Is.EqualTo(plate.width).Within(0.5f));
                Assert.That(CanvasRect(Find<TMP_Text>("Name").rectTransform).center.x, Is.EqualTo(plate.center.x).Within(0.5f));
                Assert.That(CanvasRect(Find<TMP_Text>("Tagline").rectTransform).center.x, Is.EqualTo(plate.center.x).Within(0.5f));
                Assert.That(Overlaps(plate, select), Is.False);

                // BACK (labelled, top left) and the round arrows (left / right of the miner) cannot be confused
                Rect back = CanvasRect((RectTransform)root.Find("CharacterBackButton"));
                Rect left = CanvasRect((RectTransform)root.Find("LeftArrow"));
                Rect right = CanvasRect((RectTransform)root.Find("RightArrow"));
                Assert.That(back.center.x, Is.LessThan(canvas.center.x));
                Assert.That(back.center.y, Is.GreaterThan(canvas.center.y + 200f), "BACK is in the top left corner");
                Assert.That(left.center.x, Is.LessThan(canvas.center.x));
                Assert.That(right.center.x, Is.GreaterThan(canvas.center.x));
                Assert.That(left.center.y, Is.EqualTo(right.center.y).Within(0.5f), "the arrows share a line");
                Assert.That(root.Find("CharacterBackButton").Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("BACK"));
                Assert.That(root.Find("LeftArrow").Find("Label"), Is.Null, "the arrows are round icon buttons without a word");
            });
        }

        [UnityTest]
        public IEnumerator CharacterSelection_SelectedMinerShowsSELECTED_TheOtherSELECT_AndNoEquippedTag()
        {
            yield return LoadScene(MainMenuPath);
            Find<Button>("CharacterButton").onClick.Invoke();
            yield return null;
            TMP_Text label = Find<Button>("SelectButton").transform.Find("Label").GetComponent<TMP_Text>();
            GameObject mark = Find<Button>("SelectButton").transform.Find("Icon").gameObject;
            Assert.That(label.text, Is.EqualTo("SELECTED"), "the saved miner (Barnaby by default)");
            Assert.That(mark.activeSelf, Is.True, "with a check mark");

            Find<Button>("RightArrow").onClick.Invoke();
            Assert.That(label.text, Is.EqualTo("SELECT"), "the other miner");
            Assert.That(mark.activeSelf, Is.False);

            Find<Button>("SelectButton").onClick.Invoke();
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "saving still works");
            Find<Button>("CharacterButton").onClick.Invoke();
            Assert.That(label.text, Is.EqualTo("SELECTED"), "and now Ruby is the selected one");

            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                Assert.That(t.name, Is.Not.EqualTo("EquippedBadge"), "the duplicate EQUIPPED tag is gone");
            }
        }

        // ------------------------------------------------------------------ Pause

        [UnityTest]
        public IEnumerator PauseMenu_IsCompact_AndCompletelyVisible_OnEveryTargetScreen()
        {
            yield return LoadScene(GameplayPath);
            PauseMenuController pause = UnityEngine.Object.FindAnyObjectByType<PauseMenuController>();
            pause.Pause();
            yield return new WaitForSecondsRealtime(0.5f);

            yield return OnEveryTargetScreen(() =>
            {
                Rect canvas = CanvasRect((RectTransform)Find<Canvas>("UI Canvas").transform);
                Transform window = Find<Transform>("PausePanel").Find("Window");
                Rect windowRect = CanvasRect((RectTransform)window);
                Assert.That(windowRect.width, Is.LessThanOrEqualTo(620f), "compact: 600 wide (was 720)");
                Assert.That(windowRect.height, Is.LessThanOrEqualTo(580f), "compact: 560 high (was 640)");
                Assert.That(Contains(canvas, windowRect, EdgeMargin), Is.True, "the pause window is on screen");
                Assert.That(Contains(canvas, CanvasRect((RectTransform)window.Find("TitleBanner")), 0f), Is.True, "the PAUSED banner is on screen");
                foreach (string name in new[] { "ResumeButton", "SettingsButton", "MainMenuButton" })
                {
                    AssertFullyOnScreen(name, window.Find(name), canvas);
                    Rect rect = CanvasRect((RectTransform)window.Find(name));
                    Assert.That(rect.height, Is.GreaterThanOrEqualTo(MinTouchUnits), name + " keeps a 48 dp touch size");
                    Assert.That(Contains(windowRect, rect, 8f), Is.True, name + " is inside the window");
                }
            });
        }

        // ------------------------------------------------------------------ cut-out and gesture zones

        [UnityTest]
        public IEnumerator Menus_KeepClearOfACutOutAndTheGestureZones()
        {
            // a landscape phone with a camera hole / notch on the left (and the same on the right), a bar at the bottom: 13 % / 6 % of the height
            SafeAreaFitter.SafeAreaOverride = () => new Rect(Screen.height * 0.13f, Screen.height * 0.06f, Screen.width - Screen.height * 0.26f, Screen.height * 0.94f);
            yield return LoadScene(MainMenuPath);
            SafeAreaFitter fitter = Find<SafeAreaFitter>("SafeAreaRoot");
            Assert.That(fitter.GestureMarginFraction, Is.GreaterThan(0.04f), "the menu keeps clear of the Android gesture zones");

            yield return OnEveryTargetScreen(() =>
            {
                Rect cutOut = SafeAreaFitter.SafeAreaOverride();
                Rect allowed = SafeAreaFitter.ShrinkForGestures(cutOut, fitter.GestureMarginFraction * Screen.height);
                Rect safeRoot = ScreenRect((RectTransform)fitter.transform);
                Assert.That(Contains(allowed, safeRoot, -1f), Is.True, $"the safe-area root ({safeRoot}) is inside the cut-out + gesture margin ({allowed})");
                Assert.That(allowed.xMin, Is.GreaterThan(cutOut.xMin + Screen.height * 0.04f), "a real gesture margin on the left");
                Assert.That(allowed.yMin, Is.GreaterThan(cutOut.yMin + Screen.height * 0.04f), "a real gesture margin at the bottom");

                foreach (string name in new[] { "PlayButton", "CharacterButton", "SettingsButton", "ExitButton" })
                {
                    Rect button = ScreenRect((RectTransform)Find<Button>(name).transform);
                    Assert.That(Contains(allowed, button, -1f), Is.True, $"{name} ({button}) stays inside the cut-out and gesture margins ({allowed})");
                }

                Rect title = ScreenRect((RectTransform)Find<Transform>("TitleBadge"));
                Assert.That(Contains(allowed, title, -1f), Is.True, "the logo badge stays inside too");
            });

            // Character Selection and the Pause menu live in the same safe-area root: the same guarantee
            Find<Button>("CharacterButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.7f);
            yield return OnEveryTargetScreen(() =>
            {
                Rect allowed = SafeAreaFitter.ShrinkForGestures(SafeAreaFitter.SafeAreaOverride(), fitter.GestureMarginFraction * Screen.height);
                foreach (string name in new[] { "SelectButton", "CharacterBackButton", "LeftArrow", "RightArrow", "NamePlate" })
                {
                    Rect rect = ScreenRect((RectTransform)Find<Transform>(name));
                    Assert.That(Contains(allowed, rect, -1f), Is.True, $"{name} ({rect}) stays inside the cut-out and gesture margins ({allowed})");
                }
            });
        }

        [UnityTest]
        public IEnumerator Hud_KeepsClearOfACutOutAndTheGestureZones()
        {
            SafeAreaFitter.SafeAreaOverride = () => new Rect(Screen.height * 0.13f, Screen.height * 0.06f, Screen.width - Screen.height * 0.26f, Screen.height * 0.94f);
            yield return LoadScene(GameplayPath);
            SafeAreaFitter fitter = Find<SafeAreaFitter>("SafeAreaRoot");

            yield return OnEveryTargetScreen(() =>
            {
                Rect allowed = SafeAreaFitter.ShrinkForGestures(SafeAreaFitter.SafeAreaOverride(), fitter.GestureMarginFraction * Screen.height);
                foreach (string name in new[] { "PauseButton", "LevelPlate", "HeaderPanel", "DetectorIndicator", "MovementJoystick", "DigButton", "CollectButton" })
                {
                    Rect rect = ScreenRect((RectTransform)Find<Transform>(name));
                    Assert.That(Contains(allowed, rect, -1f), Is.True, $"{name} ({rect}) stays inside the cut-out and gesture margins ({allowed})");
                }
            });
        }
    }
}
#endif
