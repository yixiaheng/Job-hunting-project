using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Characters
{
    /// <summary>仅管理展示模型。数据与操作交给养成服务。</summary>
    public sealed class CharacterSceneController : MonoBehaviour
    {
        [SerializeField]
        private Transform modelAnchor;
        [SerializeField, Min(0.1f)]
        private float modelHeight = 3.4f;
        private GameObject model;
        public string DisplayedUnitId { get; private set; }

        private void Start()
        {
            PlayerRuntime.EnsureCreated();
            CharacterSceneNavigator.AdoptDirectScene(gameObject.scene);
            if (!PlayerRuntime.Instance.IsReady)
            {
                Debug.LogError(PlayerRuntime.Instance.LastMessage);
                CharacterSceneNavigator.Instance.Return();
                return;
            }

            var nav = CharacterSceneNavigator.Instance;
            UIManager.Instance.Open(UIId.CharacterDetail, new CharacterUIContext(this, nav.SelectedUnitId, nav.SelectedTab, nav.SelectedEquipmentId), ownerSceneHandle: gameObject.scene.handle);
        }

        public void Show(UnitConfig config)
        {
            if (config == null || config.id == DisplayedUnitId)
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
        }

        public void Rotate(float pixels)
        {
            if (model != null)
                model.transform.Rotate(Vector3.up, -pixels * 0.35f, Space.World);
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
