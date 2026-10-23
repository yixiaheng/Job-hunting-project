using UnityEngine;

/// <summary>
/// 放在场景中的对象身上，用于对象标识，战斗时检测，然后获取BattleUnit
/// </summary>
[DisallowMultipleComponent]
public class BattleUnitActor : MonoBehaviour
{
    [SerializeField] private Transform healthBarAnchor;
    [SerializeField] private Vector3 fallbackHealthBarOffset = new(0f, 2f, 0f);

    public BattleUnit Unit { get; private set; }
    private Vector3 cachedHealthBarOffset;

    public Vector3 HealthBarPosition => healthBarAnchor != null
        ? healthBarAnchor.position
        : transform.TransformPoint(cachedHealthBarOffset);

    public void Bind(BattleUnit u)
    {
        Unit = u;
        cachedHealthBarOffset = fallbackHealthBarOffset;
        if (healthBarAnchor != null)
            return;

        // 没有手动挂点的模型，只在绑定时计算一次包围盒顶部。
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        cachedHealthBarOffset = transform.InverseTransformPoint(
            new Vector3(bounds.center.x, bounds.max.y + 0.3f, bounds.center.z));
    }
}
