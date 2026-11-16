using System.Collections.Generic;
using UnityEngine;

namespace Ishana.Elements
{
    /// <summary>The kinds of magic (and plain force) that the world reacts to.</summary>
    public enum Element { Force, Fire, Ice, Earth }

    /// <summary>
    /// Base class for anything in the world that reacts to magic: plants that burn, and whatever comes next.
    /// Every object registers itself so effects can find what is nearby without needing colliders
    /// (grass and weeds have none).
    /// </summary>
    public abstract class ElementalObject : MonoBehaviour
    {
        static readonly List<ElementalObject> _all = new List<ElementalObject>();
        public static IReadOnlyList<ElementalObject> All => _all;

        [Tooltip("Rough size of this object, used to decide whether a magic hit or a nearby fire reaches it.")]
        public float reachRadius = 1f;
        [Tooltip("Height of the middle of the object above its base.")]
        public float centerHeight = 1f;

        public Vector3 Center => transform.position + Vector3.up * centerHeight;

        /// <summary>Small number describing the current condition, for saving and for the world generator.</summary>
        public abstract int StateId { get; }
        /// <summary>Put the object straight into a saved condition, without effects.</summary>
        public abstract void RestoreState(int stateId);
        /// <summary>React to magic. Power is roughly the shot's charge (1 = weak shot).</summary>
        public abstract void ApplyElement(Element element, float power, Vector3 point);

        // Registered for the object's whole life (not OnEnable), so components can switch themselves
        // off to save time while nothing is happening and still be found.
        protected virtual void Awake() => _all.Add(this);
        protected virtual void OnDestroy() => _all.Remove(this);

        /// <summary>
        /// Apply magic at a point. Reaches the object that was hit directly plus everything within the radius.
        /// </summary>
        public static void Impact(Element element, Vector3 point, float radius, float power, GameObject directHit = null)
        {
            // Lets other systems react first (e.g. painted terrain plants become burnable where fire lands)
            OnImpact?.Invoke(element, point, radius, power);

            ElementalObject direct = directHit != null ? directHit.GetComponentInParent<ElementalObject>() : null;
            if (direct != null) direct.ApplyElement(element, power, point);

            // Copy first: reacting can enable/disable objects, which changes the list
            _scratch.Clear();
            for (int i = 0; i < _all.Count; i++)
            {
                ElementalObject o = _all[i];
                if (o == null || o == direct || !o.gameObject.activeInHierarchy) continue;
                float reach = radius + o.reachRadius;
                Vector3 d = o.Center - point;
                // Tall things (trees) count as hit anywhere along their height
                d.y = Mathf.Max(0f, Mathf.Abs(d.y) - o.centerHeight);
                if (d.sqrMagnitude <= reach * reach) _scratch.Add(o);
            }
            for (int i = 0; i < _scratch.Count; i++)
                if (_scratch[i] != null) _scratch[i].ApplyElement(element, power, point);
            _scratch.Clear();
        }

        static readonly List<ElementalObject> _scratch = new List<ElementalObject>();

        /// <summary>Fired at the start of every magic impact: element, point, radius, power.</summary>
        public static event System.Action<Element, Vector3, float, float> OnImpact;
    }
}
