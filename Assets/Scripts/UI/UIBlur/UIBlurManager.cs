using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class UIBlurManager : MonoBehaviour
{
    //单例模式
    public static UIBlurManager Instance { get; private set; }

    //所看相机
    [SerializeField] private Camera sourceCamera;
    //模糊材质
    [SerializeField] private Material blurMaterial;
    //采样率降低的倍数
    [SerializeField, Range(1, 8)] private int downSample = 4;
    //模糊强度
    [SerializeField, Range(0.1f, 10f)] private float blurSize = 1.5f;
    //重复模糊次数
    [SerializeField, Range(1, 4)] private int iterations = 2;
    //模糊颜色
    [SerializeField] private Color blurColor = new Color(0.72f, 0.78f, 0.9f, 0.82f);

    //记录所有需要模糊的图片
    private readonly List<UIBlurMask> masks = new List<UIBlurMask>();
    private RenderTexture blurTexture;
    private RenderTexture sourceTexture;
    private RenderTexture temporaryTexture;
    private int textureWidth;
    private int textureHeight;
    private Coroutine captureCoroutine;
    private bool captureQueued;                     //捕获已入队
    private bool hasCapturedCamera;                 //已经采集相机
    private Vector3 lastCapturedPosition;
    private Quaternion lastCapturedRotation;
    private float lastCapturedFieldOfView;
    private float lastCapturedAspect;
    private int lastCapturedScreenWidth;
    private int lastCapturedScreenHeight;

    public RenderTexture BlurTexture => blurTexture;
    public Color BlurColor => blurColor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// UIBulurMask直接在初始化时调用方法注册
    /// </summary>
    /// <param name="mask"></param>
    public void Register(UIBlurMask mask)
    {
        if (mask == null) return;
        if (!masks.Contains(mask)) masks.Add(mask);
        RequestRender();
    }
    /// <summary>
    /// 取消注册
    /// </summary>
    /// <param name="mask"></param>
    public void Unregister(UIBlurMask mask)
    {
        if (mask != null) masks.Remove(mask);
    }


    //渲染模糊
    public void RenderBlur()
    {
        RequestRender();
    }

    //LateUpdate 在Update之后调用
    private void LateUpdate()
    {
        if (masks.Count == 0) return;
        if (!hasCapturedCamera || CameraChanged())
            RequestRender();
    }

    //请求渲染
    private void RequestRender()
    {
        if (captureQueued || masks.Count == 0) return;
        captureCoroutine = StartCoroutine(RenderAtEndOfFrame());
    }

    private IEnumerator RenderAtEndOfFrame()
    {
        captureQueued = true;

        //等待当帧渲染完之后
        yield return new WaitForEndOfFrame();
        captureQueued = false;
        captureCoroutine = null;


        if (masks.Count > 0)
            RenderBlurNow();
    }

    /// <summary>
    /// 渲染模糊图片
    /// </summary>
    private void RenderBlurNow()
    {
        if (blurMaterial == null)
        {
            Debug.LogError("UIBlurManager 缺少高斯模糊材质");
            return;
        }

        if (sourceCamera == null) sourceCamera = Camera.main;
        if (sourceCamera == null)
        {
            Debug.LogError("UIBlurManager 找不到用于捕获背景的 Camera");
            return;
        }

        EnsureTextures();

        //暂存摄像机原本的位置
        RenderTexture previousTarget = sourceCamera.targetTexture;
        //记录摄像机原本是否启用
        bool previousEnabled = sourceCamera.enabled;
        //关闭自动渲染
        sourceCamera.enabled = false;
        //修改摄像机的输出目标
        sourceCamera.targetTexture = sourceTexture;
        //手动渲染一次
        sourceCamera.Render();      //本次渲染的输出目标是sourceTexture，也就是将这次的屏幕图形获取下来了

        //恢复
        sourceCamera.targetTexture = previousTarget;
        sourceCamera.enabled = previousEnabled;

        //给Material设置模糊强度
        blurMaterial.SetFloat("_Size", blurSize);
        //连续进行两次处理
        //0是横向模糊，1是纵向模糊
        Graphics.Blit(sourceTexture, temporaryTexture, blurMaterial, 0);
        Graphics.Blit(temporaryTexture, blurTexture, blurMaterial, 1);
        
        //重复模糊次数，循环
        for (int index = 1; index < iterations; index++)
        {
            Graphics.Blit(blurTexture, temporaryTexture, blurMaterial, 0);
            Graphics.Blit(temporaryTexture, blurTexture, blurMaterial, 1);
        }

        //记录本次捕获时的相机状态
        lastCapturedPosition = sourceCamera.transform.position;
        lastCapturedRotation = sourceCamera.transform.rotation;
        lastCapturedFieldOfView = sourceCamera.fieldOfView;
        lastCapturedAspect = sourceCamera.aspect;
        lastCapturedScreenWidth = Screen.width;
        lastCapturedScreenHeight = Screen.height;
        hasCapturedCamera = true;
        RefreshMasks();
    }

    private bool CameraChanged()
    {
        //在跟过去的相机状态比较，每次渲染过后会进行状态记录
        if (sourceCamera == null) return true;
        if ((sourceCamera.transform.position - lastCapturedPosition).sqrMagnitude > 0.000001f)
            return true;
        if (Quaternion.Angle(sourceCamera.transform.rotation, lastCapturedRotation) > 0.01f)
            return true;
        if (!Mathf.Approximately(sourceCamera.fieldOfView, lastCapturedFieldOfView))
            return true;
        if (!Mathf.Approximately(sourceCamera.aspect, lastCapturedAspect))
            return true;
        return Screen.width != lastCapturedScreenWidth || Screen.height != lastCapturedScreenHeight;
    }

    private void EnsureTextures()
    {
        //获取屏幕的高低
        int width = Mathf.Max(1, Screen.width / Mathf.Max(1, downSample));
        int height = Mathf.Max(1, Screen.height / Mathf.Max(1, downSample));

        if (blurTexture != null && textureWidth == width && textureHeight == height)
            return;

        ReleaseTextures();
        textureWidth = width;
        textureHeight = height;

        //给渲染图片赋值
        sourceTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "UIBlur_Source",
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        temporaryTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "UIBlur_Temporary",
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };
        blurTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "UIBlur_Result",
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false
        };

        //Create：实际渲染出来
        sourceTexture.Create();
        temporaryTexture.Create();
        blurTexture.Create();
    }

    private void RefreshMasks()
    {
        for (int index = masks.Count - 1; index >= 0; index--)
        {
            if (masks[index] == null)
            {
                masks.RemoveAt(index);
                continue;
            }

            masks[index].Apply(blurTexture, blurColor);
        }
    }

    private void ReleaseTextures()
    {
        ReleaseTexture(sourceTexture);
        ReleaseTexture(temporaryTexture);
        ReleaseTexture(blurTexture);
        sourceTexture = null;
        temporaryTexture = null;
        blurTexture = null;
    }

    private static void ReleaseTexture(RenderTexture texture)
    {
        if (texture == null) return;
        if (texture.IsCreated()) texture.Release();
        Destroy(texture);
    }

    private void OnDestroy()
    {
        if (captureCoroutine != null)
            StopCoroutine(captureCoroutine);
        if (Instance == this) Instance = null;
        ReleaseTextures();
    }
}
