using System.Collections.Generic;
using Ishana.Elements;
using UnityEngine;

namespace Ishana.World
{
    /// <summary>
    /// Lets plants painted on the terrain (grass, ferns, weeds) burn, without turning them into objects.
    ///
    /// Painted plants stay in the terrain's own batched rendering, which is what keeps them cheap. Only
    /// where fire actually reaches does the ground get a small invisible "patch" that burns like any other
    /// plant: it shows flames, spreads to neighbouring patches and plants, can be put out with ice, and
    /// thins out the painted plants as it burns. Earth magic grows them back.
    ///
    /// Created automatically by IshanaBootstrap.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Terrain Detail Fire (painted plants burn)")]
    public class TerrainDetailFire : MonoBehaviour
    {
        [Tooltip("Size of one burnable patch of ground, in player heights.")]
        public float patchSizeInPlayerHeights = 1.5f;
        [Tooltip("Seconds a patch of painted plants takes to burn away.")]
        public float burnDuration = 6f;
        [Range(0f, 1f)] public float flammability = 0.9f;
        [Range(0f, 1f)] public float spreadChance = 0.4f;
        [Tooltip("Burnt patches grow back by themselves after this many seconds (0 = only earth magic).")]
        public float regrowAfter = 0f;

        readonly Dictionary<Vector2Int, DetailPatch> _patches = new Dictionary<Vector2Int, DetailPatch>();
        readonly HashSet<Vector2Int> _nothingToBurn = new HashSet<Vector2Int>();
        float _patchSize = 3f, _playerHeight = 2f;
        bool _sized;
        readonly Dictionary<TerrainData, int> _layerCounts = new Dictionary<TerrainData, int>();

        void OnEnable()
        {
            ElementalObject.OnImpact += HandleImpact;
            Flammable.OnSpreadTick += HandleSpread;
        }

        void OnDisable()
        {
            ElementalObject.OnImpact -= HandleImpact;
            Flammable.OnSpreadTick -= HandleSpread;
        }

        void Size()
        {
            if (_sized) return;
            var player = FindFirstObjectByType<PlayerStateManager>();
            if (player == null) return;
            _sized = true;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) _playerHeight = Mathf.Max(0.5f, cc.height * player.transform.lossyScale.y);
            _patchSize = Mathf.Max(0.5f, patchSizeInPlayerHeights * _playerHeight);
        }

        // Fire magic landed: make sure the painted plants there exist as burnable patches, so the hit reaches them
        void HandleImpact(Element element, Vector3 point, float radius, float power)
        {
            if (element != Element.Fire) return;
            Size();
            EnsureAround(point, radius + _patchSize * 0.5f);
        }

        // Something is burning: the painted plants next to it become patches the fire can jump to
        void HandleSpread(Flammable source)
        {
            Size();
            EnsureAround(source.transform.position, source.reachRadius + _patchSize * 0.75f);
        }

        void EnsureAround(Vector3 point, float radius)
        {
            int reach = Mathf.Clamp(Mathf.CeilToInt(radius / _patchSize), 1, 2);
            var here = new Vector2Int(Mathf.FloorToInt(point.x / _patchSize), Mathf.FloorToInt(point.z / _patchSize));
            for (int dx = -reach; dx <= reach; dx++)
                for (int dz = -reach; dz <= reach; dz++)
                {
                    var coord = new Vector2Int(here.x + dx, here.y + dz);
                    if (_patches.ContainsKey(coord) || _nothingToBurn.Contains(coord)) continue;

                    float cx = (coord.x + 0.5f) * _patchSize, cz = (coord.y + 0.5f) * _patchSize;
                    float ddx = cx - point.x, ddz = cz - point.z;
                    if (ddx * ddx + ddz * ddz > (radius + _patchSize * 0.7f) * (radius + _patchSize * 0.7f)) continue;

                    DetailPatch patch = Create(coord, new Vector3(cx, 0f, cz));
                    if (patch != null) _patches[coord] = patch; else _nothingToBurn.Add(coord);
                }
        }

        DetailPatch Create(Vector2Int coord, Vector3 center)
        {
            Terrain terrain = TerrainRuntime.At(center);
            if (terrain == null) return null;
            TerrainData data = terrain.terrainData;
            if (!_layerCounts.TryGetValue(data, out int layers))
            {
                layers = data.detailPrototypes.Length; // Copies the whole list, so only ask once per terrain
                _layerCounts[data] = layers;
            }
            if (layers == 0 || data.detailWidth <= 0) return null;

            // The block of the terrain's plant map that this patch covers
            Vector3 origin = terrain.transform.position;
            float half = _patchSize * 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - half - origin.x) / data.size.x * data.detailWidth), 0, data.detailWidth - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + half - origin.x) / data.size.x * data.detailWidth), x0 + 1, data.detailWidth);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((center.z - half - origin.z) / data.size.z * data.detailHeight), 0, data.detailHeight - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((center.z + half - origin.z) / data.size.z * data.detailHeight), y0 + 1, data.detailHeight);

            // Only remember the kinds of plant that actually grow here: every kind that has to be
            // redrawn while burning costs time, and most patches only hold one or two
            var indices = new List<int>();
            var originals = new List<int[,]>();
            for (int l = 0; l < layers; l++)
            {
                int[,] values = data.GetDetailLayer(x0, y0, x1 - x0, y1 - y0, l);
                bool any = false;
                foreach (int v in values) if (v != 0) { any = true; break; }
                if (!any) continue;
                indices.Add(l);
                originals.Add(values);
            }
            if (indices.Count == 0) return null; // Bare ground: nothing to burn

            center.y = terrain.SampleHeight(center) + origin.y;
            var go = new GameObject("Burnable Ground Patch");
            go.transform.SetParent(transform, false);
            go.transform.position = center;

            var plant = go.AddComponent<Flammable>();
            plant.reachRadius = _patchSize * 0.6f;
            plant.centerHeight = _playerHeight * 0.25f;
            plant.burnDuration = burnDuration;
            plant.flammability = flammability;
            plant.spreadRadius = _patchSize * 0.6f;
            plant.spreadInterval = 0.9f;
            plant.spreadChance = spreadChance;
            plant.autoRegrowAfter = regrowAfter;
            plant.regrowTime = 2.5f;
            plant.smoulders = false;

            var patch = go.AddComponent<DetailPatch>();
            patch.Init(terrain, plant, x0, y0, indices.ToArray(), originals.ToArray());
            return patch;
        }
    }
}
