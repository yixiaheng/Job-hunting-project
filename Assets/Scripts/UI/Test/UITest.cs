using Game.UI;
using UnityEngine;
using Sirenix.OdinInspector;

public class UITest : MonoBehaviour
{
    private UIHandle settingsHandle;

    [Button]
    public void OpenSettings()
    {
        Debug.Log("UITest.OpenSettings 被调用", this);

        if (UIManager.Instance == null)
        {
            Debug.LogError("UITest 无法打开设置界面：UIManager.Instance 为空", this);
            return;
        }

        settingsHandle = UIManager.Instance.Open(UIId.Settings);

        if (!settingsHandle.IsValid)
        {
            Debug.LogError("UITest 无法打开设置界面：UIManager.Open 返回了无效句柄。请检查 UIRegistry 和 Settings UIConfig。", this);
            return;
        }

        Debug.Log($"设置界面打开请求已发送：{settingsHandle}", this);
    }

    [Button]
    public void CloseSettings()
    {
        Debug.Log("UITest.CloseSettings 被调用", this);

        if (UIManager.Instance == null)
        {
            Debug.LogError("UITest 无法关闭设置界面：UIManager.Instance 为空", this);
            return;
        }

        if (!settingsHandle.IsValid)
        {
            Debug.LogWarning("UITest 没有可关闭的设置界面句柄", this);
            return;
        }

        UIManager.Instance.Close(settingsHandle);
    }
}
