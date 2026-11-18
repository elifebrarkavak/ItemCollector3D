#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Puts the Game View at exact resolutions for one test, so layout tests do not depend on whatever size the Game View happens to have
    /// (the open Editor: free aspect; batch mode: no Game View at all and <c>Screen</c> is 640x480). <see cref="Restore"/> puts everything back: the
    /// selected size (in batch mode the original <c>Screen</c> size), the custom sizes added here and, when this class had to create the Game View window
    /// (batch mode), the window itself.
    /// Uses the same internal Game View API as <c>ItemCollector3D.EditorTools.GameViewTools</c>; it needs no public equivalent because
    /// <c>PlayModeWindow</c> can set a custom resolution but never restore the previous selection.
    /// </summary>
    public sealed class TestGameView
    {
        private const string SizeLabelPrefix = "IC3D_TEST_";
        private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Assembly EditorAssembly = typeof(Editor).Assembly;
        private static readonly Type GameViewType = EditorAssembly.GetType("UnityEditor.GameView");
        private static readonly Type SizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
        private static readonly Type SizeType = EditorAssembly.GetType("UnityEditor.GameViewSize");
        private static readonly Type SizeKindType = EditorAssembly.GetType("UnityEditor.GameViewSizeType");

        private readonly EditorWindow window;
        private readonly bool createdWindow;
        private readonly int previousIndex;
        private readonly Vector2Int previousScreen;
        private readonly object group;

        public TestGameView()
        {
            // A Game View window cannot exist without a graphics device (batch mode with -nographics): fail with the reason, not with an engine error.
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "Setting the Game View resolution needs a graphics device: do not run this test with -nographics.");
            previousScreen = new Vector2Int(Screen.width, Screen.height);
            createdWindow = Resources.FindObjectsOfTypeAll(GameViewType).Length == 0;
            window = EditorWindow.GetWindow(GameViewType, false, null, false);
            previousIndex = SelectedIndex;

            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(SizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            group = SizesType.GetProperty("currentGroup", AnyMember).GetValue(sizes); // the group of the active build target
        }

        private int SelectedIndex
        {
            get => (int)GameViewType.GetProperty("selectedSizeIndex", AnyMember).GetValue(window);
            set => GameViewType.GetProperty("selectedSizeIndex", AnyMember).SetValue(window, value);
        }

        /// <summary>Selects a fixed width x height. <c>Screen.width/height</c> follow within a frame, so wait a frame or two before measuring.</summary>
        public void SetResolution(int width, int height)
        {
            string label = $"{SizeLabelPrefix}{width}x{height}";
            int index = IndexOf(label);
            if (index < 0)
            {
                object fixedResolution = Enum.ToObject(SizeKindType, 1);
                object size = Activator.CreateInstance(SizeType, fixedResolution, width, height, label);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = IndexOf(label);
            }

            SelectedIndex = index;
        }

        /// <summary>
        /// Puts everything back. A coroutine, because <c>Screen</c> only follows a selection one frame later: call it from the test's TearDown
        /// (<c>yield return gameView.Restore()</c>) so it also runs when an assertion failed.
        /// </summary>
        public IEnumerator Restore()
        {
            if (window == null)
            {
                yield break;
            }

            if (createdWindow)
            {
                // Batch mode: there was no Game View, so there is no size to select again, and Screen keeps the last size a closed window had.
                // Hand it its original size while the window still exists.
                SetResolution(previousScreen.x, previousScreen.y);
            }
            else
            {
                SelectedIndex = previousIndex;
            }

            yield return null;
            yield return null;

            Type groupType = group.GetType();
            for (int i = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1; i >= 0; i--)
            {
                if (LabelAt(i).StartsWith(SizeLabelPrefix, StringComparison.Ordinal))
                {
                    groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { i });
                }
            }

            if (createdWindow)
            {
                window.Close();
            }
        }

        private int IndexOf(string label)
        {
            int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            for (int i = 0; i < total; i++)
            {
                if (LabelAt(i) == label)
                {
                    return i;
                }
            }

            return -1;
        }

        private string LabelAt(int index)
        {
            object size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
            return (string)SizeType.GetProperty("baseText").GetValue(size);
        }
    }
}
#endif
