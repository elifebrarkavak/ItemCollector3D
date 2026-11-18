using System.Collections;
using System.Reflection;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Treasure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>Settings: storage, clamping, persistence, and their REAL effect on the camera and the sound effects.</summary>
    public sealed class SettingsPlayModeTests
    {
        private readonly System.Collections.Generic.List<Object> created = new();
        private readonly System.Collections.Generic.List<InputDevice> devices = new();
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;

        [SetUp]
        public void SetUp()
        {
            GameSettings.ResetCurrent(); // fresh in-memory settings (see ProgressTestIsolation)
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (InputDevice device in devices)
            {
                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            devices.Clear();
            foreach (Object o in created)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            created.Clear();
            GamePause.Resume();
            GameSettings.ResetCurrent();
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
        }

        // ------------------------------------------------------------ storage

        [Test]
        public void Defaults_AreUsed_WhenNothingIsStored()
        {
            var settings = new GameSettings(new MemorySettingsStore());
            Assert.That(settings.MusicVolume, Is.EqualTo(GameSettings.DefaultMusic));
            Assert.That(settings.SfxVolume, Is.EqualTo(GameSettings.DefaultSfx));
            Assert.That(settings.CameraSensitivity, Is.EqualTo(GameSettings.DefaultCameraSensitivity));
        }

        [Test]
        public void Values_AreClamped_AndSaved_AndReloadedByANewInstance()
        {
            var store = new MemorySettingsStore();
            var settings = new GameSettings(store);
            int changes = 0;
            settings.Changed += () => changes++;

            settings.SetMusicVolume(0.3f);
            settings.SetSfxVolume(0.5f);
            settings.SetSfxVolume(5f);          // above range: clamped to 1 (a real change from 0.5)
            settings.SetCameraSensitivity(-3f); // below range
            Assert.That(settings.MusicVolume, Is.EqualTo(0.3f));
            Assert.That(settings.SfxVolume, Is.EqualTo(1f), "clamped to 1");
            Assert.That(settings.CameraSensitivity, Is.EqualTo(GameSettings.MinCameraSensitivity), "clamped to the minimum");
            Assert.That(changes, Is.EqualTo(4));
            Assert.That(store.SaveCount, Is.EqualTo(4), "Saved on every change.");

            settings.SetMusicVolume(0.3f); // same value: no event, no save
            settings.SetSfxVolume(1f);     // already 1: no event either
            Assert.That(changes, Is.EqualTo(4));
            Assert.That(store.SaveCount, Is.EqualTo(4));

            var reopened = new GameSettings(store); // "the game was restarted"
            Assert.That(reopened.MusicVolume, Is.EqualTo(0.3f));
            Assert.That(reopened.SfxVolume, Is.EqualTo(1f));
            Assert.That(reopened.CameraSensitivity, Is.EqualTo(GameSettings.MinCameraSensitivity));
        }

        [Test]
        public void NaNAndOutOfRangeStoredValues_FallBackToSafeValues()
        {
            var store = new MemorySettingsStore();
            store.SetFloat(GameSettings.MusicKey, float.NaN);
            store.SetFloat(GameSettings.SfxKey, 99f);
            store.SetFloat(GameSettings.CameraKey, float.PositiveInfinity);
            var settings = new GameSettings(store);
            Assert.That(settings.MusicVolume, Is.EqualTo(GameSettings.DefaultMusic), "NaN -> default");
            Assert.That(settings.SfxVolume, Is.EqualTo(1f), "99 -> clamped");
            Assert.That(settings.CameraSensitivity, Is.EqualTo(GameSettings.DefaultCameraSensitivity), "Infinity -> default");

            settings.SetMusicVolume(float.NaN);
            Assert.That(settings.MusicVolume, Is.EqualTo(GameSettings.DefaultMusic), "setting NaN is ignored safely");
        }

        [Test]
        public void ResetToDefaults_RestoresEverything()
        {
            var settings = new GameSettings(new MemorySettingsStore());
            settings.SetMusicVolume(0.1f);
            settings.SetSfxVolume(0.2f);
            settings.SetCameraSensitivity(1.7f);
            settings.ResetToDefaults();
            Assert.That(settings.MusicVolume, Is.EqualTo(GameSettings.DefaultMusic));
            Assert.That(settings.SfxVolume, Is.EqualTo(GameSettings.DefaultSfx));
            Assert.That(settings.CameraSensitivity, Is.EqualTo(GameSettings.DefaultCameraSensitivity));
        }

        [Test]
        public void PlayerPrefsStore_RoundTrips_UsingAPrivatePrefix()
        {
            string prefix = "ic3d.test." + System.Guid.NewGuid().ToString("N") + ".";
            var store = new PlayerPrefsSettingsStore(prefix);
            try
            {
                Assert.That(store.TryGetFloat(GameSettings.MusicKey, out _), Is.False);
                var first = new GameSettings(store);
                first.SetMusicVolume(0.42f);
                first.SetCameraSensitivity(1.5f);

                var second = new GameSettings(new PlayerPrefsSettingsStore(prefix));
                Assert.That(second.MusicVolume, Is.EqualTo(0.42f).Within(1e-5f), "stored in PlayerPrefs");
                Assert.That(second.CameraSensitivity, Is.EqualTo(1.5f).Within(1e-5f));
            }
            finally
            {
                store.DeleteKey(GameSettings.MusicKey);
                store.DeleteKey(GameSettings.SfxKey);
                store.DeleteKey(GameSettings.CameraKey);
                PlayerPrefs.Save();
            }
        }

        // ------------------------------------------------------------ real effects

        [Test]
        public void SfxVolume_ScalesTheCollectAndDigSounds()
        {
            var collectObject = new GameObject("Test Collect", typeof(CollectSoundPlayer));
            var digObject = new GameObject("Test Dig", typeof(DigSoundPlayer));
            created.Add(collectObject);
            created.Add(digObject);
            CollectSoundPlayer collect = collectObject.GetComponent<CollectSoundPlayer>();
            DigSoundPlayer dig = digObject.GetComponent<DigSoundPlayer>();

            GameSettings.Current.SetSfxVolume(1f);
            Assert.That(collect.Play(null, 1f), Is.True);
            Assert.That(dig.PlayStroke(0), Is.True);
            float collectFull = collect.LastVolume;
            float digFull = dig.LastVolume;
            Assert.That(collectFull, Is.GreaterThan(0f));
            Assert.That(digFull, Is.GreaterThan(0f));

            GameSettings.Current.SetSfxVolume(0.5f);
            Object.DestroyImmediate(collectObject);
            Object.DestroyImmediate(digObject);
            collectObject = new GameObject("Test Collect 2", typeof(CollectSoundPlayer));
            digObject = new GameObject("Test Dig 2", typeof(DigSoundPlayer));
            created.Add(collectObject);
            created.Add(digObject);
            collect = collectObject.GetComponent<CollectSoundPlayer>();
            dig = digObject.GetComponent<DigSoundPlayer>();
            collect.Play(null, 1f);
            dig.PlayStroke(0);
            Assert.That(collect.LastVolume, Is.EqualTo(collectFull * 0.5f).Within(1e-4f), "half SFX volume = half collect volume");
            Assert.That(dig.LastVolume, Is.EqualTo(digFull * 0.5f).Within(1e-4f), "half SFX volume = half dig volume");

            GameSettings.Current.SetSfxVolume(0f);
            Object.DestroyImmediate(collectObject);
            collectObject = new GameObject("Test Collect 3", typeof(CollectSoundPlayer));
            created.Add(collectObject);
            collectObject.GetComponent<CollectSoundPlayer>().Play(null, 1f);
            Assert.That(collectObject.GetComponent<CollectSoundPlayer>().LastVolume, Is.EqualTo(0f), "SFX 0 = silent");
        }

        [UnityTest]
        public IEnumerator CameraSensitivity_ScalesTheOrbit_AndPauseStopsIt()
        {
            var targetObject = new GameObject("Test Target");
            created.Add(targetObject);
            Mouse mouse = InputSystem.AddDevice<Mouse>("Settings Test Mouse");
            devices.Add(mouse);
            mouse.MakeCurrent();
            yield return null;

            float YawAfterDrag(float sensitivity, bool paused)
            {
                GameSettings.Current.SetCameraSensitivity(sensitivity);
                var cameraObject = new GameObject("Test Camera", typeof(Camera));
                created.Add(cameraObject);
                ThirdPersonCameraController controller = cameraObject.AddComponent<ThirdPersonCameraController>();
                controller.SetTarget(targetObject.transform);
                controller.enabled = false; // read the input exactly once, by hand

                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(500f, 400f), delta = new Vector2(100f, 0f) }
                    .WithButton(MouseButton.Right));
                InputSystem.Update();

                if (paused)
                {
                    GamePause.Pause();
                }

                typeof(ThirdPersonCameraController).GetMethod("ReadOrbitInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                GamePause.Resume();
                return (float)typeof(ThirdPersonCameraController).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            }

            float normal = YawAfterDrag(1f, false);
            float fast = YawAfterDrag(2f, false);
            float slow = YawAfterDrag(0.5f, false);
            Assert.That(normal, Is.GreaterThan(1f), "The drag must rotate the camera.");
            Assert.That(fast, Is.EqualTo(normal * 2f).Within(0.01f), "Camera Sensitivity 2x = twice the rotation.");
            Assert.That(slow, Is.EqualTo(normal * 0.5f).Within(0.01f), "Camera Sensitivity 0.5x = half the rotation.");
            Assert.That(YawAfterDrag(1f, true), Is.EqualTo(0f), "While paused the orbit input is ignored.");
        }

        [Test]
        public void GamePause_FreezesTimeAndAudio_AndRestoresThePreviousTimeScale()
        {
            float before = Time.timeScale;
            Time.timeScale = 0.5f; // pretend slow motion
            try
            {
                GamePause.Pause();
                Assert.That(GamePause.IsPaused, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(0f));
                Assert.That(AudioListener.pause, Is.True);
                GamePause.Pause(); // pausing twice must not lose the remembered scale
                GamePause.Resume();
                Assert.That(GamePause.IsPaused, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(0.5f), "the previous time scale is restored");
                Assert.That(AudioListener.pause, Is.False);
                GamePause.Resume(); // resuming when not paused is harmless
                Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            }
            finally
            {
                GamePause.Resume();
                Time.timeScale = before;
            }
        }
    }
}
