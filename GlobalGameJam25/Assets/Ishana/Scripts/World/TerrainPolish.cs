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
        public float fullDetailTextureDistance = 2000f;

        [Header("Shape")]
        [Tooltip("Lower = the terrain keeps its true shape further away (crisper silhouettes), at some CPU/GPU cost. Unity's default is 5.")]
        [Range(1f, 10f)] public float pixelError = 2f;
        [Tooltip("Draw the terrain with GPU instancing (fewer draw calls).")]
        public bool drawInstanced = true;

        [Header("Painted Grass, Ferns and Weeds")]
        [Tooltip("Make painted plants sway in the wind.")]
        public bool windOnPaintedPlants = true;
        [Range(0f, 1f)] public float windStrength = 0.45f;
        [Range(0f, 1f)] public float windSpeed = 0.4f;
        [Range(0f, 1f)] public float windBending = 0.5f;
        [Tooltip("Painted plants are drawn out to this many player heights away. Lower = cheaper.")]
        public float plantDistanceInPlayerHeights = 60f;
        [Tooltip("How many of the painted plants are drawn (1 = all). Lower = cheaper.")]
        [Range(0.1f, 1f)] public float plantDensity = 1f;

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
                    if (shader != null) layered = new Material(shader) { name = "Ishana Terrain (runtime)" };
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
                if (layered != null && hasLayers) terrain.materialTemplate = layered;

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

                terrain.basemapDistance = Mathf.Max(terrain.basemapDistance, fullDetailTextureDistance);

                // --- Shape
                terrain.heightmapPixelError = Mathf.Min(terrain.heightmapPixelError, pixelError);
                terrain.drawInstanced = drawInstanced;

                // --- Painted plants
                terrain.detailObjectDistance = Mathf.Min(250f, plantDistanceInPlayerHeights * playerHeight);
                terrain.detailObjectDensity = plantDensity;

                if (windOnPaintedPlants)
                {
                    data.wavingGrassStrength = windStrength;
                    data.wavingGrassSpeed = windSpeed;
                    data.wavingGrassAmount = windBending;

                    // Mesh plants only sway in "Grass" mode; that mode is also drawn in big batches
                    DetailPrototype[] details = data.detailPrototypes;
                    bool changed = false;
                    for (int i = 0; i < details.Length; i++)
                    {
                        if (!details[i].usePrototypeMesh) continue;
                        if (details[i].renderMode == DetailRenderMode.Grass && !details[i].useInstancing) continue;
                        details[i].renderMode = DetailRenderMode.Grass;
                        details[i].useInstancing = false;
                        changed = true;
                    }
                    if (changed) data.detailPrototypes = details;
                }

                terrain.Flush();
            }
        }
    }
}
