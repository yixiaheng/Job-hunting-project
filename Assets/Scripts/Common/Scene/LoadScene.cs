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

        if (!manager.TryLoad(sceneId, out string error, mode))
            Debug.LogError(error, this);
    }
}
