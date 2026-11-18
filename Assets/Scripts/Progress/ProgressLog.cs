using System;
using UnityEngine;

namespace ItemCollector3D.Progress
{
    /// <summary>
    /// Single place for progress / save diagnostics (recovered saves, duplicate treasure ids, I/O problems). Goes to the Unity
    /// Console by default; tests replace <see cref="WarningSink"/> to capture and assert the warnings instead of printing them.
    /// </summary>
    public static class ProgressLog
    {
        /// <summary>When null the warning is written with Debug.LogWarning.</summary>
        public static Action<string> WarningSink;

        public static void Warning(string message)
        {
            if (WarningSink != null)
            {
                WarningSink(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }
    }
}
