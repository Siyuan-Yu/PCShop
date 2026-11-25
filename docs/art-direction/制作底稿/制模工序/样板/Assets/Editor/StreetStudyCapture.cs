using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class StreetStudyCapture
{
    const string Root = "Assets/Art/OldTownStreetStudy";
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
        var mat = new Material(Shader.Find("PCShop/Old Town Street Paint"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        if(name=="Foliage")
        {
            mat.SetFloat("_AlphaClip",1f); mat.SetFloat("_Cutoff",.48f);mat.SetFloat("_Cull",0f);
            mat.SetFloat("_PaperFill",.69f);
            mat.SetFloat("_Smoothness",0f);mat.SetFloat("_SpecularHighlights",0f);
            mat.EnableKeyword("_ALPHATEST_ON");mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=(int)RenderQueue.AlphaTest;
        }
        else mat.SetFloat("_PaperFill",.43f);
        AssetDatabase.CreateAsset(mat, Root + "/" + name + ".mat");
        var model = new GameObject(name);
        foreach(string file in Directory.GetFiles(Path.Combine(Project,"StreetWork"),name+"-*.bin").OrderBy(x=>x))
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
    static void BakeContactShade(GameObject architecture)
    {
        var filters=architecture.GetComponentsInChildren<MeshFilter>();
        var colliders=filters.Select(f=>{var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;return c;}).ToArray();
        Physics.SyncTransforms();
        foreach(var f in filters)
        {
            var mesh=f.sharedMesh;var verts=mesh.vertices;var normals=mesh.normals;var shade=new Color32[verts.Length];
            for(int i=0;i<verts.Length;i++)
            {
                var n=normals[i];var axis=Vector3.Cross(n,Mathf.Abs(n.y)>.85f?Vector3.right:Vector3.up).normalized;var second=Vector3.Cross(n,axis);
                var point=f.transform.TransformPoint(verts[i])+n*.018f;float cover=0;
                for(int j=0;j<24;j++)
                {
                    float v=(j+.5f)/24f, r=Mathf.Sqrt(v), a=j*2.39996f;
                    var dir=axis*(r*Mathf.Cos(a))+second*(r*Mathf.Sin(a))+n*Mathf.Sqrt(1-v);
                    if(Physics.Raycast(point,dir,out var hit,1.9f))cover+=1-Mathf.Clamp01(hit.distance/1.9f);
                }
                byte value=(byte)Mathf.RoundToInt(255*(1-.81f*cover/24));
                shade[i]=new Color32(value,value,value,255);
            }
            mesh.colors32=shade;EditorUtility.SetDirty(mesh);
        }
        foreach(var c in colliders)Object.DestroyImmediate(c);
    }
    static void Frame(Vector3 focus,Vector3 eye,float halfHeight)
    {
        camera.orthographic=true; camera.orthographicSize=halfHeight;
        camera.transform.position=focus+eye.normalized*36;
        camera.transform.rotation=Quaternion.LookRotation(-eye.normalized,Vector3.up);
    }
    static void SaveShot(string name,int w,int h)
    {
        var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);
        var prev=RenderTexture.active; camera.targetTexture=rt;camera.aspect=(float)w/h;
        camera.Render();camera.Render(); RenderTexture.active=rt;
        var frame=new Texture2D(w,h,TextureFormat.RGB24,false);
        frame.ReadPixels(new Rect(0,0,w,h),0,0);frame.Apply();
        File.WriteAllBytes(Path.Combine(Project,"StreetShots",name+".png"),frame.EncodeToPNG());
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
        var body=BuildModel("Street");var foliage=BuildModel("Foliage");
        var house=Place(body,Vector3.zero,0);Place(foliage,Vector3.zero,0);
        BakeContactShade(house);
        const string npcs="Assets/Art/OldTownShopStudy/";
        Place(AssetDatabase.LoadAssetAtPath<GameObject>(npcs+"JacketYouth.prefab"),new Vector3(-.46f,.107f,-1.60f),-6);
        Place(AssetDatabase.LoadAssetAtPath<GameObject>(npcs+"Landlady.prefab"),new Vector3(4.73f,.107f,-.68f),29,.94f);
        var sun=new GameObject("午后斜阳").AddComponent<Light>();sun.type=LightType.Directional;
        sun.intensity=1.14f;sun.color=new Color(1,.94f,.84f);sun.shadows=LightShadows.Soft;
        sun.shadowStrength=.85f;sun.shadowBias=.055f;sun.shadowNormalBias=.13f;
        sun.transform.rotation=Quaternion.LookRotation(new Vector3(-1.20f,-.92f,.54f));
        RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.64f,.64f,.56f);
        camera=new GameObject("街区经营视角").AddComponent<Camera>();camera.tag="MainCamera";
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.60f,.60f,.47f);
        camera.nearClipPlane=.08f;camera.farClipPlane=140;camera.allowHDR=true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        Frame(new Vector3(.80f,4.76f,1.10f),new Vector3(-7.8f,9.8f,-13.5f),8.53f);
        camera.aspect=16f/9;
        EditorSceneManager.SaveScene(scene,Root+"/OldTownShopfront.unity");AssetDatabase.SaveAssets();
        SaveShot("01-街区经营视角",1920,1080);
        var shopFocus=new Vector3(.35f,2.08f,-.10f);
        Frame(shopFocus,new Vector3(-4.8f,4.1f,-13),3.20f);
        camera.transform.position=shopFocus-camera.transform.forward*9.3f;
        SaveShot("02-店门与人行道",1800,1080);
        Debug.Log("STREET_ART_CAPTURE_COMPLETE");EditorApplication.Exit(0);
    }
}
