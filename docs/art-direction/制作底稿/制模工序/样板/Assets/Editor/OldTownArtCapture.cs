using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class OldTownArtCapture
{
    const string Root = "Assets/Art/OldTownShopStudy";
    static readonly string Project = Path.GetDirectoryName(Application.dataPath);
    static Vector3 ReadVector(BinaryReader b) => new Vector3(b.ReadSingle(), b.ReadSingle(), b.ReadSingle());
    static GameObject BuildModel(string name)
    {
        string texturePath = Root + "/" + name + "Albedo.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.SaveAndReimport();
        var mat = new Material(Shader.Find("PCShop/Old Town Painted Study"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        mat.SetFloat("_PaperFill",name=="Shop" ? .48f : .68f);
        AssetDatabase.CreateAsset(mat, Root + "/" + name + ".mat");
        var model = new GameObject(name);
        foreach(string file in Directory.GetFiles(Path.Combine(Project,"SourceData"),name+"-*.bin").OrderBy(x=>x))
        {
            string part = Path.GetFileNameWithoutExtension(file).Substring(name.Length+1);
            using var b=new BinaryReader(File.OpenRead(file));
            int count=b.ReadInt32(), indices=b.ReadInt32();
            var pos=new Vector3[count]; var normals=new Vector3[count]; var uv=new Vector2[count];
            for(int i=0;i<count;i++)
            {
                pos[i]=ReadVector(b); normals[i]=ReadVector(b); uv[i]=new Vector2(b.ReadSingle(),b.ReadSingle());
            }
            var tri=new int[indices]; for(int i=0;i<indices;i++)tri[i]=b.ReadInt32();
            var bounds=new Bounds(pos[0],Vector3.zero); foreach(var v in pos)bounds.Encapsulate(v);
            Vector3 pivot=bounds.center;
            if(name=="Shop")pivot.y=bounds.min.y;
            else if(part.Contains("Sleeve") || part.Contains("Leg"))pivot.y=bounds.max.y;
            for(int i=0;i<count;i++)pos[i]-=pivot;
            var mesh=new Mesh{name=part, indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=pos;mesh.normals=normals;mesh.uv=uv;mesh.triangles=tri;mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Root+"/Meshes/"+name+"_"+part+".asset");
            var obj=new GameObject(part);obj.transform.SetParent(model.transform,false);obj.transform.localPosition=pivot;
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(model,Root+"/"+name+".prefab");
        Object.DestroyImmediate(model);return prefab;
    }
    static Camera camera;
    static void Frame(Vector3 focus,Vector3 eye,float halfHeight)
    {
        camera.orthographic=true; camera.orthographicSize=halfHeight;
        camera.transform.position=focus+eye.normalized*16;
        camera.transform.rotation=Quaternion.LookRotation(-eye.normalized,Vector3.up);
    }
    static void SaveShot(string name,int w,int h)
    {
        var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);
        var prev=RenderTexture.active; camera.targetTexture=rt;camera.aspect=(float)w/h;
        camera.Render();camera.Render(); RenderTexture.active=rt;
        var frame=new Texture2D(w,h,TextureFormat.RGB24,false);
        frame.ReadPixels(new Rect(0,0,w,h),0,0);frame.Apply();
        File.WriteAllBytes(Path.Combine(Project,"Captures",name+".png"),frame.EncodeToPNG());
        Object.DestroyImmediate(frame);camera.targetTexture=null;RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);
    }
    static GameObject Place(GameObject prefab,Vector3 pos,float yaw,float scale=1)
    {
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        obj.transform.position=pos;obj.transform.rotation=Quaternion.Euler(0,yaw,0);
        obj.transform.localScale=Vector3.one*scale;return obj;
    }
    public static void Run()
    {
        ShaderUtil.allowAsyncCompilation=false;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(!AssetDatabase.IsValidFolder(Root+"/Meshes"))AssetDatabase.CreateFolder(Root,"Meshes");
        var room=BuildModel("Shop");var man=BuildModel("JacketYouth");var woman=BuildModel("Landlady");
        var roomObj=Place(room,Vector3.zero,0);
        var manObj=Place(man,new Vector3(1.08f,.02f,-1.65f),12);
        var ladyObj=Place(woman,new Vector3(-2.18f,.02f,.91f),-7,.94f);
        var sun=new GameObject("午后日光").AddComponent<Light>();sun.type=LightType.Directional; sun.intensity=1.12f;
        sun.color=new Color(1f,.929f,.816f);sun.shadows=LightShadows.Soft;sun.shadowStrength=.88f;
        sun.shadowBias=.052f;sun.shadowNormalBias=.10f;sun.transform.rotation=Quaternion.LookRotation(new Vector3(.72f,-1f,.07f));
        RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.64f,.61f,.54f);
        camera=new GameObject("经营视角").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.55f,.53f,.43f);camera.nearClipPlane=.08f;camera.farClipPlane=100;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var axis=new Vector3(-8,9.4f,-12);
        Frame(new Vector3(.15f,.74f,-.42f),axis,4.32f);camera.aspect=16f/9;
        string path=Root+"/OldTownComputerShop.unity";
        EditorSceneManager.SaveScene(scene,path);AssetDatabase.SaveAssets();
        SaveShot("01-电脑店经营视角",1920,1080);
        Frame(new Vector3(-.55f,.93f,-.26f),new Vector3(-5,5.3f,-10),2.25f);
        SaveShot("02-店内两位NPC",1500,1000);
        // Neutral studio view of the same two prefab instances and materials.
        roomObj.SetActive(false);manObj.transform.position=new Vector3(-.61f,0,0);ladyObj.transform.position=new Vector3(.61f,0,.02f);
        manObj.transform.rotation=Quaternion.Euler(0,-22,0);ladyObj.transform.rotation=Quaternion.Euler(0,-17,0);
        camera.backgroundColor=new Color(.78f,.75f,.68f);
        Frame(new Vector3(0,.85f,0),new Vector3(0,.9f,-10),1.015f);
        SaveShot("03-两位NPC模型近景",1280,960);
        manObj.transform.rotation=Quaternion.Euler(0,120,0);ladyObj.transform.rotation=Quaternion.Euler(0,-96,0);
        SaveShot("04-背面与侧面",1280,960);
        Debug.Log("OLD_TOWN_CAPTURE_COMPLETE "+path);EditorApplication.Exit(0);
    }
}
