using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Battle.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public static class BattleSkillSamples
{
    public const string Folder = "Assets/Config/Battle/Presentation/Skills";
    [MenuItem("Tools/Battle/Presentation/Create Skill Template Samples")]
    public static void Create()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var walk = Clip("Capsule_Approach", true, new[] { 0f, .15f, .3f }, new[] { 1f, 1.08f, 1f }, new[] { 0f, 8f, 0f });
        var hit = Clip("Capsule_Hit", false, new[] { 0f, .08f, .22f }, new[] { .85f, 1.08f, 1f }, new[] { -15f, 6f, 0f });
        var idle = Clip("Capsule_Idle", true, new[] { 0f, .5f }, new[] { 1f, 1f }, new[] { 0f, 0f });
        var controller = Controller(walk, idle);
        var cast = Clip("Capsule_Cast", false, new[] { 0f, .2f, .45f, .8f }, new[] { 1f, .85f, 1.12f, 1f }, new[] { 0f, -12f, 10f, 0f });
        var supportEffect = EffectPrefab("SupportGlow", new Color(.2f, 1, .6f));
        var impactEffect = EffectPrefab("ImpactGlow", new Color(1, .65f, .15f));
        var sample = AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(BattlePresentationSetup.Root + "/SampleBattlefield.asset");
        foreach (var template in new[] { BattleSkillTemplate.MeleeApproach, BattleSkillTemplate.Ranged, BattleSkillTemplate.Support, BattleSkillTemplate.Staged })
        {
            string path = Folder + "/" + template + ".asset"; var definition = AssetDatabase.LoadAssetAtPath<BattleSequenceDefinition>(path);
            if (definition != null && definition.timeline != null)
            {
                if (template == BattleSkillTemplate.MeleeApproach && definition.timeline == sample.timeline)
                { definition.timeline = CopyTimeline(sample.timeline, "MeleeAttack"); EditorUtility.SetDirty(definition); }
                if (template == BattleSkillTemplate.Staged && definition.timeline == AssetDatabase.LoadAssetAtPath<TimelineAsset>(BattlePresentationSetup.Root + "/SampleSeparateStage.playable"))
                { definition.timeline = CopyTimeline(definition.timeline, "StageAttack"); EditorUtility.SetDirty(definition); }
                ConfigureStageCamera(definition);
                if (definition.ImpactPrefab == null)
                {
                    var existingSO = new SerializedObject(definition); existingSO.FindProperty("impactPrefab").objectReferenceValue = template == BattleSkillTemplate.Support ? supportEffect : impactEffect;
                    existingSO.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                }
                EnsureCastTrack(definition, cast);
                continue;
            }
            bool created = definition == null; if (created) definition = ScriptableObject.CreateInstance<BattleSequenceDefinition>();
            if (created) AssetDatabase.CreateAsset(definition, path);
            var so = new SerializedObject(definition); so.FindProperty("template").enumValueIndex = (int)template;
            TimelineAsset selectedTimeline = null;
            if (template == BattleSkillTemplate.MeleeApproach) selectedTimeline = CopyTimeline(sample.timeline, "MeleeAttack");
            else if (template == BattleSkillTemplate.Staged)
            { selectedTimeline = CopyTimeline(AssetDatabase.LoadAssetAtPath<TimelineAsset>(BattlePresentationSetup.Root + "/SampleSeparateStage.playable"), "StageAttack"); }
            else
            {
                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(Folder + "/" + template + ".playable");
                if (timeline == null) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); timeline.name = template + "Timeline"; AssetDatabase.CreateAsset(timeline, Folder + "/" + template + ".playable"); }
                var marker = timeline.markerTrack;
                if (marker == null) { timeline.CreateMarkerTrack(); marker = timeline.markerTrack; }
                var cue = marker.GetMarkers().OfType<BattleImpactMarker>().FirstOrDefault() ?? marker.CreateMarker<BattleImpactMarker>(.45); var cueSO = new SerializedObject(cue);
                cueSO.FindProperty("kind").enumValueIndex = (int)(template == BattleSkillTemplate.Ranged ? BattleCueKind.LaunchGroup : BattleCueKind.ImpactGroup);
                cueSO.ApplyModifiedPropertiesWithoutUndo();
                timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = .8;
                selectedTimeline = timeline; EditorUtility.SetDirty(timeline); AssetDatabase.SaveAssets();
            }
            if (template == BattleSkillTemplate.Ranged) so.FindProperty("projectile").FindPropertyRelative("enabled").boolValue = true;
            so.FindProperty("impactPrefab").objectReferenceValue = template == BattleSkillTemplate.Support ? supportEffect : impactEffect;
            so.ApplyModifiedPropertiesWithoutUndo(); definition.timeline = selectedTimeline; EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
            ConfigureStageCamera(definition);
            EnsureCastTrack(definition, cast);
        }
        // Authored prefabs and clips are not overwritten. Sample prefabs are isolated copies.
        CreateActor("Caster", "Assets/Prefabs/Ally/Stone.prefab", controller, hit, true);
        CreateActor("Target", "Assets/Prefabs/Enemies/enemy.prefab", null, hit, false);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Skill samples ready: " + Folder + ". Select a definition and open Skill Studio. Existing authored assets were preserved.");
    }
    private static TimelineAsset CopyTimeline(TimelineAsset source, string name)
    {
        string path = Folder + "/" + name + ".playable";
        if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(path) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
        return AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
    }
    private static void ConfigureStageCamera(BattleSequenceDefinition definition)
    {
        if (definition.Template != BattleSkillTemplate.Staged || definition.timeline == null || definition.timeline.name == "StageAttackConfigured") return;
        if (AssetDatabase.GetAssetPath(definition.timeline) != Folder + "/StageAttack.playable") return;
        foreach (var track in definition.timeline.GetRootTracks().OfType<BattleCameraShotTrack>()) foreach (var clip in track.GetClips())
        {
            var asset = (BattleCameraShotClip)clip.asset; asset.shot.space = BattleShotSpace.Battlefield;
            asset.shot.fromPosition = new Vector3(4, 3, -7); asset.shot.toPosition = new Vector3(3.8f, 2.8f, -6.8f);
            asset.shot.lookAtTarget = true; asset.shot.fieldOfView = AnimationCurve.Linear(0, 48, 1, 48); asset.shot.keys = Array.Empty<BattleCameraKey>();
            EditorUtility.SetDirty(asset);
        }
        var so = new SerializedObject(definition); so.FindProperty("casterPosition").vector3Value = new Vector3(0, 0, -2.2f); so.ApplyModifiedPropertiesWithoutUndo();
        definition.timeline.name = "StageAttackConfigured"; EditorUtility.SetDirty(definition.timeline); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
    }
    private static void EnsureCastTrack(BattleSequenceDefinition definition, AnimationClip cast)
    {
        if (definition.Template is not (BattleSkillTemplate.Support or BattleSkillTemplate.Ranged) || definition.timeline == null) return;
        if (!definition.timeline.GetRootTracks().OfType<AnimationTrack>().Any())
        {
            var track = definition.timeline.CreateTrack<AnimationTrack>(null, "Caster"); var clip = track.CreateClip(cast); clip.start = 0; clip.duration = .8;
            EditorUtility.SetDirty(track); EditorUtility.SetDirty(definition.timeline); AssetDatabase.SaveAssets();
        }
    }
    private static GameObject EffectPrefab(string name, Color color)
    {
        string path = Folder + "/" + name + ".prefab"; var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        string materialPath = Folder + "/SkillParticles.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath);
        }
        if (existing != null)
        {
            var existingParticles = existing.GetComponent<ParticleSystem>();
            bool enlargeSample = Mathf.Approximately(existingParticles.shape.radius, .35f);
            if (existing.GetComponent<ParticleSystemRenderer>().sharedMaterial == null || enlargeSample)
            {
                var loaded = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    loaded.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                    if (enlargeSample) { var shape = loaded.GetComponent<ParticleSystem>().shape; shape.radius = .8f; }
                    PrefabUtility.SaveAsPrefabAsset(loaded, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(loaded); }
            }
            return existing;
        }
        var go = new GameObject(name); var particles = go.AddComponent<ParticleSystem>();
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        try
        {
            var main = particles.main; main.duration = .8f; main.loop = false; main.startLifetime = .45f; main.startSpeed = .7f; main.startSize = .15f; main.startColor = color; main.playOnAwake = false;
            var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 20) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .8f;
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { Object.DestroyImmediate(go); }
    }
    private static AnimationClip Clip(string name, bool loop, float[] times, float[] height, float[] lean)
    {
        string path = Folder + "/" + name + ".anim"; var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path); if (existing != null) return existing;
        var clip = new AnimationClip { name = name, frameRate = 60 };
        void Curve(string property, float[] values) { var keys = times.Select((t, i) => new Keyframe(t, values[i])).ToArray(); clip.SetCurve("Capsule", typeof(Transform), property, new AnimationCurve(keys)); }
        Curve("localScale.y", height); Curve("localEulerAnglesRaw.x", lean);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path); return clip;
    }
    private static AnimatorController Controller(AnimationClip walk, AnimationClip idle)
    {
        string path = Folder + "/Capsule_Skill.controller"; var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); if (existing != null && existing.layers.Length > 0) return existing;
        var controller = existing != null ? existing : AnimatorController.CreateAnimatorControllerAtPath(path);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer"); var machine = controller.layers[0].stateMachine;
        controller.AddParameter(new AnimatorControllerParameter { name = "ApproachPlaybackRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        controller.AddParameter(new AnimatorControllerParameter { name = "ReturnPlaybackRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        var rest = machine.AddState("Idle"); rest.motion = idle; machine.defaultState = rest;
        foreach (string name in new[] { "Approach", "Return" })
        {
            var state = machine.AddState(name); state.motion = walk; state.speedParameterActive = true; state.speedParameter = name == "Approach" ? "ApproachPlaybackRate" : "ReturnPlaybackRate";
        }
        var attack = machine.AddState("Attack"); attack.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animators/Battle/Allies/Capsule_BasicAttack.anim");
        EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets(); return controller;
    }
    private static void CreateActor(string name, string original, AnimatorController controller, AnimationClip hit, bool caster)
    {
        string path = Folder + "/" + name + ".prefab"; if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(original));
        try
        {
            instance.name = name;
            var actor = instance.GetComponent<BattleUnitActor>();
            if (!caster)
            { var visible = instance.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(); if (visible != null) visible.name = "Capsule"; }
            var animator = instance.GetComponent<Animator>(); if (animator == null) animator = instance.AddComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            var component = instance.GetComponent<BattleActorPresentation>(); if (component == null) component = instance.AddComponent<BattleActorPresentation>();
            Transform Anchor(string label, Vector3 point, Vector3 euler)
            { var t = new GameObject(label).transform; t.SetParent(instance.transform, false); t.localPosition = point; t.localEulerAngles = euler; return t; }
            component.ConfigureAnchors(Anchor("MeleeStand", new Vector3(0, 0, 1.7f), new Vector3(0, 180, 0)), Anchor("Hit", Vector3.up, Vector3.zero), Anchor("Foot", Vector3.zero, Vector3.zero), Anchor("Cast", new Vector3(0, 1, .5f), Vector3.zero));
            var so = new SerializedObject(component); so.FindProperty("bodyAnimator").objectReferenceValue = animator; so.FindProperty("lightHit").objectReferenceValue = hit; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally { Object.DestroyImmediate(instance); }
    }
}
