using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public sealed class BattleCameraShot
{
    public string name = "Shot";
    public Vector3 position;
    public Vector3 eulerAngles;
    [Min(1f)] public float fieldOfView = 60f;
    [Min(0f)] public float blendDuration = 0.35f;
}

/// <summary>
/// Drives simple battle camera poses. Combat presentation can request these shots
/// now and replace the implementation with Cinemachine later.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class BattleCameraController : MonoBehaviour
{
    [SerializeField] private int defaultShotIndex;
    [SerializeField] private bool enableDebugInput = true;
    [SerializeField] private List<BattleCameraShot> shots = new();

    private Camera controlledCamera;
    private Coroutine blendCoroutine;

    public int ShotCount => shots.Count;
    public int CurrentShotIndex { get; private set; } = -1;
    public string CurrentShotName => IsValidShot(CurrentShotIndex)
        ? shots[CurrentShotIndex].name : string.Empty;
    public bool IsBlending => blendCoroutine != null;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        CutToDefault();
    }

    private void Update()
    {
        if (!enableDebugInput)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f1Key.wasPressedThisFrame) BlendTo(0);
        else if (keyboard.f2Key.wasPressedThisFrame) BlendTo(1);
        else if (keyboard.f3Key.wasPressedThisFrame) BlendTo(2);
        else if (keyboard.f4Key.wasPressedThisFrame) BlendTo(3);
    }

    private void OnDisable()
    {
        StopBlend();
    }

    public void ConfigureDefaultShots(IReadOnlyList<Transform> allyAnchors,
        IReadOnlyList<Transform> enemyAnchors)
    {
        if (shots.Count > 0)
            return;

        bool hasAllies = TryGetCenter(allyAnchors, out Vector3 allyCenter);
        bool hasEnemies = TryGetCenter(enemyAnchors, out Vector3 enemyCenter);
        if (!hasAllies && !hasEnemies)
            return;
        if (!hasAllies)
            allyCenter = enemyCenter - Vector3.forward * 5f;
        if (!hasEnemies)
            enemyCenter = allyCenter + Vector3.forward * 5f;

        Vector3 battleDirection = enemyCenter - allyCenter;
        battleDirection.y = 0f;
        float battleSpan = Mathf.Max(5f, battleDirection.magnitude);
        battleDirection = battleDirection.sqrMagnitude > 0.001f
            ? battleDirection.normalized : Vector3.forward;
        Vector3 sideDirection = Vector3.Cross(Vector3.up, battleDirection).normalized;
        Vector3 focus = Vector3.Lerp(allyCenter, enemyCenter, 0.5f) + Vector3.up * 1.25f;
        float sideOffset = battleSpan * 0.6f;
        float backOffset = battleSpan * 0.65f;
        float shotHeight = Mathf.Max(2.6f, battleSpan * 0.55f);

        shots.Add(CreateShot("Overview", transform.position, transform.rotation,
            controlledCamera.fieldOfView, 0.35f));

        Vector3 allyPosition = allyCenter - battleDirection * backOffset
            - sideDirection * sideOffset + Vector3.up * shotHeight;
        shots.Add(CreateLookAtShot("AllyAngle", allyPosition, focus, 55f, 0.45f));

        Vector3 enemyPosition = enemyCenter + battleDirection * backOffset
            + sideDirection * sideOffset + Vector3.up * shotHeight;
        shots.Add(CreateLookAtShot("EnemyAngle", enemyPosition, focus, 55f, 0.45f));

        Vector3 highPosition = focus - battleDirection * battleSpan * 0.55f
            - sideDirection * battleSpan * 0.1f + Vector3.up * Mathf.Max(6f, battleSpan * 1.2f);
        shots.Add(CreateLookAtShot("HighAngle", highPosition, focus, 58f, 0.55f));
        defaultShotIndex = 0;
    }

    public bool CutToDefault() => CutTo(defaultShotIndex);

    public bool BlendToDefault() => BlendTo(defaultShotIndex);

    public bool CutTo(int shotIndex)
    {
        if (!TryGetShot(shotIndex, out BattleCameraShot shot))
            return false;

        StopBlend();
        ApplyShot(shot);
        CurrentShotIndex = shotIndex;
        return true;
    }

    public bool BlendTo(int shotIndex)
    {
        if (!TryGetShot(shotIndex, out BattleCameraShot shot))
            return false;

        StopBlend();
        if (!isActiveAndEnabled || shot.blendDuration <= 0f)
            return CutTo(shotIndex);

        blendCoroutine = StartCoroutine(BlendToRoutine(shotIndex, shot));
        return true;
    }

    public bool BlendTo(string shotName)
    {
        if (string.IsNullOrWhiteSpace(shotName))
            return false;

        for (int index = 0; index < shots.Count; index++)
        {
            if (string.Equals(shots[index]?.name, shotName,
                StringComparison.OrdinalIgnoreCase))
                return BlendTo(index);
        }

        return false;
    }

    public bool NextShot()
    {
        if (shots.Count == 0)
            return false;
        int next = CurrentShotIndex < 0 ? defaultShotIndex : (CurrentShotIndex + 1) % shots.Count;
        return BlendTo(next);
    }

    public bool PreviousShot()
    {
        if (shots.Count == 0)
            return false;
        int previous = CurrentShotIndex < 0
            ? defaultShotIndex
            : (CurrentShotIndex - 1 + shots.Count) % shots.Count;
        return BlendTo(previous);
    }

    private IEnumerator BlendToRoutine(int shotIndex, BattleCameraShot shot)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float startFieldOfView = controlledCamera.fieldOfView;
        Quaternion targetRotation = Quaternion.Euler(shot.eulerAngles);
        float duration = Mathf.Max(0.01f, shot.blendDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(startPosition, shot.position, eased),
                Quaternion.SlerpUnclamped(startRotation, targetRotation, eased));
            controlledCamera.fieldOfView = Mathf.LerpUnclamped(
                startFieldOfView, shot.fieldOfView, eased);
            yield return null;
        }

        ApplyShot(shot);
        CurrentShotIndex = shotIndex;
        blendCoroutine = null;
    }

    private void ApplyShot(BattleCameraShot shot)
    {
        transform.SetPositionAndRotation(shot.position, Quaternion.Euler(shot.eulerAngles));
        controlledCamera.fieldOfView = shot.fieldOfView;
    }

    private bool TryGetShot(int shotIndex, out BattleCameraShot shot)
    {
        shot = IsValidShot(shotIndex) ? shots[shotIndex] : null;
        return shot != null;
    }

    private bool IsValidShot(int shotIndex) => shotIndex >= 0 && shotIndex < shots.Count;

    private static BattleCameraShot CreateLookAtShot(string shotName, Vector3 position,
        Vector3 focus, float fieldOfView, float blendDuration)
    {
        Quaternion rotation = Quaternion.LookRotation(focus - position, Vector3.up);
        return CreateShot(shotName, position, rotation, fieldOfView, blendDuration);
    }

    private static BattleCameraShot CreateShot(string shotName, Vector3 position,
        Quaternion rotation, float fieldOfView, float blendDuration)
    {
        return new BattleCameraShot
        {
            name = shotName,
            position = position,
            eulerAngles = rotation.eulerAngles,
            fieldOfView = fieldOfView,
            blendDuration = blendDuration
        };
    }

    private static bool TryGetCenter(IReadOnlyList<Transform> anchors, out Vector3 center)
    {
        center = Vector3.zero;
        int count = 0;
        if (anchors != null)
        {
            for (int index = 0; index < anchors.Count; index++)
            {
                Transform anchor = anchors[index];
                if (anchor == null)
                    continue;
                center += anchor.position;
                count++;
            }
        }

        if (count == 0)
            return false;
        center /= count;
        return true;
    }

    private void StopBlend()
    {
        if (blendCoroutine == null)
            return;
        StopCoroutine(blendCoroutine);
        blendCoroutine = null;
    }

    private void OnValidate()
    {
        if (shots == null)
            shots = new List<BattleCameraShot>();
        defaultShotIndex = shots.Count == 0
            ? 0 : Mathf.Clamp(defaultShotIndex, 0, shots.Count - 1);
        foreach (BattleCameraShot shot in shots)
        {
            if (shot == null)
                continue;
            shot.fieldOfView = Mathf.Clamp(shot.fieldOfView, 1f, 179f);
            shot.blendDuration = Mathf.Max(0f, shot.blendDuration);
        }
    }
}
