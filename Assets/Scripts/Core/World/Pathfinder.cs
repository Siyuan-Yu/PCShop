using System;
using System.Collections.Generic;

namespace PCShopSell.Core.World
{
    public sealed class PathResult
    {
        public bool Found { get; }
        public IReadOnlyList<string> NodeIds { get; }
        public double Distance { get; }

        private PathResult(bool found, IReadOnlyList<string> nodeIds, double distance)
        {
            Found = found;
            NodeIds = nodeIds;
            Distance = distance;
        }

        public static PathResult NotFound { get; } = new PathResult(false, Array.Empty<string>(), 0);

        public static PathResult Success(IReadOnlyList<string> nodeIds, double distance)
        {
            return new PathResult(true, nodeIds, distance);
        }
    }

    /// <summary>A* 寻路（启发函数为直线距离；边长不短于直线距离，故结果最优）。</summary>
    public static class Pathfinder
    {
        public static PathResult FindPath(WorldMap map, string from, string to)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            map.GetNode(from); // 不存在则抛 KeyNotFoundException
            map.GetNode(to);

            if (from == to)
                return PathResult.Success(new[] { from }, 0);

            var gScore = new Dictionary<string, double> { [from] = 0 };
            var cameFrom = new Dictionary<string, string>();
            var closed = new HashSet<string>();
            // (f, 序号, 节点)：序号保证排序键唯一，同时让先入队者优先。
            var open = new SortedSet<(double f, long order, string id)>();
            long order = 0;

            open.Add((map.StraightDistance(from, to), order++, from));

            while (open.Count > 0)
            {
                var head = open.Min;
                open.Remove(head);
                string current = head.id;

                if (!closed.Add(current)) continue;

                if (current == to)
                    return PathResult.Success(Reconstruct(cameFrom, current), gScore[current]);

                IReadOnlyList<WorldEdge> edges = map.NeighborsOf(current);
                for (int i = 0; i < edges.Count; i++)
                {
                    WorldEdge e = edges[i];
                    if (closed.Contains(e.To)) continue;

                    double tentative = gScore[current] + e.Length;
                    if (!gScore.TryGetValue(e.To, out double known) || tentative < known)
                    {
                        gScore[e.To] = tentative;
                        cameFrom[e.To] = current;
                        open.Add((tentative + map.StraightDistance(e.To, to), order++, e.To));
                    }
                }
            }

            return PathResult.NotFound;
        }

        private static List<string> Reconstruct(Dictionary<string, string> cameFrom, string end)
        {
            var path = new List<string> { end };
            string cur = end;
            while (cameFrom.TryGetValue(cur, out string prev))
            {
                path.Add(prev);
                cur = prev;
            }
            path.Reverse();
            return path;
        }
    }
}
