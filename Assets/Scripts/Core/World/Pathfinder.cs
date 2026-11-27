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
            if (map.TryCachedPath(from, to, out var cached)) return cached;

            if (from == to)
                return map.RememberPath(from, to, PathResult.Success(Array.AsReadOnly(new[] { from }), 0));

            var search = map.PathSearch ?? (map.PathSearch = new SearchWorkspace(map));
            return map.RememberPath(from, to, search.Find(from, to));
        }

        // 路网不变时复用索引和搜索工作区。以原来的入队序号打破同分，保持选路完全一致。
        internal sealed class SearchWorkspace
        {
            private readonly WorldNode[] _nodes;
            private readonly Dictionary<string, int> _index = new();
            private readonly (int to, double length)[][] _edges;
            private readonly double[] _distance;
            private readonly int[] _seen, _closed, _parent;
            private readonly List<(double score, long order, int node)> _heap = new();
            private int _generation;
            private long _order;
            internal SearchWorkspace(WorldMap map)
            {
                var nodes = new List<WorldNode>(map.Nodes);
                _nodes = nodes.ToArray();
                for (int i = 0; i < _nodes.Length; i++) _index.Add(_nodes[i].Id, i);
                _edges = new (int, double)[_nodes.Length][];
                for (int i = 0; i < _nodes.Length; i++)
                {
                    var source = map.NeighborsOf(_nodes[i].Id);
                    var edges = _edges[i] = new (int, double)[source.Count];
                    for (int e = 0; e < edges.Length; e++) edges[e] = (_index[source[e].To], source[e].Length);
                }
                _distance = new double[_nodes.Length];
                _seen = new int[_nodes.Length]; _closed = new int[_nodes.Length]; _parent = new int[_nodes.Length];
            }
            internal PathResult Find(string from, string to)
            {
                if (_generation == int.MaxValue)
                {
                    Array.Clear(_seen, 0, _seen.Length); Array.Clear(_closed, 0, _closed.Length); _generation = 0;
                }
                int generation = ++_generation, start = _index[from], target = _index[to];
                _heap.Clear(); _order = 0;
                _seen[start] = generation; _distance[start] = 0; _parent[start] = -1;
                Push((Heuristic(start, target), _order++, start));
                while (_heap.Count > 0)
                {
                    int current = Pop();
                    if (_closed[current] == generation) continue;
                    _closed[current] = generation;
                    if (current == target)
                    {
                        var path = new List<string>();
                        for (int at = current; at >= 0; at = _parent[at]) path.Add(_nodes[at].Id);
                        path.Reverse();
                        return PathResult.Success(path.AsReadOnly(), _distance[current]);
                    }
                    foreach (var edge in _edges[current])
                    {
                        if (_closed[edge.to] == generation) continue;
                        double tentative = _distance[current] + edge.length;
                        if (_seen[edge.to] == generation && tentative >= _distance[edge.to]) continue;
                        _seen[edge.to] = generation; _distance[edge.to] = tentative; _parent[edge.to] = current;
                        Push((tentative + Heuristic(edge.to, target), _order++, edge.to));
                    }
                }
                return PathResult.NotFound;
            }
            internal Dictionary<string, double> DistancesFrom(string from)
            {
                if (_generation == int.MaxValue)
                {
                    Array.Clear(_seen, 0, _seen.Length); Array.Clear(_closed, 0, _closed.Length); _generation = 0;
                }
                int generation = ++_generation, start = _index[from];
                _heap.Clear(); _order = 0; _seen[start] = generation; _distance[start] = 0;
                Push((0, _order++, start));
                while (_heap.Count > 0)
                {
                    int current = Pop();
                    if (_closed[current] == generation) continue;
                    _closed[current] = generation;
                    foreach (var edge in _edges[current])
                    {
                        if (_closed[edge.to] == generation) continue;
                        double candidate = _distance[current] + edge.length;
                        if (_seen[edge.to] == generation && candidate >= _distance[edge.to]) continue;
                        _seen[edge.to] = generation; _distance[edge.to] = candidate;
                        Push((candidate, _order++, edge.to));
                    }
                }
                var result = new Dictionary<string, double>();
                for (int i = 0; i < _nodes.Length; i++) if (_seen[i] == generation) result.Add(_nodes[i].Id, _distance[i]);
                return result;
            }
            private double Heuristic(int a, int b)
            {
                double dx = _nodes[a].X - _nodes[b].X, dy = _nodes[a].Y - _nodes[b].Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            private static bool Earlier((double score, long order, int node) a, (double score, long order, int node) b)
                => a.score < b.score || a.score == b.score && a.order < b.order;
            private void Push((double score, long order, int node) item)
            {
                int i = _heap.Count; _heap.Add(item);
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (!Earlier(item, _heap[parent])) break;
                    _heap[i] = _heap[parent]; i = parent;
                }
                _heap[i] = item;
            }
            private int Pop()
            {
                int result = _heap[0].node, count = _heap.Count - 1;
                var last = _heap[count]; _heap.RemoveAt(count);
                if (count == 0) return result;
                int i = 0;
                while (i * 2 + 1 < count)
                {
                    int child = i * 2 + 1;
                    if (child + 1 < count && Earlier(_heap[child + 1], _heap[child])) child++;
                    if (!Earlier(_heap[child], last)) break;
                    _heap[i] = _heap[child]; i = child;
                }
                _heap[i] = last;
                return result;
            }
        }

    }
}
