#if UNITY_EDITOR
using System.Collections;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Treasure;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>Main Menu, Settings panel and Pause Menu: buttons, flow, persistence, and that pausing really freezes gameplay.</summary>
    public sealed class MenuFlowPlayModeTests
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayPath = "Assets/Scenes/GameplayTest.unity";
        private const string SettingsPrefabPath = "Assets/Prefabs/Menus/SettingsPanel.prefab";

        private Scene loadedScene;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameSettings.ResetCurrent();
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GamePause.Resume();
            SceneNavigator.LoadHook = null;
            MainMenuController.ExitHook = null;
            if (keyboard != null && keyboard.added)
            {
                InputSystem.RemoveDevice(keyboard);
            }

            keyboard = null;
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
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

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                yield return null;
            }
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        // ------------------------------------------------------------------ Main Menu

        [UnityTest]
        public IEnumerator MainMenu_HasLightScenery_AndCorrectButtons()
        {
            yield return LoadScene(MainMenuPath);
            Transform scenery = Find<Transform>("Scenery");
            Assert.That(scenery.childCount, Is.InRange(8, 40), "Light scenery: rocks, trees, mine entrance, miner.");
            Assert.That(GameObject.Find("MineEntrance"), Is.Not.Null);
            Assert.That(GameObject.Find("PlaceholderMiner"), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(), Is.Not.Null);
            Assert.That(Find<TMP_Text>("Title").text, Does.Contain("ITEM COLLECTOR"));
            foreach (string label in new[] { "PlayButton", "SettingsButton", "ExitButton" })
            {
                Assert.That(Find<Button>(label).interactable, Is.True, label);
            }

            // placeholder primitives only: no colliders / heavy components in the scenery
            Assert.That(scenery.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator MainMenu_Play_Settings_Exit_Work()
        {
            yield return LoadScene(MainMenuPath);
            string requested = null;
            bool exited = false;
            SceneNavigator.LoadHook = name => requested = name;
            MainMenuController.ExitHook = () => exited = true;
            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
            Transform buttons = Find<Transform>("Buttons");

            Find<Button>("PlayButton").onClick.Invoke();
            Assert.That(requested, Is.EqualTo(SceneNavigator.GameplayScene), "PLAY opens the existing GameplayTest scene.");

            Find<Button>("SettingsButton").onClick.Invoke();
            Assert.That(menu.IsSettingsOpen, Is.True);
            Assert.That(buttons.gameObject.activeSelf, Is.False, "The menu buttons give way to the settings panel.");

            Find<Button>("BackButton").onClick.Invoke();
            Assert.That(menu.IsSettingsOpen, Is.False);
            Assert.That(buttons.gameObject.activeSelf, Is.True);

            Find<Button>("ExitButton").onClick.Invoke();
            Assert.That(exited, Is.True);
        }

        [UnityTest]
        public IEnumerator Settings_ShowStoredValues_ApplyLive_AndArePersisted()
        {
            var store = new MemorySettingsStore();
            GameSettings.StoreFactoryOverride = () => store;
            GameSettings.ResetCurrent();
            GameSettings.Current.SetMusicVolume(0.25f);
            GameSettings.Current.SetCameraSensitivity(1.5f);

            yield return LoadScene(MainMenuPath);
            Find<Button>("SettingsButton").onClick.Invoke();
            SettingsPanelController panel = Find<SettingsPanelController>("SettingsPanel");
            AmbientMusicPlayer music = Object.FindAnyObjectByType<AmbientMusicPlayer>();

            Assert.That(panel.MusicSlider.value, Is.EqualTo(0.25f).Within(1e-4f), "the slider shows the stored value");
            Assert.That(panel.CameraSlider.value, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(Find<TMP_Text>("Value").text, Is.EqualTo("25%").Or.EqualTo("100%").Or.EqualTo("1.50x"));
            Assert.That(music.IsPlaying, Is.True, "The ambient music plays in the menu.");

            panel.MusicSlider.value = 0.6f;
            panel.SfxSlider.value = 0.3f;
            panel.CameraSlider.value = 0.75f;
            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(GameSettings.Current.CameraSensitivity, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(music.CurrentVolume, Is.EqualTo(0.35f * 0.6f).Within(1e-4f), "Music Volume really changes the music loudness.");

            var reopened = new GameSettings(store); // the next launch of the game
            Assert.That(reopened.MusicVolume, Is.EqualTo(0.6f).Within(1e-4f), "saved");
            Assert.That(reopened.SfxVolume, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(reopened.CameraSensitivity, Is.EqualTo(0.75f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator OpeningSettings_NeverChangesTheStoredValues()
        {
            var store = new MemorySettingsStore();
            GameSettings.StoreFactoryOverride = () => store;
            GameSettings.ResetCurrent();
            GameSettings.Current.SetMusicVolume(0.1f);
            GameSettings.Current.SetSfxVolume(0.2f);
            GameSettings.Current.SetCameraSensitivity(1.9f);
            int writes = store.SaveCount;
            int changes = 0;
            GameSettings.Current.Changed += () => changes++;

            yield return LoadScene(MainMenuPath);
            Find<Button>("SettingsButton").onClick.Invoke();
            Find<Button>("BackButton").onClick.Invoke();
            Find<Button>("SettingsButton").onClick.Invoke(); // open again

            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(GameSettings.Current.CameraSensitivity, Is.EqualTo(1.9f).Within(1e-4f),
                "Slider min/max changes must not overwrite the saved camera sensitivity.");
            Assert.That(changes, Is.EqualTo(0), "Opening the panel changes nothing.");
            Assert.That(store.SaveCount, Is.EqualTo(writes), "...and saves nothing.");
        }

        [Test]
        public void MainMenu_And_PauseMenu_UseTheSameSettingsPanelPrefab()
        {
            var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            Assert.That(settingsPrefab, Is.Not.Null);

            foreach (string ownerPath in new[] { "Assets/Prefabs/Menus/MainMenuUI.prefab", "Assets/Prefabs/Menus/PauseMenu.prefab" })
            {
                var owner = AssetDatabase.LoadAssetAtPath<GameObject>(ownerPath);
                Assert.That(owner, Is.Not.Null, ownerPath);
                Transform nested = owner.transform.Find("SettingsPanel");
                Assert.That(nested, Is.Not.Null, ownerPath + " must contain a SettingsPanel.");
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(nested.gameObject), Is.True, "It is a nested prefab instance, not a copy.");
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(nested.gameObject), Is.EqualTo(settingsPrefab),
                    ownerPath + " uses the shared Settings prefab.");
            }

            // and both menus' controllers really own a SettingsPanelController (the same component type, one implementation)
            Assert.That(settingsPrefab.GetComponent<SettingsPanelController>(), Is.Not.Null);
        }

        // ------------------------------------------------------------------ Pause

        [UnityTest]
        public IEnumerator Pause_FreezesGameplay_SettingsWork_ResumeRestores()
        {
            yield return LoadScene(GameplayPath);
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();
            ThirdPersonPlayerController playerController = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
            Transform player = playerController.transform;
            AmbientMusicPlayer music = Object.FindAnyObjectByType<AmbientMusicPlayer>();
            Button pauseButton = Find<Button>("PauseButton");
            GameObject pausePanel = Find<Transform>("PausePanel").gameObject;
            Assert.That(pauseButton.gameObject.activeSelf, Is.True, "The PAUSE button is on the gameplay HUD.");
            Assert.That(pausePanel.activeSelf, Is.False);

            // normal play: the player walks
            playerController.SetMoveInput(Vector2.up);
            float z0 = player.position.z;
            float end = Time.time + 0.4f;
            while (Time.time < end)
            {
                yield return null;
            }

            Assert.That(player.position.z - z0, Is.GreaterThan(1f), "The player walks while the game runs.");

            // --- PAUSE (the joystick input is still held)
            pauseButton.onClick.Invoke();
            yield return null;
            Assert.That(GamePause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(pausePanel.activeSelf, Is.True);
            Assert.That(pauseButton.gameObject.activeSelf, Is.False);
            float pausedZ = player.position.z;
            yield return WaitUnscaled(0.5f);
            Assert.That(Mathf.Abs(player.position.z - pausedZ), Is.LessThan(0.001f), "Gameplay stops while paused.");
            Assert.That(music.IsPlaying, Is.True, "The music keeps playing in the pause menu.");

            // --- SETTINGS inside the pause menu, then BACK
            Find<Button>("SettingsButton").onClick.Invoke();
            Assert.That(pause.IsSettingsOpen, Is.True);
            Assert.That(pausePanel.activeSelf, Is.False);
            Find<SettingsPanelController>("SettingsPanel").SfxSlider.value = 0.3f;
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.3f).Within(1e-4f));
            Find<Button>("BackButton").onClick.Invoke();
            Assert.That(pause.IsSettingsOpen, Is.False);
            Assert.That(pausePanel.activeSelf, Is.True, "BACK returns to the pause panel.");
            Assert.That(GamePause.IsPaused, Is.True);

            // --- RESUME
            Find<Button>("ResumeButton").onClick.Invoke();
            yield return null;
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(pausePanel.activeSelf, Is.False);
            Assert.That(pauseButton.gameObject.activeSelf, Is.True);
            float resumedZ = player.position.z;
            end = Time.time + 0.4f;
            while (Time.time < end)
            {
                yield return null;
            }

            Assert.That(player.position.z - resumedZ, Is.GreaterThan(1f), "The player walks again after RESUME.");
            playerController.ClearMoveInput();
        }

        [UnityTest]
        public IEnumerator Pause_DuringDig_FreezesTheDig_AndResumeCompletesIt()
        {
            yield return LoadScene(GameplayPath);
            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            TreasureSpot coin = Object.FindAnyObjectByType<TreasureSpot>();
            Transform player = GameObject.Find("Player").transform;
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();

            Teleport(player, coin.transform.position + new Vector3(0f, 1.1f, -1.5f));
            yield return WaitUnscaled(0.4f);
            Find<Button>("DigButton").onClick.Invoke();
            yield return null;
            Assert.That(hunt.IsDigging, Is.True);

            pause.Pause();
            yield return WaitUnscaled(hunt.DigDuration + 0.5f); // longer than the whole dig
            Assert.That(hunt.IsDigging, Is.True, "The dig timer is frozen while paused.");
            Assert.That(coin.State, Is.EqualTo(TreasureState.Hidden), "No reveal while paused.");

            // gameplay buttons do nothing while paused (even if invoked directly)
            Find<Button>("DigButton").onClick.Invoke();
            Find<Button>("CollectButton").onClick.Invoke();
            Assert.That(coin.State, Is.EqualTo(TreasureState.Hidden));
            Assert.That(hunt.TotalGold, Is.EqualTo(0));

            pause.Resume();
            float end = Time.time + 3f;
            while (coin.State == TreasureState.Hidden && Time.time < end)
            {
                yield return null;
            }

            Assert.That(coin.State, Is.EqualTo(TreasureState.Revealed), "After RESUME the dig finishes.");
        }

        [UnityTest]
        public IEnumerator MainMenuButton_ResumesTheGame_AndGoesToTheMainMenu()
        {
            yield return LoadScene(GameplayPath);
            string requested = null;
            SceneNavigator.LoadHook = name => requested = name;
            Find<Button>("PauseButton").onClick.Invoke();
            Assert.That(GamePause.IsPaused, Is.True);

            Find<Button>("MainMenuButton").onClick.Invoke();
            Assert.That(requested, Is.EqualTo(SceneNavigator.MainMenuScene));
            Assert.That(GamePause.IsPaused, Is.False, "The next scene must never start frozen.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator EscapeKey_TogglesPause_AndClosesSettings()
        {
            yield return LoadScene(GameplayPath);
            keyboard = InputSystem.AddDevice<Keyboard>("Menu Test Keyboard");
            keyboard.MakeCurrent();
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();
            yield return null;

            IEnumerator Press()
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }

            yield return Press();
            Assert.That(pause.IsPaused, Is.True, "Escape / Android Back pauses.");
            pause.OpenSettings();
            Assert.That(pause.IsSettingsOpen, Is.True);
            yield return Press();
            Assert.That(pause.IsSettingsOpen, Is.False, "Escape closes the settings panel first.");
            Assert.That(pause.IsPaused, Is.True);
            yield return Press();
            Assert.That(pause.IsPaused, Is.False, "Escape again resumes.");
        }

        [UnityTest]
        public IEnumerator PauseMenu_DoesNotBlockTouchControlsWhenNotPaused()
        {
            yield return LoadScene(GameplayPath);
            Assert.That(Find<Transform>("PausePanel").gameObject.activeSelf, Is.False, "The full-screen dim panel is hidden during play.");
            // the pause root itself has no raycast graphic, so the joystick / swipe area underneath stay reachable
            Transform root = Find<PauseMenuController>("PauseMenu").transform;
            Assert.That(root.GetComponent<Graphic>(), Is.Null);
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            Vector2 joystick = RectTransformUtility.WorldToScreenPoint(null, Find<Transform>("MovementJoystick").position);
            var data = new UnityEngine.EventSystems.PointerEventData(eventSystem) { position = joystick };
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(data, results);
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results[0].gameObject.name, Is.EqualTo("MovementJoystick"), "The joystick is the top hit: nothing covers it.");
        }
    }
}
#endif
