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
        [Tooltip("Grant the earth shot (regrows burnt plants) once this many groves are cleansed. " +
                 "It is only granted after the shots before it (freeze, teleport) are unlocked, so their pickups still matter. " +
                 "Set to -1 to never grant it here.")]
        public int earthShotAfterGroves = 1;
        const int EarthShotIndex = 3;
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
            // Cheap periodic check, so it also catches loading a save and picking up the teleport shot later
            if (earthShotAfterGroves < 0 || Time.time < _nextEarthCheck) return;
            _nextEarthCheck = Time.time + 2f;

            if (_player == null) _player = FindFirstObjectByType<PlayerStateManager>();
            if (_player == null) return;
            if (_player.MaxUnlockedBulletIndex >= EarthShotIndex) { earthShotAfterGroves = -1; return; }
            if (_player.MaxUnlockedBulletIndex < EarthShotIndex - 1) return;

            int cleansed = 0;
            foreach (var grove in FindObjectsByType<AreaCleansingManager>(FindObjectsSortMode.None))
                if (grove != null && grove.IsCleared) cleansed++;
            if (cleansed < earthShotAfterGroves) return;

            if (_player.UnlockBulletType(EarthShotIndex))
                Ishana.UI.ObjectiveHUD.Toast("Earth shot unlocked!  It regrows burnt plants");
        }
    }
}
