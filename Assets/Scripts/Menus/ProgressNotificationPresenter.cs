using System.Collections.Generic;
using ItemCollector3D.Progress;
using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// Shows "AREA COMPLETE!" and "NEW AREA UNLOCKED!" when the existing ProgressManager raises AreaCompleted / AreaUnlocked.
    /// Notifications are queued (shown one after another) and each (kind, area) is shown at most once per session; the progress
    /// service itself never re-raises them (not even after loading a save), so they cannot repeat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProgressNotificationPresenter : MonoBehaviour
    {
        [SerializeField] private ProgressManager progress;
        [SerializeField] private NotificationBanner banner;
        [SerializeField] private Color completeColor = new(0.95f, 0.71f, 0.2f, 1f);
        [SerializeField] private Color unlockedColor = new(0.55f, 0.9f, 0.55f, 1f);

        private readonly Queue<(string title, string subtitle, Color color)> queue = new();
        private readonly HashSet<string> shown = new();
        private ProgressManager subscribedTo;

        public int QueuedCount => queue.Count;
        public int ShownCount => shown.Count;
        public IReadOnlyCollection<string> ShownKeys => shown;

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (progress == null || subscribedTo != null)
            {
                return;
            }

            subscribedTo = progress;
            subscribedTo.AreaCompleted += HandleCompleted;
            subscribedTo.AreaUnlocked += HandleUnlocked;
        }

        private void Unsubscribe()
        {
            if (subscribedTo == null)
            {
                return;
            }

            subscribedTo.AreaCompleted -= HandleCompleted;
            subscribedTo.AreaUnlocked -= HandleUnlocked;
            subscribedTo = null;
        }

        private void Update()
        {
            if (subscribedTo == null)
            {
                Subscribe();
            }

            if (banner != null && !banner.IsShowing && queue.Count > 0)
            {
                (string title, string subtitle, Color color) next = queue.Dequeue();
                banner.Show(next.title, next.subtitle, next.color);
            }
        }

        private void HandleCompleted(string areaId)
        {
            Enqueue("complete", areaId, ProgressService.AreaCompleteMessage + "!", completeColor);
        }

        private void HandleUnlocked(string areaId)
        {
            Enqueue("unlock", areaId, ProgressService.NewAreaUnlockedMessage + "!", unlockedColor);
        }

        private void Enqueue(string kind, string areaId, string title, Color color)
        {
            if (!shown.Add(kind + ":" + areaId))
            {
                return; // never show the same notification twice
            }

            string areaName = subscribedTo != null && subscribedTo.Service != null ? subscribedTo.Service.GetDisplayName(areaId) : null;
            queue.Enqueue((title, areaName ?? areaId, color));
        }
    }
}
