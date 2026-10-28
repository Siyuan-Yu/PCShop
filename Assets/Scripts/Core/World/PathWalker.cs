using System;
using System.Collections.Generic;

namespace PCShopSell.Core.World
{
    public readonly struct MapPoint
    {
        public double X { get; }
        public double Y { get; }

        public MapPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// 沿路径折线行走的插值器：给定已走距离，返回当前位置。
    /// 界面层用它驱动玩家和顾客小人移动（每帧按时钟推进量换算成距离）。
    /// 边长可以大于直线距离（绕路），这里按"沿边按比例"插值，路径的总长度取边的实际长度之和。
    /// </summary>
    public sealed class PathWalker
    {
        private readonly List<MapPoint> _points = new List<MapPoint>();
        private readonly List<double> _segmentLengths = new List<double>();

        public double TotalLength { get; }

        public PathWalker(WorldMap map, IReadOnlyList<string> nodeIds)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (nodeIds == null || nodeIds.Count == 0)
                throw new ArgumentException("路径至少包含一个节点。", nameof(nodeIds));

            for (int i = 0; i < nodeIds.Count; i++)
            {
                WorldNode n = map.GetNode(nodeIds[i]);
                _points.Add(new MapPoint(n.X, n.Y));
            }

            double total = 0;
            for (int i = 0; i + 1 < nodeIds.Count; i++)
            {
                double len = EdgeLength(map, nodeIds[i], nodeIds[i + 1]);
                _segmentLengths.Add(len);
                total += len;
            }
            TotalLength = total;
        }

        /// <summary>已走距离对应的位置；超出范围时夹在起点或终点。</summary>
        public MapPoint PositionAt(double distanceTraveled)
        {
            if (_points.Count == 1 || distanceTraveled <= 0) return _points[0];
            if (distanceTraveled >= TotalLength) return _points[_points.Count - 1];

            double remaining = distanceTraveled;
            for (int i = 0; i < _segmentLengths.Count; i++)
            {
                double seg = _segmentLengths[i];
                if (remaining <= seg)
                {
                    double t = seg <= 0 ? 1 : remaining / seg;
                    MapPoint a = _points[i];
                    MapPoint b = _points[i + 1];
                    return new MapPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                }
                remaining -= seg;
            }
            return _points[_points.Count - 1];
        }

        private static double EdgeLength(WorldMap map, string a, string b)
        {
            IReadOnlyList<WorldEdge> edges = map.NeighborsOf(a);
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].To == b) return edges[i].Length;
            }
            throw new ArgumentException($"路径中相邻节点之间没有边：{a} - {b}");
        }
    }
}
