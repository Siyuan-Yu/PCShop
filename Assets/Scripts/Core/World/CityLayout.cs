using System;
using System.Collections.Generic;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.World
{
    /// <summary>城市布局的数据文件格式（JSON）。字段名与 JSON 键一致，保持小写开头。</summary>
    [Serializable]
    public sealed class CityLayoutData
    {
        /// <summary>
        /// 画面距离换算成游戏内距离的倍率。画面里的城市是缩小的：
        /// 画面上 1 米按 distanceScale 米计算行走距离和耗时。
        /// </summary>
        public double distanceScale = 1;

        public LayoutNodeData[] nodes;
        public LayoutEdgeData[] edges;
        public LayoutPlaceData[] places;
    }

    [Serializable]
    public sealed class LayoutNodeData
    {
        public string id;
        public double x;
        public double y;
    }

    [Serializable]
    public sealed class LayoutEdgeData
    {
        public string a;
        public string b;
    }

    [Serializable]
    public sealed class LayoutPlaceData
    {
        public string id;
        public string name;

        /// <summary>shop / home / wholesaler / station。</summary>
        public string kind;

        /// <summary>入口所在的路径图节点。</summary>
        public string node;

        /// <summary>店铺规格：small / medium / large。</summary>
        public string size;

        /// <summary>业态（电脑、奶茶、服装……），仅用于显示。</summary>
        public string business;

        /// <summary>是否为玩家的店。</summary>
        public bool player;
    }

    public enum PlaceKind
    {
        Other,
        Shop,
        Home,
        Wholesaler,
        Station,
    }

    /// <summary>地点（店铺、住所、批发商、地铁站等），入口位于路径图的某个节点。</summary>
    public sealed class Place
    {
        public string Id { get; }
        public string Name { get; }
        public PlaceKind Kind { get; }
        public string NodeId { get; }
        public ShopSize Size { get; }
        public string Business { get; }
        public bool IsPlayerShop { get; }

        public Place(string id, string name, PlaceKind kind, string nodeId,
            ShopSize size = ShopSize.Small, string business = null, bool isPlayerShop = false)
        {
            Id = id;
            Name = name;
            Kind = kind;
            NodeId = nodeId;
            Size = size;
            Business = business;
            IsPlayerShop = isPlayerShop;
        }
    }

    /// <summary>
    /// 由数据文件构建的城市：路径图（游戏内距离）+ 地点。
    /// 布局坐标是画面坐标（米），路径图坐标 = 画面坐标 × DistanceScale。
    /// </summary>
    public sealed class CityLayout
    {
        private readonly Dictionary<string, Place> _places = new Dictionary<string, Place>();
        private readonly List<Place> _placeList = new List<Place>();

        public WorldMap Map { get; }
        public double DistanceScale { get; }
        public IReadOnlyList<Place> Places => _placeList;

        private CityLayout(WorldMap map, double distanceScale)
        {
            Map = map;
            DistanceScale = distanceScale;
        }

        public static CityLayout FromData(CityLayoutData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.distanceScale <= 0)
                throw new ArgumentException("distanceScale 必须为正。", nameof(data));
            if (data.nodes == null || data.nodes.Length == 0)
                throw new ArgumentException("布局至少需要一个节点。", nameof(data));

            var map = new WorldMap();
            foreach (LayoutNodeData n in data.nodes)
                map.AddNode(n.id, n.x * data.distanceScale, n.y * data.distanceScale);

            if (data.edges != null)
            {
                foreach (LayoutEdgeData e in data.edges)
                    map.AddEdge(e.a, e.b);
            }

            var layout = new CityLayout(map, data.distanceScale);
            if (data.places != null)
            {
                foreach (LayoutPlaceData p in data.places)
                {
                    if (string.IsNullOrEmpty(p.id))
                        throw new ArgumentException("地点 ID 不能为空。");
                    if (layout._places.ContainsKey(p.id))
                        throw new ArgumentException($"地点已存在：{p.id}");
                    if (!map.HasNode(p.node))
                        throw new ArgumentException($"地点 {p.id} 的入口节点不存在：{p.node}");

                    var place = new Place(p.id, p.name ?? p.id, ParseKind(p.kind), p.node,
                        ParseSize(p.size), p.business, p.player);
                    layout._places.Add(place.Id, place);
                    layout._placeList.Add(place);
                }
            }
            return layout;
        }

        public bool HasPlace(string id) => id != null && _places.ContainsKey(id);

        public Place GetPlace(string id)
        {
            if (!HasPlace(id)) throw new KeyNotFoundException($"地点不存在：{id}");
            return _places[id];
        }

        /// <summary>第一个指定类型的地点；没有则返回 null。</summary>
        public Place FirstOfKind(PlaceKind kind)
        {
            for (int i = 0; i < _placeList.Count; i++)
            {
                if (_placeList[i].Kind == kind) return _placeList[i];
            }
            return null;
        }

        /// <summary>离画面坐标 (x, y) 最近的节点。</summary>
        public string NearestNode(double viewX, double viewY)
        {
            string best = null;
            double bestDist = double.MaxValue;
            double mx = viewX * DistanceScale;
            double my = viewY * DistanceScale;
            foreach (WorldNode n in Map.Nodes)
            {
                double dx = n.X - mx;
                double dy = n.Y - my;
                double d = dx * dx + dy * dy;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n.Id;
                }
            }
            return best;
        }

        /// <summary>节点的画面坐标。</summary>
        public MapPoint ViewPosition(string nodeId)
        {
            WorldNode n = Map.GetNode(nodeId);
            return new MapPoint(n.X / DistanceScale, n.Y / DistanceScale);
        }

        /// <summary>玩家的店；没有则返回 null。</summary>
        public Place PlayerShop()
        {
            for (int i = 0; i < _placeList.Count; i++)
            {
                if (_placeList[i].IsPlayerShop) return _placeList[i];
            }
            return null;
        }

        private static PlaceKind ParseKind(string kind)
        {
            switch (kind)
            {
                case "shop": return PlaceKind.Shop;
                case "home": return PlaceKind.Home;
                case "wholesaler": return PlaceKind.Wholesaler;
                case "station": return PlaceKind.Station;
                default: return PlaceKind.Other;
            }
        }

        private static ShopSize ParseSize(string size)
        {
            switch (size)
            {
                case "medium": return ShopSize.Medium;
                case "large": return ShopSize.Large;
                default: return ShopSize.Small;
            }
        }
    }
}
