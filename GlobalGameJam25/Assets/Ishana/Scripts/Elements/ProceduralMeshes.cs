using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ishana.Elements
{
    /// <summary>
    /// Low-poly meshes built in code: rocks, tree trunks, leafy and pine crowns, bushes and grass tufts.
    /// Each shape has a handful of variants that are built once and shared by every object that uses them.
    /// All shapes are about 1 unit in size and sit on the ground at y = 0; scale the object to size it.
    /// </summary>
    public static class ProceduralMeshes
    {
        public const int Variants = 4;

        static readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        static List<Vector3> _icoLow, _icoHigh;

        // ------------------------------------------------------------------ public shapes

        /// <summary>Lumpy rock, radius about 1, centred on the origin.</summary>
        public static Mesh Rock(int variant) => Get("rock", variant, (b, seed) =>
            b.AddBlob(Ico(true), Vector3.zero, new Vector3(1f, 0.8f, 1f), seed, 0.28f, 1.6f));

        /// <summary>Small sharp rock fragment for debris.</summary>
        public static Mesh Chunk(int variant) => Get("chunk", variant, (b, seed) =>
            b.AddBlob(Ico(false), Vector3.zero, new Vector3(1f, 0.75f, 0.9f), seed, 0.4f, 1.2f));

        /// <summary>Tapered trunk from y = 0 to y = 1, radius 0.5 at the base.</summary>
        public static Mesh Trunk() => Get("trunk", 0, (b, seed) => b.AddCone(Vector3.zero, 0.5f, 0.28f, 1f, 6, true));

        /// <summary>Leafy crown: a cluster of blobs about 1 unit in radius, centred a little above the origin.</summary>
        public static Mesh LeafCrown(int variant) => Get("leaf", variant, (b, seed) =>
        {
            var rng = new System.Random(seed * 7919 + 13);
            b.AddBlob(Ico(false), new Vector3(0f, 0.25f, 0f), new Vector3(0.85f, 0.75f, 0.85f), seed, 0.2f, 1.3f);
            int extra = 2 + variant % 2;
            for (int i = 0; i < extra; i++)
            {
                float angle = (i / (float)extra + (float)rng.NextDouble() * 0.2f) * Mathf.PI * 2f;
                float size = 0.45f + (float)rng.NextDouble() * 0.2f;
                var pos = new Vector3(Mathf.Cos(angle) * 0.5f, -0.05f + (float)rng.NextDouble() * 0.35f, Mathf.Sin(angle) * 0.5f);
                b.AddBlob(Ico(false), pos, Vector3.one * size, seed + i + 1, 0.2f, 1.3f);
            }
        });

        /// <summary>Pine crown: stacked cones from y = 0 to about y = 1, radius about 0.5.</summary>
        public static Mesh PineCrown(int variant) => Get("pine", variant, (b, seed) =>
        {
            int tiers = 3 + variant % 2;
            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)tiers;
                float radius = Mathf.Lerp(0.5f, 0.2f, t);
                float height = 1.6f / tiers;
                b.AddCone(new Vector3(0f, t * 0.8f, 0f), radius, 0f, height * 0.75f, 7, false);
            }
        });

        /// <summary>Low rounded bush, radius about 1, sitting on the ground.</summary>
        public static Mesh Bush(int variant) => Get("bush", variant, (b, seed) =>
        {
            b.AddBlob(Ico(false), new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.55f, 0.8f), seed, 0.25f, 1.4f);
            b.AddBlob(Ico(false), new Vector3(0.45f, 0.3f, 0.2f), Vector3.one * 0.45f, seed + 3, 0.25f, 1.4f);
            b.AddBlob(Ico(false), new Vector3(-0.4f, 0.28f, -0.25f), Vector3.one * 0.4f, seed + 5, 0.25f, 1.4f);
        });

        /// <summary>Tuft of grass blades (or broader weed leaves), about 1 unit tall and 1 unit across.</summary>
        public static Mesh GrassTuft(int variant, bool weed = false) => Get(weed ? "weed" : "grass", variant, (b, seed) =>
        {
            var rng = new System.Random(seed * 104729 + 7);
            int blades = weed ? 5 : 9;
            for (int i = 0; i < blades; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = (float)rng.NextDouble() * 0.4f;
                var root = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                float height = (weed ? 0.5f : 0.6f) + (float)rng.NextDouble() * 0.4f;
                float width = weed ? 0.22f : 0.08f;
                float facing = (float)rng.NextDouble() * Mathf.PI;
                var side = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing)) * width;
                // Blades lean outward a little
                var lean = new Vector3(root.x, 0f, root.z) * 0.6f + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.25f;
                b.AddBlade(root, side, Vector3.up * height + lean);
            }
        });

        /// <summary>
        /// Raised-earth mound ("solid volcano"): base radius 1, flat-ish top of radius 0.6 at y = 1 with a
        /// shallow crater, and a skirt reaching below y = 0 so it meets sloped ground.
        /// </summary>
        public static Mesh Mound(int variant) => Get("mound", variant, (b, seed) =>
        {
            const int sides = 10;
            var rng = new System.Random(seed * 3571 + 5);
            var low = new Vector3[sides]; var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                low[i] = dir * (1f + (float)rng.NextDouble() * 0.2f) + Vector3.down * 0.6f;
                top[i] = dir * (0.55f + (float)rng.NextDouble() * 0.12f) + Vector3.up * (0.97f + (float)rng.NextDouble() * 0.06f);
            }
            Vector3 crater = Vector3.up * 0.9f;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                b.AddTri(low[i], top[i], low[j]);
                b.AddTri(low[j], top[i], top[j]);
                b.AddTri(crater, top[j], top[i]);
            }
        });

        /// <summary>Flat hexagonal pad, radius 1, top at y = 0, 0.2 thick. Used for branch platforms.</summary>
        public static Mesh Pad() => Get("pad", 0, (b, seed) => b.AddCone(Vector3.down * 0.2f, 0.85f, 1f, 0.2f, 6, true));

        // ------------------------------------------------------------------ materials

        /// <summary>A plain matte material in the given colour (shared; one per key).</summary>
        public static Material Matte(string key, Color color)
        {
            if (_materials.TryGetValue(key, out Material existing) && existing != null) return existing;

            Material template = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultMaterial : null;
            Material m;
            if (template != null) m = new Material(template);
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
            }
            m.name = key + " (runtime)";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            m.enableInstancing = true;
            _materials[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ building

        delegate void Build(Builder b, int seed);

        static Mesh Get(string kind, int variant, Build build)
        {
            variant = ((variant % Variants) + Variants) % Variants;
            string key = kind + variant;
            if (_meshes.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;

            var builder = new Builder();
            build(builder, variant * 31 + kind.Length * 17 + 1);
            mesh = builder.ToMesh(key + " (runtime)");
            _meshes[key] = mesh;
            return mesh;
        }

        /// <summary>Triangles of a unit sphere made from an icosahedron (20 faces, or 80 when detailed).</summary>
        static List<Vector3> Ico(bool detailed)
        {
            if (_icoLow == null)
            {
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                Vector3[] v =
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                int[] f =
                {
                    0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                    3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
                };
                _icoLow = new List<Vector3>(60);
                for (int i = 0; i < f.Length; i++) _icoLow.Add(v[f[i]].normalized);

                _icoHigh = new List<Vector3>(240);
                for (int i = 0; i < _icoLow.Count; i += 3)
                {
                    Vector3 a = _icoLow[i], b = _icoLow[i + 1], c = _icoLow[i + 2];
                    Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + c) * 0.5f).normalized, ca = ((c + a) * 0.5f).normalized;
                    _icoHigh.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
            }
            return detailed ? _icoHigh : _icoLow;
        }

        class Builder
        {
            readonly List<Vector3> _verts = new List<Vector3>();
            readonly List<int> _tris = new List<int>();

            public void AddTri(Vector3 a, Vector3 b, Vector3 c) => Tri(a, b, c);

            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                // Every triangle gets its own corners, which gives the faceted low-poly look
                int n = _verts.Count;
                _verts.Add(a); _verts.Add(b); _verts.Add(c);
                _tris.Add(n); _tris.Add(n + 1); _tris.Add(n + 2);
            }

            static Vector3 Displace(Vector3 unit, int seed, float amount, float frequency)
            {
                float s = seed * 12.345f;
                float n = Mathf.PerlinNoise(unit.x * frequency + s + 31.7f, unit.y * frequency + s * 0.7f + 11.3f)
                        + Mathf.PerlinNoise(unit.y * frequency + s * 1.3f + 57.1f, unit.z * frequency + s + 91.9f);
                return unit * (1f + (n - 1f) * amount);
            }

            public void AddBlob(List<Vector3> sphere, Vector3 center, Vector3 scale, int seed, float amount, float frequency)
            {
                for (int i = 0; i < sphere.Count; i += 3)
                    Tri(center + Vector3.Scale(Displace(sphere[i], seed, amount, frequency), scale),
                        center + Vector3.Scale(Displace(sphere[i + 1], seed, amount, frequency), scale),
                        center + Vector3.Scale(Displace(sphere[i + 2], seed, amount, frequency), scale));
            }

            /// <summary>Cone or tapered cylinder standing on its base. topRadius 0 makes a point.</summary>
            public void AddCone(Vector3 baseCenter, float baseRadius, float topRadius, float height, int sides, bool cap)
            {
                Vector3 top = baseCenter + Vector3.up * height;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                    var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 b0 = baseCenter + d0 * baseRadius, b1 = baseCenter + d1 * baseRadius;

                    if (topRadius <= 0.0001f)
                    {
                        Tri(b0, top, b1);
                    }
                    else
                    {
                        Vector3 t0 = top + d0 * topRadius, t1 = top + d1 * topRadius;
                        Tri(b0, t0, b1);
                        Tri(b1, t0, t1);
                        if (cap) Tri(top, t1, t0);
                    }
                    Tri(baseCenter, b0, b1); // Underside
                }
            }

            /// <summary>One grass blade: a thin triangle visible from both sides.</summary>
            public void AddBlade(Vector3 root, Vector3 halfWidth, Vector3 tipOffset)
            {
                Vector3 a = root - halfWidth, b = root + halfWidth, tip = root + tipOffset;
                Tri(a, tip, b);
                Tri(b, tip, a);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_verts);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
