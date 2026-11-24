#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using ItemCollector3D.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// The NEW GAME introduction (stage 17A): CANCEL changes nothing and shows nothing; START NEW GAME resets the save and then opens a three-page introduction (WELCOME, FIND HIDDEN TREASURES, UNLOCK NEW WORLDS)
    /// with SKIP / NEXT, BACK / SKIP / NEXT and BACK / LET'S PLAY; SKIP and LET'S PLAY return to the Main Menu; the introduction stays inside the iPhone safe area, takes every touch (nothing behind it can be
    /// pressed), never appears on a normal start and never touches the save, the settings or the miner. In-memory stores only; the developer's real save file is hashed before and after.
    /// </summary>
    public sealed class IntroSlidesPlayModeTests
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string PlayedSave = "{\"version\":1,\"totalGold\":500,\"collectedTreasureIds\":[\"green_mine.gold_coin_01\",\"green_mine.meadow_01\"],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"completedAreaIds\":[]}";

        private static string realSavePath, realSaveHashBefore;
        private Scene loadedScene;
        private MemoryProgressStore store;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode previousBehaviour;

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
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(0f, 0f, Screen.width, Screen.height);
            previousBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>("IC3D_TestMouseIntro");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null)
            {
                InputSystem.RemoveDevice(mouse);
                mouse = null;
            }

            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehaviour;
            SafeAreaFitter.SafeAreaOverride = null;
            GamePause.Resume();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator LoadMenu()
        {
            store = new MemoryProgressStore(PlayedSave);
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(MainMenuPath);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.8f);
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

            Assert.Fail($"{typeof(T).Name} '{name}' not found");
            return null;
        }

        private IEnumerator StartNewGame()
        {
            Find<Button>("SettingsButton").onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
            var panel = Find<SettingsPanelController>("SettingsPanel");
            panel.NewGameButton.onClick.Invoke();
            yield return null;
            panel.ConfirmNewGameButton.onClick.Invoke();
            yield return null;
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Vector2 Centre(Button button) => ScreenRect((RectTransform)button.transform).center;

        private IEnumerator Press(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;
        }

        private static string VisibleButtons(IntroSlidesController intro)
        {
            string s = string.Empty;
            s += intro.BackButton.gameObject.activeSelf ? "BACK " : string.Empty;
            s += intro.SkipButton.gameObject.activeSelf ? "SKIP " : string.Empty;
            s += intro.NextButton.gameObject.activeSelf ? "NEXT " : string.Empty;
            s += intro.PlayButton.gameObject.activeSelf ? "PLAY " : string.Empty;
            return s.Trim();
        }

        private void AssertBackInTheMainMenu(IntroSlidesController intro, SettingsPanelController settings)
        {
            Assert.That(intro.IsOpen, Is.False, "the introduction is gone");
            Assert.That(settings.IsOpen, Is.False, "and so is the Settings panel with its note");
            Assert.That(Find<MainMenuController>("MainMenuUI").IsIntroOpen, Is.False);
            Assert.That(Find<Button>("PlayButton").gameObject.activeInHierarchy, Is.True, "the Main Menu is showing again");
        }

        private CharacterSelectController Characters()
        {
            foreach (CharacterSelectController c in Resources.FindObjectsOfTypeAll<CharacterSelectController>())
            {
                if (c.gameObject.scene == loadedScene)
                {
                    return c;
                }
            }

            Assert.Fail("CharacterSelectController not found");
            return null;
        }

        /// <summary>SKIP and LET'S PLAY: the introduction and the Settings panel are closed and the existing Character Selection is open.</summary>
        private void AssertInCharacterSelection(IntroSlidesController intro, SettingsPanelController settings)
        {
            Assert.That(intro.IsOpen, Is.False, "the introduction is gone");
            Assert.That(settings.IsOpen, Is.False, "and so is the Settings panel with its note");
            Assert.That(Characters().IsOpen, Is.True, "the existing Character Selection is open");
            Assert.That(Find<MainMenuController>("MainMenuUI").IsCharacterSelectOpen, Is.True);
        }

        private static void AssertFresh(string json)
        {
            var data = JsonUtility.FromJson<GameProgressData>(json);
            Assert.That(data.totalGold, Is.EqualTo(0));
            Assert.That(data.collectedTreasureIds, Is.Empty);
            Assert.That(data.unlockedAreaIds, Is.EqualTo(new System.Collections.Generic.List<string> { "green_mine" }));
        }

        // ================================================================ tests

        [UnityTest]
        public IEnumerator NormalStart_ShowsNoIntroduction_AndCancelChangesNothing()
        {
            yield return LoadMenu();
            var intro = Find<IntroSlidesController>("IntroPanel");
            Assert.That(intro.IsOpen, Is.False, "a normal start with an existing save shows no introduction");
            Assert.That(intro.PagesBuilt, Is.EqualTo(3));
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(intro.IsOpen, Is.False, "nor does PLAY");
            Find<AreaSelectController>("AreaPanel").Cancel();
            yield return null;

            Find<Button>("SettingsButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            var panel = Find<SettingsPanelController>("SettingsPanel");
            string before = store.Json;
            int writes = store.WriteCount;
            panel.NewGameButton.onClick.Invoke();
            yield return null;
            Assert.That(panel.DialogState, Is.EqualTo(SettingsPanelController.NewGameState.Confirm));
            panel.CancelNewGameButton.onClick.Invoke();
            yield return null;
            Assert.That(panel.IsNewGameDialogOpen, Is.False);
            Assert.That(intro.IsOpen, Is.False, "CANCEL shows no introduction");
            Assert.That(store.Json, Is.EqualTo(before), "and changes nothing");
            Assert.That(store.WriteCount, Is.EqualTo(writes));
        }

        [UnityTest]
        public IEnumerator StartNewGame_ResetsTheSave_ThenOpensTheIntroduction_OnTopOfEverything_AndKeepsMinerAndSettings()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return LoadMenu();
            GameSettings.Current.SetMusicVolume(0.3f);
            GameSettings.Current.SetSfxVolume(0.6f);
            yield return StartNewGame();
            var intro = Find<IntroSlidesController>("IntroPanel");
            var settings = Find<SettingsPanelController>("SettingsPanel");
            AssertFresh(store.Json);
            Assert.That(intro.IsOpen, Is.True, "the introduction opens right after the reset");
            Assert.That(intro.PageIndex, Is.EqualTo(0));
            Assert.That(intro.transform.GetSiblingIndex(), Is.EqualTo(intro.transform.parent.childCount - 1), "drawn above every other panel of the menu");
            Assert.That(Find<MainMenuController>("MainMenuUI").IsIntroOpen, Is.True);
            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "Ruby is still the chosen miner");
            int writes = store.WriteCount;

            intro.Skip();
            yield return null;
            AssertInCharacterSelection(intro, settings);
            Assert.That(store.WriteCount, Is.EqualTo(writes), "SKIP writes nothing: the reset happened once, in the dialog");
            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(Characters().ViewingId, Is.EqualTo(CharacterSelection.FemaleId), "the Character Selection starts on the chosen miner");
            Characters().Cancel();
            yield return null;
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "leaving it with BACK keeps Ruby");
            Assert.That(Find<Button>("PlayButton").gameObject.activeInHierarchy, Is.True, "back in the Main Menu");
            Assert.That(store.WriteCount, Is.EqualTo(writes));

            // and the new game is what the Area Selection shows
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(Find<AreaSelectController>("AreaPanel").IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator ThePages_AreInOrder_TheButtonsMatchEachPage_AndBackNextSkipPlayWork()
        {
            yield return LoadMenu();
            yield return StartNewGame();
            var intro = Find<IntroSlidesController>("IntroPanel");
            var settings = Find<SettingsPanelController>("SettingsPanel");
            Assert.That(IntroSlidesController.Titles, Is.EqualTo(new[] { "WELCOME TO THE ADVENTURE", "FIND HIDDEN TREASURES", "UNLOCK NEW WORLDS" }));

            Assert.That(VisibleButtons(intro), Is.EqualTo("SKIP NEXT"), "page 1");
            intro.BackButton.onClick.Invoke(); // not visible, and a call on page 1 stays on page 1
            intro.Back();
            Assert.That(intro.PageIndex, Is.EqualTo(0));
            intro.NextButton.onClick.Invoke();
            Assert.That(intro.PageIndex, Is.EqualTo(1));
            Assert.That(VisibleButtons(intro), Is.EqualTo("BACK SKIP NEXT"), "page 2");
            intro.NextButton.onClick.Invoke();
            Assert.That(intro.PageIndex, Is.EqualTo(2));
            Assert.That(VisibleButtons(intro), Is.EqualTo("BACK PLAY"), "page 3");
            intro.Next();
            Assert.That(intro.PageIndex, Is.EqualTo(2), "there is no fourth page");
            Assert.That(intro.PlayButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("LET'S PLAY"));
            intro.BackButton.onClick.Invoke();
            Assert.That(intro.PageIndex, Is.EqualTo(1));
            intro.BackButton.onClick.Invoke();
            Assert.That(intro.PageIndex, Is.EqualTo(0));

            // every page has its own title and text, and the text fits its box
            for (int page = 0; page < 3; page++)
            {
                while (intro.PageIndex < page)
                {
                    intro.Next();
                }

                yield return new WaitForSecondsRealtime(0.4f);
                Transform root = intro.transform.Find("Page_" + (page + 1));
                Assert.That(root.gameObject.activeSelf, Is.True);
                Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(0.01f), "the current page has faded in");
                TMP_Text title = root.Find("Heading").GetComponent<TMP_Text>(), body = root.Find("Body").GetComponent<TMP_Text>();
                Assert.That(title.text, Is.EqualTo(IntroSlidesController.Titles[page]));
                Assert.That(body.text.Length, Is.GreaterThan(60));
                body.ForceMeshUpdate();
                Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(((RectTransform)body.transform).rect.height), $"page {page + 1}: the text fits");
                Assert.That(title.preferredHeight, Is.LessThanOrEqualTo(((RectTransform)title.transform).rect.height + 1f), $"page {page + 1}: the title fits");
                for (int other = 0; other < 3; other++)
                {
                    if (other != page)
                    {
                        Assert.That(intro.transform.Find("Page_" + (other + 1)).gameObject.activeSelf, Is.False, "the other pages are hidden once faded out");
                    }
                }
            }

            // SKIP from the middle and LET'S PLAY from the end both lead to the existing Character Selection, and neither resets anything again
            intro.Open();
            intro.Next();
            int writes = store.WriteCount;
            intro.SkipButton.onClick.Invoke();
            yield return null;
            AssertInCharacterSelection(intro, settings);
            Characters().Cancel();
            yield return null;

            yield return StartNewGame();
            writes = store.WriteCount;
            intro.Next();
            intro.Next();
            intro.PlayButton.onClick.Invoke();
            yield return null;
            AssertInCharacterSelection(intro, settings);
            Assert.That(store.WriteCount, Is.EqualTo(writes), "LET'S PLAY writes nothing");
        }

        [UnityTest]
        public IEnumerator AndroidBack_ClosesTheIntroduction_ReturnsToTheMainMenu_AndNothingIsResetAgain()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return LoadMenu();
            InputSettings.BackgroundBehavior previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("Intro Test Keyboard");
            try
            {
                yield return StartNewGame();
                var intro = Find<IntroSlidesController>("IntroPanel");
                var settings = Find<SettingsPanelController>("SettingsPanel");
                AssertFresh(store.Json);
                int writes = store.WriteCount;
                intro.Next();
                keyboard.MakeCurrent();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                AssertBackInTheMainMenu(intro, settings);
                Assert.That(Characters().IsOpen, Is.False, "the Android Back key does not go on to the Character Selection");
                Assert.That(store.WriteCount, Is.EqualTo(writes), "and resets nothing again");
                AssertFresh(store.Json);
                Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId));
            }
            finally
            {
                if (keyboard != null && keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                InputSystem.settings.backgroundBehavior = previousBackground;
            }
        }

        [UnityTest]
        public IEnumerator TheSave_IsResetExactlyOnce_AcrossTheWholeFlow_AndPlayStillWorksAfterwards()
        {
            yield return LoadMenu();
            int writes = store.WriteCount;
            yield return StartNewGame();
            Assert.That(store.WriteCount, Is.EqualTo(writes + 1), "START NEW GAME saves exactly once");
            var intro = Find<IntroSlidesController>("IntroPanel");
            intro.Next();
            intro.Back();
            intro.Next();
            intro.Next();
            intro.PlayButton.onClick.Invoke();
            yield return null;
            Characters().Confirm(); // choose the shown miner: the existing Character Selection
            yield return null;
            Assert.That(store.WriteCount, Is.EqualTo(writes + 1), "the introduction and the Character Selection write no progress");
            AssertFresh(store.Json);

            // the normal PLAY flow: Area Selection with the fresh game (Green Mine playable, 0 / 500)
            Assert.That(Find<Button>("PlayButton").gameObject.activeInHierarchy, Is.True);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            AreaSelectController areas = Find<AreaSelectController>("AreaPanel");
            Assert.That(areas.IsOpen, Is.True);
            AreaCardView green = null;
            for (int i = 0; i < areas.CardCount; i++)
            {
                if (areas.GetCard(i).AreaId == "green_mine")
                {
                    green = areas.GetCard(i);
                }
            }

            Assert.That(green, Is.Not.Null);
            Assert.That(green.State.Availability, Is.EqualTo(AreaAvailability.Playable));
            Assert.That(green.CaptionLabel.text, Is.EqualTo("0 / 500 COLLECTED"));
        }

        [UnityTest]
        public IEnumerator OnAnIphone_TheIntroductionStaysInsideTheSafeArea_TakesEveryTouch_AndWorksWithRealTaps()
        {
            SafeAreaFitter.SafeAreaOverride = () => new Rect(Screen.width * 0.0633f, Screen.height * 0.0488f, Screen.width * (1f - 2f * 0.0633f), Screen.height * (1f - 0.0488f));
            yield return LoadMenu();
            Rect safe = new Rect(Screen.width * 0.0633f, Screen.height * 0.0488f, Screen.width * (1f - 2f * 0.0633f), Screen.height * (1f - 0.0488f));
            Vector2 playButtonCentre = Centre(Find<Button>("PlayButton"));
            yield return StartNewGame();
            var intro = Find<IntroSlidesController>("IntroPanel");

            // the backdrop covers the WHOLE screen, including the strips above and below the safe area: the Main Menu is not visible anywhere
            Rect cover = ScreenRect((RectTransform)intro.transform.Find("Backdrop"));
            Assert.That(cover.xMin <= 0f && cover.yMin <= 0f && cover.xMax >= Screen.width && cover.yMax >= Screen.height, Is.True, $"backdrop {cover} covers the screen {Screen.width}x{Screen.height}");
            Image backdropImage = intro.transform.Find("Backdrop").GetComponent<Image>();
            Assert.That(backdropImage.color.a, Is.EqualTo(1f), "fully opaque");
            Assert.That(backdropImage.raycastTarget, Is.True);

            for (int page = 0; page < 3; page++)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                foreach (RectTransform rt in intro.transform.Find("Page_" + (page + 1)).GetComponentsInChildren<RectTransform>(false))
                {
                    if (rt.GetComponent<TMP_Text>() != null || rt.name == "Picture")
                    {
                        Rect r = ScreenRect(rt);
                        Assert.That(safe.Contains(r.min) && safe.Contains(r.max), Is.True, $"page {page + 1}: {rt.name} {r} inside the safe area {safe}");
                    }
                }

                foreach (Button b in new[] { intro.BackButton, intro.SkipButton, intro.NextButton, intro.PlayButton })
                {
                    if (b.gameObject.activeSelf)
                    {
                        Rect r = ScreenRect((RectTransform)b.transform);
                        Assert.That(safe.Contains(r.min) && safe.Contains(r.max), Is.True, $"page {page + 1}: {b.name} {r} inside the safe area");
                        Assert.That(r.height, Is.GreaterThanOrEqualTo(48f * Screen.dpi / 160f * 0.5f), "a comfortable touch size");
                    }
                }

                intro.Next();
            }

            // the dim takes every touch: a real tap where the PLAY button of the menu lies does not open the Area Selection
            intro.Open();
            yield return null;
            yield return Press(playButtonCentre);
            Assert.That(Find<AreaSelectController>("AreaPanel").IsOpen, Is.False, "nothing behind the introduction can be pressed");
            Assert.That(intro.IsOpen, Is.True);

            // real taps: NEXT, BACK, NEXT, NEXT, LET'S PLAY
            yield return Press(Centre(intro.NextButton));
            Assert.That(intro.PageIndex, Is.EqualTo(1));
            yield return Press(Centre(intro.BackButton));
            Assert.That(intro.PageIndex, Is.EqualTo(0));
            yield return Press(Centre(intro.NextButton));
            yield return Press(Centre(intro.NextButton));
            Assert.That(intro.PageIndex, Is.EqualTo(2));
            yield return Press(Centre(intro.PlayButton));
            Assert.That(intro.IsOpen, Is.False);
            Assert.That(Characters().IsOpen, Is.True, "LET'S PLAY by a real tap opens the Character Selection");
            Characters().Cancel();
            yield return null;

            // and SKIP by a real tap
            yield return StartNewGame();
            yield return Press(Centre(intro.SkipButton));
            Assert.That(intro.IsOpen, Is.False);
            Assert.That(Characters().IsOpen, Is.True, "SKIP by a real tap opens the Character Selection");
            Assert.That(store.Json, Does.Contain("\"totalGold\":0"), "still the fresh save: the introduction never writes");
        }
    }
}
#endif
