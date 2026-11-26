using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.UI
{
    internal static class ScenePortraits
    {
        private static readonly Dictionary<Texture2D, Texture2D> Cutouts = new Dictionary<Texture2D, Texture2D>();

        public static Texture2D FromPortrait(Texture2D source)
        {
            if (source == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
            if (Cutouts.TryGetValue(source, out var cached)) return cached;

            int width = source.width, height = source.height;
            var cutout = new Texture2D(width, height, TextureFormat.RGBA32, true)
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
                cutout.Apply(true, true);
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

        // Portraits are painted on a flat pale matte. Pixels are classified by their
        // distance to the matte colour sampled at the top corners:
        //  1. flood from the top/side edges with a tight tolerance (the bottom edge is
        //     skipped because white coats are cropped there);
        //  2. flat matte-coloured pockets enclosed by dark hair/outlines are removed too,
        //     otherwise the gaps between hair strands stay as grey patches;
        //  3. the light fringe along the silhouette is eaten for three pixels;
        //  4. near the silhouette, matte-like pixels surrounded by strong colour (thin
        //     gaps between strands, anti-aliased hair tips) are unmixed: the colour is
        //     replaced by the nearby foreground colour and alpha is the blend factor.
        private const int EdgeTolerance = 18;
        private const int PocketTolerance = 10;
        private const int FringeTolerance = 60;
        private const int PocketMinArea = 6;
        private const int PocketMaxArea = 20000;
        private const float PocketDarkRatio = .6f;
        private const int UnmixReach = 12;
        private const int UnmixWindow = 3;
        private const int StrongDifference = 60;
        private const int UnmixContrast = 40;
        private const float UnmixStrongRatio = .4f;

        private static void RemoveBackdrop(Color32[] pixels, int width, int height)
        {
            int top = (height - 1) * width;
            if (pixels[top].a < 48 && pixels[top + width - 1].a < 48) return;

            int corner = Math.Min(10, Math.Min(width, height));
            var left = MedianColor(pixels, width, 0, height - corner, corner, corner);
            var right = MedianColor(pixels, width, width - corner, height - corner, corner, corner);
            int count = pixels.Length;
            var distance = new byte[count];
            var nearLeft = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int toLeft = Difference(pixels[i], left), toRight = Difference(pixels[i], right);
                nearLeft[i] = toLeft <= toRight;
                distance[i] = (byte)Math.Min(255, Math.Min(toLeft, toRight));
            }

            var backdrop = new bool[count];
            var stack = new Stack<int>();
            void Seed(int index)
            {
                if (backdrop[index] || distance[index] > EdgeTolerance) return;
                backdrop[index] = true;
                stack.Push(index);
            }
            int sideStart = Mathf.Max(0, Mathf.RoundToInt(height * .1f));
            for (int x = 0; x < width; x++) Seed(top + x);
            for (int y = sideStart; y < height; y++) { Seed(y * width); Seed(y * width + width - 1); }
            while (stack.Count != 0)
            {
                int i = stack.Pop(), x = i % width;
                if (x > 0) Seed(i - 1);
                if (x < width - 1) Seed(i + 1);
                if (i >= width) Seed(i - width);
                if (i < top) Seed(i + width);
            }

            RemovePockets(pixels, distance, backdrop, width);

            for (int pass = 0; pass < 3; pass++)
            {
                var grow = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    if (backdrop[i] || distance[i] > FringeTolerance) continue;
                    int x = i % width;
                    if ((x > 0 && backdrop[i - 1]) || (x < width - 1 && backdrop[i + 1])
                        || (i >= width && backdrop[i - width]) || (i < top && backdrop[i + width])) grow.Add(i);
                }
                foreach (int i in grow) backdrop[i] = true;
            }

            var alpha = new float[count];
            for (int i = 0; i < count; i++) alpha[i] = backdrop[i] ? 0f : 1f;
            Unmix(pixels, distance, nearLeft, backdrop, alpha, left, right, width, height);
            BleedColor(pixels, alpha, width, height);
            SoftenAlpha(pixels, alpha, width, height);
        }

        // Transparent pixels still carry the matte colour, and bilinear/mip sampling mixes it
        // back in as a pale outline. Spread the foreground colour outwards over them.
        private static void BleedColor(Color32[] pixels, float[] alpha, int width, int height)
        {
            int count = pixels.Length;
            var filled = new bool[count];
            var frontier = new List<int>();
            long r = 0, g = 0, b = 0, n = 0;
            for (int i = 0; i < count; i++)
            {
                filled[i] = alpha[i] >= .5f;
                if (!filled[i]) continue;
                r += pixels[i].r; g += pixels[i].g; b += pixels[i].b; n++;
            }
            if (n == 0) return;
            for (int pass = 0; pass < 8; pass++)
            {
                frontier.Clear();
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (filled[i]) continue;
                    int sr = 0, sg = 0, sb = 0, sn = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int j = ny * width + nx;
                        if (!filled[j]) continue;
                        sr += pixels[j].r; sg += pixels[j].g; sb += pixels[j].b; sn++;
                    }
                    if (sn == 0) continue;
                    pixels[i] = new Color32((byte)(sr / sn), (byte)(sg / sn), (byte)(sb / sn), pixels[i].a);
                    frontier.Add(i);
                }
                if (frontier.Count == 0) break;
                foreach (int i in frontier) filled[i] = true;
            }
            var average = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
            for (int i = 0; i < count; i++)
                if (!filled[i]) pixels[i] = new Color32(average.r, average.g, average.b, pixels[i].a);
        }

        private static void RemovePockets(Color32[] pixels, byte[] distance, bool[] backdrop, int width)
        {
            int count = pixels.Length;
            var candidate = new bool[count];
            for (int i = 0; i < count; i++) candidate[i] = !backdrop[i] && distance[i] <= PocketTolerance;
            var visited = new bool[count];
            var stamp = new int[count];
            var component = new List<int>();
            var ring = new List<int>();
            var stack = new Stack<int>();
            int id = 0;
            for (int start = 0; start < count; start++)
            {
                if (!candidate[start] || visited[start]) continue;
                component.Clear();
                visited[start] = true;
                stack.Push(start);
                while (stack.Count != 0)
                {
                    int i = stack.Pop(), x = i % width;
                    component.Add(i);
                    if (x > 0 && candidate[i - 1] && !visited[i - 1]) { visited[i - 1] = true; stack.Push(i - 1); }
                    if (x < width - 1 && candidate[i + 1] && !visited[i + 1]) { visited[i + 1] = true; stack.Push(i + 1); }
                    if (i >= width && candidate[i - width] && !visited[i - width]) { visited[i - width] = true; stack.Push(i - width); }
                    if (i < count - width && candidate[i + width] && !visited[i + width]) { visited[i + width] = true; stack.Push(i + width); }
                }
                if (component.Count < PocketMinArea || component.Count > PocketMaxArea) continue;

                // Breadth-first dilation by three pixels; the ring is distance 2..3.
                id++;
                foreach (int i in component) stamp[i] = id;
                var frontier = new List<int>(component);
                ring.Clear();
                for (int depth = 1; depth <= 3; depth++)
                {
                    var next = new List<int>();
                    foreach (int i in frontier)
                    {
                        int x = i % width;
                        Visit(x > 0, i - 1);
                        Visit(x < width - 1, i + 1);
                        Visit(i >= width, i - width);
                        Visit(i < count - width, i + width);
                    }
                    frontier = next;

                    void Visit(bool inside, int index)
                    {
                        if (!inside || stamp[index] == id) return;
                        stamp[index] = id;
                        next.Add(index);
                        if (depth >= 2 && !candidate[index]) ring.Add(index);
                    }
                }
                if (ring.Count == 0) continue;
                int dark = 0;
                foreach (int i in ring)
                {
                    var p = pixels[i];
                    if ((p.r + p.g + p.b) < 450) dark++;
                }
                if (dark < ring.Count * PocketDarkRatio) continue;
                foreach (int i in component) backdrop[i] = true;
            }
        }

        private static void Unmix(Color32[] pixels, byte[] distance, bool[] nearLeft, bool[] backdrop, float[] alpha,
            Color32 left, Color32 right, int width, int height)
        {
            int count = pixels.Length;
            var reach = new int[count];
            var queue = new Queue<int>();
            for (int i = 0; i < count; i++)
            {
                if (backdrop[i]) { reach[i] = 0; queue.Enqueue(i); }
                else reach[i] = int.MaxValue;
            }
            while (queue.Count != 0)
            {
                int i = queue.Dequeue(), x = i % width, next = reach[i] + 1;
                if (next > UnmixReach) continue;
                if (x > 0 && reach[i - 1] > next) { reach[i - 1] = next; queue.Enqueue(i - 1); }
                if (x < width - 1 && reach[i + 1] > next) { reach[i + 1] = next; queue.Enqueue(i + 1); }
                if (i >= width && reach[i - width] > next) { reach[i - width] = next; queue.Enqueue(i - width); }
                if (i < count - width && reach[i + width] > next) { reach[i + width] = next; queue.Enqueue(i + width); }
            }

            var source = (Color32[])pixels.Clone();
            int window = (2 * UnmixWindow + 1) * (2 * UnmixWindow + 1);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (backdrop[i] || reach[i] > UnmixReach || distance[i] > StrongDifference) continue;
                int best = distance[i], bestIndex = i, strong = 0;
                for (int dy = -UnmixWindow; dy <= UnmixWindow; dy++)
                for (int dx = -UnmixWindow; dx <= UnmixWindow; dx++)
                {
                    int nx = Mathf.Clamp(x + dx, 0, width - 1), ny = Mathf.Clamp(y + dy, 0, height - 1);
                    int n = ny * width + nx;
                    if (backdrop[n]) continue;
                    if (distance[n] >= StrongDifference) strong++;
                    if (distance[n] > best) { best = distance[n]; bestIndex = n; }
                }
                if (best < distance[i] + UnmixContrast || strong < window * UnmixStrongRatio) continue;

                var matte = nearLeft[i] ? left : right;
                var fore = source[bestIndex];
                var pixel = source[i];
                float fr = fore.r - matte.r, fg = fore.g - matte.g, fb = fore.b - matte.b;
                float pr = pixel.r - matte.r, pg = pixel.g - matte.g, pb = pixel.b - matte.b;
                float length = Mathf.Max(1f, fr * fr + fg * fg + fb * fb);
                alpha[i] = Mathf.Clamp01((pr * fr + pg * fg + pb * fb) / length);
                pixels[i] = new Color32(fore.r, fore.g, fore.b, pixel.a);
            }
        }

        private static void SoftenAlpha(Color32[] pixels, float[] alpha, int width, int height)
        {
            int count = pixels.Length;
            // Gaussian blur, sigma 0.5, separable.
            const float side = .1353f, centre = 1f;
            float sum = centre + 2f * side;
            var temp = new float[count];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                float l = alpha[y * width + Math.Max(0, x - 1)], r = alpha[y * width + Math.Min(width - 1, x + 1)];
                temp[i] = (alpha[i] * centre + (l + r) * side) / sum;
            }
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                float d = temp[Math.Max(0, y - 1) * width + x], u = temp[Math.Min(height - 1, y + 1) * width + x];
                float v = (temp[i] * centre + (d + u) * side) / sum;
                // The portrait is cropped at the waist; fade the last rows so it does not end in a hard line.
                float fade = Mathf.SmoothStep(0f, 1f, y / (height * .05f));
                pixels[i].a = (byte)Mathf.Clamp(Mathf.RoundToInt(v * fade * pixels[i].a), 0, 255);
            }
        }

        private static Color32 MedianColor(Color32[] pixels, int width, int x0, int y0, int w, int h)
        {
            var r = new List<byte>(w * h);
            var g = new List<byte>(w * h);
            var b = new List<byte>(w * h);
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                var p = pixels[y * width + x];
                r.Add(p.r); g.Add(p.g); b.Add(p.b);
            }
            r.Sort(); g.Sort(); b.Sort();
            int mid = r.Count / 2;
            return new Color32(r[mid], g[mid], b[mid], 255);
        }

        private static int Difference(Color32 a, Color32 b) =>
            Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));

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
