using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Characters;
using Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real Play Mode regression using an isolated save slot. Does not save/reopen the user's edit scene.</summary>
[InitializeOnLoad]
public static class SceneFlowVerification
{
    private const string Key = "SceneFlowVerification.Active";
    private const string ConfigPath = "Assets/Resources/Bootstrap/PlayerRuntimeConfig.asset";
    private static readonly List<string> checks = new();
    private static readonly List<string> notes = new();
    private static readonly List<UnityEngine.Object> animationFixtures = new();
    private static bool launched;
    private static double deadline;
    private static string phase;
    public static string Status => $"{phase} launched={launched} active={SessionState.GetBool(Key, false)} playerReady={PlayerRuntime.Instance != null && PlayerRuntime.Instance.IsReady}";
    public static void Stop() { if (SessionState.GetBool(Key, false)) Finish("Stopped for test harness maintenance."); }
    static SceneFlowVerification()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Changed;
    }
    [MenuItem("Tools/Scenes/Verify Unified Scene Flow")]
    public static void Begin() => BeginFrom(SceneId.None);
    [MenuItem("Tools/Scenes/Verify Direct Character Startup")]
    public static void BeginCharacter() => BeginFrom(SceneId.Character);
    [MenuItem("Tools/Scenes/Verify Direct Party Startup")]
    public static void BeginParty() => BeginFrom(SceneId.Party);
    [MenuItem("Tools/Scenes/Verify Direct Battle Startup")]
    public static void BeginBattle() => BeginFrom(SceneId.Battle);
    private static void BeginFrom(SceneId start)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        if (start == SceneId.None && SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open SampleScene before running this regression.");
        Directory.CreateDirectory(SceneFlowSetup.DirectoryPath);
        var config = AssetDatabase.LoadAssetAtPath<PlayerRuntimeConfig>(ConfigPath);
        if (config == null || EditorUtility.IsDirty(config)) throw new InvalidOperationException("Save runtime config edits before testing.");
        File.WriteAllBytes(SceneFlowSetup.DirectoryPath + "/runtime-config-before.bin", File.ReadAllBytes(ConfigPath));
        SessionState.SetString(Key + ".slot", config.saveSlot);
        string testSlot = "slot_scene_flow_" + Guid.NewGuid().ToString("N");
        SessionState.SetString(Key + ".testSlot", testSlot);
        config.saveSlot = testSlot;
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        SessionState.SetInt(Key + ".start", (int)start);
        SessionState.SetString(Key + ".startScene", AssetDatabase.GetAssetPath(UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene));
        var catalog = Resources.Load<SceneCatalog>(SceneCatalog.ResourcePath);
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = start != SceneId.None && catalog.TryGetPath(start, out var startPath)
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(startPath) : null;
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { launched = false; checks.Clear(); notes.Clear(); deadline = EditorApplication.timeSinceStartup + 240; phase = "bootstrap"; Application.runInBackground = true; EditorApplication.isPaused = false; }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetBool(Key, false);
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".startScene", ""));
        foreach (var fixture in animationFixtures) if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
        animationFixtures.Clear();
        var config = AssetDatabase.LoadAssetAtPath<PlayerRuntimeConfig>(ConfigPath);
        config.saveSlot = SessionState.GetString(Key + ".slot", "slot_0");
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        string backup = SceneFlowSetup.DirectoryPath + "/runtime-config-before.bin";
        if (File.Exists(backup)) { File.WriteAllBytes(ConfigPath, File.ReadAllBytes(backup)); AssetDatabase.ImportAsset(ConfigPath); }
        string testSlot = SessionState.GetString(Key + ".testSlot", "");
        if (testSlot.StartsWith("slot_scene_flow_", StringComparison.Ordinal)) TapTap21.SaveSystem.dyh.SaveSystem.DeleteSlot(testSlot);
        File.WriteAllText(SceneFlowSetup.DirectoryPath + "/restored.txt", "Original save configuration restored; isolated test slot removed.");
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup > deadline) { Finish("Timed out: " + phase); return; }
        if (!launched && GameSceneManager.Instance != null && PlayerRuntime.Instance != null && PlayerRuntime.Instance.IsReady && Time.frameCount > 5)
        {
            launched = true;
            var start = (SceneId)SessionState.GetInt(Key + ".start", (int)SceneId.None);
            GameSceneManager.Instance.StartCoroutine(Guard(start == SceneId.None ? Run() : RunStandalone(start)));
        }
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(phase + ": " + name);
        checks.Add(name);
    }
    private static void Finish(string error = null)
    {
        var start = (SceneId)SessionState.GetInt(Key + ".start", (int)SceneId.None);
        string name = start == SceneId.None ? "results" : "direct-" + start;
        string hotUpdateError = HotUpdateBootstrap.Instance != null ? HotUpdateBootstrap.Instance.StartupError : null;
        File.WriteAllText(SceneFlowSetup.DirectoryPath + "/" + name + ".json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = error == null, error, phase, unityVersion = Application.unityVersion,
                completedAt = DateTime.Now.ToString("O"), checkCount = checks.Count, checks, notes, hotUpdateError }, Newtonsoft.Json.Formatting.Indented));
        EditorApplication.isPlaying = false;
    }
    private static IEnumerator Guard(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        while (stack.Count > 0)
        {
            object value = null; bool next = false; Exception failure = null;
            try { next = stack.Peek().MoveNext(); if (next) value = stack.Peek().Current; }
            catch (Exception e) { failure = e; }
            if (failure != null) { Finish(failure.ToString()); yield break; }
            if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (value is IEnumerator nested) stack.Push(nested); else yield return value;
        }
        Finish();
    }
    private static IEnumerator Run()
    {
        var scenes = GameSceneManager.Instance;
        var source = SceneManager.GetActiveScene();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        Check(player != null, "exploration player exists");
        Check(UnityEngine.Object.FindFirstObjectByType<PartyEntryButton>() != null, "real exploration party entry installed");
        yield return VerifyCameraHandoffs(source);
        var input = player.InputHandler;
        int partySubscribers = Game.GameEvents.Battle.GetSubscriberCount<Battle.Events.PartyFormationChangedEvent>();
        int characterSubscribers = Game.GameEvents.Character.GetSubscriberCount<CharacterChangedEvent>();
        float oldTime = Time.timeScale;
        Time.timeScale = .7f;
        input.inputActions.Player.Zoom.Disable();
        var camera = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First();
        bool cameraEnabled = camera.enabled;
        camera.enabled = false;
        int originalMask = camera.cullingMask;

        phase = "invalid requests";
        bool prepared = false;
        var invalid = scenes.LoadAsync(SceneId.None, new SceneLoadOptions { Prepare = () => prepared = true });
        Check(!invalid.Accepted && invalid.IsDone && !prepared, "rejected operation does not prepare business context");
        Check(!scenes.ReturnAsync(default).Accepted, "stale return rejected");

        for (int i = 0; i < 3; i++)
        {
            foreach (SceneId id in new[] { SceneId.Character, SceneId.Party })
            {
                phase = id + " cycle " + i;
                if (i == 1) player.playerMoveStateMachine.ChangeState<PlayerMoveState>();
                if (i == 2) player.playerMoveStateMachine.ChangeState<PlayerJumpState>();
                player.rb.linearVelocity = i == 0 ? Vector3.zero : new Vector3(1, i == 2 ? 4 : 0, 2);
                Vector3 position = player.transform.position, velocity = player.rb.linearVelocity;
                Quaternion rotation = player.transform.rotation;
                object moveState = player.playerMoveStateMachine.currentState;
                var explorationAnimator = player.Animation.animator;
                var animationState = explorationAnimator.GetCurrentAnimatorStateInfo(0);
                SceneOperation enter = id == SceneId.Character
                    ? scenes.EnterVisitAsync(id, new ScenePresentationLifecycle<CharacterSceneController>(), transition: SceneTransitionMode.Fade)
                    : scenes.EnterVisitAsync(id, new ScenePresentationLifecycle<PartySceneController>(), transition: SceneTransitionMode.Fade);
                Check(enter.Accepted, "visit accepted " + phase);
                Check(!scenes.LoadAsync(SceneId.Battle).Accepted, "concurrent single load rejected " + phase);
                yield return enter;
                Check(enter.Succeeded, "visit initialized " + phase + " " + enter.Error);
                Check(!scenes.EnterVisitAsync(id, new ScenePresentationLifecycle<PartySceneController>()).Accepted, "same target rejected " + phase);
                Check(source.isLoaded && player.IsScenePaused, "original exploration stays loaded and paused " + phase);
                Check(player.rb.linearVelocity == velocity && player.transform.position == position && ReferenceEquals(moveState, player.playerMoveStateMachine.currentState), "movement state preserved during visit " + phase);
                Check(explorationAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime == animationState.normalizedTime, "exploration animator does not advance during visit " + phase);
                Check(!scenes.ReturnAsync(default).Accepted, "invalid handle rejected during visit " + phase);
                Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.enabled) == 1, "one active listener " + phase);
                yield return null;
                if (i == 0) { ScreenCapture.CaptureScreenshot(SceneFlowSetup.DirectoryPath + "/" + id + ".png"); yield return null; }
                bool exactRestore = false;
                var back = scenes.ReturnAsync(enter.Visit);
                back.OnCompleted(result => exactRestore = result.Succeeded && player != null && player.transform.position == position
                    && player.transform.rotation == rotation && player.rb.linearVelocity == velocity && ReferenceEquals(moveState, player.playerMoveStateMachine.currentState));
                yield return back;
                Check(back.Succeeded && exactRestore, "exact source snapshot at return completion " + phase);
                Check(SceneManager.GetActiveScene() == source && scenes.VisitCount == 0 && !player.IsScenePaused, "source restored and visit released " + phase);
                Check(Mathf.Approximately(Time.timeScale, .7f) && !camera.enabled && !input.inputActions.Player.Zoom.enabled, "original time/camera/action values restored " + phase);
                Check(!scenes.TryGetLoadedScene(id, out _), "target scene unloaded " + phase);
                Check(camera.cullingMask == originalMask && !uiBlocked(), "camera mask and transition input restored " + phase);
                Check(Game.GameEvents.Battle.GetSubscriberCount<Battle.Events.PartyFormationChangedEvent>() == partySubscribers
                    && Game.GameEvents.Character.GetSubscriberCount<CharacterChangedEvent>() == characterSubscribers, "subscriptions released " + phase);
            }
        }
        camera.enabled = cameraEnabled;
        phase = "nested visits";
        var party = scenes.EnterVisitAsync(SceneId.Party, new ScenePresentationLifecycle<PartySceneController>());
        yield return party;
        var partyController = UnityEngine.Object.FindFirstObjectByType<PartySceneController>();
        var partyManager = BattlePartyManager.Instance;
        var owned = PlayerRuntime.Instance.Characters.OwnedCharacters.First();
        Check(partyManager.AddOrReplace(owned, 0), "party can be edited");
        var partyAnimator = partyController.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.runtimeAnimatorController != null);
        // The preview anchors may be siblings of the controller.
        partyAnimator ??= party.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Animator>(true)).FirstOrDefault(a => a.runtimeAnimatorController != null);
        if (partyAnimator == null)
        {
            notes.Add("Existing party capsule prefabs contain no Animator; an in-memory animated probe exercises the scene pause policy");
            partyAnimator = CreateAnimationProbe(partyController.transform);
        }
        Check(partyAnimator.updateMode == AnimatorUpdateMode.UnscaledTime, "preview animator uses unscaled time");
        yield return new WaitForSecondsRealtime(.1f);
        float animationBefore = partyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        yield return new WaitForSecondsRealtime(.1f);
        Check(partyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime != animationBefore, "party animation advances while timeScale is zero");
        ScreenCapture.CaptureScreenshot(SceneFlowSetup.DirectoryPath + "/PartyWithCharacter.png");
        yield return null;
        var partyPanel = UnityEngine.Object.FindFirstObjectByType<BattlePartyCanvasUI>();
        partyPanel.AddOrReplaceAlly(0);
        yield return null; yield return null;
        Check(UIManager.Instance.Back() && scenes.VisitCount == 1, "Esc closes party child before returning scene");
        yield return new WaitForSecondsRealtime(.3f);
        animationBefore = partyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        var character = scenes.EnterVisitAsync(SceneId.Character, new ScenePresentationLifecycle<CharacterSceneController>());
        yield return character;
        Check(character.Succeeded && scenes.VisitCount == 2, "nested character initialized");
        Check(!partyAnimator.enabled && partyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime == animationBefore, "nested source preview stops updating");
        Check(!scenes.ReturnAsync(party.Visit).Accepted, "non-top return rejected");
        yield return scenes.ReturnAsync(character.Visit);
        Check(scenes.VisitCount == 1 && Mathf.Approximately(Time.timeScale, 0) && player.IsScenePaused, "nested return keeps exploration paused");
        Check(partyController != null && BattlePartyManager.Instance == partyManager && partyManager.Formation.members.Count > 0, "same party scene and formation restored");
        Check(partyAnimator.enabled, "nested source animator restored");
        yield return scenes.ReturnAsync(party.Visit);

        phase = "backpack and cultivation";
        var ui = UIManager.Instance;
        var backpack = ui.Open(UIId.Backpack, ownerSceneHandle: source.handle);
        yield return ScenePresentationLifecycle<CharacterSceneController>.WaitForPanel(ui, backpack);
        character = scenes.EnterVisitAsync(SceneId.Character, new ScenePresentationLifecycle<CharacterSceneController>());
        yield return character;
        var service = PlayerRuntime.Instance.Characters;
        var progress = service.GetCharacter(owned.id);
        // A real preview/commit in the isolated slot; skip no business data back into the user's profile.
        var request = new CharacterRequest { operation = CharacterOperation.Level, unitId = owned.id, materialId = "character_exp", quantity = 1 };
        var preview = service.Preview(request);
        Check(preview.Success && service.TryCommit(preview, out _), "cultivation preview and commit");
        int afterLevel = service.GetCharacter(owned.id).level;
        Check(afterLevel > progress.level, "cultivation actually changes level");
        CharacterPreview equip = null;
        string equipmentId = null;
        foreach (var item in service.Inventory.CreateSnapshot().equipment)
        {
            var candidate = service.Preview(new CharacterRequest { operation = CharacterOperation.Equip, unitId = owned.id, slot = EquipmentSlot.LightCone, equipmentId = item.instanceId });
            if (!candidate.Success) continue;
            equip = candidate; equipmentId = item.instanceId; break;
        }
        Check(equip != null && service.TryCommit(equip, out _), "equipment actually equipped");
        var modelController = UnityEngine.Object.FindFirstObjectByType<CharacterSceneController>();
        var model = modelController.GetComponentInChildren<Renderer>();
        Check(model != null, "character model exists");
        Quaternion modelRotation = model.transform.rotation;
        modelController.Rotate(40);
        Check(model.transform.rotation != modelRotation, "character rotation works while paused");
        yield return scenes.ReturnAsync(character.Visit);
        Check(service.GetCharacter(owned.id).level == afterLevel && service.GetCharacter(owned.id).equipment[0] == equipmentId, "cultivation and equipment survive return");
        Check(ui.TryGetPanel<UIPanel>(backpack, out var original) && !original.IsPresentationSuspended && ui.Input.Current.BlockCamera, "backpack instance and restriction restored");
        ui.Close(backpack); yield return null; yield return null;

        phase = "initialization failure cleanup";
        var broken = scenes.EnterVisitAsync(SceneId.Character, new BrokenLifecycle());
        yield return broken;
        Check(!broken.Succeeded && !scenes.IsLoading && scenes.VisitCount == 0 && source.isLoaded && !player.IsScenePaused, "failed initialization cleans target and resumes source");
        Check(!scenes.TryGetLoadedScene(SceneId.Character, out _), "failed target unloaded");

        phase = "missing controller cleanup";
        var missing = scenes.EnterVisitAsync(SceneId.Party, new ScenePresentationLifecycle<CharacterSceneController>());
        yield return missing;
        Check(!missing.Succeeded && scenes.VisitCount == 0 && !player.IsScenePaused && !scenes.TryGetLoadedScene(SceneId.Party, out _), "wrong or missing entry controller cleans created scene");

        phase = "unload failure and retry";
        party = scenes.EnterVisitAsync(SceneId.Party, new ScenePresentationLifecycle<PartySceneController>());
        yield return party;
        SetUnloadFailure(scenes, true);
        var refused = scenes.ReturnAsync(party.Visit); yield return refused;
        Check(!refused.Succeeded && scenes.CurrentVisit.Equals(party.Visit) && party.Scene.isLoaded && player.IsScenePaused, "failed unload retains session and source pause");
        Check(SceneManager.GetActiveScene() == party.Scene && !UnityEngine.Object.FindFirstObjectByType<BattlePartyCanvasUI>().IsPresentationSuspended, "failed unload restores target presentation");
        SetUnloadFailure(scenes, false);
        yield return scenes.ReturnAsync(party.Visit);
        Check(scenes.VisitCount == 0 && !player.IsScenePaused, "unload retry restores source");

        phase = "initialization plus cleanup failure";
        SetUnloadFailure(scenes, true);
        broken = scenes.EnterVisitAsync(SceneId.Character, new BrokenLifecycle()); yield return broken;
        Check(!broken.Succeeded && scenes.HasFaultedVisit && scenes.VisitCount == 1 && player.IsScenePaused, "cleanup failure retains recovery ownership");
        Check(!scenes.LoadAsync(SceneId.Battle).Accepted && !scenes.EnterVisitAsync(SceneId.Party, new ScenePresentationLifecycle<PartySceneController>()).Accepted, "faulted session rejects unrelated requests");
        SetUnloadFailure(scenes, false);
        var retry = scenes.GetComponentsInChildren<Button>(true).Single(b => b.gameObject.name == "Retry");
        retry.onClick.Invoke();
        while (scenes.IsLoading) yield return null;
        Check(scenes.VisitCount == 0 && !scenes.HasFaultedVisit && !player.IsScenePaused && !uiBlocked(), "recovery overlay retry releases all ownership");

        phase = "business prepare rollback";
        party = scenes.EnterVisitAsync(SceneId.Party, new ScenePresentationLifecycle<PartySceneController>()); yield return party;
        int businessState = 0;
        var preparation = scenes.ReplaceFromVisitAsync(party.Visit, SceneId.Battle, new SceneLoadOptions
        { Prepare = () => { businessState = 1; throw new InvalidOperationException("Injected prepare failure"); }, Rollback = () => businessState = 0 });
        yield return preparation;
        Check(preparation.Accepted && !preparation.Succeeded && businessState == 0 && party.Scene.isLoaded && scenes.VisitCount == 1, "accepted preparation failure rolls back business without unloading party");
        yield return scenes.ReturnAsync(party.Visit);

        phase = "exploration party to battle";
        Check(PartySceneNavigator.TryEnter(SceneTransitionMode.Immediate), "party business entry accepted");
        while (scenes.IsLoading) yield return null;
        partyController = UnityEngine.Object.FindFirstObjectByType<PartySceneController>();
        Check(BattlePartyManager.Instance.Formation.members.Count > 0, "formation survives closed party");
        var battleField = typeof(PartySceneController).GetField("battleScene", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var originalBattle = battleField.GetValue(partyController);
        battleField.SetValue(partyController, SceneId.None);
        partyController.StartBattle();
        partyPanel = UnityEngine.Object.FindFirstObjectByType<BattlePartyCanvasUI>();
        Check(!scenes.IsLoading && BattleSessionContext.Current == null && ReadObject<Button>(partyPanel, "startBattleButton").interactable, "rejected battle request restores start button without context mutation");
        battleField.SetValue(partyController, originalBattle);
        partyController.StartBattle();
        while (scenes.IsLoading) yield return null;
        yield return null;
        Check(scenes.VisitCount == 0 && !source.isLoaded && BattleManager.Instance != null && BattleManager.Instance.battleFlow != null, "battle activated and presentation sessions ended");
        Check(Mathf.Approximately(Time.timeScale, .7f), "battle does not inherit paused time");
        var session = BattleSessionContext.Current;
        Check(session != null && session.BattleScene == SceneId.Battle, "battle session retained");
        phase = "battle reformation";
        yield return EndBattle();
        var resultPanel = UnityEngine.Object.FindFirstObjectByType<GameOverCanvasUI>();
        ReadObject<Toggle>(resultPanel, "reformPartyToggle").isOn = true;
        var sessionController = UnityEngine.Object.FindFirstObjectByType<BattleSessionController>();
        var partyField = typeof(BattleSessionController).GetField("partyScene", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var originalParty = partyField.GetValue(sessionController);
        partyField.SetValue(sessionController, SceneId.None);
        ReadObject<Button>(resultPanel, "retryButton").onClick.Invoke();
        Check(!scenes.IsLoading && !BattleSessionContext.Current.IsReforming && ReadObject<Button>(resultPanel, "retryButton").interactable, "rejected result navigation restores button and original battle context");
        partyField.SetValue(sessionController, originalParty);
        ReadObject<Button>(resultPanel, "retryButton").onClick.Invoke();
        while (scenes.IsLoading) yield return null;
        Check(scenes.TryGetLoadedScene(SceneId.Party, out _) && !scenes.TryGetLoadedScene(SceneId.Sample, out _) && BattleSessionContext.Current.IsReforming, "actual result button opens standalone reformation");
        UnityEngine.Object.FindFirstObjectByType<PartySceneController>().StartBattle();
        while (scenes.IsLoading) yield return null;
        yield return null;
        Check(BattleManager.Instance != null && BattleSessionContext.Current.BattleScene == session.BattleScene, "reformation challenges original stage");
        yield return EndBattle();
        resultPanel = UnityEngine.Object.FindFirstObjectByType<GameOverCanvasUI>();
        var firstBattle = BattleManager.Instance;
        ReadObject<Button>(resultPanel, "retryButton").onClick.Invoke();
        while (scenes.IsLoading) yield return null;
        yield return null;
        Check(BattleManager.Instance != null && BattleManager.Instance != firstBattle, "actual retry button reloads battle");
        yield return EndBattle();
        resultPanel = UnityEngine.Object.FindFirstObjectByType<GameOverCanvasUI>();
        ReadObject<Button>(resultPanel, "exitButton").onClick.Invoke();
        while (scenes.IsLoading) yield return null;
        Check(scenes.TryGetLoadedScene(SceneId.Sample, out _) && BattleSessionContext.Current == null && Mathf.Approximately(Time.timeScale, .7f), "actual result exit reloads exploration and clears battle session");

        phase = "cancel post-battle reformation";
        yield return scenes.LoadAsync(SceneId.Battle);
        yield return EndBattle();
        resultPanel = UnityEngine.Object.FindFirstObjectByType<GameOverCanvasUI>();
        ReadObject<Toggle>(resultPanel, "reformPartyToggle").isOn = true;
        ReadObject<Button>(resultPanel, "retryButton").onClick.Invoke();
        while (scenes.IsLoading) yield return null;
        Check(UIManager.Instance.Back(), "reformation return accepted through UIManager");
        while (scenes.IsLoading) yield return null;
        Check(scenes.TryGetLoadedScene(SceneId.Sample, out _) && BattleSessionContext.Current == null, "canceling reformation clears battle context and returns to exploration");

        phase = "direct presentation fallback";
        foreach (SceneId id in new[] { SceneId.Character, SceneId.Party })
        {
            var direct = scenes.LoadAsync(id); yield return direct;
            Check(direct.Succeeded && direct.Visit.IsValid, "direct presentation adopted " + id);
            var back = scenes.ReturnAsync(direct.Visit); yield return back;
            Check(back.Succeeded && scenes.TryGetLoadedScene(SceneId.Sample, out _) && scenes.VisitCount == 0, "direct return loads Sample " + id);
        }
        phase = "low level additive API";
        var lowSource = SceneManager.GetActiveScene();
        var additive = scenes.LoadAsync(SceneId.Party, new SceneLoadOptions { LoadMode = LoadSceneMode.Additive, SetActive = false });
        yield return additive;
        Check(additive.Succeeded && SceneManager.GetActiveScene() == lowSource && scenes.VisitCount == 0, "raw additive respects SetActive false and creates no implicit visit");
        Check(scenes.TrySetActiveScene(additive.Scene, out _) && scenes.TrySetActiveScene(lowSource, out _), "checked active scene changes work");
        var unloaded = scenes.UnloadAsync(additive.Scene); yield return unloaded;
        Check(unloaded.Succeeded && !additive.Scene.isLoaded, "low level unload waits for completion");
        Time.timeScale = oldTime;
        phase = "completed";
    }
    private static IEnumerator RunStandalone(SceneId start)
    {
        phase = "direct Editor startup " + start;
        var scenes = GameSceneManager.Instance;
        while (scenes.IsLoading) yield return null;
        Check(scenes.TryGetLoadedScene(start, out var scene) && scene == SceneManager.GetActiveScene() && SceneManager.sceneCount == 1, "Editor bootstraps target as the only scene");
        if (start == SceneId.Battle)
        {
            Check(BattleManager.Instance != null && BattleManager.Instance.battleFlow != null && BattleSessionContext.Current != null, "standalone battle initializes from Inspector party");
            yield return EndBattle();
            ReadObject<Button>(UnityEngine.Object.FindFirstObjectByType<GameOverCanvasUI>(), "exitButton").onClick.Invoke();
        }
        else
        {
            Check(scenes.TryGetVisit(start, out _) && scenes.VisitCount == 1, "Start adopts direct presentation without a source");
            Check(UIManager.Instance.Back(), "Esc returns direct presentation");
        }
        while (scenes.IsLoading) yield return null;
        Check(scenes.TryGetLoadedScene(SceneId.Sample, out _) && scenes.VisitCount == 0 && Mathf.Approximately(Time.timeScale, 1f), "direct startup returns to Sample and restores time");
        phase = "completed";
    }
    private static bool uiBlocked() => UIManager.Instance.Input.Current.BlockMovement;
    private static Animator CreateAnimationProbe(Transform parent)
    {
        var clip = new AnimationClip { name = "SceneFlowVerificationClip" };
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 1, .1f));
        var controller = new UnityEditor.Animations.AnimatorController { name = "SceneFlowVerificationController" };
        controller.AddLayer("Base");
        var state = controller.layers[0].stateMachine.AddState("Motion");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        var probe = new GameObject("SceneFlowAnimationProbe").AddComponent<Animator>();
        probe.transform.SetParent(parent, false);
        probe.runtimeAnimatorController = controller;
        probe.updateMode = AnimatorUpdateMode.UnscaledTime;
        probe.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animationFixtures.Add(controller); animationFixtures.Add(clip);
        return probe;
    }
    private static void SetUnloadFailure(GameSceneManager scenes, bool fail)
        => typeof(GameSceneManager).GetField("RejectUnloadForVerification", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(scenes, fail ? new Func<Scene, bool>(_ => true) : null);
    private static T ReadObject<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object
        => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    private static IEnumerator EndBattle()
    {
        foreach (var actor in UnityEngine.Object.FindObjectsByType<BattleUnitActor>(FindObjectsSortMode.None))
            if (actor.Unit != null && actor.Unit.faction == Faction.Enemy) actor.Unit.currentHealth = 0;
        BattleManager.Instance.battleFlow.Step();
        yield return null;
        var panel = UnityEngine.Object.FindFirstObjectByType<GameOverCanvasUI>();
        Check(panel != null, "battle completion opens real result panel");
        yield return ScenePresentationLifecycle<PartySceneController>.WaitForPanel(UIManager.Instance, panel.Handle);
    }
    private static bool HasDisplayCamera(Scene scene) => scene.IsValid() && scene.isLoaded
        && scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))
            .Any(c => c.isActiveAndEnabled && c.targetTexture == null);

    private static IEnumerator ObserveCameraCoverage(SceneOperation operation)
    {
        while (!operation.IsDone)
        {
            if (!Camera.allCameras.Any(c => c.isActiveAndEnabled && c.targetTexture == null))
                throw new InvalidOperationException(phase + ": a transition frame has no display camera");
            yield return null;
        }
        Check(Camera.allCameras.Any(c => c.isActiveAndEnabled && c.targetTexture == null), "display camera present at operation completion " + phase);
    }

    private static IEnumerator VerifyCameraHandoffs(Scene source)
    {
        var scenes = GameSceneManager.Instance;
        Check(HasDisplayCamera(source), "source has a display camera before handoff regression");
        foreach (var id in new[] { SceneId.Character, SceneId.Party })
        foreach (var mode in new[] { SceneTransitionMode.Immediate, SceneTransitionMode.Fade })
        {
            phase = "camera handoff " + id + " " + mode;
            var renderedScenes = new HashSet<int>();
            void Rendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
            { if (camera.cameraType == CameraType.Game && camera.targetTexture == null) renderedScenes.Add(camera.gameObject.scene.handle); }
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += Rendered;
            try
            {
                ISceneVisitLifecycle inner = id == SceneId.Character
                    ? new ScenePresentationLifecycle<CharacterSceneController>()
                    : new ScenePresentationLifecycle<PartySceneController>();
                var delayed = new DelayedPresentationLifecycle(inner, source);
                var enter = scenes.EnterVisitAsync(id, delayed, transition: mode);
                yield return ObserveCameraCoverage(enter);
                Check(enter.Succeeded, "camera handoff enter succeeds " + phase + " " + enter.Error);
                Check(!HasDisplayCamera(source) && HasDisplayCamera(enter.Scene), "camera ownership transfers only after initialization " + phase);
                yield return null;
                Check(renderedScenes.Contains(source.handle) && renderedScenes.Contains(enter.Scene.handle), "source and destination actually render during handoff " + phase);

                if (mode == SceneTransitionMode.Immediate)
                {
                    SetUnloadFailure(scenes, true);
                    var refused = scenes.ReturnAsync(enter.Visit);
                    yield return ObserveCameraCoverage(refused);
                    Check(!refused.Succeeded && !HasDisplayCamera(source) && HasDisplayCamera(enter.Scene), "failed return restores destination camera ownership " + phase);
                    SetUnloadFailure(scenes, false);
                }

                var back = scenes.ReturnAsync(enter.Visit);
                yield return ObserveCameraCoverage(back);
                Check(back.Succeeded && HasDisplayCamera(source), "return camera covers target unloading " + phase);
            }
            finally
            {
                SetUnloadFailure(scenes, false);
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Rendered;
            }
        }
    }

    private sealed class DelayedPresentationLifecycle : ISceneVisitLifecycle
    {
        private readonly ISceneVisitLifecycle inner;
        private readonly Scene source;
        public DelayedPresentationLifecycle(ISceneVisitLifecycle inner, Scene source)
        { this.inner = inner; this.source = source; }
        public ISceneSuspension SuspendSource(Scene scene) => inner.SuspendSource(scene);
        public IDisposable SuspendTarget(Scene scene) => inner.SuspendTarget(scene);
        public void ReleaseTarget(Scene scene) => inner.ReleaseTarget(scene);
        public IEnumerator InitializeTarget(Scene target, SceneVisitHandle visit, object context)
        {
            yield return new WaitForSecondsRealtime(.15f);
            Check(HasDisplayCamera(source) && !HasDisplayCamera(target), "source keeps rendering while target initialization is delayed " + phase);
            yield return inner.InitializeTarget(target, visit, context);
            Check(HasDisplayCamera(source) && !HasDisplayCamera(target), "source keeps rendering until target UI is ready " + phase);
        }
    }

    private sealed class BrokenLifecycle : ISceneVisitLifecycle
    {
        private readonly ISceneVisitLifecycle inner = new ScenePresentationLifecycle<CharacterSceneController>();
        public ISceneSuspension SuspendSource(Scene source) => inner.SuspendSource(source);
        public IDisposable SuspendTarget(Scene target) => inner.SuspendTarget(target);
        public void ReleaseTarget(Scene target) => inner.ReleaseTarget(target);
        public IEnumerator InitializeTarget(Scene target, SceneVisitHandle visit, object context)
        { yield return null; throw new InvalidOperationException("Injected initialization failure"); }
    }
}
