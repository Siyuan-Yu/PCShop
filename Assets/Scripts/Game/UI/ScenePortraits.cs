using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.UI
{
    internal static class ScenePortraits
    {
        private const int BackgroundTolerance = 22;
        private const int OpaqueDifference = 55;
        private static readonly Dictionary<Texture2D, Texture2D> Cutouts = new Dictionary<Texture2D, Texture2D>();

        public static Texture2D FromPortrait(Texture2D source)
        {
            if (source == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
            if (Cutouts.TryGetValue(source, out var cached)) return cached;

            int width = source.width, height = source.height;
            var cutout = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = source.name + " Scene Portrait",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            RenderTexture target = null;
            try
            {
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                cutout.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                var pixels = cutout.GetPixels32();
                RemoveBackdrop(pixels, width, height);
                cutout.SetPixels32(pixels);
                cutout.Apply(false, true);
                Cutouts[source] = cutout;
                return cutout;
            }
            catch (Exception error)
            {
                Release(cutout);
                Debug.LogWarning($"Could not composite scene portrait {source.name}: {error.Message}");
                Cutouts[source] = null;
                return null;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                GL.sRGBWrite = previousSrgb;
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void RemoveBackdrop(Color32[] pixels, int width, int height)
        {
            int upperRow = (height - 1) * width;
            var left = pixels[upperRow];
            var right = pixels[upperRow + width - 1];
            if (left.a < 48 && right.a < 48) return;

            // Stock portraits are painted on a pale matte. Sample both sides so a
            // slight gradient or a differently shaded strip can be removed as well.
            var backdrop = new bool[pixels.Length];
            var frontier = new Queue<int>();
            for (int y = 0; y < height; y++)
            {
                Mark(y * width);
                Mark(y * width + width - 1);
            }
            for (int x = 0; x < width; x++)
            {
                Mark(x);
                Mark(upperRow + x);
            }
            while (frontier.Count != 0)
            {
                int i = frontier.Dequeue(), x = i % width;
                if (x > 0) Mark(i - 1);
                if (x < width - 1) Mark(i + 1);
                if (i >= width) Mark(i - width);
                if (i < upperRow) Mark(i + width);
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                if (backdrop[i])
                {
                    pixels[i].a = 0;
                    continue;
                }
                int x = i % width;
                bool onSilhouette = (x > 0 && backdrop[i - 1]) || (x < width - 1 && backdrop[i + 1])
                    || (i >= width && backdrop[i - width]) || (i < upperRow && backdrop[i + width]);
                if (!onSilhouette) continue;

                // Partially covered pixels contain some matte. Remove that color
                // at the silhouette while keeping similar colors inside the character.
                var pixel = pixels[i];
                var matte = Difference(pixel, left) <= Difference(pixel, right) ? left : right;
                float alpha = Mathf.InverseLerp(BackgroundTolerance, OpaqueDifference, Difference(pixel, matte));
                if (alpha >= 1f) continue;
                pixels[i] = new Color32(Unblend(pixel.r, matte.r, alpha), Unblend(pixel.g, matte.g, alpha),
                    Unblend(pixel.b, matte.b, alpha), (byte)Mathf.RoundToInt(pixel.a * alpha));
            }

            void Mark(int index)
            {
                if (backdrop[index] || (Difference(pixels[index], left) > BackgroundTolerance
                    && Difference(pixels[index], right) > BackgroundTolerance)) return;
                backdrop[index] = true;
                frontier.Enqueue(index);
            }
        }

        private static int Difference(Color32 a, Color32 b) =>
            Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));

        private static byte Unblend(byte value, byte matte, float alpha) =>
            (byte)Mathf.Clamp(Mathf.RoundToInt(matte + (value - matte) / alpha), 0, 255);

        public static void Clear()
        {
            foreach (var texture in Cutouts.Values) Release(texture);
            Cutouts.Clear();
        }

        private static void Release(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
