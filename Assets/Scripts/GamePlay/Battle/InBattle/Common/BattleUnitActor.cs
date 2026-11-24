using UnityEngine;

/// <summary>
/// 放在场景中的对象身上，用于对象标识，战斗时检测，然后获取BattleUnit
/// </summary>
[DisallowMultipleComponent]
public class BattleUnitActor : MonoBehaviour
{
    [SerializeField] private Transform healthBarAnchor;
    [SerializeField] private Vector3 fallbackHealthBarOffset = new(0f, 2f, 0f);
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform cameraFocusAnchor;
    [SerializeField] private Battle.Presentation.BattleCameraProfile cameraProfile;
    [SerializeField] private Transform presentationRoot;
    [SerializeField] private bool automaticFramingBounds = true;
    [SerializeField] private Vector3 framingCenter = new(0, 1, 0);
    [SerializeField] private Vector3 framingSize = new(1, 2, 1);
    public Transform PresentationRoot => presentationRoot != null ? presentationRoot : transform;
    public Battle.Presentation.BattleActorPresentation Presentation => GetComponent<Battle.Presentation.BattleActorPresentation>();
    public bool WorldPresentationVisible { get; private set; } = true;
    public void SetWorldPresentationVisible(bool visible) => WorldPresentationVisible = visible;
    public Vector3 FocusPosition => cameraFocusAnchor != null ? cameraFocusAnchor.position : PresentationRoot.TransformPoint(framingCenter);
    public Bounds FramingBounds
    {
        get
        {
            var basis = PresentationRoot.localToWorldMatrix;
            var extents = framingSize * .5f;
            var x = basis.MultiplyVector(new Vector3(extents.x,0,0));
            var y = basis.MultiplyVector(new Vector3(0,extents.y,0));
            var z = basis.MultiplyVector(new Vector3(0,0,extents.z));
            return new Bounds(basis.MultiplyPoint3x4(framingCenter), new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x), Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z))*2);
        }
    }
    public void ConfigurePresentationRoot(Transform root, Transform model) { presentationRoot = root; visualRoot = model; }

    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public Transform CameraFocusAnchor => cameraFocusAnchor;
    public Battle.Presentation.BattleCameraProfile CameraProfile => cameraProfile;

    public BattleUnit Unit { get; private set; }
    private Vector3 cachedHealthBarOffset;

    public Vector3 HealthBarPosition => healthBarAnchor != null
        ? healthBarAnchor.position
        : transform.TransformPoint(cachedHealthBarOffset);

    public void Bind(BattleUnit u)
    {
        Unit = u;
        cachedHealthBarOffset = fallbackHealthBarOffset;
        if (u == null)
            return;

        // Primitive placeholders can also demonstrate Timeline motion. Real rigs keep their Animator.
        if (VisualRoot.GetComponentInChildren<Animator>(true) == null)
            VisualRoot.gameObject.AddComponent<Animator>();



        // 没有手动挂点的模型，只在绑定时计算一次包围盒顶部。
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        if (automaticFramingBounds)
        {
        framingCenter = PresentationRoot.InverseTransformPoint(bounds.center);
        var scale = PresentationRoot.lossyScale;
        framingSize = new Vector3(bounds.size.x/Mathf.Max(.001f,Mathf.Abs(scale.x)), bounds.size.y/Mathf.Max(.001f,Mathf.Abs(scale.y)), bounds.size.z/Mathf.Max(.001f,Mathf.Abs(scale.z)));
        }
        cachedHealthBarOffset = transform.InverseTransformPoint(
            new Vector3(bounds.center.x, bounds.max.y + 0.3f, bounds.center.z));
    }
}
