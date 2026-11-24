using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Battle.Logic;
using Battle.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

[CustomEditor(typeof(BattleSequenceDefinition))]
public sealed class BattleSequenceDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("配置静态表现；原角色与目标在运行时绑定。近战到位后启动主 Timeline；接近播速只影响接近状态。", MessageType.Info);
        Field("template"); Field("timeline"); Field("animation"); Field("casterTrack"); Field("targetTrack");
        var definition = (BattleSequenceDefinition)target;
        if (definition.Template == BattleSkillTemplate.Staged && definition.StageSpace == BattleStageSpace.IsolatedSet && definition.stagePrefab == null)
            EditorGUILayout.HelpBox("IsolatedSet 需要指定 BattleStage prefab。", MessageType.Error);
        if (definition.timeline == null && definition.Animation.Driver == BattleBodyDriver.Timeline)
            EditorGUILayout.HelpBox("未指定 Timeline，使用配置中的攻击时长与生效时间回退。", MessageType.Warning);
        if (definition.Template == BattleSkillTemplate.MeleeApproach)
        { Field("standOffset"); Field("standEuler"); Field("approach"); Field("returning"); Field("returnMode"); }
        if (definition.Template == BattleSkillTemplate.Ranged) Field("projectile");
        if (definition.Template == BattleSkillTemplate.Staged)
        {
            Field("stagePrefab"); Field("stageSpace"); Field("stageOrigin"); Field("stageEuler");
            Field("isolatedSetOffset");
            Field("casterPosition"); Field("casterEuler"); Field("targetPosition"); Field("targetEuler"); Field("additionalTargetSpacing");
            EditorGUILayout.HelpBox("指定 stagePrefab 时使用隔离布景，移动原表现对象。布景挂点优先于下方局部位置。", MessageType.None);
        }
        Field("impactPrefab"); Field("impactAnchor"); Field("impactOffset"); Field("effectLifetime"); Field("hideCommands"); Field("timeoutSeconds");
        serializedObject.ApplyModifiedProperties();
        if (AssetDatabase.Contains(target))
        {
            if (GUILayout.Button("打开技能表现预览")) BattleSkillStudio.Open(definition);
            if (GUILayout.Button("保存配置资产")) { EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target); }
        }
    }
    private void Field(string name) { var property = serializedObject.FindProperty(name); if (property != null) EditorGUILayout.PropertyField(property, true); }
}

[CustomEditor(typeof(BattleActorPresentation))]
public sealed class BattleActorPresentationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("补齐默认挂点（已有引用不覆盖）"))
        {
            var component = (BattleActorPresentation)target; var so = new SerializedObject(component);
            var stand = Anchor(so, component.transform, "meleeStand", "MeleeStand", new Vector3(0, 0, 1.7f), new Vector3(0, 180, 0));
            var hit = Anchor(so, component.transform, "hit", "Hit", Vector3.up, Vector3.zero);
            var foot = Anchor(so, component.transform, "foot", "Foot", Vector3.zero, Vector3.zero);
            var cast = Anchor(so, component.transform, "cast", "Cast", new Vector3(0, 1, .5f), Vector3.zero);
            Undo.RecordObject(component, "Configure battle anchors"); component.ConfigureAnchors(stand, hit, foot, cast); EditorUtility.SetDirty(component);
        }
    }
    internal static Transform Anchor(SerializedObject so, Transform root, string field, string name, Vector3 position, Vector3 euler)
    {
        var existing = so.FindProperty(field).objectReferenceValue as Transform; if (existing != null) return existing;
        var node = new GameObject(name).transform; Undo.RegisterCreatedObjectUndo(node.gameObject, "Create battle anchor"); node.SetParent(root, false); node.localPosition = position; node.localEulerAngles = euler; return node;
    }
    private void OnSceneGUI()
    {
        var component = (BattleActorPresentation)target;
        foreach (string field in new[] { "meleeStand", "hit", "foot", "cast" })
        {
            var anchor = serializedObject.FindProperty(field).objectReferenceValue as Transform; if (anchor == null) continue;
            Handles.Label(anchor.position, field); EditorGUI.BeginChangeCheck();
            Vector3 position = Handles.PositionHandle(anchor.position, anchor.rotation); Quaternion rotation = Handles.RotationHandle(anchor.rotation, position);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(anchor, "Adjust battle anchor"); anchor.SetPositionAndRotation(position, rotation); }
        }
    }
}

