using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static class CityRoomRoute
    {
        internal static bool Advance(List<Vector3> path, ref int index, Transform owner, float dt, float speed,
            Action<Vector3, float> moveOwner)
        {
            const float waypointTolerance = .001f;
            while (dt > 0f && index < path.Count)
            {
                Vector3 start = owner.localPosition;
                Vector3 delta = path[index] - start;
                float distance = delta.magnitude;
                if (distance <= waypointTolerance)
                {
                    index++;
                    continue;
                }

                float step = Mathf.Min(dt, distance / speed);
                if (step * speed < .00001f) break; // A fractional remainder can round to zero movement.
                moveOwner(delta / distance, step);
                Vector3 end = owner.localPosition;
                if ((end - start).sqrMagnitude < 1e-10f) return false;
                if ((path[index] - end).sqrMagnitude <= waypointTolerance * waypointTolerance) index++;
                dt -= step;
            }
            return true;
        }

        internal static List<Vector3> Find(Vector3 from, Rect walkArea, Func<Vector3, bool> isDestination,
            IReadOnlyList<Rect> blockers)
        {
            const float cell = .2f;
            int nx = Mathf.FloorToInt(walkArea.width / cell + .0001f) + 1;
            int nz = Mathf.FloorToInt(walkArea.height / cell + .0001f) + 1;
            Vector3 Point(int id) => new Vector3(Mathf.Min(walkArea.xMax, walkArea.xMin + id % nx * cell), 0,
                Mathf.Min(walkArea.yMax, walkArea.yMin + id / nx * cell));
            int startX = Mathf.Clamp(Mathf.RoundToInt((from.x - walkArea.xMin) / cell), 0, nx - 1);
            int startZ = Mathf.Clamp(Mathf.RoundToInt((from.z - walkArea.yMin) / cell), 0, nz - 1);
            var previous = new int[nx * nz];
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            var queue = new Queue<int>();
            var entryTurns = new Dictionary<int, Vector3>();

            // A rounded grid point can fall inside furniture even when the player is standing outside it.
            for (int z = Mathf.Max(0, startZ - 1); z <= Mathf.Min(nz - 1, startZ + 1); z++)
            for (int x = Mathf.Max(0, startX - 1); x <= Mathf.Min(nx - 1, startX + 1); x++)
            {
                int node = x + nx * z;
                Vector3 point = Point(node);
                Vector3 turn = new Vector3(point.x, 0, from.z);
                if (!CanTraverse(from, turn, blockers) || !CanTraverse(turn, point, blockers))
                {
                    turn = new Vector3(from.x, 0, point.z);
                    if (!CanTraverse(from, turn, blockers) || !CanTraverse(turn, point, blockers)) continue;
                }
                previous[node] = node;
                entryTurns.Add(node, turn);
                queue.Enqueue(node);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                Vector3 at = Point(current);
                if (isDestination(at))
                {
                    var route = new List<Vector3>();
                    int node = current;
                    for (; node != previous[node]; node = previous[node]) route.Add(Point(node));
                    route.Add(Point(node));
                    route.Add(entryTurns[node]);
                    route.Reverse();
                    return route;
                }
                for (int direction = 0; direction < 4; direction++)
                {
                    int x = current % nx + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                    int z = current / nx + (direction == 2 ? 1 : direction == 3 ? -1 : 0);
                    if (x < 0 || x >= nx || z < 0 || z >= nz) continue;
                    int next = x + nx * z;
                    if (previous[next] >= 0 || !CanTraverse(at, Point(next), blockers)) continue;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        internal static bool CanTraverse(Vector3 from, Vector3 to, IReadOnlyList<Rect> blockers)
        {
            var start = new Vector2(from.x, from.z);
            var end = new Vector2(to.x, to.z);
            foreach (Rect blocker in blockers)
            {
                if (blocker.Contains(end)) return false;
                // Allow an old or newly moved furniture layout to release a player who starts inside a blocker.
                if (blocker.Contains(start)) continue;
                bool vertical = from.x == to.x;
                float side = vertical ? from.x : from.z;
                float sideMin = vertical ? blocker.xMin : blocker.yMin;
                float sideMax = vertical ? blocker.xMax : blocker.yMax;
                if (side < sideMin || side >= sideMax) continue;
                float startAlong = vertical ? from.z : from.x;
                float endAlong = vertical ? to.z : to.x;
                float blockerMin = vertical ? blocker.yMin : blocker.xMin;
                float blockerMax = vertical ? blocker.yMax : blocker.xMax;
                if (Mathf.Max(startAlong, endAlong) >= blockerMin && Mathf.Min(startAlong, endAlong) < blockerMax)
                    return false;
            }
            return true;
        }
    }
}
