using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class RefineActors
{
    const string dir="Assets/Art/OldTownShopStudy/";
    const string dest="Assets/Resources/OldTown/";
    static Vector3 Vec(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    public static void Run(){
      try {
        Directory.CreateDirectory(dest);AssetDatabase.Refresh();
        foreach(string actor in new[]{"JacketYouth","Landlady"}){
          var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(dir+actor+".prefab");
          var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();var weights=new List<BoneWeight>();
          foreach(Transform t in prefab.transform){
            var m=t.GetComponent<MeshFilter>().sharedMesh;
            var path="Refined/"+actor+"-"+t.name+".bin";
            if(File.Exists(path)){using var br=new BinaryReader(File.OpenRead(path));
              int n=br.ReadInt32(),ct=br.ReadInt32();var p=new Vector3[n];var no=new Vector3[n];var tc=new Vector2[n];var tris=new int[ct];
              for(int i=0;i<n;i++){p[i]=Vec(br)-t.localPosition;no[i]=Vec(br);tc[i]=new Vector2(br.ReadSingle(),br.ReadSingle());}
              for(int i=0;i<ct;i++)tris[i]=br.ReadInt32();
              m.Clear();m.vertices=p;m.normals=no;m.uv=tc;m.triangles=tris;m.RecalculateBounds();EditorUtility.SetDirty(m);
            }
            int offset=vertices.Count;
            foreach(var point in m.vertices){
              var p=point+t.localPosition;p=new Vector3(-p.x,p.y,-p.z);string k=t.name.ToLowerInvariant();
              // Map each limb to the same wrist/ankle landmarks as the gameplay skeleton.
              bool hand=k.Contains("hand"),arm=k.Contains("sleeve")||hand,leg=k.Contains("leg")||k.Contains("shoe");
              int side=p.x<0?0:1;int shoulder=3+3*side,hip=9+3*side;
              float y=p.y; float scaled=y;
              if(arm){scaled=y<=.78f?Mathf.LerpUnclamped(.69f,.774f,(y-.63f)/.15f):Mathf.LerpUnclamped(.774f,1.165f,(y-.78f)/.43f);}
              p.y=scaled;vertices.Add(p);
              BoneWeight bw=new BoneWeight();
              void Pair(int a,int b,float f){bw.boneIndex0=a;bw.boneIndex1=b;bw.weight1=Mathf.SmoothStep(0,1,f);bw.weight0=1-bw.weight1;}
              if(hand)Pair(shoulder+2,shoulder+1,Mathf.InverseLerp(.70f,.77f,scaled)*.45f);
              else if(arm)Pair(shoulder+1,shoulder,Mathf.InverseLerp(.83f,.98f,scaled));
              else if(leg && k.Contains("shoe")) Pair(hip+2,hip+1,Mathf.InverseLerp(.09f,.16f,y)*.5f);
              else if(leg)Pair(hip+1,hip,Mathf.InverseLerp(.33f,.49f,y));
              else if(k=="skirt")Pair(hip,1,Mathf.InverseLerp(.30f,.82f,y));
              else if(k.Contains("hair")||k.Contains("face")||k.Contains("waves")||k=="crown"||k=="bobback") Pair(2,2,0);
              else Pair(1,1,0);
              weights.Add(bw);
            }
            normals.AddRange(m.normals.Select(n=>new Vector3(-n.x,n.y,-n.z)));uv.AddRange(m.uv);indices.AddRange(m.triangles.Select(i=>offset+i));
          }
          var mesh=new Mesh{name=actor,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.boneWeights=weights.ToArray();
          Vector3[] bones=new Vector3[15];bones[2]=new Vector3(0,1.275f,.015f);
          for(int i=0;i<2;i++){float s=i==0?-1:1;int j=3+i*3; bones[j]=new Vector3(s*.133824f,1.165f,.00384f);bones[j+1]=bones[j]+new Vector3(s*.055896f,-.269f,.00672f);bones[j+2]=bones[j+1]+new Vector3(s*.03964f,-.191f,.01344f);
          int l=9+i*3;bones[l]=new Vector3(s*.09064f,.755f,0);bones[l+1]=bones[l]+Vector3.down*.3425f;bones[l+2]=bones[l+1]+Vector3.down*.3425f;}
          mesh.bindposes=bones.Select(p=>Matrix4x4.Translate(-p)).ToArray();AssetDatabase.CreateAsset(mesh, dest+actor+".asset");
          // A shared material dependency keeps the exact study artwork available in player builds.
          var mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(dir+actor+".mat"));mat.SetFloat("_PaperFill",.69f);EditorUtility.CopySerialized(mat, AssetDatabase.LoadAssetAtPath<Material>(dest+actor+"Paint.mat"));
        }
        AssetDatabase.SaveAssets();AlternateHeads.Ensure();
        var scene=EditorSceneManager.OpenScene(dir+"OldTownComputerShop.unity");var cam=Camera.main;Photo(cam,"sample-shop");
        var people=scene.GetRootGameObjects().Where(g=>g.name.Contains("JacketYouth")||g.name.Contains("Landlady")).OrderBy(g=>g.name).ToArray();
        foreach(var o in scene.GetRootGameObjects())if(o.name.Contains("Shop")&&o.GetComponent<Camera>()==null)o.SetActive(false);
        for(int i=0;i<2;i++)people[i].transform.position=new Vector3(-.4f+i*.8f,0,0);
        cam.backgroundColor=new Color(.78f,.75f,.68f);cam.transform.position=new Vector3(0,1.08f,-6);cam.transform.LookAt(new Vector3(0,.89f,0));cam.orthographicSize=.96f;
        foreach(int angle in new[]{0,35,75,95,145,180,235,285}){foreach(var o in people)o.transform.rotation=Quaternion.Euler(0,angle,0);Photo(cam,"turn-"+angle);}
        cam.orthographicSize=.51f;cam.transform.position+=Vector3.up*.47f;
        foreach(int angle in new[]{0,-35,75,145,-95}){foreach(var o in people)o.transform.rotation=Quaternion.Euler(0,angle,0);Photo(cam,"face-"+angle);}
        EditorApplication.Exit(0);
      }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    public static void Photo(Camera cam,string name){
     ShaderUtil.allowAsyncCompilation=false;Directory.CreateDirectory("ReviewShots");cam.aspect=1.4f;var rt=RenderTexture.GetTemporary(1260,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);cam.targetTexture=rt;var prev=RenderTexture.active;
     cam.Render();cam.Render();RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();File.WriteAllBytes("ReviewShots/"+name+".png",t.EncodeToPNG());RenderTexture.active=prev;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(t);RenderTexture.ReleaseTemporary(rt);
    }
}