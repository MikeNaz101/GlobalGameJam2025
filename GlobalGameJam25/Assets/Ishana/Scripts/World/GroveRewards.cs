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

        PlayerStateManager _player;

        void OnEnable() => AreaCleansingManager.OnAreaCleared += HandleAreaCleared;
        void OnDisable() => AreaCleansingManager.OnAreaCleared -= HandleAreaCleared;

        void HandleAreaCleared(AreaCleansingManager grove)
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerStateManager>();
            if (_player == null || _player.currentHealth <= 0) return;

            if (refillStats) _player.RestoreAllStats();
            if (bonusXP > 0) _player.GainXP(bonusXP);
        }
    }
}
