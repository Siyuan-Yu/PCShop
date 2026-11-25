using System;
using System.IO;
using UnityEngine;
using UnityEditor;

public static class ImportQuarterModules
{
    public static void Run()
    {
        try
        {
            const string dir="Assets/Art/OldTownStreetStudy/Modules";
            Directory.CreateDirectory(dir); AssetDatabase.Refresh();
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/OldTownStreetStudy/Street.mat");
            var root=new GameObject("QuarterModules");
            foreach(var file in Directory.GetFiles("Modules","Street-*.bin")){
                var name=Path.GetFileNameWithoutExtension(file).Substring(7);
                using var r=new BinaryReader(File.OpenRead(file));
                var count=r.ReadInt32();var indexCount=r.ReadInt32();var pos=new Vector3[count];var norm=new Vector3[count];var uv=new Vector2[count];var color=new Color32[count];
                for(int i=0;i<count;i++){
                    pos[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                    norm[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                    byte shade=(byte)Mathf.RoundToInt(255*Mathf.Lerp(.76f,1,Mathf.InverseLerp(-.1f,.70f,pos[i].y)));
                    color[i]=new Color32(shade,shade,shade,255);
                }
                var idx=new int[indexCount];for(int i=0;i<idx.Length;i++)idx[i]=r.ReadInt32();
                var m=new Mesh{name=name};m.vertices=pos;m.normals=norm;m.uv=uv;m.colors32=color;m.triangles=idx;
                AssetDatabase.CreateAsset(m,dir+"/"+name+".asset");
                var child=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(root.transform,false);
                child.GetComponent<MeshFilter>().sharedMesh=m;child.GetComponent<MeshRenderer>().sharedMaterial=mat;
            }
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/OldTown/QuarterModules.prefab");UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
