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
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public static class BattleSkillVerification
{
    private static readonly List<string> checks = new();
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
    [MenuItem("Tools/Battle/Presentation/Verify Skill Templates")]
    public static void Run()
    {
        checks.Clear(); string error = null;
        try
        {
            BattleSkillSamples.Create();
            AuthoringRoundTrip();
            FollowingProjectile();
            foreach (var template in new[] { BattleSkillTemplate.MeleeApproach, BattleSkillTemplate.Ranged, BattleSkillTemplate.Support, BattleSkillTemplate.Staged })
            { Scenario(template, false); Scenario(template, true); }
            for (int a = 0; a < 4; a++) for (int b = 0; b < 5; b++) Scenario(BattleSkillTemplate.MeleeApproach, false, a, b);
            Scenario(BattleSkillTemplate.MeleeApproach, false, controller: true);
            Scenario(BattleSkillTemplate.Staged, false, isolated: true);
            Scenario(BattleSkillTemplate.Staged, true, isolated: true);
        }
        catch (Exception e) { error = e.ToString(); Debug.LogException(e); }
        finally
        {
            Directory.CreateDirectory("Temp/BattlePresentation");
            File.WriteAllText("Temp/BattlePresentation/skills.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = error == null, error, checkCount = checks.Count, checks, completedAt = DateTimeOffset.Now }, Newtonsoft.Json.Formatting.Indented));
        }
        if (error != null) throw new InvalidOperationException(error);
        Debug.Log("Skill template verification passed: " + checks.Count);
    }
    private static void AuthoringRoundTrip()
    {
        string path = "Assets/__BattleSkillVerification_" + Guid.NewGuid().ToString("N") + ".playable";
        try
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, path);
            var track = timeline.CreateTrack<BattleStagePoseTrack>(null, "Stage movement"); var clip = track.CreateClip<BattleStagePoseClip>(); clip.duration = 2;
            var so = new SerializedObject(clip.asset); var pose = so.FindProperty("pose");
            pose.FindPropertyRelative("toPosition").vector3Value = new Vector3(2, 0, 4);
            pose.FindPropertyRelative("lateral").animationCurveValue = AnimationCurve.Linear(0, 1, 1, 1);
            pose.FindPropertyRelative("height").animationCurveValue = AnimationCurve.Linear(0, 2, 1, 2);
            pose.FindPropertyRelative("forward").animationCurveValue = AnimationCurve.Linear(0, 3, 1, 3);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(track); EditorUtility.SetDirty(clip.asset); AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            var loadedClip = loaded.GetRootTracks().OfType<BattleStagePoseTrack>().Single().GetClips().Single().asset as BattleStagePoseClip;
            Check(loadedClip != null, "stage movement track and clip survive saved asset reimport");
            var graph = PlayableGraph.Create("Stage serialization verification");
            try
            {
                var playable = (ScriptPlayable<BattleStagePoseBehaviour>)loadedClip.CreatePlayable(graph, null);
                Check(Vector3.Distance(playable.GetBehaviour().Evaluate(.5f).position, new Vector3(2, 2, 5)) < .001f, "saved stage curves preserve authored lateral height and forward movement");
            }
            finally { graph.Destroy(); }
        }
        finally { AssetDatabase.DeleteAsset(path); }
    }
    private static void FollowingProjectile()
    {
        var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene); BattleSequenceDefinition definition = null; BattleActionPresentationSession session = null;
        try
        {
            var caster = new GameObject("Caster").AddComponent<BattleUnitActor>();
            var target = new GameObject("Target").AddComponent<BattleUnitActor>();
            var anchors = target.gameObject.AddComponent<BattleActorPresentation>();
            var hit = new GameObject("Hit").transform; hit.SetParent(target.transform); hit.localPosition = Vector3.up; anchors.ConfigureAnchors(null, hit, null, null);
            target.transform.position = Vector3.forward * 10; target.transform.rotation = Quaternion.Euler(0, 90, 0);
            definition = ScriptableObject.CreateInstance<BattleSequenceDefinition>(); var so = new SerializedObject(definition);
            so.FindProperty("impactOffset").vector3Value = Vector3.forward;
            var projectile = so.FindProperty("projectile"); projectile.FindPropertyRelative("seconds").floatValue = 1;
            projectile.FindPropertyRelative("followTarget").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
            session = new BattleActionPresentationSession(null, definition, null, scene);
            Vector3 start = BattleActionPresentationSession.Point(caster, BattleAnchor.Cast, Vector3.zero); int arrivals = 0;
            session.Launch(caster, target, () => arrivals++);
            target.transform.position += Vector3.right * 2; target.transform.rotation = Quaternion.Euler(0, 180, 0);
            session.Tick(.5f);
            var flight = scene.GetRootGameObjects().Single(g => g.name == "Battle projectile");
            var expected = Vector3.Lerp(start, BattleActionPresentationSession.Point(target, BattleAnchor.Body, Vector3.forward), .5f);
            Check(Vector3.Distance(flight.transform.position, expected) < .001f, "following projectile preserves local anchor offset as target moves and turns");
            session.Tick(.5f); Check(arrivals == 1 && !session.HasFlights, "following projectile arrives once and releases its blocking wait");
        }
        finally
        {
            session?.Dispose(); if (definition != null) Object.DestroyImmediate(definition);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true);
        }
    }
    private sealed class Lookup : IBattleActorLookup
    {
        public readonly Dictionary<BattleUnit, BattleUnitActor> Map = new();
        public bool TryGetActor(BattleUnit unit, out BattleUnitActor actor) { actor = null; return unit != null && Map.TryGetValue(unit, out actor); }
    }
    private static void Scenario(BattleSkillTemplate template, bool skip, int allySlot = 0, int targetSlot = 0, bool controller = false, bool isolated = false)
    {
        var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        GameObject root = null; SkillConfig skill = null; BattleSequenceDefinition definitionCopy = null;
        try
        {
            root = new GameObject("SkillVerification"); SceneManager.MoveGameObjectToScene(root, scene);
            var layout = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(BattlePresentationSetup.CameraRoot + "/DefaultLayout.asset");
            var field = root.AddComponent<BattlefieldDirector>(); field.Configure(layout, root.transform);
            var lookup = new Lookup(); var units = new List<BattleUnit>();
            BattleUnit Unit(Faction faction, int slot, int speed) => new() { faction = faction, pos = slot, displayName = faction + " " + slot, maxHealth = 1000, currentHealth = 1000, attack = 100, speed = speed, maxEnergy = 100 };
            var caster = Unit(Faction.OurSide, allySlot, 200); var support = Unit(Faction.OurSide, (allySlot + 1) % 4, 10);
            units.Add(caster); units.Add(support); for (int i = 0; i < 5; i++) units.Add(Unit(Faction.Enemy, i, 10));
            skill = ScriptableObject.CreateInstance<SkillConfig>(); skill.id = -9109; skill.Name = "Verification"; skill.targetSide = template == BattleSkillTemplate.Support ? TargetSide.OurSide : TargetSide.OtherSide; skill.targetRule = TargetRule.AoE;
            skill.effectConfigs = new List<EffectConfig> { new() { effectType = template == BattleSkillTemplate.Support ? EffectType.Heal : EffectType.Damage, scaleStat = StatType.Attack, value = 1 } }; caster.skills.Add(skill);
            foreach (var unit in units)
            {
                var identity = new GameObject(unit.displayName); identity.transform.SetParent(root.transform, false);
                var movement = new GameObject("PresentationRoot").transform; movement.SetParent(identity.transform, false);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleSkillSamples.Folder + (unit.faction == Faction.OurSide ? "/Caster.prefab" : "/Target.prefab"));
                var model = Object.Instantiate(prefab, movement); var actor = model.GetComponent<BattleUnitActor>(); actor.ConfigurePresentationRoot(movement, model.transform); actor.Bind(unit); lookup.Map.Add(unit, actor); field.Register(actor, unit.faction == Faction.OurSide ? 4 : 5);
            }
            var flow = new BattleFlow(units); flow.Ready(); var coordinator = root.AddComponent<BattlePresentationCoordinator>(); coordinator.ConfigureBattlefield(field);
            coordinator.timelineDirector = root.AddComponent<PlayableDirector>(); coordinator.timelineDirector.playOnAwake = false;
            var shots = root.AddComponent<BattleShotDirector>(); coordinator.cameraDirector = shots; shots.ConfigureBattlefield(field);
            var output = new GameObject("Output"); output.transform.SetParent(root.transform); shots.outputCamera = output.AddComponent<Camera>(); shots.outputCamera.enabled = false; shots.brain = output.AddComponent<CinemachineBrain>();
            shots.normalCamera = new GameObject("Normal").AddComponent<CinemachineCamera>(); shots.normalCamera.transform.SetParent(root.transform);
            shots.cinematicCamera = new GameObject("Cinematic").AddComponent<CinemachineCamera>(); shots.cinematicCamera.transform.SetParent(root.transform);
            shots.profile = AssetDatabase.LoadAssetAtPath<BattleCameraProfile>(BattlePresentationSetup.CameraRoot + "/DefaultCamera.asset");
            coordinator.Bind(flow, lookup);
            var definition = AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(BattleSkillSamples.Folder + "/" + template + ".asset");
            Check(definition != null && definition.timeline != null, template + " has authored timeline binding");
            definitionCopy = Object.Instantiate(definition); definition = definitionCopy;
            var definitionSO = new SerializedObject(definition);
            if (controller) definitionSO.FindProperty("animation").FindPropertyRelative("driver").enumValueIndex = (int)BattleBodyDriver.AnimatorController;
            if (isolated) definitionSO.FindProperty("stagePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(BattlePresentationSetup.Root + "/SampleSeparateStage.asset").stagePrefab;
            if (template == BattleSkillTemplate.Ranged) definitionSO.FindProperty("projectile").FindPropertyRelative("seconds").floatValue = 1.2f;
            definitionSO.ApplyModifiedPropertiesWithoutUndo();
            coordinator.skills = new[] { new BattleSkillSequence { casterFaction = Faction.OurSide, skillId = skill.id, sequence = definition } };
            var target = template == BattleSkillTemplate.Support ? support : units.First(u => u.faction == Faction.Enemy && u.pos == targetSlot);
            var start = lookup.Map[caster].PresentationRoot.position; var targetStart = lookup.Map[target].PresentationRoot.position; int logical = caster.pos; int finished = 0; int presented = 0;
            coordinator.EffectPresented += _ => presented++;
            var result = flow.TryExecute(new BattleAction { caster = caster, skill = skill, mainTarget = target });
            Check(result.Succeeded && result.Resolution.Record.PrimaryTargets.Contains(target), template + " records real primary target");
            if (template == BattleSkillTemplate.Support) Check(result.Resolution.Record.ResolvedTargets[0].Targets.Count == 2, "full-health support retains both recipients without heal records");
            coordinator.Present(result.Resolution, () => finished++); coordinator.Tick(.01f);
            if (template == BattleSkillTemplate.MeleeApproach) Check(coordinator.CurrentPhase == "Approach", "melee approaches before starting attack Timeline");
            if (template == BattleSkillTemplate.Support) Check(lookup.Map[support].WorldPresentationVisible, "support retains full party visibility");
            if (template == BattleSkillTemplate.Staged)
            {
                Check(shots.SequenceCaster == lookup.Map[caster].VisualRoot && shots.SequenceTarget == lookup.Map[target].VisualRoot, "stage binds original actors");
                Check(lookup.Map[target].PresentationRoot.position != targetStart, "stage actually repositions original target");
                if (isolated) Check(lookup.Map[target].gameObject.layer != 0 && shots.outputCamera.cullingMask != -1, "isolated set changes original layers and render mask");
            }
            coordinator.Clock.Paused = true; var beforePause = lookup.Map[caster].PresentationRoot.position; coordinator.Tick(3); Check(lookup.Map[caster].PresentationRoot.position == beforePause, template + " pause freezes root"); coordinator.Clock.Paused = false;
            if (skip) coordinator.SkipCurrent();
            else
            {
                bool sawAttack = false, sawFlight = false, sawReaction = false, sawSupportParticles = false;
                for (int i = 0; i < 1800 && coordinator.Busy; i++)
                {
                    coordinator.Tick(1f / 60);
                    if (template == BattleSkillTemplate.MeleeApproach && coordinator.CurrentPhase == "Main" && !sawAttack)
                    {
                        sawAttack = true; var stand = lookup.Map[target].Presentation.MeleeStand;
                        Check(Vector3.Distance(lookup.Map[caster].PresentationRoot.position, stand.position + stand.rotation * definition.StandOffset) < .001f, "melee reaches authored stand before attack");
                        Check(coordinator.timelineDirector.time < .02, "attack Timeline starts at local zero after variable approach");
                        Check(Mathf.Abs(lookup.Map[caster].Presentation.Animator.GetFloat("ApproachPlaybackRate") - 1) < .001f, "attack does not inherit approach playback rate");
                    }
                    if (template == BattleSkillTemplate.Ranged && coordinator.CurrentPhase == "Waiting" && coordinator.Session?.HasFlights == true && !sawFlight)
                    { sawFlight = true; Check(BattleDisplayState.Health(target) == 1000, "long projectile keeps displayed HP until arrival after Timeline end"); }
                    if (coordinator.Session?.Animation(target)?.Reacting == true) sawReaction = true;
                    if (template == BattleSkillTemplate.Support && coordinator.timelineDirector.time > .5 && coordinator.timelineDirector.time < .65)
                        sawSupportParticles |= scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ParticleSystem>(true)).Any(p => p.particleCount > 0);
                }
                if (template == BattleSkillTemplate.MeleeApproach) { Check(sawAttack, "melee entered attack stage"); Check(sawReaction, "impact starts configured target reaction"); }
                if (template == BattleSkillTemplate.Ranged) Check(sawFlight, "projectile remains blocking after authored sequence");
                if (template == BattleSkillTemplate.Support) Check(sawSupportParticles, "support cue simulates visible particles on full-health recipients");
            }
            Check(!coordinator.Busy && finished == 1, template + " completes once");
            Check(caster.pos == logical && Vector3.Distance(lookup.Map[caster].PresentationRoot.position, start) < .001f, template + " returns home without changing logical slot");
            Check(Vector3.Distance(lookup.Map[target].PresentationRoot.position, targetStart) < .001f, template + " restores target root");
            Check(!shots.HasExclusiveOwner && lookup.Map[support].WorldPresentationVisible, template + " restores camera and world visibility");
            Check(lookup.Map[target].gameObject.layer == 0 && shots.outputCamera.cullingMask == -1, "original layers and camera mask restored");
            Check(Mathf.Approximately(lookup.Map[caster].Presentation.Animator.speed, 1), "Animator speed restored");
            Check(presented == result.Resolution.Record.Effects.Count, template + " consumes every effect once");
            Check(BattleDisplayState.Health(target) == target.currentHealth, template + " synchronizes displayed health");
            coordinator.Tick(1); Check(finished == 1, template + " late tick cannot duplicate completion"); coordinator.Cancel();
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root); if (skill != null) Object.DestroyImmediate(skill); if (definitionCopy != null) Object.DestroyImmediate(definitionCopy);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true);
        }
    }
}
