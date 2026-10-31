using System;
using UnityEngine;

public sealed class BattleSession
{
    public BattleLaunchData LaunchData { get; }
    public SceneId BattleScene { get; }
    public SceneId ExitScene { get; }
    public bool IsReforming { get; }

    internal BattleSession(BattleLaunchData data, SceneId battleScene,
        SceneId exitScene, bool isReforming = false)
    {
        LaunchData = data ?? throw new ArgumentNullException(nameof(data));
        BattleScene = battleScene;
        ExitScene = exitScene;
        IsReforming = isReforming;
    }
}

/// <summary>Retains the current stage until the player explicitly exits it.</summary>
public static class BattleSessionContext
{
    public static BattleSession Current { get; private set; }

    public static void Begin(BattleLaunchData data, SceneId battleScene, SceneId exitScene)
    {
        if (battleScene == SceneId.None || exitScene == SceneId.None
            || !Enum.IsDefined(typeof(SceneId), battleScene) || !Enum.IsDefined(typeof(SceneId), exitScene))
            throw new ArgumentException("Battle and exit scene IDs must be configured.");

        Current = new BattleSession(data, battleScene, exitScene);
    }

    public static void SetReforming(bool value)
    {
        if (Current == null)
            throw new InvalidOperationException("There is no active battle session.");

        Current = new BattleSession(Current.LaunchData,
            Current.BattleScene, Current.ExitScene, value);
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
