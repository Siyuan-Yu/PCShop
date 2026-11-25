using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    /// <summary>Shared rig and materials for sculpted characters.</summary>
    internal sealed class PlayerCharacterModel : MonoBehaviour
    {
        internal const float HipHeight = .755f, LegSegment = .3425f, AnkleHeight = .07f;

        private enum Paint : byte { Skin, Hair, Brow, Eye, Lip, Top, Trim, Pants, Sole, Shoe, Cream, Tie }

        private sealed class ModelMesh
        {
            public Mesh Mesh;
            public Paint[] Paints;
        }

        private static readonly Dictionary<string, ModelMesh> Meshes = new Dictionary<string, ModelMesh>();
        private SkinnedMeshRenderer _core, _top, _legs;
        private Transform[] _bones;
        private Matrix4x4[] _bindPoses;
        private Material[] _paint;
        private bool _prefersSkirt;

        internal void Build(Transform visual, Transform torso, FigureAppearance appearance, NpcLook npc)
        {
            _bones = new Transform[15];
            _bones[0] = visual;
            _bones[1] = torso;
            _bones[2] = Joint(torso, "Head", new Vector3(0, 1.275f, .015f));
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1 : 1;
                string side = i == 0 ? "L" : "R";
                int arm = 3 + i * 3;
                _bones[arm] = Joint(torso, "Shoulder" + side, new Vector3(sign * .133824f, 1.165f, .00384f));
                _bones[arm + 1] = Joint(_bones[arm], "Elbow", new Vector3(sign * .055896f, -.269f, .00672f));
                _bones[arm + 2] = Joint(_bones[arm + 1], "Hand", new Vector3(sign * .03964f, -.191f, .01344f));
                int leg = 9 + i * 3;
                _bones[leg] = Joint(visual, "Hip" + side, new Vector3(sign * .09064f, HipHeight, 0));
                _bones[leg + 1] = Joint(_bones[leg], "Knee" + side, Vector3.down * LegSegment);
                _bones[leg + 2] = Joint(_bones[leg + 1], "Foot" + side, Vector3.down * LegSegment);
            }
            _bindPoses = new Matrix4x4[_bones.Length];
            Matrix4x4 meshToWorld = visual.localToWorldMatrix;
            for (int i = 0; i < _bones.Length; i++)
                _bindPoses[i] = _bones[i].worldToLocalMatrix * meshToWorld;

            _core = Renderer(visual, "Character");
            _paint = new Material[Enum.GetValues(typeof(Paint)).Length];
            SetPaint(Paint.Skin, "ECA16F", .25f, 1);
            SetPaint(Paint.Hair, "765039", .31f, 2);
            SetPaint(Paint.Brow, "523527");
            SetPaint(Paint.Eye, "30221E", .70f, 3);
            SetPaint(Paint.Lip, "96523F");
            SetPaint(Paint.Sole, "514335");
            SetPaint(Paint.Shoe, "6C5744", .23f, 4);
            SetPaint(Paint.Cream, "F0DDC0");
            SetPaint(Paint.Tie, "683C3F");
            if (appearance == FigureAppearance.Npc)
            {
                _prefersSkirt = npc.PrefersSkirt;
                _paint[(int)Paint.Skin] = PlayerPaint(npc.Skin, .25f, 1);
                _paint[(int)Paint.Hair] = PlayerPaint(npc.Hair, .31f, 2);
                _paint[(int)Paint.Brow] = PlayerPaint(Color.Lerp(npc.Hair, ArtDirection.Hex("382729"), .68f), .12f);
                _top = Renderer(visual, "Clothing");
                _legs = Renderer(visual, "Legwear and shoes");
                Apply(_core, "NpcHead-" + npc.Head);
                return;
            }
            if (appearance is FigureAppearance.Florist or FigureAppearance.Coach)
            {
                bool florist = appearance == FigureAppearance.Florist;
                SetPaint(Paint.Skin, florist ? "ECB090" : "DEA078", .25f, 1);
                SetPaint(Paint.Hair, florist ? "51353F" : "3C2C2C", .31f, 2);
                SetPaint(Paint.Brow, florist ? "69434C" : "3C2C2C");
                SetPaint(Paint.Lip, florist ? "A85862" : "955F50");
                SetPaint(Paint.Top, florist ? "A15C72" : "363B48");
                SetPaint(Paint.Trim, florist ? "7B4A62" : "9398A4");
                SetPaint(Paint.Pants, florist ? "68516D" : "343845");
                SetPaint(Paint.Cream, florist ? "F3DFCD" : "E2E1DC");
                SetPaint(Paint.Tie, "CFAB72", .28f, 4);
                SetPaint(Paint.Shoe, florist ? "63444E" : "4D535C", .23f, 4);
                SetPaint(Paint.Sole, florist ? "42333C" : "C3C2BC");
                Apply(_core, florist ? "Su" : "Zheng");
                return;
            }
            if (appearance != FigureAppearance.Player)
            {
                bool clerk = appearance == FigureAppearance.PhoneClerk;
                SetPaint(Paint.Skin, clerk ? "F0AE89" : "E7A37D", .25f, 1);
                SetPaint(Paint.Hair, clerk ? "473037" : "614139", .31f, 2);
                SetPaint(Paint.Brow, clerk ? "573741" : "322A2B");
                SetPaint(Paint.Lip, clerk ? "A45554" : "96523F");
                SetPaint(Paint.Top, clerk ? "41415C" : "984D3E");
                SetPaint(Paint.Trim, clerk ? "53536A" : "A35646");
                SetPaint(Paint.Pants, clerk ? "BCA99B" : "44404A");
                SetPaint(Paint.Cream, clerk ? "F4DFCC" : "35313B");
                SetPaint(Paint.Tie, clerk ? "CDA879" : "454359", .24f);
                SetPaint(Paint.Shoe, clerk ? "4B3940" : "524038", .23f, 4);
                SetPaint(Paint.Sole, "392F33");
                Apply(_core, clerk ? "Tang" : "Chen");
                return;
            }
            _top = Renderer(visual, "Shirt");
            _legs = Renderer(visual, "Trousers");
            Apply(_core, "Core");
        }

        internal void Dress(string cut, Color shirt, Color trousers)
        {
            bool shortSleeve = cut is "short" or "sport";
            _paint[(int)Paint.Top] = PlayerPaint(shirt, .14f);
            _paint[(int)Paint.Trim] = PlayerPaint(Color.Lerp(shirt, ArtDirection.Hex("5E352A"), .10f), .12f);
            _paint[(int)Paint.Pants] = PlayerPaint(Color.Lerp(trousers, ArtDirection.Hex("677186"), .18f), .10f);
            Apply(_top, shortSleeve ? "short" : cut is "winter" or "formal" ? cut : "long");
            Apply(_legs, shortSleeve ? "shorts" : "trousers");
        }

        internal void DressNpc(string cut, Color shirt, Color trousers, Color layer, Color accent)
        {
            bool uniform = cut is "uniform" or "apron" or "patrol";
            string bottoms = _prefersSkirt && !uniform && cut != "coat" ? "skirt"
                : cut == "short" ? "shorts" : "pants";
            _paint[(int)Paint.Top] = PlayerPaint(shirt, .14f);
            Color trim = cut == "patrol" ? accent : cut == "apron" ? layer : Color.Lerp(shirt, layer, .32f);
            _paint[(int)Paint.Trim] = PlayerPaint(trim, .14f);
            _paint[(int)Paint.Pants] = PlayerPaint(trousers, .10f);
            _paint[(int)Paint.Cream] = PlayerPaint(layer, .12f);
            _paint[(int)Paint.Tie] = PlayerPaint(accent, .22f);
            Apply(_top, "NpcTop-" + cut);
            Apply(_legs, "NpcBottom-" + bottoms);
        }

        private void SetPaint(Paint part, string hex, float smoothness = .12f, int surface = 0) =>
            _paint[(int)part] = PlayerPaint(ArtDirection.Hex(hex), smoothness, surface);

        private static readonly Dictionary<(Color, float, int), Material> PlayerMaterials = new Dictionary<(Color, float, int), Material>();
        private static Material PlayerPaint(Color color, float smoothness, int surface = 0)
        {
            if (PlayerMaterials.TryGetValue((color, smoothness, surface), out var material) && material != null) return material;
            var shader = Resources.Load<Shader>("Characters/PlayerSoftClay");
            material = shader != null ? new Material(shader) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "Player soft clay " + color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Grain", surface == 0 ? .009f : .002f);
            material.SetFloat("_SurfaceKind", surface);
            PlayerMaterials[(color, smoothness, surface)] = material;
            return material;
        }

        private SkinnedMeshRenderer Renderer(Transform parent, string name)
        {
            var renderer = Joint(parent, name, Vector3.zero).gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.rootBone = parent;
            renderer.localBounds = new Bounds(new Vector3(0, .78f, 0), new Vector3(1.45f, 2.02f, 1.45f));
            renderer.quality = SkinQuality.Bone4;
            renderer.forceMatrixRecalculationPerRender = true;
            return renderer;
        }

        private void Apply(SkinnedMeshRenderer renderer, string id)
        {
            var model = GetModel(id);
            if (renderer.sharedMesh != model.Mesh)
            {
                renderer.sharedMesh = model.Mesh;
                renderer.bones = _bones;
            }
            var materials = new Material[model.Paints.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = _paint[(int)model.Paints[i]];
            renderer.sharedMaterials = materials;
        }

        // The asset stores vertex data in rest-pose world units. This keeps the sculpt out of the frame loop.
        private ModelMesh GetModel(string id)
        {
            if (Meshes.TryGetValue(id, out var model) && model.Mesh != null) return model;
            var asset = Resources.Load<TextAsset>("Characters/PlayerB-" + id);
            if (asset == null) throw new InvalidOperationException("Missing player character mesh " + id);

            using var bytes = new MemoryStream(asset.bytes, false);
            using var reader = new BinaryReader(bytes);
            if (reader.ReadUInt32() != 0x314D4250) throw new InvalidDataException("Unsupported player mesh " + id);
            int count = reader.ReadInt32();
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var triangles = new Dictionary<Paint, List<int>>();
            var paints = new List<Paint>();
            for (int section = 0; section < count; section++)
            {
                var paint = (Paint)reader.ReadByte();
                int vertexCount = reader.ReadInt32();
                int indexCount = reader.ReadInt32();
                int offset = positions.Count;
                for (int vertex = 0; vertex < vertexCount; vertex++)
                {
                    positions.Add(ReadVector(reader));
                    normals.Add(ReadVector(reader));
                    weights.Add(new BoneWeight
                    {
                        boneIndex0 = reader.ReadByte(), boneIndex1 = reader.ReadByte(),
                        boneIndex2 = reader.ReadByte(), boneIndex3 = reader.ReadByte(),
                        weight0 = reader.ReadSingle(), weight1 = reader.ReadSingle(),
                        weight2 = reader.ReadSingle(), weight3 = reader.ReadSingle()
                    });
                }
                if (!triangles.TryGetValue(paint, out var indices))
                {
                    paints.Add(paint);
                    triangles.Add(paint, indices = new List<int>(indexCount));
                }
                for (int index = 0; index < indexCount; index++) indices.Add(offset + reader.ReadUInt16());
            }
            var mesh = new Mesh { name = "Player B · " + id, indexFormat = positions.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = _bindPoses;
            mesh.subMeshCount = paints.Count;
            for (int i = 0; i < paints.Count; i++) mesh.SetTriangles(triangles[paints[i]], i, false);
            mesh.RecalculateBounds();
            model = new ModelMesh { Mesh = mesh, Paints = paints.ToArray() };
            Meshes[id] = model;
            return model;
        }

        private static Vector3 ReadVector(BinaryReader reader) =>
            new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        private static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            return joint;
        }
    }
}
