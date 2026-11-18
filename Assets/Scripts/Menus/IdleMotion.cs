using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>Tiny idle motion for menu scenery (a bobbing / swinging placeholder miner). Cheap: one transform write per frame.</summary>
    [DisallowMultipleComponent]
    public sealed class IdleMotion : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float bobHeight = 0.05f;
        [SerializeField, Min(0f)] private float bobSpeed = 1.6f;
        [SerializeField, Range(0f, 45f)] private float swingDegrees;
        [SerializeField, Min(0f)] private float swingSpeed = 2.2f;
        [SerializeField] private Vector3 swingAxis = Vector3.right;

        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            transform.localPosition = startPosition + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);
            if (swingDegrees > 0f)
            {
                transform.localRotation = startRotation * Quaternion.AngleAxis(Mathf.Sin(t * swingSpeed) * swingDegrees, swingAxis);
            }
        }
    }
}
