#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>Design review helper: renders the OPEN scene from any camera pose into a PNG with a temporary camera (the same URP pipeline as the game camera). Changes nothing in the scene.</summary>
    public static class RcShots
    {
        public static string Shot(string path, Vector3 position, Vector3 lookAt, float fov = 60f, int width = 1280, int height = 720)
        {
            var go = new GameObject("RcShotCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.Skybox;
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

        /// <summary>Third-person style shot: the camera stands <paramref name="distance"/> m behind a point (x, z) at the given yaw (degrees, 0 = looking north), 20 degrees down, like the game camera.</summary>
        public static string Orbit(string path, float x, float z, float yaw, float distance = 5f, float pitch = 20f, float fov = 60f, float yOverride = float.NaN, int width = 1280, int height = 720)
        {
            float ground = float.IsNaN(yOverride) ? RcLayout.Surface(x, z) : yOverride;
            Vector3 pivot = new Vector3(x, ground + 1.0f + 1.4f, z);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pos = pivot + rot * Vector3.back * distance;
            return Shot(path, pos, pivot, fov, width, height);
        }
    }
}
#endif
