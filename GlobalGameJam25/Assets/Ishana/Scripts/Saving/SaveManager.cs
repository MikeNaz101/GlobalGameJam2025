using System.Collections;
using System.Collections.Generic;
using Ishana.CameraSystem;
using Ishana.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ishana.Saving
{
    /// <summary>
    /// Autosave for the gameplay scene. Created automatically by IshanaBootstrap.
    ///
    /// Saved: level, XP, damage multiplier, unlocked abilities, cleansed groves, opened chests,
    /// broken boulders, tutorial messages already shown, and a checkpoint position.
    ///
    /// The game saves when a grove is cleansed, an ability is unlocked, a chest is opened or the player
    /// levels up, plus a quiet checkpoint every so often while the player is safe. On load (including
    /// "Restart" after dying) the player comes back at the last checkpoint with their progress.
    ///
    /// A grove that was only partly cleared is not saved: its creatures come back.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Save Manager")]
    public class SaveManager : MonoBehaviour
    {
        [Tooltip("Load the save file when the scene starts.")]
        public bool loadOnStart = true;
        [Tooltip("Seconds between quiet checkpoint saves while the player is safe. 0 turns them off.")]
        public float checkpointInterval = 30f;
        [Tooltip("Show a small 'Progress saved' message for event saves.")]
        public bool showSaveMessage = true;

        public static SaveManager Instance { get; private set; }

        PlayerStateManager _player;
        GlobalAmbianceController _ambiance;
        SaveData _data = new SaveData();
        bool _ready, _dirty;
        float _nextWriteTime, _nextCheckpointTime, _sessionStart;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnEnable()
        {
            AreaCleansingManager.OnAreaCleared += HandleAreaCleared;
            ChestInteraction.OnChestOpened += HandleChestOpened;
            BreakableBoulder.OnBoulderBroken += HandleBoulderBroken;
            BulletUnlockTrigger.OnAbilityUnlocked += HandleAbilityUnlocked;
            TriggerEvent.OnFired += HandleTriggerFired;
            PlayerStateManager.OnLevelChanged += HandleLevelChanged;
        }

        void OnDisable()
        {
            AreaCleansingManager.OnAreaCleared -= HandleAreaCleared;
            ChestInteraction.OnChestOpened -= HandleChestOpened;
            BreakableBoulder.OnBoulderBroken -= HandleBoulderBroken;
            BulletUnlockTrigger.OnAbilityUnlocked -= HandleAbilityUnlocked;
            TriggerEvent.OnFired -= HandleTriggerFired;
            PlayerStateManager.OnLevelChanged -= HandleLevelChanged;

            if (_ready && _dirty) WriteNow();
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            // Wait a frame so every other Start() (player stats, HUD bars, spawner) has run first
            yield return null;

            _player = FindFirstObjectByType<PlayerStateManager>();
            _ambiance = FindFirstObjectByType<GlobalAmbianceController>();
            _sessionStart = Time.unscaledTime;
            _nextCheckpointTime = Time.time + checkpointInterval;

            if (_player == null) yield break;

            string sceneName = SceneManager.GetActiveScene().name;
            SaveData loaded = loadOnStart ? SaveSystem.Load() : null;
            if (loaded != null && loaded.scene == sceneName)
            {
                _data = loaded;
                ApplyToWorld();
            }
            else
            {
                _data = new SaveData { scene = sceneName };
            }

            _ready = true;
        }

        // ------------------------------------------------------------------ load

        void ApplyToWorld()
        {
            _player.ApplySavedProgress(_data.level, _data.xp, _data.effectMultiplier, _data.unlockedBulletIndex);
            if (_data.earthUnlocked) _player.UnlockEarth();

            // Groves
            var cleared = new HashSet<string>(_data.clearedGroves);
            var spawner = FindFirstObjectByType<EnemySpawner>();
            foreach (var grove in FindObjectsByType<AreaCleansingManager>(FindObjectsSortMode.None))
            {
                if (!cleared.Contains(SaveKeys.For(grove))) continue;
                grove.RestoreCleared();
                if (spawner == null) continue;
                foreach (var area in spawner.spawnAreas)
                    if (area != null && area.areaManager == grove) area.enemiesHaveSpawned = true;
            }

            // One-off world objects
            var chests = new HashSet<string>(_data.openedChests);
            foreach (var chest in FindObjectsByType<ChestInteraction>(FindObjectsSortMode.None))
                if (chests.Contains(SaveKeys.For(chest))) Destroy(chest.gameObject);

            var boulders = new HashSet<string>(_data.brokenBoulders);
            foreach (var boulder in FindObjectsByType<BreakableBoulder>(FindObjectsSortMode.None))
                if (boulders.Contains(SaveKeys.For(boulder))) Destroy(boulder.gameObject);

            var tutorials = new HashSet<string>(_data.firedTutorialTriggers);
            foreach (var trigger in FindObjectsByType<TriggerEvent>(FindObjectsSortMode.None))
                if (tutorials.Contains(SaveKeys.For(trigger))) trigger.MarkAlreadyTriggered();

            // Checkpoint
            if (_data.hasCheckpoint)
            {
                _player.TeleportTo(_data.checkpointPosition + Vector3.up * 0.25f, _data.checkpointYaw);
                var cam = _player.GetComponent<ThirdPersonCameraController>();
                if (cam != null) cam.SnapBehind(_player.transform.forward);
            }

            ObjectiveHUD.Refresh();
        }

        // ------------------------------------------------------------------ events

        void HandleAreaCleared(AreaCleansingManager grove)
        {
            if (!_ready) return;
            AddOnce(_data.clearedGroves, SaveKeys.For(grove));
            RequestSave(checkpoint: true, announce: true);
        }

        void HandleChestOpened(ChestInteraction chest)
        {
            if (!_ready) return;
            AddOnce(_data.openedChests, SaveKeys.For(chest));
            RequestSave(checkpoint: true, announce: false);
        }

        void HandleBoulderBroken(BreakableBoulder boulder)
        {
            if (!_ready) return;
            AddOnce(_data.brokenBoulders, SaveKeys.For(boulder));
            RequestSave(checkpoint: false, announce: false);
        }

        void HandleAbilityUnlocked(BulletUnlockTrigger trigger)
        {
            if (!_ready) return;
            RequestSave(checkpoint: true, announce: true);
        }

        void HandleTriggerFired(TriggerEvent trigger)
        {
            if (!_ready || trigger == null || string.IsNullOrEmpty(trigger.eventName)) return;
            if (!trigger.triggerOnce || !trigger.eventName.StartsWith("Tutorial")) return;
            AddOnce(_data.firedTutorialTriggers, SaveKeys.For(trigger));
            RequestSave(checkpoint: false, announce: false);
        }

        void HandleLevelChanged(int level)
        {
            if (!_ready) return; // Also fires once from the player's Start(), before we're ready
            RequestSave(checkpoint: false, announce: false);
        }

        static void AddOnce(List<string> list, string key)
        {
            if (!string.IsNullOrEmpty(key) && !list.Contains(key)) list.Add(key);
        }

        // ------------------------------------------------------------------ save

        /// <summary>Queue a save. Writes are batched to at most one per second.</summary>
        public void RequestSave(bool checkpoint, bool announce)
        {
            if (!_ready || _player == null) return;
            if (checkpoint) TryCaptureCheckpoint();
            _dirty = true;
            if (announce && showSaveMessage) ObjectiveHUD.Toast("Progress saved");
        }

        void Update()
        {
            if (!_ready || _player == null) return;

            if (checkpointInterval > 0f && Time.time >= _nextCheckpointTime)
            {
                _nextCheckpointTime = Time.time + checkpointInterval;
                if (PlayerIsSafe() && TryCaptureCheckpoint()) _dirty = true;
            }

            if (_dirty && Time.unscaledTime >= _nextWriteTime) WriteNow();
        }

        bool PlayerIsSafe()
        {
            if (_player.currentHealth <= 0 || !_player.isGrounded) return false;
            if (_player.currentState == _player.hitState) return false;
            // Not in the middle of a fight: either outside every grove, or in one that's already cleansed
            var area = _ambiance != null ? _ambiance.ActiveArea : null;
            return area == null || area.IsCleared;
        }

        bool TryCaptureCheckpoint()
        {
            if (_player.currentHealth <= 0 || !_player.isGrounded) return false;
            _data.hasCheckpoint = true;
            _data.checkpointPosition = _player.transform.position;
            _data.checkpointYaw = _player.transform.eulerAngles.y;
            return true;
        }

        void WriteNow()
        {
            if (_player == null) return;

            _data.scene = SceneManager.GetActiveScene().name;
            _data.level = _player.currentLevel;
            _data.xp = _player.currentXP;
            _data.effectMultiplier = _player.bulletEffectMultiplier;
            _data.unlockedBulletIndex = _player.MaxUnlockedBulletIndex;
            _data.earthUnlocked = _player.EarthUnlocked;
            _data.playSeconds += Time.unscaledTime - _sessionStart;
            _sessionStart = Time.unscaledTime;

            SaveSystem.Save(_data);
            _dirty = false;
            _nextWriteTime = Time.unscaledTime + 1f;
        }
    }
}
