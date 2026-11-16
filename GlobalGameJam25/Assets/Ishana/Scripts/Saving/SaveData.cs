using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Ishana.Saving
{
    /// <summary>Everything written to the save file. Plain fields so JsonUtility can serialize it.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string scene;
        public string savedAtUtc;
        public float playSeconds;

        // Progression
        public int level = 1;
        public int xp;
        public float effectMultiplier = 1f;
        public int unlockedBulletIndex;
        public bool earthUnlocked;

        // Checkpoint
        public bool hasCheckpoint;
        public Vector3 checkpointPosition;
        public float checkpointYaw;

        // World state (keys from SaveKeys.For)
        public List<string> clearedGroves = new List<string>();
        public List<string> openedChests = new List<string>();
        public List<string> brokenBoulders = new List<string>();
        public List<string> firedTutorialTriggers = new List<string>();
    }

    /// <summary>
    /// Stable identifiers for scene objects, so a save can say "this chest was opened".
    /// Built from the object's type, name and starting position, which survive between play sessions
    /// as long as the object isn't moved or renamed in the scene.
    /// </summary>
    public static class SaveKeys
    {
        public static string For(Component component)
        {
            if (component == null) return string.Empty;
            Vector3 p = component.transform.position;
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}@{2:F1},{3:F1},{4:F1}",
                component.GetType().Name, component.gameObject.name, p.x, p.y, p.z);
        }
    }
}
