using System;
using System.Collections;
using Game.UI;
using UnityEngine;

namespace Game.Characters
{
    /// <summary>
    /// 初始化本场景的模型和必要 UI。数据规则交给养成服务，导航交给场景管理器。
    /// </summary>
    public sealed class CharacterSceneController : MonoBehaviour, ISceneEntryPoint
    {
        [SerializeField]
        private Transform modelAnchor;
        [SerializeField, Min(0.1f)]
        private float modelHeight = 3.4f;
        private GameObject model;
        private UIHandle panelHandle;
        public string DisplayedUnitId { get; private set; }
        public ISceneVisitLifecycle VisitLifecycle =>
            new ScenePresentationLifecycle<CharacterSceneController>(CharacterSceneNavigator.PreviewMask);

        private void Start()
        {
            if (GameSceneManager.Instance != null && GameSceneManager.Instance.IsScenePending(gameObject.scene))
                return;
            CharacterSceneNavigator.AdoptDirectScene(gameObject.scene);
        }

        public IEnumerator InitializeScene(SceneVisitHandle visit, object context)
        {
            PlayerRuntime.EnsureCreated();
            if (!PlayerRuntime.Instance.IsReady)
                throw new InvalidOperationException(PlayerRuntime.Instance.LastMessage);
            var ui = UIManager.Instance;
            if (ui == null)
                throw new InvalidOperationException("UIManager 尚未初始化。");
            var selection = context as CharacterSceneContext ?? new CharacterSceneContext();
            panelHandle = ui.Open(UIId.CharacterDetail,
                new CharacterUIContext(this, selection.UnitId, selection.Tab, selection.EquipmentId),
                ownerSceneHandle: gameObject.scene.handle);
            yield return ScenePresentationLifecycle<CharacterSceneController>.WaitForPanel(ui, panelHandle);
            if (model == null)
                throw new InvalidOperationException("角色展示模型未能初始化。");
        }

        public void Show(UnitConfig config)
        {
            if (config == null || config.prefab == null || config.id == DisplayedUnitId)
                return;
            if (model != null)
                Destroy(model);
            DisplayedUnitId = config.id;
            model = Instantiate(config.prefab, modelAnchor != null ? modelAnchor : transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // 展示尺度与战斗 prefab 相互独立。
            model.transform.localScale = Vector3.one;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                    bounds.Encapsulate(renderer.bounds);
                model.transform.localScale *= modelHeight / Mathf.Max(.01f, bounds.size.y);
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                    bounds.Encapsulate(renderer.bounds);
                model.transform.position += model.transform.parent.position + Vector3.up * modelHeight * .5f - bounds.center;
            }

            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = 8;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        public void Rotate(float pixels)
        {
            if (GameSceneManager.Instance != null && GameSceneManager.Instance.IsSceneSuspended(gameObject.scene))
                return;
            if (model != null)
                model.transform.Rotate(Vector3.up, -pixels * 0.35f, Space.World);
        }

        private void OnDestroy()
        {
            if (UIManager.Instance != null && panelHandle.IsValid)
                UIManager.Instance.Close(panelHandle);
        }
    }

    public sealed class CharacterUIContext : IUIContext
    {
        public CharacterSceneController Controller { get; }
        public string UnitId { get; }
        public CharacterTab Tab { get; }
        public string EquipmentId { get; }

        public CharacterUIContext(CharacterSceneController controller, string id, CharacterTab tab, string equipmentId = null)
        {
            Controller = controller;
            UnitId = id;
            Tab = tab;
            EquipmentId = equipmentId;
        }
    }
}
