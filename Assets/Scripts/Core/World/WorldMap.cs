using System;
using System.Collections.Generic;

namespace PCShopSell.Core.World
{
    /// <summary>路径图上的节点（路口、建筑入口等）。坐标单位为"米"。</summary>
    public sealed class WorldNode
    {
        public string Id { get; }
        public double X { get; }
        public double Y { get; }

        public WorldNode(string id, double x, double y)
        {
            Id = id;
            X = x;
            Y = y;
        }
    }

    /// <summary>从某节点出发的一条边。Length 为实际行走距离（米）。</summary>
    public readonly struct WorldEdge
    {
        public string To { get; }
        public double Length { get; }

        public WorldEdge(string to, double length)
        {
            To = to;
            Length = length;
        }
    }

    /// <summary>
    /// 世界路径图：无向图。玩家沿边行走，距离用于折算移动耗时。
    /// 边长默认等于两点直线距离，也可指定更长的实际路程（绕路、过桥），但不得短于直线距离，
    /// 以保证寻路启发函数可采纳（A* 结果最优）。
    /// </summary>
    public sealed class WorldMap
    {
        private const double Epsilon = 1e-6;

        private readonly Dictionary<string, WorldNode> _nodes = new Dictionary<string, WorldNode>();
        private readonly Dictionary<string, List<WorldEdge>> _adjacency = new Dictionary<string, List<WorldEdge>>();

        public int NodeCount => _nodes.Count;
        public int Revision { get; private set; }
        public IEnumerable<WorldNode> Nodes => _nodes.Values;

        // 缓存仅属于这张路网；任何节点或边变化都会使路径失效。
        private readonly Dictionary<(string from, string to), PathResult> _paths = new();
        private readonly Queue<(string from, string to)> _pathOrder = new();
        internal bool TryCachedPath(string from, string to, out PathResult path) => _paths.TryGetValue((from, to), out path);
        internal PathResult RememberPath(string from, string to, PathResult path)
        {
            if (_paths.Count >= 2048) _paths.Remove(_pathOrder.Dequeue());
            _paths.Add((from, to), path);
            _pathOrder.Enqueue((from, to));
            return path;
        }
        internal Pathfinder.SearchWorkspace PathSearch;
        private void InvalidatePaths()
        {
            Revision++;
            PathSearch = null;
            _paths.Clear();
            _pathOrder.Clear();
        }

        public WorldNode AddNode(string id, double x, double y)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("节点 ID 不能为空。", nameof(id));
            if (_nodes.ContainsKey(id))
                throw new ArgumentException($"节点已存在：{id}", nameof(id));

            var node = new WorldNode(id, x, y);
            _nodes.Add(id, node);
            _adjacency.Add(id, new List<WorldEdge>());
            InvalidatePaths();
            return node;
        }

        public bool HasNode(string id) => id != null && _nodes.ContainsKey(id);

        public WorldNode GetNode(string id)
        {
            if (!HasNode(id))
                throw new KeyNotFoundException($"节点不存在：{id}");
            return _nodes[id];
        }

        /// <summary>添加无向边。length 为空则取两点直线距离。</summary>
        public void AddEdge(string a, string b, double? length = null)
        {
            WorldNode na = GetNode(a);
            WorldNode nb = GetNode(b);
            if (a == b)
                throw new ArgumentException("边的两端不能是同一节点。");
            if (HasEdge(a, b))
                throw new ArgumentException($"边已存在：{a} - {b}");

            double straight = StraightDistance(na, nb);
            double len = length ?? straight;
            if (len + Epsilon < straight)
                throw new ArgumentException($"边长不能短于直线距离（{len} < {straight}）：{a} - {b}");

            _adjacency[a].Add(new WorldEdge(b, len));
            _adjacency[b].Add(new WorldEdge(a, len));
            InvalidatePaths();
        }

        public bool HasEdge(string a, string b)
        {
            if (!HasNode(a) || !HasNode(b)) return false;
            List<WorldEdge> list = _adjacency[a];
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].To == b) return true;
            }
            return false;
        }

        public IReadOnlyList<WorldEdge> NeighborsOf(string id)
        {
            if (!HasNode(id))
                throw new KeyNotFoundException($"节点不存在：{id}");
            return _adjacency[id];
        }

        public double StraightDistance(string a, string b)
        {
            return StraightDistance(GetNode(a), GetNode(b));
        }

        private static double StraightDistance(WorldNode a, WorldNode b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
