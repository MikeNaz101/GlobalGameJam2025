using Ishana.AI;
using Ishana.CameraSystem;
using Ishana.Combat;
using Ishana.Saving;
using Ishana.UI;
using Ishana.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ishana.Core
{
    /// <summary>
    /// Installs the feel upgrade into any gameplay scene without hand-editing scenes or prefabs:
    ///
    /// - ThirdPersonCameraController on the player (if the prefab doesn't already have one)
    /// - CombatFeedback service (hit-stop, shake, hit flash, damage numbers)
    /// - RuntimeNavMesh (pathfinding for enemies)
    /// - In scenes that use the grove cleansing system: BlightPostFX, GroveRewards, ObjectiveHUD, SaveManager
    /// - MainMenuSaveOptions (Continue / New Game) in the start menu
    ///
    /// A scene only counts as a gameplay scene if it contains a PlayerStateManager.
    /// If you add any of these components to your prefabs or scenes yourself, those copies are used
    /// (and their Inspector values respected) instead of the defaults.
    /// </summary>
    public static class IshanaBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

        static void Install()
        {
            // Start menu: Continue / New Game when a save exists
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            if (menu != null && menu.GetComponent<MainMenuSaveOptions>() == null
                && Object.FindFirstObjectByType<MainMenuSaveOptions>() == null)
                menu.gameObject.AddComponent<MainMenuSaveOptions>();

            var player = Object.FindFirstObjectByType<PlayerStateManager>();
            if (player == null) return; // Not a gameplay scene

            if (player.GetComponent<ThirdPersonCameraController>() == null)
                player.gameObject.AddComponent<ThirdPersonCameraController>();

            Ensure<CombatFeedback>("[CombatFeedback]");
            Ensure<RuntimeNavMesh>("[RuntimeNavMesh]");

            bool usesGroves = Object.FindFirstObjectByType<AreaCleansingManager>() != null;
            if (usesGroves)
            {
                Ensure<BlightPostFX>("[BlightPostFX]");
                Ensure<GroveRewards>("[GroveRewards]");
                Ensure<ObjectiveHUD>("[ObjectiveHUD]");
                Ensure<SaveManager>("[SaveManager]"); // After the HUD, so load can refresh it
            }
        }

        static void Ensure<T>(string name) where T : Component
        {
            if (Object.FindFirstObjectByType<T>() == null) new GameObject(name).AddComponent<T>();
        }
    }
}
