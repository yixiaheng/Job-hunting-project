using System;
using Battle.Events;
using Battle.SaveData;
using Game;
using Game.UI;
using UnityEngine;

public sealed class PartySceneController : MonoBehaviour, ISceneEntryPoint
{
    [SerializeField] private BattlePartyManager partyManager;
    [SerializeField] private SceneId battleScene = SceneId.Battle;
    [SerializeField] private SceneId exitScene = SceneId.Sample;
    [SerializeField] private Transform[] alliesPosition;
    [SerializeField, Min(0.01f)] private float modelScale = 7f;

    private readonly GameObject[] models = new GameObject[BattleFormation.MaxMembers];
    private readonly UnitConfig[] shownConfigs = new UnitConfig[BattleFormation.MaxMembers];
    private IDisposable formationSubscription;
    private UIManager uiManager;
    private UIHandle partyHandle;
    private bool isStartingBattle;
    private bool restoredReformation;
    private SceneVisitHandle visit;
    public ISceneVisitLifecycle VisitLifecycle => new ScenePresentationLifecycle<PartySceneController>();

    private void Start()
    {
        if (GameSceneManager.Instance != null && GameSceneManager.Instance.IsScenePending(gameObject.scene)) return;
        PartySceneNavigator.AdoptDirectScene(gameObject.scene);
    }

    public System.Collections.IEnumerator InitializeScene(SceneVisitHandle handle, object context)
    {
        Game.Characters.PlayerRuntime.EnsureCreated();
        if (!Game.Characters.PlayerRuntime.Instance.IsReady)
            throw new InvalidOperationException(Game.Characters.PlayerRuntime.Instance.LastMessage);
        visit = handle;
        ConfigurePresentation();
        OpenParty();
        yield return ScenePresentationLifecycle<PartySceneController>.WaitForPanel(uiManager, partyHandle);
    }

    private void OpenParty()
    {
        uiManager = UIManager.Instance;
        if (partyManager == null || uiManager == null)
        {
            throw new InvalidOperationException("PartySceneController requires a scene party manager and the global UI runtime.");
        }

        formationSubscription?.Dispose();
        formationSubscription = GameEvents.Battle.Subscribe<PartyFormationChangedEvent>(HandleFormationChanged);
        BattleSession session = BattleSessionContext.Current;
        if (!restoredReformation && session != null && session.IsReforming)
        {
            restoredReformation = true;
            if (!partyManager.ApplySortedFormation(session.LaunchData.Allies))
                Debug.LogWarning("The previous battle party contains unavailable characters. Select a valid party to continue.", this);
        }
        RefreshFormation();
        partyHandle = uiManager.Open(UIId.BattleParty, new BattlePartyUIContext(partyManager, StartBattle, PartySceneNavigator.Return),
            ownerSceneHandle: gameObject.scene.handle);
    }

    public void StartBattle()
    {
        if (isStartingBattle || partyManager == null)
            return;

        if (!partyManager.TryCreateBattleLaunchData(out BattleLaunchData data, out string error))
        {
            Debug.LogWarning(error, this);
            return;
        }

        BattleSession previous = BattleSessionContext.Current;
        bool continuing = previous != null && previous.IsReforming;
        SceneId targetScene = continuing ? previous.BattleScene : battleScene;
        SceneId destination = continuing ? previous.ExitScene : exitScene;
        GameSceneManager scenes = GameSceneManager.Instance;
        if (scenes == null)
        {
            Debug.LogError("场景加载器尚未初始化。", this);
            return;
        }

        isStartingBattle = true;
        SetBusy(true);
        BattleLaunchData pending = BattleLaunchContext.Peek();
        var options = new SceneLoadOptions
        {
            Prepare = () =>
            {
                BattleLaunchContext.Set(data);
                BattleSessionContext.Begin(data, targetScene, destination);
            },
            Rollback = () => { BattleLaunchContext.Restore(pending); BattleSessionContext.Restore(previous); }
        };
        var operation = visit.IsValid ? scenes.ReplaceFromVisitAsync(visit, targetScene, options) : scenes.LoadAsync(targetScene, options);
        operation.OnCompleted(result =>
        {
            if (result.Succeeded) return;
            if (this != null) { isStartingBattle = false; SetBusy(false); }
            Debug.LogError(result.Error);
        });
    }

    private void SetBusy(bool value)
    {
        if (uiManager != null && uiManager.TryGetPanel<BattlePartyCanvasUI>(partyHandle, out var panel)) panel.SetBusy(value);
    }

    private void ConfigurePresentation()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 9;
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.cullingMask = (1 << 9) | (1 << 5);
                camera.clearFlags = CameraClearFlags.SolidColor;
            }
            foreach (var light in root.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << 9;
        }
    }

    private void HandleFormationChanged(PartyFormationChangedEvent message)
    {
        if (message.Manager == partyManager)
            RefreshFormation();
    }

    private void RefreshFormation()
    {
        if (partyManager == null)
            return;

        BattleFormation formation = partyManager.Formation;
        for (int position = 0; position < BattleFormation.MaxMembers; position++)
        {
            FormationMember member = formation.GetMemberAtPosition(position);
            UnitConfig config = member != null ? partyManager.FindConfig(member.unitId) : null;
            if (shownConfigs[position] == config && (config == null || models[position] != null))
                continue;

            DestroyModel(position);
            if (config == null || config.prefab == null || alliesPosition == null
                || position >= alliesPosition.Length || alliesPosition[position] == null)
                continue;

            models[position] = Instantiate(config.prefab, alliesPosition[position], false);
            models[position].transform.localPosition = Vector3.zero;
            models[position].transform.localRotation = Quaternion.identity;
            models[position].transform.localScale *= modelScale;
            foreach (var child in models[position].GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 9;
            foreach (var animator in models[position].GetComponentsInChildren<Animator>(true)) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            foreach (var collider in models[position].GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            shownConfigs[position] = config;
        }
    }

    private void DestroyModel(int position)
    {
        if (models[position] != null)
        {
            models[position].SetActive(false);
            Destroy(models[position]);
        }

        models[position] = null;
        shownConfigs[position] = null;
    }

    private void OnDisable()
    {
        formationSubscription?.Dispose();
        formationSubscription = null;
        if (uiManager != null && partyHandle.IsValid)
            uiManager.Close(partyHandle);

        partyHandle = default;
        uiManager = null;
        for (int i = 0; i < models.Length; i++)
            DestroyModel(i);
    }
}
