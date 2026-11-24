using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Battle.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

/// <summary>One authoring surface; persistent drafts are separate from both assets and live battle state.</summary>
public sealed class BattlePresentationStudio : EditorWindow
{
    private const string Folder = "Library/BattlePresentationDrafts";
    // HideAndDontSave also contains NotEditable, which disables SerializedProperty fields.
    private const HideFlags DraftFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
    private BattleCameraProfile source, draft;
    private BattlefieldLayout layoutSource, layoutDraft, originalLiveLayout;
    private BattleCameraShotClip shotSource, shotDraft;
    private string profileBaseline, layoutBaseline, shotBaseline;
    private BattleShotDirector director;
    private BattlePresentationClock pausedClock;
    private bool previousPause, selectedState = true, handles = true;
    private PreviewRenderUtility preview;
    private GameObject previewRoot;
    private BattlefieldDirector previewField;
    private BattleShotDirector previewDirector;
    private readonly List<BattleUnit> units = new();
    private readonly Lookup lookup = new();
    private int actorIndex, targetIndex, enemyCount = 3, tab;
    private bool boss;
    private BattleShotMode mode;
    private Vector2 scroll;
    private float aspect = 16f/9f, sampleTime, sampleDuration = 1.2f;
    private BattleCameraPose lastPose;
    private GameObject allyModel, enemyModel;
    private sealed class Lookup : IBattleActorLookup
    {
        internal readonly Dictionary<BattleUnit, BattleUnitActor> map = new();
        public bool TryGetActor(BattleUnit unit, out BattleUnitActor actor) { actor = null; return unit != null && map.TryGetValue(unit, out actor) && actor != null; }
    }
    [Serializable] private sealed class DraftEnvelope { public string baseline; public string json; }
    [MenuItem("Tools/Battle/Presentation/Presentation Studio")]
    public static void Open()
    {
        var window = GetWindow<BattlePresentationStudio>("战斗表现工作台");
        window.minSize = new Vector2(620, 600);
        if(!window.docked){var main=EditorGUIUtility.GetMainWindowPosition();window.position=new Rect(main.x+30,main.y+45,Mathf.Min(780,main.width-60),Mathf.Max(600,Mathf.Min(900,main.height-90)));}
        window.Show(); window.Focus();
    }
    private void OnEnable()
    {
        source = AssetDatabase.LoadAssetAtPath<BattleCameraProfile>(SessionState.GetString("BattleStudio.Profile", BattlePresentationSetup.CameraRoot + "/DefaultCamera.asset"));
        layoutSource = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(SessionState.GetString("BattleStudio.Layout", BattlePresentationSetup.CameraRoot + "/DefaultLayout.asset"));
        if (source == null) source = AssetDatabase.LoadAssetAtPath<BattleCameraProfile>(BattlePresentationSetup.CameraRoot + "/DefaultCamera.asset");
        if (layoutSource == null) layoutSource = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(BattlePresentationSetup.CameraRoot + "/DefaultLayout.asset");
        draft = CreateInstance<BattleCameraProfile>(); layoutDraft = CreateInstance<BattlefieldLayout>();
        LoadDraft(source, draft, out profileBaseline); LoadDraft(layoutSource, layoutDraft, out layoutBaseline);
        if (draft.Shots.Length == 0) draft.InitializeShots();
        if (layoutDraft.Allies.Length == 0) layoutDraft.InitializeDefaults();
        SceneView.duringSceneGui += SceneGUI;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        Undo.undoRedoPerformed += Changed;
        var configs = AssetDatabase.FindAssets("t:UnitConfig").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UnitConfig>).Where(x => x != null && x.prefab != null).ToArray();
        allyModel = configs.FirstOrDefault(x => AssetDatabase.GetAssetPath(x).Contains("Allies"))?.prefab;
        enemyModel = configs.FirstOrDefault(x => AssetDatabase.GetAssetPath(x).Contains("Enemies"))?.prefab;
        RebuildPreview();
    }
    private static string DraftPath(Object asset)
    { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id); return Folder + "/" + guid + "-" + id + ".json"; }
    private static void LoadDraft(Object asset, Object value, out string baseline)
    {
        if (asset != null) EditorUtility.CopySerialized(asset, value);
        baseline = JsonUtility.ToJson(value);
        if (asset != null && File.Exists(DraftPath(asset)))
        {
            var stored = JsonUtility.FromJson<DraftEnvelope>(File.ReadAllText(DraftPath(asset)));
            // A saved, unchanged draft must not mask later Inspector edits to the asset.
            if (stored.json != stored.baseline)
            { JsonUtility.FromJsonOverwrite(stored.json, value); baseline = stored.baseline; }
        }
        value.hideFlags = DraftFlags;
    }
    internal static bool SyncCleanDraft(Object asset, Object value, ref string baseline)
    {
        if (asset == null || value == null) return false;
        string current = JsonUtility.ToJson(asset);
        if (current == baseline || JsonUtility.ToJson(value) != baseline) return false;
        EditorUtility.CopySerialized(asset, value);
        value.hideFlags = DraftFlags;
        baseline = current;
        return true;
    }
    private void OnInspectorUpdate()
    {
        bool changed = SyncCleanDraft(source, draft, ref profileBaseline);
        changed |= SyncCleanDraft(layoutSource, layoutDraft, ref layoutBaseline);
        changed |= SyncCleanDraft(shotSource, shotDraft, ref shotBaseline);
        if (changed) Changed();
        else Repaint();
    }
    public static void OpenForProfile(BattleCameraProfile profile)
    {
        Open();
        var window = GetWindow<BattlePresentationStudio>();
        if (window.director != null) window.EndLive();
        window.Remember();
        window.source = profile;
        LoadDraft(profile, window.draft, out window.profileBaseline);
        if (window.draft.Shots.Length == 0) window.draft.InitializeShots();
        window.Changed();
    }
    private static void Persist(Object asset, Object value, string baseline)
    {
        if (asset == null || value == null) return;
        Directory.CreateDirectory(Folder);
        File.WriteAllText(DraftPath(asset), JsonUtility.ToJson(new DraftEnvelope { baseline = baseline, json = JsonUtility.ToJson(value) }, true));
    }
    private void Remember()
    {
        Persist(source, draft, profileBaseline); Persist(layoutSource, layoutDraft, layoutBaseline); Persist(shotSource, shotDraft, shotBaseline);
        if (source != null) SessionState.SetString("BattleStudio.Profile", AssetDatabase.GetAssetPath(source));
        if (layoutSource != null) SessionState.SetString("BattleStudio.Layout", AssetDatabase.GetAssetPath(layoutSource));
    }
    private void OnDisable()
    {
        Remember(); EndLive(); CleanupPreview();
        SceneView.duringSceneGui -= SceneGUI; EditorApplication.playModeStateChanged -= PlayModeChanged; Undo.undoRedoPerformed -= Changed;
        if (draft != null) DestroyImmediate(draft); if (layoutDraft != null) DestroyImmediate(layoutDraft); if (shotDraft != null) DestroyImmediate(shotDraft);
    }
    private void PlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) { Remember(); EndLive(); } Repaint(); }
    private void Changed() { Remember(); ApplyPreview(); Repaint(); SceneView.RepaintAll(); }
    private void BeginLive()
    {
        var manager = BattleManager.Instance;
        if (manager == null || !manager.CanAcceptCommands || manager.Battlefield == null) return;
        director = manager.CameraController; pausedClock = manager.Presentation.Clock;
        previousPause = pausedClock.Paused; pausedClock.Paused = true;
        originalLiveLayout = manager.Battlefield.Layout;
        actorIndex = Mathf.Max(0, LiveUnits(Faction.OurSide).IndexOf(manager.Flow.CurrentUnit)); targetIndex = 0;
        ApplyPreview();
    }
    private void EndLive()
    {
        if (director != null)
        {
            var manager = BattleManager.Instance;
            if (manager != null && originalLiveLayout != null)
                manager.Battlefield.ApplyLayout(originalLiveLayout, manager.EnemyCapacity, manager.FormationPreset, manager.Selection?.Actor);
            director.EndConfigurationPreview();
        }
        if (pausedClock != null) pausedClock.Paused = previousPause;
        pausedClock = null; director = null; originalLiveLayout = null;
    }
    private List<BattleUnit> LiveUnits(Faction side) => BattleManager.Instance.Flow.Units.Where(u => u.faction == side && u.IsActive).ToList();
    private BattleCameraContext MakeContext(bool live)
    {
        var allies = live ? LiveUnits(Faction.OurSide) : units.Where(u => u.faction == Faction.OurSide).ToList();
        var enemies = live ? LiveUnits(Faction.Enemy) : units.Where(u => u.faction == Faction.Enemy).ToList();
        var actor = allies.ElementAtOrDefault(Mathf.Clamp(actorIndex, 0, allies.Count-1));
        var candidates = mode is BattleShotMode.AllySupport or BattleShotMode.Self ? allies : enemies;
        var target = mode == BattleShotMode.Self ? actor : candidates.ElementAtOrDefault(Mathf.Clamp(targetIndex, 0, candidates.Count-1));
        var affected = mode is BattleShotMode.EnemyGroup or BattleShotMode.Overview or BattleShotMode.AllySupport ? candidates
            : mode == BattleShotMode.EnemySpread ? candidates.Where(u => target != null && Mathf.Abs(u.pos-target.pos) <= 1).ToList()
            : target != null ? new List<BattleUnit> { target } : new List<BattleUnit>();
        return new BattleCameraContext(actor, selectedState ? target : null, affected, mode);
    }
    private void ApplyPreview()
    {
        if (director != null)
        {
            var manager = BattleManager.Instance; var context = MakeContext(true);
            manager.Battlefield.ApplyLayout(layoutDraft, manager.EnemyCapacity, manager.FormationPreset, context.Actor);
            director.PreviewContext(draft, context); return;
        }
        if (previewDirector == null) return;
        var selected = MakeContext(false);
        previewField.ApplyLayout(layoutDraft, enemyCount, boss ? "Boss" : null, selected.Actor);
        previewDirector.PreviewContext(draft, selected);
        var cam = previewDirector.normalCamera;
        lastPose = new BattleCameraPose(cam.transform.position, cam.transform.rotation, cam.Lens.FieldOfView);
        if (tab == 2 && shotDraft != null)
        {
            previewDirector.SequenceCaster = lookup.TryGetActor(selected.Actor, out var caster) ? caster.VisualRoot : null;
            previewDirector.SequenceTarget = lookup.TryGetActor(selected.Target, out var target) ? target.VisualRoot : null;
            shotDraft.shot.ResetReference();
            lastPose = shotDraft.shot.Evaluate(previewDirector, sampleTime/Mathf.Max(.01f,sampleDuration), sampleTime);
        }
        preview.camera.transform.SetPositionAndRotation(lastPose.Position, lastPose.Rotation);
        preview.camera.fieldOfView = lastPose.Fov; preview.camera.aspect = aspect;
    }
    public void ReloadFromAssets()
    {
        if(source!=null) EditorUtility.CopySerialized(source,draft);
        if(layoutSource!=null) EditorUtility.CopySerialized(layoutSource,layoutDraft);
        profileBaseline=JsonUtility.ToJson(source); layoutBaseline=JsonUtility.ToJson(layoutSource);
        draft.hideFlags=layoutDraft.hideFlags=DraftFlags; Changed();
    }
    public void ExportPreview(string path)
    {
        ApplyPreview();
        var rect = new Rect(0,0,1280,720);
        preview.BeginPreview(rect,GUIStyle.none); preview.Render(true);
        var texture = preview.EndPreview();
        var previous = RenderTexture.active;
        var temp = RenderTexture.GetTemporary(1280,720,0);
        var output = new Texture2D(1280,720,TextureFormat.RGB24,false);
        try { Graphics.Blit(texture,temp); RenderTexture.active=temp;output.ReadPixels(new Rect(0,0,1280,720),0,0);output.Apply();File.WriteAllBytes(path,output.EncodeToPNG()); }
        finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temp);DestroyImmediate(output); }
    }
    private void CleanupPreview() { if (previewDirector != null) previewDirector.Cancel(); preview?.Cleanup(); preview = null; previewRoot = null; previewDirector = null; units.Clear(); lookup.map.Clear(); }
    private void RebuildPreview()
    {
        CleanupPreview();
        preview = new PreviewRenderUtility(); preview.camera.nearClipPlane = .05f; preview.camera.farClipPlane = 200;
        preview.camera.backgroundColor = new Color(.07f,.08f,.11f); preview.camera.clearFlags = CameraClearFlags.Color;
        preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(45,-25,0); preview.lights[1].intensity = .8f;
        previewRoot = new GameObject("Battle Studio Preview"); preview.AddSingleGO(previewRoot);
        previewField = previewRoot.AddComponent<BattlefieldDirector>(); previewField.Configure(layoutDraft, previewRoot.transform);
        previewDirector = previewRoot.AddComponent<BattleShotDirector>();
        var cam = new GameObject("Preview shot"); cam.transform.SetParent(previewRoot.transform,false);
        previewDirector.normalCamera = cam.AddComponent<CinemachineCamera>(); previewDirector.normalCamera.enabled = false;
        previewDirector.profile = draft; previewDirector.outputCamera = preview.camera; previewDirector.ConfigureBattlefield(previewField);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.SetParent(previewRoot.transform,false); floor.transform.localScale = new Vector3(3,1,3); DestroyImmediate(floor.GetComponent<Collider>());
        for (int i = 0; i < 4+enemyCount; i++)
        {
            bool ally = i < 4; int index = ally ? i : i-4;
            var identity = new GameObject((ally ? "Ally " : "Enemy ") + (index+1)); identity.transform.SetParent(previewRoot.transform,false);
            var root = new GameObject(identity.name + " visual"); root.transform.SetParent(identity.transform,false);
            var prefab = ally ? allyModel : enemyModel;
            GameObject model = prefab != null ? BattleStage.CloneVisual(prefab.transform, root.transform) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            if (prefab == null) { model.transform.SetParent(root.transform,false); model.transform.localPosition = Vector3.up; DestroyImmediate(model.GetComponent<Collider>()); }
            if (!ally && boss) model.transform.localScale *= 2.5f;
            var actor = model.AddComponent<BattleUnitActor>(); actor.ConfigurePresentationRoot(root.transform, model.transform);
            var unit = new BattleUnit { displayName = root.name, faction = ally ? Faction.OurSide : Faction.Enemy, pos = index, maxHealth = 100, currentHealth = 100 };
            actor.Bind(unit); units.Add(unit); lookup.map.Add(unit, actor); previewField.Register(actor, ally ? 4 : enemyCount, boss ? "Boss" : null);
        }
        previewDirector.Bind(lookup, units, new BattlePresentationClock()); ApplyPreview();
    }
    private void OnGUI()
    {
        if (draft == null) return;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("草稿即时预览；保存按钮才写入资产。运行结束或重启编辑器后，Library 中的草稿仍可恢复。镜头演出在独立预览中拖动时间，不会触发伤害。", MessageType.Info);
        if ((source != null && JsonUtility.ToJson(source) != profileBaseline) ||
            (layoutSource != null && JsonUtility.ToJson(layoutSource) != layoutBaseline))
        {
            EditorGUILayout.HelpBox("Config 资产已改变。若草稿也有未保存修改，将保留草稿；点击下方按钮可放弃相机和阵型草稿，采用 Config 中的数值。", MessageType.Warning);
            if (GUILayout.Button("采用 Config 数值（重新载入相机和阵型）") &&
                EditorUtility.DisplayDialog("采用 Config 数值", "放弃当前相机和阵型草稿，重新载入配置资产？", "重新载入", "取消")) ReloadFromAssets();
        }
        using (new EditorGUI.DisabledScope(director != null))
        {
            var next = (BattleCameraProfile)EditorGUILayout.ObjectField("相机配置", source, typeof(BattleCameraProfile), false);
            var nextLayout = (BattlefieldLayout)EditorGUILayout.ObjectField("阵型配置", layoutSource, typeof(BattlefieldLayout), false);
            if (next != source || nextLayout != layoutSource)
            {
                Remember(); source = next; layoutSource = nextLayout;
                LoadDraft(source,draft,out profileBaseline); LoadDraft(layoutSource,layoutDraft,out layoutBaseline);
                if (draft.Shots.Length == 0) draft.InitializeShots(); if (layoutDraft.Allies.Length == 0) layoutDraft.InitializeDefaults(); Changed();
            }
        }
        if (director == null)
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying || BattleManager.Instance == null || !BattleManager.Instance.CanAcceptCommands))
                if (GUILayout.Button("暂停当前战斗并实时调整")) BeginLive();
        }
        else if (GUILayout.Button("结束实时调整，恢复战斗（草稿保留）")) EndLive();
        EditorGUI.BeginChangeCheck();
        mode = (BattleShotMode)EditorGUILayout.EnumPopup("镜头状态", mode);
        actorIndex = EditorGUILayout.IntSlider("行动者槽位", actorIndex, 0, 3);
        targetIndex = EditorGUILayout.IntSlider("目标槽位", targetIndex, 0, mode == BattleShotMode.AllySupport ? 3 : Mathf.Max(0,(director != null ? LiveUnits(Faction.Enemy).Count : enemyCount)-1));
        selectedState = EditorGUILayout.Toggle("应用选中目标偏移", selectedState);
        handles = EditorGUILayout.Toggle("Scene 机位和站位手柄", handles);
        if (EditorGUI.EndChangeCheck()) Changed();
        if (director == null)
        {
            EditorGUI.BeginChangeCheck();
            enemyCount = EditorGUILayout.IntSlider("预览敌人数", enemyCount, 1, 5); boss = EditorGUILayout.Toggle("大型敌人阵型", boss);
            allyModel = (GameObject)EditorGUILayout.ObjectField("我方预览模型", allyModel, typeof(GameObject), false);
            enemyModel = (GameObject)EditorGUILayout.ObjectField("敌方预览模型", enemyModel, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) { if (boss) enemyCount = 1; RebuildPreview(); }
            int ratio = Mathf.Abs(aspect-16f/9f)<.01f ? 0 : Mathf.Abs(aspect-4f/3f)<.01f ? 1 : 2;
            int newRatio = GUILayout.Toolbar(ratio,new[] { "16:9", "4:3", "21:9" });
            if (ratio != newRatio) { aspect = newRatio == 0 ? 16f/9f : newRatio == 1 ? 4f/3f : 21f/9f; ApplyPreview(); }
            Rect rect = GUILayoutUtility.GetAspectRect(aspect, GUILayout.MaxHeight(Mathf.Clamp(position.height * .27f, 140, 240)));
            if (Event.current.type == EventType.Repaint && preview != null)
            {
                preview.BeginPreview(rect,GUIStyle.none); preview.Render(true); var texture = preview.EndPreview(); GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,false);
                var safe = draft.Find(mode)?.safeArea ?? new Rect(.1f,.2f,.8f,.65f);
                var area = new Rect(rect.x+safe.x*rect.width, rect.y+(1-safe.yMax)*rect.height,safe.width*rect.width,safe.height*rect.height);
                Handles.BeginGUI(); Handles.color = Color.cyan; Handles.DrawAAPolyLine(2,new Vector3(area.xMin,area.yMin),new Vector3(area.xMax,area.yMin),new Vector3(area.xMax,area.yMax),new Vector3(area.xMin,area.yMax),new Vector3(area.xMin,area.yMin)); Handles.EndGUI();
                var setting = draft.Find(mode);
                if (setting != null && setting.lockActorViewport)
                {
                    var anchor = new Vector2(rect.x + setting.actorViewport.x * rect.width, rect.y + (1-setting.actorViewport.y) * rect.height);
                    Handles.BeginGUI(); Handles.color = Color.yellow;
                    Handles.DrawLine(new Vector3(anchor.x-7,anchor.y),new Vector3(anchor.x+7,anchor.y));
                    Handles.DrawLine(new Vector3(anchor.x,anchor.y-7),new Vector3(anchor.x,anchor.y+7)); Handles.EndGUI();
                }
            }
        }
        else EditorGUILayout.LabelField("生效来源",director.SettingsSource);
        int nextTab = GUILayout.Toolbar(tab,new[] { "镜头构图", "阵型 / 前移", "演出关键帧" });
        if (nextTab != tab) { tab = nextTab; ApplyPreview(); }
        if (tab == 0)
        {
            var serialized = new SerializedObject(draft); serialized.Update();
            var shots = serialized.FindProperty("shots");
            EditorGUILayout.LabelField("正在编辑：" + mode + "（此配置内各敌人数共用）", EditorStyles.boldLabel);
            for (int i=0;i<shots.arraySize;i++) if (shots.GetArrayElementAtIndex(i).FindPropertyRelative("modeValue").enumValueIndex == (int)mode)
            {
                var shot = shots.GetArrayElementAtIndex(i);
                var child = shot.Copy(); var end = shot.GetEndProperty();
                if (child.NextVisible(true)) do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    if (child.name != "modeValue") EditorGUILayout.PropertyField(child, true);
                } while (child.NextVisible(false));
            }
            if (serialized.ApplyModifiedProperties()) Changed();
            if (GUILayout.Button("捕获当前 Scene 视角为此状态机位")) { var scene = SceneView.lastActiveSceneView; if (scene != null) Capture(scene.camera.transform.position,scene.camera.transform.rotation,scene.camera.fieldOfView); }
        }
        else if (tab == 1)
        {
            var serialized = new SerializedObject(layoutDraft); serialized.Update();
            foreach (string name in new[] { "allies", "enemyFormations", "readySeconds", "generatedEnemySpacing", "enemyDepth" }) EditorGUILayout.PropertyField(serialized.FindProperty(name),true);
            if (serialized.ApplyModifiedProperties()) Changed();
        }
        else DrawSequence();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("保存相机与阵型草稿")) { SaveChecked(source,draft,ref profileBaseline); SaveChecked(layoutSource,layoutDraft,ref layoutBaseline); Remember(); ApplyPreview(); }
            if (GUILayout.Button("另存为配置组合")) SaveAs();
            if (GUILayout.Button("从资产重新载入"))
            {
                if (EditorUtility.DisplayDialog("重新载入", "覆盖当前相机与阵型草稿？", "重新载入", "取消"))
                { ReloadFromAssets(); }
            }
        }
        EditorGUILayout.EndScrollView();
    }
    private void DrawSequence()
    {
        if (GUILayout.Button("打开完整技能预览（接近／攻击／返回）")) BattleSkillStudio.OpenMenu();
        EditorGUILayout.HelpBox("选择 Timeline 内的 BattleCameraShotClip 子资产。时间以片段起点为 0 秒；勾选关键帧 cut 可硬切。完整动画轨与命中标记在 Timeline 编辑。", MessageType.None);
        var next = (BattleCameraShotClip)EditorGUILayout.ObjectField("镜头片段",shotSource,typeof(BattleCameraShotClip),false);
        if (GUILayout.Button("使用当前选中的镜头片段") && Selection.activeObject is BattleCameraShotClip selected) next = selected;
        if(next!=shotSource) { Remember(); shotSource=next; if(shotDraft!=null)DestroyImmediate(shotDraft);shotDraft=CreateInstance<BattleCameraShotClip>();LoadDraft(shotSource,shotDraft,out shotBaseline);ApplyPreview(); }
        if(shotDraft==null)return;
        EditorGUI.BeginChangeCheck(); sampleDuration=Mathf.Max(.01f,EditorGUILayout.FloatField("片段时长（预览）",sampleDuration));sampleTime=EditorGUILayout.Slider("预览时间（秒）",sampleTime,0,sampleDuration);
        if(EditorGUI.EndChangeCheck())ApplyPreview();
        var serialized=new SerializedObject(shotDraft);serialized.Update();EditorGUILayout.PropertyField(serialized.FindProperty("shot"),true);if(serialized.ApplyModifiedProperties())Changed();
        if(GUILayout.Button("将当前 Scene 机位记录到此时间"))
        {
            var scene=SceneView.lastActiveSceneView;if(scene!=null && previewDirector!=null)
            {
                Undo.RecordObject(shotDraft,"Record camera key");var behavior=shotDraft.shot;
                var anchor=behavior.space==BattleShotSpace.Caster?previewDirector.SequenceCaster:behavior.space==BattleShotSpace.Target?previewDirector.SequenceTarget:previewDirector.SequenceOrigin;
                var origin=anchor!=null?anchor.position:previewDirector.BattlefieldOrigin;
                var basis=behavior.useAnchorRotation&&anchor!=null?anchor.rotation:previewDirector.BattlefieldRotation;
                var keys=behavior.keys.ToList();keys.RemoveAll(k=>Mathf.Abs(k.time-sampleTime)<.001f);
                keys.Add(new BattleCameraKey { time=sampleTime,position=Quaternion.Inverse(basis)*(scene.camera.transform.position-origin),euler=(Quaternion.Inverse(basis)*scene.camera.transform.rotation).eulerAngles,fov=scene.camera.fieldOfView });
                behavior.keys=keys.OrderBy(k=>k.time).ToArray();behavior.lookAtTarget=false;Changed();
            }
        }
        if(GUILayout.Button("保存演出镜头片段")){SaveChecked(shotSource,shotDraft,ref shotBaseline);Remember();}
    }
    private void Capture(Vector3 position,Quaternion rotation,float fov)
    {
        var active=director!=null?director:previewDirector;if(active==null)return;
        var setting=draft.Find(mode);if(setting==null)return;
        Undo.RecordObject(draft,"Capture battle shot");
        var local=Quaternion.Inverse(active.ShotRotation)*(position-active.ShotAnchor)-active.SlotCameraOffset;
        setting.manualRotation=true;setting.fitTargets=false;
        if(selectedState){setting.selectionPosition=local-setting.position;setting.selectionRotation=(Quaternion.Inverse(Quaternion.Euler(setting.rotation))*Quaternion.Inverse(active.ShotRotation)*rotation).eulerAngles;setting.selectionFov=fov-setting.fov;}
        else{setting.position=local;setting.rotation=(Quaternion.Inverse(active.ShotRotation)*rotation).eulerAngles;setting.fov=fov;}
        Changed();
    }
    private void SceneGUI(SceneView scene)
    {
        if(!handles || tab==2)return;
        var active=director!=null?director:previewDirector;if(active==null)return;
        var camera=active.normalCamera;Handles.color=Color.cyan;
        EditorGUI.BeginChangeCheck();var pos=Handles.PositionHandle(camera.transform.position,active.BattlefieldRotation);var rot=Handles.RotationHandle(camera.transform.rotation,pos);
        if(EditorGUI.EndChangeCheck())Capture(pos,rot,camera.Lens.FieldOfView);
        Handles.Label(pos,"Camera / "+mode);Handles.DrawLine(pos,active.AnchorPosition);
        if(tab!=1)return;
        var field=director!=null?BattleManager.Instance.Battlefield:previewField;
        var slots=layoutDraft.Allies;
        for(int i=0;i<slots.Length;i++)SlotHandle(field,slots[i],"Ally "+i);
        string preset=director!=null?BattleManager.Instance.FormationPreset:boss?"Boss":null;
        int count=director!=null?BattleManager.Instance.EnemyCapacity:enemyCount;
        var formation=layoutDraft.EnemyFormations.FirstOrDefault(f=>f.id==(string.IsNullOrEmpty(preset)?"Count"+count:preset));
        if(formation!=null)for(int i=0;i<formation.slots.Length;i++)SlotHandle(field,formation.slots[i],"Enemy "+i);
    }
    private void SlotHandle(BattlefieldDirector field,BattleSlotPose slot,string label)
    {
        var pos=field.Root.TransformPoint(slot.position);Handles.Label(pos,label);
        EditorGUI.BeginChangeCheck();var moved=Handles.PositionHandle(pos,field.Root.rotation);var rot=Handles.RotationHandle(field.Root.rotation*Quaternion.Euler(slot.euler),pos);
        var ready=Handles.PositionHandle(field.Root.TransformPoint(slot.position+slot.readyOffset),field.Root.rotation);
        if(EditorGUI.EndChangeCheck()){Undo.RecordObject(layoutDraft,"Move battle slot");slot.position=field.Root.InverseTransformPoint(moved);slot.euler=(Quaternion.Inverse(field.Root.rotation)*rot).eulerAngles;slot.readyOffset=field.Root.InverseTransformPoint(ready)-slot.position;Changed();}
    }
    public static void SaveDraft(BattleCameraProfile destination,BattleCameraProfile value) => SaveAsset(destination,value);
    private static void SaveAsset(Object destination,Object value)
    {
        if(destination==null||value==null||!AssetDatabase.Contains(destination))throw new ArgumentException("An asset destination is required.");
        Undo.RecordObject(destination,"Save battle presentation draft");string name=destination.name;var flags=destination.hideFlags;
        EditorUtility.CopySerialized(value,destination);destination.name=name;destination.hideFlags=flags;
        EditorUtility.SetDirty(destination);AssetDatabase.SaveAssetIfDirty(destination);if(destination is BattleCameraProfile profile)RefreshUsers(profile);
    }
    private static void SaveChecked(Object asset,Object value,ref string baseline)
    {
        if(asset==null)return;
        if(JsonUtility.ToJson(asset)!=baseline && !EditorUtility.DisplayDialog("资产已被其他编辑修改","覆盖资产，还是取消并保留草稿？","覆盖","取消"))return;
        SaveAsset(asset,value);baseline=JsonUtility.ToJson(asset);
    }
    private void SaveAs()
    {
        string path=EditorUtility.SaveFilePanelInProject("另存配置组合","BattleCamera","asset","选择相机配置路径");if(string.IsNullOrEmpty(path))return;
        path=AssetDatabase.GenerateUniqueAssetPath(path);
        string layoutPath=AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path,null)+"Layout.asset");
        var camera=Instantiate(draft);camera.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(camera,path);
        var layout=Instantiate(layoutDraft);layout.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(layout,layoutPath);
        source=camera;layoutSource=layout;profileBaseline=JsonUtility.ToJson(source);layoutBaseline=JsonUtility.ToJson(layoutSource);Remember();AssetDatabase.SaveAssets();
    }
    public static void RefreshUsers(BattleCameraProfile profile)
    {foreach(var item in Object.FindObjectsByType<BattleShotDirector>(FindObjectsSortMode.None))if(item.IsBound&&!item.IsEditing)item.RefreshConfiguration();}
}
[CustomEditor(typeof(BattleCameraProfile))]
public sealed class BattleCameraProfileEditor : Editor
{
    public override void OnInspectorGUI(){if(GUILayout.Button("打开战斗表现工作台"))BattlePresentationStudio.OpenForProfile((BattleCameraProfile)target);EditorGUI.BeginChangeCheck();DrawDefaultInspector();if(EditorGUI.EndChangeCheck())BattlePresentationStudio.RefreshUsers((BattleCameraProfile)target);}
}
[InitializeOnLoad]
public static class BattleCameraRuntimeOutputCleanup
{
    static BattleCameraRuntimeOutputCleanup()
    {
        // Use Cinemachine's pre-capture hook; do not change its global Save During Play preference.
        Unity.Cinemachine.Editor.SaveDuringPlay.OnHotSave += Restore;
    }
    private static void Restore()
    {
        foreach (var camera in UnityEngine.Object.FindObjectsByType<BattleShotDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            camera.PrepareForEditorExit();
    }
}