public sealed class BattleSkillStudio : EditorWindow
{
    private sealed class Lookup : IBattleActorLookup
    {
        public readonly Dictionary<BattleUnit, BattleUnitActor> Actors = new();
        public bool TryGetActor(BattleUnit unit, out BattleUnitActor actor) { actor = null; return unit != null && Actors.TryGetValue(unit, out actor) && actor != null; }
    }
    [SerializeField] private BattleSequenceDefinition source;
    [SerializeField] private GameObject casterPrefab, targetPrefab;
    [SerializeField] private float distance = 8;
    [SerializeField] private int allySlot, targetSlot, targetCount = 3;
    [SerializeField] private TargetRule previewRule = TargetRule.Single;
    private BattleSequenceDefinition draft;
    private Editor draftEditor;
    private Scene previewScene;
    private PreviewRenderUtility preview;
    private GameObject root;
    private Camera camera;
    private RenderTexture texture;
    private BattlePresentationCoordinator coordinator;
    private SkillConfig skill;
    private Lookup lookup;
    private bool playing, paused;
    private float elapsed, seek;
    private double lastTick;
    private Vector2 scroll;
    [MenuItem("Tools/Battle/Presentation/Skill Studio")]
    public static void OpenMenu() => Open(null);
    public static void Open(BattleSequenceDefinition definition)
    { var window = GetWindow<BattleSkillStudio>("技能表现预览"); window.minSize = new Vector2(700, 700); if (definition != null) window.Load(definition); window.Show(); }
    private void OnEnable() { EditorApplication.update += UpdatePreview; EditorApplication.playModeStateChanged += PlayModeChanged; lastTick = EditorApplication.timeSinceStartup; if (source != null && !EditorApplication.isPlayingOrWillChangePlaymode) Load(source, true, false); }
    private void OnDisable() { EditorApplication.update -= UpdatePreview; EditorApplication.playModeStateChanged -= PlayModeChanged; Cleanup(); SaveDraft(); if (draftEditor != null) DestroyImmediate(draftEditor); if (draft != null) DestroyImmediate(draft); }
    private void PlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) { SaveDraft(); Cleanup(); } }
    private string DraftPath => source != null ? "Library/BattlePresentationDrafts/Skill_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) + ".json" : null;
    private void SaveDraft() { if (draft == null || DraftPath == null) return; Directory.CreateDirectory("Library/BattlePresentationDrafts"); File.WriteAllText(DraftPath, JsonUtility.ToJson(draft)); }
    private void Load(BattleSequenceDefinition value, bool restoreDraft = true, bool build = true)
    {
        SaveDraft(); Cleanup(); if (draftEditor != null) DestroyImmediate(draftEditor); if (draft != null) DestroyImmediate(draft);
        bool changedSource = source != value;
        source = value; if (source == null) return;
        if (changedSource) previewRule = source.Template == BattleSkillTemplate.Support ? TargetRule.AoE : TargetRule.Single;
        draft = Instantiate(source); draft.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
        if (restoreDraft && File.Exists(DraftPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(DraftPath), draft);
        if (casterPrefab == null) casterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleSkillSamples.Folder + "/Caster.prefab");
        if (targetPrefab == null) targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleSkillSamples.Folder + "/Target.prefab");
        draftEditor = Editor.CreateEditor(draft); if (build) Rebuild();
    }
    private void Cleanup()
    {
        playing = false; paused = false; if (coordinator != null) coordinator.Cancel();
        if (preview != null) { preview.Cleanup(); preview = null; }
        else if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        previewScene = default;
        if (skill != null) DestroyImmediate(skill);
        if (texture != null) { texture.Release(); DestroyImmediate(texture); }
        coordinator = null; root = null; camera = null; texture = null; skill = null;
    }
    private BattleUnitActor Spawn(GameObject prefab, BattleUnit unit, Vector3 position)
    {
        var identity = new GameObject(unit.displayName); identity.transform.SetParent(root.transform, false); identity.transform.position = position;
        var movement = new GameObject("PresentationRoot").transform; movement.SetParent(identity.transform, false);
        var model = prefab != null ? Instantiate(prefab, movement) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        if (prefab == null) { model.transform.SetParent(movement, false); model.transform.localPosition = Vector3.up; }
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is not BattleUnitActor && behaviour is not BattleActorPresentation) behaviour.enabled = false;
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var actor = model.GetComponent<BattleUnitActor>(); if (actor == null) actor = model.AddComponent<BattleUnitActor>(); actor.ConfigurePresentationRoot(movement, actor.VisualRoot); actor.Bind(unit); lookup.Actors.Add(unit, actor); return actor;
    }
    private void Rebuild()
    {
        Cleanup(); if (draft == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        preview = new PreviewRenderUtility(); root = new GameObject("Skill Preview (no player data)"); preview.AddSingleGO(root); previewScene = root.scene;
        lookup = new Lookup(); var allies = new List<BattleUnit>(); var enemies = new List<BattleUnit>();
        for (int i = 0; i < 4; i++) allies.Add(new BattleUnit { displayName = "Ally " + (i + 1), faction = Faction.OurSide, pos = i, maxHealth = 1000, currentHealth = 600, attack = 100, speed = i == allySlot ? 200 : 10, maxEnergy = 100 });
        for (int i = 0; i < targetCount; i++) enemies.Add(new BattleUnit { displayName = "Enemy " + (i + 1), faction = Faction.Enemy, pos = i, maxHealth = 1000, currentHealth = 1000, speed = 10 });
        skill = CreateInstance<SkillConfig>(); skill.id = -809; skill.Name = "Skill Preview"; skill.targetSide = draft.Template == BattleSkillTemplate.Support ? TargetSide.OurSide : TargetSide.OtherSide;
        skill.targetRule = previewRule; skill.effectConfigs = new List<EffectConfig> { new() { effectType = draft.Template == BattleSkillTemplate.Support ? EffectType.Heal : EffectType.Damage, scaleStat = StatType.Attack, value = 1 } };
        allies[allySlot].skills.Add(skill);
        var units = allies.Concat(enemies).ToArray(); var flow = new BattleFlow(units); flow.Ready();
        var field = root.AddComponent<BattlefieldDirector>();
        var layout = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(BattlePresentationSetup.CameraRoot + "/DefaultLayout.asset");
        field.Configure(layout, root.transform);
        foreach (var unit in units) { var actor = Spawn(unit.faction == Faction.OurSide ? casterPrefab : targetPrefab, unit, Vector3.zero); field.Register(actor, unit.faction == Faction.OurSide ? 4 : targetCount); }
        foreach (var enemy in enemies) lookup.Actors[enemy].PresentationRoot.position += Vector3.forward * (distance - 8);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.SetParent(root.transform); floor.transform.localScale = Vector3.one * 5;
        var light = new GameObject("Preview light"); light.transform.SetParent(root.transform); light.transform.rotation = Quaternion.Euler(45, -25, 0); var lamp = light.AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 1.3f;
        coordinator = root.AddComponent<BattlePresentationCoordinator>(); coordinator.timelineDirector = root.AddComponent<PlayableDirector>(); coordinator.timelineDirector.playOnAwake = false;
        var shots = root.AddComponent<BattleShotDirector>(); coordinator.cameraDirector = shots; coordinator.ConfigureBattlefield(field); shots.ConfigureBattlefield(field);
        camera = preview.camera; var output = camera.gameObject; camera.enabled = false;
        camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(previewScene);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .14f, .18f);
        texture = new RenderTexture(960, 540, 24); camera.aspect = 16f / 9f; camera.nearClipPlane = .05f; camera.farClipPlane = 3000;
        shots.outputCamera = camera; shots.brain = output.AddComponent<CinemachineBrain>();
        shots.normalCamera = new GameObject("Normal").AddComponent<CinemachineCamera>(); shots.normalCamera.transform.SetParent(root.transform);
        shots.cinematicCamera = new GameObject("Sequence").AddComponent<CinemachineCamera>(); shots.cinematicCamera.transform.SetParent(root.transform);
        shots.profile = AssetDatabase.LoadAssetAtPath<BattleCameraProfile>(BattlePresentationSetup.CameraRoot + "/DefaultCamera.asset");
        coordinator.Bind(flow, lookup); coordinator.skills = new[] { new BattleSkillSequence { skillId = skill.id, casterFaction = Faction.OurSide, sequence = draft } };
        var target = skill.targetSide == TargetSide.OurSide ? allies[Mathf.Min(targetSlot, 3)] : enemies[Mathf.Min(targetSlot, enemies.Count - 1)];
        var result = flow.TryExecute(new BattleAction { caster = allies[allySlot], skill = skill, mainTarget = target });
        if (!result.Succeeded) throw new InvalidOperationException(result.Validation.Message);
        coordinator.Present(result.Resolution, () => { playing = false; paused = false; }); elapsed = 0; Step(.0001f);
    }
    private void Step(float delta)
    {
        if (coordinator == null) return; coordinator.Tick(delta); elapsed += delta; coordinator.cameraDirector.FlushOutput();
        // Editor seeking can evaluate many ticks in one Unity frame; the brain deduplicates that frame.
        // Render the actual solved virtual-camera pose so seeking and export match the runtime result.
        var shot = coordinator.cameraDirector.HasExclusiveOwner ? coordinator.cameraDirector.cinematicCamera : coordinator.cameraDirector.normalCamera;
        camera.transform.SetPositionAndRotation(shot.transform.position, shot.transform.rotation); camera.fieldOfView = shot.Lens.FieldOfView; RenderPreview();
    }
    private void RenderPreview()
    {
        preview.BeginPreview(new Rect(0, 0, 960, 540), GUIStyle.none); preview.Render(true); var rendered = preview.EndPreview(); Graphics.Blit(rendered, texture);
    }
    private void UpdatePreview()
    {
        double now = EditorApplication.timeSinceStartup; float delta = Mathf.Min(.05f, (float)(now - lastTick)); lastTick = now;
        if (!playing || coordinator == null) return; Step(delta); Repaint();
    }
    private void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorGUILayout.HelpBox("完整技能隔离预览在编辑模式使用。运行时请在 Coordinator 查看实际阶段或跳过技能。", MessageType.Info); return; }
        var value = (BattleSequenceDefinition)EditorGUILayout.ObjectField("技能表现", source, typeof(BattleSequenceDefinition), false); if (value != source) Load(value);
        if (draft == null) return;
        EditorGUI.BeginChangeCheck(); casterPrefab = (GameObject)EditorGUILayout.ObjectField("攻击者模型", casterPrefab, typeof(GameObject), false); targetPrefab = (GameObject)EditorGUILayout.ObjectField("目标模型", targetPrefab, typeof(GameObject), false);
        distance = EditorGUILayout.Slider("双方参考距离", distance, 2, 20); allySlot = EditorGUILayout.IntSlider("行动者槽位（0起）", allySlot, 0, 3); targetCount = EditorGUILayout.IntSlider("敌人数", targetCount, 1, 5);
        targetSlot = EditorGUILayout.IntSlider("主目标槽位（0起）", targetSlot, 0, draft.Template == BattleSkillTemplate.Support ? 3 : targetCount - 1);
        previewRule = (TargetRule)EditorGUILayout.EnumPopup("测试目标规则", previewRule);
        if (EditorGUI.EndChangeCheck()) Rebuild();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(coordinator == null || !coordinator.Busy || (!playing && !paused)))
                if (GUILayout.Button(paused ? "继续" : "暂停")) { playing = !playing; paused = !playing; }
            if (GUILayout.Button("重播")) { Rebuild(); playing = true; }
            if (GUILayout.Button("单帧")) { playing = false; Step(1f / 60); paused = coordinator != null && coordinator.Busy; }
            if (GUILayout.Button("跳过")) { coordinator?.SkipCurrent(); playing = false; paused = false; Step(.0001f); }
            if (GUILayout.Button("保存到配置资产"))
            { Undo.RecordObject(source, "Save skill draft"); string name = source.name; EditorUtility.CopySerialized(draft, source); source.name = name; source.hideFlags = HideFlags.None; EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source); SaveDraft(); }
            if (GUILayout.Button("从资产重新载入")) Load(source, false);
        }
        seek = EditorGUILayout.Slider("重新求值到秒数", seek, 0, 15);
        if (GUILayout.Button("跳到此时间（重新构建，不累计位移）")) { float t = seek; Rebuild(); playing = false; while (elapsed < t && coordinator.Busy) Step(Mathf.Min(1f / 60, t - elapsed)); paused = coordinator.Busy; }
        if (texture != null)
        {
            var rect = GUILayoutUtility.GetAspectRect(16f / 9); GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            foreach (var pair in lookup.Actors)
            {
                if (pair.Key.faction != Faction.Enemy || !pair.Value.WorldPresentationVisible || !BattleDisplayState.Visible(pair.Key)) continue;
                var point = camera.WorldToViewportPoint(pair.Value.HealthBarPosition); if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
                GUI.Label(new Rect(rect.x + point.x * rect.width - 55, rect.y + (1 - point.y) * rect.height - 10, 120, 22), $"{pair.Key.displayName} {BattleDisplayState.Health(pair.Key)}");
            }
            if (draft.Template == BattleSkillTemplate.MeleeApproach && coordinator.Session?.Resolution?.Record is BattleActionRecord record
                && lookup.TryGetActor(record.MainTarget, out var targetActor) && lookup.TryGetActor(record.Caster, out var casterActor) && targetActor.Presentation?.MeleeStand != null)
            {
                var anchor = targetActor.Presentation.MeleeStand;
                Vector3 stand = anchor.position + anchor.rotation * draft.StandOffset;
                var a = camera.WorldToViewportPoint(casterActor.PresentationRoot.position); var b = camera.WorldToViewportPoint(stand);
                if (a.z > 0 && b.z > 0)
                {
                    Vector2 Pixel(Vector3 p) => new(rect.x + p.x * rect.width, rect.y + (1 - p.y) * rect.height);
                    Handles.BeginGUI(); Handles.color = Color.cyan; Handles.DrawAAPolyLine(2, Pixel(a), Pixel(b)); Handles.EndGUI();
                    GUI.Label(new Rect(Pixel(b).x, Pixel(b).y, 160, 20), "攻击站位 + 技能偏移");
                }
            }
        }
        EditorGUILayout.LabelField($"阶段：{coordinator?.CurrentPhase}  行动时间：{elapsed:F2}s  Timeline：{coordinator?.timelineDirector.time:F2}s");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUI.BeginChangeCheck(); draftEditor?.OnInspectorGUI(); if (EditorGUI.EndChangeCheck()) { SaveDraft(); Rebuild(); }
        if (coordinator?.Session != null) foreach (var line in coordinator.Session.EventLog.TakeLast(10)) EditorGUILayout.LabelField(line);
        EditorGUILayout.EndScrollView();
    }
    public void Export(string path) { if (camera == null) Rebuild(); RenderPreview(); var old = RenderTexture.active; RenderTexture.active = texture; var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); DestroyImmediate(image); RenderTexture.active = old; }
    public void ExportSequence(string folder)
    {
        Directory.CreateDirectory(folder); Load(source, false); playing = false;
        foreach (float time in new[] { 0f, .2f, .5f, .85f, 1.2f, 1.7f, 2.4f })
        { while (elapsed < time && coordinator.Busy) Step(Mathf.Min(1f / 60, time - elapsed)); Export(Path.Combine(folder, time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".png")); }
    }
}

[CustomEditor(typeof(BattlePresentationCoordinator))]
public sealed class BattlePresentationCoordinatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var coordinator = (BattlePresentationCoordinator)target;
        EditorGUILayout.LabelField("当前表现阶段", coordinator.CurrentPhase);
        if (coordinator.Session != null)
        {
            EditorGUILayout.LabelField("行动时间", coordinator.Session.Elapsed.ToString("F3"));
            EditorGUILayout.LabelField("移动所有者", "当前技能会话");
            EditorGUILayout.LabelField("相机所有者", coordinator.cameraDirector != null && coordinator.cameraDirector.HasExclusiveOwner ? "当前技能会话" : "常态镜头");
            foreach (var line in coordinator.Session.EventLog.TakeLast(8)) EditorGUILayout.LabelField(line);
            if (GUILayout.Button("跳过本次技能（继续战斗）")) coordinator.SkipCurrent();
            Repaint();
        }
    }
}
