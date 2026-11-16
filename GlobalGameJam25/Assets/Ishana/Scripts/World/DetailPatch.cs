using Ishana.Elements;
using UnityEngine;

namespace Ishana.World
{
    /// <summary>One patch of painted plants that fire has reached. Thins the plants out as it burns.</summary>
    public class DetailPatch : MonoBehaviour
    {
        Terrain _terrain;
        Flammable _plant;
        int _x0, _y0;
        int[][,] _originals;
        int _shownStep; // 0 = untouched ... 3 = burnt away

        public void Init(Terrain terrain, Flammable plant, int x0, int y0, int[][,] originals)
        {
            _terrain = terrain;
            _plant = plant;
            _x0 = x0;
            _y0 = y0;
            _originals = originals;
        }

        void Update()
        {
            if (_plant == null || _terrain == null) return;

            // Redrawing the terrain's plants is not free, so only do it at a few steps of the burn
            float burn = _plant.BurnAmount;
            int step = burn >= 0.95f ? 3 : burn >= 0.6f ? 2 : burn >= 0.25f ? 1 : 0;
            if (step == _shownStep) return;
            _shownStep = step;

            float keep = 1f - step / 3f;
            TerrainData data = TerrainRuntime.Writable(_terrain);
            for (int l = 0; l < _originals.Length; l++)
            {
                int[,] source = _originals[l];
                int h = source.GetLength(0), w = source.GetLength(1);
                var now = new int[h, w];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        now[y, x] = Mathf.RoundToInt(source[y, x] * keep);
                data.SetDetailLayer(_x0, _y0, l, now);
            }
        }
    }
}
