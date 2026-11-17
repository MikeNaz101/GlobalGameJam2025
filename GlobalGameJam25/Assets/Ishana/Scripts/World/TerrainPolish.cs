using UnityEngine;

namespace Ishana.World
{
    /// <summary>
    /// Makes the terrain look cleaner and crisper when the game starts, without changing the terrain assets:
    ///
    /// - Layered ground: the terrains were drawn with one ordinary material (a single photo stretched across
    ///   the whole terrain), which is why the ground looked blurry and abstract and why painted terrain
    ///   textures never showed. This switches them to Unity's terrain material, so the painted layers are
    ///   drawn as tiled, sharp textures. Untick "Use Layered Terrain Material" to get the old look back.
    /// - Texture tiling sized to the player, so the ground has detail up close without looking noisy.
    /// - Sharper terrain shape in the distance (lower pixel error) and textures that stay sharp further away.
    /// - Wind for painted grass, ferns and weeds, using the terrain's own batched detail rendering (cheap on the GPU).
    ///
    /// Created automatically by IshanaBootstrap. Add it to a scene object yourself to tune it.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Ishana/Terrain Polish")]
    public class TerrainPolish : MonoBehaviour
    {
        [Header("Ground Textures")]
        [Tooltip("Draw the painted terrain layers with Unity's terrain material instead of the single stretched texture.")]
        public bool useLayeredTerrainMaterial = true;
        [Tooltip("Optional: your own terrain material. Empty = the standard URP terrain material.")]
        public Material terrainMaterial;
        [Tooltip("How large one repeat of a ground texture is, in player heights. Smaller = finer detail but more visible repetition. 0 = leave as painted.")]
        public float textureTileInPlayerHeights = 3f;
        [Tooltip("Ground textures stay fully detailed out to this distance.")]
        public float fullDetailTextureDistance = 300f;

        [Header("Shape")]
        [Tooltip("Lower = the terrain keeps its true shape further away (crisper silhouettes), but many more triangles. Unity's default is 5. " +
                 "A terrain is never made coarser than it already is.")]
        [Range(1f, 10f)] public float pixelError = 5f;
        [Tooltip("Draw the terrain with GPU instancing (fewer draw calls). Off = leave each terrain's own setting alone.")]
        public bool drawInstanced = false;

        [Header("Painted Grass, Ferns and Weeds")]
        [Tooltip("Set the terrain's wind so painted grass sways.")]
        public bool windOnPaintedPlants = true;
        [Tooltip("Painted MESH plants (ferns, weeds, grass planes) sway too. They are drawn with the Ishana/Wind Plant shader " +
                 "using GPU instancing, which is cheap. Your prefabs and materials are not changed: copies are made when the game starts. " +
                 "Off by default: on a densely painted terrain, drawing every plant as its own instance was far slower than " +
                 "the terrain's normal batched drawing and dropped the frame rate badly. Only turn on for sparse plants.")]
        public bool windOnMeshPlants = false;
        [Tooltip("How far the top of a mesh plant sways, as a fraction of its height.")]
        [Range(0f, 0.5f)] public float meshPlantSway = 0.12f;
        [Tooltip("Brightness of mesh plants compared to the scene lighting.")]
        [Range(0.2f, 2f)] public float meshPlantBrightness = 1f;

        readonly System.Collections.Generic.Dictionary<GameObject, GameObject> _windCopies = new System.Collections.Generic.Dictionary<GameObject, GameObject>();
        readonly System.Collections.Generic.List<Material> _windMaterials = new System.Collections.Generic.List<Material>();
        Shader _windShader;
        readonly System.Collections.Generic.Dictionary<Terrain, Material> _originalMaterials = new System.Collections.Generic.Dictionary<Terrain, Material>();
        readonly System.Collections.Generic.Dictionary<Terrain, Material> _layeredMaterials = new System.Collections.Generic.Dictionary<Terrain, Material>();

        /// <summary>Switch between the layered ground and the terrain's original material (used by the performance panel).</summary>
        public void SetLayered(bool layered)
        {
            foreach (var pair in layered ? _layeredMaterials : _originalMaterials)
                if (pair.Key != null) pair.Key.materialTemplate = pair.Value;
        }

