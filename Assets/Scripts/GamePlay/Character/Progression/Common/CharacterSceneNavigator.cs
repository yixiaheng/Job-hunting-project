using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Characters
{
    /// <summary>校验养成入口并传递上下文；加载和暂停统一交给 GameSceneManager。</summary>
    public static class CharacterSceneNavigator
    {
        public const int PreviewMask = 1 << 8;
        public static bool IsActive => GameSceneManager.Instance != null
            && GameSceneManager.Instance.TryGetVisit(SceneId.Character, out _);

        public static bool TryEnter(string unitId = null, CharacterTab tab = CharacterTab.Attributes, string equipmentId = null)
        {
            var scenes = GameSceneManager.Instance;
            if (scenes == null || BattleManager.Instance != null)
                return false;

            var ui = UIManager.Instance;
            if (ui == null || ui.Registry == null
                || !ui.Registry.TryGet(UIId.CharacterDetail, out var config) || config.prefab == null)
            {
                Debug.LogError("角色界面尚未正确登记到 UIManager。");
                return false;
            }
            PlayerRuntime.EnsureCreated();
            if (!PlayerRuntime.Instance.IsReady)
            {
                Debug.LogError(PlayerRuntime.Instance.LastMessage);
                return false;
            }

            var context = new CharacterSceneContext(unitId, tab, equipmentId);
            var operation = scenes.EnterVisitAsync(SceneId.Character,
                new ScenePresentationLifecycle<CharacterSceneController>(PreviewMask), context);
            Observe(operation);
            return operation.Accepted;
        }

        internal static void AdoptDirectScene(Scene scene)
        {
            var scenes = GameSceneManager.Instance;
            if (scenes != null)
                Observe(scenes.AdoptDirectScene(scene,
                    new ScenePresentationLifecycle<CharacterSceneController>(PreviewMask)));
        }

        public static void Return()
        {
            var scenes = GameSceneManager.Instance;
            if (scenes != null && scenes.TryGetVisit(SceneId.Character, out var handle))
                Observe(scenes.ReturnAsync(handle));
        }

        private static void Observe(SceneOperation operation)
        {
            operation.OnCompleted(result =>
            {
                if (!result.Succeeded)
                    Debug.LogError(result.Error);
            });
        }
    }
}
