using Ishana.CameraSystem;
using Ishana.Combat;
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
    /// - BlightPostFX in scenes that use the grove cleansing system
    ///
    /// A scene only counts as a gameplay scene if it contains a PlayerStateManager, so menus are untouched.
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
            var player = Object.FindFirstObjectByType<PlayerStateManager>();
            if (player == null) return; // Not a gameplay scene

            if (player.GetComponent<ThirdPersonCameraController>() == null)
                player.gameObject.AddComponent<ThirdPersonCameraController>();

            if (Object.FindFirstObjectByType<CombatFeedback>() == null)
                new GameObject("[CombatFeedback]").AddComponent<CombatFeedback>();

            bool usesGroves = Object.FindFirstObjectByType<AreaCleansingManager>() != null;
            if (usesGroves && Object.FindFirstObjectByType<BlightPostFX>() == null)
                new GameObject("[BlightPostFX]").AddComponent<BlightPostFX>();
        }
    }
}