        float _nextLightUpdate;
        [Range(0f, 1f)] public float windStrength = 0.45f;
        [Range(0f, 1f)] public float windSpeed = 0.4f;
        [Range(0f, 1f)] public float windBending = 0.5f;
        [Tooltip("Painted plants are drawn out to this many player heights away. Lower = cheaper. 0 = keep each terrain's own distance.")]
        public float plantDistanceInPlayerHeights = 0f;
        [Tooltip("How many of the painted plants are drawn (1 = all). Lower = cheaper. The painted grass is made of large " +
                 "see-through planes that overlap heavily, which is expensive to draw, so a bit less of it goes a long way.")]
        [Range(0.1f, 1f)] public float plantDensity = 0.65f;

        void Start()
        {
            float playerHeight = 2f;
            var player = FindFirstObjectByType<PlayerStateManager>();
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) playerHeight = Mathf.Max(0.5f, cc.height * player.transform.lossyScale.y);
            }

            Material layered = null;
            if (useLayeredTerrainMaterial)
            {
                layered = terrainMaterial;
                if (layered == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                    if (shader != null) layered = new Material(shader) { name = "Ishana Terrain (runtime)", enableInstancing = true };
                    else Debug.LogWarning("TerrainPolish: URP terrain shader not found, keeping the current terrain material.", this);
                }
            }

            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                TerrainData data = TerrainRuntime.Writable(terrain);

                // --- Ground textures
                TerrainLayer[] layers = data.terrainLayers;
                bool hasLayers = layers != null && layers.Length > 0 && layers[0] != null;
                if (layered != null && hasLayers)
                {
                    _originalMaterials[terrain] = terrain.materialTemplate;
                    _layeredMaterials[terrain] = layered;
                    terrain.materialTemplate = layered;
                }

                if (hasLayers && textureTileInPlayerHeights > 0f)
                {
                    float tile = textureTileInPlayerHeights * playerHeight;
                    var copies = new TerrainLayer[layers.Length];
                    for (int i = 0; i < layers.Length; i++)
                    {
                        if (layers[i] == null) continue;
                        copies[i] = Instantiate(layers[i]); // Copy, so the layer assets keep their own tiling
                        copies[i].name = layers[i].name;    // Keep the name: other systems look layers up by it
                        copies[i].tileSize = new Vector2(tile, tile);
                        copies[i].tileOffset = Vector2.zero;
                    }
                    data.terrainLayers = copies;
                }

                // Past this distance the terrain is drawn from one pre-mixed texture, which is much cheaper
                if (_layeredMaterials.ContainsKey(terrain)) terrain.basemapDistance = fullDetailTextureDistance;

                // --- Shape
                terrain.heightmapPixelError = Mathf.Min(terrain.heightmapPixelError, pixelError);
                if (drawInstanced) terrain.drawInstanced = true;

                // --- Painted plants
                if (plantDistanceInPlayerHeights > 0f)
                    terrain.detailObjectDistance = Mathf.Min(250f, plantDistanceInPlayerHeights * playerHeight);
                terrain.detailObjectDensity = plantDensity;

                if (windOnPaintedPlants)
                {
                    data.wavingGrassStrength = windStrength;
                    data.wavingGrassSpeed = windSpeed;
                    data.wavingGrassAmount = windBending;

                    // Mesh plants only sway in "Grass" mode; that mode is also drawn in big batches
                    if (windOnMeshPlants) ApplyWindToMeshPlants(data);
                }

