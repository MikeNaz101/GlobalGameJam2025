using Ishana.Elements;
using UnityEngine;

namespace Ishana.World
{
    /// <summary>One patch of painted plants that fire has reached. Thins the plants out as it burns.</summary>
    public class DetailPatch : MonoBehaviour
    {
        /// <summary>
        /// Most plant redraws allowed in one frame, across all patches. Redrawing the terrain's plants is
        /// the expensive part of a grass fire, so when many patches burn at once they take turns.
        /// </summary>
        public static int RedrawsPerFrame = 2;
        static int _frame = -1, _used;

        Terrain _terrain;
        Flammable _plant;
        int _x0, _y0;
        int[] _layers;       // Which of the terrain's plant kinds grow in this patch
        int[][,] _originals; // How much of each there was before the fire
        int _shownStep;      // 0 = untouched, 1 = half burnt away, 2 = gone

        public void Init(Terrain terrain, Flammable plant, int x0, int y0, int[] layers, int[][,] originals)
        {
            _terrain = terrain;
            _plant = plant;
            _x0 = x0;
            _y0 = y0;
            _layers = layers;
            _originals = originals;
        }

        void Update()
        {
            if (_plant == null || _terrain == null) return;

            float burn = _plant.BurnAmount;
            int step = burn >= 0.9f ? 2 : burn >= 0.4f ? 1 : 0;
            if (step == _shownStep) return;

            // Take turns: wait for a later frame if this frame's redraws are used up
            if (_frame != Time.frameCount) { _frame = Time.frameCount; _used = 0; }
            if (_used > 0 && _used + _layers.Length > RedrawsPerFrame) return;
            _used += _layers.Length;
            _shownStep = step;

            float keep = 1f - step / 2f;
            TerrainData data = TerrainRuntime.Writable(_terrain);
            for (int l = 0; l < _originals.Length; l++)
            {
                int[,] source = _originals[l];
                int h = source.GetLength(0), w = source.GetLength(1);
                var now = new int[h, w];
                if (keep > 0f)
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            now[y, x] = Mathf.RoundToInt(source[y, x] * keep);
                data.SetDetailLayer(_x0, _y0, _layers[l], now);
            }
        }
    }
}
