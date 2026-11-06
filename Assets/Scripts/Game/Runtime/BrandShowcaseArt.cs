using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.Runtime
{
    // Shared transparent layers. The existing studio / 3D venue remains the background.
    public static class BrandShowcaseArt
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, int> Models = ModelProducts.All
            .Select((p, i) => new { p.Id, Index = i }).ToDictionary(p => p.Id, p => p.Index);

        public readonly struct ProductLayer
        {
            public readonly Texture2D Texture;
            public readonly Rect Uv;
            public readonly Color Tint;
            public ProductLayer(Texture2D texture, Rect uv, Color tint) { Texture = texture; Uv = uv; Tint = tint; }
        }

        private static Texture2D Load(string path)
        {
            if (!Cache.TryGetValue(path, out var texture) || texture == null)
                Cache[path] = texture = Resources.Load<Texture2D>(path);
            return texture;
        }

        public static Texture2D Host(string person) => KolMarket.Get(person) == null ? null : Load("KolStudio/Hosts/" + person);
        public static Texture2D Studio(string kind) => Load("KolStudio/" + kind + "_studio");

        private static Rect Cell(int index, int columns, int rows) =>
            new Rect((index % columns) / (float)columns, 1f - (index / columns + 1f) / rows, 1f / columns, 1f / rows);

        public static ProductLayer Product(string id)
        {
            if (string.IsNullOrEmpty(id)) return default;
            if (PartCatalog.Exists(id))
                return new ProductLayer(Load("KolStudio/Products/parts"), Cell((int)PartCatalog.Get(id).Category, 3, 2), Color.white);
            if (Models.TryGetValue(id, out int model))
                return new ProductLayer(Load("KolStudio/Products/models"), Cell(model, 4, 3), Color.white);
            var outfit = ClothingProducts.Outfit(id);
            if (outfit == null) return default;
            int cell = outfit.Id == "knit_basic" ? 7 : outfit.VisualPreset == "short" ? 0 :
                outfit.VisualPreset == "sport" ? 1 : outfit.VisualPreset == "long" ? 2 :
                outfit.VisualPreset == "winter" ? 3 : outfit.VisualPreset == "jacket" ? 4 :
                outfit.VisualPreset == "sport_long" ? 5 : 6;
            ColorUtility.TryParseHtmlString("#" + outfit.ColorHex, out var tint);
            return new ProductLayer(Load("KolStudio/Products/clothing"), Cell(cell, 4, 2), tint);
        }

        public static void ApplyProduct(RawImage image, string product)
        {
            var art = Product(product);
            image.texture = art.Texture; image.uvRect = art.Uv; image.color = art.Tint;
            image.raycastTarget = false; image.enabled = art.Texture != null;
            var fit = image.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = art.Texture == null ? 1 : art.Texture.width * art.Uv.width / (art.Texture.height * art.Uv.height);
        }

        public static void ApplyHost(RawImage image, string person)
        {
            image.texture = Host(person); image.color = Color.white; image.raycastTarget = false;
            image.enabled = image.texture != null;
            var fit = image.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = image.texture == null ? .75f : (float)image.texture.width / image.texture.height;
        }
    }
}
