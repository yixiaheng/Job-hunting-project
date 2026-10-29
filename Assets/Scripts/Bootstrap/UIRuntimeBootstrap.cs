using Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class UIRuntimeBootstrap
{
    private const string ConfigPath = "Bootstrap/UIBootstrapConfig";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (UIManager.Instance == null)
        {
            UIBootstrapConfig config = Resources.Load<UIBootstrapConfig>(ConfigPath);
            if (config == null || config.RuntimePrefab == null)
            {
                Debug.LogError("UI bootstrap requires Resources/Bootstrap/UIBootstrapConfig and a configured runtime prefab.");
                return;
            }

            UIManager runtime = Object.Instantiate(config.RuntimePrefab);
            runtime.name = config.RuntimePrefab.name;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureSingleEventSystem();
    }

    private static void HandleSceneLoaded(Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        EnsureSingleEventSystem();
    }

    private static void EnsureSingleEventSystem()
    {
        UIManager manager = UIManager.Instance;
        if (manager == null)
            return;

        EventSystem runtimeEventSystem = manager.GetComponentInChildren<EventSystem>(true);
        if (runtimeEventSystem == null)
        {
            Debug.LogError("The UI runtime prefab requires an EventSystem with an input module.", manager);
            return;
        }

        foreach (EventSystem candidate in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == runtimeEventSystem)
                continue;

            foreach (BaseInputModule module in candidate.GetComponents<BaseInputModule>())
                module.enabled = false;
            candidate.enabled = false;
        }

        runtimeEventSystem.gameObject.SetActive(true);
        runtimeEventSystem.enabled = true;
        EventSystem.current = runtimeEventSystem;
    }
}
