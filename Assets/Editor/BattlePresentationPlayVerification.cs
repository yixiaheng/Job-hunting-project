using System;
using System.IO;
using System.Linq;
using Battle.Logic;
using Battle.Presentation;
using Game.Characters;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real BattleScene/HUD/Timeline smoke test using a disposable save slot; restores editor state on exit.</summary>
[InitializeOnLoad]
public static class BattlePresentationPlayVerification
{
    private const string Active = "BattlePresentation.play";
    private const string Folder = "Temp/BattlePresentation";
    private const string Request = Folder + "/play-request.txt";
    private const string ConfigPath = "Assets/Resources/Bootstrap/PlayerRuntimeConfig.asset";
    private static int step;
    private static double at;
    private static int previousHP;
    private static double frozenTime;
    private static int originalMask;
    private static int cancelledCallbacks;
    private static BattleUnit target;
    static BattlePresentationPlayVerification()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += Changed;
    }
    [MenuItem("Tools/Battle/Presentation/Verify Real Battle Scene")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var config = Resources.Load<PlayerRuntimeConfig>("Bootstrap/PlayerRuntimeConfig");
        if (config == null || EditorUtility.IsDirty(config)) throw new InvalidOperationException("Save runtime configuration before verification.");
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(Folder + "/runtime-before.bin", File.ReadAllBytes(ConfigPath));
        SessionState.SetString(Active + ".startScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Active + ".slot", config.saveSlot);
        config.saveSlot = "slot_battle_camera_verification_" + Guid.NewGuid().ToString("N");
        SessionState.SetString(Active + ".testSlot", config.saveSlot);
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        File.WriteAllText(Folder + "/playmode.txt", "");
        SessionState.SetBool(Active, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/BattleScene.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            step = -1; at = EditorApplication.timeSinceStartup; cancelledCallbacks = 0;
            Application.runInBackground = true;
        }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetBool(Active, false);
        var config = Resources.Load<PlayerRuntimeConfig>("Bootstrap/PlayerRuntimeConfig");
        config.saveSlot = SessionState.GetString(Active + ".slot", "slot_0");
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        string slot = SessionState.GetString(Active + ".testSlot", "");
        if (slot.StartsWith("slot_battle_camera_verification_", StringComparison.Ordinal))
            TapTap21.SaveSystem.dyh.SaveSystem.DeleteSlot(slot);
        File.WriteAllBytes(ConfigPath, File.ReadAllBytes(Folder + "/runtime-before.bin"));
        AssetDatabase.ImportAsset(ConfigPath);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Active + ".startScene", ""));
        File.AppendAllText(Folder + "/playmode.txt", "RESTORED original save configuration and editor start scene\n");
    }
    private static void Next(int next) { step = next; at = EditorApplication.timeSinceStartup; }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        File.AppendAllText(Folder + "/playmode.txt", "PASS " + name + "\n");
    }
    private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/" + name + ".png"));
    private static void Update()
    {
        if (!SessionState.GetBool(Active, false))
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Request))
            {
                File.Delete(Request);
                Begin();
            }
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            if (EditorApplication.timeSinceStartup - at > 45) throw new TimeoutException("Battle presentation step " + step);
            var manager = BattleManager.Instance;
            if (Time.frameCount < 5 || manager == null || manager.Flow == null || manager.Presentation == null) return;
            var presentation = manager.Presentation;
            if (step == -1)
            { if (manager.CanAcceptCommands) Next(0); return; }
            if (step == 0)
            {
                if (!manager.CanAcceptCommands || EditorApplication.timeSinceStartup - at < 0.7) return;
                Check(UIManager.Instance != null && manager.OutputCamera != null && manager.CameraController != null,
                    "real BattleScene opens HUD and configured camera");
                var actionTimeline = Object.FindFirstObjectByType<BattleActionTimelinePanel>();
                var actionOrder = manager.Flow.GetActionOrderSnapshot(manager.Flow.CurrentUnit);
                Check(actionTimeline != null && UIManager.Instance.IsOpen(UIId.BattleActionTimeline)
                    && actionTimeline.VisibleEntryCount == manager.Flow.Units.Count(unit => unit.IsActive),
                    "UIManager opens the real battle action timeline with one row per active unit");
                Check(actionOrder.Count > 0 && actionOrder[0].Unit == manager.Flow.CurrentUnit
                    && actionOrder[0].RemainingActionValue == 0
                    && actionTimeline.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
                    "real action timeline pins current actor at zero without blocking input");
                var current = manager.Selection.Actor;
                int logicalSlot = current.pos; int turn = manager.Flow.TurnCount;
                var originalSkill = manager.Selection.Skill;
                var alternative = current.skills.FirstOrDefault(s => s != null && s.kind == SkillKind.Skill && manager.Flow.ValidateAction(manager.CreateAbilityAction(s.kind)).IsValid);
                if (alternative != null)
                {
                    manager.ChooseAbility(alternative.kind);
                    Check(manager.Selection.Skill == alternative && manager.Flow.TurnCount == turn && manager.CanAcceptCommands,
                        "first click changes skill preview without submitting an action");
                    manager.ChooseAbility(originalSkill.kind);
                }
                var studio = ScriptableObject.CreateInstance<BattlePresentationStudio>();
                try
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    typeof(BattlePresentationStudio).GetMethod("BeginLive",flags).Invoke(studio,null);
                    Check(presentation.Clock.Paused && manager.CameraController.IsEditing && !manager.CanAcceptCommands,"Studio pauses battle and locks input");
                    typeof(BattlePresentationStudio).GetField("targetIndex",flags).SetValue(studio,1);
                    typeof(BattlePresentationStudio).GetField("actorIndex",flags).SetValue(studio,1);
                    typeof(BattlePresentationStudio).GetMethod("ApplyPreview",flags).Invoke(studio,null);
                    Check(manager.Selection.Actor == current && current.pos == logicalSlot && manager.Flow.TurnCount == turn,"live actor and target preview does not alter logical selection");
                    typeof(BattlePresentationStudio).GetMethod("EndLive",flags).Invoke(studio,null);
                    Check(!presentation.Clock.Paused && !manager.CameraController.IsEditing && manager.Selection.Actor == current,"ending Studio preview restores clock and selection");
                }
                finally { Object.DestroyImmediate(studio); }
                originalMask = manager.OutputCamera.cullingMask;
                foreach (var enemy in manager.Flow.Units.Where(u => u.faction == Faction.Enemy))
                { enemy.maxHealth = enemy.currentHealth = 100000; }
                presentation.Display.Synchronize();
                target = manager.Flow.Units.First(u => u.faction == Faction.Enemy && u.IsActive);
                previousHP = BattleDisplayState.Health(target);
                Capture("normal-view");
                presentation.Clock.Paused = true;
                manager.ChooseBasic();
                Check(manager.TurnDriver.State == BattleTurnState.Presenting && !manager.CanAcceptCommands,
                    "submitted skill blocks command input immediately");
                Check(actionTimeline.IsHolding,
                    "action timeline freezes its pre-resolution order during skill presentation");
                Check(BattleDisplayState.Health(target) == previousHP && target.currentHealth < previousHP,
                    "real HUD data waits while logical skill already deals damage");
                Next(1); return;
            }
            if (step == 1)
            {
                if (EditorApplication.timeSinceStartup - at < 0.4) return;
                Check(BattleDisplayState.Health(target) == previousHP, "paused real action cannot reveal damage");
                presentation.Clock.Paused = false; Next(2); return;
            }
            if (step == 2)
            {
                if (presentation.timelineDirector.time < 0.5) return;
                presentation.Clock.Paused = true; frozenTime = presentation.timelineDirector.time;
                Capture("skill-view"); Next(3); return;
            }
            if (step == 3)
            {
                if (EditorApplication.timeSinceStartup - at < 0.4) return;
                Check(Math.Abs(presentation.timelineDirector.time - frozenTime) < 0.0001,
                    "pause freezes authored camera and animation Timeline");
                presentation.Clock.Paused = false; Next(4); return;
            }
            if (step == 4)
            {
                if (!manager.CanAcceptCommands) return;
                Check(!manager.CameraController.HasExclusiveOwner, "real skill returns camera to ally base");
                var actionTimeline = Object.FindFirstObjectByType<BattleActionTimelinePanel>();
                Check(actionTimeline != null && !actionTimeline.IsHolding
                    && manager.Flow.GetActionOrderSnapshot(manager.Flow.CurrentUnit)[0].RemainingActionValue == 0,
                    "action timeline releases and advances after skill presentation completes");
                var stage = AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(BattlePresentationSetup.Root + "/SampleSeparateStage.asset");
                presentation.events = new[] { new BattleEventSequence { eventId = "VerificationStage", sequence = stage } };
                manager.QueuePresentationEvent("VerificationStage", new[] { manager.Flow.CurrentUnit, target }, () => cancelledCallbacks++);
                Next(5); return;
            }
            if (step == 5)
            {
                if (presentation.timelineDirector.time < 0.3) return;
                Check(manager.OutputCamera.cullingMask != originalMask && !manager.CanAcceptCommands,
                    "real independent stage controls render mask and input gate");
                var panel = Object.FindFirstObjectByType<BattleUIController>();
                Check(panel != null && !panel.GetComponent<CanvasGroup>().blocksRaycasts,
                    "cinematic HUD is hidden and does not intercept clicks");
                presentation.Clock.Paused = true; Capture("stage-view"); Next(6); return;
            }
            if (step == 6)
            {
                if (EditorApplication.timeSinceStartup - at < 0.3) return;
                manager.StartBattle();
                Check(manager.OutputCamera.cullingMask == originalMask && cancelledCallbacks == 0,
                    "retry during stage restores camera and suppresses stale completion");
                Next(7); return;
            }
            if (step == 7)
            {
                if (!manager.CanAcceptCommands) return;
                Check(cancelledCallbacks == 0 && !manager.CameraController.HasExclusiveOwner,
                    "retried battle reaches usable base view without old stage ownership");
                var actionTimeline = Object.FindFirstObjectByType<BattleActionTimelinePanel>();
                Check(actionTimeline != null && UIManager.Instance.IsOpen(UIId.BattleActionTimeline)
                    && actionTimeline.VisibleEntryCount == manager.Flow.Units.Count(unit => unit.IsActive),
                    "retried battle rebuilds its UIManager-owned action timeline without stale rows");
                File.AppendAllText(Folder + "/playmode.txt", "SUCCESS real battle presentation smoke test\n");
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception error)
        {
            File.AppendAllText(Folder + "/playmode.txt", "FAIL " + error + "\n");
            Debug.LogException(error); EditorApplication.isPlaying = false;
        }
    }
}
