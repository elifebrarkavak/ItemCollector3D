#if UNITY_EDITOR
using System.Collections;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// iPhone 15 Pro Max in landscape: 2796 x 1290 pixels (430 x 932 points at 3x). Apple's landscape safe area keeps 59 pt (177 px) free on the left and
    /// right (the Dynamic Island side and its mirror) and 21 pt (63 px) at the bottom for the home indicator, nothing at the top. Every tappable piece of the
    /// Main Menu, Character Selection, Area Selection and the gameplay HUD must stay inside that area (plus the gesture margin the screens already keep).
    /// </summary>
    public sealed class IphoneSafeAreaPlayModeTests
    {
        private const int Width = 2796;
        private const int Height = 1290;
        private const float SideInset = 59f * 3f;
        private const float BottomInset = 21f * 3f;
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayPath = "Assets/Scenes/GameplayTest.unity";

        private Scene loadedScene;
        private TestGameView gameView;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore(); // never touch the real PlayerPrefs
            CharacterSelection.ResetCurrent();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(SideInset, BottomInset, Screen.width - 2f * SideInset, Screen.height - BottomInset);
            gameView = new TestGameView();
            gameView.SetResolution(Width, Height);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SafeAreaFitter.SafeAreaOverride = null;
            GamePause.Resume();
            SceneNavigator.LoadHook = null;
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            if (gameView != null)
            {
                yield return gameView.Restore(); // also when an assertion failed
                gameView = null;
            }

            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
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
            yield return new WaitForSecondsRealtime(1.0f); // entrance animations finish
            Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(Width, Height)), "the Game View is the iPhone 15 Pro Max landscape size");
            Canvas.ForceUpdateCanvases();
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

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - 1f && inner.xMax <= outer.xMax + 1f && inner.yMin >= outer.yMin - 1f && inner.yMax <= outer.yMax + 1f;
        }

        private Rect AllowedArea()
        {
            SafeAreaFitter fitter = Find<SafeAreaFitter>("SafeAreaRoot");
            return SafeAreaFitter.ShrinkForGestures(SafeAreaFitter.SafeAreaOverride(), fitter.GestureMarginFraction * Screen.height);
        }

        private void AssertInside(Rect allowed, params string[] names)
        {
            foreach (string name in names)
            {
                Rect rect = ScreenRect((RectTransform)Find<Transform>(name));
                Assert.That(Contains(allowed, rect), Is.True, $"{name} ({rect}) must stay inside the iPhone safe area and gesture margin ({allowed})");
            }
        }

        [UnityTest]
        public IEnumerator MainMenu_And_CharacterSelection_StayInsideTheDynamicIslandAndHomeIndicatorAreas()
        {
            yield return LoadScene(MainMenuPath);
            Rect allowed = AllowedArea();
            Assert.That(allowed.xMin, Is.GreaterThanOrEqualTo(SideInset), "the safe root keeps clear of the Dynamic Island side");
            Assert.That(allowed.yMin, Is.GreaterThanOrEqualTo(BottomInset), "and of the home indicator");
            AssertInside(allowed, "PlayButton", "CharacterButton", "SettingsButton", "ExitButton", "TitleBadge");

            Find<Button>("CharacterButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.7f);
            AssertInside(allowed, "SelectButton", "CharacterBackButton", "LeftArrow", "RightArrow", "NamePlate");
        }

        [UnityTest]
        public IEnumerator AreaSelection_StaysInsideTheSafeArea_WithFullSizeTouchTargets()
        {
            yield return LoadScene(MainMenuPath);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.0f);
            Rect allowed = AllowedArea();
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            Assert.That(ScreenRect(screen.BackButton.GetComponent<RectTransform>()).xMin, Is.GreaterThanOrEqualTo(allowed.xMin - 1f), "BACK clears the Dynamic Island side");
            for (int i = 0; i < screen.CardCount; i++)
            {
                AreaCardView card = screen.GetCard(i);
                Rect frame = ScreenRect((RectTransform)card.transform.Find("Frame"));
                Assert.That(Contains(allowed, frame), Is.True, $"{card.AreaId} card ({frame}) stays inside the safe area ({allowed})");
            }

            // 48 dp = 126 reference units (a 1080 px high phone at 2.625 px per dp); this phone is 1290 px high at 3 px per dp, so 48 dp is 144 px = 120.9 units
            float pixelsPerUnit = Screen.height / 1080f;
            float minimumPixels = 48f * 3f; // 48 dp at 3x
            foreach (string name in new[] { "AreaBackButton" })
            {
                Assert.That(ScreenRect((RectTransform)Find<Transform>(name)).height, Is.GreaterThanOrEqualTo(minimumPixels), name + " is at least 48 dp high on this phone");
            }

            for (int i = 0; i < screen.CardCount; i++)
            {
                AreaCardView card = screen.GetCard(i);
                foreach (Button button in new[] { card.PlayButton, card.ComingSoonButton, card.LockedButton })
                {
                    if (button.gameObject.activeSelf)
                    {
                        Assert.That(ScreenRect((RectTransform)button.transform).height, Is.GreaterThanOrEqualTo(minimumPixels), $"{card.AreaId}: the button is at least 48 dp high ({pixelsPerUnit:F2} px per unit)");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator GameplayHud_StaysInsideTheSafeArea()
        {
            yield return LoadScene(GameplayPath);
            Rect allowed = AllowedArea();
            AssertInside(allowed, "PauseButton", "LevelPlate", "HeaderPanel", "DetectorIndicator", "MovementJoystick", "DigButton", "CollectButton");
        }
    }
}
#endif
