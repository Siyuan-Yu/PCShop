using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Home
{
    /// <summary>家具摆在哪：地面（占地，挡路）、薄地面（地毯，可以踩）、墙面（挂在后墙或右墙上）。</summary>
    public enum FurnitureSlot
    {
        Floor,
        Flat,
        Wall,
    }

    /// <summary>家具的玩法角色：床=睡觉，衣柜=换衣服，电饭锅小桌=做饭吃饭。</summary>
    public enum FurnitureRole
    {
        None,
        Bed,
        Wardrobe,
        Cooker,
    }

    /// <summary>一件家具的定义。尺寸单位米：Width 是朝向摆正时左右的宽度，Depth 是前后的进深。</summary>
    public sealed class FurnitureDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Money Price { get; }
        public bool ForShop { get; }
        public bool Starter { get; }
        public FurnitureSlot Slot { get; }
        public double Width { get; }
        public double Depth { get; }
        public FurnitureRole Role { get; set; }

        /// <summary>重量（公斤）。超过 <see cref="FurnitureCatalog.HeavyKg"/> 的塞不进背包，得叫货车拉。</summary>
        public double WeightKg { get; set; }

        public bool IsHeavy => WeightKg > FurnitureCatalog.HeavyKg;

        /// <summary>床的睡眠质量（只看摆出来的最好的那张床）。</summary>
        public double BedQuality { get; set; }

        /// <summary>摆出来后额外的睡眠加成（空调、风扇、窗帘）。</summary>
        public double SleepBonus { get; set; }

        /// <summary>每月多付的水电。</summary>
        public Money Utilities { get; set; }

        /// <summary>装饰分：家里累计加一点睡眠质量，店里提升客流。</summary>
        public double Appeal { get; set; }

        /// <summary>店里：顾客多等几分钟才走。</summary>
        public int PatienceBonus { get; set; }

        public FurnitureDef(string id, string name, string description, long priceYuan, FurnitureSlot slot, double width, double depth, bool forShop = false, bool starter = false)
        {
            Id = id;
            Name = name;
            Description = description;
            Price = Money.FromYuan(priceYuan);
            Slot = slot;
            Width = width;
            Depth = depth;
            ForShop = forShop;
            Starter = starter;
        }
    }

    /// <summary>摆在房间里的一件家具。Rot：地面家具 0~3（每 +1 顺时针转 90°，0 = 背朝后墙）；墙面家具 0 = 后墙，1 = 右墙。X/Z 是中心（墙面家具只看沿墙方向的那个坐标）。</summary>
    public sealed class PlacedFurniture
    {
        public string Id;
        public double X;
        public double Z;
        public int Rot;

        public PlacedFurniture Clone() => new PlacedFurniture { Id = Id, X = X, Z = Z, Rot = Rot };
    }

    public static class FurnitureCatalog
    {
        public const string BedBoard = "bed_board";
        public const string WardrobeCloth = "wardrobe_cloth";
        public const string TableCooker = "table_cooker";
        public const string Curtain = "curtain";

        /// <summary>单件超过这个重量就太大件，没法放进背包。</summary>
        public const double HeavyKg = 20;

        /// <summary>叫货车送货的运费（一趟一件，次日早上送到）。</summary>
        public static readonly Money TruckFee = Money.FromYuan(120);

        private static readonly Dictionary<string, double> Weights = new Dictionary<string, double>
        {
            { "bed_spring", 45 }, { "bed_good", 80 }, { "wardrobe_wood", 50 }, { "fan_new", 4 }, { "aircon", 30 },
            { Curtain, 1.5 }, { "rug", 3 }, { "plant", 5 }, { "desk", 25 }, { "sofa", 40 }, { "poster", 0.3 },
            { "shop_plant", 6 }, { "shop_sofa", 40 }, { "shop_dispenser", 18 }, { "shop_poster", 0.5 }, { "shop_led", 22 },
        };

        public static readonly IReadOnlyList<FurnitureDef> All = Build();

        private static IReadOnlyList<FurnitureDef> Build()
        {
            var l = new List<FurnitureDef>();
            FurnitureDef Add(FurnitureDef d)
            {
                l.Add(d);
                return d;
            }

            // ---- 出租屋：开局自带 ----
            Add(new FurnitureDef(BedBoard, "硬板床", "一块木板加一层薄褥子。", 0, FurnitureSlot.Floor, 2.0, 1.0, false, true) { Role = FurnitureRole.Bed, BedQuality = 1.0 });
            Add(new FurnitureDef(WardrobeCloth, "布衣柜", "拉链布衣柜，塑料骨架。", 0, FurnitureSlot.Floor, 1.1, 0.6, false, true) { Role = FurnitureRole.Wardrobe });
            Add(new FurnitureDef(TableCooker, "折叠小桌（电饭锅）", "吃饭、做饭都在这张小桌上。", 0, FurnitureSlot.Floor, 1.2, 0.6, false, true) { Role = FurnitureRole.Cooker });
            Add(new FurnitureDef("stool", "塑料矮凳", "红色塑料凳。", 0, FurnitureSlot.Floor, 0.4, 0.4, false, true));
            Add(new FurnitureDef("fan_broken", "坏电风扇", "摇头的地方卡住了，一直耷拉着。", 0, FurnitureSlot.Floor, 0.45, 0.45, false, true));
            Add(new FurnitureDef("junk", "纸箱和塑料盆", "搬家时留下的杂物。", 0, FurnitureSlot.Floor, 0.9, 0.9, false, true));

            // ---- 出租屋：可以买 ----
            Add(new FurnitureDef("bed_spring", "弹簧床", "有像样的床垫，睡着不硌人。睡眠质量 ×1.12。", 900, FurnitureSlot.Floor, 2.0, 1.1) { Role = FurnitureRole.Bed, BedQuality = 1.12 });
            Add(new FurnitureDef("bed_good", "实木床 + 乳胶床垫", "睡眠质量 ×1.25。", 2200, FurnitureSlot.Floor, 2.1, 1.3) { Role = FurnitureRole.Bed, BedQuality = 1.25 });
            Add(new FurnitureDef("wardrobe_wood", "实木衣柜", "结实好看，屋里显得体面。", 600, FurnitureSlot.Floor, 1.4, 0.6) { Role = FurnitureRole.Wardrobe, Appeal = 1 });
            Add(new FurnitureDef("fan_new", "落地扇", "能转的电风扇。睡眠 +0.05，水电 +¥10/月。", 180, FurnitureSlot.Floor, 0.45, 0.45) { SleepBonus = 0.05, Utilities = Money.FromYuan(10) });
            Add(new FurnitureDef("aircon", "二手空调", "挂在墙上。睡眠 +0.12，水电 +¥40/月。", 1200, FurnitureSlot.Wall, 1.1, 0.3) { SleepBonus = 0.12, Utilities = Money.FromYuan(40) });
            Add(new FurnitureDef(Curtain, "窗帘", "挡光，晚上睡得安稳点。睡眠 +0.04。", 150, FurnitureSlot.Wall, 1.5, 0.1) { SleepBonus = 0.04 });
            Add(new FurnitureDef("rug", "小地毯", "遮住一块难看的地面。", 120, FurnitureSlot.Flat, 1.6, 1.2) { Appeal = 1 });
            Add(new FurnitureDef("plant", "绿萝盆栽", "屋里有点生气。", 60, FurnitureSlot.Floor, 0.4, 0.4) { Appeal = 1 });
            Add(new FurnitureDef("desk", "书桌", "放电脑或者看书。", 350, FurnitureSlot.Floor, 1.2, 0.6) { Appeal = 1 });
            Add(new FurnitureDef("sofa", "小沙发", "可以窝着。", 800, FurnitureSlot.Floor, 1.6, 0.8) { Appeal = 2 });
            Add(new FurnitureDef("poster", "墙上海报", "盖住墙上的水渍。", 40, FurnitureSlot.Wall, 0.7, 0.05) { Appeal = 1 });

            // ---- 店里的摆设：装修要歇业一天 ----
            Add(new FurnitureDef("shop_plant", "门店绿植", "客流 +。", 80, FurnitureSlot.Floor, 0.5, 0.5, true) { Appeal = 1 });
            Add(new FurnitureDef("shop_sofa", "等候沙发", "排队的人能坐一会儿。客流 +，顾客多等 2 分钟。", 600, FurnitureSlot.Floor, 1.6, 0.8, true) { Appeal = 2, PatienceBonus = 2 });
            Add(new FurnitureDef("shop_dispenser", "饮水机", "顾客多等 3 分钟。", 350, FurnitureSlot.Floor, 0.5, 0.5, true) { Appeal = 1, PatienceBonus = 3 });
            Add(new FurnitureDef("shop_poster", "品牌海报", "客流 +。", 120, FurnitureSlot.Wall, 1.0, 0.05, true) { Appeal = 1 });
            Add(new FurnitureDef("shop_led", "LED 灯屏", "夜里很显眼。客流 ++。", 900, FurnitureSlot.Wall, 1.6, 0.1, true) { Appeal = 3 });
            foreach (FurnitureDef d in l) d.WeightKg = Weights.TryGetValue(d.Id, out double kg) ? kg : 10;
            return l;
        }

        public static FurnitureDef Get(string id)
        {
            foreach (FurnitureDef d in All)
                if (d.Id == id) return d;
            return null;
        }

        public static IEnumerable<FurnitureDef> Purchasable(bool forShop) => All.Where(d => !d.Starter && d.ForShop == forShop);

        /// <summary>开局时出租屋里摆好的家具。</summary>
        public static List<PlacedFurniture> StartHomeLayout() => new List<PlacedFurniture>
        {
            new PlacedFurniture { Id = BedBoard, X = 1.05, Z = 4.05, Rot = 0 },
            new PlacedFurniture { Id = WardrobeCloth, X = 5.65, Z = 3.1, Rot = 1 },
            new PlacedFurniture { Id = TableCooker, X = 4.6, Z = 4.25, Rot = 0 },
            new PlacedFurniture { Id = "stool", X = 4.4, Z = 3.3, Rot = 0 },
            new PlacedFurniture { Id = "fan_broken", X = 2.5, Z = 1.9, Rot = 0 },
            new PlacedFurniture { Id = "junk", X = 0.55, Z = 0.55, Rot = 0 },
        };
    }

    public readonly struct RectD
    {
        public readonly double MinX, MinZ, MaxX, MaxZ;

        public RectD(double minX, double minZ, double maxX, double maxZ)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public bool Overlaps(RectD o) => MinX < o.MaxX - 1e-6 && MaxX > o.MinX + 1e-6 && MinZ < o.MaxZ - 1e-6 && MaxZ > o.MinZ + 1e-6;
    }

    /// <summary>房间的尺寸和禁摆区。</summary>
    public sealed class RoomSpec
    {
        public double Width { get; }
        public double Depth { get; }

        /// <summary>地面家具中心能放的范围（门口、货架留出来）。</summary>
        public RectD FloorArea { get; }

        /// <summary>门口要留空的地面。</summary>
        public RectD DoorClear { get; }

        /// <summary>后墙上窗户（墙面家具不能挡，窗帘除外）：x 范围；没有窗则 MinX = MaxX。</summary>
        public double WindowMinX { get; }
        public double WindowMaxX { get; }

        /// <summary>右墙上门的 z 范围。</summary>
        public double DoorMinZ { get; }
        public double DoorMaxZ { get; }

        /// <summary>店里墙面家具挂得高，不会和货架冲突；家里没有这个概念。</summary>
        public bool IsShop { get; }

        private RoomSpec(double w, double d, RectD floor, RectD door, double wMin, double wMax, double dMin, double dMax, bool shop)
        {
            Width = w;
            Depth = d;
            FloorArea = floor;
            DoorClear = door;
            WindowMinX = wMin;
            WindowMaxX = wMax;
            DoorMinZ = dMin;
            DoorMaxZ = dMax;
            IsShop = shop;
        }

        public static readonly RoomSpec Home = new RoomSpec(6.0, 4.6, new RectD(0.05, 0.05, 5.95, 4.55), new RectD(4.7, 0.2, 6.0, 1.9), 2.55, 3.75, 0.45, 1.55, false);

        public static RoomSpec ForShop(ShopSize size)
        {
            (double w, double d) = size == ShopSize.Large ? (18.0, 12.0) : size == ShopSize.Medium ? (12.0, 9.0) : (8.0, 6.0);
            return new RoomSpec(w, d, new RectD(0.35, 0.45, w - 0.95, d - 1.25), new RectD(0, 0, 2.6, 1.8), 0, 0, 0, 0, true);
        }
    }

    /// <summary>摆放规则：不能出界、不能压在别的家具上、不能挡门。</summary>
    public static class RoomPlanner
    {
        public const double Grid = 0.25;

        public static bool IsRotated(int rot) => (rot & 1) == 1;

        /// <summary>占地（墙面家具给出贴墙的一条）。</summary>
        public static RectD Footprint(RoomSpec room, FurnitureDef def, PlacedFurniture p)
        {
            if (def.Slot == FurnitureSlot.Wall)
            {
                double half = def.Width / 2;
                return p.Rot == 1
                    ? new RectD(room.Width - 0.12, p.Z - half, room.Width, p.Z + half)
                    : new RectD(p.X - half, room.Depth - 0.12, p.X + half, room.Depth);
            }
            double w = IsRotated(p.Rot) ? def.Depth : def.Width;
            double d = IsRotated(p.Rot) ? def.Width : def.Depth;
            return new RectD(p.X - w / 2, p.Z - d / 2, p.X + w / 2, p.Z + d / 2);
        }

        /// <summary>鼠标位置吸附到网格；地面家具会被推回房间里（贴墙放很方便）。</summary>
        public static PlacedFurniture Snap(RoomSpec room, FurnitureDef def, double x, double z, int rot)
        {
            var p = new PlacedFurniture { Id = def.Id, X = Math.Round(x / Grid) * Grid, Z = Math.Round(z / Grid) * Grid, Rot = rot & 3 };
            if (def.Slot == FurnitureSlot.Wall)
            {
                p.Rot = p.Rot == 1 ? 1 : 0;
                double half = def.Width / 2;
                if (p.Rot == 1) p.Z = Math.Max(half + 0.1, Math.Min(room.Depth - half - 0.1, p.Z));
                else p.X = Math.Max(half + 0.1, Math.Min(room.Width - half - 0.1, p.X));
                return p;
            }
            double w = IsRotated(p.Rot) ? def.Depth : def.Width;
            double d = IsRotated(p.Rot) ? def.Width : def.Depth;
            RectD f = room.FloorArea;
            p.X = Math.Max(f.MinX + w / 2, Math.Min(f.MaxX - w / 2, p.X));
            p.Z = Math.Max(f.MinZ + d / 2, Math.Min(f.MaxZ - d / 2, p.Z));
            return p;
        }

        /// <summary>能摆返回 null，否则返回原因。ignore 是正在移动的那件（不和自己比）。</summary>
        public static string Check(RoomSpec room, IReadOnlyList<PlacedFurniture> placed, FurnitureDef def, PlacedFurniture cand, int ignore, IReadOnlyList<RectD> extraBlockers)
        {
            RectD r = Footprint(room, def, cand);
            if (def.Slot == FurnitureSlot.Wall)
            {
                if (cand.Rot == 1)
                {
                    if (r.MinZ < 0.05 || r.MaxZ > room.Depth - 0.05) return "超出墙面。";
                    if (!room.IsShop && !def.Id.Equals(FurnitureCatalog.Curtain) && r.MinZ < room.DoorMaxZ && r.MaxZ > room.DoorMinZ) return "挡住门了。";
                }
                else
                {
                    if (r.MinX < 0.05 || r.MaxX > room.Width - 0.05) return "超出墙面。";
                    if (!room.IsShop && !def.Id.Equals(FurnitureCatalog.Curtain) && r.MinX < room.WindowMaxX && r.MaxX > room.WindowMinX) return "挡住窗户了。";
                }
                for (int i = 0; i < placed.Count; i++)
                {
                    if (i == ignore) continue;
                    FurnitureDef od = FurnitureCatalog.Get(placed[i].Id);
                    if (od == null || od.Slot != FurnitureSlot.Wall) continue;
                    if (placed[i].Rot != cand.Rot) continue;
                    if (r.Overlaps(Footprint(room, od, placed[i]))) return "墙上这个位置已经挂了东西。";
                }
                return null;
            }

            RectD f = room.FloorArea;
            if (r.MinX < f.MinX - 1e-6 || r.MaxX > f.MaxX + 1e-6 || r.MinZ < f.MinZ - 1e-6 || r.MaxZ > f.MaxZ + 1e-6) return "超出房间。";
            if (def.Slot == FurnitureSlot.Floor)
            {
                if (r.Overlaps(room.DoorClear)) return "挡住门口了。";
                if (extraBlockers != null)
                    foreach (RectD b in extraBlockers)
                        if (r.Overlaps(b)) return "那里有固定的东西。";
            }
            for (int i = 0; i < placed.Count; i++)
            {
                if (i == ignore) continue;
                FurnitureDef od = FurnitureCatalog.Get(placed[i].Id);
                if (od == null || od.Slot == FurnitureSlot.Wall) continue;
                bool bothFlat = def.Slot == FurnitureSlot.Flat && od.Slot == FurnitureSlot.Flat;
                bool bothFloor = def.Slot == FurnitureSlot.Floor && od.Slot == FurnitureSlot.Floor;
                if (!bothFlat && !bothFloor) continue;
                if (r.Overlaps(Footprint(room, od, placed[i]))) return "和别的家具重叠了。";
            }
            return null;
        }
    }
}
