using TMPro;
using UnityEngine;

namespace Ishana.Combat
{
    /// <summary>
    /// A pooled, world-space floating damage number. Created and recycled by CombatFeedback.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        const float Lifetime = 0.8f;
        const float RiseSpeed = 2.2f;

        static Material _outlinedMaterial;

        TextMeshPro _text;
        float _spawnTime;
        float _baseSize;
        Color _color;
        Vector3 _velocity;

        public static DamageNumber Create(Transform parent)
        {
            var go = new GameObject("DamageNumber");
            go.transform.SetParent(parent, false);
            var number = go.AddComponent<DamageNumber>();
            number._text = go.AddComponent<TextMeshPro>();
            number._text.alignment = TextAlignmentOptions.Center;
            number._text.fontStyle = FontStyles.Bold;
            number._text.textWrappingMode = TextWrappingModes.NoWrap;
            number._text.sortingOrder = 100;

            // One shared outlined material for all numbers (readable against bright skies and dark forest)
            if (_outlinedMaterial == null && number._text.font != null)
            {
                _outlinedMaterial = new Material(number._text.font.material) { name = "DamageNumber (Outlined)" };
                _outlinedMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
                _outlinedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
                _outlinedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.85f));
            }
            if (_outlinedMaterial != null) number._text.fontSharedMaterial = _outlinedMaterial;

            go.SetActive(false);
            return number;
        }

        public void Show(Vector3 position, string label, Color color, float size)
        {
            transform.position = position;
            _text.text = label;
            _color = color;
            _text.color = color;
            _baseSize = size;
            _text.fontSize = size;
            _spawnTime = Time.unscaledTime;
            _velocity = new Vector3(Random.Range(-0.6f, 0.6f), RiseSpeed, 0f);
            gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            float age = Time.unscaledTime - _spawnTime;
            if (age >= Lifetime) { gameObject.SetActive(false); return; }
            float t = age / Lifetime;

            // Pop in (overshoot), then drift up and fade
            float pop = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f;
            _text.fontSize = _baseSize * pop;
            transform.position += _velocity * Time.unscaledDeltaTime;
            _velocity.y = Mathf.Max(0.3f, _velocity.y - 4f * Time.unscaledDeltaTime);

            var c = _color;
            c.a = t < 0.6f ? 1f : Mathf.InverseLerp(1f, 0.6f, t);
            _text.color = c;

            // Billboard toward the camera
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
        }
    }
}
