using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools
{
    /// <summary>Sets the Game View to a fixed resolution (design review at several phone aspect ratios).</summary>
    public static class GameViewTools
    {
        /// <summary>Selects "Free Aspect" again and removes the IC3D_* review sizes (leaves the Game View as the user had it).</summary>
        public static string Reset()
        {
            Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            Type sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetProperty("currentGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(sizes); // the group the Game View really shows
            Type groupType = group.GetType();
            Type sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");

            EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
            gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(window, 0);

            int removed = 0;
            for (int i = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1; i >= 0; i--)
            {
                object s = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if (((string)sizeType.GetProperty("baseText").GetValue(s)).StartsWith("IC3D_"))
                {
                    groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { i });
                    removed++;
                }
            }

            window.Repaint();
            return "removed " + removed;
        }

        public static string SetResolution(int width, int height)
        {
            Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            Type sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetProperty("currentGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(sizes); // the group the Game View really shows
            Type groupType = group.GetType();

            Type sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            Type sizeKind = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            string label = $"IC3D_{width}x{height}";
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            var getSize = groupType.GetMethod("GetGameViewSize");
            for (int i = 0; i < total; i++)
            {
                object s = getSize.Invoke(group, new object[] { i });
                if ((string)sizeType.GetProperty("baseText").GetValue(s) == label) { index = i; break; }
            }

            if (index < 0)
            {
                object fixedKind = Enum.ToObject(sizeKind, 1);
                object size = Activator.CreateInstance(sizeType, new object[] { fixedKind, width, height, label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new object[] { size });
                index = total;
            }

            EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
            gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(window, index);
            window.Repaint();
            return label + " index=" + index;
        }
    }
}
