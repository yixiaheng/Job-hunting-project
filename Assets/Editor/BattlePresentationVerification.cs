using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Battle.Logic;
using Battle.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BattlePresentationVerification
{
    private static readonly List<string> checks = new();
    private static void Check(bool value, string name)
    { if (!value) throw new InvalidOperationException("FAILED: " + name); checks.Add(name); }
    [MenuItem("Tools/Battle/Presentation/Verify Camera Tuning")]
    public static void RunTuning()
    {
        checks.Clear(); string error = null;
        try { TuningChecks(); }
        catch (Exception e) { error = e.ToString(); throw; }
        finally
        {
            Directory.CreateDirectory("Temp/BattlePresentation");
            File.WriteAllText("Temp/BattlePresentation/camera-tuning.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            { passed = error == null, error, checkCount = checks.Count, checks, completedAt = DateTimeOffset.Now }, Newtonsoft.Json.Formatting.Indented));
        }
    }
    [MenuItem("Tools/Battle/Presentation/Verify Presentation")]
    public static void Run()
    {
        checks.Clear(); string error = null;
        try { ClockChecks(); QueueChecks(); DriverChecks(); VisualChecks(); StageChecks(); TuningChecks(); }
        catch (Exception e) { error = e.ToString(); throw; }
        finally
        {
            Directory.CreateDirectory("Temp/BattlePresentation");
            File.WriteAllText("Temp/BattlePresentation/results.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            { passed = error == null, error, checkCount = checks.Count, checks, completedAt = DateTimeOffset.Now }, Newtonsoft.Json.Formatting.Indented));
        }
        Debug.Log("Battle presentation verification passed: " + checks.Count);
    }
    private static void ClockChecks()
    {
        var clock = new BattlePresentationClock(); clock.Tick(0.25f);
        Check(clock.Time == 0.25, "presentation clock advances independently of action time");
        clock.Paused = true; clock.Tick(1);
        Check(clock.Delta == 0 && clock.Time == 0.25, "pause freezes time and delta");
        clock.Paused = false; clock.Speed = 2; clock.Tick(0.25f);
        Check(clock.Time == 0.75, "speed applies once to shared clock");
        clock.Tick(10, true); Check(clock.Time == 0.75, "scene suspension freezes presentation");
    }
    private static void QueueChecks()
    {
        var queue = new BattlePresentationQueue(); Action first = null; int count = 0;
        queue.Enqueue(done => first = done, () => { count++; queue.Enqueue(done => done(), () => count++); });
        queue.Enqueue(done => done(), () => count++);
        Check(queue.IsPresenting && count == 0, "queue waits for asynchronous whole segment");
        first(); first();
        Check(count == 3 && !queue.IsPresenting, "reentrant enqueue and duplicate completion execute once");
        queue.Enqueue(done => first = done, () => count++); queue.Cancel();
        queue.Enqueue(done => done(), () => count += 10); first();
        Check(count == 13 && !queue.IsPresenting, "cancelled generation cannot consume new queue work");
        int synchronous = 0;
        queue.Enqueue(done => { for (int i = 0; i < 2000; i++) queue.Enqueue(next => next(), () => synchronous++); done(); });
        Check(synchronous == 2000, "synchronous queue drains without recursive segment stack growth");
        Action actionEnd = null, specialEnd = null;
        var order = new List<string>();
        queue.ActionPresenter = (_, done) => { order.Add("action"); actionEnd = done; };
        queue.Present(new ActionResolution(), () => order.Add("next-turn"));
        queue.Enqueue(done => { order.Add("special"); specialEnd = done; });
        actionEnd();
        Check(string.Join(",", order) == "action,special", "action appended event holds turn continuation");
        specialEnd(); specialEnd(); actionEnd();
        Check(string.Join(",", order) == "action,special,next-turn", "action barrier completes only once after special event");
    }
    private sealed class Actors : IBattleActorLookup, IDisposable
    {
        private readonly Dictionary<BattleUnit, BattleUnitActor> map = new();
        public Actors(IEnumerable<BattleUnit> units, Scene scene)
        {
            int index = 0;
            foreach (var unit in units)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.name = unit.displayName;
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.position = new Vector3(index++ * 2f, 1f, unit.faction == Faction.OurSide ? -5 : 0);
                go.AddComponent<Animator>();
                var actor = go.AddComponent<BattleUnitActor>(); actor.Bind(unit); map[unit] = actor;
            }
        }
        public bool TryGetActor(BattleUnit unit, out BattleUnitActor actor)
        { actor = null; return unit != null && map.TryGetValue(unit, out actor); }
        public void Dispose() { foreach (var actor in map.Values) if (actor != null) Object.DestroyImmediate(actor.gameObject); }
    }
    private sealed class Fixture : IDisposable
    {
        public BattleUnit Ally = Unit(Faction.OurSide, 0, 200);
        public BattleUnit Enemy = Unit(Faction.Enemy, 1, 100);
        public SkillConfig Skill;
        public BattleFlow Flow;
        public Fixture(bool lethal = false)
        {
            Skill = ScriptableObject.CreateInstance<SkillConfig>(); Skill.id = -9182;
            Skill.Name = "Presentation Verification"; Skill.targetRule = TargetRule.Single; Skill.targetSide = TargetSide.OtherSide;
            Skill.effectConfigs = new List<EffectConfig>
            {
                new() { effectType = EffectType.Damage, scaleStat = StatType.Attack, value = lethal ? 100 : 1 },
                new() { effectType = EffectType.Damage, scaleStat = StatType.Attack, value = 1 }
            };
            Ally.skills.Add(Skill); Enemy.skills.Add(Skill);
            Flow = new BattleFlow(new[] { Ally, Enemy }); Flow.Ready();
        }
        public BattleAction Action => new() { caster = Ally, skill = Skill, mainTarget = Enemy };
        public void Dispose() => Object.DestroyImmediate(Skill);
    }
    private static BattleUnit Unit(Faction side, int index, int speed) => new()
    { displayName = side + " " + index, faction = side, pos = index, tieBreaker = index,
        maxHealth = 1000, currentHealth = 1000, attack = 100, defence = 10, speed = speed, maxEnergy = 100 };
    private static void DriverChecks()
    {
        using var f = new Fixture(); Action finish = null;
        var queue = new BattlePresentationQueue { ActionPresenter = (_, done) => finish = done };
        var driver = new BattleTurnDriver(f.Flow, queue); driver.Start();
        Check(driver.Submit(f.Action).Succeeded && driver.State == BattleTurnState.Presenting, "driver holds at presentation boundary");
        int turn = f.Flow.TurnCount;
        Check(!driver.Submit(f.Action).Succeeded && !driver.StepOnce().Succeeded && f.Flow.TurnCount == turn,
            "both player and debug step reject during presentation");
        driver.Stop(); finish(); Check(driver.State == BattleTurnState.Stopped, "stale action continuation cannot restart stopped driver");
        using var wave = new Fixture(true);
        wave.Flow.SetPendingWave(true);
        Action<bool> enter = null;
        var waveDriver = new BattleTurnDriver(wave.Flow) { TransitionWaveAsync = done => enter = done };
        waveDriver.Start(); waveDriver.Submit(wave.Action);
        Check(waveDriver.State == BattleTurnState.TransitioningWave && enter != null, "wave boundary waits for entrance completion");
        double time = wave.Flow.CurrentTime;
        Check(!waveDriver.Submit(wave.Action).Succeeded && wave.Flow.CurrentTime == time, "entrance cannot consume logical action time");
        waveDriver.Stop(); enter(true); Check(waveDriver.State == BattleTurnState.Stopped, "stale wave continuation cannot resume stopped session");
    }
    private static BattlePresentationCoordinator MakeCoordinator(Scene scene, out GameObject root)
    {
        root = new GameObject("PresentationVerification"); SceneManager.MoveGameObjectToScene(root, scene);
        var coordinator = root.AddComponent<BattlePresentationCoordinator>();
        var director = root.AddComponent<BattleShotDirector>(); coordinator.cameraDirector = director;
        coordinator.timelineDirector = root.AddComponent<PlayableDirector>(); coordinator.timelineDirector.playOnAwake = false;
        var output = new GameObject("Output"); output.transform.SetParent(root.transform);
        director.outputCamera = output.AddComponent<Camera>(); director.outputCamera.enabled = false;
        director.brain = output.AddComponent<CinemachineBrain>();
        var normal = new GameObject("Normal"); normal.transform.SetParent(root.transform);
        var cinematic = new GameObject("Cinematic"); cinematic.transform.SetParent(root.transform);
        director.normalCamera = normal.AddComponent<CinemachineCamera>();
        director.cinematicCamera = cinematic.AddComponent<CinemachineCamera>();
        director.profile = AssetDatabase.LoadAssetAtPath<BattleCameraProfile>(BattlePresentationSetup.CameraRoot + "/DefaultCamera.asset");
        return coordinator;
    }
    private static void VisualChecks()
    {
        var previousScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene); GameObject root = null;
        try
        {
            using var f = new Fixture(); using var actors = new Actors(f.Flow.Units, scene);
            var coordinator = MakeCoordinator(scene, out root); coordinator.Bind(f.Flow, actors);
            var sample = AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(BattlePresentationSetup.Root + "/SampleBattlefield.asset");
            Check(sample != null && sample.timeline != null && BattlePresentationCoordinator.CollectMarkers(sample.timeline).Count == 2,
                "sample contains two ordered impact markers; loaded=" + (sample != null) + "; timeline=" + (sample?.timeline != null)
                + "; markers=" + (sample?.timeline != null ? BattlePresentationCoordinator.CollectMarkers(sample.timeline).Count : -1));
            coordinator.skills = new[] { new BattleSkillSequence { skillId = f.Skill.id, casterFaction = Faction.OurSide, sequence = sample } };
            int before = BattleDisplayState.Health(f.Enemy);
            var result = f.Flow.TryExecute(f.Action);
            Check(result.Succeeded && f.Enemy.currentHealth < before && BattleDisplayState.Health(f.Enemy) == before,
                "logic resolves while displayed HP remains at pre-action value");
            int completed = 0; int effects = 0; coordinator.EffectPresented += _ => effects++;
            coordinator.Present(result.Resolution, () => completed++); coordinator.Tick(0.01f);
            Check(coordinator.cameraDirector.HasExclusiveOwner && BattleDisplayState.Health(f.Enemy) == before, "cinematic owns camera before first impact");
            coordinator.Clock.Paused = true; coordinator.Tick(5);
            Check(BattleDisplayState.Health(f.Enemy) == before && completed == 0, "pause freezes Timeline and impact markers");
            coordinator.Clock.Paused = false; coordinator.Tick(0.5f);
            Check(BattleDisplayState.Health(f.Enemy) < before && BattleDisplayState.Health(f.Enemy) > f.Enemy.currentHealth,
                "first marker reveals only first damage record");
            coordinator.Tick(2f);
            Check(BattleDisplayState.Health(f.Enemy) == f.Enemy.currentHealth && completed == 1
                && effects == result.Resolution.Record.Effects.Count, "skipped frame dispatches every crossed effect exactly once");
            Check(!coordinator.cameraDirector.HasExclusiveOwner, "completion returns camera ownership to base");
            int token = coordinator.cameraDirector.Acquire(); coordinator.cameraDirector.Release(token);
            int next = coordinator.cameraDirector.Acquire();
            Check(!coordinator.cameraDirector.Release(token) && coordinator.cameraDirector.HasExclusiveOwner,
                "old camera token cannot release a new cinematic");
            coordinator.cameraDirector.Release(next); coordinator.Cancel();
            Check(!BattleDisplayState.IsManaged(f.Enemy), "teardown releases display registrations");
            using var lethal = new Fixture(true); using var deadActors = new Actors(lethal.Flow.Units, scene);
            coordinator.Bind(lethal.Flow, deadActors);
            var deathResult = lethal.Flow.TryExecute(lethal.Action);
            int deathDone = 0; coordinator.skills = Array.Empty<BattleSkillSequence>();
            coordinator.Present(deathResult.Resolution, () => deathDone++); coordinator.Tick(0.01f);
            deadActors.TryGetActor(lethal.Enemy, out var enemyActor);
            Check(!lethal.Enemy.IsActive && enemyActor.gameObject.activeSelf && BattleDisplayState.Visible(lethal.Enemy),
                "logical death keeps model and HUD alive until death segment");
            for (int i = 0; i < 40; i++) coordinator.Tick(0.1f);
            Check(deathDone == 1 && !enemyActor.gameObject.activeSelf && !BattleDisplayState.Visible(lethal.Enemy),
                "death segment hides model and finishes action once");
            coordinator.PresentEvent("Transform", new[] { lethal.Ally }, () => deathDone++);
            coordinator.Tick(0.01f); coordinator.Cancel(); coordinator.Tick(10f);
            Check(deathDone == 1, "cancel suppresses outstanding event completion");
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
    private static void StageChecks()
    {
        var previousScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene); GameObject root = null;
        try
        {
            using var f = new Fixture(); using var actors = new Actors(f.Flow.Units, scene);
            var coordinator = MakeCoordinator(scene, out root); coordinator.Bind(f.Flow, actors);
            var sample = AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(BattlePresentationSetup.Root + "/SampleSeparateStage.asset");
            Check(sample != null && sample.stagePrefab.IsSafe(out _), "stage prefab contains only approved visual components");
            coordinator.skills = new[] { new BattleSkillSequence { skillId = f.Skill.id, casterFaction = Faction.OurSide, sequence = sample } };
            var camera = coordinator.cameraDirector.outputCamera; int mask = camera.cullingMask;
            bool visible = true; coordinator.HUDVisibilityChanged += state => visible = state;
            actors.TryGetActor(f.Ally, out var actor); Vector3 position = actor.transform.position;
            var result = f.Flow.TryExecute(f.Action);
            coordinator.Present(result.Resolution, () => { }); coordinator.Tick(0.01f);
            Check(camera.cullingMask == 1 << sample.stagePrefab.stageLayer && !visible,
                "stage masks battlefield and suspends only battle HUD");
            Check(!actor.GetComponent<Renderer>().enabled && actor.transform.position == position,
                "stage uses clones without moving authoritative actor");
            Transform clone = coordinator.cameraDirector.SequenceCaster;
            Check(clone != actor.transform && clone.GetComponent<BattleUnitActor>() == null
                && clone.GetComponent<Collider>() == null, "stage clones omit gameplay scripts and colliders");
            coordinator.Cancel();
            Check(camera.cullingMask == mask && visible && actor.GetComponent<Renderer>().enabled,
                "mid-cinematic cancellation restores camera mask HUD and original renderers");
            Check(actor.transform.position == position && clone == null, "stage cancellation destroys clones and preserves original pose");
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void TuningChecks()
    {
        var previousScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene); GameObject root = null;
        var draft = ScriptableObject.CreateInstance<BattleCameraProfile>();
        try
        {
            using var f = new Fixture(); using var actors = new Actors(f.Flow.Units, scene);
            var coordinator = MakeCoordinator(scene, out root); coordinator.Bind(f.Flow, actors);
            var camera = coordinator.cameraDirector;
            camera.SetBase(f.Ally); camera.SetSelection(f.Enemy);
            draft.InitializeShots();
            var setting = draft.Find(BattleShotMode.EnemySingle);
            setting.alignToFormation = false; setting.manualRotation = true; setting.rotation = new Vector3(12,20,3);
            setting.position = new Vector3(2,3,-4); setting.fov = 55; setting.fitTargets = false;
            setting.lockActorViewport = false;
            setting.selectionPosition = new Vector3(.3f,.2f,.1f);
            setting.selectionRotation = new Vector3(0,4,0); setting.selectionFov = -7;
            Check(camera.PreviewContext(draft,new BattleCameraContext(f.Ally,null,null,BattleShotMode.EnemySingle)), "tuning draft acquires base preview");
            Vector3 expected = camera.AnchorPosition + camera.BattlefieldRotation * setting.position;
            Check(Vector3.Distance(camera.normalCamera.transform.position, expected) < .001f
                && Quaternion.Angle(camera.normalCamera.transform.rotation,camera.BattlefieldRotation * Quaternion.Euler(setting.rotation)) < .01f,
                $"manual position and rotation apply immediately without advancing clock (lock={setting.lockActorViewport}, actual={camera.normalCamera.transform.position}, expected={expected})");
            Check(Mathf.Abs(camera.normalCamera.Lens.FieldOfView-55)<.001f,"draft FOV applies immediately");
            var context = new BattleCameraContext(f.Ally,f.Enemy,new[]{f.Enemy},BattleShotMode.EnemySingle);
            camera.PreviewContext(draft,context);
            Vector3 selectedPosition = camera.normalCamera.transform.position;
            Check(Vector3.Distance(selectedPosition,expected+camera.BattlefieldRotation*setting.selectionPosition)<.001f
                && Mathf.Abs(camera.normalCamera.Lens.FieldOfView-48)<.001f,"selection adds position and FOV offsets");
            camera.PreviewContext(draft,context);
            Check(Vector3.Distance(camera.normalCamera.transform.position,selectedPosition)<.001f,"selection offsets never accumulate");
            camera.EndConfigurationPreview();
            camera.SetSelection(null);
            var lateUpdate = typeof(BattleShotDirector).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < 20; i++) { coordinator.Clock.Tick(.1f); lateUpdate.Invoke(camera, null); }
            Check(!camera.IsTransitioning, "camera transition terminates after finite duration");
            camera.normalCamera.transform.position += Vector3.right;
            Vector3 moved = camera.normalCamera.transform.position;
            lateUpdate.Invoke(camera, null);
            Check(camera.normalCamera.transform.position == moved, "idle LateUpdate does not rewrite camera transform");
            camera.RefreshConfiguration();
            Check(camera.normalCamera.transform.position != moved, "explicit configuration event reapplies camera pose");
            string json = JsonUtility.ToJson(draft);
            var restored = ScriptableObject.CreateInstance<BattleCameraProfile>();
            try
            {
                JsonUtility.FromJsonOverwrite(json, restored);
                Check(restored.Find(BattleShotMode.EnemySingle).rotation == setting.rotation && restored.Find(BattleShotMode.EnemySingle).selectionPosition == setting.selectionPosition
                    && restored.Find(BattleShotMode.EnemySingle).selectionFov == -7 && !restored.Find(BattleShotMode.EnemySingle).lockActorViewport,
                    "draft JSON roundtrip retains new configuration fields");
                string path = "Assets/Config/Battle/Presentation/VerificationDraft_" + Guid.NewGuid().ToString("N") + ".asset";
                try
                {
                    AssetDatabase.CreateAsset(restored, path);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    BattlePresentationStudio.SaveDraft(restored, draft);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var saved = AssetDatabase.LoadAssetAtPath<BattleCameraProfile>(path);
                    Check(saved.Find(BattleShotMode.EnemySingle).position == setting.position && saved.Find(BattleShotMode.EnemySingle).selectionFov == -7
                        && saved.hideFlags == HideFlags.None && AssetDatabase.AssetPathToGUID(path) == guid,
                        "explicit draft save persists values and preserves destination GUID and flags");

                }
                finally { AssetDatabase.DeleteAsset(path); }
            }
            finally { Object.DestroyImmediate(restored); }
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root); Object.DestroyImmediate(draft);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
