using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// Gives the Green Mine river and waterfall their motion without any particles, shaders or new materials: it only scrolls the existing water textures
    /// by writing the tiling/offset (<c>_BaseMap_ST</c>) of each water renderer through a MaterialPropertyBlock, so the shared materials stay untouched and
    /// nothing is allocated per frame. The art lives under GreenMineEnvironment/Water and holds no scripts, so this component sits under GameplaySystems and
    /// points at the renderers. Time is the scaled game time, so the water stands still while the game is paused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterMotion : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Flow
        {
            [Tooltip("The water renderer (its mesh keeps its own UVs, materials are shared).")]
            public Renderer target;

            [Tooltip("How often the texture repeats over the mesh UV range (x along u, y along v).")]
            public Vector2 tiling = Vector2.one;

            [Tooltip("Texture repeats per second the pattern travels (negative u or positive v moves the pattern towards smaller u / v: the flow direction).")]
            public Vector2 scrollPerSecond;

            [Tooltip("Gentle sideways / lengthways sway added on top of the flow, in texture repeats (stops the flow from looking like a conveyor belt).")]
            public Vector2 swayAmplitude;

            [Tooltip("Speed of that sway in radians per second.")]
            [Min(0f)] public float swayRadiansPerSecond = 0.8f;
        }

        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        [SerializeField] private Flow[] flows = System.Array.Empty<Flow>();

        private MaterialPropertyBlock block;
        private Vector4[] lastApplied = System.Array.Empty<Vector4>();

        /// <summary>Number of water renderers this component animates.</summary>
        public int FlowCount => flows.Length;

        /// <summary>The tiling (xy) and offset (zw) last written to the renderer of flow <paramref name="index"/>.</summary>
        public Vector4 AppliedTilingOffset(int index) => index >= 0 && index < lastApplied.Length ? lastApplied[index] : new Vector4(1f, 1f, 0f, 0f);

        private void OnEnable()
        {
            block ??= new MaterialPropertyBlock();
            if (lastApplied.Length != flows.Length)
            {
                lastApplied = new Vector4[flows.Length];
            }

            Apply(Time.time);
        }

        private void OnDisable()
        {
            // give the renderers back to their plain shared material
            foreach (Flow flow in flows)
            {
                if (flow != null && flow.target != null)
                {
                    flow.target.SetPropertyBlock(null);
                }
            }
        }

        private void Update()
        {
            Apply(Time.time);
        }

        private void Apply(float time)
        {
            for (int i = 0; i < flows.Length; i++)
            {
                Flow flow = flows[i];
                if (flow == null || flow.target == null)
                {
                    continue;
                }

                float sway = Mathf.Sin(time * flow.swayRadiansPerSecond);
                // wrap the travelled distance so the offset stays small and precise during a long session
                float ox = Mathf.Repeat(time * flow.scrollPerSecond.x, 1f) + flow.swayAmplitude.x * sway;
                float oy = Mathf.Repeat(time * flow.scrollPerSecond.y, 1f) + flow.swayAmplitude.y * Mathf.Cos(time * flow.swayRadiansPerSecond * 0.7f);
                var st = new Vector4(flow.tiling.x, flow.tiling.y, ox, oy);
                lastApplied[i] = st;

                flow.target.GetPropertyBlock(block);
                block.SetVector(BaseMapSt, st);
                flow.target.SetPropertyBlock(block);
            }
        }
    }
}
