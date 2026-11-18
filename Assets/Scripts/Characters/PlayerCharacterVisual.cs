using UnityEngine;

namespace ItemCollector3D.Characters
{
    /// <summary>
    /// Puts the miner chosen in the menu into the Player's visual layer. It ONLY adds a model under the "Visual" child and hides
    /// the placeholder capsule renderer: the Player's controller, CharacterController, camera and dig system are untouched.
    /// Both miners share the same abilities, so the choice is cosmetic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCharacterVisual : MonoBehaviour
    {
        public const string ModelName = "MinerModel";

        [SerializeField] private CharacterCatalog catalog;
        [Tooltip("The player's 'Visual' child (the dig animation moves it). Falls back to a child named 'Visual'.")]
        [SerializeField] private Transform visual;
        [Tooltip("Local Y of the model's feet inside the Visual (the CharacterController is 2 m tall, centred on the player).")]
        [SerializeField] private float feetOffset = -1f;
        [SerializeField, Min(0.1f)] private float modelScale = 1f;

        public GameObject Model { get; private set; }
        public string AppliedId { get; private set; }

        private void Awake()
        {
            Apply();
        }

        public void Apply()
        {
            if (catalog == null || catalog.Count == 0)
            {
                return;
            }

            if (visual == null)
            {
                visual = transform.Find("Visual");
            }

            if (visual == null)
            {
                return;
            }

            string id = CharacterSelection.ResolveId(catalog);
            if (Model != null && AppliedId == id)
            {
                return;
            }

            CharacterCatalog.Entry entry = catalog.Find(id) ?? catalog[0];
            if (entry == null || entry.prefab == null)
            {
                return;
            }

            if (Model != null)
            {
                Destroy(Model);
            }

            foreach (Renderer placeholder in visual.GetComponents<Renderer>())
            {
                placeholder.enabled = false; // the capsule stays as an invisible, harmless stand-in
            }

            Model = Instantiate(entry.prefab, visual);
            Model.name = ModelName;
            Model.transform.localPosition = new Vector3(0f, feetOffset, 0f);
            Model.transform.localRotation = Quaternion.identity;
            Model.transform.localScale = Vector3.one * modelScale;
            foreach (MinerAnimator animator in Model.GetComponentsInChildren<MinerAnimator>(true))
            {
                animator.MenuPreview = false;
                animator.SetLampLightEnabled(false); // gameplay uses the emissive lens only (no extra realtime light)
            }

            AppliedId = entry.id;
        }
    }
}
