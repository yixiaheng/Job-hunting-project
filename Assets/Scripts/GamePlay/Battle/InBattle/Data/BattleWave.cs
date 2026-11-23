// 一波敌人的配置；逻辑单位在进入该波时由 BattleUnitFactory 创建。
[System.Serializable]
public class BattleWave
{
    public UnitConfig[] enemies;
    [UnityEngine.SerializeField, UnityEngine.Serialization.FormerlySerializedAs("formationPreset")] private string formation;
    public string FormationPreset => formation;
}
