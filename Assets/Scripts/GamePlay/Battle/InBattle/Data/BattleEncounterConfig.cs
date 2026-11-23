using System.Collections.Generic;
using Game.Inventory;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleEncounter", menuName = "Battle/Encounter")]
public sealed class BattleEncounterConfig : ScriptableObject
{
    public string id;
    [Min(0)] public int initialSkillPoints = 4;
    [Min(0)] public int maximumSkillPoints = 5;
    public EnemyWaves waves = new();
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("defaultFormationPreset")] private string formationPreset;
    public string DefaultFormationPreset => formationPreset;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("cameraProfile")] private Battle.Presentation.BattleCameraProfile presentationCamera;
    public Battle.Presentation.BattleCameraProfile CameraProfile => presentationCamera;
    public List<ItemAmount> victoryRewards = new();
}
