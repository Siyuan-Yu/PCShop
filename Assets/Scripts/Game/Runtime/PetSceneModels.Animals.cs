using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // 动物只用基础形体、纯色和现有 Lit；不接入人物的雕刻贴图或描边着色器。
    internal static partial class PetSceneModels
    {
        private static readonly Dictionary<string, Mesh> AnimalMeshes = new Dictionary<string, Mesh>();
        private static Mesh _ear, _straightLeg;

        internal static GameObject BuildAnimal(Transform parent, bool dog, int coat,
            out Transform head, out Transform tail, out Transform[] legs) =>
            BuildAnimal(parent, dog, ArtDirection.Hex(coat == 0 ? "D68F4D" : coat == 1 ? "C4A178" : coat == 2 ? "8C8F8C" : "E8DBC4"),
                !dog && coat < 2, out head, out tail, out legs);

        internal static GameObject BuildAnimal(Transform parent, bool dog, Color fur, bool tabby,
            out Transform head, out Transform tail, out Transform[] legs)
        {
            var figure = new GameObject(dog ? "BoardingDog" : "BoardingCat");
            var root = figure.transform;
            root.SetParent(parent, false);
            Color cream = Color.Lerp(fur, Color.white, .45f), dark = Color.Lerp(fur, Ink, .35f);
            Color pink = new Color(.77f, .46f, .43f);
            Material coatMat = Mats.Lit(fur, 0), lightMat = Mats.Lit(cream, 0);
            Material darkMat = Mats.Lit(dark, 0), inkMat = Mats.Lit(Ink, 0), pinkMat = Mats.Lit(pink, 0);
            float bodyY = dog ? .303f : .252f, bodyLength = dog ? .51f : .44f;
            Vector3 bodySize = dog ? new Vector3(.292f, .31f, bodyLength) : new Vector3(.244f, .228f, bodyLength);
            Ball(root, "Body", new Vector3(0, bodyY, -.025f), bodySize, coatMat);
            Ball(root, "Chest", new Vector3(0, bodyY + .012f, dog ? .159f : .145f),
                dog ? new Vector3(.218f, .278f, .17f) : new Vector3(.174f, .197f, .125f), lightMat);
            if (dog)
                Saddle(root, bodyY, bodySize, darkMat);
            else if (tabby)
                for (int i = 0; i < 3; i++)
                {
                    float z = -.145f + i * .101f;
                    BackStripe(root, i, z, bodyY, bodySize, darkMat);
                }

            head = Joint(root, "PetHead", new Vector3(0, dog ? .438f : .351f, dog ? .231f : .205f));
            var skull = Ball(head, "Head", Vector3.zero,
                dog ? new Vector3(.288f, .295f, .278f) : new Vector3(.286f, .246f, .237f), coatMat);
            if (dog)
            {
                // 额头白线是头部同一曲面上的色块，不额外叠一张会浮起的贴片。
                var blaze = PaintBlaze();
                skull.GetComponent<MeshFilter>().sharedMesh = blaze;
                skull.GetComponent<MeshRenderer>().sharedMaterials = new[] { coatMat, lightMat };
            }
            Ball(head, "Muzzle", new Vector3(0, dog ? -.068f : -.054f, dog ? .133f : .099f),
                dog ? new Vector3(.177f, .128f, .173f) : new Vector3(.147f, .079f, .063f), lightMat);
            Ball(head, "Nose", new Vector3(0, dog ? -.05f : -.045f, dog ? .218f : .132f),
                dog ? new Vector3(.046f, .034f, .029f) : new Vector3(.026f, .021f, .013f), dog ? inkMat : pinkMat);
            for (int side = -1; side <= 1; side += 2)
            {
                Ball(head, "Eye", new Vector3(side * (dog ? .073f : .075f), dog ? .008f : -.003f, dog ? .122f : .103f),
                    new Vector3(dog ? .030f : .036f, dog ? .041f : .044f, .022f), inkMat);
                if (dog)
                {
                    var ear = Ball(head, "FloppyEar", new Vector3(side * .135f, .010f, -.023f), new Vector3(.104f, .182f, .145f), darkMat);
                    ear.transform.localRotation = Quaternion.Euler(-8, 0, side * 12);
                }
                else
                {
                    var ear = Joint(head, "Ear", new Vector3(side * .088f, .079f, -.014f));
                    ear.localRotation = Quaternion.Euler(-6, 0, -side * 15);
                    ear.localScale = new Vector3(.107f, .135f, .078f);
                    ear.gameObject.AddComponent<MeshFilter>().sharedMesh = EarMesh();
                    ear.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { coatMat, pinkMat };
                    for (int j = 0; j < 2; j++)
                    {
                        var whisker = Prims.Shape(PrimitiveType.Cube, head, "Whisker", new Vector3(side * .093f, -.051f + j * .018f, .108f),
                            new Vector3(.072f, .0035f, .0035f), darkMat);
                        whisker.transform.localRotation = Quaternion.Euler(0, side * 8, side * (j == 0 ? -9 : 10));
                    }
                }
            }
            legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float hipY = dog ? .278f : .229f;
                var hip = Joint(root, "PetHip", new Vector3((i % 2 == 0 ? -1 : 1) * (dog ? .096f : .078f), hipY,
                    (i < 2 ? -.170f : .155f)));
                legs[i] = hip;
                var leg = Joint(hip, "StraightLegAndPaw", Vector3.zero);
                leg.localScale = new Vector3(dog ? .095f : .073f, hipY, dog ? .103f : .081f);
                leg.gameObject.AddComponent<MeshFilter>().sharedMesh = StraightLeg();
                leg.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { coatMat, lightMat };
            }
            tail = Joint(root, "TailPivot", new Vector3(0, dog ? .336f : .29f, dog ? -.26f : -.234f));
            if (dog)
                Tube(tail, "ShortTail", Vector3.zero, new Vector3(0, .055f, -.095f), .060f, darkMat);
            else
            {
                var elbow = new Vector3(0, .103f, -.085f);
                var tip = new Vector3(0, .331f, -.065f);
                Tube(tail, "TailBase", Vector3.zero, elbow + elbow.normalized * .015f, .045f, darkMat);
                Tube(tail, "TailTip", elbow - (tip - elbow).normalized * .015f, tip, .043f, darkMat);
                Ball(tail, "TailEnd", tip, Vector3.one * .043f, darkMat);
                if (tabby)
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 point = Vector3.Lerp(elbow, tip, .34f + i * .37f);
                        var band = Ball(tail, "TailStripe", point, new Vector3(.044f, .022f, .044f), inkMat);
                        band.transform.localRotation = Quaternion.FromToRotation(Vector3.up, tip - elbow);
                    }
            }
            // 全身只保留一只蒙皮 Renderer。同色部件跨关节共用一个子网格，
            // 四种毛色只替换材质；头、四个髋、尾仍由原动作独立驱动。
            Skin(root, dog ? "Dog" : tabby ? "Tabby" : "Cat", head, tail, legs);
            return figure;
        }

        private static GameObject Ball(Transform parent, string name, Vector3 at, Vector3 size, Material material) =>
            Prims.Shape(PrimitiveType.Sphere, parent, name, at, size, material);

        private static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            return joint;
        }

        private static void Tube(Transform parent, string name, Vector3 from, Vector3 to, float width, Material material)
        {
            var part = Prims.Shape(PrimitiveType.Cylinder, parent, name, (from + to) * .5f,
                new Vector3(width, Vector3.Distance(from, to) * .5f, width), material);
            part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        }

        private static Mesh PaintBlaze()
        {
            const string key = "Dog.Blaze";
            if (AnimalMeshes.TryGetValue(key, out var cached)) return cached;
            // 用确定的经线分区。按原球体三角形中心染色会把白线切成闪电形和孤立白斑。
            var mesh = new Mesh {name=key};
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var angles = new List<float>();
            for (int i = 0; i <= 32; i++) angles.Add(-Mathf.PI + i * Mathf.PI / 16);
            angles.Add(-.16f); angles.Add(.16f); angles.Sort();
            const int rings = 24;
            int columns = angles.Count;
            var coat = new List<int>(); var cream = new List<int>();
            for (int ring = 0; ring <= rings; ring++)
                foreach (float angle in angles)
                {
                    float theta = ring * Mathf.PI / rings;
                    var normal = new Vector3(Mathf.Sin(theta) * Mathf.Sin(angle), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Cos(angle));
                    vertices.Add(normal * .5f); normals.Add(normal);
                }
            for (int ring = 0; ring < rings; ring++)
                for (int col = 0; col < columns - 1; col++)
                {
                    float angle = (angles[col] + angles[col+1]) * .5f;
                    var target = Mathf.Abs(angle) < .16f && ring < 18 ? cream : coat;
                    int top = ring * columns + col, bottom = top + columns;
                    target.Add(top); target.Add(bottom); target.Add(bottom+1);
                    target.Add(top); target.Add(bottom+1); target.Add(top+1);
                }
            mesh.SetVertices(vertices); mesh.SetNormals(normals);
            mesh.subMeshCount = 2; mesh.SetTriangles(coat, 0); mesh.SetTriangles(cream, 1); mesh.RecalculateBounds();
            return AnimalMeshes[key] = mesh;
        }

        private static void Saddle(Transform root, float bodyY, Vector3 size, Material material)
        {
            const string key = "DogFlushSaddle";
            if (!AnimalMeshes.TryGetValue(key, out var mesh))
            {
                var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
                const int rows = 24, columns = 24;
                for (int row = 0; row <= rows; row++)
                    for (int col = 0; col <= columns; col++)
                    {
                        float u = row * Mathf.PI / rows, z = -.08f + Mathf.Cos(u) * .137f;
                        float cut = (z + .025f) / (size.z * .5f), radius = Mathf.Sqrt(1 - cut * cut);
                        float a = Mathf.PI * .5f + (col / (float)columns * 2 - 1) * Mathf.Sin(u) * 1.04f;
                        var normal = new Vector3(Mathf.Cos(a) * radius / size.x, Mathf.Sin(a) * radius / size.y, cut / size.z).normalized;
                        vertices.Add(new Vector3(size.x * .5f * radius * Mathf.Cos(a), bodyY + size.y * .5f * radius * Mathf.Sin(a), z) + normal * .0015f);
                        normals.Add(normal);
                        if (row == 0 || col == 0) continue;
                        int k = row * (columns+1) + col, prior = k - columns - 1;
                        triangles.Add(prior-1); triangles.Add(k); triangles.Add(prior);
                        triangles.Add(prior-1); triangles.Add(k-1); triangles.Add(k);
                    }
                mesh = new Mesh {name=key}; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); AnimalMeshes.Add(key,mesh);
            }
            var part = Joint(root, "Saddle", Vector3.zero); part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void BackStripe(Transform root, int index, float z, float bodyY, Vector3 size, Material material)
        {
            string key = "CatBackStripe" + index;
            if (!AnimalMeshes.TryGetValue(key, out var mesh))
            {
                var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
                // 扁片严格贴合椭球上半面，不把条纹做成凸在背上的三根圆棍。
                const int segments = 24;
                for (int j = 0; j <= segments; j++)
                    for (int edge = 0; edge < 2; edge++)
                    {
                        float zz = z + (edge == 0 ? -.014f : .014f);
                        float cut = (zz + .025f) / (size.z * .5f), radius = Mathf.Sqrt(1 - cut * cut);
                        float a = Mathf.Lerp(16, 164, j / (float)segments) * Mathf.Deg2Rad;
                        Vector3 normal = new Vector3(Mathf.Cos(a) * radius / size.x, Mathf.Sin(a) * radius / size.y, cut / size.z).normalized;
                        vertices.Add(new Vector3(size.x * .5f * radius * Mathf.Cos(a), bodyY + size.y * .5f * radius * Mathf.Sin(a), zz) + normal * .0012f);
                        normals.Add(normal);
                        if (j == 0 || edge != 0) continue;
                        int k = j * 2;
                        triangles.Add(k - 2); triangles.Add(k + 1); triangles.Add(k - 1);
                        triangles.Add(k - 2); triangles.Add(k); triangles.Add(k + 1);
                    }
                mesh = new Mesh {name=key}; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); AnimalMeshes.Add(key,mesh);
            }
            var part = Joint(root, "BackStripe", Vector3.zero);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh EarMesh()
        {
            if (_ear != null) return _ear;
            // 粉色内耳嵌在同一三角锥正面；避免另一只小锥体的双层边和穿插。
            _ear = new Mesh { name = "PetTriangleEar" };
            _ear.vertices = new[] {
                new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(0,1,0),
                new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,1,-.25f),
                new Vector3(-.31f,.12f,.44f),new Vector3(.31f,.12f,.44f),new Vector3(0,.78f,.11f) };
            _ear.subMeshCount = 2;
            _ear.SetTriangles(new[] {0,1,7,0,7,6,1,2,8,1,8,7,2,0,6,2,6,8,4,3,5,3,4,1,3,1,0,1,4,5,1,5,2,3,0,2,3,2,5},0);
            _ear.SetTriangles(new[] {6,7,8},1);
            _ear.RecalculateNormals(); _ear.RecalculateBounds(); return _ear;
        }

        private static Mesh StraightLeg()
        {
            if (_straightLeg != null) return _straightLeg;
            const int sides = 12;
            var points = new List<Vector3>(); var normals = new List<Vector3>();
            var coat = new List<int>(); var paws = new List<int>();
            float[] ys = {0,-.08f,-.78f,-.84f,-.96f,-1f};
            float[] radius = {.40f,.50f,.39f,.46f,.44f,.30f};
            for (int ring = 0; ring < ys.Length; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    points.Add(new Vector3(Mathf.Cos(a) * radius[ring], ys[ring], Mathf.Sin(a) * radius[ring] + (ring >= 3 ? .07f : 0)));
                    normals.Add(new Vector3(Mathf.Cos(a), ring == ys.Length - 1 ? -.5f : 0, Mathf.Sin(a)).normalized);
                    if (ring == 0) continue;
                    int b = ring * sides + i, c = ring * sides + (i + 1) % sides;
                    var tris = ring >= 3 ? paws : coat;
                    tris.Add(b - sides); tris.Add(c - sides); tris.Add(c); tris.Add(b - sides); tris.Add(c); tris.Add(b);
                }
            // 上下封口，底部处于 y=-1，模型原点即脚底。
            for (int i = 1; i < sides - 1; i++)
            { coat.Add(0); coat.Add(i + 1); coat.Add(i); int last = (ys.Length - 1) * sides; paws.Add(last); paws.Add(last + i); paws.Add(last + i + 1); }
            _straightLeg = new Mesh {name="PetStraightLegWithCreamPaw"};
            _straightLeg.SetVertices(points); _straightLeg.SetNormals(normals); _straightLeg.subMeshCount = 2;
            _straightLeg.SetTriangles(coat,0); _straightLeg.SetTriangles(paws,1); _straightLeg.RecalculateBounds(); return _straightLeg;
        }

        private static void Skin(Transform root, string variant, Transform head, Transform tail, Transform[] legs)
        {
            var bones = new[] {root, head, tail, legs[0], legs[1], legs[2], legs[3]};
            var filters = root.GetComponentsInChildren<MeshFilter>();
            var materials = new List<Material>();
            foreach (var filter in filters)
                foreach (var material in filter.GetComponent<MeshRenderer>().sharedMaterials)
                    if (!materials.Contains(material)) materials.Add(material);
            string key = "Rig." + variant;
            if (!AnimalMeshes.TryGetValue(key, out var mesh))
            {
                var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var weights = new List<BoneWeight>();
                var groups = new List<int>[materials.Count]; for (int i = 0; i < groups.Length; i++) groups[i] = new List<int>();
                foreach (var filter in filters)
                {
                    int bone = 0;
                    for (Transform owner = filter.transform; owner != null; owner = owner.parent)
                    { int index = System.Array.IndexOf(bones, owner); if (index >= 0) { bone = index; break; } }
                    Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Matrix4x4 normalMatrix = matrix.inverse.transpose;
                    var source = filter.sharedMesh; int offset = vertices.Count;
                    foreach (var position in source.vertices) vertices.Add(matrix.MultiplyPoint3x4(position));
                    foreach (var normal in source.normals) normals.Add(normalMatrix.MultiplyVector(normal).normalized);
                    for (int i = 0; i < source.vertexCount; i++) weights.Add(new BoneWeight {boneIndex0=bone, weight0=1});
                    var sourceMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                    for (int sub = 0; sub < source.subMeshCount; sub++)
                    {
                        var triangles = groups[materials.IndexOf(sourceMaterials[sub])];
                        foreach (int vertex in source.GetTriangles(sub)) triangles.Add(offset + vertex);
                    }
                }
                var bindposes = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
                mesh = new Mesh {name=key+" ("+filters.Length+" shapes)"}; mesh.SetVertices(vertices); mesh.SetNormals(normals);
                mesh.boneWeights = weights.ToArray(); mesh.bindposes = bindposes; mesh.subMeshCount = groups.Length;
                for (int i = 0; i < groups.Length; i++) mesh.SetTriangles(groups[i],i);
                mesh.RecalculateBounds(); AnimalMeshes.Add(key,mesh);
            }
            var renderer = root.gameObject.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials.ToArray(); renderer.bones = bones; renderer.rootBone = root; renderer.quality = SkinQuality.Bone1;
            renderer.localBounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * .4f);
            foreach (var filter in filters)
            { Object.DestroyImmediate(filter.GetComponent<MeshRenderer>()); Object.DestroyImmediate(filter); }
        }
    }
}
