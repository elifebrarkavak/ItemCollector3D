using System;
using System.Collections;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestTools;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Gives ONE test a realistic frame pace (vSync off, a <see cref="TargetFps"/> cap, both restored afterwards). Put it only on tests that walk
    /// the Player through the real frame loop (keyboard, joystick or <c>SetMoveInput</c>) and then assert distances.
    ///
    /// Why: in batch mode no display paces the loop, so the small test scenes run at about 10,000 fps (frame time about 0.1 ms).
    /// <c>CharacterController.Move</c> silently discards any move shorter than <c>minMoveDistance</c> (1 mm) and then does not refresh
    /// <c>isGrounded</c>; at 5 m/s one 0.1 ms frame is only 0.5 mm, so the Player barely moved (measured: 0.69 m instead of 10 m in 2 s, never grounded).
    /// The game itself is frame-rate independent across real frame rates (measured: 5.0 m/s at caps of 30, 60, 144 and 240 fps), and no device runs
    /// at thousands of fps, so the fix belongs in the test environment, not in the movement code. Tests that do not move the Player through input are
    /// deliberately not paced, and nothing is forced for the whole suite.
    ///
    /// The attribute checks that the cap really took effect and fails with an explanation otherwise, so a future environment that ignores it
    /// cannot bring back the confusing "player did not move" failures.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RealisticFrameRateAttribute : NUnitAttribute, IOuterUnityTestAction
    {
        public const int TargetFps = 60;

        private const int WarmUpFrames = 3;
        private const int MeasuredFrames = 10;
        private const float MinimumAverageFrameSeconds = 0.004f; // anything faster than 250 fps is not a realistic pace (a 60 fps cap gives 0.0167 s)

        private static int previousVSyncCount;
        private static int previousTargetFrameRate;

        public IEnumerator BeforeTest(ITest test)
        {
            previousVSyncCount = QualitySettings.vSyncCount;
            previousTargetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0; // targetFrameRate is ignored while vSync is on
            Application.targetFrameRate = TargetFps;

            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            float start = Time.realtimeSinceStartup;
            for (int i = 0; i < MeasuredFrames; i++)
            {
                yield return null;
            }

            float average = (Time.realtimeSinceStartup - start) / MeasuredFrames;
            if (average < MinimumAverageFrameSeconds)
            {
                Restore();
                throw new AssertionException(
                    $"{test.Name}: the {TargetFps} fps cap was not applied (average frame time {average * 1000f:F2} ms; batch mode with -nographics has no frame limiter). Without a realistic frame pace " +
                    "CharacterController.Move discards motions below minMoveDistance (1 mm) and the Player does not move, so this test would be meaningless.");
            }
        }

        public IEnumerator AfterTest(ITest test)
        {
            Restore();
            yield return null;
        }

        private static void Restore()
        {
            QualitySettings.vSyncCount = previousVSyncCount;
            Application.targetFrameRate = previousTargetFrameRate;
        }
    }
}
