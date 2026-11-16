using UnityEditor;
using UnityEngine;

namespace Ishana.EditorTools
{
    /// <summary>
    /// Menu: Ishana > Terrain > Smooth...
    /// Softens the jagged, lumpy bits of the terrain shape while keeping hills and valleys where they are.
    /// Works on the selected terrains, or every terrain in the scene if none is selected. Fully undoable
    /// (Ctrl+Z). Edges between neighbouring terrains are left untouched so no seams open up.
    /// </summary>
    public static class TerrainSmoothingTools
    {
        [MenuItem("Ishana/Terrain/Smooth Shape (gentle)")]
        static void Gentle() => Smooth(2, 0.5f);

        [MenuItem("Ishana/Terrain/Smooth Shape (strong)")]
        static void Strong() => Smooth(6, 0.8f);

        static void Smooth(int passes, float strength)
        {
            Terrain[] terrains = Selection.GetFiltered<Terrain>(SelectionMode.Deep);
            if (terrains == null || terrains.Length == 0) terrains = Terrain.activeTerrains;
            if (terrains == null || terrains.Length == 0)
            {
                EditorUtility.DisplayDialog("Smooth Terrain", "No terrain found in the open scene.", "OK");
                return;
            }

            foreach (Terrain terrain in terrains)
            {
                TerrainData data = terrain.terrainData;
                if (data == null) continue;
                Undo.RegisterCompleteObjectUndo(data, "Smooth Terrain Shape");

                int res = data.heightmapResolution;
                float[,] heights = data.GetHeights(0, 0, res, res);
                var buffer = new float[res, res];
                const int border = 12; // Fade the effect out toward the edges so neighbouring terrains still line up

                for (int pass = 0; pass < passes; pass++)
                {
                    for (int y = 0; y < res; y++)
                        for (int x = 0; x < res; x++)
                        {
                            int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(res - 1 - x, res - 1 - y));
                            if (edge == 0) { buffer[y, x] = heights[y, x]; continue; }

                            float sum = 0f;
                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                    sum += heights[y + dy, x + dx];
                            float average = sum / 9f;
                            float weight = strength * Mathf.Clamp01(edge / (float)border);
                            buffer[y, x] = Mathf.Lerp(heights[y, x], average, weight);
                        }
                    (heights, buffer) = (buffer, heights);
                }

                data.SetHeights(0, 0, heights);
                EditorUtility.SetDirty(data);
            }
            Debug.Log($"Smoothed {terrains.Length} terrain(s). Undo with Ctrl+Z.");
        }
    }
}
