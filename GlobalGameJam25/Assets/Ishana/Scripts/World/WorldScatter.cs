using System.Collections.Generic;
using Ishana.Elements;
using UnityEngine;

namespace Ishana.World
{
    /// <summary>
    /// Procedurally fills the terrain around the player with trees, boulders, bushes, grass and weeds.
    ///
    /// - The world is split into square cells. Cells near the player are filled in, far ones are removed,
    ///   so the map can be any size. Grass only exists close to the player.
    /// - Everything comes from the seed: the same seed always grows the same forest. Change the seed for
    ///   a different layout.
    /// - Plants can burn, be put out with ice and regrown with earth magic (Flammable). Some trees can be
    ///   grown into climbable trees (GrowablePlant). Boulders crack and crumble (BreakableBoulder).
    ///   What happened to an object is remembered while you play, even if you walk away and come back.
    /// - Objects are built in code by default. Drop your own prefabs into the prefab lists to use those
    ///   instead; they get the same fire and crumble behaviour automatically.
    /// - Nothing is placed on roads/rock (by terrain layer name), on steep slopes, inside other objects
    ///   or next to chests, pickups and bosses. Add a ScatterBlocker to keep any other area clear.
    ///
    /// Created automatically by IshanaBootstrap. Add it to a scene object yourself to tune it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/World Scatter (procedural trees, rocks, grass)")]
    public class WorldScatter : MonoBehaviour
    {
        [Header("General")]
        [Tooltip("Same seed = same world. Change it for a different layout.")]
        public int seed = 2026;
        [Tooltip("Sizes below are for a 2-unit-tall character and are scaled to the real player size.")]
        public bool scaleWithCharacterSize = true;
        [Tooltip("What the world is generated around. Empty = the player.")]
        public Transform center;

        [Header("Streaming (in units for a 2-unit-tall character)")]
        public float cellSize = 24f;
        [Tooltip("Trees, boulders and bushes exist within this distance of the player.")]
        public float objectDistance = 130f;
        [Tooltip("Grass and weeds exist within this distance of the player.")]
        public float grassDistance = 48f;
        [Tooltip("How many cells may be filled in per frame. Higher = less pop-in, more stutter.")]
        public int cellsPerFrame = 1;

        [Header("Trees")]
        [Tooltip("Average trees per cell. Forest patches get about double, clearings almost none.")]
        public float treesPerCell = 2.5f;
        public Vector2 treeHeight = new Vector2(5f, 9f);
        [Range(0f, 1f)] public float pineShare = 0.4f;
        [Tooltip("Share of trees that earth magic can grow into climbable trees. They have lighter leaves.")]
        [Range(0f, 1f)] public float growableShare = 0.25f;
        [Tooltip("Optional: your own tree prefabs. Empty = trees built in code.")]
        public GameObject[] treePrefabs;

        [Header("Trees Painted On The Terrain")]
        [Tooltip("Trees painted with Unity's terrain tool can't burn or grow on their own. With this on, painted trees " +
                 "near the player are swapped for real objects that can (far ones stay as they are, so the forest looks the same). " +
                 "Your terrain assets are never changed: the game works on a copy made when it starts.")]
        public bool convertPaintedTrees = true;
        [Tooltip("Share of converted painted trees that earth magic can grow.")]
        [Range(0f, 1f)] public float paintedGrowableShare = 0.15f;

        [Header("Boulders")]
        public float bouldersPerCell = 0.5f;
        public Vector2 boulderRadius = new Vector2(0.8f, 2.2f);
        [Tooltip("Total damage a generated boulder takes before it crumbles.")]
        public int boulderToughness = 30;
        public GameObject[] boulderPrefabs;

        [Header("Bushes")]
        public float bushesPerCell = 2f;
        public GameObject[] bushPrefabs;

        [Header("Grass and Weeds")]
        public float grassPerCell = 60f;
        [Range(0f, 1f)] public float weedShare = 0.2f;
        [Tooltip("Burnt grass grows back by itself after this many seconds (0 = only earth magic).")]
        public float grassRegrowsAfter = 0f;
        public GameObject[] grassPrefabs;

