using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ishana.Elements
{
    /// <summary>
    /// A plant that earth magic can make grow. It shoots up to a larger size and sprouts branch
    /// platforms that spiral up the trunk, so the player can jump from branch to branch to reach high places.
    /// Only plants with this component grow; the rest are just healed by earth magic.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Growable Plant")]
    public class GrowablePlant : MonoBehaviour
    {
        [Tooltip("How much bigger the plant gets.")]
        public float growFactor = 1.8f;
        [Tooltip("Seconds the growth takes.")]
        public float growTime = 1.5f;
        [Tooltip("Shrink back after this many seconds (0 = stays grown).")]
        public float shrinkAfter = 0f;

        [Header("Branch Platforms")]
        [Tooltip("Height of the first branch above the ground and the gap between branches. " +
                 "Keep it below the player's jump height. 0 = work it out from the player.")]
        public float branchSpacing = 0f;
        [Tooltip("Radius of each branch platform. 0 = work it out from the player's size.")]
        public float branchRadius = 0f;
        [Tooltip("Trunk radius at the base before growing. 0 = guess from the plant's size.")]
        public float trunkRadius = 0f;
        [Tooltip("Height of the plant before growing. 0 = measure it.")]
        public float plantHeight = 0f;
        public int maxBranches = 10;
        public Color branchColor = new Color(0.36f, 0.25f, 0.16f);
        public Color padColor = new Color(0.3f, 0.6f, 0.25f);

        public bool IsGrown { get; private set; }
        public bool IsGrowing { get; private set; }

        readonly List<GameObject> _branches = new List<GameObject>();
        Vector3 _baseScale;
        bool _scaleKnown;

        /// <summary>Grow now. Returns false if it is already grown.</summary>
        public bool Grow(bool instant = false)
        {
            if (IsGrown || IsGrowing) return false;
            if (!_scaleKnown) { _baseScale = transform.localScale; _scaleKnown = true; }

            if (instant)
            {
                transform.localScale = _baseScale * growFactor;
                IsGrown = true;
                var plant = GetComponent<Flammable>();
                if (plant != null) { plant.reachRadius *= growFactor; plant.centerHeight *= growFactor; }
                SproutBranches(instant: true);
                return true;
            }

            StartCoroutine(GrowRoutine());
            return true;
        }

        IEnumerator GrowRoutine()
        {
            IsGrowing = true;
            var plant = GetComponent<Flammable>();
            ElementFX.Heal(transform.position + Vector3.up * (plant != null ? plant.centerHeight : 1f), plant != null ? plant.reachRadius : 1f);

            Vector3 target = _baseScale * growFactor;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.05f, growTime));
                // Overshoot a little and settle, so it feels like it bursts upward
                float k = 1f - Mathf.Pow(1f - t, 3f);
                k += Mathf.Sin(t * Mathf.PI) * 0.06f;
                transform.localScale = Vector3.LerpUnclamped(_baseScale, target, k);
                yield return null;
            }
            transform.localScale = target;

            if (plant != null) { plant.reachRadius *= growFactor; plant.centerHeight *= growFactor; }
            IsGrowing = false;
            IsGrown = true;
            SproutBranches(instant: false);

            if (shrinkAfter > 0f)
            {
                yield return new WaitForSeconds(shrinkAfter);
                yield return ShrinkRoutine();
            }
        }

        IEnumerator ShrinkRoutine()
        {
            foreach (GameObject b in _branches) if (b != null) Destroy(b);
            _branches.Clear();

            Vector3 from = transform.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.05f, growTime));
                transform.localScale = Vector3.Lerp(from, _baseScale, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            var plant = GetComponent<Flammable>();
            if (plant != null) { plant.reachRadius /= growFactor; plant.centerHeight /= growFactor; }
            IsGrown = false;
        }

        void SproutBranches(bool instant)
        {
            // Sizes come from the player so the climb always works with their jump
            float size = 1f, jump = 2f;
            var player = FindFirstObjectByType<PlayerStateManager>();
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) size = Mathf.Max(0.25f, cc.height * player.transform.lossyScale.y / 2f);
                jump = Mathf.Clamp(player.jumpForce, size, size * 12f);
            }

            float height = (plantHeight > 0f ? plantHeight : MeasureHeight() / growFactor) * growFactor;
            float spacing = branchSpacing > 0f ? branchSpacing : jump * 0.6f;
            float padRadius = branchRadius > 0f ? branchRadius : size * 1.4f;
            float trunk = (trunkRadius > 0f ? trunkRadius : Mathf.Max(0.15f * size, height * 0.04f)) * growFactor;

            // Make sure even a short plant gets a few steps
            float usable = height * 0.85f;
            spacing = Mathf.Min(spacing, usable / 3f);
            int count = Mathf.Clamp(Mathf.FloorToInt(usable / Mathf.Max(0.01f, spacing)), 1, maxBranches);

            Material wood = ProceduralMeshes.Matte("Branch", branchColor);
            Material pad = ProceduralMeshes.Matte("Branch Pad", padColor);
            float angle = Random.Range(0f, 360f);
            Vector3 basePos = transform.position;

            for (int i = 0; i < count; i++)
            {
                angle += 105f; // Spiral, so each branch is offset from the one below
                var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                float y = spacing * (i + 1);
                float trunkHere = trunk * Mathf.Lerp(1f, 0.6f, y / Mathf.Max(0.01f, height));
                Vector3 padPos = basePos + Vector3.up * y + dir * (trunkHere + padRadius * 0.9f);

                // Branches are kept out of the tree's own hierarchy so its scale doesn't distort them
                var branch = new GameObject("Branch Platform");
                branch.transform.SetPositionAndRotation(padPos, Quaternion.Euler(0f, angle, 0f));
                branch.transform.localScale = new Vector3(padRadius, padRadius, padRadius);
                branch.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Pad();
                branch.AddComponent<MeshRenderer>().sharedMaterial = pad;
                branch.AddComponent<MeshCollider>().sharedMesh = ProceduralMeshes.Pad();

                // The limb joining the pad to the trunk
                var limb = new GameObject("Limb");
                limb.transform.SetParent(branch.transform, false);
                float limbLength = trunkHere + padRadius * 0.9f;
                limb.transform.position = padPos - Vector3.up * padRadius * 0.12f;
                limb.transform.rotation = Quaternion.FromToRotation(Vector3.up, -dir); // Trunk mesh runs along local Y
                limb.transform.localScale = new Vector3(0.22f, limbLength / padRadius, 0.22f);
                limb.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Trunk();
                limb.AddComponent<MeshRenderer>().sharedMaterial = wood;

                _branches.Add(branch);
                if (!instant) StartCoroutine(PopIn(branch.transform, i * 0.08f));
            }
        }

        static IEnumerator PopIn(Transform t, float delay)
        {
            Vector3 full = t.localScale;
            t.localScale = full * 0.05f;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float k = 0f;
            while (k < 1f && t != null)
            {
                k = Mathf.Min(1f, k + Time.deltaTime / 0.25f);
                t.localScale = full * Mathf.Max(0.05f, 1f - Mathf.Pow(1f - k, 2f));
                yield return null;
            }
        }

        float MeasureHeight()
        {
            float top = transform.position.y + 1f;
            bool found = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                top = found ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
                found = true;
            }
            return Mathf.Max(0.5f, top - transform.position.y);
        }

        void OnDestroy()
        {
            foreach (GameObject b in _branches) if (b != null) Destroy(b);
        }
    }
}
