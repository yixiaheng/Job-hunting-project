using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using BelobogOptimization;

public static class BelobogOptimizationSetup
{
    public const string AssetRoot="Assets/Art/Scenes/BelobogUpperDistrict";
    public const string Folder=AssetRoot+"/Optimized";
    public const string ScenePath="Assets/Scenes/BelobogOptimization/Belobog_Optimized.unity";
    public const string Delivery=@"E:\UnityReSource\模之屋\use\BelobogUpperDistrict\Optimized";

    public static object Install()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit Mode required");
        if (GameObject.Find("Belobog_Optimized") != null) throw new InvalidOperationException("Optimized map already installed. Use the saved scene/prefab; do not install twice.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        foreach(var name in new[]{"Belobog_Visual.fbx","Belobog_Collision.fbx","Optimization_Geometry.json"})
            File.Copy(Path.Combine(Delivery,name),Folder+"/"+name,true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var name in new[]{"Belobog_Visual","Belobog_Collision"})
        {
            var imp=(ModelImporter)AssetImporter.GetAtPath(Folder+"/"+name+".fbx");
            imp.animationType=ModelImporterAnimationType.None;imp.importAnimation=false;imp.addCollider=false;
            imp.importCameras=false;imp.importLights=false;imp.meshCompression=ModelImporterMeshCompression.Off;
            imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;
            if(name=="Belobog_Visual")
            {
                foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{AssetRoot+"/Materials/URP"}))
                {
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if(mat.name.StartsWith("BelobogUpperDistrict_"))imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),mat.name),mat);
                }
            }
            else imp.materialImportMode=ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();
        }
        var originalScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(!File.Exists(ScenePath))AssetDatabase.CopyAsset("Assets/Scenes/BelobogOptimization/Belobog_BeforeOptimization.unity",ScenePath);
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        var roots=scene.GetRootGameObjects();
        var original=roots.First(r=>r.name=="BelobogUpperDistrict");
        var split=roots.First(r=>r.name=="BelobogUpperDistrict_Split");
        var map=new GameObject("Belobog_Optimized");map.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);map.transform.localScale=original.transform.localScale;
        var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Belobog_Visual.fbx"),scene);
        visual.name="Visual";visual.transform.SetParent(map.transform,false);
        var collision=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Belobog_Collision.fbx"),scene);
        collision.name="Collision";collision.transform.SetParent(map.transform,false);
        foreach(var importedLod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(importedLod);
        int lodCount=0,missing=0;long triangles=0;
        foreach(var tr in visual.GetComponentsInChildren<Transform>(true))
        {
            var renderers=tr.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length!=3||!renderers.All(r=>r.transform.parent==tr))continue;
            var lod=tr.gameObject.AddComponent<LODGroup>();
            lod.SetLODs(new[]{new LOD(.20f,new Renderer[]{renderers.Single(r=>r.name.EndsWith("_LOD0"))}),new LOD(.07f,new Renderer[]{renderers.Single(r=>r.name.EndsWith("_LOD1"))}),new LOD(.000001f,new Renderer[]{renderers.Single(r=>r.name.EndsWith("_LOD2"))})});
            lod.fadeMode=LODFadeMode.None;lod.RecalculateBounds();lodCount++;
            foreach(var r in renderers)
            {
                r.gameObject.isStatic=true;
                if(tr.name.StartsWith("Far"))r.shadowCastingMode=ShadowCastingMode.Off;
                foreach(var mat in r.sharedMaterials)if(mat==null||mat.mainTexture==null)missing++;
                if(r.name.EndsWith("_LOD0")){var mesh=r.GetComponent<MeshFilter>().sharedMesh;for(int i=0;i<mesh.subMeshCount;i++)triangles+=mesh.GetIndexCount(i)/3;}
            }
        }
        int colliders=0;long collisionTriangles=0;
        foreach(var filter in collision.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=filter.GetComponent<MeshRenderer>();if(renderer!=null)renderer.enabled=false;
            filter.gameObject.layer=filter.name.Contains("Buildings")?3:7;
            filter.gameObject.isStatic=true;
            var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;collider.convex=false;collider.isTrigger=false;
            colliders++;for(int i=0;i<filter.sharedMesh.subMeshCount;i++)collisionTriangles+=filter.sharedMesh.GetIndexCount(i)/3;
        }
        var stairs=new GameObject("Stairs");stairs.transform.SetParent(map.transform,false);
        original.SetActive(false);split.SetActive(false);
        foreach(var c in split.GetComponentsInChildren<Collider>(true))c.enabled=false;
        if(missing!=0||lodCount==0)throw new Exception("LOD/material validation failed");
        string pipelinePath=Folder+"/Belobog_Performance120.asset";
        if(!File.Exists(pipelinePath))AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset",pipelinePath);
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        var serialized=new SerializedObject(pipeline);
        serialized.FindProperty("m_ShadowCascadeCount").intValue=2;
        serialized.FindProperty("m_ShadowDistance").floatValue=35;
        serialized.FindProperty("m_SoftShadowQuality").intValue=1;
        serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue=false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var profile=map.AddComponent<BelobogPerformanceProfile>();profile.performancePipeline=pipeline;
        var benchmark=map.AddComponent<BelobogBenchmark>();benchmark.originalVisual=original;benchmark.splitVisual=split;benchmark.optimizedVisual=visual;
        benchmark.baselinePipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");benchmark.optimizedPipeline=pipeline;benchmark.mapOffset=map.transform.position;
        Physics.SyncTransforms();
        var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerController>(true)).FirstOrDefault();
        if(player!=null)
        {
            var rb=player.GetComponent<Rigidbody>();rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var ground=player.GetComponent<GroundCheck>();var so=new SerializedObject(ground);so.FindProperty("groundLayer").intValue=1<<7;so.ApplyModifiedPropertiesWithoutUndo();
        }
        // Save the reusable map without references to scene-only comparison roots.
        benchmark.originalVisual=null;benchmark.splitVisual=null;
        PrefabUtility.SaveAsPrefabAsset(map,Folder+"/Belobog_Optimized.prefab");
        benchmark.originalVisual=original;benchmark.splitVisual=split;
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();Selection.activeGameObject=map;
        var result=new{scene=ScenePath,lodGroups=lodCount,lod0Triangles=triangles,missingMaterials=missing,colliders,collisionTriangles,player=player!=null,originalSceneStillOpen=originalScene.isLoaded};
        File.WriteAllText(Folder+"/Unity_Optimization_Install.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }

    public static void Build()
    {
        string output=@"E:\Tools\Blender\BelobogBuild\Belobog.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        bool previous=PlayerSettings.enableFrameTimingStats;
        try
        {
            PlayerSettings.enableFrameTimingStats=true;
            string benchmarkScene="Assets/Scenes/BelobogOptimization/Belobog_Benchmark.unity";
            if(!File.Exists(benchmarkScene))throw new InvalidOperationException("Benchmark scene is required for comparison builds.");
            var scenes=new[]{benchmarkScene}.Concat(EditorBuildSettings.scenes.Where(s=>s.enabled&&s.path!=ScenePath&&s.path!=benchmarkScene&& !s.path.EndsWith("SampleScene.unity")).Select(s=>s.path)).ToArray();
            var report=BuildPipeline.BuildPlayer(scenes,output,BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText(@"E:\Tools\Blender\belobog_build_result.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,seconds=report.summary.totalTime.TotalSeconds,output},Newtonsoft.Json.Formatting.Indented));
        }
        finally{PlayerSettings.enableFrameTimingStats=previous;AssetDatabase.SaveAssets();}
    }
}
