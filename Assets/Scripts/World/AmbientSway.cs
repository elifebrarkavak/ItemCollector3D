using ItemCollector3D.Menus;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// Very light life for a handful of separate props (dry shrubs rocking in the breeze, pennants and loose ropes fluttering at the bridge): each one is tilted a few degrees by a slow, gusting
    /// sine, no physics, no colliders, nothing allocated per frame. Only the props within <see cref="activeDistance"/> metres of the camera are updated; the others keep their last pose. It uses
    /// scaled game time, so everything stands still while the game is paused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientSway : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Item
        {
            public Transform target;
            [Tooltip("Largest tilt in degrees (a shrub: 1 - 3, a pennant: 10 - 18).")]
            public float degrees = 2f;
            [Tooltip("Seconds for one full sway.")]
            public float period = 4f;
            public float phase;
            [Tooltip("Local axis the prop tilts about; the second axis gets a smaller, slower sway so the motion never looks mechanical.")]
            public Vector3 axis = Vector3.forward;

            [System.NonSerialized] public Quaternion rest;
        }

        [SerializeField] private Item[] items = System.Array.Empty<Item>();
        [SerializeField, Min(5f)] private float activeDistance = 38f;

        private Camera view;

        public int ItemCount => items.Length;

        /// <summary>How many props were moved in the last frame (the ones near the camera).</summary>
        public int ActiveCount { get; private set; }

        private void Awake()
        {
            foreach (Item item in items)
            {
                if (item.target != null)
                {
                    item.rest = item.target.localRotation;
                }
            }
        }

        private void OnDisable()
        {
            foreach (Item item in items)
            {
                if (item.target != null)
                {
                    item.target.localRotation = item.rest;
                }
            }
        }

        private void Update()
        {
            if (GamePause.IsPaused)
            {
                return;
            }

            if (view == null)
            {
                view = Camera.main;
                if (view == null)
                {
                    return;
                }
            }

            Vector3 eye = view.transform.position;
            float limit = activeDistance * activeDistance;
            float time = Time.time;
            float gust = 0.7f + 0.3f * Mathf.Sin(time * 0.37f) * Mathf.Sin(time * 0.11f + 1f); // slow shared gusts
            int moved = 0;
            foreach (Item item in items)
            {
                Transform t = item.target;
                if (t == null || (t.position - eye).sqrMagnitude > limit)
                {
                    continue;
                }

                float w = Mathf.PI * 2f / Mathf.Max(0.2f, item.period);
                float main = Mathf.Sin(time * w + item.phase) * item.degrees * gust;
                float side = Mathf.Sin(time * w * 0.71f + item.phase * 1.7f + 1.3f) * item.degrees * 0.4f * gust;
                Vector3 other = Vector3.Cross(item.axis, Vector3.up);
                if (other.sqrMagnitude < 0.01f)
                {
                    other = Vector3.right;
                }

                t.localRotation = item.rest * Quaternion.AngleAxis(main, item.axis) * Quaternion.AngleAxis(side, other.normalized);
                moved++;
            }

            ActiveCount = moved;
        }
    }
}
