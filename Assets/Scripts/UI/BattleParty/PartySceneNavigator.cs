using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exploration party entry; post-battle reformation continues to use a root scene load.</summary>
public static class PartySceneNavigator
{
    public static bool TryEnter(SceneTransitionMode transition = SceneTransitionMode.Fade)
    {
        var scenes = GameSceneManager.Instance;
        if (scenes == null || BattleManager.Instance != null) return false;
        Game.Characters.PlayerRuntime.EnsureCreated();
        if (!Game.Characters.PlayerRuntime.Instance.IsReady)
        { Debug.LogError(Game.Characters.PlayerRuntime.Instance.LastMessage); return false; }
        var ui = UIManager.Instance;
        if (ui == null || ui.Registry == null || !ui.Registry.TryGet(UIId.BattleParty, out var config) || config.prefab == null)
        { Debug.LogError("配队界面尚未登记。"); return false; }
        var operation = scenes.EnterVisitAsync(SceneId.Party, new ScenePresentationLifecycle<PartySceneController>(), transition: transition);
        Observe(operation);
        return operation.Accepted;
    }
    public static void AdoptDirectScene(Scene scene)
    {
        if (GameSceneManager.Instance != null)
            Observe(GameSceneManager.Instance.AdoptDirectScene(scene, new ScenePresentationLifecycle<PartySceneController>()));
    }
    public static void Return()
    {
        var scenes = GameSceneManager.Instance;
        if (scenes == null || !scenes.TryGetVisit(SceneId.Party, out var handle)) return;
        var previous = BattleSessionContext.Current;
        var pending = BattleLaunchContext.Peek();
        if (previous != null && previous.IsReforming)
            Observe(scenes.ReplaceFromVisitAsync(handle, previous.ExitScene, new SceneLoadOptions
            {
                Prepare = () => { BattleLaunchContext.Clear(); BattleSessionContext.Clear(); },
                Rollback = () => { BattleLaunchContext.Restore(pending); BattleSessionContext.Restore(previous); }
            }));
        else Observe(scenes.ReturnAsync(handle));
    }
    internal static void Observe(SceneOperation operation)
        => operation.OnCompleted(result => { if (!result.Succeeded) Debug.LogError(result.Error); });
}
