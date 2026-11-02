using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.World
{
    /// <summary>城市布局的数据文件格式（JSON）。字段名与 JSON 键一致，保持小写开头。</summary>
    [Serializable]
    public sealed class CityLayoutData
    {
        public int layoutVersion;
        public LayoutAgencyDistrictData[] agencyDistricts;
        public LayoutPlaceAliasData[] placeAliases;
        public LayoutBlockData[] blocks;
        public LayoutStreetData[] streets;
        /// <summary>
        /// 画面距离换算成游戏内距离的倍率。画面里的城市是缩小的：
        /// 画面上 1 米按 distanceScale 米计算行走距离和耗时。
        /// </summary>
        public double distanceScale = 1;
        // Optional common road-distance multiplier. Zero preserves legacy/custom map lengths.
        public double uniformRoadScale;

        public LayoutNodeData[] nodes;
        public LayoutEdgeData[] edges;
        public LayoutPlaceData[] places;
        public LayoutPatrolData[] patrols;
    }

    [Serializable] public sealed class LayoutAgencyDistrictData
    {
        public string id,name,agencyId;
        public string[] streets;
    }
    [Serializable] public sealed class LayoutPlaceAliasData { public string from,to; }
    [Serializable]
    public sealed class LayoutBlockData
    {
        public string street, model;
        public double x,y,width,depth,height;
        public int variant;
        public int tier,columns=1,rows=1;
    }

    [Serializable]
    public sealed class LayoutStreetData
    {
        public string id, name, purpose;
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

        /// <summary>实际路程（画面米，乘 distanceScale 前）；0 表示取直线距离。用于把很长的连接路在画面上压缩。</summary>
        public double length;
    }

    /// <summary>交警巡逻路线：沿节点来回走。</summary>
    [Serializable]
    public sealed class LayoutPatrolData
    {
        public string id;
        public string[] nodes;
    }

    [Serializable]
    public sealed class LayoutPlaceData
    {
        public string model;
        public int tier;
        public string street;
        public bool explicitEntrance, tradable;
        public double entranceX, entranceY, width, depth, heading;
        public int openMinute, closeMinute;
        public string id;
        public string name;

        /// <summary>shop / home / wholesaler / station / dealer / agency（房产中介）。</summary>
        public string kind;

        /// <summary>入口所在的路径图节点。</summary>
        public string node;

        /// <summary>店铺规格：small / medium / large。</summary>
        public string size;

        /// <summary>业态（电脑、奶茶、服装……），仅用于显示。</summary>
        public string business;

        /// <summary>是否为玩家的店。</summary>
        public bool player;

        /// <summary>档口卖的品类，逗号分隔（Cpu,Gpu…）。</summary>
        public string sells;
    }

    public enum PlaceKind
    {
        Other,
        Shop,
        Home,
        Wholesaler,
        Station,
        Dealer,
        Agency,
    }

    /// <summary>地点（店铺、住所、批发商、地铁站等），入口位于路径图的某个节点。</summary>
    public sealed class Place
    {
        public string Model { get; }
        public int Tier { get; }
        public string StreetId { get; }
        public bool ExplicitEntrance { get; }
        public bool IsTradable { get; }
        public double EntranceX { get; }
        public double EntranceY { get; }
        public double Width { get; }
        public double Depth { get; }
        public double Heading { get; }
        public int OpenMinute { get; }
        public int CloseMinute { get; }
        public string Id { get; }
        public string Name { get; }
        public PlaceKind Kind { get; }
        public string NodeId { get; }
        public ShopSize Size { get; }
        public string Business { get; }
        public bool IsPlayerShop { get; }
        public IReadOnlyList<PartCategory> Sells { get; }

        public Place(string id, string name, PlaceKind kind, string nodeId,
            ShopSize size = ShopSize.Small, string business = null, bool isPlayerShop = false,
            IReadOnlyList<PartCategory> sells = null, string streetId = null, bool explicitEntrance = false,
            double entranceX = 0, double entranceY = 0, double width = 10, double depth = 9, double heading = 0,
            bool tradable = false, int openMinute = 0, int closeMinute = 0, string model = null, int tier = 0)
        {
            Model = model; Tier = tier;
            Id = id;
            Name = name;
            Kind = kind;
            NodeId = nodeId;
            Size = size;
            Business = business;
            IsPlayerShop = isPlayerShop;
            Sells = sells ?? Array.Empty<PartCategory>();
            StreetId = streetId ?? (kind == PlaceKind.Wholesaler ? "s_electronics" : kind == PlaceKind.Home ? "s_oldhomes" : "s_start");
            ExplicitEntrance = explicitEntrance; EntranceX = entranceX; EntranceY = entranceY;
            Width = width > 0 ? width : 10; Depth = depth > 0 ? depth : 9; Heading = heading;
            IsTradable = tradable || (!explicitEntrance && (kind == PlaceKind.Shop || kind == PlaceKind.Wholesaler));
            OpenMinute = openMinute; CloseMinute = closeMinute;
        }
    }

    public sealed class PatrolRoute
    {
        public string Id { get; }
        public IReadOnlyList<string> NodeIds { get; }

        public PatrolRoute(string id, IReadOnlyList<string> nodeIds)
        {
            Id = id;
            NodeIds = nodeIds;
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
        private readonly List<PatrolRoute> _patrols = new List<PatrolRoute>();

        public int LayoutVersion { get; private set; }
        public IReadOnlyList<LayoutAgencyDistrictData> AgencyDistricts { get; private set; } = Array.Empty<LayoutAgencyDistrictData>();
        public string CanonicalPlaceId(string id) => HasPlace(id)?GetPlace(id).Id:id;
        public LayoutAgencyDistrictData AgencyDistrictFor(string street)
        { foreach(var d in AgencyDistricts)if(Array.IndexOf(d.streets??Array.Empty<string>(),street)>=0)return d;return null; }
        public string AgencyCoverageName(Place p)=>AgencyDistrictFor(p?.StreetId)?.name??StreetName(p?.StreetId);
        public IReadOnlyList<LayoutBlockData> Blocks { get; private set; } = Array.Empty<LayoutBlockData>();
        public WorldMap Map { get; }
        public double DistanceScale { get; }
        public bool HasUniformRoadScale { get; private set; }
        public double TravelDistanceScale { get; private set; }
        public IReadOnlyList<Place> Places => _placeList;
        public IReadOnlyList<PatrolRoute> Patrols => _patrols;
        public IReadOnlyList<LayoutStreetData> Streets { get; private set; } = Array.Empty<LayoutStreetData>();
        public string StreetName(string id)
        {
            foreach (var street in Streets) if (street.id == id) return street.name;
            return id == "s_electronics" ? "电子市场街" : id == "s_oldhomes" ? "老居民街" : "创业商业街";
        }

        public IEnumerable<Place> OfKind(PlaceKind kind)
        {
            foreach (Place p in _placeList)
            {
                if (p.Kind == kind) yield return p;
            }
        }

        /// <summary>入口在该节点上的地点；没有则返回 null。</summary>
        public Place PlaceAtNode(string nodeId)
        {
            foreach (Place p in _placeList)
            {
                if (p.NodeId == nodeId) return p;
            }
            return null;
        }

        private CityLayout(WorldMap map, double distanceScale)
        {
            Map = map;
            DistanceScale = distanceScale;
            TravelDistanceScale = distanceScale;
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
                {
                    // The current city uses one physical distance per displayed meter, on every road.
                    // Keep the coordinate scale separate: map/UI positions must not move.
                    double? len = data.uniformRoadScale > 0
                        ? map.StraightDistance(e.a, e.b) * data.uniformRoadScale
                        : e.length > 0 ? e.length * data.distanceScale : (double?)null;
                    if (len.HasValue && len.Value < map.StraightDistance(e.a, e.b))
                        len = null;
                    map.AddEdge(e.a, e.b, len);
                }
            }

            var layout = new CityLayout(map, data.distanceScale);
            layout.HasUniformRoadScale = data.uniformRoadScale > 0;
            layout.TravelDistanceScale = data.distanceScale * (layout.HasUniformRoadScale ? data.uniformRoadScale : 1);
            layout.AgencyDistricts=data.agencyDistricts??Array.Empty<LayoutAgencyDistrictData>();
            layout.LayoutVersion=data.layoutVersion; layout.Blocks=data.blocks??Array.Empty<LayoutBlockData>();
            layout.Streets = data.streets ?? Array.Empty<LayoutStreetData>();
            if (data.patrols != null)
            {
                foreach (LayoutPatrolData pr in data.patrols)
                {
                    if (pr.nodes == null || pr.nodes.Length < 2)
                        throw new ArgumentException($"巡逻路线 {pr.id} 至少需要两个节点。");
                    foreach (string n in pr.nodes)
                    {
                        if (!map.HasNode(n)) throw new ArgumentException($"巡逻路线 {pr.id} 的节点不存在：{n}");
                    }
                    layout._patrols.Add(new PatrolRoute(pr.id, pr.nodes));
                }
            }
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
                        ParseSize(p.size), p.business, p.player, ParseSells(p.sells), p.street, p.explicitEntrance,
                        p.entranceX, p.entranceY, p.width, p.depth, p.heading, p.tradable, p.openMinute, p.closeMinute, p.model, p.tier);
                    layout._places.Add(place.Id, place);
                    layout._placeList.Add(place);
                }
            }
            foreach(var alias in data.placeAliases??Array.Empty<LayoutPlaceAliasData>())
            {
                if(alias.from==null||layout._places.ContainsKey(alias.from)||!layout._places.ContainsKey(alias.to))continue;
                layout._places.Add(alias.from,layout._places[alias.to]);
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
                case "dealer": return PlaceKind.Dealer;
                case "agency": return PlaceKind.Agency;
                default: return PlaceKind.Other;
            }
        }

        private static IReadOnlyList<PartCategory> ParseSells(string sells)
        {
            var list = new List<PartCategory>();
            if (string.IsNullOrEmpty(sells)) return list;
            foreach (string s in sells.Split(','))
            {
                if (Enum.TryParse(s.Trim(), true, out PartCategory c)) list.Add(c);
                else throw new ArgumentException($"未知的配件品类：{s}");
            }
            return list;
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