        [Header("Placement Rules")]
        public float maxTreeSlope = 28f;
        public float maxGrassSlope = 40f;
        [Tooltip("Nothing is placed below this height (e.g. your water level). Very low = no limit.")]
        public float minWorldHeight = -100000f;
        [Tooltip("Terrain layers whose name or texture contains one of these words stay clear (roads, rock...).")]
        public string[] clearTerrainLayers = { "road", "path", "dirt", "rock", "stone", "sand", "gravel", "cliff" };
        [Tooltip("Trees and boulders keep this far from chests, pickups, bosses, teleporters and the start point.")]
        public float keepClearRadius = 8f;
        [Tooltip("Layers that count as 'something is already here'.")]
        public LayerMask obstacleLayers = ~((1 << 1) | (1 << 2) | (1 << 4) | (1 << 5));

        [Header("Fire")]
        [Tooltip("Most plants that can burn at the same time.")]
        public int maxFires = 40;

        [Header("Colours")]
        public Color barkColor = new Color(0.33f, 0.24f, 0.17f);
        public Color[] leafColors = { new Color(0.2f, 0.45f, 0.18f), new Color(0.26f, 0.5f, 0.2f), new Color(0.17f, 0.38f, 0.2f) };
        public Color growableLeafColor = new Color(0.5f, 0.72f, 0.25f);
        public Color pineColor = new Color(0.12f, 0.32f, 0.2f);
        public Color[] grassColors = { new Color(0.3f, 0.55f, 0.2f), new Color(0.36f, 0.6f, 0.22f) };
        public Color weedColor = new Color(0.42f, 0.5f, 0.2f);
        public Color rockColor = new Color(0.45f, 0.44f, 0.42f);

        // ------------------------------------------------------------------ internals

        const int Removed = -1;      // Saved state of an object that no longer exists (broken boulder)
        const int GrownOffset = 10;  // Added to a plant's saved state when it has been grown

        class Tracked
        {
            public int index; public GameObject go; public ElementalObject plant; public GrowablePlant growable;
            public PaintedTree painted; // Set when this object stands in for a tree painted on the terrain
        }

        class PaintedTree { public Terrain terrain; public int instanceIndex; public TreeInstance original; }

        class Cell
        {
            public Vector2Int coord;
            public bool objectsLoaded, grassLoaded;
            public readonly List<Tracked> objects = new List<Tracked>();
            public readonly List<Tracked> grass = new List<Tracked>();
        }

        struct Job { public Cell cell; public bool grass; public float distance; }

        readonly Dictionary<Vector2Int, Cell> _cells = new Dictionary<Vector2Int, Cell>();
        readonly Dictionary<long, int> _saved = new Dictionary<long, int>();
        readonly List<Job> _jobs = new List<Job>();
        readonly List<Vector2Int> _toRemove = new List<Vector2Int>();
        readonly List<Vector3> _keepClear = new List<Vector3>();
        readonly Collider[] _overlap = new Collider[16];
        readonly Dictionary<Terrain, bool[]> _blockedLayers = new Dictionary<Terrain, bool[]>();
        readonly Dictionary<Vector2Int, List<PaintedTree>> _painted = new Dictionary<Vector2Int, List<PaintedTree>>();

        Terrain[] _terrains;
        Transform _root;
        PlayerStateManager _player;
        float _scale = 1f;
        float _nextScan;
        bool _firstScan = true;
        int _jobIndex;

        float Cs => cellSize * _scale;

        void Start()
        {
            _root = new GameObject("Scattered Objects").transform;
            _root.SetParent(transform, false);
            Flammable.MaxSimultaneousFires = maxFires;
        }

