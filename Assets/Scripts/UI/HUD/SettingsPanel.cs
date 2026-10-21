using System;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : UIPanel
{
    [SerializeField] private Button closeButton;

    protected override void OnInitialize()
    {
        closeButton.onClick.AddListener(CloseSelf);
    }
    protected override void OnOpen(IUIContext context)
    {
        Debug.Log("设置界面打开，读取当前设置");
    }

    protected override void OnClosing()
    {
        Debug.Log("设置界面开始关闭");
    }

    private void CloseSelf()
    {
        Owner.Close(Handle);
    }

}
