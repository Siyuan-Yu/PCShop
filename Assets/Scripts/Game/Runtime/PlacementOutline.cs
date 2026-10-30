using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    /// <summary>Only the preview silhouette changes colour; furniture keeps its shared materials.</summary>
    [ExecuteAlways]
    public sealed class PlacementOutline : MonoBehaviour
    {
        private static readonly int ColourId = Shader.PropertyToID("_OutlineColor");
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Material> _edges = new List<Material>();
        private Mesh _mesh;
        private bool _hasValidity;
        public bool IsValid { get; private set; }

        public static PlacementOutline Attach(GameObject furniture)
        {
            var outline = furniture.AddComponent<PlacementOutline>();
            outline.Build();
            outline.SetValid(true);
            return outline;
        }

        private void Build()
        {
            // A dedicated copy is needed for smooth extrusion at split normals. Never modify
            // the cached bevel/primitive meshes, which are shared by the whole scene.
            _mesh = BuildSilhouette();
            Shader shader = Resources.Load<Shader>("Shaders/PlacementOutline");
            if (shader == null)
            {
                Debug.LogError("Missing placement outline shader.");
                return;
            }

            // Stencil excludes the entire furniture silhouette, including neighbouring pieces.
            // This prevents wide glow shells from painting over the countertop or shelf boards.
            // Run after opaque furniture and before normal transparent/UI drawing; clear our bit.
            AddLayer(shader, "Mask", 2990, 0, 0, true);
            AddLayer(shader, "OuterGlow", 2991, 7f, .08f, false);
            AddLayer(shader, "InnerGlow", 2992, 4.2f, .18f, false);
            AddLayer(shader, "Edge", 2993, 1.8f, .95f, false);
            AddLayer(shader, "ClearMask", 2994, 0, 0, true, true);
        }

        private void AddLayer(Shader shader, string label, int queue, float width, float alpha, bool mask, bool clear = false)
        {
            var mat = new Material(shader) { name = "PlacementOutline " + label, hideFlags = HideFlags.DontSave, renderQueue = queue };
            mat.SetFloat("_OutlineWidth", width);
            mat.SetColor(ColourId, new Color(1, 1, 1, alpha));
            mat.SetInt("_Cull", (int)(mask ? CullMode.Back : CullMode.Front));
            mat.SetInt("_ZTest", (int)(mask ? CompareFunction.Always : CompareFunction.LessEqual));
            mat.SetInt("_ColorMask", mask ? 0 : 15);
            mat.SetInt("_StencilRef", clear ? 0 : 1);
            mat.SetInt("_StencilComp", (int)(mask ? CompareFunction.Always : CompareFunction.NotEqual));
            mat.SetInt("_StencilOp", (int)(mask ? StencilOp.Replace : StencilOp.Keep));
            _materials.Add(mat);
            if (!mask) _edges.Add(mat);
            var go = new GameObject("PlacementOutline " + label) { layer = gameObject.layer, hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        public void SetValid(bool valid)
        {
            if (_hasValidity && IsValid == valid) return;
            _hasValidity = true;
            IsValid = valid;
            // HDR edge + layered falloff keeps the glow readable even in bright edit lighting.
            Color colour = valid ? new Color(.18f, 1.65f, .42f) : new Color(1.8f, .16f, .10f);
            foreach (Material mat in _edges)
            {
                colour.a = mat.GetColor(ColourId).a;
                mat.SetColor(ColourId, colour);
            }
        }

        private Mesh BuildSilhouette()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                Mesh source = filter.sharedMesh;
                if (source == null || renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy) continue;
                Vector3[] positions = source.vertices, faceNormals = source.normals;
                Matrix4x4 toRoot = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Matrix4x4 normalToRoot = toRoot.inverse.transpose;
                var smooth = new Dictionary<Vector3, Vector3>();
                for (int i = 0; i < positions.Length; i++)
                {
                    smooth.TryGetValue(positions[i], out Vector3 sum);
                    smooth[positions[i]] = sum + normalToRoot.MultiplyVector(faceNormals[i]).normalized;
                }
                int start = vertices.Count;
                for (int i = 0; i < positions.Length; i++)
                {
                    vertices.Add(toRoot.MultiplyPoint3x4(positions[i]));
                    normals.Add(smooth[positions[i]].normalized);
                }
                foreach (int index in source.triangles) triangles.Add(start + index);
            }
            var mesh = new Mesh { name = "PlacementOutline silhouette", hideFlags = HideFlags.DontSave };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            // Keep small off-screen silhouettes alive while their expanded edge is still visible.
            Bounds bounds = mesh.bounds;
            bounds.Expand(.3f);
            mesh.bounds = bounds;
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (Material mat in _materials) Kill.It(mat);
            _materials.Clear();
            _edges.Clear();
            Kill.It(_mesh);
        }
    }
}
