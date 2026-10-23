using Battle.SaveData;
using TapTap21.SaveSystem.dyh;

/// <summary>战斗模块使用的存档定义。</summary>
public static class BattleSaveDefinitions
{
    public static readonly SaveDefinition<BattleFormation> Formation =
        SaveSystem.Register<BattleFormation>(
            "battle_formation",
            "battle_formation.json",
            1);
}
