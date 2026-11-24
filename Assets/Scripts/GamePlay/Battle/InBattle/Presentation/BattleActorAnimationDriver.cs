using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Battle.Presentation
{
    /// <summary>One manual body clock. Controller state rates never alter the attack Timeline clock.</summary>
    public sealed class BattleActorAnimationDriver : IDisposable
    {
        private readonly Animator animator;
        private readonly float originalSpeed;
        private readonly bool rootMotion;
        private readonly AnimatorCullingMode culling;
        private readonly List<(int hash, float time, float weight)> states = new();
        private readonly Dictionary<int, float> floats = new();
        private readonly Dictionary<int, int> integers = new();
        private readonly Dictionary<int, bool> booleans = new();
        private PlayableGraph reaction;
        private AnimationClipPlayable reactionClip;
        private float reactionTime, reactionDuration;
        private string rateParameter;
        private readonly List<(Transform node, Vector3 position, Quaternion rotation, Vector3 scale)> blendPose = new();
        private bool disposed;
        public bool TimelineOwned { get; set; }
        public bool Reacting => reaction.IsValid();
        public Animator Animator => animator;
        public void BeginTimeline()
        {
            blendPose.Clear();
            if (animator != null) foreach (var t in animator.GetComponentsInChildren<Transform>(true))
                blendPose.Add((t, t.localPosition, t.localRotation, t.localScale));
            TimelineOwned = true;
        }
        public void BlendTimeline(float time, float seconds)
        {
            if (seconds <= 0 || time >= seconds) return;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / seconds));
            foreach (var pose in blendPose) if (pose.node != null)
            {
                pose.node.localPosition = Vector3.Lerp(pose.position, pose.node.localPosition, t);
                pose.node.localRotation = Quaternion.Slerp(pose.rotation, pose.node.localRotation, t);
                pose.node.localScale = Vector3.Lerp(pose.scale, pose.node.localScale, t);
            }
        }
        public BattleActorAnimationDriver(BattleUnitActor actor)
        {
            animator = actor.Presentation != null ? actor.Presentation.Animator : actor.VisualRoot.GetComponentInChildren<Animator>(true);
            if (animator == null) return;
            originalSpeed = animator.speed; rootMotion = animator.applyRootMotion; culling = animator.cullingMode;
            if (animator.runtimeAnimatorController != null)
            {
                animator.Update(0);
                for (int i = 0; i < animator.layerCount; i++)
                { var state = animator.GetCurrentAnimatorStateInfo(i); states.Add((state.fullPathHash, state.normalizedTime, animator.GetLayerWeight(i))); }
                foreach (var p in animator.parameters)
                    switch (p.type)
                    {
                        case AnimatorControllerParameterType.Float: floats[p.nameHash] = animator.GetFloat(p.nameHash); break;
                        case AnimatorControllerParameterType.Int: integers[p.nameHash] = animator.GetInteger(p.nameHash); break;
                        case AnimatorControllerParameterType.Bool: booleans[p.nameHash] = animator.GetBool(p.nameHash); break;
                    }
            }
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.speed = 0;
        }
        private bool HasFloat(string name)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return false;
            foreach (var p in animator.parameters) if (p.name == name && p.type == AnimatorControllerParameterType.Float) return true;
            return false;
        }
        public bool State(string state, string rate, float value, float blend)
        {
            ClearRate();
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(state)) return false;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) { Debug.LogWarning($"Missing Animator state '{state}' on {animator.name}.", animator); return false; }
            if (HasFloat(rate)) { rateParameter = rate; animator.SetFloat(rate, value); }
            animator.CrossFadeInFixedTime(hash, Mathf.Max(0, blend), 0, 0); return true;
        }
        public void ClearRate()
        {
            if (HasFloat(rateParameter))
            { int hash = Animator.StringToHash(rateParameter); animator.SetFloat(hash, floats.TryGetValue(hash, out float value) ? value : 1); }
            rateParameter = null;
        }
        public void PlayReaction(AnimationClip clip)
        {
            if (animator == null || clip == null || TimelineOwned) return;
            if (reaction.IsValid()) reaction.Destroy();
            reaction = PlayableGraph.Create("Battle reaction"); reaction.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            reactionClip = AnimationClipPlayable.Create(reaction, clip);
            var output = AnimationPlayableOutput.Create(reaction, "Body", animator); output.SetSourcePlayable(reactionClip);
            reactionTime = 0; reactionDuration = clip.length; reaction.Play(); reaction.Evaluate(0);
        }
        public void Tick(float delta)
        {
            if (animator == null || disposed || TimelineOwned) return;
            if (reaction.IsValid())
            {
                reactionTime = Mathf.Min(reactionDuration, reactionTime + delta); reactionClip.SetTime(reactionTime); reaction.Evaluate(0);
                if (reactionTime >= reactionDuration) { reaction.Destroy(); if (animator.runtimeAnimatorController != null) animator.Update(0); }
                return;
            }
            if (animator.runtimeAnimatorController == null) return;
            animator.speed = 1; animator.Update(delta); animator.speed = 0;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (reaction.IsValid()) reaction.Destroy();
            if (animator == null) return;
            ClearRate();
            if (animator.runtimeAnimatorController != null)
            {
                foreach (var pair in floats) animator.SetFloat(pair.Key, pair.Value);
                foreach (var pair in integers) animator.SetInteger(pair.Key, pair.Value);
                foreach (var pair in booleans) animator.SetBool(pair.Key, pair.Value);
                for (int i = 0; i < states.Count; i++)
                { var state = states[i]; if (state.hash != 0) animator.Play(state.hash, i, state.time); animator.SetLayerWeight(i, state.weight); }
                animator.Update(0);
            }
            animator.speed = originalSpeed; animator.applyRootMotion = rootMotion; animator.cullingMode = culling;
        }
    }
}
