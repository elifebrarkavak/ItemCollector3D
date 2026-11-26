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
        public const string AncientMineScene = "AncientMine"; // the third area (Area Selection -> ANCIENT MINE, opens at 1200 total gold)

        /// <summary>Test hook: when set it receives the scene name instead of a real scene load.</summary>
        public static Action<string> LoadHook;

        public static string LastRequestedScene { get; private set; }

        /// <summary>
        /// Set by <see cref="LoadAreaSelection"/>: the Main Menu opens its Area Selection as soon as it starts (stage 17C, LEVEL COMPLETE -> AREA SELECTION). Any other <see cref="Load"/> clears it,
        /// so a request that never reached the menu cannot open the Area Selection by surprise later.
        /// </summary>
        public static bool AreaSelectionRequested { get; private set; }

        /// <summary>The scene of an area id (the ids of the AreaCatalog), or null for an area that has no map.</summary>
        public static string SceneForArea(string areaId)
        {
            return areaId switch
            {
                "green_mine" => GreenMineScene,
                "rocky_canyon" => RockyCanyonScene,
                "ancient_mine" => AncientMineScene,
                _ => null,
            };
        }

        public static void Load(string sceneName)
        {
            Load(sceneName, false);
        }

        /// <summary>Back to the Main Menu with its Area Selection already open (the open areas, the total gold and the chosen miner are read from the save and the settings as always).</summary>
        public static void LoadAreaSelection()
        {
            Load(MainMenuScene, true);
        }

        /// <summary>Called by the Main Menu when it starts: true once if the Area Selection was requested.</summary>
        public static bool ConsumeAreaSelectionRequest()
        {
            bool requested = AreaSelectionRequested;
            AreaSelectionRequested = false;
            return requested;
        }

        private static void Load(string sceneName, bool openAreaSelection)
        {
            GamePause.Resume();
            LastRequestedScene = sceneName;
            AreaSelectionRequested = openAreaSelection;
            if (LoadHook != null)
            {
                LoadHook(sceneName);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
