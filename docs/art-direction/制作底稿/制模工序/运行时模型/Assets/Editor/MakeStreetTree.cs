using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class MakeStreetTree
{
 public static void Ensure(){
 const string target="Assets/Resources/OldTown/StreetTree.prefab";
 if(AssetDatabase.LoadAssetAtPath<GameObject>(target)!=null)return;
 var tree=new GameObject("行道榆树");var origin=new Vector3(4.9f,0,9.55f);
 foreach(string item in new[]{"Street","Foliage"}){
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/OldTownStreetStudy/"+item+".prefab");
 var part=prefab.transform.Find(item=="Street"?"TreeWood":"Canopies");var filter=part.GetComponent<MeshFilter>();var m=filter.sharedMesh;
 var pos=m.vertices;var ns=m.normals;var tc=m.uv;var color=m.colors32;var index=m.triangles;
 var verts=new List<Vector3>();var norm=new List<Vector3>();var uv=new List<Vector2>();var col=new List<Color32>();var indices=new List<int>();var map=new Dictionary<int,int>();
 for(int i=0;i<index.Length;i+=3){
 Vector3 center=(pos[index[i]]+pos[index[i+1]]+pos[index[i+2]])/3+part.localPosition-origin;
 if(Mathf.Abs(center.x)>2.8f||Mathf.Abs(center.z)>2.8f)continue;
 for(int j=0;j<3;j++){int old=index[i+j];
 if(!map.TryGetValue(old,out var next)){next=verts.Count;map.Add(old,next);verts.Add(pos[old]+part.localPosition-origin);uv.Add(tc[old]);norm.Add(ns[old]);col.Add(color.Length>old?color[old]:new Color32(255,255,255,255));}
 indices.Add(next);}}
 var mesh=new Mesh{name=item};mesh.SetVertices(verts);mesh.SetNormals(norm);mesh.SetColors(col);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
 AssetDatabase.CreateAsset(mesh,"Assets/Resources/OldTown/StreetTree"+item+".asset");
 var obj=new GameObject(item,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(tree.transform,false);obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=part.GetComponent<MeshRenderer>().sharedMaterial;
 }
 PrefabUtility.SaveAsPrefabAsset(tree,target);Object.DestroyImmediate(tree);AssetDatabase.SaveAssets();
 }
}