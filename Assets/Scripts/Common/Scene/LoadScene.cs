using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadScene : MonoBehaviour
{
    public SceneId sceneId;
    public LoadSceneMode mode;

    public void OnClick_LoadScene()
    {
        string name = GameSceneManager.SceneNames[(int)sceneId];
        if(mode == LoadSceneMode.Immediate)
            GameSceneManager.Instance.Load(name);
        else
            GameSceneManager.Instance.LoadSync(name, GameSceneManager.Instance.fadeDuration);
    }
}
