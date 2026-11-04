using System;
using System.Collections;
using System.Collections.Generic;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Characters
{
    /// <summary>养成展示的叠加导航会话，不关闭来源业务组件和窗口。</summary>
    public sealed class CharacterSceneNavigator : MonoBehaviour
    {
        public static CharacterSceneNavigator Instance { get; private set; }
        public static bool IsActive => Instance != null && Instance.active;
        public string SelectedUnitId { get; private set; }
        public string SelectedEquipmentId { get; private set; }
        public CharacterTab SelectedTab { get; private set; }

        private bool active;
        private bool busy;
        private Scene source;
        private Scene characterScene;
        private float originalTime;
        private IDisposable uiLease;
        private readonly List<(Behaviour component, bool enabled)> disabled = new();
        private readonly List<(PlayerInputHandler player, bool inputEnabled)> players = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            Instance = null;
        }

        public static bool TryEnter(string unitId = null, CharacterTab tab = CharacterTab.Attributes, string equipmentId = null)
        {
            if (IsActive || BattleManager.Instance != null)
                return false;
            if (UIManager.Instance == null || UIManager.Instance.Registry == null || !UIManager.Instance.Registry.TryGet(UIId.CharacterDetail, out var uiConfig) || uiConfig.prefab == null)
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

            var loader = GameSceneManager.Instance;
            if (loader == null || !loader.CanLoad(SceneId.Character, out string error))
            {
                Debug.LogError("无法进入角色界面，请检查场景登记。");
                return false;
            }

            EnsureInstance();
            Instance.SelectedUnitId = unitId;
            Instance.SelectedEquipmentId = equipmentId;
            Instance.SelectedTab = tab;
            Instance.StartCoroutine(Instance.EnterRoutine());
            return true;
        }

        private static void EnsureInstance()
        {
            if (Instance != null)
                return;
            var go = new GameObject(nameof(CharacterSceneNavigator));
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CharacterSceneNavigator>();
        }

        internal static void AdoptDirectScene(Scene scene)
        {
            EnsureInstance();
            if (Instance.active)
                return;
            Instance.originalTime = Time.timeScale;
            Instance.active = true;
            Instance.characterScene = scene;
            Time.timeScale = 0f;
        }

        private IEnumerator EnterRoutine()
        {
            active = busy = true;
            source = SceneManager.GetActiveScene();
            originalTime = Time.timeScale;
            Time.timeScale = 0f;
            uiLease = UIManager.Instance?.SuspendPresentation();
            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Hold(camera);
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                Hold(listener);
            // PlayerInputHandler owns camera input restrictions and preserves the original enabled state.
            // Do not also disable the axis here: that would be captured as its restore state.
            foreach (PlayerInputHandler player in FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None))
            {
                players.Add((player, player.inputActions.asset.enabled));
                player.SetInputRestriction(this, true, true, false);
                player.inputActions.Disable();
            }

            bool success = false;
            try
            {
                GameSceneManager.Instance.Catalog.TryGetPath(SceneId.Character, out string path);
                AsyncOperation loading = null;
                try
                {
                    loading = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                if (loading == null)
                    yield break;
                yield return loading;
                characterScene = SceneManager.GetSceneByPath(path);
                if (!characterScene.IsValid() || !characterScene.isLoaded)
                    yield break;
                bool hasController = false;
                foreach (var root in characterScene.GetRootGameObjects())
                    hasController |= root.GetComponentInChildren<CharacterSceneController>() != null;
                if (!hasController)
                {
                    Debug.LogError("养成场景缺少展示控制器，已取消进入。");
                    yield return SceneManager.UnloadSceneAsync(characterScene);
                    yield break;
                }

                SceneManager.SetActiveScene(characterScene);
                success = true;
            }
            finally
            {
                busy = false;
                if (!success)
                    Restore();
            }
        }

        private void Hold(Behaviour component)
        {
            disabled.Add((component, component.enabled));
            component.enabled = false;
        }

        public void Return()
        {
            if (!active || busy)
                return;
            StartCoroutine(ReturnRoutine());
        }

        private IEnumerator ReturnRoutine()
        {
            busy = true;
            try
            {
                if (source.IsValid() && source.isLoaded)
                    SceneManager.SetActiveScene(source);
                if (characterScene.IsValid() && characterScene.isLoaded)
                {
                    UIManager.Instance?.ReleaseSceneUI(characterScene.handle);
                    if (source.IsValid() && source.isLoaded)
                        yield return SceneManager.UnloadSceneAsync(characterScene);
                }
            }
            finally
            {
                Restore();
            }

            if (!source.IsValid() || !source.isLoaded)
                if (!GameSceneManager.Instance.TryLoad(SceneId.Sample, out string error))
                    Debug.LogError(error);
        }

        private void Restore()
        {
            foreach (var item in disabled)
                if (item.component != null)
                    item.component.enabled = item.enabled;
            disabled.Clear();
            foreach (var item in players)
                if (item.player != null)
                {
                    item.player.ClearInputRestriction(this);
                    if (item.inputEnabled && item.player.isActiveAndEnabled)
                        item.player.inputActions.Enable();
                }

            players.Clear();
            Time.timeScale = originalTime;
            uiLease?.Dispose();
            uiLease = null;
            active = busy = false;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;
            if (active)
                Restore();
            Instance = null;
        }
    }
}
