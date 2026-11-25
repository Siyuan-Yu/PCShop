using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class AlternateHeads
{
 public static void Ensure(){
 foreach(var actor in new[]{"JacketYouth","Landlady"}){
 string stem="Assets/Resources/OldTown/"+actor;
 if(AssetDatabase.LoadAssetAtPath<Material>(stem+"Paint.mat")==null)AssetDatabase.MoveAsset(stem+".mat",stem+"Paint.mat");
 var previous=AssetDatabase.LoadAssetAtPath<Mesh>(stem+"Head.asset");
 var src=AssetDatabase.LoadAssetAtPath<Mesh>(stem+".asset");var tri=src.triangles;var srcW=src.boneWeights;var srcV=src.vertices;var srcN=src.normals;var srcU=src.uv;
 var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var w=new List<BoneWeight>();var ix=new List<int>();var lookup=new Dictionary<int,int>();
 for(int i=0;i<tri.Length;i+=3){
 if(srcW[tri[i]].boneIndex0!=2)continue;
 for(int k=0;k<3;k++){int old=tri[i+k];if(!lookup.TryGetValue(old,out int j)){j=v.Count;lookup.Add(old,j);v.Add(srcV[old]);n.Add(srcN[old]);uv.Add(srcU[old]);w.Add(srcW[old]);}ix.Add(j);}}
 var mesh=new Mesh{name=actor+"Head"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(ix,0);mesh.boneWeights=w.ToArray();mesh.bindposes=src.bindposes;AssetDatabase.CreateAsset(mesh,stem+"Head.asset");
 }
 AssetDatabase.SaveAssets();
 }
}