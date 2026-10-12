using System.Collections;
using System.Security.Cryptography;
using DG.Tweening;
using Unity.Loading;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneId
{
    Sample,
    Battle
}
public enum LoadSceneMode
{
    Immediate,
    Fade
}

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance{get; private set;}

    //SceneName的顺序应该和SceneId一一对应
    public static readonly string[] SceneNames =
    {
        "SampleScene",
        "BattleScene",
    };
    public float fadeDuration = 0.4f;
    public CanvasGroup fade;
    private bool isLoading;

    void Awake() 
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        fade.alpha = 0;
        fade.interactable = false;
        fade.blocksRaycasts = false;
    }

    /// <summary>
    /// 直接加载场景
    /// </summary>
    /// <param name="sceneName"></param>
    public void Load(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
    /// <summary>
    /// 异步加载场景
    /// </summary>
    /// <param name="sceneName">场景名字</param>
    /// <param name="duration">渐入渐出，默认值直接访问变量fadeDurations</param>
    public void LoadSync(string sceneName, float duration)
    {
        LoadFade(sceneName, duration);
    }

    void LoadFade(string sceneName, float duration)
    {
        if(isLoading)
        {
            Debug.Log("场景正在加载中,请勿多次尝试");
            return;
        }
        if(duration < 0f) duration = fadeDuration;
        StartCoroutine(LoadFadeRoutine(sceneName, duration));
    }

    IEnumerator LoadFadeRoutine(string sceneName, float duration)
    {
        isLoading = true;

        fade.blocksRaycasts = true;
        yield return fade.DOFade(1f, duration).SetUpdate(true).WaitForCompletion();

        var op = SceneManager.LoadSceneAsync(sceneName);
        while(op != null && !op.isDone)
            yield return null;

        yield return fade.DOFade(0f, duration).SetUpdate(true).WaitForCompletion();
        fade.blocksRaycasts = false;

        isLoading = false;
    }

}
