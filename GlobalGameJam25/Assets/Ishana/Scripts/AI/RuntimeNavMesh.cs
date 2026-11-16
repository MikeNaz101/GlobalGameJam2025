using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Ishana.AI
{
    /// <summary>
    /// Makes sure the gameplay scene has a NavMesh for enemy pathfinding.
    ///
    /// If the scene already has a baked NavMesh, that one is used. Otherwise one is built in the
    /// background when the scene starts, from the scene's colliders (terrain, rocks, trees, invisible
    /// walls). Enemies walk in straight lines, as before, until it's ready.
    ///
    /// For faster loads and full control, bake a NavMesh in the Editor (add a NavMeshSurface to the
    /// scene and press Bake): this component then does nothing.
    ///
    /// Created automatically by IshanaBootstrap.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Runtime NavMesh")]
    public class RuntimeNavMesh : MonoBehaviour
    {
        [Tooltip("Build a NavMesh at runtime when the scene has no baked one.")]
        public bool buildIfMissing = true;
        [Tooltip("Extra space added around the groves and the player when working out the area to cover.")]
        public float padding = 40f;
        [Tooltip("Largest area covered, per side (metres). Keeps build time and memory in check on huge maps.")]
        public float maxSize = 1500f;

        [Header("Agent (who walks on it)")]
        public float agentRadius = 0.6f;
        public float agentHeight = 2f;
        public float agentMaxSlope = 45f;
        public float agentStepHeight = 0.6f;
        [Tooltip("Detail of the NavMesh. Larger = faster to build and less memory, but coarser around obstacles.")]
        public float voxelSize = 0.3f;

        [Tooltip("Layers treated as walkable ground and obstacles.")]
        public LayerMask geometryLayers = ~((1 << 1) | (1 << 2) | (1 << 4) | (1 << 5)); // not TransparentFX, Ignore Raycast, Water, UI

        /// <summary>True once enemies can ask for paths.</summary>
        public static bool Available { get; private set; }

        NavMeshData _data;
        NavMeshDataInstance _instance;

        IEnumerator Start()
        {
            Available = false;
            yield return null; // Let the scene finish setting up (terrain colliders, spawned props)

            var player = FindFirstObjectByType<PlayerStateManager>();
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;

            // A NavMesh baked in the Editor wins
            if (NavMesh.SamplePosition(origin, out _, 100f, NavMesh.AllAreas))
            {
                Available = true;
                yield break;
            }

            if (!buildIfMissing) yield break;

            Bounds bounds = WorkOutBounds(origin);

            // Don't treat the player or the enemies themselves as obstacles
            var markups = new List<NavMeshBuildMarkup>();
            if (player != null) markups.Add(new NavMeshBuildMarkup { root = player.transform, ignoreFromBuild = true });
            foreach (var enemy in FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None))
                markups.Add(new NavMeshBuildMarkup { root = enemy.transform, ignoreFromBuild = true });

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, geometryLayers, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            // Trigger volumes (groves, tutorial zones, pickups) are not obstacles
            sources.RemoveAll(s => s.component is Collider c && c.isTrigger);
            if (sources.Count == 0)
            {
                Debug.LogWarning("RuntimeNavMesh: found no colliders to build a NavMesh from. Enemies will move in straight lines.", this);
                yield break;
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = agentRadius;
            settings.agentHeight = agentHeight;
            settings.agentSlope = agentMaxSlope;
            settings.agentClimb = agentStepHeight;
            settings.overrideVoxelSize = true;
            settings.voxelSize = voxelSize;
            settings.overrideTileSize = true;
            settings.tileSize = 128;

            _data = new NavMeshData(settings.agentTypeID);
            _instance = NavMesh.AddNavMeshData(_data);

            float started = Time.realtimeSinceStartup;
            AsyncOperation build = NavMeshBuilder.UpdateNavMeshDataAsync(_data, settings, sources, bounds);
            while (!build.isDone) yield return null;

            Available = NavMesh.SamplePosition(origin, out _, 100f, NavMesh.AllAreas);
            Debug.Log($"RuntimeNavMesh: built from {sources.Count} colliders over {bounds.size.x:F0} x {bounds.size.z:F0} m " +
                      $"in {Time.realtimeSinceStartup - started:F1}s. Usable: {Available}. " +
                      "Bake a NavMesh in the Editor to skip this step.", this);
        }

        Bounds WorkOutBounds(Vector3 origin)
        {
            var bounds = new Bounds(origin, Vector3.one);
            foreach (var grove in FindObjectsByType<AreaCleansingManager>(FindObjectsSortMode.None))
            {
                var col = grove.GetComponent<Collider>();
                if (col != null) bounds.Encapsulate(col.bounds);
                else bounds.Encapsulate(grove.transform.position);
            }
            foreach (var enemy in FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None))
                bounds.Encapsulate(enemy.transform.position);

            bounds.Expand(new Vector3(padding * 2f, 0f, padding * 2f));
            Vector3 size = bounds.size;
            size.x = Mathf.Min(size.x, maxSize);
            size.z = Mathf.Min(size.z, maxSize);
            size.y = Mathf.Max(size.y, 100f) + 400f; // Generous vertical range for hills and valleys
            bounds.size = size;
            return bounds;
        }

        void OnDestroy()
        {
            Available = false;
            if (_instance.valid) _instance.Remove();
        }
    }
}
