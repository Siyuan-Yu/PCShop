using System;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public enum FishMovement { Steady, Surge, Dart, Bottom, Hover, Ambush }
    public sealed class FishDefinition
    {
        public readonly string Id, Name;
        public readonly double Kg, Difficulty;
        public readonly int SalePrice, Habitat, Bait, Weight;
        public readonly FishMovement Movement;
        public string Rarity => Weight <= 5 ? "珍稀" : Weight <= 10 ? "少见" : "常见";
        public string MovementName => new[] { "平稳游动", "间歇冲刺", "快速变向", "贴底挣扎", "上层游弋", "停顿突袭" }[(int)Movement];
        public string BehaviorHint => new[] { "平稳跟随，轻收轻放", "冲刺后会缓下来，别一直猛拉", "变向频繁，小幅点按跟鱼", "喜欢贴底，别把绿区拉得太高", "偏爱上层，适当收线保持高度", "先停顿再猛冲，留意突然变向" }[(int)Movement];
        public FishDefinition(string id, string name, double kg, int sale, int habitat, int bait, int weight, double difficulty, FishMovement movement)
        { Id = id; Name = name; Kg = kg; SalePrice = sale; Habitat = habitat; Bait = bait; Weight = weight; Difficulty = difficulty; Movement = movement; }
        public double Target(double seconds, double seed)
        {
            double t = seconds * Difficulty, cycle = (t + seed) % 7;
            switch (Movement)
            {
                case FishMovement.Surge: return cycle < 1.5 ? .5 + .34 * Math.Sin(t * 2 + seed) : .5 + .17 * Math.Sin(t * .65 + seed);
                case FishMovement.Dart: return .5 + .23 * Math.Sin(t * 1.15 + seed) + .07 * Math.Sin(t * 2.5 + seed * .3);
                case FishMovement.Bottom: return .25 + .12 * Math.Sin(t * 1.1 + seed) + .05 * Math.Sin(t * 2.5);
                case FishMovement.Hover: return .70 + .13 * Math.Sin(t * .8 + seed) + .04 * Math.Sin(t * 2);
                case FishMovement.Ambush: return cycle < 4 ? .48 + .055 * Math.Sin(t + seed) : .5 + .33 * Math.Sin(t * 2.1 + seed);
                default: return .5 + .22 * Math.Sin(t * .85 + seed) + .06 * Math.Sin(t * 1.9 + seed * .3);
            }
        }
        public double Speed(double seconds, double seed)
        {
            double factor = Movement == FishMovement.Surge && (seconds * Difficulty + seed) % 7 < 1.5 ? 1.35 : 1;
            return Math.Min(.55, .34 * Difficulty * factor);
        }
    }
    public static class FishingCatalog
    {
        public static readonly string[] Spots = { "浅滩", "芦苇边", "深水区" };
        public static readonly string[] Baits = { "面饵", "蚯蚓饵", "小鱼饵" };
        // Gameplay values for the managed fishing pond; weights are relative, not catch guarantees.
        public static readonly FishDefinition[] Fish = {
            new FishDefinition("fish_crucian", "鲫鱼", .6, 180, 0, 0, 38, .65, FishMovement.Steady),
            new FishDefinition("fish_carp", "鲤鱼", 1.2, 360, 1, 1, 30, 1, FishMovement.Surge),
            new FishDefinition("fish_bass", "鲈鱼", 2, 720, 2, 2, 24, 1.35, FishMovement.Dart),
            new FishDefinition("fish_white_stripe", "白条", .15, 120, 0, 0, 30, .65, FishMovement.Dart),
            new FishDefinition("fish_topmouth_gudgeon", "麦穗鱼", .08, 100, 0, 0, 24, .5, FishMovement.Steady),
            new FishDefinition("fish_bitterling", "鳑鲏", .12, 160, 0, 0, 18, .6, FishMovement.Hover),
            new FishDefinition("fish_loach", "泥鳅", .2, 220, 0, 1, 20, .75, FishMovement.Bottom),
            new FishDefinition("fish_yellow_catfish", "黄颡鱼", .45, 380, 1, 1, 20, .9, FishMovement.Bottom),
            new FishDefinition("fish_catfish", "鲶鱼", 2.5, 900, 2, 2, 18, 1.2, FishMovement.Bottom),
            new FishDefinition("fish_grass_carp", "草鱼", 3, 1000, 1, 0, 20, 1.15, FishMovement.Surge),
            new FishDefinition("fish_black_carp", "青鱼", 5, 1800, 2, 1, 10, 1.5, FishMovement.Surge),
            new FishDefinition("fish_silver_carp", "鲢鱼", 2.8, 850, 1, 0, 18, 1, FishMovement.Hover),
            new FishDefinition("fish_bighead_carp", "鳙鱼", 4, 1400, 2, 0, 12, 1.3, FishMovement.Surge),
            new FishDefinition("fish_bream", "鳊鱼", 1, 420, 1, 0, 25, .8, FishMovement.Steady),
            new FishDefinition("fish_yellowtail", "鲴鱼", .7, 480, 1, 1, 16, 1, FishMovement.Dart),
            new FishDefinition("fish_culter", "翘嘴", 1.8, 1100, 2, 2, 14, 1.4, FishMovement.Dart),
            new FishDefinition("fish_mandarin", "鳜鱼", 1.3, 1600, 2, 2, 8, 1.55, FishMovement.Ambush),
            new FishDefinition("fish_snakehead", "黑鱼", 2.2, 1300, 1, 2, 12, 1.3, FishMovement.Ambush),
            new FishDefinition("fish_swamp_eel", "黄鳝", .4, 680, 0, 1, 9, 1.1, FishMovement.Bottom),
            new FishDefinition("fish_farmed_sturgeon", "养殖鲟鱼", 6, 3200, 2, 1, 3, 1.7, FishMovement.Surge)
        };
        public static FishDefinition Get(string id) => Fish.FirstOrDefault(f => f.Id == id);
        public static double PoolWeight(FishDefinition fish, double distance, int spot, int bait)
        {
            int band = distance < 12 ? 0 : distance < 22 ? 1 : 2;
            int gap = Math.Abs(band - fish.Habitat);
            return fish.Weight * (gap == 0 ? 4 : gap == 1 ? 1 : 0) * (bait == fish.Bait ? 3 : 1) * (spot == fish.Habitat ? 1.3 : 1);
        }
        public static FishDefinition Pick(double distance, int spot, int bait, int roll)
        {
            double total = 0; foreach (var fish in Fish) total += PoolWeight(fish, distance, spot, bait);
            double ticket = Math.Max(0, Math.Min(9999, roll)) / 10000.0 * total;
            foreach (var fish in Fish) { ticket -= PoolWeight(fish, distance, spot, bait); if (ticket < 0) return fish; }
            return Fish[0];
        }
    }
}
