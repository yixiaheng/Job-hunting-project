using System;
using UnityEngine;

public sealed class BattleSession
{
    public BattleLaunchData LaunchData { get; }
    public string BattleSceneName { get; }
    public string ExitSceneName { get; }
    public bool IsReforming { get; }

    internal BattleSession(BattleLaunchData data, string battleSceneName,
        string exitSceneName, bool isReforming = false)
    {
        LaunchData = data ?? throw new ArgumentNullException(nameof(data));
        BattleSceneName = battleSceneName;
        ExitSceneName = exitSceneName;
        IsReforming = isReforming;
    }
}

/// <summary>Retains the current stage until the player explicitly exits it.</summary>
public static class BattleSessionContext
{
    public static BattleSession Current { get; private set; }

    public static void Begin(BattleLaunchData data, string battleSceneName, string exitSceneName)
    {
        if (string.IsNullOrWhiteSpace(battleSceneName) || string.IsNullOrWhiteSpace(exitSceneName))
            throw new ArgumentException("Battle and exit scene names must be configured.");

        Current = new BattleSession(data, battleSceneName, exitSceneName);
    }

    public static void SetReforming(bool value)
    {
        if (Current == null)
            throw new InvalidOperationException("There is no active battle session.");

        Current = new BattleSession(Current.LaunchData,
            Current.BattleSceneName, Current.ExitSceneName, value);
    }

    internal static void Restore(BattleSession session)
    {
        Current = session;
    }

    public static void Clear()
    {
        Current = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        Clear();
    }
}
