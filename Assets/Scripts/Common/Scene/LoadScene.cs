using UnityEngine;

public class LoadScene : MonoBehaviour
{
    public SceneId sceneId;
    public SceneTransitionMode mode;

    public void OnClick_LoadScene()
    {
        GameSceneManager manager = GameSceneManager.Instance;
        if (manager == null)
        {
            Debug.LogError("场景加载器尚未初始化。", this);
            return;
        }

        var operation = manager.LoadAsync(sceneId, new SceneLoadOptions { Transition = mode });
        operation.OnCompleted(result => { if (!result.Succeeded) Debug.LogError(result.Error); });
    }
}