                terrain.Flush();
            }
        }

        /// <summary>
        /// Swap each painted mesh plant for a copy that uses the swaying, instanced plant shader.
        /// A plant is left exactly as it was if anything about it doesn't fit.
        /// </summary>
        void ApplyWindToMeshPlants(TerrainData data)
        {
            if (_windShader == null) _windShader = Resources.Load<Shader>("IshanaWindPlant");
            if (_windShader == null) _windShader = Shader.Find("Ishana/Wind Plant");
            if (_windShader == null || !_windShader.isSupported)
            {
                Debug.LogWarning("TerrainPolish: wind plant shader missing or unsupported, painted mesh plants stay still.", this);
                return;
            }

            DetailPrototype[] details = data.detailPrototypes;
            bool changed = false;
            for (int i = 0; i < details.Length; i++)
            {
                DetailPrototype original = details[i];
                if (!original.usePrototypeMesh || original.prototype == null) continue;

                GameObject copy = WindCopy(original.prototype);
                if (copy == null) continue;

                var swapped = new DetailPrototype(original)
                {
                    prototype = copy,
                    renderMode = DetailRenderMode.VertexLit,
                    useInstancing = true,
                };
                if (!swapped.Validate(out string problem))
                {
                    Debug.LogWarning($"TerrainPolish: '{original.prototype.name}' can't use the wind shader ({problem}); leaving it as it is.", this);
                    continue;
                }
                details[i] = swapped;
                changed = true;
            }
            if (changed) data.detailPrototypes = details;
        }

        GameObject WindCopy(GameObject prefab)
        {
            if (_windCopies.TryGetValue(prefab, out GameObject existing)) return existing;
            _windCopies[prefab] = null; // Remember failures too

            // The terrain needs the mesh and renderer on the top object of the plant
            var sourceRenderer = prefab.GetComponent<MeshRenderer>();
            var sourceFilter = prefab.GetComponent<MeshFilter>();
            if (sourceRenderer == null || sourceFilter == null || sourceFilter.sharedMesh == null || sourceRenderer.sharedMaterial == null) return null;

            Material source = sourceRenderer.sharedMaterial;
            Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
            if (texture == null && source.HasProperty("_MainTex")) texture = source.GetTexture("_MainTex");
            Color tint = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                       : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            bool cutout = source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f;
            float cutoff = cutout && source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0f;

            var material = new Material(_windShader) { name = source.name + " (wind)", enableInstancing = true };
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", new Color(tint.r, tint.g, tint.b, 1f));
            material.SetFloat("_Cutoff", cutoff);

            var copy = new GameObject(prefab.name + " (wind copy)");
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.transform.SetParent(transform, false);
            copy.transform.localPosition = Vector3.down * 10000f; // Out of sight; it only exists for the terrain to copy from
            copy.transform.localRotation = prefab.transform.localRotation;
            copy.transform.localScale = prefab.transform.localScale;
            copy.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            // Sway is measured against the plant's real height
            float height = Mathf.Max(0.05f, renderer.bounds.size.y);
            material.SetFloat("_BendHeight", height);
            material.SetFloat("_WindStrength", height * meshPlantSway * Mathf.Lerp(0.3f, 1.5f, windStrength));
            material.SetFloat("_WindSpeed", Mathf.Lerp(0.6f, 3f, windSpeed));
            renderer.enabled = false;

            _windMaterials.Add(material);
            _windCopies[prefab] = copy;
            return copy;
        }

        void Update()
        {
            // The plant shader is unlit to stay cheap, so feed it the scene's light a couple of times a second
            if (_windMaterials.Count == 0 || Time.unscaledTime < _nextLightUpdate) return;
            _nextLightUpdate = Time.unscaledTime + 0.5f;

            Color light = RenderSettings.ambientLight;
            Light sun = RenderSettings.sun;
            if (sun != null && sun.isActiveAndEnabled) light += sun.color * Mathf.Min(sun.intensity, 2f) * 0.6f;
            light *= meshPlantBrightness;
            light = new Color(Mathf.Clamp(light.r, 0.05f, 1.6f), Mathf.Clamp(light.g, 0.05f, 1.6f), Mathf.Clamp(light.b, 0.05f, 1.6f), 1f);
            foreach (Material m in _windMaterials)
                if (m != null) m.SetColor("_PlantLight", light);
        }

        void OnDestroy()
        {
            foreach (Material m in _windMaterials) if (m != null) Destroy(m);
            foreach (GameObject copy in _windCopies.Values) if (copy != null) Destroy(copy);
        }
    }
}
