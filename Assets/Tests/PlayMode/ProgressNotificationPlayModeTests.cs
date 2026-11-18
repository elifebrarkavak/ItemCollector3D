#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>"AREA COMPLETE!" / "NEW AREA UNLOCKED!" notifications on top of the existing ProgressManager events.</summary>
    public sealed class ProgressNotificationPlayModeTests
    {
        private const string GameplayPath = "Assets/Scenes/GameplayTest.unity";
        private Scene loadedScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GamePause.Resume();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
        }

        private IEnumerator LoadScene(IProgressStore store)
        {
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(GameplayPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(GameplayPath);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntilShowing(NotificationBanner banner, float timeout = 3f)
        {
            float end = Time.unscaledTime + timeout;
            while (!banner.IsShowing && Time.unscaledTime < end)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntilHidden(NotificationBanner banner, float timeout = 4f)
        {
            float end = Time.unscaledTime + timeout;
            while (banner.IsShowing && Time.unscaledTime < end)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AreaComplete_ThenNewAreaUnlocked_AreShownInOrder_WithAnimation_AndNeverRepeat()
        {
            yield return LoadScene(new MemoryProgressStore());
            ProgressManager manager = Object.FindAnyObjectByType<ProgressManager>();
            ProgressNotificationPresenter presenter = Object.FindAnyObjectByType<ProgressNotificationPresenter>();
            NotificationBanner banner = Object.FindAnyObjectByType<NotificationBanner>();
            CanvasGroup group = banner.GetComponent<CanvasGroup>();
            Assert.That(presenter, Is.Not.Null, "GameplayTest must contain the ProgressNotification prefab.");
            Assert.That(banner.IsShowing, Is.False);
            Assert.That(group.alpha, Is.EqualTo(0f), "Hidden until a notification arrives.");

            manager.Service.RegisterCollected("note.a", 500); // crosses the Green Mine goal: complete + unlock Rocky Canyon
            Assert.That(presenter.QueuedCount, Is.EqualTo(2), "Both events are queued.");

            // --- 1) AREA COMPLETE! (watch the animation)
            yield return WaitUntilShowing(banner);
            Assert.That(banner.CurrentTitle, Is.EqualTo("AREA COMPLETE!"));
            Assert.That(banner.CurrentSubtitle, Is.EqualTo("GREEN MINE"));
            float maxScale = 0f;
            float maxAlpha = 0f;
            float firstAlpha = group.alpha;
            while (banner.IsShowing)
            {
                maxScale = Mathf.Max(maxScale, banner.transform.localScale.x);
                maxAlpha = Mathf.Max(maxAlpha, group.alpha);
                yield return null;
            }

            Assert.That(firstAlpha, Is.LessThan(1f), "It fades in.");
            Assert.That(maxAlpha, Is.EqualTo(1f).Within(0.01f), "Fully visible during the hold.");
            Assert.That(maxScale, Is.GreaterThan(1.03f), "Pop-in overshoot.");
            Assert.That(group.alpha, Is.EqualTo(0f), "Fades out again.");
            Assert.That(banner.transform.localScale, Is.EqualTo(Vector3.one), "Scale is back to normal.");

            // --- 2) NEW AREA UNLOCKED! comes next
            yield return WaitUntilShowing(banner);
            Assert.That(banner.CurrentTitle, Is.EqualTo("NEW AREA UNLOCKED!"));
            Assert.That(banner.CurrentSubtitle, Is.EqualTo("ROCKY CANYON"));
            yield return WaitUntilHidden(banner);
            Assert.That(banner.ShowCount, Is.EqualTo(2));

            // --- never repeated
            manager.Service.RegisterCollected("note.b", 100);
            manager.Service.RegisterCollected("note.a", 500); // duplicate id: ignored
            yield return WaitUnscaled(0.5f);
            Assert.That(banner.ShowCount, Is.EqualTo(2), "The same notifications are never shown again.");
            Assert.That(banner.IsShowing, Is.False);
            Assert.That(presenter.QueuedCount, Is.EqualTo(0));
            Assert.That(presenter.ShownCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Notification_NeverBlocksTouches()
        {
            yield return LoadScene(new MemoryProgressStore());
            ProgressManager manager = Object.FindAnyObjectByType<ProgressManager>();
            NotificationBanner banner = Object.FindAnyObjectByType<NotificationBanner>();
            CanvasGroup group = banner.GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            foreach (Graphic graphic in banner.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(graphic.raycastTarget, Is.False, graphic.name + " must not catch touches.");
            }

            manager.Service.RegisterCollected("touch.a", 500);
            yield return WaitUntilShowing(banner);
            yield return WaitUnscaled(0.4f);
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, banner.transform.position);
            var data = new PointerEventData(EventSystem.current) { position = center };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            foreach (RaycastResult hit in hits)
            {
                Assert.That(hit.gameObject.GetComponentInParent<NotificationBanner>(), Is.Null, "The banner must not be hit: " + hit.gameObject.name);
            }
        }

        [UnityTest]
        public IEnumerator LoadingASavedGame_DoesNotReplayNotifications()
        {
            // a save that already completed Green Mine and unlocked Rocky Canyon
            var store = new MemoryProgressStore();
            ProgressService.Create(null, store).RegisterCollected("old.treasure", 650);
            Assert.That(store.Json, Does.Contain("rocky_canyon"));

            yield return LoadScene(store);
            ProgressManager manager = Object.FindAnyObjectByType<ProgressManager>();
            ProgressNotificationPresenter presenter = Object.FindAnyObjectByType<ProgressNotificationPresenter>();
            NotificationBanner banner = Object.FindAnyObjectByType<NotificationBanner>();
            Assert.That(manager.Service.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(manager.Service.IsAreaUnlocked("rocky_canyon"), Is.True);

            yield return WaitUnscaled(0.6f);
            Assert.That(banner.ShowCount, Is.EqualTo(0), "Nothing is announced again after loading.");
            Assert.That(presenter.ShownCount, Is.EqualTo(0));

            manager.Service.RegisterCollected("new.treasure", 50); // 700: still below the next threshold
            yield return WaitUnscaled(0.4f);
            Assert.That(banner.ShowCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Notification_StillPlaysWhileThePauseMenuIsOpen()
        {
            yield return LoadScene(new MemoryProgressStore());
            ProgressManager manager = Object.FindAnyObjectByType<ProgressManager>();
            NotificationBanner banner = Object.FindAnyObjectByType<NotificationBanner>();

            GamePause.Pause();
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            manager.Service.RegisterCollected("pause.a", 500);
            yield return WaitUntilShowing(banner);
            Assert.That(banner.IsShowing, Is.True, "The banner uses unscaled time, so it works while paused.");
            Assert.That(banner.CurrentTitle, Is.EqualTo("AREA COMPLETE!"));
            yield return WaitUntilHidden(banner);
            Assert.That(banner.ShowCount, Is.GreaterThanOrEqualTo(1));
        }
    }
}
#endif
