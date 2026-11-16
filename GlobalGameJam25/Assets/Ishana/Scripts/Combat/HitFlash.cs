using UnityEngine;

namespace Ishana.Combat
{
    /// <summary>
    /// Briefly tints every renderer on an object toward a flash color when it is hit.
    /// Added automatically by CombatFeedback; uses MaterialPropertyBlocks so no materials are duplicated.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitFlash : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit / Simple Lit / Unlit
        static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in / most custom shaders

        Renderer[] _renderers;
        Color[][] _originalColors;
        MaterialPropertyBlock _block;
        float _flashStart = -1f;
        float _duration = 0.1f;
        Color _flashColor = Color.white;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            CacheRenderers();
        }

        void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _originalColors = new Color[_renderers.Length][];
            for (int r = 0; r < _renderers.Length; r++)
            {
                var mats = _renderers[r].sharedMaterials;
                _originalColors[r] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) { _originalColors[r][m] = Color.white; continue; }
                    if (mat.HasProperty(BaseColorId)) _originalColors[r][m] = mat.GetColor(BaseColorId);
                    else if (mat.HasProperty(ColorId)) _originalColors[r][m] = mat.GetColor(ColorId);
                    else _originalColors[r][m] = Color.white;
                }
            }
        }

        public void Flash(Color color, float duration)
        {
            _flashColor = color;
            _duration = Mathf.Max(0.01f, duration);
            _flashStart = Time.unscaledTime; // Unscaled so the flash still plays during hit-stop
            enabled = true;
        }

        void LateUpdate()
        {
            if (_flashStart < 0f) { enabled = false; return; }

            float t = (Time.unscaledTime - _flashStart) / _duration;
            if (t >= 1f)
            {
                ClearBlocks();
                _flashStart = -1f;
                enabled = false;
                return;
            }
            Apply(1f - t);
        }

        void ClearBlocks()
        {
            _block.Clear();
            for (int r = 0; r < _renderers.Length; r++)
            {
                var rend = _renderers[r];
                if (rend == null) continue;
                for (int m = 0; m < _originalColors[r].Length; m++) rend.SetPropertyBlock(_block, m);
            }
        }

        void Apply(float amount)
        {
            for (int r = 0; r < _renderers.Length; r++)
            {
                var rend = _renderers[r];
                if (rend == null || rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer) continue;

                var originals = _originalColors[r];
                for (int m = 0; m < originals.Length; m++)
                {
                    rend.GetPropertyBlock(_block, m);
                    Color c = Color.Lerp(originals[m], _flashColor, amount);
                    _block.SetColor(BaseColorId, c);
                    _block.SetColor(ColorId, c);
                    rend.SetPropertyBlock(_block, m);
                }
            }
        }
    }
}
