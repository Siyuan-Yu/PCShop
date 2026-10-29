using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>
    /// 衣柜里的“镜子”：远处一个小舞台 + 一台只渲染到 RenderTexture 的相机，
    /// 把主角按当前衣服画成一张图给界面用。每次需要时手动渲染一次，不占每帧开销。
    /// </summary>
    public sealed class AvatarPreview
    {
        private static readonly Vector3 Origin = new Vector3(4000, 0, 4000);

        private readonly GameObject _root;
        private readonly GameObject _figure;
        private readonly Camera _cam;

        public RenderTexture Texture { get; }

        public AvatarPreview()
        {
            Texture = new RenderTexture(300, 380, 16) { name = "AvatarPreview" };
            _root = new GameObject("AvatarPreview");
            _root.transform.position = Origin;

            _figure = Prims.Figure(_root.transform, "Figure", new Color(0.95f, 0.55f, 0.15f), new Color(0.95f, 0.8f, 0.65f), 1.1f);
            _figure.transform.localRotation = Quaternion.Euler(0, -22f, 0);
            Prims.Shape(PrimitiveType.Cylinder, _root.transform, "Floor", new Vector3(0, -0.02f, 0), new Vector3(2.4f, 0.02f, 2.4f), Mats.Lit(new Color(0.33f, 0.31f, 0.3f)));

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(_root.transform, false);
            camGo.transform.localPosition = new Vector3(0, 0.82f, 4.4f);
            camGo.transform.localRotation = Quaternion.Euler(2f, 180f, 0);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.17f, 0.19f, 0.24f);
            _cam.fieldOfView = 28f;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 20f;
            _cam.targetTexture = Texture;
            _cam.enabled = false;

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(_root.transform, false);
            lightGo.transform.localPosition = new Vector3(1.6f, 2.4f, 2.6f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 6f;
            light.color = new Color(1f, 0.96f, 0.9f);
        }

        /// <summary>按这件衣服换装，渲染一次。</summary>
        public void Render(string outfitId)
        {
            Prims.Dress(_figure, outfitId);
            _cam.Render();
        }

        public void Destroy()
        {
            Kill.It(_root);
            if (Texture != null) Texture.Release();
        }
    }
}