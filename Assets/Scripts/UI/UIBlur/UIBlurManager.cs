using System;
using System.Collections;
using System.Collections.Generic;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>由 UIManager 持有的背景模糊服务，负责相机捕获和纹理处理。</summary>
[DefaultExecutionOrder(10000)]
public class UIBlurManager : MonoBehaviour
{
    public static UIBlurManager Instance { get; private set; }
    public static event Action InstanceChanged;

    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Material blurMaterial;
    [SerializeField, Range(1, 8)] private int downSample = 4;
    [SerializeField, Range(0.1f, 10f)] private float blurSize = 1.5f;
    [SerializeField, Range(1, 4)] private int iterations = 2;
    [SerializeField] private Color blurColor = new Color(0.72f, 0.78f, 0.9f, 0.82f);

    private readonly List<UIBlurMask> masks = new List<UIBlurMask>();
    private UIBackgroundController backgroundController;
    private bool callbacksRegistered;
    private Camera capturedCamera;
    private RenderTexture blurTexture;
    private RenderTexture sourceTexture;
    private RenderTexture temporaryTexture;
    private int textureWidth;
    private int textureHeight;
    private Coroutine captureCoroutine;
    private bool captureQueued;
    private bool hasCapturedCamera;
    private Vector3 lastCapturedPosition;
    private Quaternion lastCapturedRotation;
    private float lastCapturedFieldOfView;
    private float lastCapturedAspect;
    private int lastCapturedScreenWidth;
    private int lastCapturedScreenHeight;

    public RenderTexture BlurTexture => blurTexture;
    public Color BlurColor => blurColor;

