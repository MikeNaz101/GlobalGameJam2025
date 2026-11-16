using UnityEngine;

namespace Ishana.Elements
{
    /// <summary>
    /// A piece of a broken boulder. Tumbles with physics, then shrinks away and removes itself.
    /// </summary>
    public class RockDebris : MonoBehaviour
    {
        float _shrinkAt, _shrinkTime;
        Vector3 _startScale;

        void Update()
        {
            if (Time.time < _shrinkAt) return;
            float t = (Time.time - _shrinkAt) / Mathf.Max(0.05f, _shrinkTime);
            if (t >= 1f) { Destroy(gameObject); return; }
            transform.localScale = _startScale * (1f - t * t);
        }

        /// <summary>
        /// Fill a boulder's bounds with tumbling chunks that fade after a few seconds.
        /// </summary>
        public static void Spawn(Bounds bounds, Material material, int count, float lifetime, Collider[] ignore = null)
        {
            float size = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, Mathf.Min(bounds.extents.y, bounds.extents.z)));
            Vector3 center = bounds.center;
            if (material == null) material = ProceduralMeshes.Matte("Rock", new Color(0.45f, 0.44f, 0.42f));

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Rock Chunk");
                Vector3 offset = Vector3.Scale(Random.insideUnitSphere, bounds.extents * 0.8f);
                go.transform.position = center + offset;
                go.transform.rotation = Random.rotation;
                float chunk = size * Random.Range(0.25f, 0.55f);
                go.transform.localScale = Vector3.one * chunk;

                go.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Chunk(i);
                go.AddComponent<MeshRenderer>().sharedMaterial = material;

                var col = go.AddComponent<SphereCollider>();
                col.radius = 0.8f;
                if (ignore != null)
                    foreach (Collider other in ignore)
                        if (other != null) Physics.IgnoreCollision(col, other);

                var body = go.AddComponent<Rigidbody>();
                body.mass = Mathf.Max(0.5f, chunk * chunk * chunk * 20f);
                // Burst outward and slightly up, then let gravity take over
                Vector3 push = (offset.sqrMagnitude > 0.0001f ? offset.normalized : Random.onUnitSphere) + Vector3.up * 0.6f;
                body.linearVelocity = push * size * Random.Range(1.5f, 3.5f);
                body.angularVelocity = Random.insideUnitSphere * 6f;

                var debris = go.AddComponent<RockDebris>();
                debris._startScale = go.transform.localScale;
                debris._shrinkAt = Time.time + lifetime * Random.Range(0.6f, 1f);
                debris._shrinkTime = 0.8f;
            }
        }
    }
}
