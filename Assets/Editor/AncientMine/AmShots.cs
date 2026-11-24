#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>Design review helper: renders the OPEN scene from any camera pose into a PNG with a temporary camera (the same URP pipeline as the game camera). Changes nothing in the scene.</summary>
    public static class AmShots
    {
        public static string Shot(string path, Vector3 position, Vector3 lookAt, float fov = 60f, int width = 1280, int height = 720)
        {
            var go = new GameObject("AmShotCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 280f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.045f, 0.12f);
                go.transform.position = position;
                go.transform.rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);
                var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return path;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Third-person style shot: the camera stands <paramref name="distance"/> m behind a point (x, z) at the given yaw (degrees, 0 = looking north), like the game camera.</summary>
        public static string Orbit(string path, float x, float z, float yaw, float distance = 5f, float pitch = 18f, float fov = 60f, float yOverride = float.NaN, int width = 1280, int height = 720)
        {
            float ground = float.IsNaN(yOverride) ? AmLayout.Surface(x, z) : yOverride;
            Vector3 pivot = new Vector3(x, ground + 1.0f + 1.4f, z);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pos = pivot + rot * Vector3.back * distance;
            return Shot(path, pos, pivot, fov, width, height);
        }

        /// <summary>A wide look from a free pose: <paramref name="from"/> and <paramref name="to"/> are (x, z) positions; heights are the floor plus the given eye height.</summary>
        public static string Look(string path, Vector2 from, Vector2 to, float eye = 3.5f, float targetHeight = 6f, float fov = 70f, int width = 1280, int height = 720)
        {
            return Shot(path, new Vector3(from.x, AmLayout.Surface(from.x, from.y) + eye, from.y), new Vector3(to.x, AmLayout.Surface(to.x, to.y) + targetHeight, to.y), fov, width, height);
        }
    }
}
#endif
