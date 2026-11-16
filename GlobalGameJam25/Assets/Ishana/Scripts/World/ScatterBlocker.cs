using System.Collections.Generic;
using UnityEngine;

namespace Ishana.World
{
    /// <summary>
    /// Keeps procedurally generated objects out of an area (an arena, a village, a path).
    /// Put it on an empty object and set the radius.
    /// </summary>
    [AddComponentMenu("Ishana/Scatter Blocker (keep area clear)")]
    public class ScatterBlocker : MonoBehaviour
    {
        public static readonly List<ScatterBlocker> All = new List<ScatterBlocker>();

        public float radius = 15f;
        [Tooltip("Also keep grass and bushes out. Off = only trees and boulders are blocked.")]
        public bool blockGrassToo = false;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Blocks(Vector3 point, bool isTreeOrBoulder)
        {
            if (!isTreeOrBoulder && !blockGrassToo) return false;
            float dx = point.x - transform.position.x, dz = point.z - transform.position.z;
            return dx * dx + dz * dz < radius * radius;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