        bool Prepare()
        {
            if (center == null)
            {
                if (_player == null) _player = FindFirstObjectByType<PlayerStateManager>();
                if (_player == null) return false;
                center = _player.transform;
            }
            if (_terrains != null) return _terrains.Length > 0;

            _terrains = Terrain.activeTerrains;
            if (_terrains.Length == 0)
            {
                Debug.LogWarning("WorldScatter: no Terrain in the scene, nothing to grow on.", this);
                return false;
            }

            if (_player == null) _player = FindFirstObjectByType<PlayerStateManager>();
            if (scaleWithCharacterSize && _player != null)
            {
                var cc = _player.GetComponent<CharacterController>();
                if (cc != null) _scale = Mathf.Max(0.25f, cc.height * _player.transform.lossyScale.y / 2f);
            }

            // Places to keep clear of trees and boulders
            _keepClear.Add(center.position);
            foreach (var c in FindObjectsByType<ChestInteraction>(FindObjectsSortMode.None)) _keepClear.Add(c.transform.position);
            foreach (var c in FindObjectsByType<BulletUnlockTrigger>(FindObjectsSortMode.None)) _keepClear.Add(c.transform.position);
            foreach (var c in FindObjectsByType<BossEnemy>(FindObjectsSortMode.None)) _keepClear.Add(c.transform.position);
            foreach (var c in FindObjectsByType<TeleportTrigger>(FindObjectsSortMode.None)) _keepClear.Add(c.transform.position);
            foreach (var c in FindObjectsByType<HealingAura>(FindObjectsSortMode.None)) _keepClear.Add(c.transform.position);

            // Which terrain layers are roads/rock
            foreach (Terrain t in _terrains)
            {
                TerrainLayer[] layers = t.terrainData.terrainLayers;
                var blocked = new bool[layers.Length];
                bool any = false;
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] == null) continue;
                    string text = (layers[i].name + " " + (layers[i].diffuseTexture != null ? layers[i].diffuseTexture.name : "")).ToLowerInvariant();
                    foreach (string word in clearTerrainLayers)
                        if (!string.IsNullOrEmpty(word) && text.Contains(word.ToLowerInvariant())) { blocked[i] = true; any = true; break; }
                }
                _blockedLayers[t] = any ? blocked : null;
            }

            if (convertPaintedTrees) IndexPaintedTrees();
            return true;
        }

        void Update()
        {
            if (!Prepare()) return;

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.35f;
                Scan();

                if (_firstScan)
                {
                    // Fill in the area right around the player before the first frame is shown
                    _firstScan = false;
                    float near = grassDistance * _scale * 1.5f;
                    while (_jobIndex < _jobs.Count && _jobs[_jobIndex].distance <= near) Run(_jobs[_jobIndex++]);
                }
            }

            for (int i = 0; i < Mathf.Max(1, cellsPerFrame) && _jobIndex < _jobs.Count; i++) Run(_jobs[_jobIndex++]);
        }

        // ------------------------------------------------------------------ painted terrain trees

        void IndexPaintedTrees()
        {
            float cs = Cs;
            foreach (Terrain t in _terrains)
            {
                if (t == null || t.terrainData == null || t.terrainData.treeInstanceCount == 0) continue;

                // Work on a copy, so hiding trees while playing never alters the terrain asset on disk
                TerrainData copy = TerrainRuntime.Writable(t);

                TreeInstance[] instances = copy.treeInstances;
                Vector3 origin = t.transform.position, size = copy.size;
                for (int i = 0; i < instances.Length; i++)
                {
                    Vector3 world = origin + Vector3.Scale(instances[i].position, size);
                    var coord = new Vector2Int(Mathf.FloorToInt(world.x / cs), Mathf.FloorToInt(world.z / cs));
                    if (!_painted.TryGetValue(coord, out List<PaintedTree> list))
                    {
                        list = new List<PaintedTree>();
                        _painted[coord] = list;
                    }
                    list.Add(new PaintedTree { terrain = t, instanceIndex = i, original = instances[i] });
                }
            }
        }

        /// <summary>Swap the painted trees in a cell for real objects that can burn, be healed and grow.</summary>
        void LoadPaintedTrees(Cell cell)
        {
            if (!_painted.TryGetValue(cell.coord, out List<PaintedTree> list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                PaintedTree painted = list[i];
                if (painted.terrain == null) continue;
                TerrainData data = painted.terrain.terrainData;
                TreePrototype[] prototypes = data.treePrototypes;
                TreeInstance tree = painted.original;
                if (tree.prototypeIndex < 0 || tree.prototypeIndex >= prototypes.Length || prototypes[tree.prototypeIndex].prefab == null) continue;

                GameObject prefab = prototypes[tree.prototypeIndex].prefab;
                Vector3 world = painted.terrain.transform.position + Vector3.Scale(tree.position, data.size);
                GameObject go = Instantiate(prefab, world, Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f), _root);
                go.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(tree.widthScale, tree.heightScale, tree.widthScale));

                var plant = go.GetComponent<Flammable>();
                if (plant == null)
                {
                    plant = go.AddComponent<Flammable>();
                    plant.AutoSize();
                    plant.burnDuration = 16f;
                    plant.flammability = 0.75f;
                    plant.spreadRadius = 3f * _scale;
                    plant.spreadInterval = 1.6f;
                }

                // A trunk to hit and to bump into, if the model has none
                if (go.GetComponentInChildren<Collider>() == null)
                {
                    float height = plant.centerHeight * 2f;
                    Vector3 ls = go.transform.lossyScale;
                    var trunk = go.AddComponent<CapsuleCollider>();
                    trunk.radius = Mathf.Max(0.05f, height * 0.05f) / Mathf.Max(0.0001f, Mathf.Abs(ls.x));
                    trunk.height = height * 0.85f / Mathf.Max(0.0001f, Mathf.Abs(ls.y));
                    trunk.center = Vector3.up * (height * 0.45f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)));
                }

                // The same trees are growable every time
                var roll = new System.Random(unchecked(seed * 31 + painted.instanceIndex * 7919));
                if (roll.NextDouble() < paintedGrowableShare && go.GetComponent<GrowablePlant>() == null) go.AddComponent<GrowablePlant>();

                // Hide the painted version while the real one stands in for it
                TreeInstance hidden = tree;
                hidden.widthScale = 0f;
                hidden.heightScale = 0f;
                data.SetTreeInstance(painted.instanceIndex, hidden);

                Track(cell, cell.objects, 5000 + i, go);
                cell.objects[cell.objects.Count - 1].painted = painted;
            }
        }

        // ------------------------------------------------------------------ streaming

        void Scan()
        {
            Vector3 p = center.position;
            float cs = Cs, objR = objectDistance * _scale, grassR = grassDistance * _scale;
            int range = Mathf.CeilToInt(objR / cs);
            var here = new Vector2Int(Mathf.FloorToInt(p.x / cs), Mathf.FloorToInt(p.z / cs));

            _jobs.Clear();
            _jobIndex = 0;

            for (int dx = -range; dx <= range; dx++)
                for (int dz = -range; dz <= range; dz++)
                {
                    var coord = new Vector2Int(here.x + dx, here.y + dz);
                    float distance = DistanceToCell(p, coord, cs);
                    if (distance > objR) continue;

                    if (!_cells.TryGetValue(coord, out Cell cell))
                    {
                        cell = new Cell { coord = coord };
                        _cells[coord] = cell;
                    }
                    if (!cell.objectsLoaded) _jobs.Add(new Job { cell = cell, grass = false, distance = distance });
                    if (!cell.grassLoaded && distance <= grassR) _jobs.Add(new Job { cell = cell, grass = true, distance = distance });
                }

            _jobs.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Remove what is now too far away (with some slack so cells don't flicker at the edge)
            _toRemove.Clear();
            foreach (Cell cell in _cells.Values)
            {
                float distance = DistanceToCell(p, cell.coord, cs);
                if (cell.grassLoaded && distance > grassR + cs)
                {
                    Unload(cell, cell.grass);
                    cell.grassLoaded = false;
                }
                if (distance > objR + cs * 1.5f)
                {
                    Unload(cell, cell.objects);
                    Unload(cell, cell.grass);
                    _toRemove.Add(cell.coord);
                }
            }
            foreach (Vector2Int coord in _toRemove) _cells.Remove(coord);
        }

        static float DistanceToCell(Vector3 p, Vector2Int coord, float cs)
        {
            float cx = (coord.x + 0.5f) * cs, cz = (coord.y + 0.5f) * cs;
            return Mathf.Sqrt((p.x - cx) * (p.x - cx) + (p.z - cz) * (p.z - cz));
        }

        void Run(Job job)
        {
            if (job.grass) { if (!job.cell.grassLoaded) LoadGrass(job.cell); }
            else if (!job.cell.objectsLoaded) LoadObjects(job.cell);
        }

        void Unload(Cell cell, List<Tracked> list)
        {
            foreach (Tracked t in list)
            {
                long key = Key(cell.coord, t.index);
                // Far away again: show the painted tree in its place
                if (t.painted != null && t.painted.terrain != null)
                    t.painted.terrain.terrainData.SetTreeInstance(t.painted.instanceIndex, t.painted.original);

                if (t.go == null) { _saved[key] = Removed; continue; }

                int state = t.plant != null ? t.plant.StateId : 0;
                if (t.growable != null && (t.growable.IsGrown || t.growable.IsGrowing)) state += GrownOffset;
                if (state != 0) _saved[key] = state; else _saved.Remove(key);
                Destroy(t.go);
            }
            list.Clear();
        }

        static long Key(Vector2Int coord, int index) =>
            ((long)(coord.x & 0xFFFFF) << 44) | ((long)(coord.y & 0xFFFFF) << 24) | (long)(index & 0xFFFFFF);

        System.Random Rng(Vector2Int coord, int salt)
        {
            unchecked
            {
                int h = seed;
                h = h * 486187739 + coord.x;
                h = h * 486187739 + coord.y;
                h = h * 486187739 + salt;
                return new System.Random(h);
            }
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
        static float Range(System.Random rng, Vector2 range) => Range(rng, range.x, range.y);

        /// <summary>Turn an average count into a whole number (2.5 = two, and a third half the time).</summary>
        static int Count(System.Random rng, float average)
        {
            int whole = Mathf.FloorToInt(Mathf.Max(0f, average));
            return whole + (rng.NextDouble() < average - whole ? 1 : 0);
        }

        // ------------------------------------------------------------------ generating a cell

        void LoadObjects(Cell cell)
        {
            cell.objectsLoaded = true;
            LoadPaintedTrees(cell);
            System.Random rng = Rng(cell.coord, 1);
            float cs = Cs;
            var origin = new Vector3(cell.coord.x * cs, 0f, cell.coord.y * cs);

            // Forest patches and clearings
            float forest = Mathf.PerlinNoise(cell.coord.x * 0.21f + seed * 0.013f + 100f, cell.coord.y * 0.21f + seed * 0.017f + 100f);
            float treeAverage = treesPerCell * Mathf.Lerp(0.1f, 2f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, forest)));

            int trees = Count(rng, treeAverage);
            for (int i = 0; i < trees; i++)
            {
                // Roll everything first so the result doesn't depend on what gets rejected
                Vector3 xz = origin + new Vector3(Range(rng, 0f, cs), 0f, Range(rng, 0f, cs));
                float height = Range(rng, treeHeight) * _scale;
                bool pine = rng.NextDouble() < pineShare;
                bool growable = rng.NextDouble() < growableShare;
                float yaw = Range(rng, 0f, 360f);
                int variant = rng.Next(1000);
                int index = i;

                if (IsRemoved(cell, index)) continue;
                if (!Place(xz, maxTreeSlope, height * 0.12f, true, out Vector3 point, out _)) continue;

                GameObject go = treePrefabs != null && treePrefabs.Length > 0
                    ? FromPrefab(treePrefabs[variant % treePrefabs.Length], point, yaw, Range(rng, 0.85f, 1.2f), false)
                    : BuildTree(point, yaw, height, pine, growable, variant);
                if (growable && go.GetComponent<GrowablePlant>() == null) go.AddComponent<GrowablePlant>();
                Track(cell, cell.objects, index, go);
            }

            int boulders = Count(rng, bouldersPerCell);
            for (int i = 0; i < boulders; i++)
            {
                Vector3 xz = origin + new Vector3(Range(rng, 0f, cs), 0f, Range(rng, 0f, cs));
                float radius = Range(rng, boulderRadius) * _scale;
                var squash = new Vector3(1f, Range(rng, 0.6f, 0.9f), Range(rng, 0.75f, 1.1f));
                float yaw = Range(rng, 0f, 360f);
                int variant = rng.Next(1000);
                int index = 200 + i;

                if (IsRemoved(cell, index)) continue;
                if (!Place(xz, maxGrassSlope, radius, true, out Vector3 point, out _)) continue;

                GameObject go = boulderPrefabs != null && boulderPrefabs.Length > 0
                    ? FromPrefab(boulderPrefabs[variant % boulderPrefabs.Length], point, yaw, Range(rng, 0.8f, 1.3f), true)
                    : BuildBoulder(point, yaw, radius, squash, variant);
                Track(cell, cell.objects, index, go);
            }

            int bushes = Count(rng, bushesPerCell * Mathf.Lerp(0.5f, 1.5f, forest));
            for (int i = 0; i < bushes; i++)
            {
                Vector3 xz = origin + new Vector3(Range(rng, 0f, cs), 0f, Range(rng, 0f, cs));
                float size = Range(rng, 0.8f, 1.6f) * _scale;
                float yaw = Range(rng, 0f, 360f);
                int variant = rng.Next(1000);
                int index = 400 + i;

                if (!Place(xz, maxGrassSlope, size * 0.5f, false, out Vector3 point, out _)) continue;

                GameObject go = bushPrefabs != null && bushPrefabs.Length > 0
                    ? FromPrefab(bushPrefabs[variant % bushPrefabs.Length], point, yaw, Range(rng, 0.8f, 1.3f), false)
                    : BuildBush(point, yaw, size, variant);
                Track(cell, cell.objects, index, go);
            }
        }

        void LoadGrass(Cell cell)
        {
            cell.grassLoaded = true;
            System.Random rng = Rng(cell.coord, 2);
            float cs = Cs;
            var origin = new Vector3(cell.coord.x * cs, 0f, cell.coord.y * cs);

            int tufts = Count(rng, grassPerCell);
            for (int i = 0; i < tufts; i++)
            {
                Vector3 xz = origin + new Vector3(Range(rng, 0f, cs), 0f, Range(rng, 0f, cs));
                float width = Range(rng, 1.1f, 2f) * _scale;
                float height = Range(rng, 0.5f, 1f) * _scale;
                bool weed = rng.NextDouble() < weedShare;
                float yaw = Range(rng, 0f, 360f);
                int variant = rng.Next(1000);
                int index = 1000 + i;

                // Grass grows in patches
                float patch = Mathf.PerlinNoise(xz.x / (9f * _scale) + seed * 0.031f, xz.z / (9f * _scale) + seed * 0.027f);
                if (patch < 0.38f) continue;
                if (!Place(xz, maxGrassSlope, 0f, false, out Vector3 point, out Vector3 normal)) continue;

                GameObject go = grassPrefabs != null && grassPrefabs.Length > 0
                    ? FromPrefab(grassPrefabs[variant % grassPrefabs.Length], point, yaw, Range(rng, 0.8f, 1.3f), false)
                    : BuildGrass(point, normal, yaw, width, height, weed, variant);
                Track(cell, cell.grass, index, go);
            }
        }

        bool IsRemoved(Cell cell, int index) =>
            _saved.TryGetValue(Key(cell.coord, index), out int state) && state == Removed;

        void Track(Cell cell, List<Tracked> list, int index, GameObject go)
        {
            var t = new Tracked { index = index, go = go, plant = go.GetComponent<ElementalObject>(), growable = go.GetComponent<GrowablePlant>() };
            list.Add(t);

            // Put it back the way the player left it
            if (_saved.TryGetValue(Key(cell.coord, index), out int state) && state > 0)
            {
                if (state >= GrownOffset)
                {
                    state -= GrownOffset;
                    if (t.growable != null) t.growable.Grow(instant: true);
                }
                if (t.plant != null && state > 0) t.plant.RestoreState(state);
            }
        }

        // ------------------------------------------------------------------ placement

        bool Place(Vector3 xz, float maxSlope, float clearRadius, bool keepClearOfPoints, out Vector3 point, out Vector3 normal)
        {
            point = xz;
            normal = Vector3.up;

            Terrain terrain = null;
            foreach (Terrain t in _terrains)
            {
                if (t == null) continue;
                Vector3 tp = t.transform.position, size = t.terrainData.size;
                if (xz.x >= tp.x && xz.x <= tp.x + size.x && xz.z >= tp.z && xz.z <= tp.z + size.z) { terrain = t; break; }
            }
            if (terrain == null) return false;

            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            float u = Mathf.Clamp01((xz.x - origin.x) / data.size.x), v = Mathf.Clamp01((xz.z - origin.z) / data.size.z);

            point = new Vector3(xz.x, terrain.SampleHeight(xz) + origin.y, xz.z);
            if (point.y < minWorldHeight) return false;

            normal = data.GetInterpolatedNormal(u, v);
            if (Vector3.Angle(normal, Vector3.up) > maxSlope) return false;

            // Roads, rock and so on stay clear
            if (_blockedLayers.TryGetValue(terrain, out bool[] blocked) && blocked != null)
            {
                int ax = Mathf.Clamp(Mathf.FloorToInt(u * data.alphamapWidth), 0, data.alphamapWidth - 1);
                int ay = Mathf.Clamp(Mathf.FloorToInt(v * data.alphamapHeight), 0, data.alphamapHeight - 1);
                float[,,] weights = data.GetAlphamaps(ax, ay, 1, 1);
                int strongest = 0;
                for (int i = 1; i < weights.GetLength(2); i++)
                    if (weights[0, 0, i] > weights[0, 0, strongest]) strongest = i;
                if (strongest < blocked.Length && blocked[strongest]) return false;
            }

            foreach (ScatterBlocker blocker in ScatterBlocker.All)
                if (blocker != null && blocker.Blocks(point, keepClearOfPoints)) return false;

            if (keepClearOfPoints)
            {
                float keep = keepClearRadius * _scale + clearRadius;
                foreach (Vector3 spot in _keepClear)
                {
                    float dx = spot.x - point.x, dz = spot.z - point.z;
                    if (dx * dx + dz * dz < keep * keep) return false;
                }
            }

            // Is something solid already standing here?
            if (clearRadius > 0f)
            {
                Vector3 bottom = point + Vector3.up * (clearRadius + 0.5f * _scale);
                Vector3 top = bottom + Vector3.up * 2f * _scale;
                int hits = Physics.OverlapCapsuleNonAlloc(bottom, top, clearRadius, _overlap, obstacleLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits; i++)
                    if (!(_overlap[i] is TerrainCollider)) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ building objects

        GameObject NewObject(string name, Vector3 position, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (!shadows) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        GameObject BuildTree(Vector3 point, float yaw, float height, bool pine, bool growable, int variant)
        {
            GameObject root = NewObject(pine ? "Pine" : "Tree", point - Vector3.up * height * 0.02f, yaw);
            float trunkRadius = height * 0.045f;
            float trunkHeight = pine ? height * 0.4f : height * 0.62f;

            Part(root.transform, "Trunk", ProceduralMeshes.Trunk(), ProceduralMeshes.Matte("Bark", barkColor),
                 Vector3.zero, new Vector3(trunkRadius * 2f, trunkHeight, trunkRadius * 2f));

            GameObject crown;
            float crownRadius;
            if (pine)
            {
                crownRadius = height * 0.24f;
                Material needles = growable ? ProceduralMeshes.Matte("Growable Leaves", growableLeafColor) : ProceduralMeshes.Matte("Pine", pineColor);
                crown = Part(root.transform, "Crown", ProceduralMeshes.PineCrown(variant), needles,
                             Vector3.up * height * 0.2f, new Vector3(crownRadius * 2f, height * 0.78f, crownRadius * 2f));
            }
            else
            {
                crownRadius = height * 0.3f;
                Material leaves = growable
                    ? ProceduralMeshes.Matte("Growable Leaves", growableLeafColor)
                    : ProceduralMeshes.Matte("Leaves " + variant % Mathf.Max(1, leafColors.Length), leafColors.Length > 0 ? leafColors[variant % leafColors.Length] : Color.green);
                crown = Part(root.transform, "Crown", ProceduralMeshes.LeafCrown(variant), leaves,
                             Vector3.up * trunkHeight, Vector3.one * crownRadius);
            }

            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = trunkRadius * 1.2f;
            collider.height = height * 0.8f;
            collider.center = Vector3.up * height * 0.4f;

            var plant = root.AddComponent<Flammable>();
            plant.reachRadius = crownRadius;
            plant.centerHeight = height * 0.5f;
            plant.burnDuration = 16f;
            plant.flammability = 0.75f;
            plant.spreadRadius = 3f * _scale;
            plant.spreadInterval = 1.6f;
            plant.spreadChance = 0.25f;
            plant.SetFoliage(crown.transform);

            if (growable)
            {
                var grow = root.AddComponent<GrowablePlant>();
                grow.plantHeight = height;
                grow.trunkRadius = trunkRadius;
            }
            return root;
        }

        GameObject BuildBush(Vector3 point, float yaw, float size, int variant)
        {
            GameObject root = NewObject("Bush", point, yaw);
            root.transform.localScale = Vector3.one * size;
            root.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Bush(variant);
            root.AddComponent<MeshRenderer>().sharedMaterial =
                ProceduralMeshes.Matte("Leaves " + variant % Mathf.Max(1, leafColors.Length), leafColors.Length > 0 ? leafColors[variant % leafColors.Length] : Color.green);

            var plant = root.AddComponent<Flammable>();
            plant.reachRadius = size * 0.9f;
            plant.centerHeight = size * 0.4f;
            plant.burnDuration = 7f;
            plant.flammability = 0.9f;
            plant.spreadRadius = 2.5f * _scale;
            plant.spreadInterval = 1.2f;
            plant.spreadChance = 0.35f;
            plant.collapsesWhenBurnt = true;
            return root;
        }

        GameObject BuildGrass(Vector3 point, Vector3 normal, float yaw, float width, float height, bool weed, int variant)
        {
            GameObject root = NewObject(weed ? "Weeds" : "Grass", point, yaw);
            // Lean with the ground a little so tufts don't float on slopes
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, 0.7f)) * root.transform.rotation;
            root.transform.localScale = new Vector3(width, height, width);
            root.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GrassTuft(variant, weed);
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = weed
                ? ProceduralMeshes.Matte("Weeds", weedColor)
                : ProceduralMeshes.Matte("Grass " + variant % Mathf.Max(1, grassColors.Length), grassColors.Length > 0 ? grassColors[variant % grassColors.Length] : Color.green);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var plant = root.AddComponent<Flammable>();
            plant.reachRadius = width * 0.5f;
            plant.centerHeight = height * 0.4f;
            plant.burnDuration = 4f;
            plant.flammability = 1f;
            plant.spreadRadius = 2.5f * _scale;
            plant.spreadInterval = 0.8f;
            plant.spreadChance = 0.45f;
            plant.collapsesWhenBurnt = true;
            plant.autoRegrowAfter = grassRegrowsAfter;
            plant.regrowTime = 2f;
            return root;
        }

        GameObject BuildBoulder(Vector3 point, float yaw, float radius, Vector3 squash, int variant)
        {
            GameObject root = NewObject("Boulder", point + Vector3.up * radius * squash.y * 0.45f, yaw);
            root.transform.localScale = squash * radius;

            Mesh mesh = ProceduralMeshes.Rock(variant);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = ProceduralMeshes.Matte("Rock", rockColor);
            root.AddComponent<MeshCollider>().sharedMesh = mesh;

            var boulder = root.AddComponent<BreakableBoulder>();
            boulder.accumulateDamage = true;
            boulder.damageThreshold = Mathf.Max(1, Mathf.RoundToInt(boulderToughness * Mathf.Lerp(0.7f, 1.6f, Mathf.InverseLerp(boulderRadius.x, boulderRadius.y, radius / _scale))));
            boulder.chunkCount = 7;
            var audio = root.GetComponent<AudioSource>();
            if (audio != null) { audio.spatialBlend = 1f; audio.playOnAwake = false; }
            return root;
        }

        /// <summary>Use one of your prefabs, and make sure it reacts to magic like the built-in objects.</summary>
        GameObject FromPrefab(GameObject prefab, Vector3 point, float yaw, float scale, bool isBoulder)
        {
            GameObject go = Instantiate(prefab, point, Quaternion.Euler(0f, yaw, 0f), _root);
            go.transform.localScale *= scale;

            if (isBoulder)
            {
                if (go.GetComponentInChildren<BreakableBoulder>() == null)
                {
                    var boulder = go.AddComponent<BreakableBoulder>();
                    boulder.accumulateDamage = true;
                    boulder.damageThreshold = boulderToughness;
                }
            }
            else if (go.GetComponent<ElementalObject>() == null)
            {
                var plant = go.AddComponent<Flammable>();
                plant.AutoSize();
                plant.spreadRadius = 3f * _scale;
            }
            return go;
        }
    }
}
