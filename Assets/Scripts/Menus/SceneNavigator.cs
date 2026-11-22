using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ItemCollector3D.Menus
{
    /// <summary>Scene changes for the menus. Always resumes a paused game first so the next scene never starts frozen.</summary>
    public static class SceneNavigator
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameplayScene = "GameplayTest"; // the original test scene, kept as a sandbox
        public const string GreenMineScene = "GreenMine";   // the first real area (Area Selection -> GREEN MINE)
        public const string RockyCanyonScene = "RockyCanyon"; // the second area (Area Selection -> ROCKY CANYON, opens at 500 total gold)

        /// <summary>Test hook: when set it receives the scene name instead of a real scene load.</summary>
        public static Action<string> LoadHook;

        public static string LastRequestedScene { get; private set; }

        public static void Load(string sceneName)
        {
            GamePause.Resume();
            LastRequestedScene = sceneName;
            if (LoadHook != null)
            {
                LoadHook(sceneName);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
