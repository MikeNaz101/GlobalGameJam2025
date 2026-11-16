using UnityEngine;

namespace Ishana.Elements
{
    /// <summary>
    /// Builds the fire, smoke, steam and sparkle effects in code, so no particle prefabs are needed.
    /// </summary>
    public static class ElementFX
    {
        static Material _particleMaterial;
        static Texture2D _softDot;

        public static Material ParticleMaterial
        {
            get
            {
                if (_particleMaterial != null) return _particleMaterial;

                // Soft round dot
                const int size = 32;
                _softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SoftDot (runtime)", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        _softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                _softDot.Apply();

                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                _particleMaterial = new Material(shader) { name = "Element Particles (runtime)", mainTexture = _softDot };
                return _particleMaterial;
            }
        }

        static ParticleSystem Create(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // Emit upward

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            return ps;
        }

        static Gradient Fade(Color start, Color end, float peakAlpha)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.15f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>Looping flames filling a radius/height. Call Play() on the result.</summary>
        public static ParticleSystem Flames(Transform parent, float radius, float height)
        {
            var ps = Create("Flames", parent, Vector3.zero);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 0.6f, height * 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.5f, radius * 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 26f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = Mathf.Max(0.05f, radius * 0.7f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = Fade(new Color(1f, 0.85f, 0.35f), new Color(0.9f, 0.15f, 0.02f), 0.9f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            return ps;
        }

        /// <summary>Looping smoke. Call Play() on the result.</summary>
        public static ParticleSystem Smoke(Transform parent, float radius, float height, float rate = 5f)
        {
            var ps = Create("Smoke", parent, Vector3.up * height * 0.5f);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 0.5f, height * 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 1.2f, radius * 2.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = Mathf.Max(0.05f, radius * 0.5f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = Fade(new Color(0.18f, 0.16f, 0.15f), new Color(0.4f, 0.4f, 0.4f), 0.45f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            return ps;
        }

        /// <summary>One-shot puff (steam, dust, healing sparkles). Cleans itself up.</summary>
        public static void Burst(Vector3 position, Color start, Color end, float radius, float rise, int count = 18, float lifetime = 1.2f)
        {
            var ps = Create("Burst", null, Vector3.zero);
            ps.transform.position = position;

            var main = ps.main;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(rise * 0.4f, rise);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.35f, radius * 0.8f);
            main.maxParticles = count;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.05f, radius * 0.6f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = Fade(start, end, 0.8f);

            ps.Play();
        }

        public static void Steam(Vector3 position, float radius) =>
            Burst(position, new Color(0.85f, 0.95f, 1f), Color.white, radius, radius * 2f, 20, 1.4f);

        public static void Dust(Vector3 position, float radius) =>
            Burst(position, new Color(0.55f, 0.5f, 0.42f), new Color(0.7f, 0.66f, 0.6f), radius, radius * 0.8f, 16, 1.1f);

        public static void Heal(Vector3 position, float radius) =>
            Burst(position, new Color(0.5f, 1f, 0.45f), new Color(0.95f, 1f, 0.6f), radius, radius * 1.5f, 22, 1.6f);
    }
}
