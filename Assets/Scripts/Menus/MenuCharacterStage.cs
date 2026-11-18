using ItemCollector3D.Characters;
using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// The 3D miner preview of the Main Menu and the Character Selection screen. Every catalog miner is spawned once; only the
    /// chosen one is shown. The stage is positioned from VIEWPORT coordinates (not world units), so on any 16:9 or wider screen the
    /// miner stays on the right of the menu (Menu mode) or in the middle of the screen (Select mode) and fills the same share of the
    /// screen height. Swapping miners slides one out and springs the next one in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuCharacterStage : MonoBehaviour
    {
        public enum Mode { Menu, Select }

        [SerializeField] private Camera stageCamera;
        [SerializeField] private CharacterCatalog catalog;
        [Tooltip("Optional decoration under the feet (stone pedestal + glow). Moves with the miner.")]
        [SerializeField] private Transform pedestal;
        [SerializeField, Min(1f)] private float distanceFromCamera = 9f;
        [SerializeField, Min(0.5f)] private float modelHeight = 2.1f;

        [Header("Menu layout (viewport: x = centre of the miner, y = feet)")]
        [SerializeField] private Vector2 menuAnchor = new Vector2(0.735f, 0.1f);
        [SerializeField, Range(0.3f, 0.95f)] private float menuHeightFraction = 0.62f;
        [SerializeField] private float menuYaw = -16f;

        [Header("Character Selection layout")]
        [SerializeField] private Vector2 selectAnchor = new Vector2(0.5f, 0.25f);
        [SerializeField, Range(0.3f, 0.95f)] private float selectHeightFraction = 0.56f;
        [SerializeField] private float selectYawSway = 24f;

        private sealed class Slot
        {
            public Transform root;
            public Transform model;
            public MinerAnimator animator;
            public float offset;     // local X (anchor space) of this miner
            public float offsetVel;
            public float scale;      // 0 = hidden
            public float scaleVel;
            public float offsetGoal;
            public float scaleGoal;
        }

        private Slot[] slots = System.Array.Empty<Slot>();
        private Transform anchor;
        private int shown = -1;
        private float blend;
        private float blendVelocity;
        private Mode mode = Mode.Menu;
        private float swayTime;

        public Mode CurrentMode => mode;
        public int ShownIndex => shown;
        public int Count => slots.Length;
        public Transform Anchor => anchor;

        /// <summary>The model transform currently shown (tests / tools).</summary>
        public Transform ShownModel => shown >= 0 && shown < slots.Length ? slots[shown].model : null;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (anchor != null || catalog == null)
            {
                return;
            }

            if (stageCamera == null)
            {
                stageCamera = Camera.main;
            }

            anchor = new GameObject("StageAnchor").transform;
            anchor.SetParent(transform, false);
            if (pedestal != null)
            {
                pedestal.SetParent(anchor, false);
                pedestal.localPosition = Vector3.zero;
            }

            slots = new Slot[catalog.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                CharacterCatalog.Entry entry = catalog[i];
                var slot = new Slot { root = new GameObject("Slot_" + entry.id).transform, scale = 0f };
                slot.root.SetParent(anchor, false);
                if (entry.prefab != null)
                {
                    GameObject model = Instantiate(entry.prefab, slot.root);
                    model.name = "Miner_" + entry.id;
                    slot.model = model.transform;
                    slot.animator = model.GetComponentInChildren<MinerAnimator>(true);
                    if (slot.animator != null)
                    {
                        slot.animator.MenuPreview = true;
                        slot.animator.SetLampLightEnabled(true);
                    }
                }

                slot.root.gameObject.SetActive(false);
                slots[i] = slot;
            }

            ShowIndex(Mathf.Max(0, catalog.IndexOf(CharacterSelection.ResolveId(catalog))), 0, true);
            blend = mode == Mode.Select ? 1f : 0f;
            Layout(0f);
        }

        public void SetMode(Mode newMode)
        {
            mode = newMode;
        }

        /// <summary>Shows the saved miner (used when leaving the selection screen without picking).</summary>
        public void ShowSelected(int direction = 0)
        {
            ShowIndex(Mathf.Max(0, catalog != null ? catalog.IndexOf(CharacterSelection.ResolveId(catalog)) : 0), direction, false);
        }

        /// <param name="direction">+1 the new miner comes in from the right, -1 from the left, 0 instantly.</param>
        public void ShowIndex(int index, int direction, bool instant = false)
        {
            if (slots.Length == 0)
            {
                return;
            }

            index = ((index % slots.Length) + slots.Length) % slots.Length;
            if (index == shown && !instant)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (i == index)
                {
                    slot.root.gameObject.SetActive(true);
                    slot.offsetGoal = 0f;
                    slot.scaleGoal = 1f;
                    if (instant || direction == 0)
                    {
                        slot.offset = 0f;
                        slot.scale = 1f;
                    }
                    else
                    {
                        slot.offset = 1.9f * direction;
                        slot.scale = 0.55f;
                    }

                    slot.offsetVel = slot.scaleVel = 0f;
                }
                else
                {
                    slot.offsetGoal = -1.9f * (direction == 0 ? 1 : direction);
                    slot.scaleGoal = 0f;
                    if (instant || direction == 0)
                    {
                        slot.scale = 0f;
                        slot.root.gameObject.SetActive(false);
                    }
                }
            }

            shown = index;
        }

        private void LateUpdate()
        {
            if (anchor == null)
            {
                return;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            swayTime += dt;
            float goal = mode == Mode.Select ? 1f : 0f;
            blend = Mathf.SmoothDamp(blend, goal, ref blendVelocity, 0.22f, Mathf.Infinity, dt);
            StepSlots(dt);
            Layout(swayTime);
        }

        private void StepSlots(float dt)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (!slot.root.gameObject.activeSelf)
                {
                    continue;
                }

                bool isShown = i == shown;
                slot.offsetVel += (slot.offsetGoal - slot.offset) * (isShown ? 260f : 150f) * dt;
                slot.offsetVel *= Mathf.Exp(-(isShown ? 15f : 14f) * dt);
                slot.offset += slot.offsetVel * dt;
                slot.scaleVel += (slot.scaleGoal - slot.scale) * (isShown ? 300f : 160f) * dt;
                slot.scaleVel *= Mathf.Exp(-(isShown ? 14f : 15f) * dt);
                slot.scale = Mathf.Max(0f, slot.scale + slot.scaleVel * dt);
                slot.root.localPosition = new Vector3(slot.offset, 0f, 0f);
                slot.root.localScale = Vector3.one * slot.scale;
                if (!isShown && slot.scale < 0.03f)
                {
                    slot.root.gameObject.SetActive(false);
                }
            }
        }

        private void Layout(float time)
        {
            if (stageCamera == null)
            {
                return;
            }

            Vector2 viewport = Vector2.Lerp(menuAnchor, selectAnchor, Smooth(blend));
            float heightFraction = Mathf.Lerp(menuHeightFraction, selectHeightFraction, Smooth(blend));
            float frustumHeight = 2f * distanceFromCamera * Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float scale = Mathf.Max(0.01f, heightFraction * frustumHeight / modelHeight);

            Transform cam = stageCamera.transform;
            Vector3 world = stageCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, distanceFromCamera));
            anchor.position = world;
            anchor.localScale = Vector3.one * scale;

            // face the camera (billboard around Y), turned a little towards the menu; the selection screen sways like a turntable
            Vector3 toCamera = cam.position - world;
            toCamera.y = 0f;
            float baseYaw = toCamera.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toCamera, Vector3.up).eulerAngles.y : 0f;
            float yaw = Mathf.Lerp(menuYaw + Mathf.Sin(time * 0.6f) * 4f, Mathf.Sin(time * 0.9f) * selectYawSway, Smooth(blend));
            anchor.rotation = Quaternion.Euler(0f, baseYaw + yaw, 0f);
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
