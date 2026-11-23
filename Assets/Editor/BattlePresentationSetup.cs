using System;
using System.IO;
using System.Linq;
using Battle.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

/// <summary>Explicit, idempotent setup. Existing authored assets and mappings are never overwritten.</summary>
[InitializeOnLoad]
public static class BattlePresentationSetup
{
    public const string Root = "Assets/Config/Battle/Presentation";
    public const string CameraRoot = Root + "/Camera";
    private const string Folder = "Temp/BattlePresentation";
    static BattlePresentationSetup() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
            || !File.Exists(Folder + "/request.txt")) return;
        string command = File.ReadAllText(Folder + "/request.txt").Trim();
        File.Delete(Folder + "/request.txt");
        try
        {
            if (command == "setup") Setup();
            else if (command == "preview-source") { BattlePresentationStudio.Open(); var studio = EditorWindow.GetWindow<BattlePresentationStudio>(); studio.ReloadFromAssets(); studio.ExportPreview(Folder + "/studio-preview.png"); }
            else if (command == "preview") { BattlePresentationStudio.Open(); EditorWindow.GetWindow<BattlePresentationStudio>().ExportPreview(Folder + "/studio-preview.png"); }
            else if (command == "test-scene") { SceneFlowVerification.BeginBattle(); return; }
            else if (command == "test-rebuild") BattleRebuildVerification.Run();
            else if (command == "test") BattlePresentationVerification.Run();
            else if (command == "test-tuning") BattlePresentationVerification.RunTuning();
            else if (command == "play") { BattlePresentationPlayVerification.Begin(); return; }
            else if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "tune") BattlePresentationStudio.Open();
            else if (command == "reimport-samples")
            {
                AssetDatabase.ImportAsset(Root + "/SampleBattlefield.playable", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(Root + "/SampleSeparateStage.playable", ImportAssetOptions.ForceUpdate);
            }
            else if (command == "diagnose")
            {
                var sequence = AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(Root + "/SampleBattlefield.asset");
                File.WriteAllText(Folder + "/timeline-diagnostic.txt", string.Join("\n",
                    AssetDatabase.LoadAllAssetsAtPath(Root + "/SampleBattlefield.playable").Select(a => a == null ? "null" : a.name + " " + a.GetType().FullName))
                    + "\nmarkerTrack=" + (sequence.timeline.markerTrack != null ? EditorJsonUtility.ToJson(sequence.timeline.markerTrack) : "null")
                    + "\nrawMarkers=" + (sequence.timeline.markerTrack != null ? string.Join(",", sequence.timeline.markerTrack.GetMarkers().Select(m => m == null ? "null" : m.GetType().FullName)) : "null"));
            }
            else throw new ArgumentException("Unknown presentation command: " + command);
            File.WriteAllText(Folder + "/status.txt", "SUCCESS " + command + " " + DateTimeOffset.Now);
        }
        catch (Exception error)
        {
            File.WriteAllText(Folder + "/status.txt", error.ToString()); Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Battle/Presentation/Configure Battle Scene and Samples")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        const string path = "Assets/Scenes/BattleScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("BattleScene has unsaved edits. Save it before configuring the camera.");
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Directory.CreateDirectory(CameraRoot); AssetDatabase.Refresh();
            var profile = AssetDatabase.LoadAssetAtPath<BattleCameraProfile>(CameraRoot + "/DefaultCamera.asset");
            if (profile == null) { profile = ScriptableObject.CreateInstance<BattleCameraProfile>(); AssetDatabase.CreateAsset(profile, CameraRoot + "/DefaultCamera.asset"); }
            if (profile.Shots.Length == 0)
            {
                string backup = AssetDatabase.GenerateUniqueAssetPath(CameraRoot + "/BeforeRebuildCamera.asset");
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(profile), backup);
                profile.InitializeShots(); EditorUtility.SetDirty(profile);
            }
            var layout = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(CameraRoot + "/DefaultLayout.asset");
            if (layout == null) { layout = ScriptableObject.CreateInstance<BattlefieldLayout>(); layout.InitializeDefaults(); AssetDatabase.CreateAsset(layout, CameraRoot + "/DefaultLayout.asset"); }
            var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleManager>(true)).Single();
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
            var serializedManager = new SerializedObject(manager);
            var coordinator = serializedManager.FindProperty("presentation").objectReferenceValue as BattlePresentationCoordinator;
            if (coordinator == null)
            {
                var root = new GameObject("BattlePresentation"); SceneManager.MoveGameObjectToScene(root, scene);
                coordinator = root.AddComponent<BattlePresentationCoordinator>();
                coordinator.cameraDirector = root.AddComponent<BattleShotDirector>();
                coordinator.timelineDirector = root.AddComponent<PlayableDirector>();
                coordinator.timelineDirector.playOnAwake = false;
                coordinator.timelineDirector.timeUpdateMode = DirectorUpdateMode.Manual;
                var normal = new GameObject("NormalCamera"); normal.transform.SetParent(root.transform, false);
                var cinematic = new GameObject("CinematicCamera"); cinematic.transform.SetParent(root.transform, false);
                coordinator.cameraDirector.normalCamera = normal.AddComponent<CinemachineCamera>();
                coordinator.cameraDirector.cinematicCamera = cinematic.AddComponent<CinemachineCamera>();
            }
            var field = coordinator.GetComponent<BattlefieldDirector>();
            if (field == null) field = coordinator.gameObject.AddComponent<BattlefieldDirector>();
            var origin = coordinator.transform.Find("BattlefieldRoot");
            if (origin == null) { origin = new GameObject("BattlefieldRoot").transform; origin.SetParent(coordinator.transform, false); }
            field.Configure(layout, origin); manager.ConfigureBattlefield(field);
            EditorUtility.SetDirty(field); EditorUtility.SetDirty(manager);
            var director = coordinator.cameraDirector;
            director.outputCamera = camera;
            director.brain = camera.GetComponent<CinemachineBrain>();
            if (director.brain == null) director.brain = camera.gameObject.AddComponent<CinemachineBrain>();
            director.brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            if (director.profile == null) director.profile = profile;
            // A dedicated output channel prevents exploration/character preview virtual cameras from taking over.
            director.normalCamera.OutputChannel = (OutputChannels)(1 << 2);
            director.cinematicCamera.OutputChannel = (OutputChannels)(1 << 2);
            director.brain.ChannelMask = (OutputChannels)(1 << 2);
            director.normalCamera.Priority = 10; director.cinematicCamera.Priority = 0;
            director.normalCamera.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            director.cinematicCamera.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            serializedManager.FindProperty("presentation").objectReferenceValue = coordinator;
            serializedManager.FindProperty("battleCamera").objectReferenceValue = camera;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            var battlefield = CreateSample("SampleBattlefield", null);
            var stage = CreateStage();
            var separate = CreateSample("SampleSeparateStage", stage);
            AddSampleAnimation(battlefield);
            AddSampleAnimation(separate);
            var spread = CreateTemplate("SpreadAttack", true, false);
            var group = CreateTemplate("GroupAttack", true, false);
            var support = CreateTemplate("SupportAction", true, false);
            var enemyAction = CreateTemplate("EnemyAction", true, false);
            var death = CreateTemplate("DeathFocus", true, true);
            var entrance = CreateTemplate("WaveEntrance", true, true);
            var victory = CreateTemplate("Victory", true, true);
            var mappings = (coordinator.skills ?? Array.Empty<BattleSkillSequence>()).ToList();
            foreach (var side in new[] { Faction.OurSide, Faction.Enemy })
            {
                var available = (side == Faction.OurSide ? manager.allies : manager.enemies) ?? Array.Empty<UnitConfig>();
                foreach (var skill in available.Where(u=>u!=null).SelectMany(u=>u.skills).Where(s=>s!=null).GroupBy(s=>s.id).Select(g=>g.First()))
                {
                    int index=mappings.FindIndex(m=>m.skillId==skill.id && m.casterFaction==side);
                    if(index>=0 && mappings[index].sequence!=battlefield && mappings[index].sequence!=separate)continue;
                    var sequence=side==Faction.Enemy ? enemyAction : skill.kind==SkillKind.Ultimate ? separate
                        : skill.targetSide==TargetSide.OurSide || skill.targetRule==TargetRule.Self ? support
                        : skill.targetRule==TargetRule.AoE ? group : skill.targetRule==TargetRule.Spread ? spread : battlefield;
                    var entry=new BattleSkillSequence{skillId=skill.id,casterFaction=side,sequence=sequence};
                    if(index>=0)mappings[index]=entry;else mappings.Add(entry);
                }
            }
            coordinator.skills=mappings.ToArray();
            var eventMappings=(coordinator.events ?? Array.Empty<BattleEventSequence>()).ToList();
            foreach(var pair in new[]{("BattleStart",entrance),("WaveEnter",entrance),("Spawn",entrance),("Revive",entrance),("Death",death),("Victory",victory),("Defeat",death)})
                if(!eventMappings.Any(e=>e.eventId==pair.Item1))eventMappings.Add(new BattleEventSequence{eventId=pair.Item1,sequence=pair.Item2});
            coordinator.events=eventMappings.ToArray();
            EditorUtility.SetDirty(coordinator); EditorUtility.SetDirty(director);
            foreach (var definition in new[] { battlefield, separate })
            {
                EditorUtility.SetDirty(definition.timeline);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(definition.timeline))) EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Battle presentation configured. Edit DefaultCamera and sample Timeline assets to author shots.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static BattleSequenceDefinition CreateSample(string name, BattleStage stage)
    {
        string path = Root + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(path);
        if (existing != null && existing.timeline != null && existing.timeline.GetOutputTracks().Any()) return existing;
        var timeline = existing != null && existing.timeline != null ? existing.timeline : ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = name; timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = 1.2;
        if (!AssetDatabase.Contains(timeline)) AssetDatabase.CreateAsset(timeline, Root + "/" + name + ".playable");
        var track = timeline.CreateTrack<BattleCameraShotTrack>(null, "Camera");
        var clip = track.CreateClip<BattleCameraShotClip>(); clip.start = 0; clip.duration = 0.65;
        clip.displayName = "Approach";
        var shot = ((BattleCameraShotClip)clip.asset).shot;
        shot.space = BattleShotSpace.Caster;
        shot.fromPosition = new Vector3(1.8f, 1.8f, -3.8f); shot.toPosition = new Vector3(1.2f, 1.5f, -3f);
        shot.pathY = AnimationCurve.EaseInOut(0, 0, 1, 0.1f);
        var second = track.CreateClip<BattleCameraShotClip>(); second.start = 0.65; second.duration = 0.55;
        second.displayName = "Impact cut";
        var impact = ((BattleCameraShotClip)second.asset).shot;
        impact.space = BattleShotSpace.Target;
        impact.fromPosition = new Vector3(2, 1.5f, -4f); impact.toPosition = new Vector3(2.3f, 1.5f, -4.5f);
        impact.shake = new AnimationCurve(new Keyframe(0, 0.08f), new Keyframe(0.4f, 0), new Keyframe(1, 0));
        timeline.CreateMarkerTrack();
        timeline.markerTrack.CreateMarker<BattleImpactMarker>(0.45).effectIndex = 0;
        timeline.markerTrack.CreateMarker<BattleImpactMarker>(0.7).effectIndex = -1;
        // Non-destructive sample animation: explicit full-body clips can be dropped into these tracks later.
        timeline.CreateTrack<AnimationTrack>(null, "Caster");
        timeline.CreateTrack<AnimationTrack>(null, "Target");
        var definition = existing != null ? existing : ScriptableObject.CreateInstance<BattleSequenceDefinition>();
        definition.timeline = timeline; definition.stagePrefab = stage; definition.hideBattleHUD = stage != null;
        if (!AssetDatabase.Contains(definition)) AssetDatabase.CreateAsset(definition, path);
        EditorUtility.SetDirty(definition);
        EditorUtility.SetDirty(timeline);
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(timeline))) EditorUtility.SetDirty(asset);
        return definition;
    }
    private static BattleSequenceDefinition CreateTemplate(string name,bool group,bool eventOnly)
    {
        var existing=AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(Root+"/"+name+".asset");
        if(existing!=null)return existing;
        var definition=CreateSample(name,null);
        foreach(var track in definition.timeline.GetOutputTracks().OfType<BattleCameraShotTrack>())
            foreach(var clip in track.GetClips())
            {
                var shot=((BattleCameraShotClip)clip.asset).shot;
                shot.space=BattleShotSpace.TargetGroup;shot.fitTargetGroup=group;
                shot.fromPosition=new Vector3(2,2.2f,-6);shot.toPosition=new Vector3(2,2,-5.8f);
                if(eventOnly)shot.shake=AnimationCurve.Linear(0,0,1,0);
                EditorUtility.SetDirty(clip.asset);
            }
        if(eventOnly)
        {
            foreach(var marker in definition.timeline.markerTrack.GetMarkers().ToArray())definition.timeline.markerTrack.DeleteMarker(marker);
            definition.timeline.fixedDuration=.7;
        }
        foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(definition.timeline)))EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(definition.timeline);return definition;
    }
    private static BattleStage CreateStage()
    {
        const string path = Root + "/SampleStage.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<BattleStage>();
        var root = new GameObject("SampleStage");
        try
        {
            var stage = root.AddComponent<BattleStage>();
            stage.casterAnchor = new GameObject("CasterAnchor").transform;
            stage.casterAnchor.SetParent(root.transform, false);
            stage.targetAnchor = new GameObject("TargetAnchor").transform;
            stage.targetAnchor.SetParent(root.transform, false); stage.targetAnchor.localPosition = new Vector3(0, 0, 5);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "StageFloor"; floor.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var light = new GameObject("StageLight").AddComponent<Light>();
            light.transform.SetParent(root.transform, false); light.type = LightType.Directional;
            light.transform.localRotation = Quaternion.Euler(45, -30, 0); light.intensity = 1.5f;
            BattleStage.SetLayer(root.transform, stage.stageLayer);
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BattleStage>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void AddSampleAnimation(BattleSequenceDefinition definition)
    {
        var track = definition.timeline.GetOutputTracks().OfType<AnimationTrack>().FirstOrDefault(t => t.name == "Caster");
        if (track == null || track.GetClips().Any()) return;
        const string path = Root + "/SampleCastMotion.anim";
        var animation = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (animation == null)
        {
            animation = new AnimationClip { name = "SampleCastMotion", frameRate = 30 };
            animation.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x",
                new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, -8f), new Keyframe(0.7f, 5f), new Keyframe(1.2f, 0)));
            AssetDatabase.CreateAsset(animation, path);
        }
        track.trackOffset = TrackOffset.ApplySceneOffsets;
        var clip = track.CreateClip(animation); clip.start = 0; clip.duration = 1.2;
        clip.displayName = "Placeholder cast motion - replace with character clip";
        EditorUtility.SetDirty(track); EditorUtility.SetDirty(definition.timeline);
    }
}
