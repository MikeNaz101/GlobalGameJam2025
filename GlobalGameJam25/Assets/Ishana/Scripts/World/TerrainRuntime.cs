using System.Collections.Generic;
using UnityEngine;

namespace Ishana.World
{
    /// <summary>
    /// Lets gameplay change terrains while the game runs (hide a painted tree, burn painted ferns, retile
    /// textures) without ever altering the terrain assets on disk: each terrain is switched to a private
    /// copy of its data the first time something wants to change it.
    /// </summary>
    public static class TerrainRuntime
    {
        static readonly HashSet<int> _copied = new HashSet<int>();

        /// <summary>The terrain's data, guaranteed to be a runtime copy that is safe to modify.</summary>
        public static TerrainData Writable(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null) return null;
            if (_copied.Contains(terrain.terrainData.GetInstanceID())) return terrain.terrainData;

            TerrainData copy = Object.Instantiate(terrain.terrainData);
            copy.name = terrain.terrainData.name + " (runtime copy)";
            terrain.terrainData = copy;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = copy;
            _copied.Add(copy.GetInstanceID());
            return copy;
        }

        /// <summary>The terrain under a world position, if any.</summary>
        public static Terrain At(Vector3 world)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                Vector3 p = t.transform.position, size = t.terrainData.size;
                if (world.x >= p.x && world.x <= p.x + size.x && world.z >= p.z && world.z <= p.z + size.z) return t;
            }
            return null;
        }
    }
}
