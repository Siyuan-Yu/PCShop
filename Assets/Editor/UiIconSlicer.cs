using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static class UiIconSlicer
    {
        private const string AssetDirectory = "Assets/Resources/UiArt/Icons";
        private const string DesignDirectory = "docs/art-direction/concept/ui-redesign";
        private const string ReferenceDirectory = DesignDirectory + "/target";
        private const string ContactDirectory = "docs/previews/ui-icons";
        private const int Size = 256;

        private sealed class Cell
        {
            public string Name, Label;
            public RectInt Rect;
        }

        [MenuItem("PCShop/界面/导出单张图标")]
        public static void Export()
        {
            Directory.CreateDirectory(AssetDirectory);
            Directory.CreateDirectory(ContactDirectory);
            var written = new List<Cell>();
            ExportSheet(DesignDirectory + "/place-category-icons.jpg", Grid(
                "Shop:我的店 Home:住处 Agency:房产中介 Wholesale:批发货源 Leisure:娱乐 PublicServices:公共服务 Station:车站交通 Garage:车行车库",
                new[] { 54, 358, 659, 962 }, new[] { 252, 251, 254, 254 }, new[] { 45, 368 }, new[] { 247, 237 }));
            ExportSheet(DesignDirectory + "/icons-status.jpg", Grid(
                "Hunger:饱腹 Energy:精力 Health:健康 Stress:压力 Prestige:名望 Cash:现金 Clock:时间 Load:负重 Intelligence:智力 Charm:魅力 Hands:动手能力 Fitness:体能",
                new[] { 87, 382, 672, 967 }, new[] { 221, 222, 220, 220 }, new[] { 29, 263, 483 }, new[] { 183, 174, 170 }));
            ExportSheet(DesignDirectory + "/icons-phone.jpg", Grid(
                "AppStore:应用商店 Contacts:通讯录 Settings:设置 Calendar:日历 MealDelivery:外卖 OnlineShopping:网购 Loans:借贷 Live:直播 Stocks:证券",
                new[] { 239, 540, 836 }, new[] { 198, 199, 198 }, new[] { 27, 256, 475 }, new[] { 181, 174, 175 }));
            ExportSheet(DesignDirectory + "/icons-shop.jpg", Grid(
                "Counter:收银台 Stock:库存 Pricing:定价 Orders:订单 Renovate:装修 Employees:员工 Shelves:货架 Display:展示台 Assembly:装机台 Report:经营报表 Procurement:采购 Brand:品牌",
                new[] { 103, 385, 669, 949 }, new[] { 221, 219, 218, 218 }, new[] { 27, 260, 479 }, new[] { 190, 178, 178 }));
            ExportSheet(DesignDirectory + "/icons-home.jpg", Grid(
                "Sleep:睡觉 Cooker:电饭锅 Fridge:冰箱 Wardrobe:衣柜 Repair:维修 Workbench:工作台 Parcel:快递货箱 Weather:天气 Notice:通知 Quests:任务 Relations:关系 Ledger:账本",
                new[] { 116, 394, 668, 939 }, new[] { 222, 217, 216, 217 }, new[] { 28, 263, 482 }, new[] { 188, 174, 169 }));
            ExportSheet(DesignDirectory + "/icons-controls.jpg", new[]
            {
                Tile("Backpack:背包", 881, 370, 51, 52), Tile("Phone:手机", 963, 370, 34, 52),
                Tile("Racing:赛车", 1114, 453, 48, 49), Tile("Walk:步行", 965, 531, 37, 50),
                Tile("Car:打车", 880, 537, 55, 44), Tile("Truck:叫货车", 1186, 457, 56, 45)
            });
            ExportSheet(ReferenceDirectory + "/00-hud-street.jpg", new[] { Tile("Profile:我的", 24, 73, 25, 26) });
            ExportSheet(ReferenceDirectory + "/60-system-menu.jpg", new[]
            {
                Tile("Play:继续游戏", 466, 175, 27, 31), Tile("Save:保存游戏", 461, 226, 33, 33),
                Tile("Folder:加载存档", 460, 280, 36, 32), Tile("Help:帮助", 461, 383, 32, 34),
                Tile("About:关于", 460, 436, 35, 34), Tile("Door:返回主菜单", 463, 488, 29, 34),
                Tile("Power:退出游戏", 463, 544, 29, 30)
            });

            var iconType = typeof(Ui).Assembly.GetType("PCShopSell.Game.UI.UiIcon", true);
            var missing = Enum.GetNames(iconType).Where(name => name != "None" && written.All(cell => cell.Name != name)).ToArray();
            if (missing.Length != 0) throw new InvalidOperationException("图标缺少源文件：" + string.Join("、", missing));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var cell in written)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath($"{AssetDirectory}/{cell.Name}.png");
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = Size;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            WriteContactSheet(written);

            void ExportSheet(string path, IEnumerable<Cell> cells)
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(path);
                    foreach (var cell in cells)
                    {
                        File.WriteAllBytes($"{AssetDirectory}/{cell.Name}.png", Cut(source, cell.Rect));
                        written.Add(cell);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
        }

        private static Cell Tile(string pair, int x, int y, int w, int h)
        {
            int colon = pair.IndexOf(':');
            return new Cell { Name = pair.Substring(0, colon), Label = pair.Substring(colon + 1), Rect = new RectInt(x, y, w, h) };
        }

        private static IEnumerable<Cell> Grid(string labels, int[] x, int[] width, int[] y, int[] height)
        {
            var pairs = labels.Split(' ');
            for (int i = 0; i < pairs.Length; i++)
            {
                int col = i % x.Length, row = i / x.Length;
                yield return Tile(pairs[i], x[col], y[row], width[col], height[row]);
            }
        }

        private static byte[] Cut(Texture2D source, RectInt topLeft)
        {
            var pixels = source.GetPixels(topLeft.x, source.height - topLeft.yMax, topLeft.width, topLeft.height);
            int width = topLeft.width, height = topLeft.height;
            Color corner = (pixels[0] + pixels[width - 1] + pixels[(height - 1) * width] + pixels[pixels.Length - 1]) / 4f;
            var outside = new bool[pixels.Length];
            var open = new Queue<int>();
            for (int x = 0; x < width; x++) { Visit(x, 0); Visit(x, height - 1); }
            for (int y = 1; y < height - 1; y++) { Visit(0, y); Visit(width - 1, y); }
            while (open.Count != 0)
            {
                int position = open.Dequeue(), x = position % width, y = position / width;
                if (x > 0) Visit(x - 1, y);
                if (x + 1 < width) Visit(x + 1, y);
                if (y > 0) Visit(x, y - 1);
                if (y + 1 < height) Visit(x, y + 1);
            }
            var crop = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var output = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                for (int i = 0; i < pixels.Length; i++) if (outside[i]) pixels[i].a = 0;
                crop.SetPixels(pixels);
                crop.Apply(false);
                float scale = (Size - 4f) / Mathf.Max(width, height);
                float paddedX = (Size - width * scale) * .5f, paddedY = (Size - height * scale) * .5f;
                var square = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + .5f - paddedX) / (width * scale), v = (y + .5f - paddedY) / (height * scale);
                    if (u is >= 0 and <= 1 && v is >= 0 and <= 1)
                        square[y * Size + x] = crop.GetPixelBilinear(u, v);
                }
                output.SetPixels(square);
                output.Apply(false);
                return output.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(crop);
                UnityEngine.Object.DestroyImmediate(output);
            }

            void Visit(int x, int y)
            {
                int i = y * width + x;
                if (outside[i]) return;
                Color p = pixels[i];
                float distance = Mathf.Max(Mathf.Abs(p.r - corner.r), Mathf.Abs(p.g - corner.g), Mathf.Abs(p.b - corner.b));
                if (distance > .086f) return;
                outside[i] = true;
                open.Enqueue(i);
            }
        }

        private static void WriteContactSheet(IEnumerable<Cell> cells)
        {
            var html = new StringBuilder("<!doctype html><meta charset=utf-8><title>界面图标对照</title>"
                + "<style>body{background:#efece6;color:#2f2c28;font:16px 'Microsoft YaHei',sans-serif;margin:32px}main{display:grid;grid-template-columns:repeat(8,1fr);gap:12px}figure{margin:0;padding:8px;text-align:center;border:1px solid #c2b8ab;border-radius:10px}img{display:block;width:80px;height:80px;margin:auto}code{display:block;margin-top:4px}</style>"
                + "<h1>界面图标对照</h1><main>");
            foreach (var cell in cells)
                html.Append($"<figure><img alt=\"{cell.Label}\" src=\"../../../{AssetDirectory}/{cell.Name}.png\"><figcaption>{cell.Label}<code>{cell.Name}</code></figcaption></figure>");
            html.Append("</main>");
            File.WriteAllText(ContactDirectory + "/index.html", html.ToString(), Encoding.UTF8);
        }
    }
}
