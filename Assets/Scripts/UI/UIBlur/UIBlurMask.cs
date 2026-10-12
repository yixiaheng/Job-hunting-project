using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class UIBlurMask : MonoBehaviour
{
    private RawImage rawImage;
    private bool registered;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    private void OnEnable()
    {
        TryRegister();
    }

    private void Start()
    {
        TryRegister();
    }

    private void TryRegister()
    {
        if (registered || UIBlurManager.Instance == null) return;
        UIBlurManager.Instance.Register(this);
        registered = true;
    }

    internal void Apply(RenderTexture texture, Color color)
    {
        if (rawImage == null) rawImage = GetComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.color = color;
    }

    private void OnDisable()
    {
        if (rawImage != null) rawImage.texture = null;
        if (registered && UIBlurManager.Instance != null)
            UIBlurManager.Instance.Unregister(this);
        registered = false;
    }

    private void OnDestroy()
    {
        if (registered && UIBlurManager.Instance != null)
            UIBlurManager.Instance.Unregister(this);
    }
}
