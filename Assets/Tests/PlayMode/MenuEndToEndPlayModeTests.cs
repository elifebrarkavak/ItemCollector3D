#if UNITY_EDITOR
using System.Collections;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
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
    /// The whole route with REAL scene loads (no hooks): Main Menu -> PLAY -> GREEN MINE (collect a coin) -> PAUSE -> SETTINGS -> RESUME
    /// -> PAUSE -> MAIN MENU -> PLAY again (the coin stays collected, the setting stays changed).
    /// </summary>
    public sealed class MenuEndToEndPlayModeTests
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GamePause.Resume();
            SceneNavigator.LoadHook = null;
            // The flow replaces the test runner's scene (Single loads): leave one empty scene and unload everything else.
            Scene empty = SceneManager.CreateScene("TestEmpty_" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
        }

        private static T Find<T>(string name) where T : Component
        {
            // Several controls share a name across the panels of one scene (the Pause Menu and the final celebration panel both have a MainMenuButton): the one the player can reach is the active one
            Scene active = SceneManager.GetActiveScene();
            T inactive = null;
            foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            {
                if (item.name == name && item.gameObject.scene == active)
                {
                    if (item.gameObject.activeInHierarchy)
                    {
                        return item;
                    }

                    inactive ??= item;
                }
            }

            if (inactive != null)
            {
                return inactive;
            }

            Assert.Fail($"{typeof(T).Name} '{name}' not found in {active.name}");
            return null;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeout = 8f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while ((SceneManager.GetActiveScene().name != sceneName || !SceneManager.GetActiveScene().isLoaded) && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName), "Scene did not load in time.");
            yield return null;
            yield return null;
            yield return null;
        }

        private static TreasureSpot FindSpot(string id)
        {
            foreach (TreasureSpot spot in TreasureSpot.Active)
            {
                if (spot.SpotId == id)
                {
                    return spot;
                }
            }

            Assert.Fail("Treasure spot not found: " + id);
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

        [UnityTest]
        public IEnumerator Flow_MainMenu_Play_Gameplay_Pause_Settings_Resume_MainMenu_RealSceneLoads()
        {
            MemoryProgressStore progressStore = ProgressTestIsolation.SavedGameStore(); // a game in progress: PLAY continues it
            var settingsStore = new MemorySettingsStore();
            ProgressManager.StoreFactoryOverride = () => progressStore; // survives "restarting" the scenes
            GameSettings.StoreFactoryOverride = () => settingsStore;
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();

            // ---- Main Menu
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuPath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNavigator.MainMenuScene));
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            // ---- PLAY -> Area Selection -> GREEN MINE -> Gameplay (real scene load, the scene is listed in Build Settings)
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNavigator.MainMenuScene), "PLAY opens the Area Selection, not the gameplay scene");
            Find<Button>("AreaPlay_green_mine").onClick.Invoke();
            yield return WaitForScene(SceneNavigator.GreenMineScene);
            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            TreasureSpot coin = FindSpot("green_mine.meadow_01"); // a Gold Coin (10 gold) in the GreenMine meadow
            Assert.That(hunt, Is.Not.Null);
            Assert.That(Find<TMP_Text>("GoldText").text, Is.EqualTo("0 / 500 GOLD"));
            Assert.That(Find<TMP_Text>("LevelTitle").text, Is.EqualTo("GREEN MINE"));

            // ---- the treasure system still works inside the menu flow
            Transform player = GameObject.Find("Player").transform;
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = coin.transform.position + new Vector3(0f, 1.1f, -1.5f);
            controller.enabled = true;
            yield return WaitUnscaled(0.4f);
            Find<Button>("DigButton").onClick.Invoke();
            float end = Time.time + 3f;
            while (coin.State == TreasureState.Hidden && Time.time < end)
            {
                yield return null;
            }

            yield return WaitUnscaled(0.2f);
            Find<Button>("CollectButton").onClick.Invoke();
            yield return null;
            Assert.That(coin.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(hunt.TotalGold, Is.EqualTo(10));

            // ---- PAUSE -> SETTINGS (change SFX) -> BACK -> RESUME
            Find<Button>("PauseButton").onClick.Invoke();
            Assert.That(GamePause.IsPaused, Is.True);
            Find<Button>("SettingsButton").onClick.Invoke();
            Find<SettingsPanelController>("SettingsPanel").SfxSlider.value = 0.3f;
            Find<Button>("BackButton").onClick.Invoke();
            Find<Button>("ResumeButton").onClick.Invoke();
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.3f).Within(1e-4f));

            // ---- PAUSE -> MAIN MENU (real scene load back to the menu)
            Find<Button>("PauseButton").onClick.Invoke();
            Find<Button>("MainMenuButton").onClick.Invoke();
            yield return WaitForScene(SceneNavigator.MainMenuScene);
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "The menu must not start frozen.");

            // ---- the settings survived: the Main Menu's settings panel shows them
            Find<Button>("SettingsButton").onClick.Invoke();
            Assert.That(Find<SettingsPanelController>("SettingsPanel").SfxSlider.value, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(new GameSettings(settingsStore).SfxVolume, Is.EqualTo(0.3f).Within(1e-4f), "persisted in the settings store");
            Find<Button>("BackButton").onClick.Invoke();

            // ---- PLAY again: the collected coin is still collected, gold is still 10 (and the Area Selection shows it: 10 / 500)
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(Find<AreaSelectController>("AreaPanel").GetCard(0).TreasuresLabel.text, Is.EqualTo("TREASURES: 1 / 15"), "the Area Selection shows the saved progress");
            Find<Button>("AreaPlay_green_mine").onClick.Invoke();
            yield return WaitForScene(SceneNavigator.GreenMineScene);
            Assert.That(FindSpot("green_mine.meadow_01").State, Is.EqualTo(TreasureState.Collected), "Progress survived the menu round trip.");
            Assert.That(Find<TMP_Text>("GoldText").text, Is.EqualTo("10 / 500 GOLD"));
        }
    }
}
#endif
