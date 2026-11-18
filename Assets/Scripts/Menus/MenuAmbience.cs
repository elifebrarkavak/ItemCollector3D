using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// Cheap life for the cave menu scene: warm lights flicker softly and the gold crystals pulse their glow. Only light intensity
    /// and one emission colour per material are written; nothing is allocated per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuAmbience : MonoBehaviour
    {
        [SerializeField] private Light[] flickerLights = System.Array.Empty<Light>();
        [SerializeField, Range(0f, 0.5f)] private float flickerAmount = 0.12f;
        [SerializeField] private Renderer[] pulseRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Color[] pulseColors = System.Array.Empty<Color>();
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.35f;

        private float[] baseIntensity;
        private MaterialPropertyBlock block;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            baseIntensity = new float[flickerLights.Length];
            for (int i = 0; i < flickerLights.Length; i++)
            {
                baseIntensity[i] = flickerLights[i] != null ? flickerLights[i].intensity : 0f;
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < flickerLights.Length; i++)
            {
                if (flickerLights[i] != null)
                {
                    float n = Mathf.PerlinNoise(t * 1.7f + i * 13.1f, i * 3.7f) - 0.5f;
                    flickerLights[i].intensity = baseIntensity[i] * (1f + n * 2f * flickerAmount);
                }
            }

            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < pulseRenderers.Length; i++)
            {
                if (pulseRenderers[i] != null && pulseColors.Length > 0)
                {
                    float phase = 1f + Mathf.Sin(t * 1.6f + i * 0.9f) * pulseAmount;
                    block.SetColor(EmissionColor, pulseColors[i % pulseColors.Length] * phase);
                    pulseRenderers[i].SetPropertyBlock(block);
                }
            }
        }
    }
}
