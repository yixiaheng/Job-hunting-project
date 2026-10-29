using System;
using UnityEngine;

/// <summary>
/// Carries one launch snapshot across a scene load without retaining scene objects.
/// </summary>
public static class BattleLaunchContext
{
    private static BattleLaunchData pending;

    public static void Set(BattleLaunchData data)
    {
        pending = data ?? throw new ArgumentNullException(nameof(data));
    }

    public static bool TryConsume(out BattleLaunchData data)
    {
        data = pending;
        pending = null;
        return data != null;
    }

    public static void Clear()
    {
        pending = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        Clear();
    }
}
