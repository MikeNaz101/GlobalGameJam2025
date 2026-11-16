using UnityEngine;

namespace Ishana.World
{
    /// <summary>
    /// Rewards the player for cleansing a grove: health, mana and stamina are refilled and bonus XP is
    /// granted. Gives each grove a clear payoff and a breather before the next one.
    /// Created automatically by IshanaBootstrap.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Grove Rewards")]
    public class GroveRewards : MonoBehaviour
    {
        [Tooltip("Refill health, mana and stamina when a grove is cleansed.")]
        public bool refillStats = true;
        [Tooltip("Bonus XP for cleansing a grove.")]
        public int bonusXP = 50;

        [Header("Earth Magic")]
        [Tooltip("Grant earth magic once this many groves are cleansed (1 = after the first group of enemies is defeated). " +
                 "It is separate from the freeze and teleport pickups. Set to -1 to never grant it here.")]
        public int earthShotAfterGroves = 1;
        float _nextEarthCheck;

        PlayerStateManager _player;

        void OnEnable() => AreaCleansingManager.OnAreaCleared += HandleAreaCleared;
        void OnDisable() => AreaCleansingManager.OnAreaCleared -= HandleAreaCleared;

        void HandleAreaCleared(AreaCleansingManager grove)
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerStateManager>();
            if (_player == null || _player.currentHealth <= 0) return;

            if (refillStats) _player.RestoreAllStats();
            if (bonusXP > 0) _player.GainXP(bonusXP);
            _nextEarthCheck = 0f;
        }

        void Update()
        {
            // Cheap periodic check, so it also catches loading a save where groves are already cleansed
            if (earthShotAfterGroves < 0 || Time.time < _nextEarthCheck) return;
            _nextEarthCheck = Time.time + 2f;

            if (_player == null) _player = FindFirstObjectByType<PlayerStateManager>();
            if (_player == null) return;
            if (_player.EarthUnlocked) return;

            int cleansed = 0;
            foreach (var grove in FindObjectsByType<AreaCleansingManager>(FindObjectsSortMode.None))
                if (grove != null && grove.IsCleared) cleansed++;
            if (cleansed < earthShotAfterGroves) return;

            if (_player.UnlockEarth())
            {
                Ishana.UI.ObjectiveHUD.Toast("Earth magic unlocked!  Scroll to switch shots");
                if (Ishana.Saving.SaveManager.Instance != null) Ishana.Saving.SaveManager.Instance.RequestSave(false, false);
            }
        }
    }
}
