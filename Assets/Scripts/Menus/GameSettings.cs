using System;
using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// Player settings: Music volume, SFX volume and Camera sensitivity, stored through an <see cref="ISettingsStore"/>
    /// (PlayerPrefs on the device). Values are always clamped; missing, NaN or out-of-range stored values fall back to safe ones.
    /// The game systems read them live: SFX players scale their volume, the camera scales its orbit, the music player follows.
    /// </summary>
    public sealed class GameSettings
    {
        public const string MusicKey = "music_volume";
        public const string SfxKey = "sfx_volume";
        public const string CameraKey = "camera_sensitivity";

        public const float DefaultMusic = 0.8f;
        public const float DefaultSfx = 1f;
        public const float DefaultCameraSensitivity = 1f;
        public const float MinCameraSensitivity = 0.25f;
        public const float MaxCameraSensitivity = 2f;

        private readonly ISettingsStore store;

        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }
        public float CameraSensitivity { get; private set; }

        /// <summary>Raised after any setting changed.</summary>
        public event Action Changed;

        public GameSettings(ISettingsStore settingsStore)
        {
            store = settingsStore ?? new MemorySettingsStore();
            MusicVolume = Load(MusicKey, DefaultMusic, 0f, 1f);
            SfxVolume = Load(SfxKey, DefaultSfx, 0f, 1f);
            CameraSensitivity = Load(CameraKey, DefaultCameraSensitivity, MinCameraSensitivity, MaxCameraSensitivity);
        }

        // ------------------------------------------------------------------ shared instance

        /// <summary>Test / tool hook: when set, the shared instance uses this store instead of PlayerPrefs.</summary>
        public static Func<ISettingsStore> StoreFactoryOverride;

        private static GameSettings current;

        /// <summary>The settings every system reads (created on first use).</summary>
        public static GameSettings Current =>
            current ??= new GameSettings(StoreFactoryOverride != null ? StoreFactoryOverride() : new PlayerPrefsSettingsStore());

        /// <summary>Drops the shared instance so the next access reloads from the store (tests / tools).</summary>
        public static void ResetCurrent()
        {
            current = null;
        }

        /// <summary>Multiplier for sound-effect volumes (collect, dig, detector).</summary>
        public static float SfxScale => Current.SfxVolume;

        /// <summary>Multiplier for the camera orbit speed.</summary>
        public static float CameraSensitivityScale => Current.CameraSensitivity;

        // ------------------------------------------------------------------ setters

        public void SetMusicVolume(float value)
        {
            Apply(MusicKey, ref value, 0f, 1f, DefaultMusic, v => MusicVolume = v, MusicVolume);
        }

        public void SetSfxVolume(float value)
        {
            Apply(SfxKey, ref value, 0f, 1f, DefaultSfx, v => SfxVolume = v, SfxVolume);
        }

        public void SetCameraSensitivity(float value)
        {
            Apply(CameraKey, ref value, MinCameraSensitivity, MaxCameraSensitivity, DefaultCameraSensitivity,
                v => CameraSensitivity = v, CameraSensitivity);
        }

        public void ResetToDefaults()
        {
            SetMusicVolume(DefaultMusic);
            SetSfxVolume(DefaultSfx);
            SetCameraSensitivity(DefaultCameraSensitivity);
        }

        private void Apply(string key, ref float value, float min, float max, float fallback, Action<float> assign, float old)
        {
            float clean = Sanitize(value, min, max, fallback);
            if (Mathf.Approximately(clean, old))
            {
                return;
            }

            assign(clean);
            store.SetFloat(key, clean);
            store.Save();
            Changed?.Invoke();
        }

        private float Load(string key, float fallback, float min, float max)
        {
            return store.TryGetFloat(key, out float stored) ? Sanitize(stored, min, max, fallback) : fallback;
        }

        private static float Sanitize(float value, float min, float max, float fallback)
        {
            return float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
        }
    }
}
