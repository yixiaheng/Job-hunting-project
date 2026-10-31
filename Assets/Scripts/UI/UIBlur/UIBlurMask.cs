using Game.UI;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class UIBlurMask : MonoBehaviour
{
    private RawImage rawImage;
    private UIBlurManager registeredService;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    private void OnEnable()
    {
        UIBlurManager.InstanceChanged += TryRegister;
        TryRegister();
    }

    private void Start()
    {
        TryRegister();
    }

    private void TryRegister()
    {
        if (!isActiveAndEnabled)
            return;

        UIManager owner = GetComponentInParent<UIManager>();
        UIBlurManager service = owner != null ? owner.Blur : UIBlurManager.Instance;
        if (service != null && !service.isActiveAndEnabled)
            service = null;
        if (registeredService == service && service != null)
            return;

        Unregister();
        registeredService = service;
        if (registeredService != null)
            registeredService.Register(this);
    }

    internal void Apply(RenderTexture texture, Color color)
    {
        if (rawImage == null)
            rawImage = GetComponent<RawImage>();
        if (rawImage == null)
            return;
        rawImage.texture = texture;
        rawImage.color = color;
        rawImage.enabled = texture != null;
    }

    internal void Clear()
    {
        if (rawImage == null)
            rawImage = GetComponent<RawImage>();
        if (rawImage == null)
            return;
        rawImage.texture = null;
        // 没有捕获结果时不绘制 RawImage 的默认白色纹理。
        rawImage.enabled = false;
    }

    private void Unregister()
    {
        if (registeredService != null)
            registeredService.Unregister(this);
        registeredService = null;
        Clear();
    }

    private void OnDisable()
    {
        UIBlurManager.InstanceChanged -= TryRegister;
        Unregister();
    }

    private void OnDestroy()
    {
        UIBlurManager.InstanceChanged -= TryRegister;
        Unregister();
    }
}
