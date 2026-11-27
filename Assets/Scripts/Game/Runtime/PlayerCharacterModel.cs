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

        private enum Paint : byte { Skin, Hair, Brow, Eye, Lip, Top, Trim, Pants, Sole, Shoe, Cream, Tie, Ink, HairLight, Bow }

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
        internal bool PaintedSample { get; private set; }

        internal void Build(Transform visual, Transform torso, FigureAppearance appearance, NpcLook npc)
        {
            PaintedSample = appearance == FigureAppearance.Player || npc.Head is "baker" or "twintail" || npc.Head?.StartsWith("painted_",StringComparison.Ordinal)==true;
            _bones = new Transform[18];
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
            _bones[15] = Joint(_bones[2], "HairTailL", new Vector3(-.151f, .10f, -.08f));
            _bones[16] = Joint(_bones[2], "HairTailR", new Vector3(.151f, .10f, -.08f));
            _bones[17] = Joint(visual, "GarmentHem", new Vector3(0, HipHeight, 0));
            _bindPoses = new Matrix4x4[_bones.Length];
            Matrix4x4 meshToWorld = visual.localToWorldMatrix;
            for (int i = 0; i < _bones.Length; i++)
                _bindPoses[i] = _bones[i].worldToLocalMatrix * meshToWorld;
            if (PaintedSample)
            {
                // 用户指定真实样板下排的比例；不能把 18% 放大误读成 3.5 头身。
                _bones[2].localScale = Vector3.one * 1.18f;
                if (npc.Head == "twintail") _bones[2].localScale = new Vector3(1.18f * .91f / .97f, 1.18f, 1.18f);
                _bones[2].localPosition += Vector3.down * .010f;
            }

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
            if (PaintedSample)
            {
                SetPaint(Paint.Skin, "F4B080", .12f, 1);
                SetPaint(Paint.Hair, "39302E", .12f, 2);
                SetPaint(Paint.Brow, "79543F", .12f, 11);
                SetPaint(Paint.Eye, "50362A", .12f, 3);
                SetPaint(Paint.Lip, "B47554");
                SetPaint(Paint.Ink, "39302A");
                SetPaint(Paint.HairLight, "493B38", .12f, 2);
                SetPaint(Paint.Cream, "EADAC5");
                SetPaint(Paint.Shoe, "89613E", .12f, 4);
            }
            if (appearance == FigureAppearance.Npc)
            {
                _prefersSkirt = npc.PrefersSkirt;
                _paint[(int)Paint.Skin] = PlayerPaint(npc.Skin, .25f, PaintedSample && npc.Head == "twintail" ? 12 : 1);
                _paint[(int)Paint.Hair] = PlayerPaint(npc.Hair, .31f, 2);
                _paint[(int)Paint.Brow] = PaintedSample ? PlayerPaint(ArtDirection.Hex("79543F"), .12f, 11)
                    : PlayerPaint(Color.Lerp(npc.Hair, ArtDirection.Hex("382729"), .68f), .12f);
                if (PaintedSample) _paint[(int)Paint.HairLight] = PlayerPaint(Color.Lerp(npc.Hair, ArtDirection.Hex("B57A58"), .19f), .12f, 2);
                if (PaintedSample) SetPaint(Paint.Shoe, npc.Head == "twintail" ? "805935" : "89613E", .12f, 4);
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
            Apply(_core, "PaintedCore");
        }

        internal void Dress(string cut, Color shirt, Color trousers)
        {
            bool shortSleeve = cut is "short" or "sport";
            Apply(_core, shortSleeve ? "PaintedCoreShort" : "PaintedCore");
            _paint[(int)Paint.Top] = PlayerPaint(shirt, .14f, cut == "paintedJacket" ? 5 : 0);
            _paint[(int)Paint.Trim] = PlayerPaint(Color.Lerp(shirt, ArtDirection.Hex("5E352A"), .23f), .12f, cut == "paintedJacket" ? 5 : 0);
            _paint[(int)Paint.Pants] = PlayerPaint(Color.Lerp(trousers, ArtDirection.Hex("677186"), .18f), .10f, 10);
            Apply(_top, cut == "paintedJacket" ? "PaintedJacket" : shortSleeve ? "short" : cut is "winter" or "formal" ? cut : "long");
            Apply(_legs, shortSleeve ? "shorts" : cut == "paintedJacket" ? "PaintedTrousers" : "trousers");
        }

        internal void DressNpc(string cut, Color shirt, Color trousers, Color layer, Color accent, string bottom=null)
        {
            bool population=cut.StartsWith("painted_",StringComparison.Ordinal);
            string style=population?cut.Substring(8):cut;
            bool uniform = cut is "uniform" or "apron" or "patrol";
            string bottoms = cut is "singer" or "chef" ? cut : _prefersSkirt && !uniform && cut != "coat" ? "skirt"
                : cut == "short" ? "shorts" : "pants";
            if(bottom!=null)bottoms=bottom;
            else if(population)bottoms="painted_"+(_prefersSkirt?"skirt":"pants");
            _paint[(int)Paint.Top] = PlayerPaint(shirt, .14f, !PaintedSample ? 0 : cut == "chef" ? 6 : cut == "singer" ? 8 : 0);
            Color trim = style == "patrol" ? accent : style is "apron" or "apronbow" or "chef" or "singer" or "waistapron" or "hoodie" or "windbreaker" ? layer : Color.Lerp(shirt, layer, .32f);
            _paint[(int)Paint.Trim] = PlayerPaint(trim, .14f, PaintedSample && cut == "chef" ? 7 : 0);
            _paint[(int)Paint.Pants] = PlayerPaint(trousers, .10f, !PaintedSample ? 0 : cut == "singer" ? 9 : 10);
            _paint[(int)Paint.Cream] = PlayerPaint(cut == "singer" ? accent : cut == "chef" ? ArtDirection.Hex("DAB36A") : layer, .12f);
            _paint[(int)Paint.Tie] = PlayerPaint(accent, .22f);
            if(population)
            {
                int surface=style is "floral" or "floralblazer" or "flowerblouse" ? 13 : style=="knitvest"?14 : style is "jacket" or "workjacket" or "workbadge"?5:0;
                _paint[(int)Paint.Top]=PlayerPaint(shirt,.12f,surface);
                _paint[(int)Paint.Trim]=PlayerPaint(trim,.12f,style is "apron" or "apronbow" or "waistapron"?7:0);
                _paint[(int)Paint.Pants]=PlayerPaint(trousers,.10f,bottoms.Contains("skirt")?9:10);
            }
            Apply(_top, "NpcTop-" + cut);
            Apply(_legs, "NpcBottom-" + bottoms);
        }

        private void SetPaint(Paint part, string hex, float smoothness = .12f, int surface = 0) =>
            _paint[(int)part] = PlayerPaint(ArtDirection.Hex(hex), smoothness, surface);

        private static readonly Dictionary<(Color, float, int, bool), Material> PlayerMaterials = new Dictionary<(Color, float, int, bool), Material>();
        private Material PlayerPaint(Color color, float smoothness, int surface = 0)
        {
            if (PlayerMaterials.TryGetValue((color, smoothness, surface, PaintedSample), out var material) && material != null) return material;
            var shader = Resources.Load<Shader>(PaintedSample ? "Characters/PlayerPainted" : "Characters/PlayerSoftClay");
            material = shader != null ? new Material(shader) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "Player soft clay " + color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Grain", surface == 0 || PaintedSample && surface >= 5 && surface <= 10 ? .009f : .002f);
            material.SetFloat("_SurfaceKind", surface);
            PlayerMaterials[(color, smoothness, surface, PaintedSample)] = material;
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

        internal void SetGarmentSeat(float blend)
        {
            SetGarmentSeat(_top,blend);SetGarmentSeat(_legs,blend);
        }

        private static void SetGarmentSeat(SkinnedMeshRenderer renderer, float blend)
        {
            if (renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0)
                renderer.SetBlendShapeWeight(0, Mathf.Clamp01(blend) * 100f);
        }

        private static Vector3 SeatedClothPoint(Vector3 p, bool skirt)
        {
            if (p.y >= .81f) return p;
            float t = Mathf.Clamp01((.81f - p.y) / (skirt ? .378f : .334f));
            float along = Mathf.SmoothStep(0, 1, t);
            float front = skirt ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.07f, .10f, p.z)) : 1;
            float y = .81f + .04f * Mathf.Sin(Mathf.PI * t) - (skirt ? .035f : .010f) * t * t * t;
            return Vector3.Lerp(p, new Vector3(p.x, y, p.z + (skirt ? .25f : .275f) * along), front);
        }

        private static void AddSeatedCloth(Mesh mesh, List<Vector3> positions, List<Vector3> normals,
            List<BoneWeight> weights, List<bool> cloth, bool skirt)
        {
            if (!cloth.Contains(true)) return;
            var delta = new Vector3[positions.Count];var deltaNormal = new Vector3[positions.Count];
            var pivot = new Vector3(0, HipHeight, 0);
            var hem = Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(-78, 0, 0)) * Matrix4x4.Translate(-pivot);
            for (int i = 0; i < positions.Count; i++)
            {
                if (!cloth[i]) continue;
                var w = weights[i];
                float amount = (w.boneIndex0 == 17 ? w.weight0 : 0) + (w.boneIndex1 == 17 ? w.weight1 : 0)
                    + (w.boneIndex2 == 17 ? w.weight2 : 0) + (w.boneIndex3 == 17 ? w.weight3 : 0);
                var skin = Matrix4x4.identity;
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 4; col++)skin[row,col] = Mathf.Lerp(skin[row,col],hem[row,col],amount);
                var inverse = skin.inverse;var p = positions[i];
                delta[i] = inverse.MultiplyPoint3x4(SeatedClothPoint(p,skirt)) - p;
                const float step = .0001f;
                var jacobian = Matrix4x4.identity;
                for (int axis = 0; axis < 3; axis++)
                {
                    var h = Vector3.zero;h[axis] = step;
                    var derivative = (SeatedClothPoint(p+h,skirt)-SeatedClothPoint(p-h,skirt))/(2*step);
                    jacobian.SetColumn(axis,new Vector4(derivative.x,derivative.y,derivative.z,0));
                }
                // 曲面铺平时三维雅可比可奇异，直接变换两条切线，避免法线变零或翻面。
                var n = normals[i];
                var tangent = Vector3.Cross(n,Mathf.Abs(n.y) < .8f ? Vector3.up : Vector3.right).normalized;
                var bitangent = Vector3.Cross(n,tangent);
                var targetNormal = Vector3.Cross(jacobian.MultiplyVector(tangent),jacobian.MultiplyVector(bitangent)).normalized;
                if (targetNormal.sqrMagnitude < .1f) targetNormal = n;
                deltaNormal[i] = inverse.MultiplyVector(targetNormal).normalized - normals[i];
            }
            mesh.AddBlendShapeFrame("SeatedCloth",100,delta,deltaNormal,null);
        }

        // The asset stores vertex data in rest-pose world units. This keeps the sculpt out of the frame loop.
        private ModelMesh GetModel(string id)
        {
            string cacheKey = PaintedSample ? id + ":painted" : id;
            if (Meshes.TryGetValue(cacheKey, out var model) && model.Mesh != null) return model;
            var asset = Resources.Load<TextAsset>("Characters/PlayerB-" + id);
            if (asset == null) throw new InvalidOperationException("Missing player character mesh " + id);

            using var bytes = new MemoryStream(asset.bytes, false);
            using var reader = new BinaryReader(bytes);
            uint format = reader.ReadUInt32();
            bool authoredSurface = format == 0x324D4250;
            if (format != 0x314D4250 && !authoredSurface) throw new InvalidDataException("Unsupported player mesh " + id);
            int count = reader.ReadInt32();
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var layers = new List<Vector2>();
            var surfacePositions = new List<Vector3>();
            var surfaceNormals = new List<Vector3>();
            var cloth = new List<bool>();
            var triangles = new Dictionary<Paint, List<int>>();
            var paints = new List<Paint>();
            for (int section = 0; section < count; section++)
            {
                byte paintCode = reader.ReadByte();
                bool authoredEdge = (paintCode & 128) != 0;
                bool clothSection = authoredSurface && (paintCode & 64) != 0;
                var paint = (Paint)(paintCode & 63);
                bool outlinedBow = paint == Paint.Bow;
                bool authoredInk = paint == Paint.Ink;
                if (outlinedBow) paint = Paint.Brow; // 发结沿用眉色子网格，不新增材质批次。
                if (authoredInk) paint = id.StartsWith("NpcBottom", StringComparison.Ordinal) ? Paint.Pants : Paint.Top;
                float surfaceMark = authoredInk ? 2 : outlinedBow ? 1 : 0;
                int vertexCount = reader.ReadInt32();
                int indexCount = reader.ReadInt32();
                int offset = positions.Count;
                for (int vertex = 0; vertex < vertexCount; vertex++)
                {
                    positions.Add(ReadVector(reader));
                    normals.Add(ReadVector(reader));
                    layers.Add(new Vector2(0, surfaceMark));
                    cloth.Add(clothSection);
                    weights.Add(new BoneWeight
                    {
                        boneIndex0 = reader.ReadByte(), boneIndex1 = reader.ReadByte(),
                        boneIndex2 = reader.ReadByte(), boneIndex3 = reader.ReadByte(),
                        weight0 = reader.ReadSingle(), weight1 = reader.ReadSingle(),
                        weight2 = reader.ReadSingle(), weight3 = reader.ReadSingle()
                    });
                    surfacePositions.Add(authoredSurface ? ReadVector(reader) : positions[positions.Count-1]);
                    surfaceNormals.Add(authoredSurface ? ReadVector(reader) : normals[normals.Count-1]);
                }
                if (!triangles.TryGetValue(paint, out var indices))
                {
                    paints.Add(paint);
                    triangles.Add(paint, indices = new List<int>(indexCount));
                }
                var sectionIndices = new int[indexCount];
                for (int index = 0; index < indexCount; index++)
                {
                    sectionIndices[index] = reader.ReadUInt16();
                    indices.Add(offset + sectionIndices[index]);
                }
                // 缓存的同一子网格内追加反面壳，与主体合在一次前向绘制。
                // 不创建额外 Renderer / Material / 每个人物独有的 Mesh。
                if (PaintedSample && !authoredEdge && !authoredInk && (outlinedBow || paint is not (Paint.Brow or Paint.Eye or Paint.Lip or Paint.Ink or Paint.HairLight)))
                {
                    // 衣片正面使用硬法线；轮廓壳在共位切边处平滑，避免薄衣片的描边断开。
                    var shellNormals = new Dictionary<Vector3, Vector3>();
                    for (int vertex = 0; vertex < vertexCount; vertex++)
                    {
                        var position=positions[offset+vertex];
                        shellNormals.TryGetValue(position,out var sum);
                        shellNormals[position]=sum+normals[offset+vertex];
                    }
                    int shellOffset = positions.Count;
                    for (int vertex = 0; vertex < vertexCount; vertex++)
                    {
                        positions.Add(positions[offset+vertex]);normals.Add(shellNormals[positions[offset+vertex]].normalized);
                        weights.Add(weights[offset+vertex]);layers.Add(new Vector2(1, surfaceMark));
                        surfacePositions.Add(surfacePositions[offset+vertex]);surfaceNormals.Add(surfaceNormals[offset+vertex]);
                        cloth.Add(clothSection);
                    }
                    for (int index = 0; index < indexCount; index += 3)
                    {
                        indices.Add(shellOffset+sectionIndices[index]);
                        indices.Add(shellOffset+sectionIndices[index+2]);
                        indices.Add(shellOffset+sectionIndices[index+1]);
                    }
                }
            }
            var mesh = new Mesh { name = "Player B · " + id, indexFormat = positions.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            if (PaintedSample)
            {
                mesh.SetUVs(1, layers);
                mesh.SetUVs(2, surfacePositions);
                mesh.SetUVs(3, surfaceNormals);
            }
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = _bindPoses;
            mesh.subMeshCount = paints.Count;
            for (int i = 0; i < paints.Count; i++) mesh.SetTriangles(triangles[paints[i]], i, false);
            mesh.RecalculateBounds();
            if (PaintedSample) AddSeatedCloth(mesh, positions, normals, weights, cloth, id == "NpcBottom-singer" || id.Contains("skirt") || id.EndsWith("labcoat",StringComparison.Ordinal));
            model = new ModelMesh { Mesh = mesh, Paints = paints.ToArray() };
            Meshes[cacheKey] = model;
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
