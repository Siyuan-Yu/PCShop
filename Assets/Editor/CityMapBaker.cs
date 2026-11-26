using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

// Runs before player builds, including the existing Windows/WebGL entry points.
public sealed class CityMapBaker : IPreprocessBuildWithReport
{
    private const string Output="Assets/Generated/CityBake";
    private const string PrefabPath=Output+"/Resources/BakedCity/Map.prefab";
    private static bool? _requestedTrial;
    public int callbackOrder=>-1000;
    public static void SelectEdition(bool trial)=>_requestedTrial=trial;
    public void OnPreprocessBuild(BuildReport report)
    {
        bool trial=_requestedTrial??EditorUserBuildSettings.activeScriptCompilationDefines.Contains("PCSHOP_FIRST_CHAPTER_TRIAL");
        try {EnsureBaked(trial);}finally {_requestedTrial=null;}
    }
    // Asset generation only. Does not create or launch a player and does not run tests.
    public static void RunBakeOnly()
    {
        try {EnsureBaked(false);EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void RunTrialBakeOnly()
    {
        try {EnsureBaked(true);EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static string SourceFingerprint(bool trial)
    {
        var files=Directory.GetFiles("Assets/Scripts","*",SearchOption.AllDirectories)
            .Concat(Directory.GetFiles("Assets/Editor","*.cs",SearchOption.AllDirectories))
            .Concat(Directory.GetFiles("Assets/Resources","*",SearchOption.AllDirectories))
            .Where(f=>!f.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)).OrderBy(f=>f,StringComparer.Ordinal);
        using(var sha=SHA256.Create())
        {
            void Feed(byte[] bytes)=>sha.TransformBlock(bytes,0,bytes.Length,null,0);
            Feed(Encoding.UTF8.GetBytes("CityBake/"+BakedCityMap.CurrentVersion+"/"+trial));
            foreach(var file in files){Feed(Encoding.UTF8.GetBytes(file));Feed(File.ReadAllBytes(file));}
            sha.TransformFinalBlock(Array.Empty<byte>(),0,0);
            return BitConverter.ToString(sha.Hash).Replace("-","");
        }
    }
    public static void EnsureBaked(bool trial)
    {
        if(EditorApplication.isPlaying)throw new BuildFailedException("请停止运行游戏后再预生成地图。");
        string fingerprint=SourceFingerprint(trial);
        var cached=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<BakedCityMap>();
        if(cached!=null&&cached.SourceHash==fingerprint&&cached.Trial==trial&&cached.Version==BakedCityMap.CurrentVersion)
        {Debug.Log("CITY_BAKE_RESULT 已复用最新预生成地图");return;}
        var clock=System.Diagnostics.Stopwatch.StartNew();
        // Delete only this tool's fixed generated asset directory, through AssetDatabase.
        if(AssetDatabase.IsValidFolder(Output))AssetDatabase.DeleteAsset(Output);
        Directory.CreateDirectory(Output+"/Resources/BakedCity");AssetDatabase.Refresh();
        var json=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Data/city_layout.json").text;
        var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(json));
        var world=new StreetWorldBuilder(city,7,trial);
        world.PrepareBakeCapture();
        BakedCityMap data=null;
        var library=ScriptableObject.CreateInstance<BakedCityGeometry>();
        string libraryPath=Output+"/Geometry.asset";
        AssetDatabase.CreateAsset(library,libraryPath);
        try
        {
            world.Build();
            data=world.CaptureBakedMap(json);data.SourceHash=fingerprint;
            foreach(var expo in data.Expos)
            {
                // These halls depend on the current event/session and are restored at runtime.
                foreach(string name in new[]{"ExpoClosedHall","QuarterlyExpoHall","MonthlyInvitationExhibits"})
                {var child=expo.Root.transform.Find(name);if(child!=null)Object.DestroyImmediate(child.gameObject);}
            }
            foreach(var owner in world.Root.GetComponentsInChildren<CityLandscapeResources>(true))owner.Owned.Clear();
            foreach(var behavior in world.Root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(behavior is PlaceMarker||behavior is GroundMarker||behavior is CityLandscapeResources
                    ||behavior is RaceTrackView||behavior is QuarterlyExpoView)Object.DestroyImmediate(behavior);
                else if(!(behavior is BakedCityMap)&&!(behavior is InteriorWindowLight)&&!(behavior is UnityEngine.UI.Text))
                    throw new BuildFailedException("未声明的地图运行组件："+behavior.GetType().FullName);
            }
            // OS-created dynamic fonts cannot be embedded in a prefab. Runtime restores
            // the platform font, keeping the same localization/platform behavior as before.
            foreach(var label in world.Root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var meshes=new Dictionary<string,Mesh>();
            var savedMeshes=new Dictionary<Mesh,Mesh>();
            var materials=new HashSet<Material>();var textures=new HashSet<Texture>();
            Mesh SaveMesh(Mesh mesh)
            {
                if(mesh==null)return null;
                if(savedMeshes.TryGetValue(mesh,out var saved))return saved;
                if(EditorUtility.IsPersistent(mesh)){savedMeshes.Add(mesh,mesh);return mesh;}
                string key=MeshHash(mesh);
                if(!meshes.TryGetValue(key,out saved))
                {
                    mesh.name="共用模型_"+meshes.Count;
                    // Mild storage compression, without removing triangles. Keep large
                    // continuous road meshes exact to avoid quantization at road joins.
                    var size=mesh.bounds.size;
                    if(Mathf.Max(size.x,Mathf.Max(size.y,size.z))<64)
                        MeshUtility.SetMeshCompression(mesh,ModelImporterMeshCompression.Low);
                    AssetDatabase.AddObjectToAsset(mesh,library);meshes.Add(key,mesh);saved=mesh;
                    library.VertexCount+=mesh.vertexCount;
                }
                savedMeshes.Add(mesh,saved);return saved;
            }
            void SaveMaterial(Material material)
            {
                if(material==null||EditorUtility.IsPersistent(material)||!materials.Add(material))return;
                foreach(string property in material.GetTexturePropertyNames())
                {
                    var texture=material.GetTexture(property);
                    if(texture!=null&&!EditorUtility.IsPersistent(texture)&&textures.Add(texture))AssetDatabase.AddObjectToAsset(texture,library);
                }
                AssetDatabase.AddObjectToAsset(material,library);
            }
            foreach(var filter in world.Root.GetComponentsInChildren<MeshFilter>(true))filter.sharedMesh=SaveMesh(filter.sharedMesh);
            foreach(var collider in world.Root.GetComponentsInChildren<MeshCollider>(true))collider.sharedMesh=SaveMesh(collider.sharedMesh);
            foreach(var renderer in world.Root.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)SaveMaterial(material);
            foreach(var glow in data.Glows)SaveMaterial(glow.Material);
            foreach(var chunk in data.Forest)foreach(var part in chunk.Parts){part.Mesh=SaveMesh(part.Mesh);library.ForestPlacements++;}
            // Drop omitted dynamic-hall groups before saving scene-root references.
            var detailPairs=data.DetailRoots.Select((root,index)=>new {root,group=data.Details.Groups[index]})
                .Where(pair=>pair.root!=null).ToArray();
            data.DetailRoots=detailPairs.Select(pair=>pair.root).ToArray();
            data.Details.Groups=detailPairs.Select(pair=>pair.group).ToArray();
            foreach(var group in data.Details.Groups)
            {
                SaveMaterial(group.Material);
                foreach(var part in group.Parts)part.Mesh=SaveMesh(part.Mesh);
            }
            data.Details.name="共用建筑细节摆放记录";AssetDatabase.AddObjectToAsset(data.Details,library);
            var forestData=ScriptableObject.CreateInstance<BakedForestLayout>();
            forestData.name="共用树林摆放记录";forestData.Chunks=data.Forest;
            AssetDatabase.AddObjectToAsset(forestData,library);data.ForestData=forestData;
            // Remove any deleted dynamic-hall targets from records before serialization.
            data.Markers=data.Markers.Where(m=>m.Target!=null).ToArray();
            data.GroundMarkers=data.GroundMarkers.Where(g=>g!=null).ToArray();
            library.MeshCount=meshes.Count;library.MaterialCount=materials.Count;library.TextureCount=textures.Count;
            EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            var prefab=PrefabUtility.SaveAsPrefabAsset(world.Root,PrefabPath,out bool success);
            if(!success||prefab==null)throw new BuildFailedException("预生成地图保存失败。");
            AssetDatabase.SaveAssets();
            var stored=prefab.GetComponent<BakedCityMap>();
            if(stored==null||stored.Places.Length!=world.Places.Count||stored.Markers.Any(m=>m.Target==null)
                ||stored.Details==null||stored.DetailRoots.Length!=stored.Details.Groups.Length||stored.DetailRoots.Any(r=>r==null)
                ||stored.Details.Groups.Any(g=>g.Parts.Any(p=>p.Mesh==null
                    ||float.IsNaN(Quaternion.Dot(p.Rotation,p.Rotation))
                    ||Mathf.Abs(Quaternion.Dot(p.Rotation,p.Rotation)-1)>.001f))
                ||stored.ForestData==null||stored.ForestData.Chunks.Any(c=>c.Parts.Any(p=>p.Mesh==null)))
                throw new BuildFailedException("预生成地图的引用、数量或旋转记录无效。");
            long bytes=Directory.GetFiles(Output,"*",SearchOption.AllDirectories).Where(f=>!f.EndsWith(".meta")).Sum(f=>new FileInfo(f).Length);
            Debug.Log($"CITY_BAKE_RESULT 成功 trial={trial} meshes={library.MeshCount} vertices={library.VertexCount} forestParts={library.ForestPlacements} rawBytes={bytes} elapsedMs={clock.ElapsedMilliseconds}");
        }
        catch
        {
            // An incomplete generated asset must never look like a valid cache.
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)AssetDatabase.DeleteAsset(PrefabPath);
            throw;
        }
        finally
        {
            world.EndBakeCapture();
            if(world.Root!=null)
            {
                foreach(var owner in world.Root.GetComponentsInChildren<CityLandscapeResources>(true))owner.Owned.Clear();
                Object.DestroyImmediate(world.Root);
            }
            Mats.ClearNightGlow();
        }
    }
    private static string MeshHash(Mesh mesh)
    {
        // Exact attributes and indices: no rounding, mesh compression or silhouette changes.
        using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes))
        {
            writer.Write(mesh.vertexCount);writer.Write(mesh.subMeshCount);
            var attributes=mesh.GetVertexAttributes();writer.Write(attributes.Length);
            foreach(var a in attributes){writer.Write((int)a.attribute);writer.Write((int)a.format);writer.Write(a.dimension);writer.Write(a.stream);}
            var bounds=mesh.bounds;writer.Write(bounds.center.x);writer.Write(bounds.center.y);writer.Write(bounds.center.z);
            writer.Write(bounds.size.x);writer.Write(bounds.size.y);writer.Write(bounds.size.z);
            foreach(var v in mesh.vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
            foreach(var v in mesh.normals){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
            foreach(var v in mesh.tangents){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);writer.Write(v.w);}
            for(int channel=0;channel<8;channel++)
            {var uv=new List<Vector4>();mesh.GetUVs(channel,uv);writer.Write(uv.Count);foreach(var v in uv){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);writer.Write(v.w);}}
            foreach(var c in mesh.colors32){writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}
            for(int i=0;i<mesh.subMeshCount;i++){writer.Write((int)mesh.GetTopology(i));var indices=mesh.GetIndices(i);writer.Write(indices.Length);foreach(int index in indices)writer.Write(index);}
            writer.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes.ToArray()));
        }
    }
}
