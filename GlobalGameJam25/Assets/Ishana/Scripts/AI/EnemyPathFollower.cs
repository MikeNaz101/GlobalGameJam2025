using UnityEngine;
using UnityEngine.AI;

namespace Ishana.AI
{
    /// <summary>
    /// Lightweight path following for enemies that move themselves (no NavMeshAgent needed).
    /// Give it where the enemy is and where it wants to go, and it returns the next point to walk
    /// toward so the enemy goes around trees, rocks and walls instead of pushing into them.
    ///
    /// Falls back to the destination itself (a straight line, the old behaviour) whenever there is no
    /// NavMesh or no path.
    /// </summary>
    public class EnemyPathFollower
    {
        const float RepathInterval = 0.5f;
        const float RepathIfTargetMoved = 2f;
        const float CornerReachedDistance = 0.75f;

        readonly Vector3[] _corners = new Vector3[32];
        NavMeshPath _path;
        int _cornerCount, _cornerIndex;
        float _nextRepathTime;
        Vector3 _lastDestination;

        public Vector3 GetSteerTarget(Vector3 from, Vector3 destination)
        {
            if (!RuntimeNavMesh.Available) return destination;

            bool targetMoved = (destination - _lastDestination).sqrMagnitude > RepathIfTargetMoved * RepathIfTargetMoved;
            if (Time.time >= _nextRepathTime || targetMoved) Repath(from, destination);

            if (_cornerCount == 0) return destination;

            while (_cornerIndex < _cornerCount - 1 && HorizontalDistance(from, _corners[_cornerIndex]) < CornerReachedDistance)
                _cornerIndex++;

            // At the end of the path (or of a partial one): finish with a straight line
            if (_cornerIndex == _cornerCount - 1 && HorizontalDistance(from, _corners[_cornerIndex]) < CornerReachedDistance)
                return destination;

            return _corners[_cornerIndex];
        }

        void Repath(Vector3 from, Vector3 destination)
        {
            // Spread enemies' path requests over time instead of all on the same frame
            _nextRepathTime = Time.time + RepathInterval + Random.Range(0f, 0.25f);
            _lastDestination = destination;
            _cornerCount = 0;
            _cornerIndex = 0;

            if (_path == null) _path = new NavMeshPath(); // Can't be created in a field initializer

            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 6f, NavMesh.AllAreas)) return;
            if (!NavMesh.SamplePosition(destination, out NavMeshHit end, 8f, NavMesh.AllAreas)) return;
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path)) return;
            if (_path.status == NavMeshPathStatus.PathInvalid) return;

            _cornerCount = _path.GetCornersNonAlloc(_corners);
            _cornerIndex = _cornerCount > 1 ? 1 : 0; // Corner 0 is where we already are
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
