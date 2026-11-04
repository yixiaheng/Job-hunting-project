using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ConvertedResourcesBuilder
{
    static string Root
    {
        get
        {
            string script = AssetDatabase.FindAssets("ConvertedResourcesBuilder t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath).First(p => p.EndsWith("/ConvertedResourcesBuilder.cs"));
            return Path.GetDirectoryName(Path.GetDirectoryName(script)).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
    static Material MaterialAt(string path, string shaderName, Texture texture)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Shader missing: " + shaderName);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        m.mainTexture = texture;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .15f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", .15f);
        EditorUtility.SetDirty(m); return m;
    }
    static GameObject Cat(string root, string modelName, Material material, RuntimeAnimatorController controller)
    {
        string path = root + "/CatCake/" + modelName + ".fbx";
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = controller != null;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new InvalidOperationException("Model missing: " + path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = modelName;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
        Animator animator = instance.GetComponent<Animator>();
        if (controller != null)
        {
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        }
        string prefabPath = root + "/CatCake/" + modelName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }
    static Bounds VisibleBounds(GameObject instance)
    {
        Bounds result = new Bounds();bool first = true;
        foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh();renderer.BakeMesh(mesh,false);
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 world = renderer.transform.TransformPoint(vertex);
                if (first) { result = new Bounds(world,Vector3.zero);first = false; } else result.Encapsulate(world);
            }
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        if (first) throw new InvalidOperationException("No skinned vertices");
        return result;
    }
    [MenuItem("Tools/Converted Resources/Rebuild Prefabs")]
    public static void Build()
    {
        string root = Root;
        Directory.CreateDirectory(root + "/CatCake/Materials");
        Directory.CreateDirectory(root + "/GenshinCharacterBackground/Materials");
        AssetDatabase.Refresh();
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
        string lit = pipeline == null ? "Standard" : "Universal Render Pipeline/Lit";
        Material catDefault = null, catAnimated = null;
        for (int i = 0; i < 36; i++)
        {
            string suffix = i < 2 ? "_Color_RGB.png" : "_Color.png";
            string name = "NPC_Special_RuanMadeCake_" + i.ToString("00");
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(root + "/CatCake/Textures/" + name + suffix);
            if (texture == null) throw new InvalidOperationException("Texture missing: " + name);
            Material material = MaterialAt(root + "/CatCake/Materials/CatCake_" + i.ToString("00") + ".mat", lit, texture);
            if (i == 4) { catDefault = material;catAnimated = material; }
        }
        string animatedPath = root + "/CatCake/CatCake_Animated.fbx";
        ModelImporter ai = (ModelImporter)AssetImporter.GetAtPath(animatedPath);
        ai.animationType = ModelImporterAnimationType.Generic;
        ai.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; ai.importAnimation = true;
        ai.SaveAndReimport();
        ModelImporterClipAnimation[] settings = ai.defaultClipAnimations;
        foreach (var clip in settings)
        {
            clip.name = clip.name.IndexOf("Bounce", StringComparison.OrdinalIgnoreCase) >= 0 ? "Bounce" : "Idle";
            clip.loopTime = true; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = true;
        }
        ai.clipAnimations = settings; ai.SaveAndReimport();
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(animatedPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        if (clips.Length != 2 || clips.Any(c => c.length <= 0)) throw new InvalidOperationException("Expected two nonempty cat animations");
        string controllerPath = root + "/CatCake/CatCake.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var s in machine.states) machine.RemoveState(s.state);
        foreach (AnimationClip clip in clips)
        {
            var state = machine.AddState(clip.name); state.motion = clip;
            if (clip.name == "Idle") machine.defaultState = state;
        }
        Cat(root, "CatCake", catDefault, null);
        Cat(root, "CatCake_Animated", catAnimated, controller);
        string bg = root + "/GenshinCharacterBackground";
        Material sky = MaterialAt(bg + "/Materials/Genshin_Sky.mat", "ConvertedResources/Unlit",
            AssetDatabase.LoadAssetAtPath<Texture>(bg + "/Textures/Genshin_Sky.exr"));
        Material ground = MaterialAt(bg + "/Materials/Genshin_Ground.mat", "ConvertedResources/ReflectiveGround",
            AssetDatabase.LoadAssetAtPath<Texture>(bg + "/Textures/Genshin_Sky.exr"));
        ground.color = new Color(.85f,.85f,.85f,1);
        Material particles = MaterialAt(bg + "/Materials/Genshin_Particles.mat", "ConvertedResources/Particles",
            AssetDatabase.LoadAssetAtPath<Texture>(bg + "/Textures/Eff_UI_TanukiTravel_Star01.png"));
        GameObject backgroundSource = AssetDatabase.LoadAssetAtPath<GameObject>(bg + "/GenshinCharacterBackground.fbx");
        GameObject background = (GameObject)PrefabUtility.InstantiatePrefab(backgroundSource);
        background.name = "GenshinCharacterBackground";
        foreach (Camera c in background.GetComponentsInChildren<Camera>(true)) { c.nearClipPlane = .05f;c.farClipPlane = 1000; }
        foreach (Renderer r in background.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = r.name.Contains("Sky") ? sky : ground;
            if (r.name.Contains("Sky")) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        }
        foreach (Light l in background.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l.gameObject);
        GameObject keyObject = new GameObject("Key Light");keyObject.transform.SetParent(background.transform);
        keyObject.transform.rotation = Quaternion.Euler(45,-35,0);
        Light key = keyObject.AddComponent<Light>();key.type = LightType.Directional;key.intensity = 1.2f;
        GameObject fillObject = new GameObject("Fill Light");fillObject.transform.SetParent(background.transform);
        fillObject.transform.rotation = Quaternion.Euler(25,135,0);
        Light fill = fillObject.AddComponent<Light>();fill.type = LightType.Directional;fill.intensity = .45f;fill.color = new Color(.65f,.55f,1);
        GameObject probeObject = new GameObject("Environment Reflection");probeObject.transform.SetParent(background.transform);
        probeObject.transform.localPosition = new Vector3(0,1.5f,0);
        ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;probe.size = new Vector3(100,100,100);probe.resolution = 128;
        GameObject particleObject = new GameObject("Unity Ambient Stars");particleObject.transform.SetParent(background.transform);
        particleObject.transform.localPosition = new Vector3(0,.2f,0);
        ParticleSystem ps = particleObject.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;main.loop = true;main.prewarm = true;main.duration = 12;main.startLifetime = 12;
        main.startSpeed = .18f;main.startSize = new ParticleSystem.MinMaxCurve(.015f,.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.6f,.3f,1,.6f),new Color(.9f,.75f,1,.85f));main.maxParticles = 180;
        var emission = ps.emission;emission.rateOverTime = 12;
        var shape = ps.shape;shape.shapeType = ParticleSystemShapeType.Box;shape.scale = new Vector3(8,.1f,8);
        var velocity = ps.velocityOverLifetime;velocity.enabled = true;velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(.12f,.28f);velocity.x = new ParticleSystem.MinMaxCurve(-.04f,.04f);
        velocity.z = new ParticleSystem.MinMaxCurve(-.04f,.04f);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = particles;
        ps.Play();
        PrefabUtility.SaveAsPrefabAsset(background,bg + "/GenshinCharacterBackground.prefab");
        UnityEngine.Object.DestroyImmediate(background);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("CONVERTED_RESOURCES_BUILT " + root + " clips=" + string.Join(",",clips.Select(c => c.name + ":" + c.length)));
    }
    public static void BuildAndValidate()
    {
        try
        {
            Build();string root = Root;
            foreach (string name in new[] {"CatCake/CatCake","CatCake/CatCake_Animated","GenshinCharacterBackground/GenshinCharacterBackground"})
            {
                GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/" + name + ".prefab");
                if (p == null) throw new InvalidOperationException("Prefab missing: " + name);
                foreach (Renderer renderer in p.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        if (material == null || material.shader == null || !material.shader.isSupported)
                            throw new InvalidOperationException("Invalid material on " + renderer.name);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var bg = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(root + "/GenshinCharacterBackground/GenshinCharacterBackground.prefab"));
            Camera camera = bg.GetComponentInChildren<Camera>(true);
            if (camera == null) throw new InvalidOperationException("Background camera missing");
            camera.enabled = true;camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = new Color(.05f,.02f,.1f);
            Debug.Log("BACKGROUND_CAMERA position=" + camera.transform.position + " scale=" + camera.transform.lossyScale + " fov=" + camera.fieldOfView + " clip=" + camera.nearClipPlane + "," + camera.farClipPlane);
            foreach (Renderer r in bg.GetComponentsInChildren<Renderer>()) Debug.Log("BACKGROUND_RENDERER " + r.name + " position=" + r.transform.position + " size=" + r.bounds.size);
            var cat = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(root + "/CatCake/CatCake_Animated.prefab"));
            cat.transform.localScale = Vector3.one * 4.5f;
            Bounds bounds = VisibleBounds(cat);
            cat.transform.position += new Vector3(-bounds.center.x,-bounds.min.y + .02f,-bounds.center.z);
            Debug.Log("CAT_PLACEMENT boundsBefore=" + bounds + " position=" + cat.transform.position);
            foreach (Renderer r in cat.GetComponentsInChildren<Renderer>()) Debug.Log("CAT_RENDERER " + r.name + " bounds=" + r.bounds + " enabled=" + r.enabled);
            camera.transform.LookAt(new Vector3(0, .75f, 0));
            RenderSettings.ambientMode = AmbientMode.Flat;RenderSettings.ambientLight = new Color(.35f,.3f,.45f);
            EditorSceneManager.SaveScene(scene,root + "/ConvertedResources_Demo.unity");
            AssetDatabase.SaveAssets();
            var target = new RenderTexture(1280,720,24);camera.targetTexture = target;camera.Render();
            RenderTexture.active = target;var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes(root + "/UnityPreview.png",image.EncodeToPNG());
            RenderTexture.active = null;camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
            var animationAssets = AssetDatabase.LoadAllAssetsAtPath(root + "/CatCake/CatCake_Animated.fbx").OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            foreach (AnimationClip clip in animationAssets)
            {
                clip.SampleAnimation(cat,0);
                var smr = cat.GetComponentInChildren<SkinnedMeshRenderer>();var before = new Mesh();smr.BakeMesh(before);
                clip.SampleAnimation(cat,1);var after = new Mesh();smr.BakeMesh(after);
                Vector3[] a = before.vertices,b = after.vertices;float movement = 0;
                for (int i = 0;i < a.Length;i++) movement = Mathf.Max(movement,(a[i]-b[i]).magnitude);
                Debug.Log("ANIMATION_DEFORMATION " + clip.name + " maxVertexDeltaMetres=" + movement);
                UnityEngine.Object.DestroyImmediate(before);UnityEngine.Object.DestroyImmediate(after);
                if (movement < .0001f) throw new InvalidOperationException("Animation has no visible deformation: " + clip.name);
            }
            UnityEngine.Object.DestroyImmediate(cat);
            var original = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(root + "/CatCake/CatCake.prefab"));
            original.transform.localScale = Vector3.one*4.5f;bounds = VisibleBounds(original);
            original.transform.position += new Vector3(-bounds.center.x,-bounds.min.y+.02f,-bounds.center.z);
            target = new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image = new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes(root + "/UnityPreview_Original.png",image.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
            Debug.Log("CONVERSION_VALIDATION_PASSED");EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
