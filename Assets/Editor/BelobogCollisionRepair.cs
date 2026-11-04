using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class BelobogCollisionRepair
{
 public static object Run(){
 var map=GameObject.Find("Belobog_Optimized");int fixedMeshes=0;long added=0;
 string folder=BelobogOptimizationSetup.Folder+"/CollisionMeshes";System.IO.Directory.CreateDirectory(folder);AssetDatabase.Refresh();
 foreach(var c in map.transform.Find("Collision").GetComponentsInChildren<MeshCollider>(true)){
 var source=c.sharedMesh;if(source==null)continue;
 var vertices=new List<Vector3>(source.vertices);var input=source.triangles;bool needs=false;
 for(int i=0;i<input.Length;i+=3){var a=vertices[input[i]];var b=vertices[input[i+1]];var d=vertices[input[i+2]];if((a-b).sqrMagnitude>40000||(a-d).sqrMagnitude>40000||(b-d).sqrMagnitude>40000){needs=true;break;}}
 if(!needs)continue;
 var output=new List<int>(input.Length);var stack=new Stack<(int a,int b,int c)>();var mids=new Dictionary<ulong,int>();
 int Mid(int a,int b){ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);if(mids.TryGetValue(key,out int idx))return idx;idx=vertices.Count;vertices.Add((vertices[a]+vertices[b])*.5f);mids[key]=idx;return idx;}
 for(int i=0;i<input.Length;i+=3)stack.Push((input[i],input[i+1],input[i+2]));
 while(stack.Count>0){var t=stack.Pop();float ab=(vertices[t.a]-vertices[t.b]).sqrMagnitude,bc=(vertices[t.b]-vertices[t.c]).sqrMagnitude,ca=(vertices[t.c]-vertices[t.a]).sqrMagnitude;
 if(Mathf.Max(ab,Mathf.Max(bc,ca))<=40000){output.Add(t.a);output.Add(t.b);output.Add(t.c);continue;}
 if(ab>=bc&&ab>=ca){int m=Mid(t.a,t.b);stack.Push((t.a,m,t.c));stack.Push((m,t.b,t.c));}
 else if(bc>=ca){int m=Mid(t.b,t.c);stack.Push((t.a,t.b,m));stack.Push((t.a,m,t.c));}
 else{int m=Mid(t.c,t.a);stack.Push((t.a,t.b,m));stack.Push((m,t.b,t.c));}
 }
 var mesh=new Mesh{name=source.name+"_Physics",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(output,0);mesh.RecalculateBounds();
 AssetDatabase.CreateAsset(mesh,folder+"/"+mesh.name+".asset");c.sharedMesh=mesh;added+=(output.Count-input.Length)/3;fixedMeshes++;
 }
 UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();return new{fixedMeshes,addedTriangles=added};
 }
}
