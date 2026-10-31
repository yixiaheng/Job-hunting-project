using System;
using Battle.Events;
using Battle.SaveData;
using Game;
using Game.UI;
using UnityEngine;

public sealed class PartySceneController : MonoBehaviour
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
    private bool started;
    private bool isStartingBattle;
    private bool restoredReformation;

    private void Start()
    {
        started = true;
        OpenParty();
    }

    private void OnEnable()
    {
        if (started)
            OpenParty();
    }

    private void OpenParty()
    {
        uiManager = UIManager.Instance;
        if (partyManager == null || uiManager == null)
        {
            Debug.LogError("PartySceneController requires a scene party manager and the global UI runtime.", this);
            return;
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
        partyHandle = uiManager.Open(UIId.BattleParty, new BattlePartyUIContext(partyManager, StartBattle));
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

        if (!scenes.CanLoad(targetScene, out error))
        {
            Debug.LogError(error, this);
            return;
        }

        isStartingBattle = true;
        try
        {
            BattleLaunchContext.Set(data);
            BattleSessionContext.Begin(data, targetScene, destination);
            if (!scenes.TryLoad(targetScene, out error))
                throw new InvalidOperationException(error);
        }
        catch (Exception exception)
        {
            BattleLaunchContext.Clear();
            BattleSessionContext.Restore(previous);
            isStartingBattle = false;
            Debug.LogException(exception, this);
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
