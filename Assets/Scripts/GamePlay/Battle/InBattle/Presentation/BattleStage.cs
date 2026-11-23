using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battle.Presentation
{
    /// <summary>A separate cinematic set. Prefab must contain visual components only, no gameplay scripts/cameras.</summary>
    public sealed class BattleStage : MonoBehaviour
    {
        public Transform casterAnchor;
        public Transform targetAnchor;
        [Range(8, 31)] public int stageLayer = 30;

        public static GameObject CloneVisual(Transform source, Transform parent)
        {
            var mapping = new Dictionary<Transform, Transform>();
            Transform CopyTree(Transform original, Transform destination)
            {
                var node = new GameObject(original.name).transform;
                node.SetParent(destination, false);
                node.localPosition = original.localPosition;
                node.localRotation = original.localRotation;
                node.localScale = original.localScale;
                mapping.Add(original, node);
                foreach (Transform child in original) CopyTree(child, node);
                return node;
            }
            Transform root = CopyTree(source, parent);
            root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity;
            foreach (var entry in mapping)
            {
                var mesh = entry.Key.GetComponent<MeshFilter>();
                var renderer = entry.Key.GetComponent<MeshRenderer>();
                if (mesh != null && renderer != null)
                {
                    entry.Value.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                    var copy = entry.Value.gameObject.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = renderer.sharedMaterials; copy.enabled = renderer.enabled;
                }
                var skinned = entry.Key.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null)
                {
                    var copy = entry.Value.gameObject.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = skinned.sharedMesh; copy.sharedMaterials = skinned.sharedMaterials;
                    copy.enabled = skinned.enabled;
                    copy.localBounds = skinned.localBounds; copy.updateWhenOffscreen = true;
                    copy.rootBone = skinned.rootBone != null && mapping.TryGetValue(skinned.rootBone, out var bone) ? bone : root;
                    var bones = new Transform[skinned.bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                        if (skinned.bones[i] != null) mapping.TryGetValue(skinned.bones[i], out bones[i]);
                    copy.bones = bones;
                    if (skinned.sharedMesh != null) for (int i = 0; i < skinned.sharedMesh.blendShapeCount; i++)
                        copy.SetBlendShapeWeight(i, skinned.GetBlendShapeWeight(i));
                }
                var animator = entry.Key.GetComponent<Animator>();
                if (animator != null)
                {
                    var copy = entry.Value.gameObject.AddComponent<Animator>();
                    copy.avatar = animator.avatar; copy.applyRootMotion = false;
                    copy.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }
            if (root.GetComponentInChildren<Animator>() == null) root.gameObject.AddComponent<Animator>();
            return root.gameObject;
        }
        public static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }
        public bool IsSafe(out string error)
        {
            foreach (var component in GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || component is Renderer || component is MeshFilter
                    || component is Animator || component is Light || component is BattleStage) continue;
                error = "Stage contains unsupported component: " + component.GetType().Name; return false;
            }
            error = null;
            return true;
        }
    }
}
