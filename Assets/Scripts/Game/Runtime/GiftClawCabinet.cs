using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.Game.Runtime
{
    /// <summary>A dedicated cabinet stage. The camera never sees the city or venue interior.</summary>
    internal sealed class GiftClawCabinet
    {
        private const int CabinetLayer = 28;
        private const float DeckY = .945f, RailY = 2.70f;
        private readonly Transform _root, _gantry, _carriage, _head, _wire;
        private readonly Transform[] _fingerRoots = new Transform[3];
        private readonly Transform[] _joints = new Transform[3], _tips = new Transform[3];
        private readonly Transform[] _pawns = new Transform[6];
        private readonly string[] _giftIds = new string[6];
        private readonly Dictionary<string, Material> _giftLabels = new Dictionary<string, Material>();
        private readonly Camera _camera;

        public RenderTexture Texture { get; }

        public GiftClawCabinet()
        {
            _root = new GameObject("IndependentClawCabinet").transform;
            _root.position = new Vector3(7000, 0, 7000);
            var enamel = Lit("6B9693");
            var trim = Lit("D4D9C6");
            var ink = Lit("38464A");

            Box(_root, "CabinetPlinth", new Vector3(0,-.075f,.02f), new Vector3(3.55f,.14f,2.50f), Lit("36433A"));
            Box(_root, "CabinetBase", new Vector3(0,.45f,0), new Vector3(3.06f,.90f,1.76f), enamel);
            Box(_root, "GiftBed", new Vector3(0,.90f,0), new Vector3(2.81f,.072f,1.59f), Lit("AA9D80"));
            Box(_root, "CabinetBack", new Vector3(0,1.87f,.865f), new Vector3(3.025f,2.05f,.13f), Lit("426663"));
            Box(_root, "CabinetTop", new Vector3(0,2.963f,0), new Vector3(3.06f,.26f,1.76f), enamel);
            foreach (float z in new[] { -.84f, .84f })
            {
                Box(_root, "TopTrim", new Vector3(0,2.818f,z), new Vector3(2.94f,.063f,.075f), trim);
                Box(_root, "BottomTrim", new Vector3(0,.945f,z), new Vector3(2.94f,.067f,.075f), trim);
                foreach(float x in new[] { -1.46f, 1.46f })
                    Box(_root, "GlassCorner", new Vector3(x,1.88f,z), new Vector3(.10f,1.93f,.10f), trim);
            }
            Box(_root, "ControlPaddle", new Vector3(0,.89f,-1.064f), new Vector3(3.07f,.16f,.47f), ink);
            var stick = Box(_root, "Joystick", new Vector3(-.96f,1.05f,-1.15f), new Vector3(.048f,.2f,.047f), ink);
            stick.localRotation = Quaternion.Euler(0,0,12);
            Prims.Shape(PrimitiveType.Sphere, _root, "JoystickKnob", new Vector3(-.984f,1.155f,-1.15f), Vector3.one*.13f, Lit("AC6153"));
            Prims.Shape(PrimitiveType.Cylinder, _root, "DropButton", new Vector3(.75f,.987f,-1.16f), new Vector3(.28f,.034f,.28f), Lit("B99058"));
            Box(_root, "Outlet", new Vector3(.92f,.39f,-.888f), new Vector3(.74f,.41f,.041f), Lit("243632"));
            Box(_root, "PrizeChute", new Vector3(.92f, .942f, -.675f), new Vector3(.54f, .030f, .22f), ink);
            Prims.SignText(_root, "取物口", new Vector3(.92f,.395f,-.917f), .65f,.15f, ArtDirection.Cream);
            Prims.SignText(_root, "礼品机", new Vector3(0,2.972f,-.897f), 1.32f,.20f, ArtDirection.Cream);

            foreach(float x in new[] { -1.32f, 1.32f })
                Box(_root, "GantryTrack", new Vector3(x,RailY+.03f,0), new Vector3(.045f,.045f,1.50f), trim);
            _gantry = Child(_root,"Gantry");
            Box(_gantry,"GantryBeam", Vector3.zero,new Vector3(2.65f,.075f,.075f),trim);
            _carriage = Child(_gantry,"Carriage");
            Box(_carriage, "MovingCarriage", new Vector3(0,-.058f,0),new Vector3(.32f,.143f,.215f),ink);
            _wire = Box(_carriage,"Cable", Vector3.zero,new Vector3(.032f,1,.032f),ink);
            _head = Child(_carriage,"ClawHead");
            Prims.Shape(PrimitiveType.Sphere,_head,"Hub",Vector3.zero,Vector3.one*.132f,trim);
            for(int i=0;i<3;i++)
            {
                var pivot = Child(_head,"ClawFinger");
                pivot.localRotation = Quaternion.Euler(0,i*120,0);
                _fingerRoots[i] = pivot;
                _joints[i] = Box(pivot,"FingerBase",Vector3.zero,new Vector3(.055f,.055f,1),trim);
                _tips[i] = Box(pivot,"FingerTip",Vector3.zero,new Vector3(.053f,.053f,1),trim);
            }

            _camera = Child(_root,"CabinetCamera").gameObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = ArtDirection.Hex("1D2421");
            _camera.cullingMask = 1 << CabinetLayer;
            _camera.nearClipPlane = .15f;
            _camera.farClipPlane = 30;
            _camera.orthographic = true;
            _camera.orthographicSize = 1.92f;
            _camera.transform.localPosition = new Vector3(-4.8f,3.47f,-8.6f);
            _camera.transform.LookAt(_root.TransformPoint(new Vector3(0,1.57f,0)));
            var pipeline = _camera.GetUniversalAdditionalCameraData();
            pipeline.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            Texture = new RenderTexture(1050,670,24);
            Texture.Create();
            _camera.targetTexture = Texture;
            _camera.aspect = 1050f/670;
            CabinetLight(new Vector3(15, -35, 0), 1.28f, "FFEDD0");
            CabinetLight(new Vector3(31, 150, 0), .53f, "D5E6E4");
            SetLayer(_root);
        }

        public void Render(GiftClawPlay state)
        {
            for (int i = 0; i < _pawns.Length; i++)
            {
                if (_giftIds[i] != state.Gifts[i])
                {
                    if (_pawns[i] != null) { _pawns[i].gameObject.SetActive(false); Kill.It(_pawns[i].gameObject); }
                    _giftIds[i] = state.Gifts[i];
                    _pawns[i] = string.IsNullOrEmpty(_giftIds[i]) ? null : MakeGift(_giftIds[i]);
                }
            }

            float x = (float)state.X, z = (float)state.Z;
            float height = Mathf.Lerp(1.53f,2.43f,(float)state.Height);
            _gantry.localPosition = new Vector3(0,RailY,z);
            _carriage.localPosition = Vector3.right*x;
            _head.localPosition = Vector3.up*(height-RailY);
            _head.localRotation = Quaternion.Euler(0, 0, -(float)state.Sway * 128f);
            _wire.localPosition = Vector3.up*((height-RailY-.1f)*.5f);
            _wire.localScale = new Vector3(.032f,RailY-.10f-height,.032f);
            float spread = Mathf.Lerp(.36f, .19f, (float)state.FingerClosure);
            for(int i=0;i<_fingerRoots.Length;i++)
            {
                Vector3 elbow = new Vector3(spread,-.17f,0);
                Vector3 tip = new Vector3(spread-.075f,-.31f,0);
                Rod(_joints[i],Vector3.zero,elbow);
                Rod(_tips[i],elbow,tip);
            }
            for (int i = 0; i < _pawns.Length; i++)
            {
                var gift = _pawns[i];
                if (gift == null) continue;
                var (slotX, slotZ) = state.GiftPosition(i);
                if (state.HeldPrize && i == state.Target)
                {
                    float drop = (float)state.ReleaseProgress;
                    float lift = DeckY + (float)state.Height * .90f;
                    gift.localPosition = new Vector3((float)state.HoldX, Mathf.Lerp(lift, DeckY - .33f, drop * drop),
                        (float)state.HoldZ);
                    gift.localRotation = Quaternion.Euler(0, 14, (float)state.Sway * 168f);
                }
                else if (i == state.Target && state.SlipProgress < 1)
                {
                    float fall = (float)state.SlipProgress;
                    gift.localPosition = new Vector3(Mathf.Lerp((float)state.FallX, (float)slotX, fall),
                        DeckY + .90f * (float)state.FallHeight * (1 - fall * fall),
                        Mathf.Lerp((float)state.FallZ, (float)slotZ, fall));
                    gift.localRotation = Quaternion.Euler(0, 14 + 64 * fall, Mathf.Sin(fall * Mathf.PI) * 42);
                }
                else
                {
                    gift.localPosition = new Vector3((float)slotX,DeckY,(float)slotZ);
                    gift.localRotation = Quaternion.Euler(0, i%2 == 0 ? 14 : -14, 0);
                }
            }
            _camera.Render();
        }

        private Transform MakeGift(string id)
        {
            var prop = Child(_root, "Gift_" + id);
            float height = id == Gifts.Snack ? .305f : .42f;
            var tint = id == Gifts.Snack ? Lit("AA7F5B") : id == Gifts.Soda ? Lit("9D6755") : Lit("789488");
            Box(prop,"ExistingGiftPack",new Vector3(0,height/2,0),new Vector3(.41f,height,.35f),tint);

            var art = GameObject.CreatePrimitive(PrimitiveType.Quad);
            art.transform.SetParent(prop,false);
            art.transform.localPosition = new Vector3(0,height/2,-.183f);
            art.transform.localScale = new Vector3(.33f,height*.84f,1);
            art.GetComponent<MeshRenderer>().sharedMaterial = GiftLabel(id);
            Kill.It(art.GetComponent<MeshCollider>());
            SetLayer(prop);
            return prop;
        }

        private Material GiftLabel(string id)
        {
            if (_giftLabels.TryGetValue(id, out var existing)) return existing;
            var cell = id switch
            {
                Gifts.Soda => 7, Gifts.Snack => 8,
                "model:harbor:skiff" => 0, "model:garden:mushroom" => 7,
                "model:starlight:lamp" => 9, _ => 6
            };
            var sheet = id is Gifts.Soda or Gifts.Snack ? "food" : "models";
            var label = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            label.SetTexture("_BaseMap", Resources.Load<Texture2D>("ItemPortraits/" + sheet));
            label.SetTextureScale("_BaseMap", new Vector2(.196f,.196f));
            label.SetTextureOffset("_BaseMap",new Vector2(cell%5*.2f+.002f,.8f-cell/5*.2f+.002f));
            _giftLabels.Add(id,label);
            return label;
        }

        private void CabinetLight(Vector3 angle,float strength,string color)
        {
            var light = Child(_root, "CabinetLight").gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = strength;
            light.color = ArtDirection.Hex(color);
            light.cullingMask = 1 << CabinetLayer;
            light.transform.localRotation = Quaternion.Euler(angle);
        }

        private static Material Lit(string hex) => Mats.Lit(ArtDirection.Hex(hex));
        private static Transform Box(Transform root,string name,Vector3 at,Vector3 size,Material mat)
            => Prims.Box(root,name,at,size,mat).transform;

        private static Transform Child(Transform parent,string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent,false);
            return child;
        }

        private static void Rod(Transform rod, Vector3 start, Vector3 end)
        {
            var delta = end-start;
            rod.localPosition = (start+end)*.5f;
            rod.localRotation = Quaternion.LookRotation(delta);
            var scale = rod.localScale;
            rod.localScale = new Vector3(scale.x,scale.y,delta.magnitude);
        }

        private static void SetLayer(Transform node)
        {
            node.gameObject.layer = CabinetLayer;
            foreach(Transform child in node) SetLayer(child);
        }

        public void Dispose()
        {
            _root.gameObject.SetActive(false);
            _camera.targetTexture = null;
            Texture.Release();
            Kill.It(Texture);
            Kill.It(_root.gameObject);
            foreach (var material in _giftLabels.Values) Kill.It(material);
            _giftLabels.Clear();
        }
    }
}
