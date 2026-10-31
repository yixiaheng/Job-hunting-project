using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>统一的场景入口，任意场景启动时自动创建并跨场景保留。</summary>
public sealed class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [SerializeField, Min(0f)] private float fadeDuration = 0.4f;

    private SceneCatalog catalog;
    private CanvasGroup fade;
    private bool isLoading;
    private SceneTransitionMode loadingMode;
    private string loadingPath;

    public SceneCatalog Catalog => catalog;
    public bool IsLoading => isLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        if (Instance != null)
            SceneManager.sceneLoaded -= Instance.HandleSceneLoaded;
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameSceneManager existing = FindFirstObjectByType<GameSceneManager>();
        if (existing != null)
            existing.Initialize();
        else
            new GameObject(nameof(GameSceneManager)).AddComponent<GameSceneManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 不销毁重复组件所在的整棵场景层级。
            enabled = false;
            Destroy(this);
            return;
        }

        Initialize();
    }

    private void Initialize()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        catalog = Resources.Load<SceneCatalog>(SceneCatalog.ResourcePath);
        if (catalog == null)
            Debug.LogError("缺少 Resources/Bootstrap/SceneCatalog，无法加载场景。", this);

        EnsureFadeOverlay();
        ResetTransition();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public bool CanLoad(SceneId id, out string error)
    {
        if (!isActiveAndEnabled)
            error = "场景加载器尚未启用。";
        else if (isLoading)
            error = "场景正在加载，请勿重复切换。";
        else if (catalog == null || !catalog.TryGetPath(id, out string path))
            error = $"SceneCatalog 中未正确登记场景：{id}。";
        else if (!Application.CanStreamedLevelBeLoaded(path))
            error = $"场景 {id} 未加入当前 Build Profile 的启用列表：{path}";
        else
        {
            error = null;
            return true;
        }

        return false;
    }

    /// <summary>Immediate 同步提交加载；Fade 返回值仅代表已接受异步切换请求。</summary>
    public bool TryLoad(SceneId id, out string error,
        SceneTransitionMode mode = SceneTransitionMode.Immediate)
    {
        if (!Enum.IsDefined(typeof(SceneTransitionMode), mode))
        {
            error = $"未知的场景过渡模式：{mode}。";
            return false;
        }

        if (!CanLoad(id, out error))
            return false;

        catalog.TryGetPath(id, out loadingPath);
        loadingMode = mode;
        isLoading = true;
        try
        {
            if (mode == SceneTransitionMode.Fade)
                StartCoroutine(LoadFadeRoutine(loadingPath));
            else
                SceneManager.LoadScene(loadingPath, UnityEngine.SceneManagement.LoadSceneMode.Single);

            return true;
        }
        catch (Exception exception)
        {
            ResetTransition();
            error = $"加载场景 {id} 失败：{exception.Message}";
            return false;
        }
    }

    private IEnumerator LoadFadeRoutine(string path)
    {
        fade.blocksRaycasts = true;
        try
        {
            yield return FadeTo(1f);

            AsyncOperation operation = null;
            string error = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(path, UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }

            if (operation == null)
                Debug.LogError($"异步加载场景失败：{path}。{error}", this);
            else
            {
                while (!operation.isDone)
                    yield return null;
            }

            yield return FadeTo(0f);
        }
        finally
        {
            ResetTransition();
        }
    }

    private IEnumerator FadeTo(float target)
    {
        float start = fade.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        fade.alpha = target;
    }

    private void EnsureFadeOverlay()
    {
        if (fade != null)
            return;

        GameObject overlay = new GameObject("SceneTransition", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        overlay.layer = 5;
        overlay.transform.SetParent(transform, false);
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        fade = overlay.GetComponent<CanvasGroup>();
        fade.interactable = false;

        GameObject background = new GameObject("FadeBackground", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        background.layer = 5;
        background.transform.SetParent(overlay.transform, false);
        RectTransform rect = (RectTransform)background.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = background.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
    }

    private void HandleSceneLoaded(Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (loadingMode == SceneTransitionMode.Immediate
            && string.Equals(scene.path, loadingPath, StringComparison.OrdinalIgnoreCase))
            ResetTransition();
    }

    private void ResetTransition()
    {
        isLoading = false;
        loadingPath = null;
        if (fade != null)
        {
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
            Instance = null;
    }
}
