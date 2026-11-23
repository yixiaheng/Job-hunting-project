using UnityEngine;

public sealed class HotUpdateStartupController : MonoBehaviour
{
    private bool loadingGame;

    private void Awake()
    {
        if (HotUpdateBootstrap.Instance != null)
            HotUpdateBootstrap.Instance.Ready += HandleReady;
    }

    private void Start()
    {
        if (HotUpdateBootstrap.Instance == null)
        {
            Debug.LogError("启动场景找不到 HotUpdateBootstrap。", this);
            return;
        }

        if (HotUpdateBootstrap.Instance.IsReady)
            HandleReady();
    }

    private void HandleReady()
    {
        if (loadingGame)
            return;

        GameSceneManager scenes = GameSceneManager.Instance;
        if (scenes == null)
        {
            Debug.LogError("启动场景找不到 GameSceneManager。", this);
            return;
        }

        loadingGame = true;
        SceneOperation operation = scenes.LoadAsync(SceneId.Sample);
        if (!operation.Accepted)
        {
            loadingGame = false;
            Debug.LogError("进入游戏场景失败：" + operation.Error, this);
        }
    }

    private void OnDestroy()
    {
        if (HotUpdateBootstrap.Instance != null)
            HotUpdateBootstrap.Instance.Ready -= HandleReady;
    }
}
