using System.Collections;
using UnityEngine;

namespace Ishana.Elements
{
    /// <summary>
    /// Ground raised by earth magic: a solid, volcano-shaped mound that pushes up out of the ground,
    /// can be stood on (and lifts the player if they are on the spot), then crumbles to nothing.
    /// </summary>
    public class EarthMound : MonoBehaviour
    {
        public static Color RockColor = new Color(0.36f, 0.3f, 0.24f);

        float _radius, _height, _lifetime, _riseTime, _crumbleTime;
        CharacterController _rider;
        Renderer _renderer;
        Collider _collider;

        /// <summary>Raise a mound at a point on the ground.</summary>
        public static EarthMound Raise(Vector3 point, float radius, float height, float lifetime, PlayerStateManager player,
                                       float riseTime = 0.4f, float crumbleTime = 1.2f)
        {
            var go = new GameObject("Earth Mound");
            go.transform.position = point + Vector3.down * height;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = new Vector3(radius, height, radius);

            Mesh mesh = ProceduralMeshes.Mound(Random.Range(0, ProceduralMeshes.Variants));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ProceduralMeshes.Matte("Raised Earth", RockColor);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            var mound = go.AddComponent<EarthMound>();
            mound._radius = radius;
            mound._height = height;
            mound._lifetime = lifetime;
            mound._riseTime = Mathf.Max(0.05f, riseTime);
            mound._crumbleTime = crumbleTime;
            mound._renderer = renderer;
            mound._collider = collider;
            mound._rider = player != null ? player.GetComponent<CharacterController>() : null;
            mound.StartCoroutine(mound.Life(point));
            return mound;
        }

        IEnumerator Life(Vector3 groundPoint)
        {
            ElementFX.Dust(groundPoint, _radius);
            Combat.CombatFeedback.Shake(0.2f);

            // --- Rise out of the ground
            Vector3 from = transform.position, to = groundPoint;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / _riseTime);
                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                Physics.SyncTransforms();
                LiftRider();
                yield return null;
            }

            // --- Stand solid
            yield return new WaitForSeconds(Mathf.Max(0f, _lifetime));

            // --- Crumble: shake and shed dust and chunks, then fall apart
            Bounds bounds = _renderer.bounds;
            Material material = _renderer.sharedMaterial;
            Collider[] riderColliders = _rider != null ? _rider.GetComponentsInChildren<Collider>() : null;
            Vector3 rest = transform.position;
            float end = Time.time + _crumbleTime, nextShed = 0f;
            while (Time.time < end)
            {
                float k = 1f - (end - Time.time) / Mathf.Max(0.01f, _crumbleTime);
                Vector3 jitter = Random.insideUnitSphere * _radius * Mathf.Lerp(0.005f, 0.03f, k);
                jitter.y = 0f; // Don't bounce whoever is standing on it
                transform.position = rest + jitter;
                if (Time.time >= nextShed)
                {
                    nextShed = Time.time + 0.2f;
                    Vector3 p = bounds.center + Vector3.Scale(Random.insideUnitSphere, bounds.extents);
                    ElementFX.Dust(p, _radius * 0.3f);
                    RockDebris.Spawn(new Bounds(p, Vector3.one * _radius * 0.3f), material, 1, 1.5f, riderColliders);
                }
                yield return null;
            }

            _renderer.enabled = false;
            _collider.enabled = false;
            ElementFX.Dust(bounds.center, _radius * 0.8f);
            // Only the part above ground turns into rubble
            var above = new Bounds(groundPoint + Vector3.up * _height * 0.5f, new Vector3(_radius * 1.2f, _height, _radius * 1.2f));
            RockDebris.Spawn(above, material, 9, 2.5f, riderColliders);
            Destroy(gameObject);
        }

        /// <summary>Carry the player up if the mound is rising underneath them.</summary>
        void LiftRider()
        {
            if (_rider == null || !_rider.enabled) return;

            Vector3 feet = _rider.bounds.center + Vector3.down * _rider.bounds.extents.y;
            Vector3 flat = feet - transform.position;
            flat.y = 0f;
            if (flat.magnitude > _radius * 0.75f) return; // Not on top of it

            float top = transform.position.y + _height * 0.9f; // Crater floor
            if (feet.y < top && feet.y > top - _height * 1.5f)
                _rider.Move(Vector3.up * (top - feet.y + 0.02f));
        }
    }
}
