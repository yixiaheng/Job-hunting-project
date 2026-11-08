using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Characters
{
    /// <summary>Character business entry. Scene lifetime and source snapshots belong to GameSceneManager.</summary>
    public sealed class CharacterSceneNavigator : MonoBehaviour
    {
        public static CharacterSceneNavigator Instance { get; private set; }
        public static bool IsActive => GameSceneManager.Instance != null
            && GameSceneManager.Instance.TryGetVisit(SceneId.Character, out _);
        public string SelectedUnitId { get; private set; }
        public string SelectedEquipmentId { get; private set; }
        public CharacterTab SelectedTab { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() { Instance = null; }

        public static bool TryEnter(string unitId = null, CharacterTab tab = CharacterTab.Attributes, string equipmentId = null)
        {
            if (IsActive || BattleManager.Instance != null) return false;
            var ui = UIManager.Instance;
            if (ui == null || ui.Registry == null || !ui.Registry.TryGet(UIId.CharacterDetail, out var config) || config.prefab == null)
            { Debug.LogError("角色界面尚未登记到 UIManager。"); return false; }
            PlayerRuntime.EnsureCreated();
            if (!PlayerRuntime.Instance.IsReady) { Debug.LogError(PlayerRuntime.Instance.LastMessage); return false; }
            var scenes = GameSceneManager.Instance;
            if (scenes == null) return false;
            EnsureInstance();
            Instance.SelectedUnitId = unitId; Instance.SelectedTab = tab; Instance.SelectedEquipmentId = equipmentId;
            var context = new CharacterVisitContext(unitId, tab, equipmentId);
            var operation = scenes.EnterVisitAsync(SceneId.Character, new ScenePresentationLifecycle<CharacterSceneController>(), context);
            Observe(operation);
            return operation.Accepted;
        }
        internal static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(CharacterSceneNavigator));
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CharacterSceneNavigator>();
        }
        internal static void AdoptDirectScene(Scene scene)
        {
            EnsureInstance();
            if (GameSceneManager.Instance != null)
                Observe(GameSceneManager.Instance.AdoptDirectScene(scene, new ScenePresentationLifecycle<CharacterSceneController>()));
        }
        public void Return()
        {
            var scenes = GameSceneManager.Instance;
            if (scenes != null && scenes.TryGetVisit(SceneId.Character, out var handle)) Observe(scenes.ReturnAsync(handle));
        }
        internal static void Observe(SceneOperation operation)
        {
            operation.OnCompleted(result => { if (!result.Succeeded) Debug.LogError(result.Error); });
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }

    public sealed class CharacterVisitContext
    {
        public string UnitId { get; }
        public CharacterTab Tab { get; }
        public string EquipmentId { get; }
        public CharacterVisitContext(string unitId, CharacterTab tab, string equipmentId)
        { UnitId = unitId; Tab = tab; EquipmentId = equipmentId; }
    }
}