    private bool CanRender => isActiveAndEnabled && masks.Count > 0
        && (backgroundController == null || backgroundController.Current.ShouldBlur);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        Instance = null;
        InstanceChanged = null;
    }

    /// <summary>绑定模糊请求统计，允许 UIManager 在本组件 OnEnable 前调用。</summary>
    public void Initialize(UIBackgroundController controller)
    {
        Unsubscribe();
        backgroundController = controller;
        if (isActiveAndEnabled)
        {
            Subscribe();
            RefreshDemand();
        }
    }

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            // 服务挂在 UIManager 上，重复时只销毁服务组件。
            Destroy(this);
            return;
        }

        Instance = this;
        Subscribe();
        InstanceChanged?.Invoke();
        RefreshDemand();
    }

    private void Subscribe()
    {
        if (callbacksRegistered)
            return;
        if (backgroundController != null)
            backgroundController.StateChanged += HandleBackgroundChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        callbacksRegistered = true;
    }

    private void Unsubscribe()
    {
        if (!callbacksRegistered)
            return;
        if (backgroundController != null)
            backgroundController.StateChanged -= HandleBackgroundChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        callbacksRegistered = false;
    }

    public void Register(UIBlurMask mask)
    {
        if (mask == null || !mask.isActiveAndEnabled)
            return;
        if (!masks.Contains(mask))
            masks.Add(mask);
        if (CanRender && hasCapturedCamera && !CameraChanged())
            mask.Apply(blurTexture, blurColor);
        else
            mask.Clear();
        RequestRender();
    }

    public void Unregister(UIBlurMask mask)
    {
        if (mask != null)
        {
            masks.Remove(mask);
            mask.Clear();
        }
        if (masks.Count == 0)
            CancelCaptureAndRelease();
    }

    public void RenderBlur()
    {
        RequestRender();
    }

    private void HandleBackgroundChanged(UIBackgroundState state)
    {
        RefreshDemand();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InvalidateCapture();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        InvalidateCapture();
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        InvalidateCapture();
    }

    private void InvalidateCapture()
    {
        CancelCaptureAndRelease();
        RefreshDemand();
    }

    private void RefreshDemand()
    {
        if (CanRender)
            RequestRender();
        else
            CancelCaptureAndRelease();
    }

    private void LateUpdate()
    {
        masks.RemoveAll(mask => mask == null || !mask.isActiveAndEnabled);
        if (!CanRender)
        {
            if (captureQueued || blurTexture != null)
                CancelCaptureAndRelease();
            return;
        }
        if (capturedCamera != ResolveCamera())
            CancelCaptureAndRelease();
        if (!hasCapturedCamera || CameraChanged())
            RequestRender();
    }

    private Camera ResolveCamera()
    {
        // 不缓存 Camera.main，场景或主相机更换后使用新相机。
        return sourceCamera != null ? sourceCamera : Camera.main;
    }

    private void RequestRender()
    {
        if (captureQueued || !CanRender || blurMaterial == null)
            return;
        captureQueued = true;
        captureCoroutine = StartCoroutine(RenderAtEndOfFrame());
    }

    private IEnumerator RenderAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        captureQueued = false;
        captureCoroutine = null;
        if (CanRender)
            RenderBlurNow();
    }

    private void RenderBlurNow()
    {
        Camera camera = ResolveCamera();
        if (camera == null || blurMaterial == null)
            return;

        EnsureTextures();
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        bool previousEnabled = camera.enabled;
        try
        {
            camera.enabled = false;
            camera.targetTexture = sourceTexture;
            camera.Render();
        }
        finally
        {
            if (camera != null)
            {
                camera.targetTexture = previousTarget;
                camera.enabled = previousEnabled;
            }
            RenderTexture.active = previousActive;
        }

        try
        {
            // 保留横向、纵向两遍高斯模糊及原有迭代次数。
            blurMaterial.SetFloat("_Size", blurSize);
            Graphics.Blit(sourceTexture, temporaryTexture, blurMaterial, 0);
            Graphics.Blit(temporaryTexture, blurTexture, blurMaterial, 1);
            for (int index = 1; index < iterations; index++)
            {
                Graphics.Blit(blurTexture, temporaryTexture, blurMaterial, 0);
                Graphics.Blit(temporaryTexture, blurTexture, blurMaterial, 1);
            }
        }
        finally
        {
            RenderTexture.active = previousActive;
        }

        capturedCamera = camera;
        lastCapturedPosition = camera.transform.position;
        lastCapturedRotation = camera.transform.rotation;
        lastCapturedFieldOfView = camera.fieldOfView;
        lastCapturedAspect = camera.aspect;
        lastCapturedScreenWidth = Screen.width;
        lastCapturedScreenHeight = Screen.height;
        hasCapturedCamera = true;
        foreach (UIBlurMask mask in masks)
        {
            if (mask != null && mask.isActiveAndEnabled)
                mask.Apply(blurTexture, blurColor);
        }
    }

    private bool CameraChanged()
    {
        Camera camera = ResolveCamera();
        if (camera == null || camera != capturedCamera)
            return true;
        if ((camera.transform.position - lastCapturedPosition).sqrMagnitude > 0.000001f)
            return true;
        if (Quaternion.Angle(camera.transform.rotation, lastCapturedRotation) > 0.01f)
            return true;
        if (!Mathf.Approximately(camera.fieldOfView, lastCapturedFieldOfView))
            return true;
        if (!Mathf.Approximately(camera.aspect, lastCapturedAspect))
            return true;
        return Screen.width != lastCapturedScreenWidth || Screen.height != lastCapturedScreenHeight;
    }

    private void EnsureTextures()
    {
        int width = Mathf.Max(1, Screen.width / Mathf.Max(1, downSample));
        int height = Mathf.Max(1, Screen.height / Mathf.Max(1, downSample));
        if (blurTexture != null && textureWidth == width && textureHeight == height)
            return;

        ReleaseTextures();
        textureWidth = width;
        textureHeight = height;
        sourceTexture = CreateTexture("UIBlur_Source", width, height, 24);
        temporaryTexture = CreateTexture("UIBlur_Temporary", width, height, 0);
        blurTexture = CreateTexture("UIBlur_Result", width, height, 0);
    }

    private static RenderTexture CreateTexture(string textureName, int width, int height, int depth)
    {
        RenderTexture texture = new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        texture.Create();
        return texture;
    }

    private void CancelCaptureAndRelease()
    {
        if (captureCoroutine != null)
            StopCoroutine(captureCoroutine);
        captureCoroutine = null;
        captureQueued = false;
        hasCapturedCamera = false;
        capturedCamera = null;
        ReleaseTextures();
    }

    private void ReleaseTextures()
    {
        foreach (UIBlurMask mask in masks)
        {
            if (mask != null)
                mask.Clear();
        }
        ReleaseTexture(sourceTexture);
        ReleaseTexture(temporaryTexture);
        ReleaseTexture(blurTexture);
        sourceTexture = null;
        temporaryTexture = null;
        blurTexture = null;
    }

    private static void ReleaseTexture(RenderTexture texture)
    {
        if (texture == null)
            return;
        if (texture.IsCreated())
            texture.Release();
        Destroy(texture);
    }

    private void OnDisable()
    {
        Unsubscribe();
        CancelCaptureAndRelease();
        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
        CancelCaptureAndRelease();
        masks.Clear();
    }
}
